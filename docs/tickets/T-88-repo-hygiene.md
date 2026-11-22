# T-88 — Repository hygiene: one test command, lint, identity-hash finding

Owner: Nikita / Claude. Status: in progress. Branch: claude/beautiful-allen-ukjq15.
User-approved scope (2026-11-22): no changes in areas other agents are working on (renderer,
scheduler core T-85/T-86, web/fleet/pitch).

Done here:
- `src/DigitalHeat.sln` now includes `DigitalHeat.Orchestration`, its tests (145) and
  `DigitalHeat.WorkloadRun`, so the documented `dotnet test src/DigitalHeat.sln` runs them. Gaming keeps
  its own solution; README and QUICKSTART list all test commands.
- Removed unused imports/variables flagged by ruff in `scripts/` and `rendering/bench_frame.py`.
  Renderer scene files and measured jobs were left unchanged (their bytes define scene/job identity).

Finding for the Orchestration owner (not changed here): `WorkloadJson.Identity` serializes with
`WriteIndented = true`, and .NET 8 uses the OS line ending, so the same job has a different identity
hash on Windows (CRLF) and Linux (LF). The legacy-deadline test's expected hash is the CRLF version
(verified by recomputing it), which is why it fails on Linux. `MeshPeer` compares these identities for
proposals and replies, so a Windows origin and a Linux GPU peer (`rendering/network/`) would reject
each other's proposals. Possible fix: normalize identity text to LF (or serialize identities compactly)
and accept the legacy CRLF hash when reading existing journals. Owner decides; changes old hashes.
