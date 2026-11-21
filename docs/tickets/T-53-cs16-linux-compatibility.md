# T-53: CS 1.6 Linux streaming compatibility and scheduler regression check

Owner: Nikita / Codex. Status: in progress; user approved isolated Wine test with two new nodes.
Code branch: `feat/T-53-cs16-linux`; docs branch: `docs/T-53-cs16-assessment`. ADR 0041 reviewed.

Replace the previous ReBorn candidate with the user-supplied `Counter-Strike-1.6-original.exe`.
Keep Minecraft unchanged and retain multiplayer, including user-requested public/community-server
compatibility. Keep game installation/startup out of the scheduler decision path. Do not remove
the existing working shooter until the replacement has actually been verified.

Static inspection: 184,076,620-byte unsigned PE x86 installer. SHA-256:
`a8cba24594c80a8919167cf096eb0dab9246c34d9d4d52c50c367a2b8f356512`.
Version description: Counter-strike 1.6 Installation; self-declared company includes CSDOWNLOAD.
Download metadata hosts: frageris.lt / cybersports.lt. These are observations, not publisher
authentication. 7-Zip 24.07 could not open it as an archive. Inert strings include `hl.exe`,
`hlds.exe`, `rev.ini`, `steam_api.dll`; those names alone establish neither compatibility nor
malicious behavior. No installer/game was executed, no files were extracted and no runtime changed.

User approved two Linux Wine client nodes, testing multiplayer work distribution, and choosing a
community server. This distributes client processing across nodes, not one server tick simulation.
Perform separate bounded Linux compatibility test, then validate stream/input,
multiplayer/authentication, and placement latency under game load. Respect active games and keep
all existing scheduler guards. PR #38 contains measured admission optimizations and is unmerged;
do not claim main already has them. No new measured power/heat figure is available for this game.

Preflight (before CS launch): same fixed-seed 62-case diagnostic and optimized source 9eb95ee,
20 warmups/101 samples; shared-domain 1,024-node reserve median 1.6022 ms, p95 2.5228 ms.
Three-node independent reserve median 0.0041 ms, p95 0.0052 ms. Existing unrelated games/web
remained running; laptop load is uncontrolled. Raw evidence:
`docs/performance/scheduler-cs16-preflight-2026-11-21.json`. This is not a CS-loaded result.
Code branch explicitly includes PR #38 (and #37) so it reuses that admission implementation.

Done criteria: verified client test and real multiplayer connection, repeatable config/commands,
documented runtime/latency measurements and limitations, tests passing, code PR and shared docs.
If setup is declined or prerequisites are unavailable, record the actual outcome without claiming
the game was replaced. No authentication, anti-cheat or server-policy bypass.
