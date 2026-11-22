# T-87 — Switch a fenced GPU worker back on

Owner: Nikita/Codex. Status: in progress. Branch: feat/T-87-node-rejoin.
Depends on merged T-86 / PR77. User wants the same power button to restart GPU3;
approved rejoining the current render for unstarted frames only. Preserve completed
and running/accepted work, original deadlines, heat-first policy and the existing
UI except power-button state. Retain original journals and heat commitments.
ADR0073 covers local restart identity and rejoin ordering. Tests: repeated off/on,
partial/restart recovery, preserved heat including superseded attempts, real run.
