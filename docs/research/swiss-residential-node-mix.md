# Residential node composition from BFS 2020

2026-11-21; T-59; Nikita / Codex. User approved a separate representative residential profile
alongside the existing single-family-only scenarios. [PR #49](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/49)
contains data, configuration, derivation and tests; ADR 0045 has a fresh review with changes.

## Finding

Single-family buildings are the majority of the **covered residential building stock**. Private
ownership is another dimension. The source does not support forcing a majority of all nodes
to be privately owned single-family houses, nor does it give a hotel/spa share.

The user-linked [BFS release](https://statistik.admin.ch/de/news/2022-0305), published 2022-05-19,
describes 2020 ownership. The [linked overview workbook, asset 22444710](https://dam-api.bfs.admin.ch/hub/api/dam/assets/22444710/master)
contains exact building counts and one-decimal owner percentages by building category and canton.
All sources accessed 2026-11-21. The workbook is retained unchanged with SHA256
`13817704ea5cab9e71ce620f5dc93ed79f36249aea0e25e42018161a33439f72`.

**Coverage: residential buildings at 2020-12-31, Switzerland excluding Zurich and Valais.**
The [methodology](https://dam-api.bfs.admin.ch/hub/api/dam/assets/22707037/master) explains that
ZH/VS could not be included because register quality/linkage was insufficient. This historical
profile must not be presented as the current full-Switzerland stock or a count of households,
people, DigitalHeat customers, parcels, independently available GPUs or physical servers.

## Building categories

Source CH!A6:B10; source counts sum exactly to Total. Percentages below are our count/total
calculations, not separately rounded values copied from the release.

| Building category | Source buildings | Share of covered stock | Nodes in the 1,000-building fixture |
|---|---:|---:|---:|
| Single-family | 813,592 | 57.1694% | 572 |
| Multi-family | 376,285 | 26.4407% | 264 |
| Residential with secondary use | 166,235 | 11.6810% | 117 |
| Partly residential | 67,014 | 4.7089% | 47 |
| **Total** | **1,423,126** | **100%** | **1,000** |

Method: `category_share = category_buildings / 1,423,126`. For N = 1,000, take the floor of
each `N * category_share`, then give remaining nodes to the largest fractional remainders.
Stable source row order breaks equal ties. Thus the total is exactly N and each category differs
by less than one building from its fractional quota. N = 1,000 is an operator scenario size
matching an existing fleet example, not an observed DigitalHeat installed base.

## Ownership: keep it separate and conditional

Source CH!C6:G10. Preserve all five categories:

| Owner type | Published share, all covered residential buildings | Published share within SFH | Fixture, all buildings |
|---|---:|---:|---:|
| Natural person(s) | 67.5% | 75.5% | 675 |
| Legal person | 11.5% | 3.2% | 115 |
| Community | 14.4% | 18.3% | 145 |
| Mixed | 5.5% | 2.0% | 55 |
| Unknown | 1.1% | 1.1% | 10 |

Natural-person ownership does not prove owner occupation. Communities include legal collective
ownership arrangements, such as simple partnerships or inheritances; do not silently relabel all
of them as either private households or public institutions. Mixed and unknown remain explicit.

Sampling ownership independently from building category would lose a real correlation:
natural persons own 75.5% of SFH but 56.6% of multi-family buildings in the published table.
Use `P(owner | building_category)`, not the 67.5% overall marginal for every type.

Published one-decimal rows can sum to 99.9/100.1% through rounding. Normalize all five owners
within the row, preserving original values and the row sum. For SFH:

`natural_person_weight = 75.5 / (75.5 + 3.2 + 18.3 + 2.0 + 1.1) = 75.5 / 100.1`

Then apply largest remainders to the 572 SFH nodes. This creates **431 natural-person-owned
SFH**, the largest type/owner cell but **43.1% of the entire fixture**. It is not a majority of
all buildings. The other SFH ownership counts are 18 legal-person, 105 community, 12 mixed,
and 6 unknown. Counts are synthetic quota estimates from rounded source percentages, not exact
census owner counts. The resulting overall ownership marginal can differ slightly from CH's
separately rounded Total row (here community 14.5%, unknown 1.0%). Do not force both sets of
rounded marginals to match by inventing hidden joint counts.

The extraction also preserves original XLSX numeric text, because Excel may store a displayed
1.1 as 1.1000000000000001. Published-percent columns use the declared 0.1-percentage-point
precision; raw text and cell coordinates remain available for audit.

## Canton differences matter

The derived CSV includes CH plus all 24 covered cantons, with building category and conditional
owner rows. Example source cells: 12_BS!B6:G10 and 13_BL!B6:G10.

- Basel-Stadt: 23,655 covered buildings, including 9,278 SFH. Natural persons own 40.6% of all
  buildings and 49.1% of SFH there.
- Basel-Landschaft: 67,061 covered buildings, including 46,477 SFH. Natural persons own 46.5%
  overall and 49.8% of SFH there; communities are a substantial separate ownership category.

Do not apply national 67.5/75.5% ownership shares to Basel unchanged. A regional profile can
select the appropriate source region; ZH and VS must remain unsupported by this snapshot,
not zero, inferred from neighbours or silently assigned the covered-CH shares. The T-51 spatial
inputs use a different/current register snapshot; combining snapshots needs an explicit label.

## Implementation and reproducibility

| Path | Purpose |
|---|---|
| data/raw/bfs-building-ownership-2020/source.json | Source URLs, dates, coverage, cell mapping, precision and pinned hash |
| data/raw/bfs-building-ownership-2020/overview-22444710.xlsx | Unchanged original 112,469-byte source workbook |
| data/derived/buildings/bfs-ownership-2020.csv | 125 rows: 25 geographic sheets × Total plus four categories; source-cell and numeric provenance |
| config/fleet/populations/bfs-residential-2020.json | Separate composition profile, count/region/output paths and missing thermal integration |
| data/derived/fleet/populations/bfs-residential-2020-covered-1000.summary.csv | 20 synthetic category/owner cells with source percentages and normalization sums |
| data/derived/fleet/populations/bfs-residential-2020-covered-1000.nodes.json | 1,000 synthetic identities, category and owner only; deliberately not a thermal HouseFile |
| scripts/derive_residential_population.py | Standard-library-only extraction and deterministic quota generation |

```powershell
python scripts/derive_residential_population.py
python scripts/derive_residential_population.py --check
python -m unittest discover -s scripts/tests -p test_residential_population.py
dotnet test src/DigitalHeat.Fleet.Tests --filter FullyQualifiedName~ResidentialCompositionTests
```

`--download` retrieves the pinned workbook only if absent and verifies its SHA256. `--check`
reproduces outputs in memory, comparing text independent of Git LF/CRLF checkout settings.
Copy/edit the profile to select a covered canton or a different positive scenario count/output
path; keep the coverage description accurate. No RNG or repeated sampling is needed to meet quotas.

Verification: five Python source/quota tests pass, including source totals/canton coverage,
normalization, deterministic ties, N = 1/7/100/1,000, canton ownership and absence of thermal
defaults. All 125 source count cells were independently checked with bundled openpyxl against
the standard-library extraction. The targeted Fleet test passes and confirms that the composition
array is rejected by HouseFiles.Read. An unrelated existing xUnit2013 warning in PolicyTests.cs
was observed and left unchanged. Scheduler, heat physics and scored reference parameters are
unchanged; extraction/generation happens offline, not inside admission decisions.

## Co-builder handoff and current limit

Shared with Ennio/T-58 on the [merged fleet PR](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/48#issuecomment-7063629623)
at the user's explicit request. The current HouseGenerator only supports
`proportional_to_single_family`, spatial weights `n_single_family`, and SFH-derived heat sizing.
Its existing scenarios remain valid **SFH-segment scenarios**, not representative all-building
stock. No shared generator, scheduler, API or UI was overwritten in this task.

The new profile prepares the requested representative **composition**, not a runnable mixed
thermal scenario. Before integrating it into T-58, source category-specific heat, hot-water,
storage/capacity and spatial mappings. Do not copy the 10 kW/reference hot-water/tank assumptions
onto every multi-family or partly residential building and call that measured realism. A hotel/spa
variant needs separately sourced non-residential stock and seasonal heat demand; residential
ownership data cannot supply those shares. Building count weights also are not heat-load weights:
a minority of larger sites can still absorb more useful heat. The scheduler should continue to
rank by useful heat among resource/locality/required-latency eligible nodes.
