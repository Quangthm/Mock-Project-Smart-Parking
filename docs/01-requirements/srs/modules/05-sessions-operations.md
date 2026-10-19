# Sessions Operations — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

| Record | Confirmed minimal states | Boundary |
|---|---|---|
| Incident | REPORTED → IN_REVIEW → RESOLVED → CLOSED | Operational issue and recovery; actor/guards/history still specified per transition. |
| Violation finding | SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED | Authorized human decision; recognition alone is not consequence authority. |
| Appeal | SUBMITTED → IN_REVIEW → ACCEPTED or REJECTED | Escalation separate; accepted does not mean money paid out. |
| General Support | OPEN → IN_PROGRESS → WAITING_FOR_CUSTOMER → RESOLVED → CLOSED | Minimal state set; exact skips/reopen guards are design work, not mandatory wait in every case. |
| Refund | REQUESTED / APPROVED / PROCESSING / COMPLETED / REJECTED | Separate Payment lifecycle and reconciliation; retain applicable existing baseline. |

Evidence availability, assignment and escalation are attributes rather than a state for every combination. Confirmed permitted consequences: dismiss unsupported finding; record wrong-slot incident and request assistance; correct incorrect charges; apply only already-published eligible obligations; accepted appeal corrects/reverses decision and initiates authorized adjustment; repeat concerns escalate for review. No automatic fines, bans, towing, forced relocation or government reporting inferred from a confirmed finding. Cancellation booking restrictions in §14.1 are separately approved, not a general violation ban.

| Actor | Confirmed report/data scope |
|---|---|
| Driver | Own visits/payments/appeals and permitted evidence; redact third parties/internal notes. |
| Operator | Assigned-lot operational cases and financial facts needed for checkout/case handling. |
| Owner | Owned-lot business reports, staff actions and scoped cases; no unrelated lots/unrestricted conversations. |
| Admin | Platform account/security/audit functions and explicitly authorized plate-dispute evidence; no blanket business-evidence inheritance. |
| Assistant | Requesting user's authorized scope only. |

Exports, downloads and SignalR obey the same resource scopes. Each service remains authoritative for its reports; cross-domain screens compose authorized contracts.

Accepted verified checkout freezes billable cutoff; commercial settlement completes when charge is covered. Physical departure remains separately recorded and is the only occupancy release. Audited unpaid recovery may resume the same session without fresh grace only after resolving UNKNOWN money. Never reopen a paid stay for continued parking: require actual exit/re-entry. System mispricing uses difference-only correction at original cutoff, retaining transaction/invoice history.

Operations owns session tickets/QR and reusable card assignment, release, loss, disabling and session history. IoT owns readers; Facility owns paths/entrances. Do not treat a reusable card as a reusable completed single-use visit ticket. Account-free walk-in remains supported. Actual entry/departure are explicit Operations events, not inferred from gate-open or LPR.

## Functional requirements

### FR-GATE-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Recognition Service, Device Gateway  
**Stories:** US-O01, US-O08

Identify by permitted ticket/QR, recognition evidence or authorized manual verification. LPR detailed integration/evaluation is POST-v1.0. Uncertain/unsuccessful recognition needs manual review; mismatch stops automatic passage even with high score. Backend capacity, identity and financial guards remain authoritative; simulated gate events do not prove real hardware integration.

### FR-GATE-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Driver  
**Stories:** US-O01

Walk-in intake does not require a customer account. Entry is subject to lot physical authorization and current capacity/operational conditions; an Operator may refuse entry when applicable capacity is unavailable.

### FR-GATE-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O01

Record an actual entry event and parking session separately from a scan or gate-open command; supported non-plated vehicle categories may use an Operator-managed session/ticket reference without a license plate.

### FR-GATE-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O03

Scoped Operator live overview in the domain contract includes approaching bookings, open sessions, waiting vehicles and overstay detail.

### FR-GATE-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O02, US-O05

Validate identity, accept checkout cutoff and obtain settled-charge clearance before ordinary gate authorization. Gate-open is not departure. Record authoritative actual departure separately and release occupancy once; emergency departure preserves debt under scoped audited exception. No actual-exit repricing of an already accepted checkout.

