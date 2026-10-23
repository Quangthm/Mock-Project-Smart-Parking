# State, command and race contracts — v1.1

**Technical specification derived from confirmed product rules; implementation and tests pending.** Logical state names do not require a new top-level enum where an attribute/substate is sufficient. Operations serializes reservation/claim/visit/entitlement invariants locally; Payment serializes money attempts and refundable balances locally. No cross-database transaction or distributed exactly-once delivery is claimed.

## Common command envelope

Persist operation ID, actor/service identity, action/resource scope, payload fingerprint, expected aggregate version, accepted-at, correlation/causation IDs, applicable terms versions and outcome. Reauthorize callers before serving protected replay results. Same key/same payload returns recorded authorized outcome; changed payload rejects. Each local commit atomically writes facts, result and outbox. Consumer inbox deduplication and aggregate version checks precede effects. Timers use backend time; received-at and occurred-at remain distinct, with evidence-backed Operator adjudication for delayed arrivals. No silent browser-time backdating. Persist identity tombstones/operation outcomes for the supported replay horizon; the team must document retention aligned to financial/audit recovery, not assume a short cache TTL is enough.

## Partner, account, lot and notification states

These are separate logical dimensions, not one universal pending enum. User owns identity/contact, organization/membership and review evidence; Facility configuration/readiness, Operations accepted occupancy/claims/discovery/plan coverage, Payment financial terms and IoT raw observations remain authoritative.

| Command | Guards / transition | Durable effect and retry boundary |
|---|---|---|
| Register Partner | New permitted email → pending identity + initial application | Persist immediately; existing rejected initial email disallowed; replay returns same authorized application |
| Save first-lot setup | Scoped onboarding credential; required proposal/upload and simulation evidence | Save before issuing email OTP; no normal session; application ID alone insufficient |
| Verify initial email | Saved setup; purpose-bound valid OTP | Record verified email; review remains separate; phone not needed until phone OTP login |
| Complete simulation | Consented clearly labelled simulation; Driver/Owner registration or Operator email activation | Fake VNeID API returns GUID substitute; User & Access persists GUID associated with the correct user. No government-ID/auth-token meaning. Declined/failed remains incomplete; sole seeded Admin exemption only; remaining evidence/retention/wire contract Q02 |
| Replace initial documents | Authorized Admin; initial pending application only; expected version | Preserve all versions/actor/time/reason; reject other fields/additional-lot/finalized changes |
| Approve/reject initial application | Setup, simulation, verified email; expected application/document version | One immutable review decision; record reason/version; no third outcome or in-app re-acceptance |
| Provision approved first lot | Approved review → provisioning pending/succeeded/failed/unknown | Stable application/organization/site identities and recoverable intents; lookup unknown outcome before retry; never create same lot twice; failure is not rejection |
| Initial Owner login | Review approved AND provisioning succeeded; current access, contact proof and MFA | Necessary tokens only on completed login; safe meaningful failure otherwise; no restricted normal session workaround |
| Submit/review additional lot | Approved Owner; new proposal → approved/rejected | Independent review; rejection does not disable Owner/other lots; no pending Admin edits |
| Draft as new additional lot | Rejected immutable record; Owner authorized | New proposal identity, identical content allowed; request retry still deduplicated; old rejection never reopened |
| Open approved lot | Provisioning/activation complete plus Facility configuration and Operations readiness | Opening is explicit under existing scope; approval alone never opens parking |
| Deliver approval/rejection email | Committed review outcome → queued/pending/failed/delivered delivery | Retry delivery only; preserve business decision/identity; modified-doc approval has required notification; approval does not assert provisioning complete |
| Re-enable account | Authorized Admin and reason; administratively disabled non-Admin | Remove that restriction only; no approval/verification/role/grant/security/booking/session restoration |
| Sole Admin administrative mutation | Seeded Admin target; disable/lock/delete/role-change attempted | Reject atomically, including last-Admin authority loss; ordinary own security allowed and authentication lockouts still enforced |
| Driver verification | Correct contact proof and required simulation | Mark verified, never issue an automatic normal session |
| Operator activation | Email activation proof plus mandatory simulation | Activate eligible account; optional password; current assigned-lot grants required separately |

Simulation GUID return, user association and database persistence are approved by the 21 October V11-06 clarification. Credential TTL/recovery, API envelopes, remaining simulation evidence/retention and provisioning/readiness wire contracts remain Q01–Q04 technical design tasks; logical states here do not claim deployed columns.

## Spatial deactivation and vehicle removal

