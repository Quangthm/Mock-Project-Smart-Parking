# Reservations Allocation — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## BR-RES-01

**Status:** CONFIRMED — implementation pending / not assessed

Authenticated Driver reviews bound vehicle, location, interval and enabled Specific Slot/Zone/Capacity mode. Specific Slot protects a preference on a best-effort basis, not an exact-slot guarantee. Backend validates accepted terms, current access, booking restrictions and compatible claims before acceptance. Allocation begins at `reservation_start - effective ALLOCATION_LEAD_TIME`; the confirmed default is 1 hour. Late-created confirmed reservations whose allocation point has passed are evaluated immediately by the same idempotent command. Resolve the effective value through the approved policy hierarchy. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. A requested/protected slot that is still occupied by an unrelated session does not fulfill the future reservation and never authorizes displacement; use established alternative/reallocation/conflict handling. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions.

**Trace:** FR-RES-03/13, FR-BAS-01/02; UC-RES-01, UC-RES-02

**Examples:** with the 1-hour default, a 09:30 reservation has an 08:30 allocation point. The earlier SRS's 30-minute example remains explanatory only: Driver A 09:30–11:00 → 09:00 allocation point; Driver B 10:00–12:00 → 09:30 allocation point. If a reservation confirms after its calculated point, evaluate immediately.

A slot may also be `OCCUPIED + PROTECTED`: Driver A can be the current occupant while Driver B's future reservation is protected. `OCCUPIED` does not identify Driver B and does not fulfill the reservation. If Driver A leaves, protection may remain; if Driver A remains when fulfillment needs the resource, recover/reallocate without displacement.


## BR-RES-02

**Status:** CONFIRMED — implementation pending / not assessed

Reservation minimum duration, maximum duration, advance horizon and inter-booking buffer are configurable policies with defaults 15 minutes, 24 hours, 7 days and 0. Resolve Admin default/bounds → authorized Owner/lot override → accepted context; RA-06 supersedes D02 locking for these listed numeric defaults without inventing bounds. No active-booking cap by default. Owner may enable a positive lot-scoped cap per account counting PENDING_PAYMENT, CONFIRMED and ALLOCATED only. Reject incompatible same-vehicle overlap; enforce global cancellation-abuse restrictions for new creations without disabling login or existing rights.

**Trace:** FR-RES-02; UC-RES-01

## BR-RES-03

**Status:** CONFIRMED — implementation pending / not assessed

Unpaid hold expiry is EXPIRED, distinct from NO_SHOW. Late tolerance is optional: enabled default 15 minutes with Owner range 15–30; disabled retains entitlement until reservation end. Arrival at/before applicable deadline is timely; no-show requires strictly later decision and no authoritative timely evidence.

**Trace:** FR-RES-06/11; UC-RES-03

## BR-RES-04

**Status:** CONFIRMED — implementation pending / not assessed

Early entry requires compatible actual capacity and published extra-time charges; if unavailable, offer waiting or an authorized alternative. An occupied vehicle is never automatically displaced (the domain contract).

**Trace:** FR-RES-07; UC-GATE-01

## BR-RES-05

**Status:** CONFIRMED — implementation pending / not assessed

Validate voluntary cancellation against current lifecycle and accepted terms; do not cancel a parked visit as an unused booking. Release unused claims once, count the accepted cancellation once for abuse policy, and calculate refund from eligible collected money. No additional unpaid-balance penalty. Refund execution is independent of cancellation and cannot clear occupancy.

**Trace:** FR-RES-08/09, FR-PAY-06; UC-RES-03

## BR-RES-06

**Status:** CONFIRMED — implementation pending / not assessed

Inter-booking buffer defaults to 0 and is locked absent approved override bounds. Include any approved buffer in interval compatibility, never as physical occupancy or a second booking.

**Trace:** FR-CAP-03, FR-RES-02; UC-RES-01

## BR-RES-07

**Status:** CONFIRMED — implementation pending / not assessed

