# 0011: Versioned result export contract

- **Status:** proposed; user approved export direction with snake_case JSON
- **Date:** 2026-11-21
- **Decided by:** Nikita (implementation approval; team confirmation pending)

## Context
T-13 needs dashboard-readable results without duplicating the C# model.
Existing YearRun output and reference figures must remain unchanged by default.

## Decision
Add optional --output DIRECTORY for a version 1 export bundle, keeping legacy output by default.
Use System.Text.Json snake_case property names and UTF-8 CSV with invariant numbers.
Bundle summary.json, parameters.json, run.json, sites/reference-house/hourly.csv and flows.csv.
Summary lists the explicit reference site; parameters preserve all source-labelled configuration
and the job scenario. Run metadata records reproducible arguments, input SHA-256 hashes,
Git revision and dirty state, null seed (no randomness), timezone and hourly time convention.
The header-only flows file is explicitly marked not_modelled, never evidence of zero migrations.
Export from existing YearResult only; no new heat, scheduling or economic calculations.
Reject non-empty output directories to avoid silently mixing runs.
Keep exporter in YearRun, independent of core calculation logic; no new dependency.
Commit a generated reference showcase; document its precise generation revision.

## Alternatives considered
Changing the existing summary shape would break current consumers; opt-in avoids that.
A database or new serialization package adds unnecessary setup for local offline dashboards.

## Consequences
Consumers use schema_version and site IDs; future multi-site routing must extend the contract.
Input loader schemas are unchanged; snake_case applies to the new exports.
No private workload payloads, credentials, environment variables or absolute host paths are exported.

## Review
Pending fresh-context review.