Facility validates explicit affected descendants and dependencies with Operations before planned deactivation. Same-lot batch authorization/version/dependency checks are all-or-nothing: if any item fails return per-item errors and no mutation; FE multiselect is required. No silent cascade, cross-lot batch or partial success. Preserve emergency closure truth and existing recovery handshake.

User soft-removes an own vehicle only after current operational/reservation/pass dependencies are handled and rechecked at commit. Preserve association history; removal does not authorize binding transfer. Cleanup/hard deletion follows retention and holds.

## Reservation / claim / offer

| Command | Actor; from → to | Guards and accepted timestamp | Atomic local effects / emitted outcome | Race / retry / recovery owner |
|---|---|---|---|---|
| Create reservation | Driver; absent → PENDING_PAYMENT, or CONFIRMED for genuine zero price | Current grants, no new-booking restriction, vehicle/time/accepted versions, optional lot cap, compatible full interval | Claim + reservation + financial/coverage basis + ReservationCreated/Confirmed | Operations commits one winner; losers get conflict; same operation no duplicate hold |
| Confirm ordinary payment | Payment outcome → Operations; pending or expired claim → CONFIRMED if feasible | Verified correct money; no accepted cancellation; one ordered decision checks active/expired claim | Convert hold once, or atomically reacquire compatible claim; ReservationConfirmed | After expiry cannot resurrect competing claim; failed reacquisition leaves money for Payment reconciliation/refund |
| Expire unpaid hold | Operations timer; PENDING_PAYMENT → EXPIRED | Deadline reached and no committed confirmation; version/current money workflow evaluated | Release unused hold; ReservationExpired | Payment callback may subsequently attempt approved reacquisition; no permanent rejection of all late money |
| Allocate | Operations schedule/assigned Operator; CONFIRMED → ALLOCATED | At allocation time or late-confirmed; compatible resource, stable priority | Allocation revision + claim update; ReservationAllocated | Same command for scheduler and immediate path; Operator genuine ties; no occupied displacement |
| Offer alternative | Operations; underlying entitlement retained; offer absent → OPEN | Conforming resource and current conflict resolution | Temporary alternative claim + offer expiry/version | Cannot hold incompatible duplicates; retain underlying entitlement history |
| Accept/expire alternative | Driver/Operations timer; OPEN → ACCEPTED or EXPIRED | Accept-at strictly < expiry; equality rejects; correct offer/version | Consume or release temporary claim once; AllocationChanged or OfferExpired | Only one winner; expired offer alone does not cancel entitlement |
| Cancel unused reservation | Driver; PENDING_PAYMENT/CONFIRMED/ALLOCATED → CANCELLED when eligible | Current non-parked lifecycle, accepted terms/version | Release unused claims; count voluntary event once; ReservationCancelled + refund intent | Serialize with admission/confirmation. If cancellation wins, later money is reconciliation/refund under accepted cancellation terms, never automatic new booking |
| Record gate arrival | Assigned Operator/verified Operations workflow; reservation state retained | Authoritative occurrence/evidence; at/before applicable cutoff timely | Arrival fact/history; ArrivalRecorded | Scheduler checks authoritative arrival; delayed evidence requires D06 correction, not arbitrary backdating |
| No-show | Operations; eligible confirmed/allocated → NO_SHOW | Enabled policy; decision strictly after cutoff; no timely evidence | Release unused rights/claims and reason; ReservationNoShow | Operator correction preserves mistaken state history and competing valid claims; compatible recovery or failure handling |
| End unused entitlement | Operations timer; unconsumed entitlement → EXPIRED | Reservation end reached; not PARKING; no later rights invented | Release unused claim with end reason | Disabled late tolerance never becomes start-time no-show; applicable money terms remain independent |
| Admit actual entry | Assigned Operator/verified workflow; ALLOCATED → PARKING | Identity, safe compatible space, current rights; actual entry evidence | Session + accepted occupation + linked claim conversion; VehicleEntered | Same entry/event cannot produce two sessions; claim and occupation counted once |
| Complete visit | Verified departure workflow; PARKING → COMPLETED | Actual departure accepted once | Release occupancy; VehicleDeparted/ReservationCompleted | Commercial checkout may already be settled or emergency money outstanding; gate-open/payment never substitutes for departure |
| Reject create | Operations; no accepted entitlement → REJECTED outcome | Invalid terms/scope/capacity | Audited rejection, no active claim | No financial effect; already-collected unexpected money is visible reconciliation work |

Booking restriction race: Operations commits create eligibility against authoritative cancellation restriction facts. A create accepted before a later restriction trigger retains its already-accepted right and ordinary payment/fulfillment path; the trigger blocks subsequent new creations. No blanket confiscation of in-flight paid rights.

