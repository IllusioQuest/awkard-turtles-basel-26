# Decision records

One short file per important decision (architecture, data model, interfaces, scope, business
model, larger UI choices). They make our reasoning traceable for the jury and for a possible paper
later.

- File name: `NNNN-short-title.md` (next free number). Copy [`0000-template.md`](0000-template.md).
- Keep it short: 10–30 lines. Link to code or docs instead of copying them.
- A decision is **accepted** when the team agrees. The review by a fresh-context subagent (see
  `AGENTS.md`) is recorded in the file.
- Do not rewrite an accepted decision. To change it, write a new record that supersedes it and set
  the old one to `superseded by NNNN`.

| No. | Decision | Status |
|---|---|---|
| [0001](0001-data-storage.md) | Data storage layout (`data/raw`, `data/derived`, fetch script) | accepted |
| [0002](0002-agent-guide.md) | Agent working guide (`AGENTS.md`), reviews, Git, deadlines | accepted |
| [0003](0003-webhosting-prototype.md) | Resilient local webhosting prototype | proposed; user approved stack/structure, team confirmation pending |
| [0004](0004-redundant-windows-ingress.md) | Redundant Windows ingress experiment | proposed then withdrawn; all runtime services must use Linux containers |
| [0005](0005-csharp-core.md) | Shared C# scheduler and executor boundary | accepted (PR #2 merged) |
| [0006](0006-linux-host-network-ingress.md) | Linux host-network ingress crash experiment | proposed; experiment approved by user, reviewed with changes; full HA pending |
| [0007](0007-whole-container-pause-recovery.md) | Two Linux managers for paused gateway containers | proposed; reviewed with changes, awaiting user approval |
| [0008](0008-site-heat-state.md) | Per-site heat state and forecast interface | proposed |
| [0009](0009-generic-job-control.md) | Generic Docker job control through the shared scheduler | proposed (code merged to main; renumbered from 0006) |
| [0010](0010-job-queue-year-simulation.md) | Finite job queue in the yearly heat simulation | proposed |
| [0011](0011-result-export-contract.md) | Versioned snake_case result export | proposed; review pending |
| [0012](0012-hosting-component-isolation.md) | Separate frontend and API node pools | proposed; review pending |
| [0013](0013-job-data-locality.md) | Job data-locality eligibility | proposed |
| [0015](0015-hosting-heat-routing.md) | Shared heat scheduler in hosting routing | proposed; approval/review pending |
| [0016](0016-hosting-replica-lifecycle.md) | Six-node heat-guided hosting lifecycle | proposed; user approved scope, review pending |
| [0017](0017-always-on-catalog.md) | Always-on entries and safe batch-control guard | proposed; scope approved, review pending |
| [0018](0018-two-job-types-strategy.md) | Two job types: sovereign AI services (Type 1) + batch compute filler (Type 2), job model of T-34 | proposed (Ennio); hardware split superseded by 0019; Type 1 contents superseded by 0020 |
| [0019](0019-gpu-nodes-for-all-job-types.md) | GPU nodes for all job types, no CPU-only servers (Type 2 runs on Type 1 hardware) | proposed (Ennio); partly supersedes 0018; team confirmation pending |
| [0020](0020-business-model-operator-two-job-types.md) | Business model (provisional): operator model, Type 1 = batch LLM on non-personal documents, Type 2 = research simulations | proposed (Jona); partly supersedes 0018; reviewed (go with changes); team confirmation pending |
| [0025](0025-economics-per-actor.md) | Economics per actor: system surplus, split, levers, win-win grid (T-14) | proposed; reviewed (go with changes, applied) |
| [0024](0024-network-scheduler-engine.md) | Network scheduler engine: INetworkScheduler, DigitalHeat.Network(.Lp), OR-Tools GLOP, shared HeatBalance | proposed (Jona); reviewed (go with changes, applied) |
| [0025](0025-network-work-allocation-and-map-export.md) | Network work allocation (Type 1 cohorts, filler, simulated moves), strategies B1-jobs/B2/LP, map export contract, Swiss test dataset rule | proposed (Jona); reviewed (go with changes, applied) |

| [0021](0021-selkies-gaming-lab.md) | Selkies compatibility lab and multi-node session placement | proposed; user approved direction, reviewed with changes |

| [0022](0022-gaming-heat-capacity-placement.md) | Shared useful heat capacity and atomic gaming reservations | proposed; user approved ranking, reviewed with changes |

| [0023](0023-intel-dozen-vulkan-experiment.md) | Isolated Mesa Dozen Vulkan build for Intel WSL | proposed; user approved experiment, reviewed with changes |
