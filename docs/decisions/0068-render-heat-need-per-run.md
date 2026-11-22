# 0068: Render GPU weights are heat need per run

- **Status:** proposed (user approved, 2026-11-22)
- **Date:** 2026-11-22
- **Decided by:** Nikita (team to accept)

## Context

The presentation render lab (ADR 0065, `rendering/presentation-profile.json`) splits a synthetic
500 kWh hourly heat budget over five nodes by weight; the GPU nodes get 150/100/50 kWh (weights
1.5/1/0.5). Each frame reserves 0.8 kWh for the whole clock hour (`retainHourlyHeat`). So in one hour
GPU 3 can take about 62 frames, GPU 2 about 125, GPU 1 about 187. In a replica of the lab, the frames
per GPU per run went 16/16/16, then 17/17/14, then 24/24/0, and the eighth run stalled at 38/48 until
the next hour (T-80, `docs/jobs/render-app-overhead.md`). As long as heat is far from used up, the
free-share rank (ADR 0056) also splits work evenly (16/16/16), so the weights have no visible effect.

The user's intent: the weights mean how much heat each node **needs**, and the scheduler should
compare nodes by that need, not use the weight as an allowance that runs out.

## Decision

Keep the weights and keep the heat-first policy and its code unchanged (ADR 0051/0056); change only
how a render GPU node fills its existing `HeatSignal.DemandKwh`:

- At the start of each run the origin (`RenderSession`) splits the run's frames over the GPU nodes
  by weight with a largest-remainder rule (48 frames → 24/16/8) and sends each node its share
  (`POST /run-need`, stored durably on the node).
- The node then publishes `DemandKwh = heat it committed this hour for other work + (its share ×
  frame heat − heat of the current run's frames that finished in earlier hours) + 1e-6 kWh`. The free heat the
  scheduler sees is therefore exactly the node's remaining need for the run.
- The unchanged placement fills nodes by free share and never gives a node more than its free heat,
  so a run is split in proportion to need and a node whose need is met waits. Need starts again with
  each run, so repeated runs in one hour no longer stall.

Enabled by `gpu_heat_need_per_run` in the presentation profile (passed to all nodes as
`heat_need_per_run`). CPU roles keep their hourly allowance. Without a registered run a GPU node
uses its configured demand, as before.

## Alternatives considered

- **Raise the hourly budget (e.g. 5000 kWh):** removes the stall, but the weights still have no
  visible effect (16/16/16) and the budget keeps its "allowance" meaning.
- **Weight as priority only** (pick the neediest free GPU, never block): fast and no stall, but in
  practice still about 16/16/16, so the user's need split is not visible. Offered to the user and
  not chosen.
- **Per-node frame heat, or a new rank in Core:** would change the shared heat accounting or ADR 0056
  for every caller (fleet, network, mesh, gaming). Not needed for the lab.
- **Pin frames to nodes in `RenderSession`:** would bypass heat-first placement (AGENTS.md rule 3).

## Consequences

- A run now takes about as long as the busiest node's share: with equal frame times, 24 frame-times
  instead of 16 (about 50 % longer on the replica: 56 s instead of about 38 s with 2 s frames). This
  is the accepted cost of a visible need split.
- Hourly heat commitments are still recorded; the demand simply grows with each run. The numbers
  remain synthetic lab inputs, not measured heat.
- A run that crosses the hour keeps only its uncovered need (a frame running across the boundary is
  counted once in the new hour).
- A stopped run leaves its remaining need on the nodes until the next run registers a new one; every
  render node gets a share (also 0) at each run start, so no leftover survives a new run.
- No slack by design: a GPU that dies mid-run keeps its share and the run waits for it. Recovery is
  Stop, then Render again (the new run is split over the GPUs that are up). Registration failure
  fails the run visibly instead of silently splitting over fewer GPUs.
- Diagnostics that submit frames without registering a need (`Measure-RenderConcurrency.py`) only get
  the leftover need of the last run; they should register one first or run with the flag off.
- The page shows "Heat need: N frames this run (kWh) · weight W" for GPU nodes.

## Review

Fresh-context `decision-reviewer`, 2026-11-22: **go with changes.** The formula is correct in steady
state and placement code is untouched, so heat-first is not bypassed. Applied:

- Only finished frames (`released`/`terminal`) from earlier hours count as covered. Otherwise a frame
  running or paused across the hour boundary was subtracted twice (before the coordinator re-commits it
  for the new hour) and the run could stay stuck. Test added.
- One `RunPrefix` helper for the job-ID prefix, used by both the status filter and the need registration.
- Every render node gets its share at each run start (including 0), so no node keeps old leftover need.

Not applied before the demo: re-splitting a run when a GPU dies mid-run. That is documented under
Consequences, with Stop → Render as the recovery. An end-to-end check on the five-node replica lab gave
24/16/8 frames in every run (see T-80).