### FR-GATE-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Recognition Service, Device Gateway  
**Stories:** US-O02, US-O05

Stop automated checkout on plate/ticket/session identity mismatch, alert the Operator and record the resolution. A mismatch alone is not a theft/fine finding; the Operator verifies evidence and resolves the applicable identity/session before continuing or rejecting the operation.

### FR-GATE-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O02, US-O05

Provide scoped lost-ticket session lookup, entry-evidence review and audited manual handling. A lost ticket does not automatically establish a fee; the Operator uses permitted evidence to recover the applicable session, and any additional lost-ticket charge requires an approved and published policy.

### FR-GATE-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D04

Display the Driver's active session and parking history with recorded location, actual times and financial links.

### FR-GATE-09

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O01, US-O02

Prevent duplicate open sessions and repeated entry/exit effects; completed single-use tickets cannot open a new session (BR-GATE-02).

### FR-INC-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O05

Record incident lot/resource/device/session, observation and receipt time, evidence availability, assigned handler and history. Lifecycle REPORTED → IN_REVIEW → RESOLVED → CLOSED; missing evidence is an attribute, not proof of resolution.

### FR-INC-02

**Status:** FUTURE — outside MVP  
**Actors:** Operator, Recognition Service  
**Stories:** US-O08

Multi-frame automated per-slot violation candidates are future-only; BR-VIOL-01. Do not require per-slot camera AI for MVP acceptance.

### FR-INC-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O05

Operator manually records suspected wrong-slot parking against the actual allocation, time and evidence; BR-VIOL-02. Reallocation is not itself a violation.

### FR-INC-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O05

Assigned Operator reviews evidence: SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED, with reasons. Recognition never authorizes punishment. Permitted consequences are assistance, documented correction and already-approved published obligations; no inferred fine, ban, towing, forced relocation or government report.

### FR-INC-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D07, US-OW07

Accept appeal through the flow appeal → evidence → Operator approval → refund. Owner may handle refund when the Operator escalates the case. ACCEPTED and REFUNDED remain separate outcomes.

### FR-INC-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O05

Permit audited manual slot override only for an authorized actor acting under an approved operational condition.

### FR-INC-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D07

Display Driver own appeal/refund state as a customer-safe projection. Redact third-party details and internal staff notes; evidence access follows current resource scope and approved upload/retention/hold rules.

### FR-OPS-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O04

Assigned Operator executes explicitly authorized, audited emergency departure or unpaid recovery; Owner handles escalation and Admin has no blanket operational override. Record reason, grants, incident, financial state and actual movement. Emergency does not create capacity or erase debt. UNKNOWN money blocks blind unpaid recovery; a paid visit cannot be reopened.

### FR-OPS-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O04

Assigned Operator executes explicitly authorized, audited emergency departure or unpaid recovery; Owner handles escalation and Admin has no blanket operational override. Record reason, grants, incident, financial state and actual movement. Emergency does not create capacity or erase debt. UNKNOWN money blocks blind unpaid recovery; a paid visit cannot be reopened.

### FR-OPS-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator, Device Gateway  
**Stories:** US-O05

Represent missing/unreliable physical information as UNKNOWN (the domain contract). UNKNOWN resources are excluded from automatic allocation and automated violation decisions until a newer authoritative observation or authorized manual verification establishes the state.

### FR-OPS-04

**Status:** FUTURE — outside MVP  
**Actors:** Operator, Device Gateway  
**Stories:** US-O06

Offline cached authorization is Post-MVP; BR-FAIL-01 and the domain contract. It requires a future conflict/security design.

### FR-OPS-05

**Status:** FUTURE — outside MVP  
**Actors:** Operator, Device Gateway  
**Stories:** US-O06

Durable offline queue replay and reconciliation are Post-MVP; BR-FAIL-02. No promise of MVP bidirectional synchronization.

### FR-OPS-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O05

On online-service failure, report unavailable automation and use a recorded local incident/reconciliation procedure (BR-FAIL-03); do not claim remote commands succeeded. This does not establish a general offline authorization engine.
