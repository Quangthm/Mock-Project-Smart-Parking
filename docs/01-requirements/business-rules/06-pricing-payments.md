# Pricing Payments — business rules RC3

[Rule index](README.md) · [SRS module](../srs/modules/06-pricing-payments.md)

## BR-PAY-01

**Status:** CONFIRMED — implementation pending / not assessed

Payment & Pricing owns versioned Owner day/night time-block tariffs by category/location/time. Use actual applicable overlap to accepted checkout, no ceiling of elapsed duration to a tariff block. Sum unrounded segments then half-up once to nearest 1,000 VND. Split midnight intervals without restarting exemption. Night prices do not depend on guest status; no stacked guest-only surcharge. Display covered intervals, reserved minimum, extras, prepayment credit, balance and surplus distinctly. Event/holiday/demand pricing remains FUTURE.

**Trace:** FR-PAY-01/02/10; UC-POL-01

## BR-PAY-02

**Status:** CONFIRMED — implementation pending / not assessed

Show approved extra-use charges and configured overstay notices while occupancy remains recorded. No invented per-block penalty, fee or debt follows merely from a timer or notification failure. Additional fees require separately approved/published configuration; unapproved demo amounts are inactive.

**Trace:** FR-PAY-08; UC-NOT-01

## BR-PAY-03

**Status:** CONFIRMED — implementation pending / not assessed

payOS QR receives implementation priority for MVP online payment. VNPay, MoMo and ZaloPay remain separate planned options, with integration or fixture status explicitly identified. If resources are insufficient, payOS QR provides the working online-payment path; unfinished native provider integrations remain explicitly simulated or deferred. payOS transactions must not be presented or recorded as native VNPay, MoMo or ZaloPay transactions. Payer-application compatibility is claimed only after verification. Assigned-Operator CASH_COLLECT remains unchanged. PDF invoice behavior remains the domain contract. Other additional provider/card channels remain future extensions. The acceptance profile is defined by PAY-01; choosing a payer app does not change the actual processing provider recorded on a transaction.

**Trace:** FR-PAY-03/07, FR-BAS-16; UC-PAY-01, UC-PAY-02

## BR-PAY-04

**Status:** CONFIRMED — implementation pending / not assessed

Verify provider transaction identity, order, amount, currency and payee through the trusted adapter contract; screenshots/client redirects are not proof. Deduplicate callbacks and internal operations; preserve attempts and reconcile late/duplicate money. UNKNOWN requires lookup of the same operation before another collection or cash switch. Distinguish failed, pending and unknown outcomes. If payment succeeds after the reservation hold expires, reserve it when the requested capacity is still free; if a competing claim has already won the race, notify the losing Driver and make the paid attempt eligible for refund/reconciliation. Allow three additional payment attempts after the initial attempt only after definitive retryable failure; four total. Command replay or callback redelivery consumes no attempt. UNKNOWN requires lookup/reconciliation. Issue replay-safe PDF invoices for settled financial obligations and linked adjustments; never mark a refund paid from approval alone.

**Trace:** FR-PAY-04/05; UC-PAY-01

## BR-PAY-05

**Status:** FUTURE — outside MVP

INACTIVE historical exit quote/clearance/requote proposal. No quote-expiry gate or actual-physical-exit pricing is active. B04 accepted checkout freezes billing; see FR-PAY-11. Retained ID is not authorization to implement this proposal.

**Trace:** FR-PAY-11, FR-GATE-05; UC-GATE-02

## BR-PAY-06

**Status:** CONFIRMED — implementation pending / not assessed

Ordinary free threshold is 15 minutes per whole session to accepted checkout, inclusive. Once exceeded, charge full applicable uncovered duration, not duration minus 15 minutes. Covered time stays free but counts toward elapsed threshold: 60 covered + 10 uncovered minutes charges the uncovered 10. Disclosed booked-duration minimum survives a short reserved visit. Reservation minimum and prepayment obligations remain as disclosed; excess follows E01.

**Trace:** FR-PAY-09/11; UC-GATE-02

## BR-REF-01

**Status:** CONFIRMED — implementation pending / not assessed

Scoped Operator handles ordinary appeals/mispricing; Owner handles escalation and monthly cancellation; Payment executes payouts. Reserve refundable collected balance atomically, including pending/UNKNOWN refunds. Approval is not completion; actual payout proof is required. Automatically initiate excess-deposit refund through original route where supported, or audited cash return for original cash. Covered commercial checkout may complete while surplus payout remains pending. No cancellation penalty on surplus. Voluntary reservation defaults: ≥30 minutes before start 100%, ≥15 and <30 minutes 50%, <15 minutes 0%; no-show 0%. Base is eligible money actually collected, with no collection of unpaid remainder as a penalty. Facility failure refunds 100% eligible unused value; pricing correction is difference-only at original cutoff. Sum unrounded eligible amounts, round half-up once to 1,000 VND, then apply remaining collected-money cap. No automatic offset against unrelated debt.

**Trace:** FR-PAY-06, FR-INC-05; UC-INC-02
