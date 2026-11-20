# Webhosting merge and heat-aware integration notes

The user explicitly requested merging the current prototype on 2026-11-21. Release build passes
with warnings as errors; all 17 contract/placement tests pass. Docker crash and pause experiments
are documented in webhosting-runtime.md. This is a runnable lab, not completed production HA:
controllers, etcd orchestration and automatic replica replacement remain pending.

## Other agents' work consulted

- docs/vision.md: compute should move to eligible sites needing heat; sites have parameters,
  storage and data/latency constraints. The vision remains a team draft.
- docs/decisions/0005-csharp-core.md and src/DigitalHeat.Core/Scheduling.cs: reuse HeatSignal,
  IScheduler, IExecutor and SchedulerController.Apply. No second heat scheduler.
- origin/feat/job-runtime: proposed DockerJobExecutor maps the same target load to container
  CPU quotas and pause/resume. It is not merged here. CPU use is a proxy, not measured heat.
  Its ADR number 0006 conflicts with main's Linux ingress ADR; renumber before integration.
- origin/exp/year-explorer-transparency: METHOD.md documents hourly demand, storage and the
  greedy baseline; its independent UI model must agree with the shared C# model.
- docs/research/service-brainstorm.md: webhosting requires availability even without heat demand;
  batch workloads can absorb more flexible heat demand. This is a draft service assessment.

## Proposed data distinctions (awaiting the plan photo and agreement)

A site/building owns heat demand and shared tank state; a compute node belongs to a site. Do not
copy a building's full demand to every server, which would multiply the same demand.

| Proposed property | Meaning |
|---|---|
| siteId | Building/thermal system served by this node |
| maxRecoverableHeatKw | Configured full-load recoverable node heat; fixed per hardware configuration, not a global C# const |
| neededHeatKwh | Site demand for the current simulation step; changes over time |
| tankStoredKwh / tankCapacityKwh | Site storage state and capacity |
| observedHeatKw | Measured or explicitly estimated current heat output, with provenance |
| observedAtUtc | Signal freshness; do not trust stale demand indefinitely |
| targetLoadShare | Shared scheduler output, 0..1; not a promise of achieved heat |

Convert power to energy explicitly: recoverableHeatKwh = maxRecoverableHeatKw * stepHours.
The existing scheduler uses hourly energy inputs. Illustrative unit conversion only: 0.8 kW
for one hour is 0.8 kWh; for half an hour it is 0.4 kWh. This does not choose actual node sizing.
Do not subtract storage twice or replace the shared scheduler's existing tank policy silently.

Proposed flow: site signal -> shared scheduler -> node workload budgets -> placement and ready
replicas -> gateway routing among eligible replicas. Heat preference must respect health, resource
capacity, application readiness, data locality and required redundancy. Moving requests alone does
not guarantee a requested heat output: traffic demand and workload power must be measured.
Before planned pause, make replacement replicas ready and drain traffic. Keep gateways/controllers
outside heat-driven pause decisions; their pause monitors are fault recovery, not heat scheduling.
If all sites have no heat demand, availability versus pause is an explicit policy choice, not an
implicit zero routing weight that makes the site unreachable.

The plan photo was not found among repository image files or the checked branches. Its contents
have not been analyzed. Obtain the photo/path before choosing DTOs or implementing this proposal;
then record and review the interface decision once, with fresh context.
