# T-23: Connect the webhosting platform to the heat scheduler

- **Tier:** 2 Pitch  ·  **Area:** Runtime / integration
- **Depends on:** T-03
- **Suggested owner:** Nikita  ·  **Owner:** Nikita / Codex · **Status:** in progress

## Goal

Use the existing webhosting work where it helps the case: as executor platform and as an always-on base-load workload.

## Tasks

- [ ] Check which parts of the webhosting node runtime T-03 can reuse (container control, pause recovery) and agree the boundary with the T-03 owner.
- [ ] Model webhosting as a job type (always-on, small, fits the hot-water base load ≈ 0.6 servers) in the job catalog (T-01).
- [ ] Keep webhosting scope from blocking the draft (tier 1 first).

## Done when

- Boundary agreed and documented in `docs/status.md`; webhosting appears as a job type.

## Read first

- `docs/webhosting-prototype.md`
- `docs/research/service-brainstorm.md (#13)`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.

Progress: shared-runtime experiment passed; draft PR #7. See docs/webhosting-heat-runtime.md for commands, measured observations and T-01 handoff. Current schema mandates interruptible=true and freeze support; an always-on hosting descriptor cannot be added honestly without owner agreement. No second scheduler/executor added.

Approved common catalog extension is now implemented with frontend/API always-on descriptors and an explicit LiveRun batch guard, under decision 0017. 51 core/catalog tests pass; code PR pending. Measurement values remain null and no always-on load was added to scored YearSimulation.