## Physical state / checkout

| Command/event | Guard / from → to | Effect and recovery |
|---|---|---|
| Verified actual entry | Safe compatible AVAILABLE resource → occupied fact | Operations counts once; Facility maintenance and protection remain separate facets |
| Unreliable/conflicting observation | Observation version/source invalid, stale or conflicting → UNKNOWN verification status | Exclude automatic allocation; retain accepted occupancy until explicit verified correction/departure, no timeout release |
| Manual physical verification | Assigned Operator with reason/evidence and expected version | Establish authoritative observed state; record prior/new/source; not a UI toggle or automatic global binding edit |
| Maintenance/closure | Facility restriction changes service state | Occupied fact persists; block new incompatible claims/admission; run recovery |
| Accept checkout | Open visit → checkout pending settlement | Freeze authoritative billable cutoff; persist calculation/terms; emit CheckoutAccepted; do not free space |
| Financial settlement | Verified charge covered → commercially settled | No fake payment for zero balance; surplus obligation independent; emit CheckoutSettled |
| Unpaid invalidate/recover | Pending checkout, reason, no settled charge and no UNKNOWN possible success | Operations coordinates Payment status/version before invalidation; same session resumes without fresh grace; preserve attempts |
| Paid mispricing correction | Settled bill, system error, scoped Operator | Payment recomputes at original cutoff; difference-only adjustment; no visit reopen or delayed-time billing |
| Actual/emergency departure | Verified movement; scoped emergency permission if unsettled | Release physical occupation once, retain incident/debt; payment/gate-open alone has no release effect |

Payment/recovery interlock: Payment exposes a versioned settlement/recovery decision and refuses new collection against an invalidated checkout revision. Operations cannot invalidate from an old read. Persist a recovery intent; if collection/verification is pending/UNKNOWN, reconcile it first. A successful collection wins settlement; an acknowledged unpaid invalidation closes that checkout revision. Late unexpected external effects remain visible Payment reconciliation obligations, never disappear. This is a durable coordination contract, not an atomic cross-service read/write claim.

## Money and refunds

| Command | Actor; transition | Guard / local effect | Conflict or unknown outcome |
|---|---|---|---|
| Start collection | Authorized payer/cash Operator; absent → PENDING | Payment stores order/attempt/external idempotency identity before effect; verifies current checkout/order revision | Existing unresolved attempt prevents new provider/cash collection |
| Verify outcome | Trusted Payment adapter; PENDING/UNKNOWN → SUCCESS or FAILED | Verify transaction/order/amount/currency/payee; unique external identity; emit PaymentVerified/Failed | Timeout stays UNKNOWN; later failed callback never overwrites success; reversal is new reconciliation event |
| Retry failed collection | Payment; definitive retryable FAILED → new attempt | Up to3 additional attempts after initial; same logical obligation | Broker replay is not another attempt; exhausted retries remain visible, no automatic rights or money deletion |
| Request/approve refund | Authorized handler or automatic E01; REQUESTED → APPROVED/REJECTED | Payment validates attributable eligible money, policy/case and reserves refundable amount atomically | Concurrent refund/correction counts completed and unresolved reserved amounts; cannot allocate same money twice |
| Submit refund | Payment; APPROVED → PROCESSING | Persist provider operation before sending; original supported route or original-cash audited return | Network uncertainty stays PROCESSING/UNKNOWN and reserves balance; query same operation |
| Complete refund | Verified provider/cash payout; PROCESSING → COMPLETED | Link proof and adjustment; emit RefundCompleted | Approval/request is not payout. Definitive failure can release execution reservation for guarded retry, never timeout alone |
| Correct mispricing | Assigned Operator initiates; Payment adjustment | Compare corrected rounded charge with attributable net money and pending adjustments at original cutoff | Delta collection/refund only; preserve original successful records; concurrent corrections serialize |
| Excess prepayment | Payment automatic after charge known | Charge125000/collected300000 creates surplus obligation175000 once | Commercial checkout can complete covered; payout failure/UNKNOWN remains independently tracked, no cancellation penalty |

Refund ledger invariant: completed refunds + unresolved reserved refunds cannot exceed eligible attributable collected balance. Account separately for prior authorized adjustments; do not offset unrelated debt. Rounding is applied to nonnegative magnitude after summed eligible segments; cap after rounding. Provider-supported idempotency reduces duplicates but unexpected distinct charges/payouts must be recorded and reconciled. No claim of exactly-once transport.

## Monthly/yearly entitlement and dependent reservations

