# Pricing Payments — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

Defaults are versioned per applicable policy; Owner configurability remains within Admin bounds. Approval establishes these exact initial examples as defaults, not unrestricted additional fees or monetary approval thresholds.

| Reason | Approved default | Boundary / base |
|---|---|---|
| Ordinary voluntary cancellation ≥30 min before start | 100% | Eligible collected reservation money under accepted terms. |
| Ordinary voluntary cancellation ≥15 and <30 min before start | 50% | Exact 15 min belongs here; exact 30 min belongs to 100% band. |
| Ordinary voluntary cancellation <15 min before start | 0% | Only when cancellation remains permitted by lifecycle; this band does not authorize cancelling a parked visit. |
| No-show | 0% | Applicable enabled policy and authoritative arrival rules still govern classification. |
| Monthly voluntary cancellation before S | 100% | Eligible prepaid amount for not-started period. |
| Monthly voluntary cancellation during [S,E) | 50% of unused value | P × ((E−C)/(E−S)) × 0.5, actual timestamps; no fixed 30-day approximation. |
| Yearly voluntary cancellation | Separate configurable accepted yearly policy; values design-pending | Monthly percentages above are not yearly defaults; preserve A-01 dependencies, collected-money caps and rounding. |
| Facility failure | 100% of eligible unused value | No voluntary penalty; ordinary fullness is not failure of an available-only capacity guarantee. Temporary closure retains pass with once-only verified unavailable-interval refund; permanent closure cancels remaining coverage (§16.9). |
| Pricing miscalculation | Correct overcollection / collect shortfall | Not reduced by voluntary-cancellation rate. |

At/after period E, unused-period value is zero. For multiple paid periods, calculate per-period eligible amount using accepted rates, sum unrounded values, round once half up to nearest 1,000 VND, then apply the remaining eligible-collected-money cap. No overlapping/double refund, no automatic offset against unrelated debt, no physical occupancy release merely because a refund/entitlement cancellation was recorded. Operator handles ordinary appeals; Owner escalations/monthly or yearly cancellation; Payment executes money. Accepted appeal/approved refund does not mean payout completed.

For a nonnegative total x, conceptual rule: `rounded = 1000 × floor(x / 1000 + 0.5)`. Use exact decimal calculation; do not pre-round segments or convert negative accounting signs into a separate unapproved convention. Refunds are computed as nonnegative magnitudes with ledger direction represented separately.

| Unrounded amount | Final rounded amount |
|---|---|
| 2,412 VND | 2,000 VND |
| 2,499.50 VND | 2,000 VND |
| 2,500 VND | 3,000 VND |

The initial 2,499.50 → 3,000 example is explicitly superseded by the user’s later “2,500 boundary” clarification. Do not first round to whole VND and then to thousands.

Assigned, authorized Operator initiates/completes an audited correction **only for system pricing miscalculation**. Recompute at original accepted checkout cutoff; correction delay is not billed. Payment & Pricing calculates and executes the adjustment; Owner handles escalations. Compare corrected rounded charge with actual attributable money already collected, preserving previous refunds/adjustments in reconciliation; no second blind correction against an already-refunded original total. Positive shortfall is collected, excess refunded, equal amounts need no adjustment. Persist old/new calculation, accepted terms, reason, actor, transaction and invoice links. Exact multi-adjustment ledger contract remains implementation work.

No full-refund/full-recharge route in MVP. No reopening a paid visit for continued parking: actual exit and re-entry are required for a new session. Audited unpaid checkout invalidation remains supported, without a new grace period. Physical departure and capacity release remain separate from payment success/gate opening. Cumulative refunds cannot exceed eligible collected money; the cap has precedence over rounding if legacy/provider balances are not multiples of 1,000.

Positive-price reservation uses a 5-minute payment hold and Owner prepayment FULL (default), PERCENTAGE >0–100%, or FIXED >0 with no policy upper cap, even above booked charge. Round payable money once; a positive-price deposit rounded to zero requires validation. Apply valid monthly/yearly coverage before the booked minimum; genuinely zero-price reservations confirm explicitly without provider payment, even under FIXED mode, while atomically checking capacity and cap.

Completed reserved visit owes disclosed minimum plus eligible extras; prepayment credited once. Short visit does not waive booking obligation. Cancellation/no-show applies accepted refund rate only to eligible collected prepayment; no unpaid-balance penalty. Example booked 100,000 / deposit 30,000: completed visit owes remaining 70,000 absent other adjustments, but no-show creates no additional 70,000 debt. Final charge 125,000 / collected 300,000: automatically initiate exactly one 175,000 surplus refund; covered checkout may complete before payout. Unsupported online-route refunds remain pending integration/reconciliation issues, never silently replaced by cash. Fixed high deposit is not an event-pricing engine or damage bond.

## Functional requirements

### FR-PAY-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW02

Payment & Pricing owns versioned Owner day/night time-block tariffs by category/location/time. Use actual applicable overlap to accepted checkout, no ceiling of elapsed duration to a tariff block. Sum unrounded segments then half-up once to nearest 1,000 VND. Split midnight intervals without restarting exemption. Night prices do not depend on guest status; no stacked guest-only surcharge. Display covered intervals, reserved minimum, extras, prepayment credit, balance and surplus distinctly. Event/holiday/demand pricing remains FUTURE.

### FR-PAY-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D02, US-O02

