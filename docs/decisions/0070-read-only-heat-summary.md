# 0070 — Read-only committed-heat summary

Status: proposed. T-83. User approved a small read-only summary interface to
avoid copying entire attempts for heat calculations. No scheduling/ranking,
24/16/8 allocation, concurrency, quality or ownership behavior changes.

Add HeatCommitment(WorkloadId,HeatKwhPerHour,HourStartsUtc,Finished) as a detached
read snapshot, with each interval array copied. Coordinator.HeatCommitments()
projects only these values under its existing gate. MeshPeer forwards it;
RenderHeatNeed uses these exact fields with unchanged arithmetic. Add scalar
CommittedHeatKwh(hour) for MeshPeer.Publish to sum under the gate without building
or JSON-copying any attempts. No caching, no I/O or mutation in either query.

Keep full Attempts() and all runtime Discover/Observe/reconcile checks intact.
Unknown/reappearing ownership and retained-hour commitments remain fail-closed.
Verify summary/scalar equivalence to existing records, detached arrays, hour
boundaries and failed/cancelled retained commitments. Measure elapsed and
allocated bytes for the old copy/query path and new path on the same history.

Separately keep native warm process logs and exit evidence by process activation,
so a subsequent generation cannot overwrite evidence of the observed frame2
failure. No automatic retry or success claim without a completion marker.

## Review
Go with changes: Finished exactly matches released/terminal including failed attempts; preserve record order/heat values; copy all interval arrays under the gate; scalar query includes every phase and has no runtime side effects; capture per-activation log paths before async writes. Applied.132 tests pass, including equivalence, detached arrays and allocation checks.
