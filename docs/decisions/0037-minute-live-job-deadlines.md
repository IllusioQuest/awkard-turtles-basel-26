# 0037: Minute-level deadlines for bounded live batch jobs

Status: proposed; user approved minute-level live deadlines and the targets/expiry rules below.
2026-11-21; T-48. Hourly/scored simulation and active games stay unchanged.

Add optional immutable absolute UTC deadline metadata to WorkloadRequest: submitted time, due
time, source and expiry action. Omit null metadata from JSON so existing untimed container spec
hashes/journals remain compatible. Only finite operator-declared batch requests use the new field.
Central profiles: 300 s for one transcription/summary input up to 15 audio minutes; 600 s for
one finite research batch up to 320 tasks. These are user-approved experimental targets grounded
in existing local runtime measurements, not demonstrated SLAs or power estimates on this laptop.
Large/unbounded work requires an explicit budget; no retroactive deadline for an active game.

Use the existing WorkloadPlacement and WorkloadCoordinator. Pure placement rejects expired
requests at its supplied planning time. The live coordinator uses an injected TimeProvider for
checks before create/start/resume and after blocking runtime calls. Waiting and startup consume
the budget. Overdue work is persistently marked late; only owned batch work with AllowStop may
be stopped using the existing graceful, journalled stop/remove/confirmed-release path. Never
release resources from an expired time alone, reset a due time on retry, or relaunch an old ID.

Extend container observations with optional exit code and runtime finish time as evidence, not
unqualified trusted wall-clock proof. Completed-on-time requires successful completion observed
before the deadline; first terminal observation after it is timing-unknown unless lateness was
already established. Marking a deadline missed does not claim an instant hard kill or success.

Add an explicit owner `watch-deadlines` mode to the existing WorkloadRun CLI, not a new console UI
or write API in the read-only monitoring service. It holds the existing fleet lock, orders a finite
submission manifest by due time, attempts admission, polls deadlines and reports outcomes as JSON.
Its immutable manifest supplies submission timestamps across restart; no new public/distributed
queue. It must remain running for timely expiry enforcement; after a crash, reconciliation retains
uncertain ownership before new starts. Document this operational limitation plainly. Operator
profile application validates declared input bounds; it does not inspect/validate private input files.

## Review

Fresh limited-context verdict: go with changes. Applied independent durable outcomes/stop intent,
post-blocking clock checks, immutable manifest validation before mutation, polling/clock limits,
and a pre-change journal/hash compatibility fixture. Tests cover restart, paused/late work and
uncertain stops. Successful early completion is persisted before cleanup; stop never implies success.
