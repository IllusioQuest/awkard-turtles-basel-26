"""Bound subprocess time and captured output, without limiting files the application writes."""
import argparse
import os
import signal
import subprocess
import threading


def run(command, log_path, max_log_bytes, timeout_seconds):
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               start_new_session=True, bufsize=0)
    capture_errors = []

    def capture():
        try:
            with open(log_path, "wb") as output:
                retained = 0
                while data := process.stdout.read(65536):
                    count = min(len(data), max_log_bytes - retained)
                    if count > 0:
                        output.write(data[:count])
                        output.flush()
                        retained += count
                    # Keep draining excess output, without retaining it or blocking the child.
        except (OSError, ValueError):
            capture_errors.append(True)

    collector = threading.Thread(target=capture, daemon=True)
    collector.start()
    try:
        result = process.wait(timeout=timeout_seconds)
    except subprocess.TimeoutExpired:
        for sig in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(process.pid, sig)
            except ProcessLookupError:
                pass
            try:
                process.wait(timeout=3)
                break
            except subprocess.TimeoutExpired:
                continue
        result = 124
    collector.join(timeout=1)
    if not collector.is_alive():
        process.stdout.close()
    if capture_errors:
        raise RuntimeError("Unable to retain bounded process log")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", required=True)
    parser.add_argument("--max-log-bytes", type=int, required=True)
    parser.add_argument("--timeout-seconds", type=float, required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command or args.max_log_bytes < 1 or args.timeout_seconds <= 0:
        parser.error("A command and positive limits are required")
    raise SystemExit(run(command, args.log, args.max_log_bytes, args.timeout_seconds))
