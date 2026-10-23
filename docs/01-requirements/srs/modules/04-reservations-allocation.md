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

Allocation starts one hour before reservation start; late-created confirmed reservations are evaluated immediately by the same idempotent command. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions.

Positive-price reservation uses a 5-minute payment hold and Owner prepayment FULL (default), PERCENTAGE >0–100%, or FIXED >0 with no policy upper cap, even above booked charge. Round payable money once; a positive-price deposit rounded to zero requires validation. Apply valid monthly/yearly coverage before the booked minimum; genuinely zero-price reservations confirm explicitly without provider payment, even under FIXED mode, while atomically checking capacity and cap.

Unpaid hold expiry is EXPIRED, distinct from NO_SHOW. Late tolerance is optional: enabled default 15 minutes with Owner range 15–30; disabled retains entitlement until reservation end. Arrival at/before applicable deadline is timely; no-show requires strictly later decision and no authoritative timely evidence.

Ordinary hold payment/expiry is serialized at Operations: a verified result that commits while the hold is active converts it once; once expiry wins, reacquire a compatible claim or reconcile/refund. This is deliberately distinct from strict monthly A<H. Offer acceptance requires authoritative time < expiry; equality is expired and underlying entitlement is resolved independently.

## Functional requirements

### FR-POL-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW02

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locks unspecified ranges at approved defaults while preserving explicitly approved choices. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

### FR-POL-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A02

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locks unspecified ranges at approved defaults while preserving explicitly approved choices. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

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

Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locks unspecified ranges at approved defaults while preserving explicitly approved choices. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting.

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

Authenticated Driver reviews bound vehicle, location, interval and enabled Specific Slot/Zone/Capacity mode. Specific Slot protects a preference on a best-effort basis, not an exact-slot guarantee. Backend validates accepted terms, current access, booking restrictions and compatible claims before acceptance.

### FR-RES-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D01

Minimum duration 15 minutes; maximum 24 hours; advance horizon 7 days; buffer 0. No active-booking cap by default. Owner may enable a positive lot-scoped cap per account counting PENDING_PAYMENT, CONFIRMED and ALLOCATED only. Reject incompatible same-vehicle overlap; enforce global cancellation-abuse restrictions for new creations without disabling login or existing rights. Unspecified scalar override ranges stay locked.

### FR-RES-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner, Operator  
**Stories:** US-D01

Apply the baseline allocation/protection behavior together with the Specific Slot / Zone / Capacity reservation modes and Allocation Lead Time. These reservation modes are distinct from the underlying allocation and protection behavior.

### FR-RES-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Payment Provider  
**Stories:** US-D01

Positive-price reservation uses a 5-minute payment hold and Owner prepayment FULL (default), PERCENTAGE >0–100%, or FIXED >0 with no policy upper cap, even above booked charge. Round payable money once; a positive-price deposit rounded to zero requires validation. Apply valid monthly/yearly coverage before the booked minimum; genuinely zero-price reservations confirm explicitly without provider payment, even under FIXED mode, while atomically checking capacity and cap.

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

Recover lost capacity using conforming alternatives and established priority without displacing occupied/unaffected users. Refusal of a usable conforming alternative follows voluntary-cancellation terms; no feasible conforming alternative invokes facility-failure handling. Owner handles escalations; assigned Operator executes scoped recovery.

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

Allocation starts one hour before reservation start; late-created confirmed reservations are evaluated immediately by the same idempotent command. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions.

### FR-BAS-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D17

Driver may submit two accepted reallocation requests and enquire about reasons. Transport retries and invalid submissions do not count; system recovery does not consume the allowance. No refund solely because a conforming reallocation was accepted; independent fee/refund grounds remain.

### FR-BAS-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D01, US-O05, US-OW02

Use 4-hour reservation protection, separate fixed backup and dynamic protection, compatible temporal claims and a conflict queue for affected claimants. Occupancy projection horizon is 4 hours, never a timer to free a vehicle. Protect requested preferences without promising impossible exact-slot admission.
