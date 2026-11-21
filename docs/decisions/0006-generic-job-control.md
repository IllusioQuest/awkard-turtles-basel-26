# 0006: Generic container job control from the heat signal

- **Status:** proposed
- **Date:** 2026-11-20
- **Decided by:** Jona / Codex (team confirmation pending)

## Context

The case requires one scheduler to drive the annual balance and a real running job. The core
already defines `SchedulerController.Apply`, `IScheduler` and `IExecutor` (decision 0005). Nikita's
node runtime owns deployment; Nikita and Ennio own the webhosting and transcription examples.

## Decision

`DigitalHeat.JobRuntime` adds `DockerJobExecutor : IExecutor`. Zero target freezes the attached
Linux container; a positive fractional target sets its CPU quota before unpausing; full target
restores the declared CPU allowance. A freeze-only job runs at full allowance for any positive
target. `IContainerControl` contains the four Docker operations; one CLI implementation keeps all
Docker calls together and may be replaced in Nikita's node runtime. `DigitalHeat.LiveRun` replays
a selected local day with the **same** `GreedyHeatScheduler` and `SchedulerController.Apply` as
the year simulation. Job descriptors supply metadata and sourced measurements; no example job
is implemented here.

## Alternatives considered

- A job-specific control API would exclude third-party workloads and duplicate Nikita's work.
- Stopping/restarting every hour would discard work unless a job declares checkpoint support.

## Consequences

The adapter controls arbitrary **Linux containers**, including unmodified programs inside them.
The replay requires a dedicated, prestarted container whose state it may own; cleanup resets its
CPU quota to the declared allowance and unpauses it. Each Docker CLI call has a 10-second demo
timeout; failure is reported instead of hanging the replay. This timeout is an operator policy,
not a physical or economic input. CPU quota is an upper bound, not a guaranteed achieved load.
Docker `stats` CPU percentage divided
by `100 × allowed CPU cores` is an observed CPU-use proxy, not measured electrical heat. A paused
container reports zero by command state. Each hour uses an independent reference tank snapshot;
a forced pause does not alter later snapshots. No physical sensor feedback is claimed.
Checkpoint support is descriptor-only until a job owner
provides a protocol. Docker's `--cpus` floor of 0.01 CPU is used for tiny positive requests.
Docker CLI references: [pause](https://docs.docker.com/reference/cli/docker/container/pause/),
[update](https://docs.docker.com/reference/cli/docker/container/update/),
[stats](https://docs.docker.com/reference/cli/docker/container/stats/).

## Review

Fresh-context verdict: **go with changes**. The reviewer found that Docker could hang, cleanup
could alter a shared container, and a forced pause did not affect later reference tank values.
We added a command timeout, required an exclusively controlled container, and labelled the tank
as independent reference snapshots. We kept throttle as a best-effort optional control because
the shared `IExecutor` already accepts continuous targets; freeze-only jobs map positive targets
to full allowance.
