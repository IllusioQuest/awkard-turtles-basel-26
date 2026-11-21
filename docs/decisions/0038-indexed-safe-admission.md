# 0038: Indexed admission and typed defensive copies

Status: proposed; user approved implementing T-47 performance recommendations. T-49.

Keep public scheduling/placement behavior and the atomic admission lock. Build immutable
inventory indexes after validation/defensive ownership: node/domain by ID, nodes by site/domain,
external allocations by node/domain. Publish inventory and indexes together. Compute owned
CPU/RAM/slot/heat/GPU aggregates once per decision instead of repeated per-candidate scans.
Retain stored domain identity for claims whose node disappeared and paused-batch retention rules.
Do not cache dynamic owned usage across mutations unless every mutation updates it atomically.

Replace JSON round-trip copies only for explicitly understood resource/inventory/request types,
using typed deep copies of every mutable array/policy allowlist. Immutable scalar records may be
shared. Keep JSON identity/journal compatibility and generic fallback for other record types.
Preserve ordinal tie-breaking, locality/storage/latency/freshness validation, GPU exclusivity,
decimal RAM/slot sums, defensive snapshots, unknown ownership and all deadline checks.

Validate old/new behavior with existing safety tests, seeded differential fixtures against a
test-only baseline, caller-mutation/concurrency/overflow cases, and the exact T-47 diagnostic sweep.
Report speed and allocation changes, including inventory-update cost. No promised speedup before
measurement. No journal flush removal, physical resource sharding or scored heat-model changes.

## Review

Fresh limited-context verdict: go with changes. Preserve per-group floating arithmetic order and
separate owned/external subtotals; copy before validating the owned snapshot; preserve nested
array isolation; compute effective claims after ignoreId/pause changes rather than subtracting
rounded totals. Keep generic journal/coordinator copying initially. T-48 settles first.
