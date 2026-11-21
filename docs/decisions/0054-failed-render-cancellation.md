# 0054 — Scoped failed-render comparison cancellation

Status: proposed; user explicitly approved cancelling the failed comparison while preserving
its images/logs, 2026-11-21. T-66 follow-up, not a general game-stop endpoint.

Allow an operator to cancel only the current stopped/failed comparison. First hold every
render peer; gather exact frame IDs from origin intents and attempts matching that comparison
prefix. On every peer durably record cancelled IDs and origin-job status before stopping owned,
stoppable batch attempts. Protect all interactive/nonstoppable attempts. Delayed proposal and
accepted-reply replays cannot reactivate a cancelled job, including after restart. Retain hourly
heat commitments, images, logs, existing attempts and all game/Minecraft containers.

Only acknowledge the comparison cancelled and permit a new run after every peer confirms.
Unknown stops/cancellation retain the held state; do not clear ownership or invent success.
Archive the old comparison state before creating a new ID. No automatic requeue/migration or
restart of failed workloads. Keep operator API lab-authenticated; browser reset remains scoped
to failed/recovery-paused comparisons, not running games. No bulk Docker cleanup.

## Review
Fresh review: go with changes. Cancellation is terminal for resume and preserved across restart. Fence the complete deterministic two-leg frame-ID set, not only observed attempts, so a delayed submission cannot escape. Require all selected attempts released and all peers acknowledged before enabling a clean run. Node cancellation and mesh processing share the same gate. Added tests for delayed proposals/replies, restart, delayed origin submission, and protected interactive work.

