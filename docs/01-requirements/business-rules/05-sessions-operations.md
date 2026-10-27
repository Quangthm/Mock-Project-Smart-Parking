# Sessions Operations — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## BR-GATE-01

**Status:** CONFIRMED — implementation pending / not assessed

Recognition is evidence; backend authorization remains required by the domain contract and the domain contract.2. Manual/QR fallback cannot bypass identity, capacity or payment checks.

**Trace:** FR-GATE-01; UC-POL-02, UC-GATE-01, UC-LPR-01

## BR-GATE-02

**Status:** CONFIRMED — implementation pending / not assessed

Record an actual entry event and parking session separately from a scan or gate-open command; supported non-plated vehicle categories may use an Operator-managed session/ticket reference without a license plate. Prevent duplicate open sessions and repeated entry/exit effects; completed single-use tickets cannot open a new session (BR-GATE-02). Validate identity, accept checkout cutoff and obtain settled-charge clearance before ordinary gate authorization. Gate-open is not departure. Record authoritative actual departure separately and release occupancy once; emergency departure preserves debt under scoped audited exception. No actual-exit repricing of an already accepted checkout.

**Trace:** FR-GATE-03/05/09; UC-GATE-01, UC-GATE-02, UC-MON-04

## BR-GATE-03

**Status:** CONFIRMED — implementation pending / not assessed

Lost-ticket flow requires authorized lookup, comparison against entry evidence and vehicle/account verification, published extra fee if applicable, and reasoned audit. Never infer successful verification from a plate match alone.

**Trace:** FR-GATE-07; UC-GATE-02

## BR-GATE-04

**Status:** CONFIRMED — implementation pending / not assessed

Plate/token/session mismatch stops the automatic exit process and raises an Operator review; the software does not declare theft or levy a fine from mismatch alone.

**Trace:** FR-GATE-06; UC-GATE-02

## BR-VIOL-01

**Status:** FUTURE — outside MVP

Future per-slot automated detection must use configurable persistence, multiple frames and confidence, retain evidence and request human review. Do not implement it as MVP image-input LPR.

**Trace:** FR-INC-02; UC-FUT-01

## BR-VIOL-02

**Status:** CONFIRMED — implementation pending / not assessed

MVP candidate: Operator inspects and records suspected wrong-slot use against actual allocation; no per-slot AI assumed. An accepted alternate allocation is not a violation merely because the requested slot differs.

**Trace:** FR-INC-03; UC-INC-01

## BR-VIOL-03

**Status:** CONFIRMED — implementation pending / not assessed

Assigned Operator reviews evidence: SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED, with reasons. Recognition never authorizes punishment. Permitted consequences are assistance, documented correction and already-approved published obligations; no inferred fine, ban, towing, forced relocation or government report.

**Trace:** FR-INC-04; UC-INC-01

## BR-VIOL-04

**Status:** CONFIRMED — implementation pending / not assessed

Appeal workflow is appeal → evidence → Operator approval → refund. ACCEPTED is a decision state and does not mean REFUNDED. An Owner may complete refund handling when an Operator escalates the appeal.

**Trace:** FR-INC-05/07; UC-INC-02

## BR-EMERG-01

**Status:** CONFIRMED — implementation pending / not assessed

Backend state remains authoritative over visualization. Manual override has authority only when initiated by an authorized actor under an approved operational condition and is audited; no blanket manual precedence applies.

**Trace:** FR-INC-06, FR-MAP-02; UC-LOT-01, UC-INC-01, UC-MAP-01

## BR-EMERG-02

**Status:** CONFIRMED — implementation pending / not assessed

Assigned Operator executes explicitly authorized, audited emergency departure or unpaid recovery; Owner handles escalation and Admin has no blanket operational override. Record reason, grants, incident, financial state and actual movement. Emergency does not create capacity or erase debt. UNKNOWN money blocks blind unpaid recovery; a paid visit cannot be reopened.

**Trace:** FR-OPS-01; UC-OPS-01

## BR-EMERG-03

**Status:** CONFIRMED — implementation pending / not assessed

Assigned Operator executes explicitly authorized, audited emergency departure or unpaid recovery; Owner handles escalation and Admin has no blanket operational override. Record reason, grants, incident, financial state and actual movement. Emergency does not create capacity or erase debt. UNKNOWN money blocks blind unpaid recovery; a paid visit cannot be reopened.

**Trace:** FR-OPS-02/06; UC-OPS-01

## BR-EMERG-04

**Status:** CONFIRMED — implementation pending / not assessed

Represent missing/unreliable physical information as UNKNOWN (the domain contract). UNKNOWN resources are excluded from automatic allocation and automated violation decisions until a newer authoritative observation or authorized manual verification establishes the state. Conflicting/new observations never automatically free an occupied resource; preserve original evidence and reconciliation history.

**Trace:** FR-OPS-03; UC-INC-01

## BR-FAIL-01

**Status:** FUTURE — outside MVP

Cached offline entitlement evaluation is future work; full offline mode is excluded by the domain contract.

**Trace:** FR-OPS-04; UC-OPS-02

## BR-FAIL-02

**Status:** FUTURE — outside MVP

Durable offline replay/reconciliation is future work and must prevent duplicate sessions, capacity deductions and financial effects.

**Trace:** FR-OPS-05; UC-OPS-02

## BR-FAIL-03

**Status:** CONFIRMED — implementation pending / not assessed

A service/hardware outage is not proof of payment or fee waiver. MVP stops claiming successful automatic processing, uses an approved local incident procedure, then records actual events and reconciles when connected.

**Trace:** FR-OPS-06, FR-PAY-05; UC-LOT-02, UC-OPS-01, UC-NOT-01, UC-SIM-01
