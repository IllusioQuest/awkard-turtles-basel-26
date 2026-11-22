# T-85 — Active-job bookkeeping

Owner: Nikita/Codex. Status: in review; PR #76. Branch: perf/T-85-active-bookkeeping.
User approved selective job snapshots and avoiding discarded history copies.
Preserve the renderer UI exactly, heat-first policy,24/16/8 split and three GPU
workers. Full durable history, heat accounting, runtime discovery/reconciliation
and public history reads remain intact. Test equivalence and allocation reduction.

967cf22:141 tests pass. Real48-frame MP4 complete84.9243s,24/16/8.
Evidence docs/jobs/active-bookkeeping.md and performance JSON. UI unchanged.
