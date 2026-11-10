# Reservations Allocation — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

| Parameter | Confirmed default | Configuration authority | Meaning |
|---|---|---|---|
| RAPID_CANCELLATION_COUNT | 2 | Admin only | Qualifying cancellation events within rolling window trigger restriction at threshold. |
| RAPID_CANCELLATION_WINDOW | 15 minutes | Admin only | Counts cancellation timestamps, not whether booking creation also occurred inside the window. |
| RAPID_CANCELLATION_RESTRICTION_DURATION | 3 hours | Admin only | Begins at triggering cancellation; another qualifying trigger extends to three hours from that event. |
| DAILY_CANCELLATION_LIMIT | 5 | Admin only | Fifth qualifying cancellation is allowed; new bookings blocked for the remainder of that local day. Further existing reservations can still be cancelled. |
| DAILY_CANCELLATION_TIMEZONE | Asia/Ho_Chi_Minh | Fixed baseline time basis | Calendar day is [00:00, next 00:00), not rolling 24 hours. |
| CANCELLATION_ALERT_THRESHOLD | 50 cancellations | Admin platform policy | Approved suspicious-volume threshold over the window below. |
| CANCELLATION_ALERT_WINDOW | Rolling 30 days | Admin platform policy | Retain history and alert evidence; separately identified suspicious patterns can also raise alerts. |

**Counted event:** accepted Driver voluntary cancellation of a distinct reservation, counted once from the authoritative cancellation event. Exclude duplicate transport retries, failed cancellation attempts, facility/system cancellation and expiry of unpaid holds. Track suspicious unpaid-hold abandonment separately; do not treat each abandoned hold as a completed cancellation or invent a numeric weighting. No cancellation removes historical evidence.

**Restriction scope:** new reservation creation only. Keep login, existing booking details, payments/active-session information, cancellation of existing reservations, appeals and support available. This is not authentication lockout, an automatic ban or automatic cancellation of existing rights. Booking confirmation already in flight requires an explicit command/race contract; do not silently confiscate paid existing rights.

**Expiry:** retain independent rapid and daily restriction facts. For each accepted cancellation that meets the rapid rule, rapid expiry becomes at least event time + configured duration. At/after the daily threshold, daily expiry is next midnight in the stated timezone. Effective expiry is the later applicable expiry. Further cancellations do not get rejected because the daily count is already five. New-day counters do not erase a rapid restriction crossing midnight.

| Example | Expected outcome |
|---|---|
| Cancellations at 10:00 and 10:12 | Second triggers rapid restriction until 13:12. |
| Another qualifying cancellation at 10:20 | Rapid restriction extends to 13:20; retry of that command does not extend again. |
| Fifth daily cancellation at 16:00 with rapid expiry 19:00 | Daily restriction lasts until local midnight; later expiry wins. |
| Fifth at 23:00 also triggers three-hour rapid rule | Restricted until 02:00 next day, not merely midnight. |
| Existing reservation cancelled while restricted | Allow if ordinary cancellation conditions hold; count once and evaluate trigger extension. |

**Alert handling:** OPEN → RESOLVED or SUPPRESSED (ignore is a recorded suppressed disposition). Store Admin actor/time/comments and retained linked evidence. Suppression does not delete events, end booking restrictions or stop collection. Implement deduplicated episodes and defined suppression duration; exact alert retrigger/suppression mechanics are design work, not a hidden permanent exemption.

**Technical ownership contract:** Operations owns cancellation facts and atomic booking eligibility/restriction enforcement; Admin configures global policy through authorized Operations APIs. Notification & Audit projects/delivers alerts and audit history. Keep enforcement authoritative in Operations even if an alert projection is delayed. User & Access owns actual authentication and current grants; do not misuse account-disabled for this restriction. UI shows the reason and effective unlock time.

Allocation begins at `reservation_start - effective ALLOCATION_LEAD_TIME`. The confirmed default Allocation Lead Time is 1 hour. Late-created confirmed reservations whose allocation point has already passed are evaluated immediately by the same idempotent command. Resolve the effective value through the approved policy hierarchy; permitted Owner/lot overrides must remain within Admin-defined approved bounds. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions.

**Allocation timing example retained from the earlier SRS:**

```text
Driver A reservation: 09:30–11:00
Driver B reservation: 10:00–12:00
effective ALLOCATION_LEAD_TIME: 30 minutes

Driver A allocation point: 09:00
Driver B allocation point: 09:30
```

