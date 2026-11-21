"""A time-bounded Monte Carlo request: return completed tasks on SIGTERM.

The host starts and pauses this process through Docker. A graceful stop finishes the current
small task, writes its result, then exits. The output is real computation, not a heat reading.
"""
import json
import os
from pathlib import Path
import signal
import sys
import time

sys.path.insert(0, "/usr/local/bin")
from ising import simulate

work = Path(os.environ.get("WORK", "/work"))
work.mkdir(parents=True, exist_ok=True)
results = work / "results"
results.mkdir(exist_ok=True)
stopping = False


def request_stop(_signum, _frame):
    global stopping
    stopping = True


signal.signal(signal.SIGTERM, request_stop)
done = 0
while not stopping:
    task = {
        "id": f"task_{done:06d}", "temperature": float(os.environ["TEMPERATURE"]),
        "seed": int(os.environ["BASE_SEED"]) + done, "lattice_size": int(os.environ["LATTICE_SIZE"]),
        "sweeps": int(os.environ["SWEEPS"]), "thermalisation_sweeps": int(os.environ["THERMALISATION_SWEEPS"]),
    }
    start = time.monotonic()
    result = simulate(task)
    result["runtime_s"] = time.monotonic() - start
    result_path = results / f"{task['id']}.json"
    result_tmp = result_path.with_suffix(".part")
    result_tmp.write_text(json.dumps(result) + "\n")
    os.replace(result_tmp, result_path)
    done += 1
    tmp = work / "progress.json.tmp"
    tmp.write_text(json.dumps({"tasks_done": done, "latest_result": result}) + "\n")
    os.replace(tmp, work / "progress.json")
    print(json.dumps({"tasks_done": done, "mean_energy_per_spin": result["mean_energy_per_spin"]}), flush=True)
