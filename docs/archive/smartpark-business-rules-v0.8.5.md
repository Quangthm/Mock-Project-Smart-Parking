# SmartPark Detailed Business Rules — v0.8.5

**Document Type:** Detailed Business Rule catalogue  
**Owner:** BA / team lead (review responsibility; no individual assignment)  
**Status:** WORKING — review pending  
**Related SRS Baseline:** Supplied v0.8.5 Working Baseline Revision  
**Date:** 2026-10-01

## 1. Purpose and authority

C means a confirmed fact already recorded in supplied v0.8.5, not a new approval by this document. A means PROPOSED; X means OPEN/conflicting and inactive; F means FUTURE/DEFERRED. Legacy R means a pointer to stated SRS behavior. Mixed R/A, R/X or A/X must be read clause by clause; their candidate clauses do not become confirmed. Main-flow detail marked PROPOSED stays proposed even inside an otherwise current-MVP use case. The split does not resolve architecture, provider, policy-default or event-contract questions. N values and demo/fixture labels retain their original authority.

This companion contains the single detailed BR/M catalogue and policy/scenario material moved from SRS §5.2 and Appendix F. The SRS retains summaries, FRs, domain requirements, non-functional requirements and decision records. Baseline paragraphs elsewhere still define referenced behavior; this catalogue does not invent Business Rules from those paragraphs. Internal section/ID links support traceability without an external bibliography.

**SRS:** [Split SRS](../srs/revisions/smartpark-srs-v0.8.5-split-draft.md); **review:** [conflicts and change treatment](../srs/revisions/smartpark-v0.8.5-split-review.md).

## 2. Rule index

