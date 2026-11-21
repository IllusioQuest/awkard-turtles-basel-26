# 0053 — Mixed CPU/GPU frame allocation

Status: proposed; user approved configurable 80% GPU / 20% CPU frame jobs, 2026-11-21.
Follow-up to T-66 / ADR 0052. This is a workload-count split, never an asserted utilization split.

Use deterministic evenly distributed frame classes: round(frame_count * gpu_share) GPU frames,
remainder CPU frames. With 48 frames and share .8, use 38 GPU and 10 CPU. Both comparison legs
use the same classes. CPU frames request no GPU; GPU frames require a successfully verified
backend. Heat-first placement remains unchanged within each eligible class. Single-node leg
pins both classes to one GPU-capable node; multi-node leg permits eligible peers. CPU still
performs scene construction for GPU jobs. No verified GPU means explicit all-CPU fallback.

Expose planned actual frame counts and actual backend stats. Suppress headline comparisons if
per-backend completion counts differ, frames fail, pause occurs or recovery occurs. Keep
physical GPU identities unique across independent domains. GPU kernel pause unverified locally.

Timeout bug fix is independent: cache immutable node/browser snapshots, poll peers off the UI
request path, retain a stable JSON response schema on failures, prevent overlapping browser
polls, and wait rather than discard origin ownership on transient peer reads. Do not hide an
unresponsive peer, invent progress or fix a timeout by increasing the timeout alone.

## Review
Fresh review: go with changes. Persist backend, frame mix, single-node target, image and frame count before dispatch; detect expired origin jobs even without target attempts; reconcile ambiguous submission acknowledgements by the same durable frame ID. Applied these changes. UI polling now reads immutable cached snapshots with a stable schema; peer errors remain visible.