The 30-minute value is illustrative only; it does not establish a new approved bound/default. If Driver A becomes confirmed at 09:10, its 09:00 allocation point has already passed, so Operations evaluates it immediately using the same idempotent allocation command.

**Occupied protected-slot example:** a requested/associated slot may be physically `OCCUPIED` by Driver A while Driver B's future reservation protection is active, producing `OCCUPIED + PROTECTED`. This does not fulfill Driver B. If Driver A leaves, the slot may become `AVAILABLE + PROTECTED`; if Driver A remains when allocation/fulfillment requires a usable resource, apply compatible alternative/reallocation/conflict handling without displacement.

`OCCUPIED` does not identify the reservation holder. `OCCUPIED + RESERVED` may represent the reservation-linked Driver after accepted entry, or an unrelated current occupant while a reservation association exists. Authoritative session/allocation/reservation linkage establishes fulfillment.


Positive-price reservation uses the effective `PAYMENT_HOLD_DURATION` (default 5 minutes) and Owner prepayment FULL (default), PERCENTAGE >0–100%, or FIXED >0 with no policy upper cap, even above booked charge. Round payable money once; a positive-price deposit rounded to zero requires validation. Apply valid monthly/yearly coverage before the booked minimum; genuinely zero-price reservations confirm explicitly without provider payment, even under FIXED mode, while atomically checking compatible capacity, verified-vehicle exclusivity and restrictions. H is capped by T; confirmation eligibility must be evaluated strictly before T under the required guards; expose the result only after durable commit (10 November amendment).

Unpaid hold expiry is EXPIRED, distinct from NO_SHOW. Late tolerance is optional: enabled default 15 minutes with Owner range 15–30; disabled retains entitlement until reservation end. Arrival at/before applicable deadline is timely; no-show requires strictly later decision and no authoritative timely evidence.

Ordinary hold payment/expiry is serialized at Operations: a verified result evaluated while the hold is active converts it once on successful commit; once expiry wins, reacquire a compatible claim only strictly before T or reconcile/refund. At/after T no unconfirmed entitlement may be granted, regardless of provider timestamp or timer delay. This is deliberately distinct from strict monthly A<H. Offer acceptance requires authoritative time < expiry; equality is expired and underlying entitlement is resolved independently.

## First-slice boundaries — approved 5 November

[D1–D4c](../../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md) supersede optional Account caps and the October 27 fixed T−15→T−5 recovery schedule. Product approval is separate from implementation evidence.

- D1: return a durable pending receipt and evaluate Account-current asynchronously. Read-only preparation only before Allowed; no reservation, capacity claim or payment-order intent.
- A = T − effective allocation lead (default 1 hour). Use compatible backup inside the allocation window and retry on relevant changes plus durable periodic work. No T−5 milestone. Preserve protected preferences, stable allocations, real occupancy and distinct-resource accounting.
- F = T − effective facility-failure lead (default 15 minutes). An unused confirmed booking without a usable compatible assignment earns full applicable refund eligibility at F. First confirmation/loss after F and before T tries immediate recovery, then earns eligibility if still unfulfilled. Recovery never removes an earned right.
- The Driver can exercise the right until actual accepted PARKING or unused closure. Gate arrival alone leaves it exercisable. Eligibility alone creates no payout; zero collected means no payout. Exclude facility-failure exercise from voluntary abuse counts.
- At T, confirmed but unfulfilled (including unusable ALLOCATED) cancels for facility failure with one applicable refund intent/notification and unused-claim release. Usable and PARKING bookings survive. Ordinary no-show/expiry applies when a usable slot is supplied and no Driver action follows.
- H = min(hold creation + effective payment hold, T), ordinary default 5 minutes. Operations confirmation eligibility must be evaluated strictly before T under the required guards; expose the result only after durable commit (10 November amendment). Pre-T expired holds may reacquire only if compatible. At/after T unconfirmed bookings expire; late money reconciles/refunds without a booking. Use one database evaluation timestamp at the final guarded checkpoint; no provider/browser timestamp or commit-time deadline recheck.
- Retain timestamped usability/assignment history and process F/T before later recovery after downtime. Missing history requires reconciliation, never backdated availability.

