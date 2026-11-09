# Reservation first slice — decisions D1–D4c

**Product decision date:** 5 November 2026. **Recorded:** 8 November 2026.
**Source:** lead-approved D1–D4c in `SmartPark_Reservation_First_Slice_Decisions_2026-11-05 (2).md`, supplied by the user, who explicitly requested implementation on 8 November.
**Status:** product decisions approved; implementation and acceptance pending. Proposed architecture in the source is not automatically approved.

## Scope and authorization

- **D1:** admit a durable request and return a receipt before asynchronous Account-current evaluation. Read-only preparation may overlap authorization. No reservation, capacity claim or payment-order intent before a timely, correlated Allowed and successful business checks. Denied and authorization unavailable are distinct outcomes.
- **D2:** use UserService's stable verified vehicle identity. Enforce incompatible interval/occupancy exclusivity across all lots, including live unpaid holds. A booking, its allocation and its linked visit represent one vehicle use. Two eligible vehicles on one Account may book concurrently. Remove the optional per-lot/per-Account active-booking cap; preserve cancellation restrictions and commercial capacity quotas.
- **D3:** new creation supports caller-owned verified CAR and Specific Slot only. Reject other categories/modes before claims or financial effects. Preserve legacy motorcycle/pooled data and compatible reads. Requested slot is a protected preference, not an exact-slot guarantee.
- Automatic allocation/recovery is in scope. Driver alternative-offer UI/expiry, switching/rescheduling endpoints, Zone/Capacity/motorcycle booking, discovery redesign and new monthly/yearly products are deferred. Existing applicable coverage remains part of price calculation.

## Time, recovery and rights

Use Operations authoritative time, UTC storage and accepted effective policy versions. Let T be booked start, E booked end, A allocation point, F facility-failure eligibility deadline and H payment deadline.

| Boundary | Approved behavior |
|---|---|
| A | T minus effective ALLOCATION_LEAD_TIME, default 1 hour. Start allocation at A or immediately on confirmation inside the window. Preserve priority, compatibility and existing rights. |
| Protection | Separate effective protection window, default 4 hours. Protection is not physical occupation or proof of fulfillment. |
| Recovery | Use compatible ordinary candidates then eligible backup during the allocation window. Reevaluate on relevant capacity changes and durable periodic retry. No T−5 backup/cancellation milestone. |
| F | T minus effective FACILITY_FAILURE_REFUND_LEAD_TIME, default 15 minutes. An unused confirmed booking lacking a usable compatible allocation earns the full applicable facility-failure refund right at F. |
| First confirmation or loss after F and before T | Try recovery immediately; if still unfulfilled, record eligibility then. A previously earned right survives successful recovery. |
| Exercise | Right remains exercisable until accepted actual entry makes the booking PARKING or the unused booking closes. Arrival alone does not end it. Earning a right creates no payout. |
| T, confirmed booking | Still unfulfilled, including ALLOCATED with an unusable assignment: cancel for facility failure, release unused rights and record one applicable refund intent and notification. Preserve a usable booking and any earned right; protect PARKING. |
| Ordinary no-show/expiry | With a usable assignment and no Driver action, apply ordinary no-show after the enabled cutoff, or expiry at E with late tolerance disabled. Timely verified arrival protects against incorrect no-show. |

**D4/D4a/D4b examples:** for T=10:00 and F=09:45, unfulfilled at 09:45 earns the right. Recovery at 09:47 preserves it. Gate arrival alone does not remove it; accepted actual entry does. At 10:00 an unused confirmed booking still lacking a usable assignment takes facility-failure cancellation, not voluntary cancellation or unpaid expiry.

## D4c — payment and confirmation cutoff

Approved Option A: `H = min(hold creation + effective PAYMENT_HOLD_DURATION, T)`; ordinary default 5 minutes. Disclose H before payment, pass it to Payment and enforce it server-side. Operations confirmation must **commit strictly before T**. Equality is late. Provider timestamps and browser redirects cannot backdate confirmation.

When H<T and the hold expires, compatible guarded reacquisition may succeed strictly before T. At/after T, an unconfirmed booking is EXPIRED even if the timer is late. Verified late money requires reconciliation/refund without entitlement. A pre-T confirmation remains recognizable after T on replay. Unknown collection/commit is not failure and does not justify another charge.

For a 10:00 booking created at 09:58, H=10:00. A provider debit at 09:59:58 first verified by Operations at 10:00:02 leaves the booking expired and creates a reconciliation obligation. This differs from failure of a booking already confirmed before T.

Full applicable refund is 100% of eligible collected value, accounting for prior refunds and unresolved payouts. Zero collected means no payout. Keep ordinary voluntary bands and monthly order timing separate. Facility-failure exercise/cancellation and unpaid expiry do not count as voluntary cancellation. The fifth qualifying voluntary cancellation remains permitted and blocks new creations until local midnight; independent rapid restrictions can extend beyond midnight.

## Evidence, ownership and supersession

Preserve timestamped assignment/usability evidence sufficient to evaluate F/T after downtime. Process due historical boundaries before later recovery. Missing or contradictory history requires reconciliation, not invented availability or denial of earned rights. Gate arrival, actual entry, commercial checkout and physical departure are distinct; only accepted physical departure releases occupation.

Supersedes the October 27 fixed T−15→T−5 window, T−5 backup trigger/cancellation and optional Account cap. Retains RA-01 allocation timing/ranking, protection-first projection, distinct-resource accounting, policy hierarchy and cancellation-abuse rules.

The user's 8 November implementation instructions supersede source proposals to add upgrade overlays or preserve the Operations confirmation trigger. Modify only original owning schema declarations, use idempotent CREATE and no new triggers/functions or noncircular ALTER. Do not remove required guarantees without C# replacements. Existing-volume transition is a separate reviewed deployment decision; initialization is not migration. The UserService authentication binding trigger, circular FK exception and role seed remain approved and unrelated to reservation confirmation.

## Implementation gates

- Actual commit-time evidence for D4c is OPEN; pre-commit timestamps do not establish it. Do not enable confirmation based on an unproved surrogate boundary.
- Current-access message trust, versioned quote/coverage acceptance and cross-owner resource handshakes need explicit contracts. A remote snapshot alone cannot make concurrent owner changes atomic.
- Package approval, runtime wiring, schema consolidation, competing-writer coordination and fresh acceptance evidence remain pending. No API is declared implemented by this decision.

See [API proposal](../../03-design/api/reservation-first-slice-api-design.md) and [task record](../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md) for implementation evidence and open questions.
