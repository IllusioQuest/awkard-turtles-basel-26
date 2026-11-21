# T-10: Generic sites and network model

- **Tier:** 2 Pitch  ·  **Area:** Core / model
- **Depends on:** T-09
- **Suggested owner:** Codex  ·  **Owner:**  ·  **Status:** open

## Goal

Simulate several sites (house, hotel/spa, farm, office) connected in a network, all defined as parameters.

## Tasks

- [ ] Config schema `config/scenarios/*.json`: sites (from the WP3 parameter list in `docs/research/sites-and-profiles.md`), servers per site, hardware profile, storage, eligible jobs/data rules, location.
- [ ] Network graph: links with distance/latency and allowed data flows (data sovereignty).
- [ ] Give execution sites verified site IDs and country codes for T-24 job-placement checks;
      treat unknown identity as ineligible for restricted jobs.
- [ ] Heat demand per site type: case formula for the reference house, profiles (When2Heat, BDEW) for others.
- [ ] Scenario `basel-neighbourhood`: reference house ×N + one year-round site (hotel/spa).
- [ ] Reference house stays exactly as in the case (scored figures unchanged).

## Done when

- A scenario with ≥ 3 site types runs through the year simulation; the reference-house results are unchanged.

## Read first

- `docs/research/sites-and-profiles.md`
- `data/raw/sites/`

## Rules

- Follow AGENTS.md: agree the approach with your human first (§10), build on the shared C# core and data (§9a), every number with a source (§5), update `docs/status.md`.
