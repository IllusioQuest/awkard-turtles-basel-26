#!/usr/bin/env python3
"""Record the local LiveGrid state API as newline-delimited JSON frames."""

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path
from urllib.request import urlopen

INTERVAL_SECONDS = 0.4
DEFAULT_URL = "http://127.0.0.1:5087/api/state"
DEFAULT_OUTPUT = Path("data/derived/live-demo/recording.jsonl")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--minutes", type=float, required=True, help="recording duration in minutes")
    parser.add_argument("--url", default=DEFAULT_URL, help=f"state endpoint (default: {DEFAULT_URL})")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help=f"JSONL output (default: {DEFAULT_OUTPUT})")
    args = parser.parse_args()
    if args.minutes <= 0:
        parser.error("--minutes must be greater than zero")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    deadline = time.monotonic() + args.minutes * 60
    frames = 0
    next_poll = time.monotonic()
    with args.output.open("w", encoding="utf-8", newline="\n") as recording:
        while time.monotonic() < deadline:
            with urlopen(args.url, timeout=10) as response:
                frame = json.loads(response.read().decode("utf-8"))
            recording.write(json.dumps(frame, separators=(",", ":"), ensure_ascii=False) + "\n")
            recording.flush()
            frames += 1
            next_poll += INTERVAL_SECONDS
            delay = min(next_poll - time.monotonic(), deadline - time.monotonic())
            if delay > 0:
                time.sleep(delay)
    print(f"Recorded {frames} frames to {args.output}")


if __name__ == "__main__":
    main()
