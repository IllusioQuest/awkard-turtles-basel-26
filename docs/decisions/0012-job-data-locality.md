# 0012: Job data-locality eligibility

- **Status:** proposed
- **Date:** 2026-11-21
- **Decided by:** Jona / Codex, following the user's choice of both per-job modes

## Context

The job descriptor and the single-site LiveRun can control arbitrary containers but cannot say
where a job's data may be processed. T-01 owns the broader catalog; T-10/T-12 will add routing.
A heat benefit must not send a restricted job to an ineligible site.

## Decision

Require a sourced `placementPolicy` on each job descriptor: `anywhere`, `origin-site`, or
`country-allowlist` with explicit ISO 3166-1 alpha-2 country codes. `origin-site` compares the
data-origin site ID supplied for a job instance with the execution-site ID; a country allowlist
compares the execution site's country code. Put a pure, fail-closed eligibility check in Core so
LiveRun and future routing can call the same rule. LiveRun checks before Docker control. The
customer or their contract sets the policy; the job provider records it but cannot grant the
permission. The `source` must identify the customer authorization or contractual clause.

## Alternatives considered

- A single `local` Boolean cannot distinguish a data-origin site from a legal jurisdiction.
- A free-text locality note cannot be validated or used by the scheduler.
- Defaulting a missing policy to unrestricted could silently expose data; reject it instead.

## Consequences

Current descriptors need an explicit policy. T-01 and T-02 owners must add one when they merge
their jobs. CLI-supplied site/country identifiers are demo inputs, not trusted attestation or
legal compliance. T-10/T-12 must obtain verified location identity and apply the shared check
before assignment; the current single-site year balance has no geographic routing. The loader
checks that a source exists, but it cannot verify the authority or terms of a contract.

## Review

Fresh-context verdict: **go with changes**. The reviewer required matching schema and loader
validation, a guard before Docker control, and a Core-only shared policy type. We implemented
those checks and kept location attestation and multi-site routing outside this draft change.