Examples: T=10:00, F=09:45, recovery=09:47 preserves the earned right. A 09:59:58 debit first verified at 10:00:02 leaves an unconfirmed booking expired. Snapshot effective policies without inventing override bounds.

## Functional requirements

### FR-POL-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW02

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locking remains for policies not superseded by RA-06; the numeric defaults listed by RA-06 are configurable within Admin-defined bounds. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

### FR-POL-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A02

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locking remains for policies not superseded by RA-06; the numeric defaults listed by RA-06 are configurable within Admin-defined bounds. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

### FR-POL-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Admin  
**Stories:** US-A02, US-OW02

Expose a named, typed, scoped policy catalogue with units and effective values.

### FR-POL-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Admin  
**Stories:** US-A02, US-OW02

Validate required values, supported ranges, exhaustive/non-overlapping policy bands and overlapping time-block configuration before activation; reject invalid configurations with a reason (BR-POL-02).

### FR-POL-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Admin  
**Stories:** US-A02, US-OW02

Record policy revisions and the policy/price version applied to an accepted booking or transaction; later policy revisions apply to future applicable transactions.

### FR-POL-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D01, US-OW02

Present backend-resolved scope, price, prepayment, booked minimum, extra-use terms, deadlines and refund policy before confirmation. Changed terms require renewed review; persist immutable accepted Operations and Payment versions.

### FR-POL-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin, Owner  
**Stories:** US-A02, US-OW02

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locking remains for policies not superseded by RA-06; the numeric defaults listed by RA-06 are configurable within Admin-defined bounds. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

### FR-POL-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin, Owner  
**Stories:** US-A02, US-OW02

Support draft policies, scheduled activation and revision history within assigned scope; invalid drafts cannot authorize transactions (BR-POL-02/03).

### FR-POL-09

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Admin, Recognition Service  
**Stories:** US-A07

Retain Admin-audited recognition configuration direction. Exact recognizer, score scale, threshold/equality, schemas and evaluation are POST-v1.0; historical confidence numbers are not approved current gates. Multi-frame violation detection remains FUTURE.

### FR-RES-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D01

For this slice, an authenticated Driver reviews their verified CAR, location, interval and Specific Slot mode. Reject unsupported category/mode before claims or payment effects; Zone/Capacity/motorcycle creation and Driver offer/reschedule endpoints are deferred. Preserve legacy data and compatible reads. Specific Slot protects a preference on a best-effort basis, not an exact-slot guarantee. Backend validates accepted terms, current access, booking restrictions and compatible claims before acceptance.

### FR-RES-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D01

Reservation minimum duration, maximum duration, advance horizon and inter-booking buffer are configurable policies. Confirmed defaults are 15 minutes, 24 hours, 7 days and 0 respectively. Resolve Admin default/bounds → authorized Owner/lot override → accepted context; no numeric bounds are invented by RA-06. No per-lot/per-Account active-booking cap. Enforce stable verified-vehicle interval exclusivity across all lots, including live PENDING_PAYMENT holds and actual occupancy; count a booking, allocation and its own linked visit once. Two eligible vehicles on one Account may book concurrently. Reject incompatible same-vehicle overlap; enforce global cancellation-abuse restrictions for new creations without disabling login or existing rights.

### FR-RES-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner, Operator  
**Stories:** US-D01

Apply the baseline allocation/protection behavior with Specific Slot and Allocation Lead Time in this slice; Zone and Capacity creation are deferred. These reservation modes are distinct from the underlying allocation and protection behavior.

### FR-RES-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Payment Provider  
**Stories:** US-D01

Positive-price reservation uses the effective `PAYMENT_HOLD_DURATION` (default 5 minutes) and Owner prepayment FULL (default), PERCENTAGE >0–100%, or FIXED >0 with no policy upper cap, even above booked charge. Round payable money once; a positive-price deposit rounded to zero requires validation. Apply valid monthly/yearly coverage before the booked minimum; genuinely zero-price reservations confirm explicitly without provider payment, even under FIXED mode, while atomically checking compatible capacity, verified-vehicle exclusivity and restrictions. H is capped by T; confirmation eligibility must be evaluated strictly before T under the required guards; expose the result only after durable commit (10 November amendment).

### FR-RES-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D01

Show booking identifier, QR/ticket, vehicle, requested versus allocated place, interval, lifecycle/reallocation history, reasons, accepted terms and applicable payment/arrival/offer deadlines. Payment/refund/physical visit statuses are separate; Driver sees only own authorized records.

