> Completed prototype: user-verified Xonotic browser gameplay and Intel graphics experiment merged in PR #22 (56501a7). CS2 remains deferred, not verified. Reusable runtime follow-up completed in T-40; current guide: [../gaming.md](../gaming.md).

# T-39: Intel browser gaming compatibility experiment

Owner: Nikita / Codex. Branch: featur-gaming. Status: done: verified Xonotic prototype (PR #22); CS2 and live distribution deferred
User approved browser clients, no game code changes, Linux workloads and an Intel compatibility experiment. Active sessions retain resources, new sessions follow heat placement, batch pauses first; failure permits reconnect.

## Goal

Show that a game can run unmodified on a Linux node, be played from a browser (Selkies) and be placed by heat capacity across several servers. C# remains the orchestration language; no new heat scheduler.

## Done criteria

- Done: two to three Selkies desktops with auth, separate homes, loopback access (ADR 0021).
- Done: Intel OpenGL accelerated, Dozen Vulkan experiment (ADR 0023), original Xonotic playable in the browser (user-confirmed).
- Done: heat-capacity reservation model with shared `HeatCapacity` helper (ADR 0022), runtime profiles (T-40).
- Pending: automatic launch after reservation, observed batch preemption, termination reconciliation, remote nodes, zero-capacity policy decision, CS2 (deferred).

Commands, architecture, sources, measurements and limitations: [../gaming.md](../gaming.md).
