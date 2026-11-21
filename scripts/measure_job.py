#!/usr/bin/env python3
"""Measure runtime and energy of a containerised DigitalHeat job on this machine.

Two meters, sampled once per second (``SAMPLE_INTERVAL_S``) during an idle baseline and every run:

* CPU package energy from the Linux powercap/RAPL counter
  (``/sys/class/powercap/intel-rapl:0/energy_uj``; on AMD Zen the kernel exposes the package
  counter under the same name). Package only: no RAM, display, SSD or power-supply losses.
* Whole-laptop power from the battery (``power_now``, µW). Only valid while discharging.

Reported per run: gross energy (all electricity the machine used, which all ends up as heat) and
net energy (gross minus idle power × runtime, i.e. what the job adds). Energy per work unit is
divided by the work units the job reports in ``progress.json`` (contract in
docs/jobs/transcription.md). The counter is root-only by default; make it readable once with
``sudo chmod o+r /sys/class/powercap/intel-rapl:0/energy_uj`` (resets on reboot).

Subcommands (one documented command each, see docs/jobs/transcription.md):

  energy   idle baseline, then --reps runs of the job, each until it exits
  pause    one long-running job; freeze it with ``docker pause`` for a while, then resume

Standard library only. Output goes to --out as CSV/JSON files.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import platform
import shutil
import statistics
import subprocess
import threading
import time
from pathlib import Path

RAPL_DIR = Path("/sys/class/powercap/intel-rapl:0")
SAMPLE_INTERVAL_S = 1.0
MICRO = 1e-6
J_PER_KWH = 3.6e6
NS_PER_S = 1e9


def find_battery() -> Path | None:
    for p in sorted(Path("/sys/class/power_supply").glob("BAT*")):
        if (p / "power_now").exists():
            return p
    return None


def read_int(path: Path) -> int | None:
    try:
        return int(path.read_text().strip())
    except (OSError, ValueError):
        return None


def read_text(path: Path) -> str:
    try:
        return path.read_text().strip()
    except OSError:
        return ""


class Sampler(threading.Thread):
    """Samples RAPL energy and battery power every SAMPLE_INTERVAL_S seconds with a phase label."""

    def __init__(self) -> None:
        super().__init__(daemon=True)
        self.battery = find_battery()
        self.max_range_uj = read_int(RAPL_DIR / "max_energy_range_uj") or 0
        self.rows: list[dict] = []
        self.phase = "start"
        self._stop = threading.Event()

    def sample(self) -> dict:
        row = {
            "t_unix_s": time.time(),
            "phase": self.phase,
            "rapl_energy_uj": read_int(RAPL_DIR / "energy_uj"),
            "battery_power_uw": read_int(self.battery / "power_now") if self.battery else None,
            "battery_status": read_text(self.battery / "status") if self.battery else "",
        }
        self.rows.append(row)
        return row

    def run(self) -> None:
        next_t = time.monotonic()
        while not self._stop.is_set():
            self.sample()
            next_t += SAMPLE_INTERVAL_S
            self._stop.wait(max(0.0, next_t - time.monotonic()))

    def stop(self) -> None:
        self._stop.set()
        self.join()
        self.sample()

    def rapl_delta_j(self, a: int, b: int) -> float:
        """Energy between two counter readings in J, handling one counter wrap-around."""
        d = b - a
        if d < 0:
            d += self.max_range_uj
        return d * MICRO


def summarise_phase(s: Sampler, phase: str) -> dict:
    rows = [r for r in s.rows if r["phase"] == phase]
    if len(rows) < 2:
        raise RuntimeError(f"too few samples in phase {phase}")
    dur = rows[-1]["t_unix_s"] - rows[0]["t_unix_s"]
    rapl = [r["rapl_energy_uj"] for r in rows]
    rapl_j = sum(s.rapl_delta_j(a, b) for a, b in zip(rapl, rapl[1:])) if None not in rapl else None
    bat = [r["battery_power_uw"] for r in rows if r["battery_power_uw"] is not None]
    discharging = all(r["battery_status"] == "Discharging" for r in rows)
    bat_w = statistics.fmean(bat) * MICRO if bat and discharging else None
    return {"duration_s": dur, "rapl_j": rapl_j, "rapl_w": rapl_j / dur if rapl_j is not None else None,
            "battery_w": bat_w, "battery_valid": bool(bat) and discharging}


def docker(*args: str, check: bool = True) -> str:
    return subprocess.run(["docker", *args], check=check, capture_output=True, text=True).stdout.strip()


def docker_run_args(a: argparse.Namespace, detach: bool, env: dict[str, str]) -> list[str]:
    # Run as the calling user so the host can delete the outputs between runs.
    args = ["run", "--rm", "--user", f"{os.getuid()}:{os.getgid()}"] + (["-d"] if detach else []) + ["--cpus", str(a.cpus)]
    if a.name:
        args += ["--name", a.name]
    for m in a.mount:
        args += ["-v", m]
    for k, v in {**dict(e.split("=", 1) for e in a.env), **env}.items():
        args += ["-e", f"{k}={v}"]
    return args + [a.image, *a.cmd]


def units_done(progress_file: Path) -> tuple[float, str]:
    p = json.loads(progress_file.read_text())
    return float(p["units_done"]), p["work_unit"]


def machine_info(a: argparse.Namespace) -> dict:
    cpu = ""
    for line in read_text(Path("/proc/cpuinfo")).splitlines():
        if line.startswith("model name"):
            cpu = line.split(":", 1)[1].strip()
            break
    labels = json.loads(docker("image", "inspect", "--format", "{{json .Config.Labels}}", a.image) or "{}")
    return {
        "cpu_model": cpu, "logical_cpus": os.cpu_count(), "kernel": platform.release(),
        "os": read_text(Path("/etc/os-release")).split("PRETTY_NAME=")[-1].split("\n")[0].strip('"'),
        "scaling_governor": read_text(Path("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor")),
        "platform_profile": read_text(Path("/sys/firmware/acpi/platform_profile")),
        "rapl_domain": read_text(RAPL_DIR / "name"),
        "image": a.image, "image_id": docker("image", "inspect", "--format", "{{.Id}}", a.image),
        "image_labels": labels, "docker_cpus": a.cpus, "env": a.env, "cmd": a.cmd,
        "started_local": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
    }


def write_csv(path: Path, rows: list[dict]) -> None:
    with path.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)


def check_meters(s: Sampler, require_battery: bool) -> None:
    if read_int(RAPL_DIR / "energy_uj") is None:
        raise SystemExit(f"cannot read {RAPL_DIR}/energy_uj; run: sudo chmod o+r {RAPL_DIR}/energy_uj")
    if require_battery and (not s.battery or read_text(s.battery / "status") != "Discharging"):
        raise SystemExit("battery meter needs the charger unplugged (status must be Discharging); "
                         "use --no-battery to measure with the CPU counter only")


def reset_outputs(a: argparse.Namespace) -> None:
    for d in a.clear:
        if d.exists():
            shutil.rmtree(d)  # fail loudly: leftover outputs would make the queue skip work
    if a.progress_file.exists():
        a.progress_file.unlink()


def cmd_energy(a: argparse.Namespace) -> None:
    s = Sampler()
    check_meters(s, not a.no_battery)
    a.out.mkdir(parents=True, exist_ok=True)
    info = machine_info(a)
    s.start()
    s.phase = "idle"
    time.sleep(a.idle_seconds)
    idle = summarise_phase(s, "idle")
    runs = []
    for rep in range(a.reps):
        reset_outputs(a)
        s.phase = "cooldown"
        time.sleep(a.cooldown_seconds)
        s.phase = f"run_{rep}"
        t0 = time.time_ns()
        subprocess.run(["docker", *docker_run_args(a, False, {})], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        t1 = time.time_ns()
        ph = summarise_phase(s, f"run_{rep}")
        units, unit = units_done(a.progress_file)
        wall = (t1 - t0) / NS_PER_S
        row = {"rep": rep, "wall_s": round(wall, 3), "work_units": units, "work_unit": unit,
               "rapl_gross_kwh": ph["rapl_j"] / J_PER_KWH,
               "rapl_net_kwh": (ph["rapl_j"] - idle["rapl_w"] * wall) / J_PER_KWH,
               "battery_gross_kwh": ph["battery_w"] * wall / J_PER_KWH if ph["battery_valid"] else None,
               "battery_net_kwh": (ph["battery_w"] - idle["battery_w"]) * wall / J_PER_KWH
               if ph["battery_valid"] and idle["battery_valid"] else None,
               "rapl_mean_w": ph["rapl_w"], "battery_mean_w": ph["battery_w"]}
        runs.append(row)
        print(f"rep {rep}: {wall:.1f} s for {units:.2f} {unit}, CPU package {ph['rapl_w']:.1f} W, "
              f"battery {ph['battery_w'] if ph['battery_w'] is not None else float('nan'):.1f} W")
    s.stop()

    def stats(key: str) -> dict | None:
        vals = [r[key] / r["work_units"] for r in runs if r[key] is not None]
        if not vals:
            return None
        return {"mean": statistics.fmean(vals), "stdev": statistics.stdev(vals) if len(vals) > 1 else 0.0,
                "min": min(vals), "max": max(vals), "n": len(vals)}

    summary = {
        "machine": info, "idle": idle, "reps": a.reps, "work_unit": runs[0]["work_unit"],
        "work_units_per_run": runs[0]["work_units"],
        "runtime_s_per_unit": stats("wall_s"),
        "rapl_gross_kwh_per_unit": stats("rapl_gross_kwh"), "rapl_net_kwh_per_unit": stats("rapl_net_kwh"),
        "battery_gross_kwh_per_unit": stats("battery_gross_kwh"),
        "battery_net_kwh_per_unit": stats("battery_net_kwh"),
        "mean_rapl_w": statistics.fmean(r["rapl_mean_w"] for r in runs),
        "mean_battery_w": statistics.fmean(r["battery_mean_w"] for r in runs) if runs[0]["battery_mean_w"] else None,
    }
    write_csv(a.out / "runs.csv", runs)
    write_csv(a.out / "samples.csv", s.rows)
    (a.out / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(f"wrote {a.out}/runs.csv, samples.csv, summary.json")


def cmd_pause(a: argparse.Namespace) -> None:
    s = Sampler()
    check_meters(s, not a.no_battery)
    a.out.mkdir(parents=True, exist_ok=True)
    if not a.name:
        raise SystemExit("--name is required for pause")
    info = machine_info(a)
    reset_outputs(a)
    s.start()
    s.phase = "idle"
    time.sleep(a.idle_seconds)
    cid = docker(*docker_run_args(a, True, {}))
    events = []
    try:
        s.phase = "running"
        time.sleep(a.pause_at)
        docker("pause", cid)
        events.append({"t_unix_s": time.time(), "event": "pause", "units_done": units_done(a.progress_file)[0]})
        s.phase = "paused"
        time.sleep(a.pause_seconds)
        events.append({"t_unix_s": time.time(), "event": "unpause", "units_done": units_done(a.progress_file)[0]})
        docker("unpause", cid)
        s.phase = "resumed"
        time.sleep(a.after_seconds)
    finally:
        docker("rm", "-f", cid, check=False)
        s.stop()
    phases = {p: summarise_phase(s, p) for p in ("idle", "running", "paused", "resumed")}
    write_csv(a.out / "samples.csv", s.rows)
    write_csv(a.out / "events.csv", events)
    (a.out / "summary.json").write_text(json.dumps({"machine": info, "phases": phases}, indent=2) + "\n")
    for p, v in phases.items():
        print(f"{p:8s} CPU package {v['rapl_w']:.1f} W, battery "
              f"{v['battery_w'] if v['battery_w'] is not None else float('nan'):.1f} W")
    print(f"wrote {a.out}/samples.csv, events.csv, summary.json")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="command", required=True)
    for name in ("energy", "pause"):
        p = sub.add_parser(name)
        p.add_argument("--image", required=True)
        p.add_argument("--name", default="")
        p.add_argument("--mount", action="append", default=[], help="docker -v value, repeatable")
        p.add_argument("--env", action="append", default=[], help="KEY=VALUE, repeatable")
        p.add_argument("--cpus", type=float, required=True, help="docker --cpus allowance")
        p.add_argument("--progress-file", type=Path, required=True, help="host path of progress.json")
        p.add_argument("--clear", type=Path, action="append", default=[], help="host dir to delete before a run")
        p.add_argument("--idle-seconds", type=float, default=60)
        p.add_argument("--no-battery", action="store_true")
        p.add_argument("--out", type=Path, required=True)
        p.add_argument("cmd", nargs="*", default=["run"])
    sub.choices["energy"].add_argument("--reps", type=int, default=5)
    sub.choices["energy"].add_argument("--cooldown-seconds", type=float, default=10)
    sub.choices["pause"].add_argument("--pause-at", type=float, default=30)
    sub.choices["pause"].add_argument("--pause-seconds", type=float, default=30)
    sub.choices["pause"].add_argument("--after-seconds", type=float, default=30)
    a = ap.parse_args()
    {"energy": cmd_energy, "pause": cmd_pause}[a.command](a)


if __name__ == "__main__":
    main()
