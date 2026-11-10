# Reservation first-slice acceptance scenarios

These scenarios describe product behavior, not implementation or execution evidence. References: [reservation rules](../../01-requirements/business-rules/04-reservations-allocation.md), [policy catalogue](../../01-requirements/business-rules/policy-catalogue.md), [authorization freshness](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md).

| Scenario | Expected behavior |
|---|---|
| Durable admission before current-access evaluation | Return a receipt only after admission is durable; create no booking, claim, order or payment effect while authorization is pending. Restart resumes the same request. |
| Denied, late or mismatched authorization | Denial produces no effect. A response from an expired attempt or different actor/resource correlation cannot advance the request. The workflow has one durable decision; retries replay its original result without a fresh access evaluation. A current timely delivery envelope may carry that earlier decision timestamp. |
| Two different verified cars belonging to one Account | Both can reserve if their own resource/interval constraints pass; no Account-wide booking-count cap. |
| Same stable verified car across lots or live unpaid holds | Reject conflicting effective intervals across sites. Ongoing occupancy remains a conflict until observed physical departure, even after expected end. Plate formatting is not stable identity. |
| New reservation eligibility | Only the caller's verified CAR and Specific Slot mode can enter this creation flow; existing other-mode records remain readable. Requested slot is a preference, not observed location or an exact-slot guarantee. |
| Policy configuration and historical terms | Resolve approved platform/site revisions from persisted configuration; missing required values fail. Require explicit site late-tolerance enablement. Later policy changes do not rewrite accepted values or deadlines. |
| Allocation window | Evaluate allocation/recovery from A=T−effective allocation lead; backup is available during that window. No independent T−5-minute milestone is introduced. Protection and allocation windows remain distinct. |
| Facility-failure eligibility | At F, an unused confirmed unfulfilled booking earns the right. Confirmation or loss after F and before T prompts immediate recovery evaluation, then earns if unresolved. Recovery and arrival retain an earned right; actual parking or unused-booking closure ends it. Earning is not payout. |
| At booking start | Cancel an unused confirmed booking that cannot be fulfilled with one facility-failure refund intent and one notification. Preserve usable or already parked bookings. |
| Hold/confirmation deadline | H=min(creation+effective hold,T). Confirmation eligibility must be evaluated strictly before T under the required guards; expose the result only after durable commit (10 November amendment). Expired holds may be reacquired only before T; money arriving too late is reconciled/refunded without entitlement. Unknown outcomes remain unknown pending reconciliation. |
| Commercial acceptance | Preserve authoritative accepted versioned quote and coverage facts. Missing producers or stale terms cannot silently become zero-price approval or invented coverage. |
| Physical entry/departure and audit | Actual entry requires a usable allocation; requested preference alone cannot authorize entry. Payment preparation does not mean physical departure. Persist state change and audit event atomically; no event on rejected/rolled-back writes or completed departure replay. |
| Audit consumer unavailable | Configured durable capture survives broker interruption/restart; consumer deduplicates event IDs. Missing audit configuration prevents effectful Gate writes. Audit retention belongs to the audit service. |

Read-only projections distinguish pending receipt, booking lifecycle, payment/refund, arrival, actual parking, physical departure, requested resource and observed location. No status shortcut may imply an unverified effect.
