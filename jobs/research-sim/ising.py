#!/usr/bin/env python3
"""Type 2 job (decision 0020): a Monte Carlo research simulation, the 2D Ising model.

Each task simulates one (temperature, seed) point of a phase-transition scan with Metropolis
sweeps on an L x L periodic lattice (checkerboard update, J = 1, k_B = 1). This is a textbook
statistical-physics workload (Newman & Barkema, "Monte Carlo Methods in Statistical Physics",
1999, ch. 3): many independent, seeded, CPU-bound tasks, as in research parameter sweeps.
The exact critical temperature T_c = 2 / ln(1 + sqrt(2)) ≈ 2.269 (Onsager 1944) lets anyone
check the results.

  prepare   write the task queue to $WORK/in (one JSON file per task)
  run       process every task that has no result yet; PASSES=0 repeats forever (live demo)

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
from multiprocessing import Pool
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

WORK_UNIT = "simulation task"
ONSAGER_TC = 2.0 / math.log(1.0 + math.sqrt(2.0))


def simulate(task: dict) -> dict:
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
    for sweep in range(task["thermalisation_sweeps"] + task["sweeps"]):
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


def write_progress(pass_no: int, done: int, total: int, units_done: int) -> None:
    tmp = WORK / "progress.json.tmp"
    tmp.write_text(json.dumps({"work_unit": WORK_UNIT, "units_done": units_done,
                               "pass": pass_no, "tasks_done": done, "tasks_total": total}) + "\n")
    os.replace(tmp, WORK / "progress.json")


def run() -> None:
    tasks = [json.loads(p.read_text()) for p in sorted((WORK / "in").glob("task_*.json"))]
    if not tasks:
        sys.exit(f"no tasks in {WORK / 'in'}; run prepare first")
    out_root = WORK / "out"
    out_root.mkdir(parents=True, exist_ok=True)
    log = out_root / "log.csv"
    if not log.exists():
        log.write_text("pass,task,start_unix_ns,end_unix_ns\n")
    units_done = 0
    pass_no = 0
    with Pool(WORKERS) as pool:
        while PASSES == 0 or pass_no < PASSES:
            out = out_root / f"pass_{pass_no:03d}"
            out.mkdir(exist_ok=True)
            for stale in out.glob("*.part"):
                stale.unlink()  # unfinished task from an earlier kill: redo it
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


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "run"
    {"prepare": prepare, "run": run}.get(cmd, lambda: sys.exit("usage: ising.py prepare | run"))()
