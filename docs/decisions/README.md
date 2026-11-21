# Decision records

One short file per important decision (architecture, data model, interfaces, scope, business
model, larger UI choices). They make our reasoning traceable for the jury and for a possible paper
later.

- File name: `NNNN-short-title.md` (next free number). Copy [`0000-template.md`](0000-template.md).
- Keep it short: 10â€“30 lines. Link to code or docs instead of copying them.
- A decision is **accepted** when the team agrees. The review by a fresh-context subagent (see
  `AGENTS.md`) is recorded in the file.
- Do not rewrite an accepted decision. To change it, write a new record that supersedes it and set
  the old one to `superseded by NNNN`.

| No. | Decision | Status |
|---|---|---|
| [0036](0036-monitor-ui-neutral-contract.md) | Keep monitor logic in place; transfer typed JSON to existing /admin via loopback API | proposed; user approved direction; fresh review go with changes |
| [0001](0001-data-storage.md) | Data storage layout (`data/raw`, `data/derived`, fetch script) | accepted |
| [0002](0002-agent-guide.md) | Agent working guide (`AGENTS.md`), reviews, Git, deadlines | accepted |
| [0003](0003-webhosting-prototype.md), [0004](0004-redundant-windows-ingress.md), [0006](0006-linux-host-network-ingress.md), [0007](0007-whole-container-pause-recovery.md), [0012](0012-hosting-component-isolation.md), [0015](0015-hosting-heat-routing.md), [0016](0016-hosting-replica-lifecycle.md), [0017](0017-always-on-catalog.md) | Webhosting prototype cluster (ingress, pause recovery, isolation, heat routing, replicas, always-on catalog) | superseded by 0026 (webhosting retired) |
| [0005](0005-csharp-core.md) | Shared C# scheduler and executor boundary | accepted (PR #2 merged) |
| [0008](0008-site-heat-state.md) | Per-site heat state and forecast interface | proposed |
| [0009](0009-generic-job-control.md) | Generic Docker job control through the shared scheduler | proposed (code merged to main; renumbered from 0006) |
| [0010](0010-job-queue-year-simulation.md) | Finite job queue in the yearly heat simulation | proposed |
| [0011](0011-result-export-contract.md) | Versioned snake_case result export | proposed; review pending |
| [0013](0013-job-data-locality.md) | Job data-locality eligibility | proposed |
| [0014](0014-routing-optimisation-model.md) | Routing optimisation model (rolling-horizon LP on a time-expanded network) | proposed |
| [0018](0018-two-job-types-strategy.md) | Two job types: sovereign AI services (Type 1) + batch compute filler (Type 2), job model of T-34 | proposed (Ennio); hardware split superseded by 0019; Type 1 contents superseded by 0020 |
| [0019](0019-gpu-nodes-for-all-job-types.md) | GPU nodes for all job types, no CPU-only servers (Type 2 runs on Type 1 hardware) | proposed (Ennio); partly supersedes 0018; team confirmation pending |
| [0020](0020-business-model-operator-two-job-types.md) | Business model (provisional): operator model, Type 1 = batch LLM on non-personal documents, Type 2 = research simulations | proposed (Jona); partly supersedes 0018; reviewed (go with changes); team confirmation pending |
| [0021](0021-selkies-gaming-lab.md) | Selkies compatibility lab and multi-node session placement | proposed; user approved direction, reviewed with changes |
| [0022](0022-gaming-heat-capacity-placement.md) | Shared useful heat capacity and atomic gaming reservations | proposed; user approved ranking, reviewed with changes |
| [0023](0023-intel-dozen-vulkan-experiment.md) | Isolated Mesa Dozen Vulkan build for Intel WSL | proposed; user approved experiment, reviewed with changes |
| [0024](0024-network-scheduler-engine.md) | Network scheduler engine: INetworkScheduler, DigitalHeat.Network(.Lp), OR-Tools GLOP, shared HeatBalance | proposed (Jona); reviewed (go with changes, applied) |
| [0025](0025-network-work-allocation-and-map-export.md) | Network work allocation (Type 1 cohorts, filler, simulated moves), strategies B1-jobs/B2/LP, map export contract, Swiss test dataset rule | proposed (Jona); reviewed (go with changes, applied) |
| [0026](0026-profile-driven-gaming-runtime.md) | Profile-driven C# gaming runtime and retirement of webhosting | proposed; user authorized, reviewed with changes, implemented PR #22; team acceptance pending |
| 0027, 0028 | L4 GPU basis, economics per actor | reserved on unmerged branch `feat/T-14-economics`; not on `main` |
| [0029](0029-type1-heat-budget-reservation.md) | Reserve heat-led capacity for Type 1 (B1-reserve/B2-reserve control; LP-persist dropped) | accepted as control experiment (T-12) |
| [0030](0030-transfer-delay.md) | Transfer delay for moved Type 1 work (amends 0025 Â§3; data staged at arrival) | proposed; reviewed with changes (T-12) |
| [0031](0031-mixed-job-queue.md) | Finite jobs and sold filler capacity in one C# year run | proposed; user approved T-31, reviewed with changes |

