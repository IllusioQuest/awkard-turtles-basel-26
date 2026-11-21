# T-51: Four-node live heat and work demo

- **Tier:** 2 Pitch
- **Owner:** Jona / Codex
- **Status:** in progress
- **Depends on:** T-03, T-12, T-30, T-43

## Goal

Run four independent Docker containers on one laptop as four simulated heat-network nodes. A
full-screen local view shows four large node panels and makes real scheduler decisions, work
progress and pause/resume visible. This is the first stage toward two laptops with four containers
each.

## Agreed direction

- Test one laptop first; later extend to two laptops.
- Use the existing Type 2 Ising research simulation because its seeded tasks are independent and
  results are atomic per task.
- Each container is a distinct node with its own node ID and heat signal. Use the shared C# core
  scheduler path for load decisions.
- Use the Funkenläufer animation as a visual encoding of actual task units, not a substitute for
  work.
- A UI target temperature is only a demo input mapped to a heat-demand signal; it is not a room
  sensor reading. The node view stays deliberately minimal: four edge-to-edge room-color panels,
  one gradient temperature slider and animated work in each, with no dashboard header or prose.

## Implementation notes

- Four-node local task claims and result commits are described in decision
  [0040](../decisions/0040-local-four-node-demo.md).
- `DigitalHeat.LiveGrid` is the local full-screen UI/API; slider input immediately updates the room
  color and is serialized through the existing core scheduler and Docker executor.
- The four Ising workers run continuous queue passes. A finite one-pass container exits as soon as
  the initial demo queue is complete and cannot demonstrate live pause/resume.
- Ising simulation tasks are indivisible work units. A paused node leaves its current unit in place
  unless its lease expires; peers claim subsequent units from the shared queue. Do not claim a task
  is subdivided.
- Visual worker movement is triggered by per-node completed-task counts. It represents queue
  progress after a node pauses; it does not make a separate scheduling decision.

## First milestone

- Reproducible offline start/stop instructions for four containers on one laptop.
- Per-node heat slider, large visual state, actual scheduler load, task progress and pause behavior.
- Verify work is claimed by at most one container and results survive pause/restart.
- Demonstrate task distribution/movement through real task assignments and result files.
- Preserve the existing annual model and scored reference-house inputs.

## Later

Extend node discovery/communication across two laptops, record relative layout for cross-screen
animation, and measure actual hardware behavior. Do not claim this from the local prototype.
