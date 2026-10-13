# Monthly Passes — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## M-01

**Status:** CONFIRMED — implementation pending / not assessed

Prepaid calendar-month cycle, not first-of-month billing. For a future purchase, start S is the chosen local date at00:00. For start-now, S is backend entitlement commitment time A. End E is the same local day/time next month, clamped to that month's last day if necessary. Preserve ORIGINAL anchor day/time across consecutive renewals: Jan31→Feb28(or29)→Mar31, not Mar28. Feb15→Mar15→Apr15. Rights cover [S,E); at E the old period is no longer valid. Do not treat one month as30days. Full-price calendar periods only, including a clamped February period; no partial-month sale in the initial extension. Store the contract timezone with the original day/time; later lot-timezone edits cannot shift purchased periods. If additional timezones with daylight-saving changes are supported, their ambiguous/missing-time rule must be defined first.

**Trace:** FR-MON-01–14; UC-MON-01, UC-MON-02, UC-MON-03

## M-02

**Status:** CONFIRMED — implementation pending / not assessed

Order creation O; monthly hold 15 minutes; future purchase/renewal H=min(O+15 minutes,S), start-now H=O+15 minutes. Do not create an activatable order without a payable interval. Full verified money and entitlement commitment A must satisfy A<H; equality is late. Renewal opens 7 days before final committed E and appends there; max three prepaid consecutive calendar periods. Late repurchase starts a new non-retroactive anchor. No automatic charge, future-period debt or parking-capacity hold.

**Trace:** FR-MON-01–14; UC-MON-02, UC-MON-03

## M-03

**Status:** CONFIRMED — implementation pending / not assessed

Separate order PENDING_PAYMENT/FULFILLED/EXPIRED/CANCELLED, money PENDING/SUCCESS/FAILED/UNKNOWN and entitlement SCHEDULED/ACTIVE/EXPIRED/CANCELLED. FULFILLED requires verified money and committed rights. Monthly paidAt before H with commit at/after H does not activate: reconcile/refund. Under/overpayment or currency/order/payee mismatch does not automatically grant rights. Screenshots are not proof. One provider transaction funds one order; duplicate callback is idempotent, distinct duplicate collection separately reconciled/refunded. Later failed callback cannot erase success; reversal is separate. UNKNOWN requires lookup before retry. Refund approval is not payout. Validity is checked against timestamps/revocation even if scheduler labels lag. Available-only order/entitlement creates no parking-capacity hold.

**Trace:** FR-MON-01–14; UC-MON-02, UC-MON-03

## M-04

**Status:** CONFIRMED — implementation pending / not assessed

An available-only pass reserves no capacity while outside. Each visit requires actual compatible availability or an independently confirmed ordinary reservation. Count occupation once; no guaranteed-capacity/slot product is active. Operations owns coverage rights; Payment owns financial paid periods. Exclude valid ordinary day/night covered intervals from money, but evaluate the 15-minute exemption against whole session to accepted checkout. Never restart grace at monthly boundaries. Separate service fees remain separate; no EV charging feature is implied. Covered 60 + uncovered 10 minutes charges only the uncovered 10. Actual occupancy survives pass expiry; only departure releases it.

**Trace:** FR-MON-01–14; UC-GATE-01, UC-GATE-02, UC-MON-02, UC-MON-03, UC-MON-04

## M-05

**Status:** CONFIRMED — implementation pending / not assessed

Voluntary monthly cancellation requires A-01 dependent-reservation resolution first: show affected future bookings and require their explicit Driver cancellation. No automatic cancellation/repricing/charge. Revalidate dependency set with final cancellation; C is backend accepted cancellation after prerequisites, not first click. Owner handles cancellation, Payment executes refund. Before S: 100% eligible prepaid money. During [S,E): P×((E−C)/(E−S))×50%; at/after E: zero unused value. Future paid periods are evaluated separately. Sum unrounded period values once, half-up to nearest 1,000 VND, then cap against remaining eligible collected money including pending refunds. No repeated compensation of same interval/receipt or offset against unrelated debt; occupancy survives. Facility failure uses its separate 100% eligible unused/unavailable-value policy without voluntary penalty or A-01 forced pre-cancellation.

**Trace:** FR-MON-01–14; UC-MON-01, UC-MON-05

## M-06

**Status:** CONFIRMED — implementation pending / not assessed

Preserve accepted paid periods against later price or sales changes. Temporary closure keeps pass active and end date unchanged, refunding verified unavailable interval value once. Permanent closure cancels remaining coverage and refunds eligible unused value at 100%, excluding previously compensated overlap. No automatic extension; ordinary fullness is not facility failure. Verified temporary intervals are intersected with each paid [S,E), unioned and valued by paid price × unavailable duration / period duration. Remove previously compensated overlap before later closure/cancellation refund. Preserve reasons, original periods and financial history.

**Trace:** FR-MON-01–14; UC-MON-01, UC-MON-05

## M-07

**Status:** CONFIRMED — implementation pending / not assessed

Overlapping available-only coverage must not duplicate sold rights or charges. A monthly pass creates no reservation automatically; Driver may separately reserve capacity. Apply valid coverage before disclosed booking minimum (E03); if genuinely zero, confirm without payment, even under fixed-deposit mode, with ordinary atomic capacity and cap checks. Record entitlement/version/coverage basis. A-01 prevents voluntary pass cancellation while future reservations still depend on removed coverage. Guaranteed-resource lifecycle is FUTURE, not offered or accepted in MVP.

**Trace:** FR-MON-01–14; UC-MON-02, UC-MON-04

## M-08

**Status:** CONFIRMED — implementation pending / not assessed

Repurchase while parked validates existing vehicle/session and eligibility without synthetic entry or second occupancy deduction. Before new S remains uncovered, after S is covered only if valid; no retroactivity. Use accepted checkout cutoff, not later physical exit, for billing. Preserve legitimate prior charges and do not relocate occupants automatically.

**Trace:** FR-MON-01–14; UC-MON-03, UC-MON-04

## M-09

**Status:** CONFIRMED — implementation pending / not assessed

Plans bind account, vehicle, lot/zone and available-only entitlement. Vehicle/lot/type transfers and suspend-and-add-days are unsupported in MVP; use disclosed cancellation/new purchase with A-01 dependency guard. Preserve accepted financial versions; Operator cannot edit paid periods to postpone money or grant credit.

**Trace:** FR-MON-01–14; UC-MON-01, UC-MON-05
