#!/usr/bin/env python3
"""Type 2 job (decision 0020): a Monte Carlo research simulation, the 2D Ising model.

Each task simulates one (temperature, seed) point of a phase-transition scan with Metropolis
sweeps on an L x L periodic lattice (checkerboard update, J = 1, k_B = 1). This is a textbook
statistical-physics workload (Newman & Barkema, "Monte Carlo Methods in Statistical Physics",
1999, ch. 3): many independent, seeded, CPU-bound tasks, as in research parameter sweeps.
The exact critical temperature T_c = 2 / ln(1 + sqrt(2)) ≈ 2.269 (Onsager 1944) lets anyone
check the results.

  prepare   write the task queue to $WORK/in (one JSON file per task)
  run       process every task that has no result yet; PASSES=0 repeats forever
            with NODE_ID set: live-demo node with its own queue (see _run_node, decision 0042)
  calibrate print the sweeps for one task of TARGET_SECONDS on this CPU (live-demo task size)

Contract with the host (same shape as the transcription job, docs/jobs/transcription.md):
  $WORK/in/task_NNNN.json          input queue
  $WORK/out/pass_NNN/task_NNNN.json  one result per task; a task counts as done once it exists
  $WORK/out/log.csv                pass, task, start_unix_ns, end_unix_ns
  $WORK/progress.json              work_unit, units_done, tasks_done, tasks_total, pass
Results are written atomically per task, so a freeze (docker pause) loses nothing and a kill
loses at most the tasks in flight, which are redone on the next start (checkpoint at task
boundaries).
"""
from __future__ import annotations

import json
import math
import os
import sys
import time
from multiprocessing import Pool, Process
from pathlib import Path
import random

WORK = Path(os.environ.get("WORK", "/work"))
PASSES = int(os.environ.get("PASSES", "1"))
WORKERS = int(os.environ.get("WORKERS", "0")) or os.cpu_count() or 1

# Task grid (env-overridable). Defaults give tasks of a few seconds on a laptop core.
LATTICE_SIZE = int(os.environ.get("LATTICE_SIZE", "32"))
SWEEPS = int(os.environ.get("SWEEPS", "10000"))
THERMALISATION_SWEEPS = int(os.environ.get("THERMALISATION_SWEEPS", "2000"))
T_MIN = float(os.environ.get("T_MIN", "1.5"))
T_MAX = float(os.environ.get("T_MAX", "3.5"))
N_TEMPERATURES = int(os.environ.get("N_TEMPERATURES", "16"))
N_SEEDS = int(os.environ.get("N_SEEDS", "2"))
BASE_SEED = int(os.environ.get("BASE_SEED", "20251115"))
NODE_ID = os.environ.get("NODE_ID", "local")
LIVE_LOOP = os.environ.get("LIVE_LOOP", "0").lower() in {"1", "true", "yes"}

WORK_UNIT = "simulation task"
ONSAGER_TC = 2.0 / math.log(1.0 + math.sqrt(2.0))


