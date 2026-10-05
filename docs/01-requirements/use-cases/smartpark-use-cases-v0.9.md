# SmartPark Use Cases — v0.9

**Document Type:** Use Case catalogue  
**Owner:** BA / team lead (review responsibility; no individual assignment)  
**Status:** WORKING — v0.9 BA-confirmed updates applied; unrelated OPEN items retained  
**Related SRS Baseline:** v0.9 Working Baseline Revision  
**Date:** 2026-10-05

## Catalogue authority and notation

C means confirmed baseline behavior for the v0.9 working baseline, including behavior explicitly confirmed during the BA review. A means PROPOSED; X means OPEN/conflicting and inactive; F means FUTURE/DEFERRED. Legacy R means a pointer to stated SRS behavior. Mixed R/A, R/X or A/X must be read clause by clause; their candidate clauses do not become confirmed. Main-flow detail remains proposed where it depends on an unresolved architecture/provider/policy/event-contract decision. N values and demo/fixture labels retain their original authority.

This single file follows docs/templates/use-case-template.md for each case: actors, SRS sections, goal, preconditions, numbered main flow, alternate/exception flows, postconditions and open questions. The template References field is called Traceability and contains internal IDs/links only. WORKING means the specification is still being reviewed; individual C/BASELINE/PROPOSED/OPEN/F tags preserve behavior authority. v0.9 promotes the BA-reviewed non-conflicting behavior to C while retaining unrelated OPEN/conflicting/deferred material. Automatic backend triggers are not separate human actors. Real gate/device integrations remain deferred even when an operational permission is confirmed.