### FR-RES-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01

Unpaid hold expiry is EXPIRED, distinct from NO_SHOW. Late tolerance is optional: enabled default 15 minutes with Owner range 15–30; disabled retains entitlement until reservation end. Arrival at/before applicable deadline is timely; no-show requires strictly later decision and no authoritative timely evidence.

### FR-RES-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-D01, US-O01

Early admission requires actual compatible capacity and separately disclosed applicable extra interval fees. Otherwise offer waiting or a feasible compatible alternative; do not displace occupants or consume incompatible prior rights.

### FR-RES-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D10

Validate voluntary cancellation against current lifecycle and accepted terms; do not cancel a parked visit as an unused booking. Release unused claims once, count the accepted cancellation once for abuse policy, and calculate refund from eligible collected money. No additional unpaid-balance penalty. Refund execution is independent of cancellation and cannot clear occupancy.

### FR-RES-09

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Owner  
**Stories:** US-D10, US-O05

Recover lost capacity using conforming alternatives and established priority without displacing occupied/unaffected users. An earned facility-failure refund right survives a usable conforming recovery. Without that earned right, refusal follows voluntary-cancellation terms; unresolved fulfillment follows the F/T boundaries below. Owner handles escalations; assigned Operator executes scoped recovery.

### FR-RES-10

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D05

Show booking identifier, QR/ticket, vehicle, requested versus allocated place, interval, lifecycle/reallocation history, reasons, accepted terms and applicable payment/arrival/offer deadlines. Payment/refund/physical visit statuses are separate; Driver sees only own authorized records.

### FR-RES-11

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-D01, US-O01

Record verified gate arrival separately from entry, with source/event ID, actor, occurred/received times and evidence. Browser GPS is not authoritative arrival. Operator resolves genuine simultaneous-arrival ties and delayed-evidence mistaken no-show with history; recover compatible capacity without displacement or apply failure handling if impossible.

### FR-RES-12

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Payment Provider  
**Stories:** US-D01, US-D10, US-O01

Apply guarded, versioned transitions PENDING_PAYMENT → CONFIRMED → ALLOCATED → PARKING → COMPLETED and explicit cancellation, unpaid expiry, rejection and no-show paths. Reallocation, refund, dispute and violation are linked histories/lifecycles, not replacements for physical state. Serialize conflicting payment/cancellation/arrival/expiry commands; see transition contracts.

### FR-RES-13

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01, US-O01

Apply deterministic two-stage allocation ranking under the domain contract: reservation priority by start time then confirmation time, followed by requested-slot/same-zone/fallback-zone/nearest-compatible candidate ranking with configured coordinate distance and stable-slot tie-breaking. A final unresolved tie is resolved and logged by the Operator. This ranking never bypasses Allocation Lead Time.

### FR-BAS-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner  
**Stories:** US-D01

Reservation modes and requested-slot preference: the domain contract. Exact preferred-slot availability is not guaranteed.

### FR-BAS-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01, US-O05

Allocation begins at `reservation_start - effective ALLOCATION_LEAD_TIME`. The confirmed default Allocation Lead Time is 1 hour. Late-created confirmed reservations whose allocation point has already passed are evaluated immediately by the same idempotent command. Resolve the effective value through the approved policy hierarchy; permitted Owner/lot overrides must remain within Admin-defined approved bounds. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions.

### FR-BAS-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D17

Driver-initiated switching is deferred in this slice. Its future policy permits accepted reallocation requests up to the effective `DRIVER_REALLOCATION_REQUEST_LIMIT` (default 2). Transport retries and invalid submissions do not count; system recovery does not consume the allowance. No refund solely because a conforming reallocation was accepted; independent fee/refund grounds remain.

### FR-BAS-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D01, US-O05, US-OW02

Use the effective `RESERVATION_PROTECTION_WINDOW` (default 4 hours), separate fixed backup and dynamic protection, compatible temporal claims and a conflict queue for affected claimants. Use the effective `OCCUPANCY_VALIDITY_TIMESPAN` (default 4 hours) for occupancy projection; it is never a timer to free a vehicle. A requested/protected slot may remain physically occupied by another Driver during that horizon; protection tracks the future claim without implying fulfillment or displacement. Protect requested preferences without promising impossible exact-slot admission.