| Command | States / guards | Atomic effect and emitted outcome | Race / recovery |
|---|---|---|---|
| Create monthly/yearly order | Explicit enabled product and accepted policies; eligible vehicle/period; no duplicate sold rights; monthly max3 periods or yearly max1 period in advance (12 calendar months) | Payment order PENDING_PAYMENT, linked Operations proposed period; deadline H; no capacity claim | Operation replay does not shift anchor/deadline |
| Fulfill monthly/yearly | Verified full money and Operations commit before applicable accepted deadline; monthly A<H unchanged, yearly deadline design-pending | Entitlement SCHEDULED or ACTIVE with immutable [S,E)/anchor; MonthlyEntitlementCommitted, Payment marks FULFILLED recoverably | At H/later do not grant/backdate; money pending reconciliation/refund; callback paidAt alone insufficient |
| Activate/expire by time | Timestamp in [S,E) or ≥E; current revocation | Effective entitlement validity follows time even if scheduler label delayed | Expiry stops new coverage, not existing occupation; no next-period debt |
| Create coverage-dependent reservation | Current entitlement covers priced interval; expected entitlement version | Operations atomically stores coverage dependency with compatible ordinary claim/confirmation | Same Operations authority as cancellation guard; cancelled coverage forces new quote and explicit confirmation |
| Accept voluntary cancellation | Driver prerequisites, Owner handling; no affected future dependent reservation; entitlement/version current | Operations atomically closes removable coverage and records C + cancellation/refund intent | Recheck dependency set in same transaction. If dependent booking wins first, return list; if cancellation wins, reject stale coverage quote. Never auto-cancel/reprice/charge |
| Temporary closure compensation | Verified closed interval intersected with paid periods | Retain entitlement/end; union intervals and emit eligible compensation reference | Payment reserves value once; repeat/overlap with permanent closure/cancellation excluded |
| Permanent facility closure | Verified permanent end of service | Cancel remaining coverage and failure-refund intent; retain histories | A-01 voluntary prerequisites do not block emergency/failure handling; no automatic extension, no occupied release |

## Cases / evidence

Incident: assigned Operator accepts REPORTED → IN_REVIEW after taking responsibility; IN_REVIEW → RESOLVED requires recorded finding/action/recovery outcome; RESOLVED → CLOSED records closure. Violation: scoped human moves SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED after sufficient evidence and reason; no implicit penalty. Appeal: Driver SUBMITTED → handler IN_REVIEW → ACCEPTED/REJECTED with reason; accepted adjustment is a separate Payment request. Owner handles escalation. General Support: authorized routed handler takes OPEN → IN_PROGRESS, may wait for customer, returns WAITING_FOR_CUSTOMER → IN_PROGRESS on response, resolves with disposition and closes after record completion. Waiting is optional; no automatic closure on silence or new appeal deadline. A correction/reopen preserves prior decision history and linked financial workflow rather than overwriting it.

Evidence: owner stores PENDING_UPLOAD → ATTACHED after provider+metadata success. Upload success/DB failure is reconciled by stable asset identity; do not show attached early. At retention due with no holds, transition DELETE_PENDING → DELETED only with evaluated provider proof; timeout/failure remains recoverable. Hold acknowledgement and delete eligibility serialize locally: do not acknowledge preservation after irreversible deletion. If hold wins, deletion waits; if irreversible delete already won, report missing evidence and case pending/escalation. Releasing one hold never releases others. Approved emergency may proceed with audited missing-evidence incident.

## Recovery ownership and transport

Operations coordinates reservation, admission and monthly fulfillment/dependencies; Payment owns collection/refund reconciliation; Facility initiates planned/emergency structure changes and requires Operations acknowledgement for planned finalization. Each asset owner reconciles media. Notification/Audit retries projections and delivery without undoing source facts. Use authenticated HTTP/OpenAPI for immediate contracts and RabbitMQ/MassTransit for durable outcomes. Retry backoff/queue technology are implementation choices; exhaustion leaves durable pending work and authorized escalation. Never use retry TTL to cancel rights, lose money or clear occupied capacity.


## SEC-01/SEC-02 identity state machines

[Canonical authentication/recovery contract](authentication-recovery.md) adds disabled/pending/enabled factor states, restricted login/recovery challenges, backup-code consumption, Admin-reset REPLACEMENT_REQUIRED and password-reset grants. User & Access owns atomic security-version guards; Notification & Audit projects ticket outcomes. The linked race table is normative for authentication; it does not change the parking/money transitions above.


Design/implementation owners and exit criteria: [V11-DES-01–12](../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