def simulate(task: dict, progress=None) -> dict:
    """Metropolis checkerboard simulation of one task; returns per-spin observables.

    Energy per spin e = -(1/N) sum_<ij> s_i s_j; |m| = |sum s_i| / N. Susceptibility
    chi = N (<m^2> - <|m|>^2) / T and specific heat c = N (<e^2> - <e>^2) / T^2
    (Newman & Barkema 1999, eqs. 1.36 and 3.15).
    """
    size, temp = task["lattice_size"], task["temperature"]
    rng = random.Random(task["seed"])
    n = size * size
    spins = [rng.choice((-1, 1)) for _ in range(n)]
    # Periodic neighbours of site i = r * size + c: right, left, down, up.
    right = [r * size + (c + 1) % size for r in range(size) for c in range(size)]
    left = [r * size + (c - 1) % size for r in range(size) for c in range(size)]
    down = [((r + 1) % size) * size + c for r in range(size) for c in range(size)]
    up = [((r - 1) % size) * size + c for r in range(size) for c in range(size)]
    # Checkerboard order: all even sites, then all odd sites (one sweep = n updates).
    order = ([i for i in range(n) if (i // size + i % size) % 2 == 0]
             + [i for i in range(n) if (i // size + i % size) % 2 == 1])
    # Only dE = 4 and 8 can be rejected; acceptance probabilities exp(-dE/T).
    accept = {4: math.exp(-4.0 / temp), 8: math.exp(-8.0 / temp)}
    m_abs, m2, e1, e2 = [], [], [], []
    total_sweeps = task["thermalisation_sweeps"] + task["sweeps"]
    report_every = max(1, total_sweeps // 50)
    for sweep in range(total_sweeps):
        if progress is not None and sweep % report_every == 0:
            progress(sweep / total_sweeps)
        for i in order:
            d_e = 2 * spins[i] * (spins[right[i]] + spins[left[i]] + spins[down[i]] + spins[up[i]])
            if d_e <= 0 or rng.random() < accept[d_e]:
                spins[i] = -spins[i]
        if sweep >= task["thermalisation_sweeps"]:
            e = -sum(spins[i] * (spins[right[i]] + spins[down[i]]) for i in range(n)) / n
            m = abs(sum(spins)) / n
            m_abs.append(m); m2.append(m * m); e1.append(e); e2.append(e * e)
    mean = lambda xs: sum(xs) / len(xs)  # noqa: E731
    return {
        **task,
        "mean_abs_magnetisation": mean(m_abs),
        "mean_energy_per_spin": mean(e1),
        "susceptibility": n * (mean(m2) - mean(m_abs) ** 2) / temp,
        "specific_heat": n * (mean(e2) - mean(e1) ** 2) / temp ** 2,
    }


def prepare() -> None:
    inbox = WORK / "in"
    inbox.mkdir(parents=True, exist_ok=True)
    for old in inbox.glob("task_*.json"):
        old.unlink()
    step = (T_MAX - T_MIN) / (N_TEMPERATURES - 1) if N_TEMPERATURES > 1 else 0.0
    temps = [T_MIN + k * step for k in range(N_TEMPERATURES)]
    k = 0
    for t in temps:
        for s in range(N_SEEDS):
            task = {"id": f"task_{k:04d}", "temperature": round(float(t), 6),
                    "seed": BASE_SEED + k, "lattice_size": LATTICE_SIZE, "sweeps": SWEEPS,
                    "thermalisation_sweeps": THERMALISATION_SWEEPS}
            (inbox / f"{task['id']}.json").write_text(json.dumps(task))
            k += 1
    print(f"prepared {k} tasks in {inbox} (L={LATTICE_SIZE}, sweeps={SWEEPS}, "
          f"T={T_MIN}..{T_MAX}, Onsager T_c={ONSAGER_TC:.4f})")


def _work(args: tuple[dict, str]) -> tuple[str, int, int]:
    task, out_dir = args
    start = time.time_ns()
    result = simulate(task)
    end = time.time_ns()
    result["runtime_s"] = (end - start) / 1e9
    target = Path(out_dir) / f"{task['id']}.json"
    tmp = target.with_suffix(".part")
    tmp.write_text(json.dumps(result))
    os.replace(tmp, target)
    return task["id"], start, end


def _calculate(task: dict, progress=None) -> tuple[dict, int, int]:
    start = time.time_ns()
    result = simulate(task, progress)
    end = time.time_ns()
    result["runtime_s"] = (end - start) / 1e9
    return result, start, end


def write_progress(pass_no: int, done: int, total: int, units_done: int) -> None:
    tmp = WORK / f"progress.{NODE_ID}.{os.getpid()}.tmp"
    tmp.write_text(json.dumps({"work_unit": WORK_UNIT, "units_done": units_done,
                               "pass": pass_no, "tasks_done": done, "tasks_total": total}) + "\n")
    os.replace(tmp, WORK / "progress.json")


def run() -> None:
    if NODE_ID != "local":
        # One process per server slot (SLOTS, live demo: servers per house); claims are atomic renames.
        slots = int(os.environ.get("SLOTS", "1"))
        if slots == 1:
            _run_node(WORK / "out", 0)
            return
        procs = [Process(target=_run_node, args=(WORK / "out", k)) for k in range(slots)]
        for p in procs:
            p.start()
        for p in procs:
            p.join()
        return
    tasks = [json.loads(p.read_text()) for p in sorted((WORK / "in").glob("task_*.json"))]
    if not tasks:
        sys.exit(f"no tasks in {WORK / 'in'}; run prepare first")
    out_root = WORK / "out"
    out_root.mkdir(parents=True, exist_ok=True)
    log = out_root / "log.csv"
    if not log.exists():
        log.write_text("pass,task,start_unix_ns,end_unix_ns\n")
    # Keep the original single-container runner semantics unchanged.
    units_done = 0
    pass_no = 0
    with Pool(WORKERS) as pool:
        while PASSES == 0 or pass_no < PASSES:
            out = out_root / f"pass_{pass_no:03d}"
            out.mkdir(exist_ok=True)
            for stale in out.glob("*.part"):
                stale.unlink()
            pending = [t for t in tasks if not (out / f"{t['id']}.json").exists()]
            done = len(tasks) - len(pending)
            units_done += done
            write_progress(pass_no, done, len(tasks), units_done)
            for task_id, start, end in pool.imap_unordered(_work, [(t, str(out)) for t in pending]):
                with log.open("a") as f:
                    f.write(f"{pass_no},{task_id},{start},{end}\n")
                done += 1
                units_done += 1
                write_progress(pass_no, done, len(tasks), units_done)
            pass_no += 1


def _run_node(out_root: Path, slot: int = 0) -> None:
    """Live-demo node (decision 0042): claim only from this node's own queue, one task at a time.

    $WORK/queue/<node>/<task>.json    waiting; the coordinator may move it to another node's queue
    $WORK/running/<node>/s<slot>/<task>.json  claimed by one server slot of this node (atomic rename; never moved)
    $WORK/done/<task>.json            result, written atomically; then the running file is removed
    $WORK/out/<node>.csv              task, job, start_unix_ns, end_unix_ns, node_id
    A rename that fails because the file is gone means the coordinator moved it first: skip it.
    Earliest deadline first (field due_step), then by file name.
    """
    queue = WORK / "queue" / NODE_ID
    running = WORK / "running" / NODE_ID / f"s{slot}"
    done = WORK / "done"
    for d in (queue, running, done, out_root):
        d.mkdir(parents=True, exist_ok=True)
    node_log = out_root / f"{NODE_ID}.csv"
    if not node_log.exists():
        node_log.write_text("task,job,start_unix_ns,end_unix_ns,node_id\n")
    # A task left in running/ by a killed worker is still ours: finish it first.
    while True:
        claimed = sorted(running.glob("*.json"))
        if not claimed:
            claimed = _claim_next(queue, running)
        if claimed is None or not claimed:
            if not LIVE_LOOP:
                return
            time.sleep(0.2)
            continue
        path = claimed[0]
        try:
            task = json.loads(path.read_text())
        except (OSError, json.JSONDecodeError):
            time.sleep(0.2)
            continue
        result, start, end = _calculate(task, lambda f, name=path.name: _write_progress(name, f))
        _clear_progress(path.name)
        result["node_id"] = NODE_ID
        target = done / path.name
        tmp = done / f".{path.name}.{NODE_ID}.s{slot}.part"
        tmp.write_text(json.dumps(result))
        for attempt in range(50):  # the host may hold done/ or running/ files open for a moment (Windows)
            try:
                os.replace(tmp, target)
                path.unlink(missing_ok=True)
                break
            except OSError:
                time.sleep(0.1)
        try:
            with node_log.open("a") as f:
                f.write(f"{task['id']},{task.get('job', '')},{start},{end},{NODE_ID}\n")
        except OSError:
            pass


def _write_progress(task_name: str, fraction: float) -> None:
    """$WORK/progress/<node>/<task>.json: share of the running task's sweeps done (the view shows it)."""
    try:  # best effort: on Windows bind mounts the host may hold the file open for a moment
        d = WORK / "progress" / NODE_ID
        d.mkdir(parents=True, exist_ok=True)
        tmp = d / f".{task_name}.tmp"
        tmp.write_text(json.dumps({"task": task_name.removesuffix(".json"), "fraction": round(fraction, 3)}))
        os.replace(tmp, d / task_name)
    except OSError:
        pass


def _clear_progress(task_name: str) -> None:
    try:
        (WORK / "progress" / NODE_ID / task_name).unlink(missing_ok=True)
    except OSError:
        pass


def _claim_next(queue: Path, running: Path) -> list[Path] | None:
    """Claim the most urgent waiting task by atomic rename; None if the queue is empty."""
    waiting = []
    for p in queue.glob("*.json"):
        try:
            waiting.append((json.loads(p.read_text()).get("due_step", 0), p.name, p))
        except (OSError, json.JSONDecodeError):
            continue  # moved away or still being written by the coordinator
    for _, name, p in sorted(waiting):
        try:
            os.rename(p, running / name)
            return [running / name]
        except OSError:
            continue  # the coordinator moved it first, or it is locked for a moment
    return None


def calibrate() -> None:
    """Print the sweep count for which one task takes TARGET_SECONDS on this CPU (live-demo setting)."""
    target = float(os.environ.get("TARGET_SECONDS", "4"))
    probe = {"id": "calibrate", "temperature": 2.3, "seed": BASE_SEED, "lattice_size": LATTICE_SIZE,
             "sweeps": 2000, "thermalisation_sweeps": 200}
    _, start, end = _calculate(probe)
    seconds = (end - start) / 1e9
    sweeps = max(10, round(probe["sweeps"] * target / seconds))
    print(json.dumps({"lattice_size": LATTICE_SIZE, "probe_sweeps": 2200, "probe_seconds": round(seconds, 3),
                      "target_seconds": target, "sweeps": sweeps, "thermalisation_sweeps": sweeps // 10}))


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "run"
    {"prepare": prepare, "run": run, "calibrate": calibrate}.get(
        cmd, lambda: sys.exit("usage: ising.py prepare | run | calibrate"))()
