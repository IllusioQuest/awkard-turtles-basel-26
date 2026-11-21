# 0061: Paced Fleet decisions for a real household demo job

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** user / Codex

## Context

The household day examples use the Fleet scheduler and virtual jobs, while the existing console LiveRun controls a real container using separate simulated tank snapshots and does not feed the dashboard. The request is to show the winter and summer examples alongside a real, interruptible workload.

## Decision

Use an opt-in, single-job demonstration adapter around the existing `HeatFirstFleetScheduler` and its `IFleetActions`: pace simulated time and forward the scheduler's actual accepted start/load decisions to an exclusively owned local container through the existing Docker executor. Do not alter the scheduler, thermal model or yearly scenario. The public workload runs locally; transcription is preferred when already installed, otherwise use the available research simulation image.

Publish atomic, bounded local snapshots for a household dashboard via a read-only API, explicitly separating modelled heat/storage and simulated time from observed process state, output and completion. The real request computes until the simulated job ends, then is asked to finish its current task and exit; successful real completion requires an independently observed clean exit and output. An early process exit, failed Docker action, or lost connection is surfaced rather than silently treated as simulated success. The accelerated clock and full-load constraint are scenario assumptions; actual electrical power and recovered heat are not measured.

## Alternatives considered

- Replay a recorded event list: this would not prove that the shared scheduler controls the process.
- Add dashboard output to the older LiveRun only: its independent tank snapshots do not reproduce the Fleet day examples.

## Consequences

This is a single-container demo, not a production service or a claim that simulated work progress matches real progress. Workload ownership and cleanup must not touch existing Docker containers. Live transcription requires an installed model, image and input; no unapproved download or sensitive data is needed.

## Review

Verdict: **go with changes**. Handle simulated chunk completion and end-of-run separately from scheduler actions; stop or pause only the owned container on completion, error and cancellation. Pace event batches against a monotonic clock, serialize Docker actions and publish snapshots only after acknowledged actions; surface lag and errors. Verify the yearly Fleet scheduler and real process both demonstrate the heat-driven pause/resume, including early completion and cleanup. A console proof with recorded dashboard fallback is the smaller first milestone.