| Rule | Original status | Detailed definition |
| --- | --- | --- |
| [BR-AUTH-01](#br-auth-01) | R/A | AUTH rule |
| [BR-AUTH-02](#br-auth-02) | C | AUTH rule |
| [BR-AUTH-03](#br-auth-03) | A | AUTH rule |
| [BR-VEH-01](#br-veh-01) | R/A | VEH rule |
| [BR-VEH-02](#br-veh-02) | A/X | VEH rule |
| [BR-VEH-03](#br-veh-03) | R/A | VEH rule |
| [BR-CAP-01](#br-cap-01) | C | CAP rule |
| [BR-CAP-02](#br-cap-02) | C | CAP rule |
| [BR-CAP-03](#br-cap-03) | A | CAP rule |
| [BR-CAP-04](#br-cap-04) | C | CAP rule |
| [BR-CAP-05](#br-cap-05) | A | CAP rule |
| [BR-RES-01](#br-res-01) | C | RES rule |
| [BR-RES-02](#br-res-02) | A | RES rule |
| [BR-RES-03](#br-res-03) | C | RES rule |
| [BR-RES-04](#br-res-04) | A/X | RES rule |
| [BR-RES-05](#br-res-05) | R/A | RES rule |
| [BR-RES-06](#br-res-06) | A | RES rule |
| [BR-RES-07](#br-res-07) | R/A | RES rule |
| [BR-RES-08](#br-res-08) | R | RES rule |
| [BR-GATE-01](#br-gate-01) | R | GATE rule |
| [BR-GATE-02](#br-gate-02) | A/X | GATE rule |
| [BR-GATE-03](#br-gate-03) | A/X | GATE rule |
| [BR-GATE-04](#br-gate-04) | A | GATE rule |
| [BR-PAY-01](#br-pay-01) | X | PAY rule |
| [BR-PAY-02](#br-pay-02) | A | PAY rule |
| [BR-PAY-03](#br-pay-03) | C | PAY rule |
| [BR-PAY-04](#br-pay-04) | C | PAY rule |
| [BR-PAY-05](#br-pay-05) | F | PAY rule |
| [BR-PAY-06](#br-pay-06) | A/X | PAY rule |
| [BR-REF-01](#br-ref-01) | A/X | REF rule |
| [BR-VIOL-01](#br-viol-01) | F | VIOL rule |
| [BR-VIOL-02](#br-viol-02) | A | VIOL rule |
| [BR-VIOL-03](#br-viol-03) | R/A | VIOL rule |
| [BR-VIOL-04](#br-viol-04) | C | VIOL rule |
| [BR-EMERG-01](#br-emerg-01) | C | EMERG rule |
| [BR-EMERG-02](#br-emerg-02) | C | EMERG rule |
| [BR-EMERG-03](#br-emerg-03) | C | EMERG rule |
| [BR-EMERG-04](#br-emerg-04) | R/A | EMERG rule |
| [BR-FAIL-01](#br-fail-01) | F | FAIL rule |
| [BR-FAIL-02](#br-fail-02) | F | FAIL rule |
| [BR-FAIL-03](#br-fail-03) | A | FAIL rule |
| [BR-PRIV-01](#br-priv-01) | R/A | PRIV rule |
| [BR-PRIV-02](#br-priv-02) | R/A | PRIV rule |
| [BR-PRIV-03](#br-priv-03) | C | PRIV rule |
| [BR-POL-01](#br-pol-01) | A | POL rule |
| [BR-POL-02](#br-pol-02) | A | POL rule |
| [BR-POL-03](#br-pol-03) | C | POL rule |
| [BR-POL-04](#br-pol-04) | A | POL rule |
| [BR-AI-01](#br-ai-01) | A | AI rule |
| [BR-AI-02](#br-ai-02) | A | AI rule |
| [BR-AI-03](#br-ai-03) | A | AI rule |
| [BR-AI-04](#br-ai-04) | R/A | AI rule |
| [BR-AI-05](#br-ai-05) | A | AI rule |

## 3. Detailed non-monthly rules

<a id="br-auth-01"></a>

### BR-AUTH-01

**Original status:** `R/A`  
**Trigger / context:** Registration, sign-in, provisioning or protected resource access.  
**Functional coverage:** FR-AUTH-02/04/05/06  
**Recorded dependency:** C-02/04

**Retained rule text:**

Role and lot/resource scope follow §2.3 and §3.7.5; detailed permission matrix is §2.3.1. No implicit Admin inheritance of operational or financial rights.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Scope is checked by role and resource; Admin platform access does not imply Operator checkout/refund rights.
- Apply C-02 for devices and C-04 for policy hierarchy; detailed permission rows marked A/X remain inactive.

**Use-case coverage:** UC-AUTH-01, UC-AUTH-02, UC-AUTH-03, UC-LOT-02, UC-POL-02, UC-RPT-01

<a id="br-auth-02"></a>

### BR-AUTH-02

**Original status:** `C`  
**Trigger / context:** Registration, sign-in, provisioning or protected resource access.  
**Functional coverage:** FR-AUTH-01/04, FR-BAS-08  
**Recorded dependency:** C-14/23

**Retained rule text:**

Security-critical authentication values remain constrained. Business-policy variables such as retry limits, notification timing and late tolerance may be configurable within Admin-defined bounds. Owner registration does not require a corporate email domain.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Registration security retains OTP 5 minutes, three failures and 15-minute lock from §3.1.1; do not expose these as arbitrary Owner settings.
- Retry/notification/late-tolerance policy values use Admin defaults/bounds and applicable Owner override. Owner Operator accounts may use any email domain.

**Use-case coverage:** UC-AUTH-01, UC-AUTH-02, UC-AUTH-03

<a id="br-auth-03"></a>

### BR-AUTH-03

**Original status:** `A`  
**Trigger / context:** Registration, sign-in, provisioning or protected resource access.  
**Functional coverage:** FR-AUTH-03/06, FR-AI-05  
**Recorded dependency:** —

**Retained rule text:**

Read/write access is checked against authenticated identity and the particular resource; Operator may access only assigned lot/lane/case data, not the global customer database.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed enforcement uses the authenticated actor and target resource, not model/client-selected ownership.
- Negative review cases include another customer’s booking and an unassigned Operator lot.

**Use-case coverage:** UC-AUTH-01, UC-AUTH-02, UC-PAY-02

<a id="br-veh-01"></a>

### BR-VEH-01

**Original status:** `R/A`  
**Trigger / context:** Vehicle registration/edit, compatibility check or disputed binding.  
**Functional coverage:** FR-VEH-01/03  
**Recorded dependency:** C-23

**Retained rule text:**

Vehicle fields and plate validation follow §3.1.3. Proposed temporary identifiers for approved non-plated categories remain account/session-linked and non-reusable across unrelated vehicles.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Plate/type/image fields and multiple vehicles per account remain the baseline.
- Normalization/editing and non-plated identifiers remain refinements; no document evidence is implied.

**Use-case coverage:** UC-VEH-01

<a id="br-veh-02"></a>

### BR-VEH-02

**Original status:** `A/X`  
**Trigger / context:** Vehicle registration/edit, compatibility check or disputed binding.  
**Functional coverage:** FR-VEH-02  
**Recorded dependency:** C-11

**Retained rule text:**

Proposed: one primary active Driver binding per normalized plate; conflicting claims require a reasoned authorized review; approved transfers never rewrite historical sessions/bookings. Evidence must not be inferred from OTP alone.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Duplicate binding review, primary active binding and transfer procedure remain A/X.
- C-11 excludes CCCD/GPLX; identify permitted evidence and authorized reviewer before enabling transfer.

**Use-case coverage:** UC-VEH-02

<a id="br-veh-03"></a>

### BR-VEH-03

**Original status:** `R/A`  
**Trigger / context:** Vehicle registration/edit, compatibility check or disputed binding.  
**Functional coverage:** FR-VEH-04  
**Recorded dependency:** C-23

**Retained rule text:**

Compatibility is required by §3.4.3. Supported motorcycle parking is part of the baseline. An EV-compatible slot is a slot type/attribute used for occupancy, allocation and payment only; charging functionality and charging-state validation are not implemented. Other proposed size/type constraints remain refinement.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Motorcycle support and non-charging EV slot attributes are confirmed by C-23.
- Other dimensions/restricted-bay details remain proposals; a charging-state API must not be introduced.

**Use-case coverage:** UC-VEH-01

<a id="br-cap-01"></a>

### BR-CAP-01

**Original status:** `C`  
**Trigger / context:** Availability query, hold, allocation, entry, cancellation or departure accounting.  
**Functional coverage:** FR-CAP-02, FR-VEH-04  
**Recorded dependency:** C-23

**Retained rule text:**

Capacity is reported and validated separately by supported vehicle type, including motorcycle. Unused motorcycle capacity does not demonstrate automobile capacity.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Use compatible category capacity; free motorcycle units cannot admit an automobile.
- This is a category invariant, not approval of a particular layout/slot-record schema.

**Use-case coverage:** UC-VEH-01, UC-LOT-01, UC-SEARCH-01

<a id="br-cap-02"></a>

### BR-CAP-02

**Original status:** `C`  
**Trigger / context:** Availability query, hold, allocation, entry, cancellation or departure accounting.  
**Functional coverage:** FR-CAP-03/06  
**Recorded dependency:** C-06

**Retained rule text:**

Canonical capacity states remain separate: PENDING_PAYMENT consumes temporary capacity during the active hold; RESERVED follows successful payment; OCCUPIED is physical reality; PROTECTED is reservation protection; BACKUP is excluded from ordinary availability. Available Capacity = Total Capacity − Occupied − Protected − Pending Payment − Backup.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Apply the active payment hold to availability without labelling it RESERVED.
- Example in §3.4.5: 100−60−20−5−10=5. The categories must not double-count the same unit; representation remains open.

**Use-case coverage:** UC-RES-01

<a id="br-cap-03"></a>

### BR-CAP-03

**Original status:** `A`  
**Trigger / context:** Availability query, hold, allocation, entry, cancellation or departure accounting.  
**Functional coverage:** FR-CAP-04, FR-RES-12  
**Recorded dependency:** ARCH-DEF-09

**Retained rule text:**

No incompatible claims may consume the same last resource. Idempotent retries return the same logical result and do not deduct again. Implementation lock/database/event strategy is not prescribed.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Review concurrent last-unit requests and duplicate retries as separate attempts with one logical effect.
- The invariant is a proposed refinement here; this document does not select SQL locks, distributed transactions or event topology.

**Use-case coverage:** UC-RES-01

<a id="br-cap-04"></a>

### BR-CAP-04

**Original status:** `C`  
**Trigger / context:** Availability query, hold, allocation, entry, cancellation or departure accounting.  
**Functional coverage:** FR-CAP-07, FR-BAS-04  
**Recorded dependency:** C-06

**Retained rule text:**

Backup inventory is excluded from ordinary availability. Owner-configured backup capacity may be a fixed capacity allocation, while the dynamic protection backup pool protects reservation-related capacity during the Protection Window. A protected/backup capacity claim never forces an already-occupied vehicle to move.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Configured backup and dynamic reservation-protection capacity remain distinct.
- A protection claim is not permission to evict an already occupied vehicle. Operator-designated backup stays backup until policy release.

**Use-case coverage:** UC-POL-01, UC-RES-02

<a id="br-cap-05"></a>

### BR-CAP-05

**Original status:** `A`  
**Trigger / context:** Availability query, hold, allocation, entry, cancellation or departure accounting.  
**Functional coverage:** FR-CAP-05, FR-GATE-05/09  
**Recorded dependency:** TESTER-DEF-05/06

**Retained rule text:**

A claim becoming occupied changes accounting once; physical/protection states may coexist. Expiry/cancellation of a booking cannot release a vehicle's still-occupied space; release follows verified departure.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed conversion/release must preserve actual occupation after cancellation or expiry.
- Monthly M-04 separately confirms the same occupied-vehicle boundary for monthly rights; do not promote the whole ordinary transition proposal from that narrower fact.

**Use-case coverage:** UC-GATE-01

<a id="br-res-01"></a>

### BR-RES-01

**Original status:** `C`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-03/13, FR-BAS-01/02  
**Recorded dependency:** C-03

**Retained rule text:**

Specific Slot, Zone and Capacity are the canonical reservation modes. Specific Slot expresses a preferred physical slot, not an absolute arrival guarantee. Allocation begins at Allocation Time = Reservation Start − Allocation Lead Time.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Canonical choices are Specific Slot, Zone and Capacity; selected slot is a preference.
- Allocation Time=start−lead; hard immediate assignment and gate-only allocation are not alternative approved modes.

**Use-case coverage:** UC-RES-01, UC-RES-02

<a id="br-res-02"></a>

### BR-RES-02

**Original status:** `A`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-02  
**Recorded dependency:** TESTER-DEF-01/08

**Retained rule text:**

Proposed configurable advance window, active-booking count and action-rate window; reject overlapping incompatible reservations for the same vehicle. Active-state counting and all N values must be approved, not inferred.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Advance/action/active-booking limits and overlap rules remain proposed.
- Approve counted states/events and interval boundaries before adopting sample N values.

**Use-case coverage:** UC-RES-01

<a id="br-res-03"></a>

### BR-RES-03

**Original status:** `C`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-06/11  
**Recorded dependency:** C-05

**Retained rule text:**

Free-parking duration and arrival eligibility are separate. Without an Owner late-tolerance policy, reservation entitlement lasts until end time. With an enabled Late Arrival Tolerance, no authoritative arrival by start + tolerance produces NO_SHOW and releases protected capacity according to policy.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Unpaid checkout expiry is EXPIRED; enabled late-tolerance absence of authoritative arrival is NO_SHOW.
- Disabled late tolerance preserves validity to end time subject to other applicable conditions; grace for free parking is a different policy.

**Use-case coverage:** UC-RES-03

<a id="br-res-04"></a>

### BR-RES-04

**Original status:** `A/X`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-07  
**Recorded dependency:** C-03/08

**Retained rule text:**

Early entry requires compatible actual capacity and published extra-time charges; if unavailable, offer waiting or an authorized alternative. An occupied vehicle is never automatically displaced (§3.4.11).

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Early arrival checks actual eligible capacity; published extra-time pricing is not automatically the proposed day/night formula.
- If unavailable, operational waiting/alternatives remain proposed; no displaced occupied car.

**Use-case coverage:** UC-GATE-01

<a id="br-res-05"></a>

### BR-RES-05

**Original status:** `R/A`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-08/09, FR-PAY-06  
**Recorded dependency:** C-12

**Retained rule text:**

Cancellation/refund entitlement follows §3.4.10. Proposed cancellation is validated again at execution, releases only unused rights once, and shows refund as a separate process. A live occupied session cannot be freed by cancellation.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Driver cancellation ends reservation entitlement under §3.4.10.
- Refund eligibility follows the effective Owner policy; physical occupation and payout are separate outcomes.

**Use-case coverage:** UC-RES-03

<a id="br-res-06"></a>

### BR-RES-06

**Original status:** `A`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-CAP-03, FR-RES-02  
**Recorded dependency:** C-06

**Retained rule text:**

A configurable inter-booking buffer may protect successive use of the same physical resource; buffer must participate in availability checks and is not physical occupancy or a second booking.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Inter-booking buffer is an optional proposed claim interval, not actual occupation.
- Demo 15 minutes is not an approved default.

**Use-case coverage:** UC-RES-01

<a id="br-res-07"></a>

### BR-RES-07

**Original status:** `R/A`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-RES-12  
**Recorded dependency:** TESTER-DEF-01/12

**Retained rule text:**

Lifecycle meanings remain §3.4.2. Candidate transitions must validate current state and preserve one consistent outcome for simultaneous payment, cancellation, expiry and entry; missing transition definitions stay open.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Keep reservation, payment, protection, session and physical state separate.
- Full transitions, simultaneous outcomes and precise scheduler timing remain TESTER-DEF-01/12 work.

**Use-case coverage:** UC-RES-03

<a id="br-res-08"></a>

### BR-RES-08

**Original status:** `R`  
**Trigger / context:** Reservation creation, allocation, arrival, cancellation, expiry or reallocation.  
**Functional coverage:** FR-BAS-02/03/04  
**Recorded dependency:** C-18

**Retained rule text:**

Normal and exceptional physical-arrival priorities, no automatic displacement after occupation, accepted-reallocation default refund treatment and request limit are defined once in §3.4.6/7.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Normal priority: earlier start, then earlier confirmation. Unexpected loss after allocation uses affected-driver gate-arrival priority.
- C-18 confirms configurable default 2 and Operator simultaneous-arrival tie-break with audit; stale fixed-two text is SPLIT-CF-05.

**Use-case coverage:** UC-RES-02

<a id="br-gate-01"></a>

### BR-GATE-01

**Original status:** `R`  
**Trigger / context:** Vehicle identification, arrival/passage evidence, session intake or checkout.  
**Functional coverage:** FR-GATE-01  
**Recorded dependency:** C-14/15

**Retained rule text:**

Recognition is evidence; backend authorization remains required by §3.4.11 and §6.1/6.2. Manual/QR fallback cannot bypass identity, capacity or payment checks.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Recognition result/QR is input evidence; backend validates authorization and compatible capacity.
- LPR equality at exactly 90% is open; real barrier integrations remain deferred.

**Use-case coverage:** UC-POL-02, UC-GATE-01, UC-LPR-01

<a id="br-gate-02"></a>

### BR-GATE-02

**Original status:** `A/X`  
**Trigger / context:** Vehicle identification, arrival/passage evidence, session intake or checkout.  
**Functional coverage:** FR-GATE-03/05/09  
**Recorded dependency:** TESTER-DEF-35; C-21

**Retained rule text:**

A scan/open command is not passage. Proposed session starts on verified entry and closes on verified exit; ticket is one-use and one vehicle cannot have duplicate open sessions. Payment, entitlement and actual occupancy remain separate.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Arrival at the gate, scan, open command, verified passage and occupation are different events.
- The proposed event/ticket/session contract still needs TESTER-DEF-35; no synthetic command implies actual departure.

**Use-case coverage:** UC-GATE-01, UC-GATE-02, UC-MON-04

<a id="br-gate-03"></a>

### BR-GATE-03

**Original status:** `A/X`  
**Trigger / context:** Vehicle identification, arrival/passage evidence, session intake or checkout.  
**Functional coverage:** FR-GATE-07  
**Recorded dependency:** C-11

**Retained rule text:**

Lost-ticket flow requires authorized lookup, comparison against entry evidence and vehicle/account verification, published extra fee if applicable, and reasoned audit. Never infer successful verification from a plate match alone.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Lost-ticket identity verification and extra fee remain A/X, not an automatic plate-match release.
- Define permitted evidence under no-CCCD/GPLX collection and explicit Operator permission.

**Use-case coverage:** UC-GATE-02

<a id="br-gate-04"></a>

### BR-GATE-04

**Original status:** `A`  
**Trigger / context:** Vehicle identification, arrival/passage evidence, session intake or checkout.  
**Functional coverage:** FR-GATE-06  
**Recorded dependency:** —

**Retained rule text:**

Plate/token/session mismatch stops the automatic exit process and raises an Operator review; the software does not declare theft or levy a fine from mismatch alone.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed mismatch handling stops automation for human review.
- A mismatch alone does not prove theft or justify a fine.

**Use-case coverage:** UC-GATE-02

<a id="br-pay-01"></a>

### BR-PAY-01

**Original status:** `X`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-01/02/10  
**Recorded dependency:** C-08

**Retained rule text:**

Proposed TIME_SLICING splits [entry,billing-end) at each configured day/night boundary, rounds each nonempty continuous segment up to its N-minute block, and sums segment rates. Do not merge distinct days, double-add base fees or combine with fixed overnight surcharge. Baseline §3.5.1 remains unresolved.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- TIME_SLICING and 60-minute demo blocks remain X. C-08 confirms Owner time-block capability only.
- Do not layer this candidate on existing guest-only overnight tiers; final rounding/crossing algorithm remains open.

**Use-case coverage:** UC-POL-01

<a id="br-pay-02"></a>

### BR-PAY-02

**Original status:** `A`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-08  
**Recorded dependency:** C-08/12

**Retained rule text:**

Proposed overstay: published ordinary time charge plus separately itemized N-configured penalty; send warning N time before expiry. Actual occupancy persists even after reservation time ends. Human-confirmed violation charges remain separate.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Warning/penalty bands are proposals; show separate time charge versus confirmed violation charge.
- Actual overstay occupation continues even after entitlement expires.

**Use-case coverage:** UC-NOT-01

<a id="br-pay-03"></a>

### BR-PAY-03

**Original status:** `C`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-03/07, FR-BAS-16  
**Recorded dependency:** C-10

**Retained rule text:**

MVP payment channels are VNPay, MoMo and ZaloPay, with CASH_COLLECT for Operator-recorded manual cash collection; PDF invoice behavior remains §3.5.3. Additional provider/card channels are future extensions.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Use VNPay/MoMo/ZaloPay and explicitly granted CASH_COLLECT. Additional card/provider channels are future.
- Provider result validation, idempotency/webhooks and PDF invoices remain applicable §3.5.3.

**Use-case coverage:** UC-PAY-01, UC-PAY-02

<a id="br-pay-04"></a>

### BR-PAY-04

**Original status:** `C`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-04/05  
**Recorded dependency:** C-09; TESTER-DEF-12

**Retained rule text:**

Provider results are validated and idempotent. A payment that succeeds after hold expiry may still create the reservation when the requested capacity remains free. If a competing reservation has already won the backend race, the losing paid attempt is notified and is eligible for refund/reconciliation. Unknown and duplicate money remain separate reconciliation outcomes.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Late ordinary reservation success: recheck free compatible capacity and resolve the winning claim; notify/refund-reconcile a paid loser.
- Do not apply this exception to monthly strict A<H. UNKNOWN requires result lookup, not automatic retry.

**Use-case coverage:** UC-PAY-01

<a id="br-pay-05"></a>

### BR-PAY-05

**Original status:** `F`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-11, FR-GATE-05  
**Recorded dependency:** C-21

**Retained rule text:**

Proposed exit quote/clearance/requote behavior is retained as an inactive proposal for traceability. It is not the v0.8.5 billing rule; actual recorded exit determines the final price.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- The entire quote/clearance/requote alternative is inactive; C-21 actual entry/exit is normative.
- Do not seed EXIT_QUOTE_VALIDITY/EXIT_CLEARANCE_WINDOW in a baseline environment.

**Use-case coverage:** UC-GATE-02

<a id="br-pay-06"></a>

### BR-PAY-06

**Original status:** `A/X`  
**Trigger / context:** Pricing, payment initiation/callback/retry, overstay or checkout.  
**Functional coverage:** FR-PAY-09/11  
**Recorded dependency:** C-08/21

**Retained rule text:**

Proposed free short stay: N>0 and actual exit-entry<=N makes time-based parking charge zero; N=0 disables. If exceeded, bill the full chargeable interval, not duration minus N. Separate approved penalties/service fees/deposit consequences remain. Free exit right cannot extend beyond min(D,entry+N); late passage triggers settlement/reconciliation. Overpayment produces a refund request, not assumed completion.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Exact N free, N+epsilon full interval and N=0 disabled are proposed boundaries, not already approved by §3.5.4.
- The D-based exit cutoff depends on inactive quote/clearance behavior; retain it for review only (SPLIT-CF-08).

**Use-case coverage:** UC-GATE-02

<a id="br-ref-01"></a>

### BR-REF-01

**Original status:** `A/X`  
**Trigger / context:** Refund request, approval, provider processing or result lookup.  
**Functional coverage:** FR-PAY-06, FR-INC-05  
**Recorded dependency:** C-12/14

**Retained rule text:**

Proposed refund workflow separates request, human approval, provider submission and confirmed paid-back result. Owner is proposed approver; retries are idempotent and aggregate refunds cannot exceed eligible collected money. ACCEPTED appeal is not REFUNDED. Baseline turnaround remains §3.5.5 until reviewed.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- General refund state/limit/retry elaboration remains proposed; existing full/partial refunds and 3–5 business-day target are baseline.
- C-12 ordinary appeals use Operator approval with Owner escalation, not blanket Owner-only authority. Monthly cancellation uses Owner under M-05.

**Use-case coverage:** UC-INC-02

<a id="br-viol-01"></a>

### BR-VIOL-01

**Original status:** `F`  
**Trigger / context:** Suspected violation, human review or appeal.  
**Functional coverage:** FR-INC-02  
**Recorded dependency:** §1.2, §6.3

**Retained rule text:**

Future per-slot automated detection must use configurable persistence, multiple frames and confidence, retain evidence and request human review. Do not implement it as MVP image-input LPR.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Future multi-frame/per-slot evidence is separate from MVP image-input plate recognition.
- No MVP camera-per-slot infrastructure or automatic fine is added.

**Use-case coverage:** UC-FUT-01

<a id="br-viol-02"></a>

### BR-VIOL-02

**Original status:** `A`  
**Trigger / context:** Suspected violation, human review or appeal.  
**Functional coverage:** FR-INC-03  
**Recorded dependency:** C-12

**Retained rule text:**

MVP candidate: Operator inspects and records suspected wrong-slot use against actual allocation; no per-slot AI assumed. An accepted alternate allocation is not a violation merely because the requested slot differs.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- The MVP candidate is an Operator’s direct observation and recorded allegation.
- Compare against actual accepted allocation; a different originally requested slot alone is not wrong-slot parking.

**Use-case coverage:** UC-INC-01

<a id="br-viol-03"></a>

### BR-VIOL-03

**Original status:** `R/A`  
**Trigger / context:** Suspected violation, human review or appeal.  
**Functional coverage:** FR-INC-04  
**Recorded dependency:** C-12

**Retained rule text:**

AI cannot make punitive/legal decisions (§6.1). Proposed authorized human review records evidence, confirmed/rejected outcome and reason before a charge is added.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Human punitive/legal authority is baseline; proposed evidence/review states and fee entry need review.
- AI suspicion cannot itself become an enforceable penalty.

**Use-case coverage:** UC-INC-01

<a id="br-viol-04"></a>

### BR-VIOL-04

**Original status:** `C`  
**Trigger / context:** Suspected violation, human review or appeal.  
**Functional coverage:** FR-INC-05/07  
**Recorded dependency:** C-12

**Retained rule text:**

Appeal workflow is appeal → evidence → Operator approval → refund. ACCEPTED is a decision state and does not mean REFUNDED. An Owner may complete refund handling when an Operator escalates the appeal.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Use appeal→evidence→Operator approval→refund; Owner may handle escalated refund.
- ACCEPTED indicates decision, not bank/provider payout; detailed states/redaction remain refinement.

**Use-case coverage:** UC-INC-02

<a id="br-emerg-01"></a>

### BR-EMERG-01

**Original status:** `C`  
**Trigger / context:** Operational override, emergency, unknown state or recovery.  
**Functional coverage:** FR-INC-06, FR-MAP-02  
**Recorded dependency:** C-07

**Retained rule text:**

Backend state remains authoritative over visualization. Manual override has authority only when initiated by an authorized actor under an approved operational condition and is audited; no blanket manual precedence applies.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Backend state is authoritative over visual display; manual authority requires actor permission and approved condition.
- No global manual-over-sensor precedence or automatic real-IoT scope is inferred.

**Use-case coverage:** UC-LOT-01, UC-INC-01, UC-MAP-01

<a id="br-emerg-02"></a>

### BR-EMERG-02

**Original status:** `C`  
**Trigger / context:** Operational override, emergency, unknown state or recovery.  
**Functional coverage:** FR-OPS-01  
**Recorded dependency:** C-13

**Retained rule text:**

Emergency handling uses permission-gated release/priority actions. It does not imply physically impossible over-capacity admission or forced eviction of occupied vehicles.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Emergency release/priority is explicitly permission-gated.
- Paid/emergency entitlement never implies impossible occupancy or automatic forced eviction.

**Use-case coverage:** UC-OPS-01

<a id="br-emerg-03"></a>

### BR-EMERG-03

**Original status:** `C`  
**Trigger / context:** Operational override, emergency, unknown state or recovery.  
**Functional coverage:** FR-OPS-02/06  
**Recorded dependency:** C-13/15

**Retained rule text:**

Emergency release/priority actions record the authorized actor, time, relevant gate/lot context, reason and outcome. Physical/manual recovery actions must be recorded when connectivity is restored.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Record actor, timestamp, relevant resource, reason and actual outcome for emergency actions.
- Record actual manual recovery when connected; this does not activate future durable offline replay.

**Use-case coverage:** UC-OPS-01

<a id="br-emerg-04"></a>

### BR-EMERG-04

**Original status:** `R/A`  
**Trigger / context:** Operational override, emergency, unknown state or recovery.  
**Functional coverage:** FR-OPS-03  
**Recorded dependency:** C-07

**Retained rule text:**

UNKNOWN means unverifiable physical state (§3.2.2); proposed allocation excludes it until reliable verification. Restoring availability requires the approved reconciliation process, not UI inference.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- UNKNOWN is unverifiable physical state; visual availability must not replace verification.
- Allocation exclusion and restoration/reconciliation mechanics remain proposed.

**Use-case coverage:** UC-INC-01

<a id="br-fail-01"></a>

### BR-FAIL-01

**Original status:** `F`  
**Trigger / context:** Service/connectivity/hardware failure or a deferred offline operation.  
**Functional coverage:** FR-OPS-04  
**Recorded dependency:** Future security/conflict design

**Retained rule text:**

Cached offline entitlement evaluation is future work; full offline mode is excluded by §1.2.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Cached offline authorization is F and outside current MVP.
- Future stale-credential/resource conflict rules must be separately specified.

**Use-case coverage:** UC-OPS-02

<a id="br-fail-02"></a>

### BR-FAIL-02

**Original status:** `F`  
**Trigger / context:** Service/connectivity/hardware failure or a deferred offline operation.  
**Functional coverage:** FR-OPS-05  
**Recorded dependency:** Future synchronization design

**Retained rule text:**

Durable offline replay/reconciliation is future work and must prevent duplicate sessions, capacity deductions and financial effects.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Durable offline replay and its duplicate/conflict guarantees are F.
- Manual emergency recovery records do not imply a functioning two-way offline queue.

**Use-case coverage:** UC-OPS-02

<a id="br-fail-03"></a>

### BR-FAIL-03

**Original status:** `A`  
**Trigger / context:** Service/connectivity/hardware failure or a deferred offline operation.  
**Functional coverage:** FR-OPS-06, FR-PAY-05  
**Recorded dependency:** C-13/15

**Retained rule text:**

A service/hardware outage is not proof of payment or fee waiver. MVP stops claiming successful automatic processing, uses an approved local incident procedure, then records actual events and reconciles when connected.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Report unavailable automation and follow a separately approved local incident procedure.
- Never turn a connectivity error into successful payment, remote command or automatic fee waiver.

**Use-case coverage:** UC-LOT-02, UC-OPS-01, UC-NOT-01, UC-SIM-01

<a id="br-priv-01"></a>

### BR-PRIV-01

**Original status:** `R/A`  
**Trigger / context:** Data collection, scoped disclosure, logging, retention, hold or deletion.  
**Functional coverage:** FR-AUTH-06, FR-RPT-05  
**Recorded dependency:** C-11/20

**Retained rule text:**

Apply least data exposure and approved processing boundaries under §4.3, §5.1 and §6.9.2; do not treat unspecified identity fields as authority to collect documents.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Process only approved data; generic encryption does not authorize CCCD/GPLX collection.
- Least exposure and scopes apply to views, APIs, tools and reports; detailed minimization remains R/A.

**Use-case coverage:** UC-RPT-01, UC-RPT-02, UC-AI-03

<a id="br-priv-02"></a>

### BR-PRIV-02

**Original status:** `R/A`  
**Trigger / context:** Data collection, scoped disclosure, logging, retention, hold or deletion.  
**Functional coverage:** FR-RPT-05  
**Recorded dependency:** C-20

**Retained rule text:**

Retention categories and legal/incident exceptions stay as written in §4.3/4.8/6.2.3. Proposed expiry jobs honor authorized holds and deletion rights; N values cannot override approved constraints.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Respect distinct approved categories and authorized holds.
- Deletion/expiry jobs are proposals; do not replace confirmed ceilings with unrestricted N.

**Use-case coverage:** UC-RPT-02, UC-LPR-01

<a id="br-priv-03"></a>

### BR-PRIV-03

**Original status:** `C`  
**Trigger / context:** Data collection, scoped disclosure, logging, retention, hold or deletion.  
**Functional coverage:** FR-RPT-03/04  
**Recorded dependency:** C-20

**Retained rule text:**

Audit records capture who, when, resource/location, action, reason and outcome. Retention remains 7 years for audit logs, 90 days hot plus 1 year cold for operational logs, and maximum 30 days for images with authorized holds; conversations are logged. Retention categories remain distinct.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Audit=7 years; operational=90 days hot+1 year cold; images=max 30 days with authorized holds; conversations logged.
- Conversation retention duration is still unspecified; do not equate it to audit retention.

**Use-case coverage:** UC-AUTH-02, UC-RPT-02

<a id="br-pol-01"></a>

### BR-POL-01

**Original status:** `A`  
**Trigger / context:** Policy review, configuration, activation or transaction-term resolution.  
**Functional coverage:** FR-POL-03/07  
**Recorded dependency:** TESTER-DEF-07/08

**Retained rule text:**

Each policy has its own identifier, typed value, unit, scope, permitted editor/override, validity domain and effective revision. N is a placeholder in requirements, never a shared variable or production literal.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Each N belongs to its own typed key/unit/scope, never a shared runtime N.
- The detailed storage/catalogue shape is proposed despite confirmed Admin/Owner hierarchy.

**Use-case coverage:** UC-POL-01

<a id="br-pol-02"></a>

### BR-POL-02

**Original status:** `A`  
**Trigger / context:** Policy review, configuration, activation or transaction-term resolution.  
**Functional coverage:** FR-POL-04/08  
**Recorded dependency:** TESTER-DEF-08/09

**Retained rule text:**

Reject activation of missing/invalid values, overlapping or uncovered applicable tariff/refund bands, or unresolved precedence. Invalid configuration is not interpreted as a zero fee or unlimited capacity.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Missing/invalid/overlapping configuration must not be interpreted as free parking or infinite capacity under the proposal.
- Range defaults and activation domains still need approval; no demo seed creates authority.

**Use-case coverage:** UC-POL-01

<a id="br-pol-03"></a>

### BR-POL-03

**Original status:** `C`  
**Trigger / context:** Policy review, configuration, activation or transaction-term resolution.  
**Functional coverage:** FR-POL-05/08  
**Recorded dependency:** C-22

**Retained rule text:**

An accepted booking retains its accepted policy/price version. Later policy revisions apply to future applicable transactions and do not retroactively reprice the accepted booking.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Preserve accepted booking price/policy version; future revision does not retroactively reprice it.
- Exact persistence/version schema and transaction effective-time representation remain refinement.

**Use-case coverage:** UC-POL-01, UC-MON-01

<a id="br-pol-04"></a>

### BR-POL-04

**Original status:** `A`  
**Trigger / context:** Policy review, configuration, activation or transaction-term resolution.  
**Functional coverage:** FR-POL-06, FR-AI-07  
**Recorded dependency:** —

**Retained rule text:**

Backend supplies effective terms; before executing a transaction, changed price/policy/availability relative to reviewed terms requires fresh review. Web and chatbot invoke the same validation path.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed refreshed review binds actual current backend terms across web/chat.
- A model response cannot approve stale availability or changed price on behalf of the user.

**Use-case coverage:** UC-RES-01, UC-AI-02

<a id="br-ai-01"></a>

### BR-AI-01

**Original status:** `A`  
**Trigger / context:** FAQ/tool lookup, draft preparation, confirmation or API error response.  
**Functional coverage:** FR-AI-02/03/04/11  
**Recorded dependency:** C-17

**Retained rule text:**

Static answers use approved help content; live price, availability, policy and personal state use authoritative APIs. Model output does not replace price/ranking/allocation engines or invent N values.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- C-17 confirms deterministic backend ranking, not model personalization.
- Approved FAQ and live-data grounding workflows remain proposed contracts; unresolved policies are not published as active help.

**Use-case coverage:** UC-SEARCH-01, UC-AI-01, UC-FUT-02

<a id="br-ai-02"></a>

### BR-AI-02

**Original status:** `A`  
**Trigger / context:** FAQ/tool lookup, draft preparation, confirmation or API error response.  
**Functional coverage:** FR-AI-05/10  
**Recorded dependency:** AI-DEP-02/06

**Retained rule text:**

Tools are allowlisted and authorized for the authenticated session on every call; model-supplied user IDs cannot change account scope. Do not forward credentials or unrelated personal records to the model.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed allowlist/resource authorization validates each call using authenticated session scope.
- Credentials and unrelated personal records are not model inputs; detailed provider handling remains AI-DEP-02/06.

**Use-case coverage:** UC-AI-01, UC-AI-02, UC-AI-03

<a id="br-ai-03"></a>

### BR-AI-03

**Original status:** `A`  
**Trigger / context:** FAQ/tool lookup, draft preparation, confirmation or API error response.  
**Functional coverage:** FR-AI-06/07  
**Recorded dependency:** AI-DEP-04

**Retained rule text:**

Reservation preparation is a draft. Final UI review/edit/confirmation binds the submitted parameters; later changes invalidate that confirmation. Backend revalidates before creating; a conversational yes alone does not silently submit an altered form.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Reservation draft/form is not a business write. C-16 confirms user review/backend execution.
- Binding final form parameters and invalidating changed confirmations remain detailed proposed contracts.

**Use-case coverage:** UC-AI-02

<a id="br-ai-04"></a>

### BR-AI-04

**Original status:** `R/A`  
**Trigger / context:** FAQ/tool lookup, draft preparation, confirmation or API error response.  
**Functional coverage:** FR-AI-08/10  
**Recorded dependency:** C-16

**Retained rule text:**

Financial/access/legal authority stays in §6.1. No raw DB, barrier, policy override or direct charge/refund tools for the model. Ordinary checkout and human refund decisions remain distinct from AI assistance.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- AI cannot charge/refund, grant access, execute raw SQL or override policies directly.
- Intent-specific UI handoff versus backend write remains open; ordinary checkout and human refund review are retained.

**Use-case coverage:** UC-AI-01, UC-AI-02, UC-FUT-03

<a id="br-ai-05"></a>

### BR-AI-05

**Original status:** `A`  
**Trigger / context:** FAQ/tool lookup, draft preparation, confirmation or API error response.  
**Functional coverage:** FR-AI-09  
**Recorded dependency:** AI-DEP-02/04

**Retained rule text:**

Report actual API outcome: empty data is different from unauthorized, invalid input, conflict, timeout or service failure. Read retry may be bounded; write retry requires a stable idempotency/result-lookup contract.

**Detailed interpretation / review checks (same authority as the underlying clauses):**

- Proposed errors distinguish empty, forbidden, invalid, conflict, timeout and unavailable.
- Unknown write result needs stable lookup/idempotency contract before retry; do not claim model-side success.

**Use-case coverage:** UC-AI-01

## 4. Retained boundary scenarios — original wording

The following material is moved verbatim from §5.2.2. It includes stale/conflicting cases identified in the review; it is not an unqualified acceptance suite. Corrected authority-sensitive review cases are in §5 below.

These are review scenarios, not claims of executable tests or approved acceptance criteria for X behavior. Complete existing v0.8 acceptance remains subject to the open decisions.

| Scenario | Expected result or decision gate | Coverage |
| --- | --- | --- |
| Two simultaneous claims for the last compatible unit | At most one incompatible claim succeeds; retry has no extra effect. | BR-CAP-03; FR-CAP-04 |
| Unpaid checkout hold | Blocks incompatible claims; reserved total is unchanged; expiry follows Admin5-minute default unless configured otherwise. | §3.4.2/3; C-04 |
| Reservation reaches Allocation Time | Apply baseline start-time then confirmation-time priority; not unconditional gate-only allocation. | §3.4.6; C-03 |
| Allocated slot lost before arrival | Apply exceptional affected-driver physical-arrival priority; no automatic eviction of an occupied car. | §3.4.7; equal/3+ arrival cases await C-18 |
| Late paid callback after hold expiry | Proposed reconciliation without entitlement revival; QA must not mark expected outcome confirmed until C-09 is closed. | BR-PAY-04 |
| Accepted alternate slot | Default no refund solely for accepting reallocation; payment remains linked to session and is not charged again for changing slot. | §3.4.6 |
| Short stay exactly N versus N+epsilon | Proposed exact N free, above N full interval billed; no subtraction of N. | BR-PAY-06; C-08 |
| Day/night crossing20:30–23:30 | Candidate60-minute blocks:2 day blocks+2 night blocks; demo20000+30000=50000. This is not a baseline fee test until C-08. | BR-PAY-01 |
| Quoted exit expires | Candidate T=11:00,Q=11:02,D=11:05: settlement11:01 permits exit11:04; requote11:06 subtracts prior receipts; occupancy remains until passage. | BR-PAY-05; C-21 |
| Appeal accepted | Proposed Owner refund approval and actual payout still separate; do not display REFUNDED yet. | BR-VIOL-04; C-12 |
| Model returns LPR confidence exactly90% | Unspecified baseline equality; must be decided, not guessed by QA. | C-14 |
| Other user's booking ID in chat | Backend denies access; model never retries with a different user identity. | BR-AI-02 |
| Search API timeout | Report temporary failure; do not say every lot is full. | BR-AI-05 |
| Reservation draft edited after confirmation | Prior confirmation cannot submit new terms; show revised form and require confirmation. | BR-AI-03 |
| Repeated write/checkout action | One logical reservation/payment effect; result lookup/reconciliation after unknown result. | BR-CAP-03, BR-PAY-04 |
| Provider unavailable | Do not fabricate payment success or erase fee; display incident/fallback path. | BR-FAIL-03 |

## 5. Authority-sensitive review scenarios

| Scenario | Applicable result / authority | Coverage |
| --- | --- | --- |
| Late ordinary reservation success, compatible capacity free | C: validate/win the backend claim and allow confirmation; not automatic rejection merely because hold expired. | BR-PAY-04; C-09 |
| Late ordinary reservation success, competing claim won | C: notify the paid loser and enter refund/reconciliation; do not displace the winner. | BR-PAY-04; C-09 |
| Monthly fulfillment A=H or A>H | C: no entitlement, even with free capacity; reconcile/refund actual received money. | M-02/M-03 |
| Enabled late tolerance; no authoritative arrival at cutoff | C: NO_SHOW, policy release/refund; unpaid checkout expiry is EXPIRED. Exact event ordering still requires a contract. | BR-RES-03; C-05 |
| Simultaneous arrival after unexpected allocation loss | C: Operator resolves/logs tie-break; no automatic new FIFO timestamp rule is invented. | BR-RES-08; C-18 |
| Ordinary appeal accepted | C: Operator approval and applicable refund; Owner escalation allowed; ACCEPTED is not payout. | BR-VIOL-04; C-12 |
| Owner device-report access | F: reporting deferred; assigned Operator management confirmed; no Owner management inheritance. | C-02; SPLIT-CF-04 |
| Exact free-parking threshold / day-night crossing | A/X: no binding exact-N or TIME_SLICING expectation until remaining fee details approved. | BR-PAY-01/06 |
| M-05 unused-value formula and demo percentages | OPEN/PROPOSED amount wording retained; actual Owner monthly authority is confirmed. | SPLIT-CF-09 |
| Projection reaches 4 hours while vehicle still present | C: projection is not departure evidence; retain actual occupation unless authoritative observation changes it. | C-06; M-04 |

## 6. Non-monthly policy register — original wording

This is the single current-phase policy register. Every N is an independent configurable value with a unit, not a literal value shown to customers. Numeric examples are isolated Dev/QA proposals unless the row explicitly identifies an existing baseline default. Withheld candidates must not seed a baseline environment. Existing fixed values remain unchanged until a recorded approval converts them to N. Technical performance/SLO metrics stay in §4/§6, not automatically in this business-policy catalogue. Allowed ranges below are proposals unless already fixed by baseline; TESTER-DEF-08 remains open until every field's detailed default, validation and representation are agreed. Current MVP monthly-plan settings are defined in Appendix F; unrelated future settings remain non-normative.

| Parameter | Unit / type | Configuration authority | Baseline value / proposed demo value | Validation / activation condition |
| --- | --- | --- | --- | --- |
| OTP_EXPIRY / OTP_MAX_FAILURES / OTP_LOCK_DURATION | Minutes / count / minutes | Admin proposed; security baseline retained | 5 / 3 / 15 in §3.1.1/4.3 | N conversion pending C-14; positive integer counts/durations proposed. |
| SESSION_TOKEN_EXPIRY / REFRESH_TOKEN_EXPIRY | Duration | Admin proposed | 24 hours / 7 days §3.1.4 | Do not silently convert security requirements into arbitrary Owner settings. |
| PAYMENT_HOLD_DURATION | Minutes | Admin default; Owner may override | 5 minutes Admin default | Positive duration; Owner override remains within Admin-defined bounds. |
| RESERVATION_MIN_DURATION / MAX_DURATION | Minutes / hours | Owner proposed within system scope | N; no numeric default approved | Positive; max >= min; TESTER-DEF-08. |
| MAX_ADVANCE_BOOKING | Hours | Owner proposed | N; isolated demo proposal24 | Previous BR8h is not a confirmed default; team approval needed. |
| MAX_ACTIVE_BOOKINGS | Count | Admin proposed | N; demo2 | Positive integer; counted states pending TESTER-DEF-01. |
| BOOKING_ACTION_LIMIT / WINDOW | Count / duration | Admin proposed | N / N; demo6 per24h | Define counted events and interval boundaries before rate-limit tests. |
| RESERVATION_PROTECTION_WINDOW | Hours | Owner baseline | N; example4h is illustrative §3.4.4 | Nonnegative proposed; not the payment hold or allocation lead time. |
| ALLOCATION_LEAD_TIME | Minutes | Owner baseline | N; example30min §3.4.6 | Allocation time=start-lead; late-created behavior TESTER-DEF-13. |
| RESERVATION_MODE | Enum/set | Owner baseline | Specific Slot / Zone / Capacity | Reservation mode selection only; allocation and protection behavior are defined separately by the baseline. |
| DRIVER_REALLOCATION_REQUEST_LIMIT | Count | Admin default; Owner may override | 2 default §3.4.6 | Positive integer; Admin defines default/bounds and Owner may override within scope. |
| BACKUP_CAPACITY_POLICY | Structured demand/capacity rule | Admin default/bounds; Owner may override | Configurable; no numeric default | Fixed backup capacity and the dynamic protection-pool behavior are supported. Optional policy activation follows the Owner choice. |
| OCCUPANCY_VALIDITY_TIMESPAN | Hours | Admin default/bounds; Owner may override | 4 hours default | Current OCCUPIED projection remains occupied for this timespan unless observed occupancy changes earlier; used only for availability projection. |
| INTER_BOOKING_BUFFER | Minutes | Owner proposed | N; demo15 | Nonnegative; include in claim interval without double counting. |
| ARRIVAL_LATE_TOLERANCE | Minutes | Admin default/bounds; Owner may override/enable | N | Optional policy; when disabled, reservation remains valid until end time. When enabled, NO_SHOW occurs at start + tolerance without authoritative arrival. |
| VERIFIED_ARRIVAL_WAIT_LIMIT | Minutes | Owner proposed | N; demo15 withheld | Proposed Operator alert only; not automatic no-show/forfeiture for verified arrivals. |
| CANCELLATION_TIME_BANDS / REFUND_RATE | Minutes / percent | Owner baseline; schema proposed | N; demo >=30:100%, 15..<30:50%, <15:0% | Proposal only, not compulsory policy; boundaries exhaustive; percentage0..100. |
| NO_SHOW_REFUND_RATE / FACILITY_FAILURE_REFUND_RATE | Percent | Owner baseline | N; demo0% /100% | Different reasons, not one generic refund rate; no compulsory100% implied by examples. |
| ACCEPTED_REALLOCATION_REFUND_POLICY | Rule | Owner baseline | No refund solely for accepted reallocation | Baseline §3.4.6; all other charge/refund conditions remain independent. |
| PREPAYMENT_MODE / VALUE | Enum + money or percent | Owner proposed | N; demo30% withheld | Deposit/full/zero-payment treatment requires C-09; one clear unit per rule. |
| BILLING_BLOCK_DURATION | Minutes | Owner proposed; baseline ownership incomplete | 15 minutes baseline §3.5.1; imported60 withheld | C-08 determines configurable block and rounding; positive integer proposed. |
| PRICING_MODE | Enum | Owner proposed / platform baseline tier wording | Baseline overnight tiers; TIME_SLICING candidate | Approve one fee algorithm and its category coverage C-08; do not stack alternatives. |
| DAY_START / NIGHT_START | Local clock time | Owner proposed | 06:00 /22:00 demo only | Frames must cover each local day without overlap; timezone proposed Asia/Ho_Chi_Minh. |
| DAY_BLOCK_PRICE / NIGHT_BLOCK_PRICE | VND per block and vehicle type | Owner proposed | N; demo car10000/15000, motorcycle2000/3000 withheld | Activation depends on C-08/C-23; fixture values are not market prices. |
| OVERNIGHT_FEE / OVERNIGHT_TIME | Money / local time | Platform wording in §3.5.1 conflicts Owner | N; example50000 VND,22:00–06:00 | Illustration retained, not imported as TIME_SLICING seed; C-08. |
| MAX_DYNAMIC_PRICE_INCREASE | Percent of base | Owner/platform split pending | N §3.5.2 | MVP triggers and precise cap formula need TESTER-DEF-25. |
| FREE_PARKING_GRACE_PERIOD | Minutes | Owner proposed | N; demo15 | Candidate BR-PAY-06: N=0 off; exact boundary included; separate from lateness C-05. |
| OVERSTAY_PENALTY / EXPIRY_NOTICE_LEAD_TIME | Money/band / minutes | Owner proposed | N /N; demo5000 per block /15 | Rule structure, triggers and allowed adjustments need approval. |
| LOST_TICKET_FEE | VND | Owner proposed | N; demo20000 | Nonnegative, disclosed before charge; lost ticket does not prove identity. |
| EXIT_QUOTE_VALIDITY / EXIT_CLEARANCE_WINDOW | Minutes | Owner proposed | N /N; demo2 /5 withheld | 0<quote<clearance; BR-PAY-05 awaits C-21. |
| VEHICLE_DIMENSION_LIMIT | Metres for each dimension | Owner proposed | N; demo height2,width2,length5 | Supported category fixtures only; C-23. |
| LPR_CONFIDENCE_THRESHOLD | Percent or calibrated score | Admin proposed | >90% baseline; <90 manual | Exact90 and model score interpretation unresolved C-14; domain proposed0..100%. |
| CHATBOT_ESCALATION_CONFIDENCE / REPEAT_COUNT | Score / count | Admin proposed | 70% /3 baseline §6.7.2 | Calibration and exact equality need team decision; no fabricated LLM certainty. |
| REFUND_ESCALATION_THRESHOLD | Money | Authority pending | Unspecified §6.7.2 | Define currency, scope and comparison operator; all final refund decisions human. |
| PAYMENT_RETRY_LIMIT | Count of retries after the initial attempt | Admin default; Owner may override if exposed | 3 default §3.5.3 | Applies only to retryable payment failures. The initial attempt is not counted. With value 3, at most 4 total attempts are permitted. UNKNOWN outcomes require lookup/reconciliation rather than automatic retry. |
| OVERDUE_REMINDER_SCHEDULE | Days after obligation due | Admin default; Owner may override | N | Notification schedule is a business-policy variable; it does not create unpaid monthly debt by implication. |
| NOTIFICATION_DAYS | Days / notification offsets | Admin default; Owner may override | N | Defines the configured timing for applicable notifications, including overdue-payment reminders. Owner overrides remain within Admin-defined bounds; security-critical timings remain constrained separately. |
| REFUND_PROCESSING_TARGET | Business days | Owner/Admin split pending | 3–5 baseline §3.5.5 | N conversion and operational enforcement remain C-14. |
| IMAGE_RETENTION | Days and exception hold | Admin proposed within approved constraints | Maximum30 baseline §6.2.3 | Do not replace baseline ceiling/legal hold with unrestricted N; C-20. |
| AUDIT / OPERATIONAL / CHAT_RETENTION | Duration by data class | Admin proposed within approved constraints | 7y audit;90d hot/1y cold logs; chat unspecified | Separate categories and holds; C-20. No permanent default imported. |
| VIOLATION_PERSISTENCE / FRAME_COUNT | Seconds / count | Admin proposed; Future only | N /N; demo5 /3, disabled MVP | Only future per-slot detection, not LPR threshold. |
| SEARCH_RADIUS | Kilometres | Configuration owner pending | Default5 §3.3.2/3 | Expansion offered by baseline; configurable ownership/N conversion requires C-14. |

**Review restriction:** stale references to resolved C decisions remain visible (SPLIT-CF-17). Confirmed hierarchy/defaults do not approve an unfinished range, fee algorithm, seed or validation contract. EXIT_QUOTE_VALIDITY/EXIT_CLEARANCE_WINDOW stay inactive. OCCUPANCY_VALIDITY_TIMESPAN is projection only.

## 7. Detailed monthly rules

Monthly scope is MVP under C-19. Existing C status does not erase embedded proposed amount/rate language in M-05. Monthly holds and expiry are separate from ordinary reservations.

<a id="m-01"></a>

### M-01 — Rolling period and anchor

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Prepaid calendar-month cycle, not first-of-month billing. For a future purchase, start S is the chosen local date at00:00. For start-now, S is backend entitlement commitment time A. End E is the same local day/time next month, clamped to that month's last day if necessary. Preserve ORIGINAL anchor day/time across consecutive renewals: Jan31→Feb28(or29)→Mar31, not Mar28. Feb15→Mar15→Apr15. Rights cover [S,E); at E the old period is no longer valid. Do not treat one month as30days. Full-price calendar periods only, including a clamped February period; no partial-month sale in the initial extension. Store the contract timezone with the original day/time; later lot-timezone edits cannot shift purchased periods. If additional timezones with daylight-saving changes are supported, their ambiguous/missing-time rule must be defined first.

**Detailed interpretation / review checks:**

- 15 February→15 March→15 April. 31 January→28/29 February→31 March preserves original day/time.
- Future S is local 00:00; immediate S=A; rights cover [S,E), not a fixed 30-day period.

**Use-case coverage:** UC-MON-01, UC-MON-02, UC-MON-03

<a id="m-02"></a>

### M-02 — Payment deadline and late renewal

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Let order creation O, configurable hold N and deadline H. Future purchase/renewal: H=min(O+N,S); if no payable interval remains, do not create an activatable order. Start-now: H=O+N. Activation requires authoritative full-settlement validation and entitlement commit A<H; exactly H is too late. Renewal new S is current E. No auto-charge or next-period debt is assumed. Unpaid/late renewal expires; subsequent purchase after expiry uses a new start and anchor, not retroactive renewal. A renewal request opens at E−MONTHLY_RENEWAL_WINDOW. If multiple periods are already paid, append to the final committed E. No order means no free indefinite hold of the next period; an order protects only until H and only after resource validation.

**Detailed interpretation / review checks:**

- Future/renewal H=min(O+N,S); immediate H=O+N; require A<H, including verified payment and entitlement commitment.
- No renewal creates no next-month debt. Late repurchase starts a new period/anchor, never retroactively extends expired rights.

**Use-case coverage:** UC-MON-02, UC-MON-03

<a id="m-03"></a>

### M-03 — Money and state exceptions

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Separate order(PENDING_PAYMENT/FULFILLED/EXPIRED/CANCELLED), transaction(PENDING/SUCCESS/FAILED/UNKNOWN) and entitlement(SCHEDULED/ACTIVE/EXPIRED/CANCELLED). FULFILLED means payment AND rights were successfully committed. Refund states are REQUESTED/APPROVED/PROCESSING/COMPLETED/REJECTED; approval is not payout. A provider paidAt before H but backend commit at/after H enters refund/reconciliation; never restores released capacity automatically. Under/overpayment, currency/order/payee mismatch do not auto-grant. Failed transactions grant nothing; UNKNOWN needs lookup before retry. Duplicate callback is idempotent; a genuinely separate duplicate charge is recorded and refunded separately. Valid paid money with failed capacity/entitlement commit enters reconciliation/refund, not disappearance. Bank success screenshot is not settlement proof. A single provider transaction cannot fund multiple orders. A later failed callback cannot erase SUCCESS; chargeback/reversal is a separate reconciliation event. Entitlement validity is checked against timestamp and revocation at use, not dependent on a scheduler updating the label on time.

**Detailed interpretation / review checks:**

- Separate order, money, entitlement and refund states. FULFILLED requires committed money and rights.
- paidAt<H with commit>=H is late monthly fulfillment; reconcile/refund without reviving released inventory.

**Use-case coverage:** UC-MON-02, UC-MON-03

<a id="m-04"></a>

### M-04 — Capacity and occupied vehicle boundaries

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

When-space-available grants no reservation of capacity. Guaranteed-capacity/slot plans reserve only the approved resource/interval and exclude incompatible ordinary claims; do not count both guarantee and its occupied vehicle twice. Gate authorization rechecks vehicle, lot, period and actual compatible space; no admission into impossible occupancy. An occupied vehicle remains counted after pass expiry. Time before S/at-or-after E and separate fees are charged under approved ordinary tariff rules; covered intervals are not charged again. FREE_PARKING_GRACE_PERIOD is evaluated once for the physical session, never restarted at each monthly boundary. Continuous renewed coverage has no artificial uncovered gap. Physical departure, not entitlement expiry, releases occupancy.

**Detailed interpretation / review checks:**

- When-space-available holds no inventory; guaranteed plans protect approved resource/interval without double-counting its parked vehicle.
- Physical occupation continues at E; covered/uncovered intervals and extra fees are distinct. Session grace does not reset.

**Use-case coverage:** UC-GATE-01, UC-GATE-02, UC-MON-02, UC-MON-03, UC-MON-04

<a id="m-05"></a>

### M-05 — Cancellation and refund behavior

**Recorded status:** C under Appendix F / C-19; embedded proposed monetary clauses remain PROPOSED/OPEN, see SPLIT-CF-09.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Owner approval and confirmed payout remain separate. Before S: proposed refundable amount P×configured prestart rate. During [S,E): proposed refundable amount P×((E-C)/(E-S))×R for cancellation time C and unused-value refund rate R; actual timestamp duration is used, not a30-day assumption. At/after E unused amount is0. Proposed demo prestart100%, midperiod50% of unused value; facility failure100% of unused value. For multiple periods, compute unused amount for each paid period separately, apply its rate, sum, then round down once to whole VND for the entire refund request. Each interval/receipt may be refunded only once; days without visits do not increase unused value. Clamp cumulative refund to eligible collected money, record cancellation/entitlement release separately, never free occupied capacity. Future paid renewal not yet started is assessed as its own period. C is the backend-accepted cancellation time; service failure uses verified loss-of-service time and does not incur the voluntary-cancellation penalty. Past disruption is handled as a separate incident without rewriting history or double refund. Do not automatically offset a refund against additional parking fees or other debt; separate obligations remain visible.

**Detailed interpretation / review checks:**

- Owner monthly cancellation authority and separate confirmed payout are adopted by FR-MON-11.
- Embedded proposed amount/formula wording and isolated percentages remain PROPOSED pending SPLIT-CF-09; do not treat the confirmed row heading as approval of demo rates.
- Candidate formula uses actual remaining/total timestamp duration; sums each period then rounds once to whole VND. All limits/rounding/details require authority clarification before binding acceptance.

**Use-case coverage:** UC-MON-01, UC-MON-05

<a id="m-06"></a>

### M-06 — Plan changes and operational failure

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Purchased period retains accepted plan/version; price/type/vehicle changes, transfers, suspension and temporary closure compensation are not implicit capabilities. Define them before offering them. For guaranteed capacity unavailable, deny impossible entry and apply approved incident/refund process; a paid pass is not a command to displace a parked vehicle. Prior paid money remains visible during any failure. Ordinary exit must settle remaining eligible charges; emergency release follows approved emergency authority and does not erase debt.

**Detailed interpretation / review checks:**

- Accepted plan/version persists; a paid guarantee cannot evict existing occupation or create impossible capacity.
- Unspecified price/type/vehicle transfer/suspension/closure compensation is not an implicit capability.

**Use-case coverage:** UC-MON-01, UC-MON-05

<a id="m-07"></a>

### M-07 — Guarantee lifecycle and overlapping rights

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

When a guaranteed-plan vehicle leaves before E, its still-valid reserved resource returns to protected inventory, not ordinary availability. After E no expired guarantee is restored. Check the whole sold interval against reservation, protection, backup and monthly commitments; opening simultaneous orders must not double-hold the same vehicle's next period. A separate booking for the same vehicle/period cannot duplicate an already-matching guaranteed entitlement; direct the Driver to existing rights. Monthly passes do not automatically create bookings or provide an unapproved booking discount.

**Detailed interpretation / review checks:**

- Valid guarantee returns to protected inventory after departure before E; after E no expired guarantee is restored.
- Check full sold interval and do not double-hold overlapping vehicle rights or infer automatic ordinary bookings/discounts.

**Use-case coverage:** UC-MON-02, UC-MON-04

<a id="m-08"></a>

### M-08 — Repurchase while already parked and exit

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Validate the existing vehicle/session, plan eligibility and capacity before granting a new period while the vehicle is in the lot. Before new S is uncovered; after S is covered only if plan conditions hold. Do not create a new entry or deduct occupancy again, and do not silently force relocation. Use the ordinary actual-exit billing rule confirmed by C-21; a free covered exit right cannot stretch coverage past E. Keep prior legitimate charges and new entitlement allocation consistent.

**Detailed interpretation / review checks:**

- Repurchase while parked validates the existing session/resource without synthetic entry or double occupancy deduction.
- Ordinary actual-exit billing controls; legitimate prior uncovered charges are preserved.

**Use-case coverage:** UC-MON-03, UC-MON-04

<a id="m-09"></a>

### M-09 — Initial-extension limits

**Recorded status:** C under Appendix F / C-19; implementation contract details remain open where stated.

**Trigger / context:** Plan sale/renewal, entitlement use/expiry, cancellation or refund.

**Retained rule text:**

Plans bind account, vehicle, lot/zone and entitlement type. Vehicle/lot/type transfers and suspend-and-add-days are unsupported in the first future extension; use a disclosed cancellation/new purchase instead of directly editing an active contract. Preserve accepted period price and configured extra-use tariff; no automatic retroactive repricing. Operator cannot modify a period to give credit or postpone payment.

**Detailed interpretation / review checks:**

- Account/vehicle/lot/zone/type binding and unsupported transfers/suspend-and-add-days are retained.
- The phrase first future extension is stale editorial text; C-19 establishes monthly scope as MVP. Operator does not postpone monthly payment by modifying periods.

**Use-case coverage:** UC-MON-01, UC-MON-05

## 8. Monthly configuration and boundary scenarios — original wording

Owner manages monthly-plan settings within the same hierarchical policy model: Admin provides defaults/bounds and Owner may override within scope. Operator validates monthly rights and may record approved CASH_COLLECT payments; Operator does not set monthly prices or approve refunds. The monthly payment hold is a monthly-plan order hold and is separate from the ordinary reservation payment hold. Numeric examples in this table remain isolated Dev/QA fixtures, not production prices. The rolling anchor, prepayment and non-retroactive grant rules are baseline monthly-pass behavior, not arbitrary Owner switches.

| Parameter | Meaning / unit | Value | Isolated Dev/QA fixture |
| --- | --- | --- | --- |
| MONTHLY_PRICE | Price per complete calendar period and plan/vehicle | N VND/period | 1200000 VND for a demo automobile plan |
| MONTHLY_PAYMENT_HOLD_DURATION | Order payment deadline window | N minutes | 15; separate from Admin booking hold |
| MONTHLY_RENEWAL_WINDOW | Open renewal request before E | N days | 7 |
| MONTHLY_EXPIRY_NOTICE | Reminder offsets before E | List of N durations | 7 days and1 day |
| MONTHLY_MAX_PREPAID_TERMS | Consecutive prepaid period limit | N periods | 3 |
| MONTHLY_SALES_LIMIT | Concurrent sold-plan limit; does not replace resource validation | N plans | 10 in an isolated fixture |
| MONTHLY_CANCEL_REFUND_RATE | Rate applied to unused value | N percent per band | Before S100%; during period50% |
| MONTHLY_ALLOCATION_TYPE | Offered entitlement | When available / guaranteed capacity / guaranteed slot | Separate demo plans |


| Case | Expected result |
| --- | --- |
| Start15/02 at00:00 | First period [15/02,15/03); timely renewal [15/03,15/04), with no bill on01/03 solely because the calendar month changed. |
| Start31/01 in non-leap year | Ends28/02 at same local time; next timely renewed period ends31/03; original31 anchor retained. |
| Start31/01 in leap year | Ends29/02, then31/03 on timely renewal. |
| Start-now at15/02 14:20 | Ends15/03 14:20; do not round activation backward to00:00. |
| Payment at deadline H | No activation under proposed strict A<H; record/reconcile real received money. |
| Payment before H but delayed callback/commit after H | No retroactive entitlement; reconciliation/refund, with no automatic resource revival. |
| No renewal payment | Expires at E; no automatic next-month debt or penalty for not renewing; actual uncovered parking charges can still arise. |
| Late repurchase20/03 after expiry15/03 | New effective start and20 anchor after validation; do not charge an unused15–20/03 monthly period retroactively. |
| Pass expires while car remains parked | Retain occupancy; charge uncovered interval once; do not reset session grace or free guaranteed occupied slot as available. |
| Duplicate payment/event | One entitlement and one logical capacity commitment; extra real money separately reconciled/refunded. |
| Paid guaranteed plan but no capacity at commit | No impossible promise; record failure and refund/reconciliation, retaining money audit. |
| Midperiod cancellation | Use actual remaining/total timestamp fraction under M-05; approved refund and confirmed payout remain separate. |

**Review restriction:** the H equality is confirmed, although the original test says proposed. All numeric fixture prices/rates remain fixtures. Existing text about optional unsupported transfers is not new future scope.

## 9. Open questions and deferred items

See the 21 identified review items in the split review and original Appendix D/AI-DEP records. Preserve unresolved N/defaults, policy schema/ranges, full transition table, receipt/provider contracts, LPR equality, actual-exit settlement sequence, chat retention, final AI intents and physical adapter ownership. Offline replay and per-slot/model forecasting/anomaly/personalization features remain deferred.