| Use case | Name | Specification status |
| --- | --- | --- |
| [UC-AUTH-01](#uc-auth-01) | Register, authenticate and manage own profile | WORKING |
| [UC-AUTH-02](#uc-auth-02) | Approve Owner registration and administer accounts | WORKING |
| [UC-AUTH-03](#uc-auth-03) | Provision Operators and lot-scoped access | WORKING |
| [UC-VEH-01](#uc-veh-01) | Register and maintain vehicles | WORKING |
| [UC-VEH-02](#uc-veh-02) | Review disputed plate binding | PROPOSED / OPEN |
| [UC-LOT-01](#uc-lot-01) | Manage facility profile, layout and service state | WORKING |
| [UC-LOT-02](#uc-lot-02) | Manage assigned-lot devices and view status | WORKING |
| [UC-POL-01](#uc-pol-01) | Configure platform, lot and pricing policies | WORKING |
| [UC-POL-02](#uc-pol-02) | Manage platform integrations and recognition settings | PROPOSED / OPEN |
| [UC-SEARCH-01](#uc-search-01) | Discover parking and directions | WORKING |
| [UC-RES-01](#uc-res-01) | Create a parking reservation | WORKING |
| [UC-RES-02](#uc-res-02) | Allocate, queue and reallocate reservations | WORKING |
| [UC-RES-03](#uc-res-03) | Cancel, expire or mark no-show; inspect reservation history | WORKING |
| [UC-GATE-01](#uc-gate-01) | Check in a reserved, walk-in or monthly vehicle | WORKING |
| [UC-GATE-02](#uc-gate-02) | Check out and resolve identity/ticket exceptions | WORKING |
| [UC-PAY-01](#uc-pay-01) | Pay, verify results and reconcile late/unknown money | WORKING |
| [UC-PAY-02](#uc-pay-02) | Inspect invoices, payment history and charge explanations | WORKING |
| [UC-INC-01](#uc-inc-01) | Record an incident or suspected violation and override safely | WORKING |
| [UC-INC-02](#uc-inc-02) | Appeal and handle a refund/escalation | WORKING |
| [UC-OPS-01](#uc-ops-01) | Handle emergency release and service interruption | WORKING |
| [UC-OPS-02](#uc-ops-02) | Authorize offline and replay queued events | DEFERRED / FUTURE |
| [UC-RPT-01](#uc-rpt-01) | Monitor lot operations and Owner reports | WORKING |
| [UC-RPT-02](#uc-rpt-02) | Audit actions and apply data retention | WORKING |
| [UC-NOT-01](#uc-not-01) | Deliver reminders and status notifications | WORKING |
| [UC-MAP-01](#uc-map-01) | Use 2D/3D lot and recorded vehicle location | WORKING |
| [UC-SIM-01](#uc-sim-01) | Run an optional isolated product simulator | PROPOSED / OPEN |
| [UC-LPR-01](#uc-lpr-01) | Recognize an uploaded plate image and review uncertainty | WORKING |
| [UC-AI-01](#uc-ai-01) | Ask general help, search and explain prices/ranking | WORKING |
| [UC-AI-02](#uc-ai-02) | Read own records and prepare a confirmed reservation action | WORKING |
| [UC-AI-03](#uc-ai-03) | Escalate conversation to human support | WORKING |
| [UC-MON-01](#uc-mon-01) | Configure and offer monthly plans | WORKING |
| [UC-MON-02](#uc-mon-02) | Buy a monthly pass and activate verified rights | WORKING |
| [UC-MON-03](#uc-mon-03) | Renew, expire or repurchase a monthly pass | WORKING |
| [UC-MON-04](#uc-mon-04) | Validate monthly visits and bill uncovered use | WORKING |
| [UC-MON-05](#uc-mon-05) | Cancel a monthly plan and reconcile refund | WORKING |
| [UC-FUT-01](#uc-fut-01) | Detect per-slot occupancy and violation candidates | DEFERRED / FUTURE |
| [UC-FUT-02](#uc-fut-02) | Provide personalized model recommendations | DEFERRED / FUTURE |
| [UC-FUT-03](#uc-fut-03) | Forecast capacity and investigate anomalies | DEFERRED / FUTURE |

<a id="uc-auth-01"></a>

## Use Case: UC-AUTH-01 — Register, authenticate and manage own profile

**Status:** WORKING  
**Actor(s):** Driver; Notification Service  
**Related SRS Section(s):** §3.1.1–4; §4.3

### Goal

Obtain an authenticated customer account and use permitted personal-profile functions.

### Preconditions

- Registration or sign-in is requested; own-profile updates require an authenticated account.

### Main Flow

1. [BASELINE] Customer supplies phone/email; the system sends an OTP.
2. [BASELINE] Validate the OTP within 5 minutes; after three consecutive failures lock the account for 15 minutes.
3. [BASELINE] Authenticate through SMS/email OTP; optional TOTP MFA and the existing token lifetimes remain applicable.
4. [C] Expose own-profile editing and logout; enforce the confirmed permitted-field rules and current authorization/account-state checks.

   - **Name/display name** → normal profile edit.
   - **Avatar** → normal profile edit.
   - **Email** → dedicated change-contact flow with verification of the new email.
   - **Phone** → dedicated change-contact flow with verification of the new phone.
   - **Password** → separate change-password security flow.
   - **Role** → never customer-editable.
   - **Account status** → never customer-editable by the customer.
   - **Permissions** → never customer-editable.

### Alternate / Exception Flows

1. Invalid/expired OTP: do not authenticate; apply the existing lock rule.
2. [C] Locked-account or cross-account updates are rejected. Revoked permissions shall not authorize subsequent protected requests even when the existing authentication token remains valid.

### Postconditions

- Authentication reflects the verified result; proposed profile changes affect only the permitted account.

### Open Questions

- No profile-edit or current-authorization behavior remains unresolved in this use case; remaining token/session infrastructure details belong to the security contract.

### Traceability

**FR:** FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08  
**US:** US-A01, US-D08  
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-auth-02"></a>

## Use Case: UC-AUTH-02 — Approve Owner registration and administer accounts

**Status:** WORKING  
**Actor(s):** Admin; prospective Owner  
**Related SRS Section(s):** §3.1.1; §3.7.4; §2.3.1

### Goal

Approve eligible Owner accounts and administer platform account access.

### Preconditions

- Admin is authenticated and authorized for ACCOUNT_ADMIN.

### Main Flow

1. [C] Collect company name, email and phone for Owner registration.
2. [C] Admin reviews the request before enabling Owner operations.
3. [BASELINE] Admin searches accounts and performs authorized lock/unlock actions.
4. [C] Record role changes and enforce current per-resource authorization for every protected operation, including revoked permissions.

### Alternate / Exception Flows

1. Unapproved Owner cannot perform Owner operations.
2. [C] No corporate email-domain restriction is imposed.
3. [C] Approval does not authorize collection of CCCD/GPLX.

### Postconditions

- Owner approval/account state is recorded; account administration does not inherit every business permission.

### Open Questions

- Approval evidence/rejection criteria and detailed role-change workflow are still refinement.

### Traceability

**FR:** FR-AUTH-05, FR-AUTH-06  
**US:** US-A01  
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03, BR-PRIV-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-auth-03"></a>

## Use Case: UC-AUTH-03 — Provision Operators and lot-scoped access

**Status:** WORKING  
**Actor(s):** Owner; Operator  
**Related SRS Section(s):** §3.7.5; §2.3.1

### Goal

Assign Operators to owned facilities with supported permissions.

### Preconditions

- Owner is authenticated and acts within owned lots.

### Main Flow

1. [BASELINE] Owner provisions an Operator and selects assigned lots.
2. [BASELINE] Assign supported roles/permissions and review staff activity.
3. [C] Lot-device management is granted to assigned Operators; Owner/Admin do not inherit it.
4. [C] Any chosen email may be used without corporate-domain enforcement.

### Alternate / Exception Flows

1. [C] Attempted grants outside delegable scope are rejected; remaining permission-lifecycle implementation details follow the confirmed current-authorization contract and any unresolved security-contract refinements.

### Postconditions

- Operator access remains scoped to assigned lots and permitted actions.

### Open Questions

- Permission revocation timing and detailed provisioning fields remain to be specified.

### Traceability

**FR:** FR-AUTH-04  
**US:** US-OW03  
**Rules:** BR-AUTH-01, BR-AUTH-02

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-veh-01"></a>

## Use Case: UC-VEH-01 — Register and maintain vehicles

**Status:** WORKING  
**Actor(s):** Driver; Operator for supported non-plated parking sessions  
**Related SRS Section(s):** §3.1.3; §3.4.3

### Goal

Maintain customer vehicles and validate compatibility for parking.

### Preconditions

- Customer vehicle management requires the own account.

### Main Flow

1. [BASELINE] Register plate, type and image; validate Vietnamese plate format.
2. [C] Support automobile and motorcycle categories; EV compatibility is a slot attribute without charging behavior.
3. [BASELINE] Validate compatibility before a reservation or entry.
4. [C] List/edit own registered plated vehicles using both raw input and a canonical normalized plate value. Supported non-plated vehicle categories may be checked in through an Operator-managed parking session without a license plate; the generated session/ticket reference identifies the parking visit and is not a permanent vehicle identity.

### Alternate / Exception Flows

1. Invalid plate/unsupported compatibility prevents the applicable operation.
2. [C] Vehicle/slot compatibility uses approved vehicle and slot compatibility categories for MVP. Exact physical-dimension validation is not assumed unless separately approved for a parking configuration.

### Postconditions

- Registered vehicle and compatibility results are available; no charging session is created.

### Open Questions

- No further vehicle-identity rule is open in this use case; any additional category-specific compatibility values remain subject to the applicable policy catalogue.

### Traceability

**FR:** FR-VEH-01, FR-VEH-03, FR-VEH-04  
**US:** US-D01, US-D09, US-O01  
**Rules:** BR-VEH-01, BR-VEH-03, BR-CAP-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-veh-02"></a>

## Use Case: UC-VEH-02 — Review disputed plate binding

**Status:** PROPOSED / OPEN  
**Actor(s):** Driver; proposed Admin reviewer  
**Related SRS Section(s):** §3.1.3; §2.3.1

### Goal

Resolve a disputed active plate binding through an approved review process.

### Preconditions

- [PROPOSED] An authenticated claim and authorized reviewer permission would be required.

### Main Flow

1. [PROPOSED] Detect a competing normalized-plate claim.
2. [PROPOSED] Gather permitted evidence and submit to the authorized reviewer.
3. [PROPOSED] Approve/reject with a reason and retain historical booking/session ownership.

### Alternate / Exception Flows

1. [C] Do not collect/store CCCD/GPLX.
2. [OPEN] OTP alone does not establish vehicle ownership; evidence and reviewer role are not approved.

### Postconditions

- No binding transfer is enabled merely by this proposed flow.

### Open Questions

- Approve evidence, reviewer permission and transfer/rejection contract.

### Traceability

**FR:** FR-VEH-02  
**US:** US-A04, US-D09  
**Rules:** BR-VEH-02

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-lot-01"></a>

## Use Case: UC-LOT-01 — Manage facility profile, layout and service state

**Status:** WORKING  
**Actor(s):** Owner; explicitly authorized Operator override  
**Related SRS Section(s):** §3.2.1–3; §3.7.2

### Goal

Maintain owned facilities and supported layout/service information.

### Preconditions

- Owner acts within an owned lot; Operator overrides need explicit operational authority.

### Main Flow

1. [BASELINE] Create/update/deactivate facility information and toggle open/closed status.
2. [C] Maintain zones, floors where applicable, slots, access paths and category capacities.
3. [BASELINE] Maintain service/maintenance states.
4. [C] Show affected active commitments and record reasons when service/layout changes.

### Alternate / Exception Flows

1. [C] A manual override requires an authorized actor, approved condition and audit; no unconditional manual precedence.
2. [C] When a facility change reduces capacity, existing physical occupancy remains authoritative and accepted reservations remain valid entitlements. Future reservations affected by the reduction are identified, applicable reallocation is attempted using earlier reservation start time then earlier confirmation time, and the same zone/request is preferred where possible. If fulfillment becomes genuinely impossible, the applicable Owner refund/exception policy applies. Already occupied vehicles are never automatically displaced to restore capacity.

### Postconditions

- Facility changes are scoped; pending layout refinements remain proposals.

### Open Questions

- Detailed layout constraints remain review work; capacity-reduction handling is confirmed above.

### Traceability

**FR:** FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04  
**US:** US-O05, US-OW01  
**Rules:** BR-CAP-01, BR-EMERG-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-lot-02"></a>

## Use Case: UC-LOT-02 — Manage assigned-lot devices and view status

**Status:** WORKING  
**Actor(s):** Operator; Owner reporting future only  
**Related SRS Section(s):** §2.3.1; §3.2.5; §2.11

### Goal

Maintain authorized lot-device configuration without expanding real-IoT MVP scope.

### Preconditions

- Operator has DEVICE_MANAGE for the assigned lot; fixtures may stand in for deferred physical integration.

### Main Flow

1. [C] Operator links/enables/disables devices within assigned parking lots.
2. [C] Show connectivity, observation timestamp and errors.
3. [F] Owner device reports remain deferred under C-02; real camera/sensor/gate adapters remain deferred under C-15.

### Alternate / Exception Flows

1. Unauthorized lot/device access is denied under the approved scope.
2. [OPEN] FR-LOT-07 still names Owner; treat Owner reporting as deferred, as recorded in SPLIT-CF-04.

### Postconditions

- Authorized configuration outcome is recorded; no live-IoT deployment is claimed.

### Open Questions

- Status contract, adapter ownership and physical integration remain open/deferred.

### Traceability

**FR:** FR-LOT-05, FR-LOT-07  
**US:** US-O03, US-O05, US-OW05  
**Rules:** BR-AUTH-01, BR-FAIL-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-pol-01"></a>

## Use Case: UC-POL-01 — Configure platform, lot and pricing policies

**Status:** WORKING  
**Actor(s):** Admin; Owner  
**Related SRS Section(s):** §3.2.4; §3.7.2–3; §5.2

### Goal

Resolve effective lot policies within Admin defaults and bounds.

### Preconditions

- Admin/Owner is authorized for the policy scope.

### Main Flow

1. [C] Admin defines defaults/bounds; Owner selects permitted overrides and optional activation.
2. [C] Owner configures supported time-block prices; preserve accepted booking policy/price versions.
3. [C] Represent typed keys/units and draft/scheduled revisions; validate bands and ranges.
4. [C] Treat each pricing rule as a time interval. Calculate charges from actual overlap between parking duration and effective pricing intervals; calculate each applicable segment without premature monetary rounding, sum the segments, and apply the configured monetary rounding rule once to the final amount. Evaluate pricing using actual local timestamps and split intervals at the date boundary for overnight periods.

### Alternate / Exception Flows

1. [C] Invalid, missing, overlapping, or out-of-range configuration is rejected with a reason.
2. [C] Later revisions do not retroactively reprice accepted bookings.
3. [F] Event/holiday/demand-driven automatic pricing remains future.

### Postconditions

- Permitted effective policies are used; unresolved algorithm/default details remain inactive.

### Open Questions

- Complete any remaining policy-catalogue defaults and implementation-level validation details that are not covered by the confirmed pricing-interval, rounding, and timestamp rules.

### Traceability

**FR:** FR-POL-01, FR-POL-02, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-07, FR-POL-08, FR-CAP-07, FR-PAY-01, FR-PAY-10, FR-BAS-05  
**US:** US-A02, US-D02, US-O05, US-OW02  
**Rules:** BR-POL-01, BR-POL-02, BR-POL-03, BR-CAP-04, BR-PAY-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-pol-02"></a>

## Use Case: UC-POL-02 — Manage platform integrations and recognition settings

**Status:** PROPOSED / OPEN  
**Actor(s):** Admin; Recognition Service  
**Related SRS Section(s):** §2.4.1; §2.11; §3.2.5; §6.2

### Goal

Separate platform adapter settings from lot-device actions.

### Preconditions

- [PROPOSED] Explicit platform settings/AI-threshold permission is required.

### Main Flow

1. [PROPOSED] Configure provider adapters at platform scope.
2. [OPEN] Review final adapter ownership and any configurable LPR score interpretation.
3. [PROPOSED] Audit permitted threshold revisions after approval.

### Alternate / Exception Flows

1. [C] Platform administration does not imply DEVICE_MANAGE.
2. [OPEN] Exactly 90% LPR confidence is unresolved; do not invent >= approval.

### Postconditions

- No unapproved threshold or adapter boundary is activated.

### Open Questions

- ARCH-DEF-03 and LPR calibration/equality remain open.

### Traceability

**FR:** FR-LOT-06, FR-POL-09  
**US:** US-A02, US-A07  
**Rules:** BR-AUTH-01, BR-GATE-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-search-01"></a>

## Use Case: UC-SEARCH-01 — Discover parking and directions

**Status:** WORKING  
**Actor(s):** Anonymous visitor or Driver; Map Service  
**Related SRS Section(s):** §3.3; §2.3.1

### Goal

Find available compatible parking and route information.

### Preconditions

- [C] Public search/price/maps do not require an account.

### Main Flow

1. [BASELINE] Obtain location or search location and retrieve nearby facilities.
2. [BASELINE] Present available lot information and distance/price/availability results.
3. [C] Keep supported vehicle-category capacity separate.
4. [C] Explain deterministic backend ranking while retaining access to all available options.
5. [C] Add vehicle/time filters and manual-location fallback through authoritative queries.

### Alternate / Exception Flows

1. No results and service failure are distinct; error contract is refinement.
2. [BASELINE] Search radius expansion is supported; no arbitrary confirmed radius policy is invented.

### Postconditions

- Customer receives authoritative available options/directions without a reservation being created.

### Open Questions

- Filter/freshness/schema details and final map provider remain implementation decisions.

### Traceability

**FR:** FR-CAP-01, FR-CAP-02, FR-MAP-01  
**US:** US-D03, US-D16, US-O03  
**Rules:** BR-CAP-01, BR-AI-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-res-01"></a>

## Use Case: UC-RES-01 — Create a parking reservation

**Status:** WORKING  
**Actor(s):** Driver; Payment Provider  
**Related SRS Section(s):** §3.4.1–5; §3.4.9; §5.2

### Goal

Create an account-scoped reservation under an enabled canonical mode.

### Preconditions

- Driver is authenticated; selected vehicle/lot/time are eligible.

### Main Flow

1. [C] Select Specific Slot, Zone or Capacity according to enabled policies; a specific slot is a preference.
2. [BASELINE] Validate authoritative compatible availability and applicable policy.
3. [PROPOSED] Validate advance/action/active-booking limits and overlapping claims using approved definitions only.
4. [C] Create PENDING_PAYMENT and hold relevant capacity under effective PAYMENT_HOLD_DURATION; Admin default is 5 minutes with permitted Owner override.
5. [BASELINE] Hand off payment; [C] present the booking/ticket and requested-versus-allocated detail.

### Alternate / Exception Flows

1. [C] Unpaid hold expiry releases the pending claim and produces EXPIRED.
2. [C] Late successful reservation payment uses UC-PAY-01, not the strict monthly H rule.
3. [OPEN] Full transition/race and late-created reservation scheduling are not finalized.
4. [OPEN] No-prepayment booking and gate-only nearest-slot allocation are not approved alternatives.

### Postconditions

- Reservation is pending or confirmed only according to actual payment/backend outcome; no exact-slot guarantee is added.

### Open Questions

- Complete TESTER-DEF-01/08/12/13 and precise concurrency/active-count contracts.

### Traceability

**FR:** FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05, FR-CAP-03, FR-CAP-04, FR-POL-06, FR-BAS-01, FR-BAS-04  
**US:** US-D01, US-O01, US-O05, US-OW02  
**Rules:** BR-RES-01, BR-RES-02, BR-RES-06, BR-CAP-02, BR-CAP-03, BR-POL-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-res-02"></a>

## Use Case: UC-RES-02 — Allocate, queue and reallocate reservations

**Status:** WORKING  
**Actor(s):** Driver; Operator; Owner policy  
**Related SRS Section(s):** §3.4.4–8

### Goal

Fulfill valid reservations using normal and exceptional allocation priority.

### Preconditions

- Reservation entitlement is valid; allocation/protection policy applies.

### Main Flow

1. [C] Begin allocation at reservation start minus ALLOCATION_LEAD_TIME.
2. [BASELINE] Prioritize earlier start, then earlier confirmation; prefer the requested slot where applicable.
3. [BASELINE] Preserve stable allocation unless an unforeseen circumstance makes it unavailable.
4. [BASELINE] On unexpected loss of already allocated capacity, prioritize affected drivers by physical arrival at the entrance/gate; [C] Operator resolves simultaneous arrival with a logged decision.
5. [C] Driver request limit defaults to 2 and is configurable within Admin bounds; provide available recorded reasons.

### Alternate / Exception Flows

1. No allocation by Allocation Time: notify the Driver, retain reservation identity and applicable protection.
2. No fulfillable capacity: apply the applicable Owner refund policy; do not evict occupied vehicles.
3. Accepted alternate slot: keep existing reservation payment; default no refund solely for accepted reallocation.
4. [C] When alternative capacity must be ranked, use a two-stage deterministic model: (1) reservation priority is earlier start time, then earlier confirmation time; (2) candidate-slot ranking is requested slot, then same zone, then compatible fallback zone, then nearest compatible slot within the selected scope. For Specific Slot use distance from the requested slot; for Zone use distance from the zone's configured reference/anchor; for Capacity use distance from the lot's configured entry/reference point. MVP uses configured layout coordinates/deterministic distance rather than routing-based optimization. Equal-ranked candidates use stable slot identifier as the final tie-break.

### Postconditions

- Allocation/queue/outcome reflects applicable capacity and priority; no guarantee of impossible admission.

### Open Questions

- Request-limit counter details and the remaining physical-loss scheduling refinements remain to be refined. When multiple eligible reservations compete for the same final allocation capacity, reservation priority is start time then confirmation time; if still tied after the deterministic candidate ranking, Operator resolution is required and logged.

### Traceability

**FR:** FR-RES-13, FR-BAS-02, FR-BAS-03, FR-CAP-06  
**US:** US-D01, US-D17, US-O01, US-O02, US-O03, US-O05  
**Rules:** BR-RES-01, BR-RES-08, BR-CAP-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-res-03"></a>

## Use Case: UC-RES-03 — Cancel, expire or mark no-show; inspect reservation history

**Status:** WORKING  
**Actor(s):** Driver; Operator; Payment Provider  
**Related SRS Section(s):** §3.4.2–3; §3.4.10

### Goal

End reservation rights appropriately and inspect their actual outcomes.

### Preconditions

- Cancellation/history is account-scoped; automated expiry/no-show applies to an eligible reservation.

### Main Flow

1. [BASELINE] Retrieve own reservation and applicable cancellation/refund conditions.
2. [BASELINE] Driver cancellation ends entitlement; subsequent parking follows non-reservation rules.
3. [C] Unpaid hold expiry produces EXPIRED; enabled late tolerance without authoritative arrival produces NO_SHOW.
4. [C] Disabled late tolerance preserves entitlement until booking end subject to capacity/operation.
5. [PROPOSED] Present lifecycle/reasons and financial links; validate concurrent transitions and release only unused claims.

### Alternate / Exception Flows

1. Refund depends on Owner policy and remains separate from cancellation.
2. [C] A verified arrival is distinct from actual occupation; do not use the stale FR-RES-06 label as a new forfeiture rule.
3. [OPEN] Full transition table and verified-arrival/event timing are not supplied.

### Postconditions

- Actual entitlement state and any refund process remain distinct; occupied reality is not erased by cancellation.

### Open Questions

- Review stale FR-RES-06; approve history schema and transition/race details.

### Traceability

**FR:** FR-RES-06, FR-RES-08, FR-RES-09, FR-RES-10, FR-RES-12  
**US:** US-D01, US-D05, US-D10, US-O01, US-O05  
**Rules:** BR-RES-03, BR-RES-05, BR-RES-07

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-gate-01"></a>

## Use Case: UC-GATE-01 — Check in a reserved, walk-in or monthly vehicle

**Status:** WORKING  
**Actor(s):** Operator; Driver/walk-in; Recognition Service; simulated Device Gateway  
**Related SRS Section(s):** §3.4.11; §3.7.6; Appendix F

### Goal

Authorize an actual parking visit using the relevant rights and physical capacity.

### Preconditions

- Operator is authorized for the lot; MVP uses recorded/simulated observations.

### Main Flow

1. [BASELINE] Identify the vehicle through simulated LPR/image or QR and backend checks.
2. [C] Walk-in parking does not require an account; reservations/monthly account transactions do.
3. [C] Record authoritative arrival separately from physical passage and evaluate early arrival using the applicable reservation/session policy.
4. [BASELINE] Check applicable rights and compatible actual space without displacing existing vehicles.
5. [C] Record the verified entry/session and change capacity accounting once; monthly visits validate the applicable entitlement.

### Alternate / Exception Flows

1. No compatible capacity: refuse impossible entry and use allocation/incident handling.
2. Low-confidence recognition: authorized manual verification; exact threshold equality remains open.
3. [PROPOSED] Duplicate session/ticket or early-arrival fee handling follows approved contracts only.
4. [F] Physical barrier command delivery is not a required live-IoT MVP behavior.

### Postconditions

- Only actual accepted entry creates occupation/session under the approved event contract; a scan is not assumed passage.

### Open Questions

- TESTER-DEF-35, early entry pricing and precise arrival/passage timing remain open.

### Traceability

**FR:** FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09, FR-RES-07, FR-RES-11, FR-CAP-05  
**US:** US-D01, US-O01, US-O02, US-O08  
**Rules:** BR-GATE-01, BR-GATE-02, BR-RES-04, BR-CAP-05, M-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-gate-02"></a>

## Use Case: UC-GATE-02 — Check out and resolve identity/ticket exceptions

**Status:** WORKING  
**Actor(s):** Operator; Driver; Recognition Service; simulated Device Gateway  
**Related SRS Section(s):** §3.5.1; §3.7.6

### Goal

Record actual departure and settle applicable parking charges.

### Preconditions

- Existing visit/session and authorized Operator handling are available.

### Main Flow

1. [C] Locate the session and compare available plate, ticket, and session evidence.
2. [C] Final price uses actual recorded entry/exit and accepted applicable terms; monthly-covered intervals are not charged twice.
3. [BASELINE] Settle the eligible charge through the supported payment flow.
4. [C] Actual departure closes the physical parking session and releases occupation independently from final financial settlement; expose own parking history. Payment clearance is a separate event and, under the current operational policy, must succeed before the gate may authorize departure. Actual departure remains a separately recorded physical event.

### Alternate / Exception Flows

1. [C] Identity mismatch stops automatic checkout processing for Operator review. The mismatch alone is not a theft/fine finding; the Operator verifies evidence, resolves the applicable identity/session and then continues or rejects the operation under the approved procedure.
2. [C] A lost ticket triggers an exception workflow rather than automatically establishing a fee. The Operator uses permitted evidence to recover the applicable parking session; any additional lost-ticket charge requires an approved and published policy.
3. [C] A parking session remains free when billable duration is less than or equal to the configured free-parking duration; charges apply only when the duration exceeds that threshold. The exit quote/clearance/requote proposal remains inactive.
4. [C] Payment clearance is a separate event from actual departure: payment clearance may authorize the gate to open, while actual departure is recorded separately and is the authoritative event for physical occupation release. Payment failure leaves the financial obligation unresolved/unpaid and does not restore physical occupation after actual departure.

### Postconditions

- Actual exit remains authoritative for billing; any unresolved financial outcome is recorded separately.

### Open Questions

- Provider settlement/retry/compensation details continue to follow the payment contract; the business ordering, lost-ticket rule and free-parking boundary are confirmed above.

### Traceability

**FR:** FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-GATE-08, FR-PAY-02, FR-PAY-09, FR-PAY-11  
**US:** US-D02, US-D04, US-O02, US-O05, US-OW02  
**Rules:** BR-GATE-02, BR-GATE-03, BR-GATE-04, BR-PAY-05, BR-PAY-06, M-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-pay-01"></a>

## Use Case: UC-PAY-01 — Pay, verify results and reconcile late/unknown money

**Status:** WORKING  
**Actor(s):** Driver; Operator; Payment Provider  
**Related SRS Section(s):** §3.4.3; §3.5.3; §5.2

### Goal

Record verified money and the actual business outcome without duplicate charges.

### Preconditions

- A valid payable obligation exists; CASH_COLLECT requires the explicit Operator grant.

### Main Flow

1. [C] Choose VNPay/MoMo/ZaloPay or authorized Operator cash collection.
2. [BASELINE] Submit through the ordinary payment flow and validate provider results in the backend.
3. [BASELINE] Use idempotency/webhook updates; [C] retry only retryable failures, default 3 retries excluding the initial attempt.
4. [C] For late ordinary reservation success, grant only if compatible capacity is still free; otherwise notify and reconcile/refund the losing paid claim.
5. [C] Keep UNKNOWN separate and look up/reconcile before another payment attempt.

### Alternate / Exception Flows

1. Duplicate callback is not another financial transaction; extra real money requires reconciliation.
2. Client success page/screenshot is not backend settlement proof.
3. [C] Monthly payment at/after H follows M-02/M-03 instead of the ordinary reservation late-success rule.
4. [OPEN] Technical transaction/compensation and exact amount-match contracts remain refinement.

### Postconditions

- Verified settlement and fulfilled rights reflect the actual backend outcome; unknown/failed money is not shown as success.

### Open Questions

- Approve provider contracts, cash evidencing, idempotency/result lookup and compensation sequence.

### Traceability

**FR:** FR-PAY-03, FR-PAY-04, FR-PAY-05, FR-BAS-16  
**US:** US-D02, US-O02  
**Rules:** BR-PAY-03, BR-PAY-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-pay-02"></a>

## Use Case: UC-PAY-02 — Inspect invoices, payment history and charge explanations

**Status:** WORKING  
**Actor(s):** Driver; Notification Service  
**Related SRS Section(s):** §3.5.3; §3.6

### Goal

Review own receipts and actual payment/refund history.

### Preconditions

- Customer is authenticated for own financial records.

### Main Flow

1. [BASELINE] Generate the PDF invoice required by payment processing.
2. [C] Display the customer's own payment/refund history and verified financial statuses.
3. [BASELINE] Use applicable notification channels for payment information.

### Alternate / Exception Flows

1. Pending/approved refund is not paid-back completion.
2. [C] Deny another account’s financial lookup.

### Postconditions

- The customer sees authorized actual financial records.

### Open Questions

- History/invoice field schema and access/error details remain refinement.

### Traceability

**FR:** FR-PAY-07  
**US:** US-D06  
**Rules:** BR-PAY-03, BR-AUTH-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-inc-01"></a>

## Use Case: UC-INC-01 — Record an incident or suspected violation and override safely

**Status:** WORKING  
**Actor(s):** Operator  
**Related SRS Section(s):** §3.2.2; §3.7.6; §6.1

### Goal

Record operational evidence and perform only authorized human decisions.

### Preconditions

- Operator has lot/case scope; overrides require the approved condition and explicit grant.

### Main Flow

1. [C] Record affected slot/session/device, observation time and evidence.
2. [C] Compare suspected wrong-slot use with the actual accepted allocation, not only the original requested slot.
3. [BASELINE] Human verification precedes any punitive/legal decision.
4. [C] Audit authorized manual override; [BASELINE] UNKNOWN represents unverifiable state.

### Alternate / Exception Flows

1. Accepted reallocation is not itself a violation.
2. [F] Per-slot automated camera detection is excluded from the MVP flow.
3. [C] A resource in UNKNOWN physical state is excluded from automatic allocation and automated violation decisions until a newer authoritative observation or authorized manual verification establishes its state. No arbitrary time threshold is invented by this use case.

### Postconditions

- Recorded allegation, confirmed decision and charge remain distinct; model/UI does not authorize enforcement.

### Open Questions

- Complete violation statuses/evidence/fees and reconciliation conditions.

### Traceability

**FR:** FR-INC-01, FR-INC-03, FR-INC-04, FR-INC-06, FR-OPS-03  
**US:** US-O05  
**Rules:** BR-VIOL-02, BR-VIOL-03, BR-EMERG-01, BR-EMERG-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-inc-02"></a>

## Use Case: UC-INC-02 — Appeal and handle a refund/escalation

**Status:** WORKING  
**Actor(s):** Driver; Operator; Owner on escalation; Payment Provider  
**Related SRS Section(s):** §3.5.5; §3.7.6; §2.3.1

### Goal

Review an appeal with evidence and follow any eligible refund separately.

### Preconditions

- Customer/case is verified; handler has the relevant lot/case permission.

### Main Flow

1. [C] Submit appeal and provide evidence.
2. [C] Operator reviews/approves the appeal and initiates the applicable refund handling.
3. [C] Owner may handle the refund when Operator escalates.
4. [C] Determine full/partial refund under the applicable policy and retain audit; present the refund lifecycle as REQUESTED → APPROVED → SUBMITTED → COMPLETED, with REQUESTED → REJECTED as an exception. Provider execution failure remains a distinct implementation/API failure or retry state and never means REFUND_COMPLETED.

### Alternate / Exception Flows

1. ACCEPTED never means REFUNDED without actual payout.
2. [OPEN] BR-REF-01 blanket Owner-approver proposal does not override C-12; monthly cancellation separately uses Owner under M-05.
3. [C] Display a redacted, customer-safe projection of the customer's own appeal/refund status. Internal evidence, staff notes and other users' information are not exposed.

### Postconditions

- Appeal decision and actual financial return are separately visible.

### Open Questions

- Exact provider processing, retry handling and any policy-specific refund limits remain implementation/refinement details; the business refund lifecycle and authority are confirmed above.

### Traceability

**FR:** FR-INC-05, FR-INC-07, FR-PAY-06  
**US:** US-D07, US-D10, US-OW04, US-OW07  
**Rules:** BR-VIOL-04, BR-REF-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-ops-01"></a>

## Use Case: UC-OPS-01 — Handle emergency release and service interruption

**Status:** WORKING  
**Actor(s):** Operator; simulated Device Gateway  
**Related SRS Section(s):** §3.7.6; §2.3.1

### Goal

Carry out permission-gated emergency actions with recorded effects and recovery.

### Preconditions

- Authorized emergency condition and explicit Operator permission apply.

### Main Flow

1. [C] Validate emergency authority before release/priority handling.
2. [C] Record actor/time/context/reason/outcome and affected reservations/capacity.
3. [C] Keep occupied vehicles and impossible capacity limitations intact.
4. [C] For unavailable online automation, use an approved local incident procedure and later record/reconcile actual events. This does not establish a general offline authorization engine.

### Alternate / Exception Flows

1. Service failure is not proof of payment or a fee waiver.
2. No automatic eviction or live offline authorization engine is implied.
3. [C] Record physical/manual recovery when connectivity returns; durable offline synchronization remains future.

### Postconditions

- Emergency action and recovery are audited without erasing financial obligations.

### Open Questions

- Approve local procedure, operational conditions and recovery API representation.

### Traceability

**FR:** FR-OPS-01, FR-OPS-02, FR-OPS-06  
**US:** US-O04, US-O05  
**Rules:** BR-EMERG-02, BR-EMERG-03, BR-FAIL-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-ops-02"></a>

## Use Case: UC-OPS-02 — Authorize offline and replay queued events

**Status:** DEFERRED / FUTURE  
**Actor(s):** Operator; Device Gateway  
**Related SRS Section(s):** §1.2; §3.7.6

### Goal

Future controlled offline operations and reconciliation.

### Preconditions

- [F] Future approved cache/security/conflict design is required.

### Main Flow

1. [F] Evaluate cached entitlement under a future authorization contract.
2. [F] Persist offline events and replay through an approved reconciliation process.
3. [F] Detect duplicate sessions/capacity/financial effects.

### Alternate / Exception Flows

1. [OPEN] Conflict order, stale permissions and bi-directional replay are unspecified.

### Postconditions

- No MVP offline capability is promised.

### Open Questions

- Define the post-MVP offline threat/conflict/synchronization model.

### Traceability

**FR:** FR-OPS-04, FR-OPS-05  
**US:** US-O06  
**Rules:** BR-FAIL-01, BR-FAIL-02

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-rpt-01"></a>

## Use Case: UC-RPT-01 — Monitor lot operations and Owner reports

**Status:** WORKING  
**Actor(s):** Operator; Owner  
**Related SRS Section(s):** §3.7.1; §3.8; §3.9.2

### Goal

Inspect authorized operational and financial/utilization reports.

### Preconditions

- Actor is authenticated with assigned/owned-lot scope.

### Main Flow

1. [BASELINE] Show live facility utilization and relevant operations.
2. [C] Include approaching bookings, open sessions, waiting vehicles and overstay detail.
3. [BASELINE] Owner reviews existing revenue/usage/customer-report fields.
4. [F] Respect future labels on geographic/retention analytics.

### Alternate / Exception Flows

1. Reporting access does not permit ledger edits or other customer-global access.
2. [C] Anonymous walk-in segmentation is compatible with C-01; stale conflict wording is documented.

### Postconditions

- Reports reflect scoped recorded operations and financial results.

### Open Questions

- Approve operational display schema, aggregation rules and missing report definitions.

### Traceability

**FR:** FR-GATE-04, FR-RPT-02, FR-BAS-07  
**US:** US-O03, US-OW04  
**Rules:** BR-AUTH-01, BR-PRIV-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-rpt-02"></a>

## Use Case: UC-RPT-02 — Audit actions and apply data retention

**Status:** WORKING  
**Actor(s):** Admin; authorized scoped staff history viewers  
**Related SRS Section(s):** §4.3; §4.8; §6.2.3; §3.7.4–5

### Goal

Inspect protected audit and apply approved category-specific retention.

### Preconditions

- Actor has the explicit data/audit scope.

### Main Flow

1. [C] Record who/when/resource/action/reason/outcome for important actions.
2. [C] Apply 7-year audit, 90-day hot + 1-year cold operational logs, maximum 30-day images with authorized holds; conversations are logged.
3. [C] Provide scoped audit search without direct editing.
4. [C] Apply expiry/deletion/hold lifecycle jobs under the approved data rules.

### Alternate / Exception Flows

1. No CCCD/GPLX collection is authorized by generic encryption text.
2. [C] Normal AI conversation records follow the approved operational-log retention policy unless an authorized hold applies. Held conversations remain retained until the applicable hold is released.

### Postconditions

- Distinct record categories and authorized holds are respected.

### Open Questions

- Deletion-job implementation and detailed search/storage mechanics remain refinement; normal conversation retention and hold behavior are confirmed above.

### Traceability

**FR:** FR-RPT-03, FR-RPT-04, FR-RPT-05  
**US:** US-A03, US-A05  
**Rules:** BR-PRIV-01, BR-PRIV-02, BR-PRIV-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-not-01"></a>

## Use Case: UC-NOT-01 — Deliver reminders and status notifications

**Status:** WORKING  
**Actor(s):** Driver; Owner/Admin where applicable; Notification Service  
**Related SRS Section(s):** §3.6; §5.2

### Goal

Notify users about existing business events and configured reminders.

### Preconditions

- Applicable notification event, permitted destination and channel exist.

### Main Flow

1. [BASELINE] Use SMS/email/push for listed payment/reservation/account/policy categories.
2. [C] Apply configured N timing within the hierarchy.
3. [C] Add notification content for expiry, refund and appeal status, plus overstay notices, using the authoritative business event/state.
4. [C] Report notification delivery failure separately without rolling back successful business state. Record event ID, recipient, channel, template/version, attempt, delivery status and failure reason.

### Alternate / Exception Flows

1. Reminder is not proof of settlement or renewal.
2. [C] Sending an overstay notice does not activate an overstay-penalty schema or establish a penalty by itself.

### Postconditions

- Delivered/failed notification outcome is distinct from the underlying business state.

### Open Questions

- Finalize event templates, timing fields and delivery/error contracts.

### Traceability

**FR:** FR-RPT-01, FR-BAS-06, FR-PAY-08  
**US:** US-D11, US-OW02  
**Rules:** BR-PAY-02, BR-FAIL-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-map-01"></a>

## Use Case: UC-MAP-01 — Use 2D/3D lot and recorded vehicle location

**Status:** WORKING  
**Actor(s):** Driver; Operator; Owner  
**Related SRS Section(s):** §3.2.3; §3.9

### Goal

Visualize backend state and locate a recorded vehicle.

### Preconditions

- Actor has access to the relevant lot/session view.

### Main Flow

1. [BASELINE] Render facility layout/status from authoritative backend state.
2. [BASELINE] Distinguish requested and allocated positions.
3. [C] Show only the recorded position precision and never fabricate a slot from a zone.
4. [C] Mark stale/disconnected display and reload authoritative state after reconnect.

### Alternate / Exception Flows

1. Changing visualization does not modify business/physical state.
2. [OPEN] Refresh/event/position schemas remain refinement.

### Postconditions

- Map/3D view reflects permitted recorded state.

### Open Questions

- Finalize visualization event contract and location precision.

### Traceability

**FR:** FR-MAP-02, FR-MAP-03, FR-MAP-04  
**US:** US-D16, US-O03  
**Rules:** BR-EMERG-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-sim-01"></a>

## Use Case: UC-SIM-01 — Run an optional isolated product simulator

**Status:** PROPOSED / OPEN  
**Actor(s):** Proposed Admin; Device Gateway fixture  
**Related SRS Section(s):** §2.11; §3.9.6

### Goal

Optional scenario controls separate from deterministic MVP test fixtures.

### Preconditions

- [PROPOSED] Simulator need/feasibility and isolated permission are approved first.

### Main Flow

1. [C] Use deterministic fixtures for MVP tests.
2. [PROPOSED] If separately approved, choose an isolated scenario and simulate authorized observations.
3. [PROPOSED] Show fixture results without touching operational resources.

### Alternate / Exception Flows

1. [C] Deterministic test fixtures remain the MVP simulation mechanism. The optional product simulator remains outside MVP unless separately approved as a scoped, isolated demonstration capability; no simulator action may mutate production resources or automatically promote SIMULATION_RUN into operational authority.

### Postconditions

- Deterministic test fixtures remain baseline MVP support. Product-level simulation remains proposed/future until separately approved.

### Open Questions

- Confirm need, permissions and scenario contract before product implementation.

### Traceability

**FR:** FR-SIM-01  
**US:** US-A06  
**Rules:** BR-FAIL-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-lpr-01"></a>

## Use Case: UC-LPR-01 — Recognize an uploaded plate image and review uncertainty

**Status:** WORKING  
**Actor(s):** Operator; Recognition Service  
**Related SRS Section(s):** §6.2; §3.4.11

### Goal

Use MVP image recognition as evidence for backend vehicle authorization.

### Preconditions

- Operator uploads a permitted test image; no live-IoT integration is required.

### Main Flow

1. [BASELINE] Process the image and return plate/confidence; record timestamp/image/result.
2. [BASELINE] Backend checks applicable identity/rights and payment independently of recognition.
3. [BASELINE] Below 90% requires attendant verification; above 90% still needs backend authorization.
4. [BASELINE] Permit audited plate correction and protect image data with existing retention/access constraints.

### Alternate / Exception Flows

1. [OPEN] Exactly 90% and score calibration remain undefined.
2. No recognition output directly commands a real gate or grants rights.
3. [OPEN] Model/provider/dataset and event schema require AI-DEP-08/09.

### Postconditions

- Recognition and any human correction are recorded; authorization remains backend-owned.

### Open Questions

- Approve confidence equality, dataset/model and manual correction contract.

### Traceability

**FR:** FR-BAS-09  
**US:** US-O08  
**Rules:** BR-GATE-01, BR-PRIV-02

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-ai-01"></a>

## Use Case: UC-AI-01 — Ask general help, search and explain prices/ranking

**Status:** WORKING  
**Actor(s):** Anonymous visitor or Driver; AI/LLM Service  
**Related SRS Section(s):** §2.3.1; §6.7; §6.8

### Goal

Provide conversation grounded in approved content and authoritative backend facts.

### Preconditions

- Public help/search is allowed; personal data or writes require authenticated scope.

### Main Flow

1. [BASELINE] Identify the chatbot as AI and maintain Vietnamese/English conversational context.
2. [PROPOSED] Retrieve approved help snippets and invoke allowlisted search/price tools.
3. [C] Explain deterministic backend ranking while exposing all available options.
4. [PROPOSED] Explain actual charges/policy values from backend output, with distinct safe API errors.

### Alternate / Exception Flows

1. Timeout is not no availability; error is not success.
2. [F] Personalized/LLM-generated ranking remains future despite legacy §6.4 wording.
3. [OPEN] Provider/tools/knowledge contracts and calibrated escalation confidence remain dependencies.

### Postconditions

- Answer is grounded in permitted returned information; no financial/access decision is made by the model.

### Open Questions

- AI-DEP-01/02/03/05/06/09 and final tool contracts remain open.

### Traceability

**FR:** FR-AI-01, FR-AI-02, FR-AI-03, FR-AI-04, FR-AI-09, FR-AI-10, FR-AI-11  
**US:** US-D12  
**Rules:** BR-AI-01, BR-AI-02, BR-AI-04, BR-AI-05

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-ai-02"></a>

## Use Case: UC-AI-02 — Read own records and prepare a confirmed reservation action

**Status:** WORKING  
**Actor(s):** Driver; AI/LLM Service; backend/UI  
**Related SRS Section(s):** §6.7; §6.8.3

### Goal

Assist with personal lookup and reservation preparation under user-confirmed backend execution.

### Preconditions

- Driver is signed in; permitted read/write intent contracts must exist before enabling them.

### Main Flow

1. [PROPOSED] Read own bookings/session/vehicles/payment/refund status through resource-scoped APIs.
2. [PROPOSED] Collect missing reservation inputs and populate the normal editable application form.
3. [C] User confirms the actual action; backend performs authorization and business write.
4. [PROPOSED] Revalidate changed terms/availability and return actual booking/payment status through ordinary checkout.

### Alternate / Exception Flows

1. Cross-account lookup is denied; model cannot supply an identity override.
2. [PROPOSED] Editing confirmed draft invalidates prior confirmation.
3. [OPEN] Extend/cancel/refund-request/profile/vehicle/payment-method/password intents need the final allowlist; no direct model charge/refund.

### Postconditions

- Draft is not a booking; only confirmed backend execution creates an actual business result.

### Open Questions

- Approve AI-DEP-01/02/04/06 and intent-by-intent confirmation/idempotency contracts.

### Traceability

**FR:** FR-AI-05, FR-AI-06, FR-AI-07, FR-AI-08, FR-BAS-11  
**US:** US-D12  
**Rules:** BR-AI-02, BR-AI-03, BR-AI-04, BR-POL-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-ai-03"></a>

## Use Case: UC-AI-03 — Escalate conversation to human support

**Status:** WORKING  
**Actor(s):** Customer; support handler role OPEN; Operator proposed  
**Related SRS Section(s):** §6.7.1–2; §6.8.2

### Goal

Request human support with permitted conversation context.

### Preconditions

- Human-support request/trigger occurs; actual handler/channel is not yet selected.

### Main Flow

1. [BASELINE] Offer Talk to human at any point.
2. [BASELINE] Apply specified escalation triggers with calibrated interpretation still to be agreed.
3. [BASELINE] Include permitted history/context in escalation.
4. [OPEN] Route/acknowledge through the approved handler/channel when AI-DEP-07 is resolved.

### Alternate / Exception Flows

1. [OPEN] Off-hours/unavailable recipient fallback remains to be defined.
2. AI does not make final refund/dispute decisions.

### Postconditions

- Do not claim successful delivery without an actual channel acknowledgement.

### Open Questions

- AI-DEP-07: handler permissions/channel, availability, consent and off-hours behavior.

### Traceability

**FR:** FR-BAS-10  
**US:** US-D12, US-O09  
**Rules:** BR-AI-02, BR-PRIV-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-mon-01"></a>

## Use Case: UC-MON-01 — Configure and offer monthly plans

**Status:** WORKING  
**Actor(s):** Owner; Admin bounds  
**Related SRS Section(s):** Appendix F.1/F.3; §3.2.4

### Goal

Offer versioned full-period monthly plans within the policy hierarchy.

### Preconditions

- Owner is authorized for the lot; Admin supplies defaults/bounds.

### Main Flow

1. [C] Configure plan price N, vehicle/lot/zone scope and when-space-available or guaranteed capacity/slot entitlement.
2. [C] Version the plan and effective date; preserve already purchased periods.
3. [C] Stop/change future sales without rewriting paid contracts; show scoped actual collected/refunded money.
4. [OPEN] Demo refund rates/prices are fixtures; M-05 embedded proposed details require review.

### Alternate / Exception Flows

1. Unsupported transfer/suspension is not an implicit ability.
2. [C] Operator may collect authorized cash but cannot set monthly prices or approve monthly refunds.

### Postconditions

- Plan revision applies to eligible future purchases; paid rights remain auditable.

### Open Questions

- Resolve M-05 proposal wording and production defaults/bounds, without adopting demo numbers.

### Traceability

**FR:** FR-MON-01, FR-MON-12, FR-MON-13  
**US:** US-OW06  
**Rules:** M-01, M-05, M-06, M-09, BR-POL-03

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-mon-02"></a>

## Use Case: UC-MON-02 — Buy a monthly pass and activate verified rights

**Status:** WORKING  
**Actor(s):** Driver; Operator cash collection; Payment Provider  
**Related SRS Section(s):** Appendix F.1–3

### Goal

Buy a full rolling calendar period with rights granted only under monthly settlement/deadline rules.

### Preconditions

- Driver account and eligible vehicle/plan exist; compatible sold resources are validated.

### Main Flow

1. [C] Display full-period price, scope, original anchor, [S,E), guarantee and refund terms.
2. [C] Choose future S at local 00:00 or immediate S=A; calculate E from the original calendar anchor.
3. [C] Create the order after limit/duplicate/resource checks; future/renewal H=min(O+N,S), immediate H=O+N.
4. [C] Verify full settlement and commit entitlement only at A<H; scheduled/active rights and transaction/order states remain separate.
5. [C] Protect guaranteed inventory without double-counting occupation; when-space-available reserves no capacity.

### Alternate / Exception Flows

1. At or after H: no activation, even if capacity is free; reconcile/refund actual money.
2. Paid before H but commit after H: no automatic restoration of released resources.
3. Mismatch/UNKNOWN/duplicate charge/failed fulfillment: follow M-03 separately, never fabricate rights.
4. 31 January clamps to February end while retaining the original 31 anchor; 15 February ends 15 March.

### Postconditions

- FULFILLED requires money and rights committed; failed/expired order does not grant entitlement.

### Open Questions

- Detailed guarantee inventory partition, provider transaction and compensation contracts require implementation review.

### Traceability

**FR:** FR-MON-02, FR-MON-03, FR-MON-04, FR-MON-05, FR-MON-06  
**US:** US-D13, US-O07, US-OW06  
**Rules:** M-01, M-02, M-03, M-04, M-07

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-mon-03"></a>

## Use Case: UC-MON-03 — Renew, expire or repurchase a monthly pass

**Status:** WORKING  
**Actor(s):** Driver; Payment Provider; Notification Service  
**Related SRS Section(s):** Appendix F.1/F.3

### Goal

Continue valid coverage or start a new non-retroactive period after expiry.

### Preconditions

- Existing pass/final committed E is available for own account.

### Main Flow

1. [C] Open renewal within MONTHLY_RENEWAL_WINDOW and append S to the final committed E.
2. [C] Enforce H=min(O+N,S) and A<H; preserve the original anchor for consecutive timely renewals.
3. [C] Present separate order/payment/refund/period status and configured reminders.
4. [C] At E stop expired new-entry rights; retain actual parked sessions and occupation.
5. [C] Later repurchase uses a new effective start/anchor after eligibility/capacity checks, including an existing parked session.

### Alternate / Exception Flows

1. No renewal: no automatic next-month debt or penalty solely for not renewing.
2. Notification failure does not renew.
3. 15 March expiry and 20 March repurchase: no retroactive 15–20 March monthly charge; legitimate uncovered parking is separate.
4. Existing parked vehicle: no new entry/second occupation deduction or silent relocation.

### Postconditions

- Coverage is consecutive only after timely committed renewal; expiry does not release occupied space.

### Open Questions

- Finalize reminder/delivery and guarantee hold-release contracts.

### Traceability

**FR:** FR-MON-09, FR-MON-10, FR-MON-14  
**US:** US-D14, US-O07  
**Rules:** M-01, M-02, M-03, M-04, M-08

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-mon-04"></a>

## Use Case: UC-MON-04 — Validate monthly visits and bill uncovered use

**Status:** WORKING  
**Actor(s):** Operator; Driver; simulated Device Gateway  
**Related SRS Section(s):** Appendix F.1; §3.5.1

### Goal

Apply monthly rights on each visit without duplicate sessions/charges.

### Preconditions

- Applicable pass/vehicle/lot and an actual visit/session are known.

### Main Flow

1. [C] Check timestamps, revocation, vehicle, lot, plan scope and actual compatible capacity at each use.
2. [C] Admit only applicable rights; pass is not a reusable single-use session ticket.
3. [C] Separate covered from uncovered [S,E) boundaries and approved extra fees using actual-exit billing.
4. [C] Evaluate short-stay grace once per physical session; do not reset at renewal/month boundary.
5. [C] On departure before E, valid guarantee returns to protected inventory; after E no expired guarantee returns.

### Alternate / Exception Flows

1. Guaranteed resource unavailable: do not evict another vehicle or promise impossible entry; incident/refund process applies.
2. Vehicle remains after E: keep occupation and bill only the eligible uncovered interval.
3. [OPEN] Ordinary short-stay exact threshold and final exit settlement order retain their unresolved status.

### Postconditions

- Covered time is not charged twice; physical occupation changes only on actual departure.

### Open Questions

- Resolve inventory disjointness and the ordinary pricing/exit refinements; do not substitute demo algorithms.

### Traceability

**FR:** FR-MON-07, FR-MON-08  
**US:** US-D14, US-O07  
**Rules:** M-04, M-07, M-08, BR-GATE-02

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-mon-05"></a>

## Use Case: UC-MON-05 — Cancel a monthly plan and reconcile refund

**Status:** WORKING  
**Actor(s):** Driver; Owner; Payment Provider  
**Related SRS Section(s):** Appendix F.1 M-05/M-06; F.2

### Goal

Cancel eligible monthly rights and follow the separate refund outcome.

### Preconditions

- Own purchased plan and authorized Owner monthly-refund handling exist.

### Main Flow

1. [C] Driver confirms cancellation time C and disclosed estimated refund.
2. [C] Owner approves/reconciles the monthly cancellation; cancellation and actual payout remain separate.
3. [C] Do not release resources still physically occupied; assess future paid periods separately.
4. [PROPOSED/OPEN] Keep M-05 formula/rates labelled as proposed where the text says proposed; the header-level C status alone does not approve demo percentages.

### Alternate / Exception Flows

1. At/after E unused value is zero under M-05.
2. Refund cannot erase unrelated legitimate parking obligations.
3. [OPEN] Review whole-VND rounding, unused-fraction formula and failure-specific policy wording before binding acceptance cases.

### Postconditions

- Cancellation/rights and payout remain distinct; no automatic double refund/occupied-space release.

### Open Questions

- SPLIT-CF-09: reconcile confirmed M-05 heading with embedded proposed refund amount/rate wording.

### Traceability

**FR:** FR-MON-11  
**US:** US-D15  
**Rules:** M-05, M-06, M-09

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-fut-01"></a>

## Use Case: UC-FUT-01 — Detect per-slot occupancy and violation candidates

**Status:** DEFERRED / FUTURE  
**Actor(s):** Operator; Recognition Service  
**Related SRS Section(s):** §6.3

### Goal

Future multi-frame occupancy/violation support with human fallback.

### Preconditions

- [F] Approved future release, datasets and per-slot observation infrastructure.

### Main Flow

1. [F] Analyze supported observations and confidence.
2. [F] Retain evidence and request human review.
3. [F] Backend validates any business consequence.

### Alternate / Exception Flows

1. No MVP per-slot camera detection is claimed.

### Postconditions

- Future candidates remain distinct from enforced decisions.

### Open Questions

- AI-DEP-10 and sensor/model/privacy/threshold design.

### Traceability

**FR:** FR-INC-02, FR-BAS-13  
**US:** US-O08  
**Rules:** BR-VIOL-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-fut-02"></a>

## Use Case: UC-FUT-02 — Provide personalized model recommendations

**Status:** DEFERRED / FUTURE  
**Actor(s):** Driver; AI/LLM Service  
**Related SRS Section(s):** §6.4; C-17

### Goal

Future personalized ranking under a separately approved release.

### Preconditions

- [F] Future data/ranking/privacy/evaluation approval.

### Main Flow

1. [F] Use approved history/context to generate personalized options.
2. [F] Validate real availability and expose all nearby options.
3. [C] Current MVP uses deterministic backend ranking instead.

### Alternate / Exception Flows

1. Legacy §6.4 present-tense wording does not activate this model scope.

### Postconditions

- Personalized scope remains future.

### Open Questions

- Reconcile §6.4 with C-17 and approve AI-DEP-10.

### Traceability

**FR:** FR-BAS-12  
**US:** US-D18  
**Rules:** BR-AI-01

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)

<a id="uc-fut-03"></a>

## Use Case: UC-FUT-03 — Forecast capacity and investigate anomalies

**Status:** DEFERRED / FUTURE  
**Actor(s):** Owner; Operator; future AI service  
**Related SRS Section(s):** §6.5–6

### Goal

Future prediction and anomaly support with human decisions.

### Preconditions

- [F] Separately approved release and permitted representative datasets.

### Main Flow

1. [F] Produce forecasts or anomaly candidates under approved model contracts.
2. [F] Present evidence/alerts to authorized staff.
3. [F] Human reviews; backend executes approved operational/financial changes.

### Alternate / Exception Flows

1. No forecast/anomaly acceptance is added to MVP.

### Postconditions

- Future support does not grant autonomous enforcement.

### Open Questions

- AI-DEP-10 and model/data/response design remain deferred.

### Traceability

**FR:** FR-BAS-14, FR-BAS-15  
**US:** US-O10, US-OW08  
**Rules:** BR-AI-04

[Related SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md) · [Detailed rules](../business-rules/smartpark-business-rules-v0.8.5.md) · [User Stories](../user-stories/smartpark-user-stories-v0.8.5.md)