Apply guarded, versioned transitions PENDING_PAYMENT → CONFIRMED → ALLOCATED → PARKING → COMPLETED and explicit cancellation, unpaid expiry, rejection and no-show paths. Reallocation, refund, dispute and violation are linked histories/lifecycles, not replacements for physical state. Serialize conflicting payment/cancellation/arrival/expiry commands; see transition contracts.

**Trace:** FR-RES-12; UC-RES-03

## BR-RES-08

**Status:** CONFIRMED — implementation pending / not assessed

Allocation begins at `reservation_start - effective ALLOCATION_LEAD_TIME`; the confirmed default is 1 hour. Late-created confirmed reservations whose allocation point has passed are evaluated immediately by the same idempotent command. Preserve stable allocations. Ordinary priority is earlier start then confirmation; choose preferred slot, same zone, compatible fallback, nearest configured coordinates/stable resource tie. An `OCCUPIED + RESERVED/PROTECTED` combination does not prove that the current occupant is the reservation claimant. If an unrelated occupant remains in a requested/protected slot when fulfillment needs it, do not mark the reservation fulfilled and do not displace the occupant; use compatible alternatives, reallocation and conflict handling. Exceptional loss after allocation uses physical arrival priority among affected claimants; genuine unresolved ties are logged Operator decisions. Driver may submit two accepted reallocation requests and enquire about reasons. Transport retries and invalid submissions do not count; system recovery does not consume the allowance. No refund solely because a conforming reallocation was accepted; independent fee/refund grounds remain. Recover lost capacity using conforming alternatives and established priority without displacing occupied/unaffected users. Refusal of a usable conforming alternative follows voluntary-cancellation terms; no feasible conforming alternative invokes facility-failure handling. Owner handles escalations; assigned Operator executes scoped recovery.

**Trace:** FR-BAS-02/03/04; UC-RES-02

## BR-POL-01

**Status:** CONFIRMED — implementation pending / not assessed

Expose a named, typed, scoped policy catalogue with units and effective values. Resolve Admin defaults/bounds → authorized Owner/lot override → supported specific scope → accepted context. Unset inherits; valid zero differs from disabled. D02 locking remains for policies not superseded by RA-06; the numeric defaults listed by RA-06 are configurable within Admin-defined bounds. RA-06 confirms the listed reservation/monthly numeric values as configurable defaults rather than hard-coded constants, including minimum/maximum duration, advance horizon, inter-booking buffer, payment hold, protection window, allocation lead time, alternative offer duration, occupancy validity, reallocation limit and the listed monthly timing/limit policies. Admin defines default/bounds; authorized Owner scope may override within bounds. No missing numeric bounds are invented. Required commercial configuration blocks sale when missing. No new Owner permission follows from a visible setting. Each entry includes unit/type, service owner, scope, default, bounds, editor, disabled/unset semantics, effective version and acceptance boundaries.

**Trace:** FR-POL-03/07; UC-POL-01

## BR-POL-02

**Status:** CONFIRMED — implementation pending / not assessed

Reject activation of missing/invalid values, overlapping or uncovered applicable tariff/refund bands, or unresolved precedence. Invalid configuration is not interpreted as a zero fee or unlimited capacity.

**Trace:** FR-POL-04/08; UC-POL-01

## BR-POL-03

**Status:** CONFIRMED — implementation pending / not assessed

An accepted booking retains its accepted policy/price version. Later policy revisions apply to future applicable transactions and do not retroactively reprice the accepted booking.

**Trace:** FR-POL-05/08; UC-POL-01, UC-MON-01

## BR-POL-04

**Status:** CONFIRMED — implementation pending / not assessed

Backend supplies effective terms; before executing a transaction, changed price/policy/availability relative to reviewed terms requires fresh review. Web and chatbot invoke the same validation path.

**Trace:** FR-POL-06, FR-AI-07; UC-RES-01, UC-AI-02
