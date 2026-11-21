# T-59 — Swiss building/ownership data and simulation node mix

- Owner: Nikita / Codex; coordinate generator integration with Ennio / Claude (T-58).
- Status: composition profile implemented; PR #49 in review; mixed thermal integration pending T-58 sourced inputs
- Branch: data/T-59-building-owner-mix, from latest main.

Analyze the user-provided BFS 2020 ownership release and linked detailed tables. Preserve
population/year/coverage and distinguish ownership, building category, households and physical
compute servers. Publish reproducible counts/weights with provenance and a generator handoff.
Check existing T-58 HouseGenerator before modifying shared assumptions: it currently samples
only single-family houses using per-cell single-family counts. Do not call this a representative
all-building mix. No replacement heat formula, reference-house change or guessed hotel/spa share.

Done: checked source tables and coverage, documented calculation path and appropriate sampling
proposal, communicated with co-builders, and implementation/handoff status accurately recorded.
Important generator/data-model choices require team/user agreement before code integration.

Results: pinned source, 125 derived source rows, 1000-node composition (572 SFH / 264 MFH / 117 residential-secondary / 47 partly residential), separate conditional owner types. Five Python tests, 125 independent count checks and the Fleet thermal-boundary test pass. ADR 0045 reviewed with changes. Co-builders notified on PR #48 and shared status; see docs/research/swiss-residential-node-mix.md. This does not change current SFH heat simulation behavior.
