# Reservation confirmation, identity and arrival amendments — 9 November 2026

These are explicit user decisions for SPARK-273. They amend the [5 November first-slice decisions](2026-11-05-reservation-first-slice.md); they are not claims of implemented or tested behavior. The current deliverable is booking through confirmed reservation. Payment/Facility/coverage producers remain with their owners, and frontend work is a handoff.

## Confirmation boundary — superseded on 10 November

The 9 November recorded-commit-timestamp design is historical and has been replaced by the user's [final evaluation-checkpoint decision](2026-11-10-reservation-evaluation-checkpoint.md). Capture database time once after guards/current facts are acquired, evaluate strictly before T, and expose confirmation only after durable commit. No candidate/evidence activation stage or commit-metadata tracking is required. Other decisions below remain in effect.

## Approved identity and workflow checks

- Conflict identity is canonical **plate**, across sites, including unregistered guest visits. Vehicle ID remains the registered vehicle reference, not the sole physical identity.
- UserService's existing protected binding lookup may return its stored canonical plate for an eligible Account/vehicle pair. Operations must not trust a browser-supplied plate.
- Account-current and vehicle binding each have one accepted durable result per workflow. Later progression reuses it. A crash before the first result commits may repeat the physical evaluation; this is not an exactly-once remote execution guarantee.
- Concurrent removal/transfer/category/plate changes after that accepted binding result can compromise the accepted booking state. The user explicitly accepts and requires documenting this gap. The change does not authorize enabling otherwise-disabled vehicle identity edits.
- Allow walk-in admission despite a future booking for that plate. Actual concurrent physical visits remain exclusive. Handle a reservation becoming due while the vehicle is already parked through the fulfillment/conflict workflow; do not add a blanket future-booking entry prohibition.

## Approved form and payment interaction

Show a calculated amount on the form as slot/time change, before proceeding with the selected payment method. Vehicle, interval and method inputs can be completed in any order; do not require a frontend layout rewrite just for ordering. Backend independently computes authoritative prices and issued terms. A browser amount is never an order amount authority.

After all required fields are present, review server-issued commercial and operational terms; changed terms require renewed acceptance. Submit the accepted intent once with an idempotency key and observe its receipt. Once an actual Payment order exists, use it for checkout. After payment, read the same receipt/own booking; a browser redirect is not payment proof. Trusted owner evidence drives progression independently of the browser. See the [frontend handoff](../handoffs/2026-11-09-reservation-frontend-handoff.md).

## Approved arrival/scan intent; next phase

Use authoritative physical gate check-in time to distinguish early arrival, not when a later scan is processed. For check-in before T, ordinary early usage is chargeable. If the vehicle occupies another slot, a scan/operator observation should initiate reallocation, release its unused original protection, and recover affected future claimants within their allocation lead window. Preserve physical occupancy and do not displace another vehicle. Repeated observations must not release twice or create repeated fees/events. Selecting the intended slot needs no wrong-slot recovery. Timing is a policy condition, not proof of malicious intent.

For wrong parking, charge a separate fee added to normal session payment. Admin defines the default and bounds; the Owner may override within those bounds for the parking lot. Waive the fee when the assigned slot is unusable or an Operator/system authorized the different slot. Arrival exactly at T is **on time**, not early or late. Enabled late tolerance retains its documented minimum of 15 minutes and existing 15–30 minute range; no-show remains strictly after the applicable deadline. Late-tolerance enablement still needs an explicit site choice.

The user explicitly kept this phase **through confirmation**. Scan, reallocation and checkout are next-phase work, not implemented by the current booking draft. Retain time-stamped assignment usability, authorization and observation evidence so a delayed scan does not invent a violation or double charge. Emit durable facts for the audit owner; do not add an audit repository.

## Settlement clarification and remaining next-phase decisions

The user confirmed **3,000 VND** due: four booked hours cost 4,000, plus one early hour costs 1,000, minus 2,000 already collected. Full booked price remains due even when only one booked hour is used. This agrees with `RESERVED_MINIMUM / FINAL_SETTLEMENT`; do not waive the unpaid balance or refund unused booked time solely because the visit is short. The five-hour example likewise adds the applicable extra hour and credits the deposit once.

OPEN for the next phase: numerical wrong-parking default/bounds (or formula), how to resolve missing/conflicting scan/assignment evidence, and the exact fee eligibility predicate for an on-time wrong-slot arrival. The user established exact-T punctuality, not that punctuality alone waives a wrong-parking fee. Ask before implementing an unspecified predicate or amount. These decisions do not block the currently selected booking-through-confirmation phase.

Keep reservation operational defaults in the approved database revision model and [separate seed](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql). Site late-tolerance enablement still requires an explicit choice. Do not add a wrong-parking default, full catalogue APIs, an audit repository, SQL overlays, new functions/triggers or general ALTER statements. Audit consumers and retention belong to the audit owner; producers use durable events.
