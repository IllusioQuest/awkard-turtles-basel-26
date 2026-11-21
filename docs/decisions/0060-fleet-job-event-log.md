# 0060: Optional fleet job event log for the household view

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** user / Codex

## Context

Fleet exports per-job totals but no per-house lifecycle timestamps. The household page needs a chronological log, and the requested reference-house examples must reflect actual scheduler actions.

## Decision

Optionally record simulation lifecycle events when the fleet engine applies chunk start, pause, resume and job completion actions. Export run-relative milliseconds, house, job and chunk identity, and event kind in a separate CSV; retain the ordering for simultaneous events. The read-only fleet API exposes bounded per-house event slices, and the dashboard filters them to its selected range. Absence of the optional file is distinct from an empty log. Old runs and the annual scenario remain valid.

Two single-day demand fixtures run through the existing heat-first scheduler and case-house weather/physics. They use the same single-server job, with a user-selected full-load-or-paused requirement whose minimum sustainable load remains unmeasured. The summer tank initial state comes from the earlier reference-year simulation, not a measurement. They are demonstration variants, not evidence of a real process or a replacement for the scored year.

## Alternatives considered

- Infer timestamps from job summaries: these do not contain pause or resume timestamps.
- Script UI events: these could contradict the simulation.

## Consequences

The optional event file has a size cost proportional to lifecycle actions. The API uses the recorded chunk count in `jobs.csv` to label events on single-chunk jobs; split-job events keep their chunk IDs. Live job control remains a separate verification requirement.

## Review

Verdict: **go with changes**. Completion is logged at the authoritative engine completion path and attributed to the final house; chunk start/pause/resume/stop remain explicitly chunk-level. The API uses half-open time ranges, keeps file order for ties, reports missing logs and indicates truncation; the UI displays that status. Scenario assumptions and verification are documented with the fixtures. Logs are opt-in, including for large runs.
