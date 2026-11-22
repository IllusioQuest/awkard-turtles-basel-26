# Paper: heat-led computing in homes

`main.tex` is the source; `main.pdf` is the reading copy. Rebuild the PDF after every source change and commit the PDF together with the TeX files.

## Build and checks

From this directory, run `tectonic main.tex` (or `latexmk -pdf main.tex` with a full TeX Live installation; on Fedora the package `texlive-pgfplots` is needed, or unpack the CTAN `pgfplots.tds.zip` into `~/texmf`). Tectonic resolves the bibliography and produces `main.pdf`. The Codex LaTeX editor can also compile the project for diagnostics and preview, but its preview alone does not update the versioned PDF. Inspect the title, tables, page breaks and references in the generated PDF. Run `git diff --check` and `dotnet test src/DigitalHeat.sln` from the repo root.

The paper combines three evidence levels: case-defined reference-house parameters, measured software executions, and explicitly synthetic network and market scenarios. The small four-L4 node is a physical and commercial hypothesis, not a measured deployment. The paper does not claim observed profitability. Before external publication, the team should confirm author approval, venue format and the final revision of the underlying results.

## Reproducibility map

| Result | Command or source |
|---|---|
| Reference house, real house, break-even sweep, Dreispitz (Fleet model) | `dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/<name>.json`, then `-- post data/derived/fleet/<name>`; inputs and results in `docs/model/fleet-simulation.md` §16 |
| Unlimited-work limit and sizing | `dotnet run --project src/DigitalHeat.YearRun -- .` and `-- . --sizing data/derived/server-sizing/reference-house.json`; fleet cross-check test `ReferenceHouseWithBacklogMatchesCore` |
| Real-house calibration | `python3 -I scripts/calibrate_real_house.py` (organisers' data, `data/raw/housedata/SOURCE.md`) |
| Network experiments A–E (research strategies) | at commit `1863f57`: `python3 scripts/network_experiments.py`; LP: add `--lp --jobs 4` (the strategies are not selectable on current `main`) |
| Operator and small-node sensitivities | Worked arithmetic and source ledger in `docs/research/free-heat-viability.md` and `docs/research/profitable-home-path.md` |
| Demonstrator and measured jobs | `docs/demo/voice-request.md`, `docs/jobs/` |

The federated-admission draft in `sections/09-federation.tex` is a separate systems note and is
not part of this paper's PDF.

All monetary scenarios are conditional on the documented input prices. The case's abstract 1 kW server and the proposed physical four-L4 node are different systems.
