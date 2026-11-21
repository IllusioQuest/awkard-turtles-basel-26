> RETIRED (T-40): the user withdrew webhosting. This page is historical evidence; its code and setup commands are no longer supported. See [gaming profiles](gaming-profiles.md).

# T-23: Hosting and heat-runtime integration

Status: integration experiment in progress; always-on catalog entry requires T-01 owner agreement.
Code branch: feat/T-23-webhosting-integration. T-09 is merged; do not duplicate its heat-state types.

## Reused boundary

DigitalHeat.LiveRun -> SchedulerController.Apply -> GreedyHeatScheduler -> DockerJobExecutor
-> IContainerControl. No new scheduler or executor implementation is added. Hosting contributes
its Linux sample image. Test-HeatControl.ps1 starts one disposable replica without published ports,
passes its ID to the shared replay and removes only that replica afterward. Gateways, pause
monitors and nested engine nodes remain outside heat-driven pause decisions.

From the repository root, after building webhosting-sample:local:

```powershell
./webhosting/scripts/Test-HeatControl.ps1
```

The probe descriptor in webhosting/deploy/heat-control-probe.json describes an isolated, disposable
replica-control experiment, not the always-on website. Its freeze support is deliberately limited
to this experiment. No HTTP customers use that replica. Missing runtime/energy quantities remain
null (TODO(source)); CPU usage is not electrical power or recovered heat.

The replay uses the shared reference year and independent tank snapshots. Its forced pause is a
labelled synthetic control test, not a claim that ordinary July demand becomes zero at that hour.
An idle HTTP server can show near-zero observed CPU load despite a positive target quota. This
illustrates why request routing or CPU quotas alone cannot guarantee a required heat output.

## Always-on catalog handoff to T-01 owner

| Field | Proposed hosting meaning |
|---|---|
| id | webhosting-always-on |
| work unit | service-hour with separately measured request demand |
| interruptible | false at service level |
| replica control | pause only after replacement readiness and traffic drain; not implemented yet |
| deadline / latency requirement | TODO(source): agreed request service objective |
| idle and loaded power, energy per service-hour | TODO(source): measurement required |
| arrivals / traffic rate, income | TODO(source): workload and price evidence required |

The existing config/jobs/job.schema.json requires interruptible=true and freeze support, and
JobDescriptorLoader enforces the same rule. Do not insert an invalid always-on descriptor or
mislabel it as freely pausable. The catalog owner must decide the shared representation for
always-on services versus interruptible execution units. This is an interface choice, not a local
loader workaround. No global hardware count, commercial service choice or routing optimiser is
selected by this ticket.

Request to T-01/T-03 owners: confirm the boundary above and provide the agreed always-on catalog
representation. Keep T-23 open until that agreement and the valid catalog entry exist. The neutral
control probe does not complete those acceptance criteria or measure energy for the draft.

## Verified result and handoff

2026-11-21: Test-HeatControl.ps1 completed all 24 July 15 hourly steps, forced a pause at step 12, resumed afterward, verified the replica was unpaused after executor cleanup, and removed the dedicated container. Exit code 0. Rounded target was 16% outside the forced pause; observed CPU was 0–1%, reflecting an idle sample rather than delivered heat. Draft PR #7: https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/7. T-23 remains in progress pending catalog representation and owner agreement; it is not marked done.

