# Paper draft in LaTeX (T-22)

LaTeX version of the skeleton in [`../outline.md`](../outline.md). Draft, not reviewed by the team.

## Build

```sh
cd docs/paper/latex
latexmk -pdf main.tex      # pdflatex + bibtex, about 14 pages
latexmk -c                 # remove auxiliary files
```

Needs a TeX Live with `amsmath`, `booktabs`, `siunitx`, `hyperref`, `graphicx`, `xcolor` (Debian/Ubuntu:
`texlive-latex-base texlive-latex-recommended texlive-latex-extra texlive-science latexmk`).
`lmodern` and `xurl` are used if installed. The PDF and auxiliary files are not committed
(`.gitignore`).

## Markers

- `\todo{...}` (red): text still to be written, or a missing source.
- `\pending{}` (orange): a result that is being computed (full-year LP runs).

## Status per section

| File | Status |
|---|---|
| `sections/01-introduction.tex` | short draft; contributions still to be chosen by the team |
| `sections/02-related-work.tex` | short draft (companies, Swiss projects); academic references `\todo` |
| `sections/03-case.tex` | drafted |
| `sections/04-system-model.tex` | drafted: sets, variables, constraints of the LP, forecast error, transfer delay |
| `sections/05-scheduling.tex` | **drafted** (prose from outline §5, strategies, lexicographic stages, greedy special case, reserves) |
| `sections/06-economics.tex` | short draft with surplus table; heat fee and price trends `\todo` |
| `sections/07-demonstrator.tex` | skeleton (architecture paragraph only) |
| `sections/08-evaluation.tex` | **drafted** (8.1, 8.3–8.5 with tables A, B, C, D, E, decision 0029); 8.2 sizing and 8.6 capacity `\todo` |
| `sections/09-limits.tex` | short draft |
| `sections/10-conclusion.tex` | short draft; outlook `\todo` |
| `sections/appendix-reproducibility.tex` | table of commands |

`\pending{}` cells: LP missed/moved in tables A (4 shares) and D (2 networks), all of table C
(forecast error), LP at d = 4 and 12 h in table E, and finding (iii) for the network.

## Where the numbers come from

Every number is copied from a repository file; each table caption names the command.

| Table | Command | Values copied from |
|---|---|---|
| Reference house, 3/4/5 servers | `python scripts/export_reference_house.py` | `docs/results/reference-house.md` |
| Strategies on the reference house | `dotnet run --project src/DigitalHeat.NetworkRun -- .` | `docs/model/network-scheduler.md` |
| A, B, D, E (greedy), decision 0029 | `python3 scripts/network_experiments.py` | `data/derived/network/experiments/*.csv`, `docs/model/network-scheduler.md`, `docs/decisions/0029-*.md` |
| LP columns, C | `python3 scripts/network_experiments.py --lp --jobs 4` | pending |
| Surplus per house | `python3 scripts/profitability_inputs.py` | `docs/business-model.md` |

When the LP runs finish, replace the `\pending{}` cells from the new CSV files and update
`docs/model/network-scheduler.md` at the same time (same numbers everywhere, AGENTS.md §9a).

## References

`references.bib` contains only sources already cited with a URL in the repository
(`docs/research/*.md`; for the datasets `docs/case/challenge.md` and `data/raw/*/SOURCE.md`), with
access dates as recorded there. Titles come from the repository notes or URL slugs and must be
checked against the pages before submission. Academic references are still missing (`\todo` in §2).
