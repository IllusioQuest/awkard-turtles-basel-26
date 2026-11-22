# 0074: Mesh planning counts the origin's own unanswered proposals

- **Status:** proposed (user approved, 2026-11-22)
- **Date:** 2026-11-22
- **Decided by:** Nikita (team to accept); T-88 scheduler runtime

## Context

`MeshPeer.Tick` plans every waiting origin job with a **new** `WorkloadPlacement` built from the latest
peer offers. Real peers talk over a queued HTTP channel, so a target's reply and its next offer arrive
after the origin's following passes. Until then each pass sees the target as free again, and every
waiting job is proposed to the same best-ranked peer. A single-slot peer accepts one and rejects the
rest. Every rejection costs a journal rewrite with fsync at the target (the outcome is kept forever),
a reply, a new offer (another rewrite), and two rewrites at the origin (back to waiting, then proposed
again).

Measured in a Linux replica of the five-node render lab (2 s placeholder frames, 8 runs of 48 frames
after T-85): GPU 1 accepted 189 frames and rejected 1158 proposals, GPU 2 128/677, GPU 3 64/178;
344 of 399 origin jobs needed more than one proposal (up to 24). In one run the origin rewrote its
1.3 MB mesh journal about 16 times per frame, and GPU 1 its scheduler journals about 25 times per frame.
These rewrites grow with history and are the main remaining per-frame scheduler cost T-85 left open.

## Decision

Reuse the existing admission code: plan with **one `WorkloadPlacement` per pass and offer set**, so the
placement's own held claims (`TryReserve`) make each job planned in the pass use up slots and heat for
the next one. Before planning, hold this origin's own proposals at their target with a new thin
overload `WorkloadPlacement.TryReserveAtSite(request, site, now)`, which calls the existing private
`TryReserveCore`:

- a proposal that is still unanswered, or
- an accepted proposal while the target has not sent a newer offer since the one it was planned
  against (sequence kept in memory only),

unless the target's offer already lists the job as an allocation (then it is counted there).

Ranking (free heat share, ADR 0056), hard heat and resource admission at the target, durable intent
before send, retry of lost replies to the same target, withdrawn peers (ADR 0072) and the per-run heat
need (ADR 0069) are unchanged. No journal format change.

## Alternatives considered

- **At most one outstanding proposal per target:** simpler, but wastes a round trip per extra slot on
  multi-slot peers and still floods when a reply arrives before the target's new offer.
- **Persist the planned-against sequence in the journal:** survives restart, but changes the journal
  schema; after a restart the old behaviour (some rejections) is a safe fallback.
- **Compact the journals (archive finished history):** addresses growth from every source; larger
  durability change. Still worth doing later; this decision removes most of the writes first.

## Consequences

- Far fewer proposals, rejections and journal rewrites per frame; journals stop collecting rejection
  outcomes. Target-side admission still protects against stale offers from **other** origins (a peer
  cannot see another origin's proposals).
- Within one pass, jobs now spread across peers by free share (water-filling, as ADR 0056 intends)
  instead of all going to the first-ranked peer.
- Test `CompetingOriginsUsingStaleOffersCannotOversubscribeTarget` was adapted (user approved): B has
  one slot, origins A and C both propose to it from stale offers, B admits one, the rejected job is
  re-planned to a peer with room. New test `QueuedMeshDoesNotProposeMoreThanAPeerCanTake` (queued bus):
  the old planner caused 66 rejections for 12 jobs, the new one none.
- After an origin restart the in-memory sequences are empty: accepted proposals no longer hold
  capacity until the target's next offer, so a few rejections can reappear briefly (safe).
- Measured on the Linux replica (3 runs x 48 frames, 2 s placeholder frames, before = main 6f3ba99):
  proposals 1818 -> 150 for 150 jobs, rejections 1668 -> 0, GPU 1 mesh journal 174 KB -> 16 KB, C# CPU
  per run origin 7-9 s -> 2.4-3 s and GPU 1 6.5-8 s -> 2.2-2.7 s; run time 54.9 s -> 53.8-54.9 s (fsync is
  cheap on Linux; the Windows effect is not measured). GPU 3 power-off mid-run (ADR 0072), a run with it
  off, and a run after the lab restart all completed 48/48 with video (25/19/4, 29/19, 24/16/8).
  Evidence: `docs/performance/mesh-proposal-churn-2026-11-22.json`.

## Review

Fresh-context `decision-reviewer`, 2026-11-22: **go with changes**. No case of permanently blocked work;
withdrawn or expired targets drop out with their offers; free-share water-filling holds. Applied:

1. An accepted job could stop counting early when the target published a routine offer between
   planning and receiving the proposal. On an accepted reply the planned-against sequence is now raised
   to the latest offer seen from that target (both directions are FIFO). New test
   `AcceptedJobStillHoldsTheSlotWhenARoutineOfferOvertakesTheProposal` fails without this change.
2. The "already counted" check now looks at the merged planning allocations, not only the target's
   offer, so `WorkloadPlacement` cannot throw on an ID another offer lists.
3. A new proposal is also held in the other planners of the same pass that include its target (mixed
   job profiles create several planners).
