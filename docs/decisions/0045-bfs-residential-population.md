# 0045 — Separate BFS residential-stock composition profile

- Status: proposed; user approved a separate representative residential profile; reviewed with changes, 2026-11-21. PR #49.
- Ticket: T-59; coordinate with Ennio / Claude, T-58.

Use BFS overview asset 22444710 (2020) as a pinned source for **residential building**
composition. Keep building category and owner type separate. The covered population excludes
ZH and VS; preserve that qualifier in output. Retain the existing SFH-only heat scenarios as
segment scenarios. No claim that privately owned means owner-occupied or single-family.

Extract original building counts and rounded conditional owner percentages for CH and each
available canton into one reproducible CSV with cell/source provenance. Generate a separate
composition fixture: allocate building categories by count using largest remainders, then owner
types within each category using normalized conditional percentages and the same method.
Use stable source order for ties. Keep mixed/unknown ownership. Normalize the entire published
owner row because rounding can sum to 99.9/100.1%; do not invent exact owner counts. Counts
are exact source inputs; joint owner counts are synthetic estimates from rounded percentages.

The default 1,000-site example matches an existing fleet scenario size and is configurable,
not a statistic. No random retries or sorting dependence on process hash. Composition work is
offline input preparation, outside scheduler/admission. It creates synthetic identities, not
real addresses, people or server inventory. No hotel/spa percentages from residential data.

Category-specific heat, hot-water, storage and spatial mappings are missing for the mixed
profile. Do not fill them with the SFH reference parameters or claim the output is a runnable
mixed heat scenario. Publish the source/profile and handoff to T-58; thermal integration needs
those sourced inputs and an agreed mapping. Keep scored reference-house results unchanged.

## Review

Verdict: go with changes. Applied: label the denominator residential buildings, preserve year and
ZH/VS exclusions; retain published percentages, raw stored numeric text and normalization sums;
exclude Total from allocation; distinguish synthetic joint estimates from exact building counts.
The generated node array has no HouseSpec wrapper/thermal defaults, and an integration test
verifies the existing thermal house reader rejects it. Category and owner order are explicit.
The simpler aggregate-only alternative is also supplied as a 20-cell summary; synthetic node
identities are included to support the user's population-generation request, without locations.

Five source/quota tests, 125 independent workbook count checks and the targeted Fleet boundary
test pass. See `../research/swiss-residential-node-mix.md` for counts, calculations and T-58 handoff.
