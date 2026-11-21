#!/usr/bin/env python3
"""Small shared-volume integration check for four independent Ising worker processes."""
import csv
import os
import subprocess
import sys
import tempfile
from pathlib import Path


SCRIPT = Path(__file__).with_name("ising.py")


def run(args: list[str], env: dict[str, str]) -> None:
    subprocess.run([sys.executable, str(SCRIPT), *args], check=True, env=env, timeout=60)


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="digitalheat-four-node-") as temp:
        work = Path(temp) / "work"
        base = os.environ.copy()
        base.update({"WORK": str(work), "LATTICE_SIZE": "12", "SWEEPS": "80",
                     "THERMALISATION_SWEEPS": "8", "N_TEMPERATURES": "16", "N_SEEDS": "1",
                     "WORKERS": "1", "PASSES": "1", "CLAIM_LEASE_SECONDS": "60"})
        run(["prepare"], base)
        workers = []
        for index in range(1, 5):
            env = base | {"NODE_ID": f"test-node-{index}"}
            workers.append(subprocess.Popen([sys.executable, str(SCRIPT), "run"], env=env))
        statuses = [worker.wait(timeout=60) for worker in workers]
        if any(status != 0 for status in statuses):
            raise SystemExit(f"Worker exit statuses: {statuses}")
        expected = {p.stem for p in (work / "in").glob("task_*.json")}
        results = {p.stem for p in (work / "out" / "pass_000").glob("task_*.json")}
        if results != expected:
            raise SystemExit(f"Expected {len(expected)} results; found {len(results)}.")
        assigned: list[str] = []
        for path in sorted((work / "out").glob("test-node-*.csv")):
            with path.open(newline="") as file:
                assigned.extend(row[1] for row in list(csv.reader(file))[1:])
        if len(assigned) != len(set(assigned)) or set(assigned) != expected:
            raise SystemExit("Task assignment log contains duplicate or missing work units.")
        print(f"PASS: {len(expected)} unique task results; {len(assigned)} unique claims across four nodes.")


if __name__ == "__main__":
    main()
