# 0008: Per-site heat state and forecast (T-09)

- Status: proposed; user authorized ticket implementation on 2026-11-21; T-00 team choices remain open
- Date: 2026-11-21
- Owner: Nikita / Codex

## Context

T-09 requires needed heat, maximum produced heat and a forecast per site. The existing
HeatSignal and IScheduler/IExecutor interfaces must keep working. Site demand must not be
counted independently for every server in one building. The original photo is still unavailable;
this proposal follows the written T-09 ticket, not an asserted interpretation of the photo.

## Proposed decision

Add an immutable SiteHeatState wrapping site identity, the existing HeatSignal, step timestamp,
and a copied read-only hourly forecast with explicit provenance. The current hour is in HeatSignal;
forecast index zero is the next hour. Derive maximum full-load recoverable energy per hour from
ServerCount times RecoverableKwhPerServerAtFullLoad rather than storing a contradictory second value.
Expose NeededHeatKwh and MaxHeatProducedKwh as named derived properties. Site hardware parameters
remain configurable; no new server count or heat capacity is chosen.

A forecast builder takes an existing hourly demand series, index and configurable horizon.
Perfect foresight copies subsequent demand exactly and is labelled as such. A synthetic-error
variant applies caller-supplied fractional errors per hour, clamps demand to zero, and is labelled
synthetic, not a calibrated weather forecast. No random generator or extra library is needed.
Reject nonfinite/negative demand, invalid tank state, duplicate/blank site identity at the collection
boundary, invalid horizon and insufficient future data; never pad missing hours silently.
Keep IScheduler unchanged; add ISiteHeatScheduler.Decide(SiteHeatState) as the optional forecast-aware entry point. An adapter delegates to existing IScheduler using state.Signal for the greedy baseline. Forecast use
in routing/optimisation belongs to later tickets. No scored annual calculation changes here.

## Alternatives

Changing IScheduler now would disrupt the separate runtime branch. Global maxHeat constants would
prevent different hardware. A second thermal policy would duplicate the shared core.

## Validation planned

Perfect foresight equals source demand; index excludes current hour; input mutation cannot change
snapshots; error bounds/finite values and missing future data are checked. Existing scheduler and
annual tests must remain unchanged and pass. Units remain kWh per hourly step, distinct from kW.

## Review

Verdict: go with changes. Add the optional forecast-aware entry point while preserving IScheduler/IExecutor; specify provenance and indexing; validate hardware and arithmetic. Defer collection identity checks until a collection exists. Forecast metadata: kind, sourceSeriesId, sourceIndex, local hour label under the existing Europe/Zurich 24-values-per-day convention. Current and forecast demands are gross site demand, not net of tank discharge. Synthetic error formula: max(0, actualKwh * (1 + fractionalError)); errors must be finite and >= -1, lengths must match, outputs and derived capacities must remain finite. Hardware values must be finite and positive. User authorized proceeding with ticket work on 2026-11-21. T-09 introduces no new hardware/service choice; T-00 remains open.