| [0032](0032-gaming-monitor-presentation.md) | Read-only gaming console and model/observation boundaries | proposed; reviewed with corrections, T-42 |

| [0033](0033-universal-workload-admission.md) | Universal resource-aware workload admission | proposed; reviewed with changes; user choices pending, T-43 |

| [0034](0034-minecraft-measured-workload.md) | Official Vanilla Minecraft and measured-power startup gate | proposed; T-44; review pending |
| [0035](0035-federated-job-ownership.md) | Federated job ownership and node-local heat control | proposed; reviewed with changes; T-45 |
| [0036](0036-monitor-ui-neutral-contract.md) | UI-independent monitor contract and read-only admin API | proposed; user approved; reviewed with changes; PR #35 |
| [0037](0037-minute-live-job-deadlines.md) | Minute-level bounded live-job deadlines | proposed; user approved; reviewed with changes; PR #37 |
| [0038](0038-indexed-safe-admission.md) | Indexed admission and typed defensive copies | proposed; reviewed with changes; PR #38 depends on #37 |
| [0039](0039-local-docker-observation-client.md) | Reusable local Docker observation transport | proposed; user approved; reviewed with changes; PR #40 depends on #35 |
| 0040 | Local four-node demo | reserved on `feat/T-51-four-node-live-demo` |
| [0041](0041-cs16-linux-compatibility.md) | Isolated CS 1.6 Linux compatibility trial | proposed; user approved two-node Wine trial; reviewed with changes; T-53 |
| [0043](0043-gaming-gpu-eligibility.md) | Gaming GPU eligibility with existing heat ranking | proposed; user approved; T-55 |
| [0044](0044-event-driven-node-feedback.md) | Event-driven workload-owner node feedback | proposed; user approved; T-56 |
| [0045](0045-bfs-residential-population.md) | Separate sourced residential-stock composition profile | proposed; user approved; T-59 |
| 0048 | Jona unified network/admission boundary | reserved on feat/T-60-unified-scheduler |
| [0049](0049-origin-owned-mesh-prototype.md) | Shared heat-first policy and origin-owned mesh prototype | proposed; user approved simulation-first/origin ownership; T-62 |
| [0050](0050-private-linux-mesh-lab.md) | Private Linux mesh and real batch lab | proposed; user approved real nested-engine execution; T-64 |

| [0051](0051-one-heat-first-policy.md) | One heat-first policy; retire competing modes | proposed; user approved; reviewed with changes; T-65; PR #52 |

| [0052](0052-landscape-render.md) | Landscape frame render benchmark | proposed; user approved; T-66 |
| [0053](0053-mixed-render-frame-devices.md) | Configurable mixed CPU/GPU frame jobs | user approved 80/20; reviewed with changes; T-66 follow-up |
| [0054](0054-failed-render-cancellation.md) | Cancel only a failed render comparison, retain data | user approved; reviewed with changes; T-66 |

- [0055 — Verified Eevee rendering for Intel WSL graphics](0055-eevee-intel-render.md): proposed; user-approved capability test, reviewed with changes.

| [0056](0056-free-heat-share-rank.md) | Rank eligible sites by free share of hourly heat budget | proposed; user approved; reviewed; replaces HeatPlacementRank in Fleet, Network, Orchestration; T-67 |

- [0057 - Portable Linux render worker packaging](0057-linux-render-worker.md): proposed; user chose compatible Linux worker, reviewed with changes; hardware unavailable.

- [0058 - Portable native Blender runtime](0058-portable-native-render.md): proposed; user approved native adapter, CPU preparation/GPU frames and frame-boundary pause; multiple nodes only.

- [0059 - Adapter-specific runtime validation](0059-runtime-validation-boundary.md): proposed; reviewed with changes; native and Docker validation remain separate behind one executor interface.
