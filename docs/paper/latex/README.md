# Paper: heat-led computing in homes

`main.tex` is the source; `main.pdf` is the reading copy. Rebuild the PDF after every source change and commit the PDF together with the TeX files.

## Build and checks

From this directory, run `tectonic main.tex` (or `latexmk -pdf main.tex` with a full TeX Live installation). Tectonic resolves the bibliography and produces `main.pdf`. The Codex LaTeX editor can also compile the project for diagnostics and preview, but its preview alone does not update the versioned PDF. Inspect the title, tables, page breaks and references in the generated PDF. Run `git diff --check` and `dotnet test src/DigitalHeat.sln` from the repo root.

The paper combines three evidence levels: case-defined reference-house parameters, measured software executions, and explicitly synthetic network and market scenarios. The small four-L4 node is a physical and commercial hypothesis, not a measured deployment. The paper does not claim observed profitability. Before external publication, the team should confirm author approval, venue format and the final revision of the underlying results.

## Reproducibility map

| Result | Command or source |
|---|---|
| Reference-house year and sizing | `python scripts/export_reference_house.py`; `dotnet run --project src/DigitalHeat.YearRun -- . --sizing data/derived/server-sizing/reference-house.json` |
| Network experiments A--E | `python3 scripts/network_experiments.py`; LP: add `--lp --jobs 4` |
| Mixed queue | The two `DigitalHeat.YearRun` commands in `docs/model/mixed-job-queue.md` |
| Operator and small-node sensitivities | Worked arithmetic and source ledger in `docs/research/free-heat-viability.md` and `docs/research/profitable-home-path.md` |
| Measured demo jobs | Commands and traces in `docs/jobs/` and `sections/appendix-reproducibility.tex` |

The federated-admission draft in `sections/09-federation.tex` is a separate systems note and is
not part of this paper's PDF.

All monetary scenarios are conditional on the documented input prices. The case's abstract 1 kW server and the proposed physical four-L4 node are different systems.
