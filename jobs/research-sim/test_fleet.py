#!/usr/bin/env python3
"""Integration check of the live-demo queues (decision 0042): four node workers, each claiming only
from its own queue, while a "coordinator" keeps moving waiting files between queues. Every task must
produce exactly one result, nothing may stay in queue/ or running/, and each result must come from a
node whose queue held the task when it was claimed. Run: python3 jobs/research-sim/test_fleet.py"""
import json
import os
import random
import subprocess
import sys
import tempfile
import time
from pathlib import Path

SCRIPT = Path(__file__).with_name("ising.py")
NODES = [f"node-{i}" for i in range(1, 5)]
TASKS = 24


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="digitalheat-live-queues-") as temp:
        work = Path(temp)
        rng = random.Random(20261121)
        for k in range(TASKS):
            node = NODES[k % 2]  # all work starts on two nodes, the mover spreads it
            q = work / "queue" / node
            q.mkdir(parents=True, exist_ok=True)
            task = {"id": f"j0001-t{k:02d}", "job": 1, "due_step": k, "temperature": 2.0 + k * 0.02,
                    "seed": 1000 + k, "lattice_size": 10, "sweeps": 60, "thermalisation_sweeps": 6}
            (q / f"{task['id']}.json").write_text(json.dumps(task))
        env = os.environ | {"WORK": str(work), "LIVE_LOOP": "1"}
        workers = [subprocess.Popen([sys.executable, str(SCRIPT), "run"], env=env | {"NODE_ID": n}) for n in NODES]
        moved = lost_races = 0
        deadline = time.time() + 60
        try:
            while len(list((work / "done").glob("*.json"))) < TASKS and time.time() < deadline:
                waiting = sorted((work / "queue").glob("*/*.json"))
                if waiting:
                    src = rng.choice(waiting)
                    dst = work / "queue" / rng.choice(NODES) / src.name
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    try:
                        os.rename(src, dst)
                        moved += 1
                    except FileNotFoundError:
                        lost_races += 1  # a worker claimed it first: the coordinator skips it
                time.sleep(0.01)
        finally:
            for w in workers:
                w.kill()
                w.wait()
        results = {p.stem: json.loads(p.read_text()) for p in (work / "done").glob("*.json")}
        leftovers = list((work / "queue").glob("*/*.json")) + list((work / "running").glob("*/*.json"))
        logged = [line.split(",")[0] for n in NODES if (work / "out" / f"{n}.csv").exists()
                  for line in (work / "out" / f"{n}.csv").read_text().splitlines()[1:]]
        if len(results) != TASKS or leftovers or sorted(logged) != sorted(results):
            raise SystemExit(f"FAIL: {len(results)} results, {len(leftovers)} leftovers, {len(logged)} log rows")
        by_node = {n: sum(r["node_id"] == n for r in results.values()) for n in NODES}
        print(f"PASS: {TASKS} tasks, each done once; {moved} moves, {lost_races} lost races; per node {by_node}")


if __name__ == "__main__":
    main()
