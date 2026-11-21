# T-43: Universal resource-aware workload placement

Owner: Nikita / Codex. Status: in progress (design; awaiting user choices).
Planned code branch: feat/T-43-universal-workloads, created from current main after decisions.

## Request and scope

User approved the direction of reusable workload placement rather than transparent distribution
of an unchanged game's internal calculations. Ask for important choices, and commit/push docs
directly to main. Existing running game, author worktrees and browser-only access must be preserved.

Proposed milestone: structured CPU/RAM/GPU/heat/locality/lifecycle requirements, node capability
and physical-resource inventory, atomic admission, shared heat ranking, Docker-context executor,
explicit lifecycle observations and deterministic offline tests. Same admission decision path for
fake and real executors. Existing heat physics and scored year calculations remain unchanged.

## Pending user decisions (asked in chat)

1. C# + Docker contexts and snake_case profiles (recommended), or evaluate Kubernetes first.
2. First end-to-end example: Minecraft server + browser client, existing Xonotic + batch
   (recommended for first proof), or a scheduler-only dry run.
3. Minecraft persistent worlds: node-local storage plus explicit backup/restore (recommended),
   or design replicated storage before implementation.

Previously agreed: protect active matches; heat-rank new placements; pause batch first;
server failure may require reconnecting, with no seamless active-match migration promise.
Minecraft Java entitlement/browser client and server EULA details must be established before
installing/starting Minecraft if that example is selected. Never request account passwords.

## Design and acceptance

ADR 0033 is proposed, not accepted. No new dependency, DTO boundary or lifecycle behavior is
implemented while the above choices are pending. Coordinator durability/HA and exact GPU
allocation policy must also be agreed before claiming fault-tolerant automated operation.

Acceptance: invalid/unknown resources fail closed; resource and heat claims are atomic; concurrent
last-capacity admission tested; pinned workload/locality/storage/latency guards; CPU versus
RAM/GPU reclamation distinguished; no duplicate launch after retries; release only after positive
observation. Use fake adapters for failures and an isolated real Docker smoke; preserve the active
game. Document sourced estimates and missing power/latency measurements. One original game
process remains on one node, and one Minecraft world is not automatically sharded.
