# T-24: Enforce job data locality before execution

- **Tier:** 1 Draft  ·  **Area:** Job catalog / placement
- **Depends on:** T-01 job descriptor, T-10/T-12 for multi-site routing
- **Suggested owner:** Codex  ·  **Owner:** Jona / Codex  ·  **Status:** done (PR #9 merged)

## Goal

A job can require execution at its data-origin site or within an explicit country allowlist for
privacy, security or legal reasons. A heat surplus must never override this restriction.

## Tasks

- [x] Add a sourced placement policy to the job descriptor and JSON schema. Keep unrestricted,
      data-origin-site and country-allowlist jobs distinguishable; reject unknown or incomplete
      policies rather than treating them as unrestricted.
- [x] Add a reusable eligibility check with explicit job origin and execution-site identity.
      Unknown location data must fail closed for restricted jobs.
- [x] Gate `DigitalHeat.LiveRun` before any Docker control action; test matching, mismatching and
      missing locations. Do not claim that a self-reported location is a security attestation.
- [x] Record the interface decision and coordinate the schema field with T-01's owner; require
      T-10/T-12 routing to use the same eligibility rule when those tickets are implemented.

## Done when

The descriptor loads both locality modes, invalid/unknown locality rules fail validation, a
restricted real job cannot run at a disallowed or unknown site through LiveRun, tests pass, and
the limitations of the current single-site runtime are documented.

## Read first

- `config/jobs/job.schema.json`
- `src/DigitalHeat.JobRuntime/JobDescriptor.cs`
- `src/DigitalHeat.LiveRun/Program.cs`
- `docs/tickets/T-01-job-catalog.md`

## Rules

- The user chose both locality modes selectable per job on 2026-11-21.
- The user chose the customer or their contract as the authority for each real job's placement
  rule. The provider records that rule; source text alone does not verify the permission.
- T-01 owns the broader catalog and always-on representation. T-24 changes only location policy.
- A country or site identifier must come from a trusted deployment inventory before this can be
  presented as a legal or security guarantee; the current CLI values are demo inputs only.

Implementation is in draft PR #9. T-01/T-02 descriptor migration is needed before merging;
the location check itself passed 31 .NET tests and a pre-Docker LiveRun denial smoke test.
