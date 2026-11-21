# 0017: Always-on entries in the existing job catalog

- **Status:** proposed; user approved one catalog with batch/always-on
- **Date:** 2026-11-21
- **Decided by:** Nikita; team confirmation pending

## Decision
Extend existing JobDescriptor with optional serviceMode, default batch for old files.
Preserve existing camelCase input fields; new hosting plan/export JSON remains snake_case.
Batch descriptors still require interruptible=true and freeze support.
Always-on requires interruptible=false and an empty supportedControlLevels list; no batch deadline.
Loader accepts both modes; LiveRun explicitly rejects always-on BEFORE any Docker action.
Do not imply that DockerJobExecutor itself can orchestrate service availability.
Add separate always-on frontend and API entries with null unmeasured runtime/energy, current
component images, and explicit sourced local-lab placement. Both components are required for
the website; catalog presence alone is not a composite deployer or billing model.
YearSimulation and finite batch JobScenario stay unchanged.
Extend the existing schema/loader, no parallel catalog or parser. T-01 owner notified in docs.

## Review
Pending focused review of compatibility and the pre-execution guard.
