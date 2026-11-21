# 0051 — One heat-first scheduler policy, adapters per execution model

Status: proposed; user explicitly approved removing old schedulers and selectable modes, 2026-11-21.

Keep one Core policy for useful-heat load and eligible-site ranking. Latency, sovereignty,
resource capacity, deadlines and lifecycle are eligibility constraints, never competing heat
weights. Among eligible sites rank remaining useful heat descending, then ordinal stable ID.
Order pending jobs by deadline then ID; do not run urgent work without useful heat. Keep active
interactive work assigned. Pause interruptible batch first. Retain hourly admitted heat.

Replace Network B1/B2/reserve/LP and Fleet mock selection with model adapters around that
policy; remove their executable implementations and selectable modes, not historical research
results or original evidence. Different interfaces for hourly cohorts, event-driven chunks and
containers remain necessary; adapters must not invent their own heat or origin-first rule.
Keep Core's existing heat balance and executor/controller interfaces. Single-site finite/mixed
YearRun keeps its work ledger/executor but uses the same heat policy; run-and-dump mode is
retired because it intentionally ignores heat. Invalid old mode IDs fail explicitly.

Change only scheduler-owned policy, wiring/configuration and directly affected tests/scripts.
Do not remove data, economics, old documentation, game launchers or active containers.
Reference-house unlimited outputs must remain equal hour by hour. Test finite/mixed work
conservation/deadlines, no-heat/no-start, topology/latency constraints and deterministic ties.
Multi-site results may intentionally change from B2/LP; publish this as policy change, never
rewrite historical scored evidence. Measure placement and complete execution separately.
Fleet performance needs event-local updates/indexes, not scanning every queued job on every
clock tick. Full-mesh network costs remain O(peers squared) for offer dissemination.

## Review
Fresh limited-context review: go with changes. Core owns stable EDF/deadline-ID ordering and heat/site-ID rank. Adapters retain their existing work ledgers and lifecycle eligibility. Full-hour heat claims never reopen on completion/pause; hour changes reset the accounting. In physical Fleet runs actual tank/demand must be reconciled without replenishing the admission ledger on repeated events. Mandatory active interactive/checkpoint continuation is an explicit exception, with its heat included in physical outputs. Validate same-hour repeated arrivals, heat loss, and ties across job types.

