# T-87 — Switch a fenced GPU worker back on

Owner: Nikita/Claude (taken over from Codex on 2026-11-22 by user decision; no Codex code had been pushed). Status: in review. Branch: claude/beautiful-allen-ukjq15.
Depends on merged T-86 / PR77. User wants the same power button to restart GPU3;
approved rejoining the current render for unstarted frames only. Preserve completed
and running/accepted work, original deadlines, heat-first policy and the existing
UI except power-button state. Retain original journals and heat commitments.
ADR0073 covers local restart identity and rejoin ordering. Tests: repeated off/on,
partial/restart recovery, preserved heat including superseded attempts, real run.

Implemented (Claude): same button "Power on GPU 3" (`POST /ui/nodes/{node}/startup`), restart of the same
controller with its unchanged config/state via `LocalNodePowerControl.Restart`, readiness wait, readmission
(`MeshPeer.ReadmitPeer`, `/readmit`), current-render re-split of open frames (`RenderFailoverPlan.RejoinNeeds`,
superseded attempts via `RunHeatNeed.SupersededIds`). Also fixed two T-86 fencing bugs that only show on Linux
(start-time comparison, exiting child without module). Details and verification: ADR 0073 "Implementation".