Payment & Pricing owns versioned Owner day/night time-block tariffs by category/location/time. Use actual applicable overlap to accepted checkout, no ceiling of elapsed duration to a tariff block. Sum unrounded segments then half-up once to nearest 1,000 VND. Split midnight intervals without restarting exemption. Night prices do not depend on guest status; no stacked guest-only surcharge. Display covered intervals, reserved minimum, extras, prepayment credit, balance and surplus distinctly. Event/holiday/demand pricing remains FUTURE.

### FR-PAY-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Payment Provider  
**Stories:** US-D02, US-O02

payOS QR receives implementation priority for MVP online payment. VNPay, MoMo and ZaloPay remain separate planned options, with integration or fixture status explicitly identified. If resources are insufficient, payOS QR provides the working online-payment path; unfinished native provider integrations remain explicitly simulated or deferred. payOS transactions must not be presented or recorded as native VNPay, MoMo or ZaloPay transactions. Payer-application compatibility is claimed only after verification. Assigned-Operator CASH_COLLECT remains unchanged.

Controlled, low-value payOS production testing plus deterministic payment failure/race fixtures replaces the one-real-sandbox-adapter requirement. VNPay native integration may remain simulated/deferred when resources are insufficient; MoMo/ZaloPay fixture arrangements remain. No separate working sandbox adapter is required. Real-money checkout exposes functioning routes only; demo fixtures are explicitly labelled and isolated from real-money data and effects. Fixture-only results cannot satisfy the required payOS real integration evidence.

### FR-PAY-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Payment Provider  
**Stories:** US-D02, US-O02

Verify provider transaction identity, order, amount, currency and payee through the trusted adapter contract; screenshots/client redirects are not proof. Deduplicate callbacks and internal operations; preserve attempts and reconcile late/duplicate money. UNKNOWN requires lookup of the same operation before another collection or cash switch.

### FR-PAY-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Payment Provider  
**Stories:** US-D02, US-O02

Distinguish failed, pending and unknown outcomes. If payment succeeds after the reservation hold expires, reserve it when the requested capacity is still free; if a competing claim has already won the race, notify the losing Driver and make the paid attempt eligible for refund/reconciliation.

### FR-PAY-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Owner, Payment service  
**Stories:** US-D10, US-O05, US-OW04, US-OW07

Scoped Operator handles ordinary appeals/mispricing; Owner handles escalation and monthly/yearly cancellation; Payment executes payouts. Reserve refundable collected balance atomically, including pending/UNKNOWN refunds. Approval is not completion; actual payout proof is required. Automatically initiate excess-deposit refund through original route where supported, or audited cash return for original cash. Covered commercial checkout may complete while surplus payout remains pending. No cancellation penalty on surplus.

### FR-PAY-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Notification Service  
**Stories:** US-D06

Generate the PDF invoice required by the domain contract and expose the Driver’s own payment/refund history and verified financial statuses; notification delivery follows the domain contract. Cross-account financial lookup is denied.

### FR-PAY-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Notification Service  
**Stories:** US-D11, US-OW02

Show approved extra-use charges and configured overstay notices while occupancy remains recorded. No invented per-block penalty, fee or debt follows merely from a timer or notification failure. Additional fees require separately approved/published configuration; unapproved demo amounts are inactive.

### FR-PAY-09

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D02, US-OW02

Ordinary free threshold is 15 minutes per whole session to accepted checkout, inclusive. Once exceeded, charge full applicable uncovered duration, not duration minus 15 minutes. Covered time stays free but counts toward elapsed threshold: 60 covered + 10 uncovered minutes charges the uncovered 10. Disclosed booked-duration minimum survives a short reserved visit.

### FR-PAY-10

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Driver, Operator  
**Stories:** US-D02, US-OW02

Payment & Pricing owns versioned Owner day/night time-block tariffs by category/location/time. Use actual applicable overlap to accepted checkout, no ceiling of elapsed duration to a tariff block. Sum unrounded segments then half-up once to nearest 1,000 VND. Split midnight intervals without restarting exemption. Night prices do not depend on guest status; no stacked guest-only surcharge. Display covered intervals, reserved minimum, extras, prepayment credit, balance and surplus distinctly. Event/holiday/demand pricing remains FUTURE.

### FR-PAY-11

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Payment service, Device Gateway  
**Stories:** US-D02, US-O02, US-OW02

Accepted verified checkout freezes billable cutoff; commercial settlement completes when charge is covered. Physical departure remains separately recorded and is the only occupancy release. Audited unpaid recovery may resume the same session without fresh grace only after resolving UNKNOWN money. Never reopen a paid stay for continued parking: require actual exit/re-entry. System mispricing uses difference-only correction at original cutoff, retaining transaction/invoice history.

### FR-BAS-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Driver  
**Stories:** US-D02, US-OW02

Dynamic pricing is an MVP capability using Owner-configured time blocks; event/holiday/demand triggers remain future.

### FR-BAS-16

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D02, US-O02

Allow three additional payment attempts after the initial attempt only after definitive retryable failure; four total. Command replay or callback redelivery consumes no attempt. UNKNOWN requires lookup/reconciliation. Issue replay-safe PDF invoices for settled financial obligations and linked adjustments; never mark a refund paid from approval alone.

[PAY-01 decision](../../../05-project-management/decision-log/2026-10-13-payos-priority-pay-01.md) · [payOS adapter design](../../../02-architecture/payment-integration/payos.md) · [Integration test plan](../../../04-testing/payment-integration/payos-test-plan.md)
