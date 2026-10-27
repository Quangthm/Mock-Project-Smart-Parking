> **RC2 scope update:** any TOTP-FUTURE wording inside reproduced older source tables is superseded by SEC-01/SEC-02. Other deferrals are unchanged.

# Deferred and implementation register — v1.1 review candidate

**Status separation:** IMPLEMENTATION PENDING = confirmed MVP behavior needs code/schema/tests. POST-v1.0 DETAIL = confirmed AI/LPR direction retained, detailed integration/evaluation intentionally later. FUTURE = feature outside selected MVP. Historical alternatives in the source tables are not active requirements.

## Current disposition by original follow-up ID

| ID | Current disposition | Main destination |
|---|---|---|
| TESTER-DEF-01 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-02 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-03 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-04 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-05 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-06 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-07 | CONFIRMED CONTRACT; implementation/evidence pending | policy catalogue |
| TESTER-DEF-08 | CONFIRMED CONTRACT; implementation/evidence pending | policy catalogue |
| TESTER-DEF-09 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-10 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-11 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-12 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-13 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-14 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-15 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-16 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-17 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-18 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-19 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-20 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-21 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-22 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-23 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-24 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-25 | CONFIRMED CONTRACT; implementation/evidence pending | pricing module |
| TESTER-DEF-26 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-27 | CONFIRMED CONTRACT; implementation/evidence pending | local acceptance |
| TESTER-DEF-28 | CONFIRMED CONTRACT; implementation/evidence pending | local acceptance |
| TESTER-DEF-29 | CONFIRMED CONTRACT; implementation/evidence pending | service boundaries |
| TESTER-DEF-30 | CONFIRMED CONTRACT; implementation/evidence pending | service boundaries |
| TESTER-DEF-31 | CONFIRMED CONTRACT; implementation/evidence pending | quality module |
| TESTER-DEF-32 | POST-v1.0 DETAIL; manual fallback current | AI module |
| TESTER-DEF-33 | IMPLEMENTATION PENDING; fixture hardware only | state-transitions / acceptance |
| TESTER-DEF-34 | CONFIRMED CONTRACT; implementation/evidence pending | state-transitions / acceptance |
| TESTER-DEF-35 | IMPLEMENTATION PENDING; fixture hardware only | state-transitions / acceptance |
| ARCH-DEF-01 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-02 | Boundary/principle confirmed; schema/API/recovery implementation pending; detailed AI/LPR contracts POST-v1.0 | ownership-and-events / state-transitions |
| ARCH-DEF-03 | Boundary/principle confirmed; schema/API/recovery implementation pending; detailed AI/LPR contracts POST-v1.0 | ownership-and-events / state-transitions |
| ARCH-DEF-04 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-05 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-06 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-07 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-08 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| ARCH-DEF-09 | Boundary/principle confirmed; schema/API/recovery implementation pending | ownership-and-events / state-transitions |
| AI-DEP-01 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-02 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-03 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-04 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-05 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-06 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-07 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-08 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-09 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |
| AI-DEP-10 | General direction retained; detailed integration/evaluation POST-v1.0 | AI module and retained detail |

## v1.1 design and implementation actions

All V11-01–12 and D01–D12 business choices in the [approved register](2026-10-20-approved-v1.1-changes.md) are settled. These technical actions are **DESIGN/IMPLEMENTATION PENDING**, not permission to change those choices. Role owners below are accountable workstreams; PM-02 must assign named people, sprint and dates. No implementation commit or runtime evidence is claimed by SPARK-296.

| Work ID | Owner/action | Completion evidence |
|---|---|---|
| V11-DES-01 | User + media/backend security: agree agreement upload/download contracts, allowed formats/limits, protected references, retention classification, version binding and concurrency controls | Scoped initial-application replacement only; original/version/actor/time/reason retained; no arbitrary URL bypass; approved/rejected records immutable |
| V11-DES-02 | User + FE/security: specify the fake VNeID API returned GUID substitute and its persisted User binding, other mock fields, explicit consent, failure/completion evidence and retention | Every non-exempt role covered; Operator email activation; sole seeded Admin exemption; no real government assurance |
| V11-DES-03 | User/security + FE: agree scoped setup/resend/verification credential expiry, storage, renewal, secure recovery and safe login error envelope | Application ID alone cannot authorize; no normal Owner login before review and successful provisioning; no token/profile overexposure |
| V11-DES-04 | User/Facility/Operations/Payment architects: specify durable provisioning keys, outcome lookup, event versions, retries and readiness mapping | One tenant/site per accepted proposal; pending/failed/unknown outcome visible; approval and notification cannot imply opening |
| V11-DES-05 | Operations + Payment + QA: specify separate yearly policy keys, defaults/bounds, financial examples and calendar boundaries | Twelve calendar months; at most one yearly period ahead; unchanged monthly maximum three prepaid periods; no invented yearly refund rate/renewal default |
| V11-DES-06 | User + QA: consistent email normalization and immutable initial rejection enforcement | No repeated initial Partner registration using rejected email; re-enable cannot reverse rejection; additional-lot Draft as new unaffected |
| V11-DES-07 | User + Notification/Audit: agree revision-bound review/notification payload and delivery reconciliation | Approval with modifications message; delivery retries retain committed result and do not repeat provisioning |
| V11-DES-08 | Operations discovery + Facility + Payment + FE: settle request parameters, source/freshness and interval estimate contracts | MapTiler geographical discovery, manual/current origin, filters, current tariffs vs interval totals, external directions; unsourced ETA deferred |
| V11-DES-09 | Facility + Operations + FE: implement same-lot batch deactivation validation/version guards | Explicit descendants/dependencies, all-or-nothing mutation and per-item errors; FE multi-select |
| V11-DES-10 | User + Operations + retention owners: coordinate vehicle soft removal and active-dependency checks | Retained binding/visit history; removal is not transfer approval; retention controls cleanup |
| V11-DES-11 | User/security + deployment maintainer: enforce sole seeded Admin protections and fixed role assignments | No Admin registration/role-management endpoint, self administrative lock/disable/delete denied, auth lockouts preserved; controlled maintainer recovery |
| V11-DES-12 | PM + database teammate + service owners: plan development reset **only after docs review approval and schema/seed/application alignment** | Target environment/database names, responsible named teammate, reviewed reset/seed script paths/versions and execution date all **TBD before execution**; no reset authorized/executed by this task |

Track implementation commit/build and QA evidence against the [decision matrix](../../01-requirements/requirements-traceability/v1.1-decision-coverage.md). Jira is unchanged. The selected development transition replaces migration/grandfathering of invalid development applications; secure runtime credential recovery remains required independently.

## Future exclusions explicitly preserved

- Additional provider-hosted card channels remain FUTURE. TOTP enrollment/challenge/backup recovery and Admin-assisted account recovery are promoted to current MVP by SEC-01/SEC-02; implementation and tests remain pending.
- Real sensors/cameras/barriers/readers and MQTT adapters; full offline cached authorization and durable offline replay.
- Product simulator UI (isolated QA fixtures remain current).
- Guaranteed monthly capacity/slot plans, sales caps/queues/slot activation, transfers/suspension/automatic extensions or renewal charging.
- EV charging and automatic mixed-category area conversion; exact vehicle-size automation unless separately approved.
- Event/holiday/demand pricing, personalized/model ranking, per-slot multi-frame AI, forecasting and anomaly detection.
- Production HA/clusters/24-hour on-call SLOs; Kafka; optional Redis scale-out is not mandatory local acceptance.
- R2 migration/dual-provider writes, dedicated Search/AI extraction and advanced knowledge CMS.
- Revenue payment-method breakdown, retention/geographic/user-pattern analytics.
- Exit quote/clearance/requote is an inactive superseded proposal, not a scheduled future commitment.

## Original review registers — retained for exact lineage

The following source rows include historical alternatives and recommendations. Use the current disposition above and latest confirmed contracts; an old OPEN does not demand a new PM decision and a recommendation is not implemented evidence.

## 6. Every tester deferred item

“Preserve closure” below means the underlying business choice is already confirmed; implementation details can still require acceptance criteria. This review does not reopen it or mark unapproved proposals closed.

| TESTER-DEF | Recommended resolution | Alternative: benefit / cost | Impact and scope risk |
|---|---|---|---|
| 01 Lifecycle | Retain baseline states including NO_SHOW; publish transition table with actor, guard, side effects and race result. Refund/reallocation are separate lifecycles. C-09 late success needs an explicit conditional recovery path. | Add a state for every failure: more visibility but state explosion. | **Critical:** Payment, arrival, cancellation, capacity. Do not confuse EXPIRED with NO_SHOW. |
| 02 Allocation | Persist reservation/session-owned allocation revisions; preserve requested preference and history. | Derive assignments on read: fewer rows but poor audit/reallocation reproducibility. | **High:** schema, maps, conflict evidence. Current allocation alone cannot explain historical fulfillment. |
| 03 Backup assignment | Represent resource coverage and protection in the same compatible claim ledger; identify which units cover which commitments. | Global scalar reserve: simpler but cannot prove compatibility or prevent double consumption. | **Critical:** all ordinary booking modes/visits. Available-only monthly rights add no reserved unit; occupation counts once. |
| 04 Reallocation | Separate Driver request from system action; default two Driver requests within configured bounds. **CONFIRMED:** count accepted submitted requests, not transport retries/invalid payloads; record rejections and outcome. | Count only successful moves: friendlier but unbounded requests; count every click: simpler but penalizes retries. | **High:** UI, policy, audit. System recovery must not consume Driver allowance. |
| 05 Physical transitions | Define available/occupied/unknown/maintenance/unavailable transitions; preserve occupancy as an independent fact when maintenance/closure occurs. | One exclusive status field: simpler display but can hide an occupied vehicle. | **High:** Parking/session/schema. Unknown cannot expire automatically into available. |
| 06 Lost capacity | Block new claims/admission, retain affected entitlement, attempt compatible reallocation, then explicit unfulfillable/refund path. | Immediately cancel all affected claims: simpler but contradicts preservation and harms users. | **Critical:** accepted rights survive a slot becoming unavailable until resolved. |
| 07 Policy hierarchy | Null/unset inherits; zero is an explicit value only where allowed; optional enable flag; lower scopes cannot exceed bounds; booking context resolves/snapshots policy. | Flat site settings: easier but drops hierarchy. | **High:** all domains. Booking context is not a Driver privilege to override policy. |
| 08 Policy catalogue | One catalogue of key/type/unit/owner/scope/default/bounds/override/effective version. Preserve confirmed defaults and mark all others proposed. | Scatter defaults in SQL/UI: fast but causes drift. | **High:** configuration/tests. Missing required policy should fail validation rather than acquire a hidden default. |
| 09 Refund policy | **CONFIRMED:** Owner-configurable versioned rates by reason/lot; Operator ordinary appeals, Owner escalations/monthly cancellation; no monetary approval threshold in MVP. | Rate defaults and time bands confirmed in §16.2; remaining override bounds must be specified. No new penalty authority. | **Critical:** accepted terms preserved; approval is not payout; pricing errors use correction workflow. |
| 10 Conflict policy | Fixed tested resolution order with configurable permitted thresholds/options; explicit Operator escalation. | Owner-authored scripts/workflows: flexible but much larger validation/security scope. | **High:** allocation/refunds. Configuration must not permit impossible admission. |
| 11 Backup configuration | Separate fixed backup inventory from dynamic protection coverage, with explicit activation/release and conversion to occupancy. | One shared emergency/backup counter: simpler but obscures rights and double accounting. | **Critical:** capacity/visits; available-only monthly rights add no guarantee; emergency priority is not additional physical capacity. |
| 12 Late payment | Preserve C-09 closure: reattempt atomic ordinary claim if free, otherwise reconcile/refund losing paid attempt. | Always reject late money would simplify state but contradict confirmed behavior. | **Critical:** monthly strict A<H is deliberately different. |
| 13 Late-created booking | On confirmation after allocation time, run allocation immediately and idempotently; scheduler uses same command. | Wait for next scheduler pass: simpler but temporary undefined fulfillment. | **High:** payment/allocation timing; no allocation before entitlement qualifies. |
| 14 Pending/protection | Preserve C-04/C-06: active payment hold consumes availability; confirmed protection is separate. Convert claim category atomically. | Deduct both simultaneously: conservative but wrong double deduction. | **Critical:** publish exact boundary-time cases and counter definitions. |
| 15 Simultaneous arrival | Preserve Operator tie-break under C-18; capture candidates, actor, reason and selected outcome. | Automatic ID/timestamp tie-break: deterministic but changes confirmed authority. | **High:** gate/audit; arrival and payment ranking are not interchangeable. |
| 16 Refused alternative | Allow refusal. **CONFIRMED:** compatible in-scope alternative refusal follows published voluntary-cancellation policy; no feasible conforming alternative follows facility-failure policy. | Always full refund: simpler/customer-friendly but raises cost. No refusal: simpler but poor user control. | **Critical:** mandatory vehicle/accessibility/promised-facility compatibility is confirmed; apply the confirmed five-minute default and avoid punishing objectively unusable offers. Offer expiry must not hold two resources indefinitely. |
| 17 Multiple affected | Queue all affected arrived claimants and feasible resources; apply established priority and Operator ties without displacing occupied/unaffected users. | Pairwise fixes: quick but unstable/fairness problems with three or more. | **High:** allocation/UI; define priority recomputation after each resolution. |
| 18 Guest/walk-in | **CONFIRMED:** account-free walk-in retained; nighttime rates depend on category/location/time, not account status. Active monthly coverage includes ordinary day/night parking. | Guest-only overnight surcharge is superseded. | **Medium:** separate identity, visit type and coverage; no charging/service fees included in monthly MVP. |
| 19 Terminology | One glossary: reservation entitlement, claim, allocation, physical arrival, session, occupancy, payment and fulfillment. | Per-service synonyms: locally easy, integration ambiguity. | **Medium:** all contracts; a paid reservation is not occupied capacity. |
| 20 Arrival event | Authorized Operator records verified gate arrival with event ID, source, actor, occurred/received times and evidence. Arrival at or before cutoff is confirmed timely; no-show only after cutoff without authoritative timely arrival. | Client GPS arrival: convenient but insufficient operational proof. | **High:** NO_SHOW/conflict races. Late reported events need controlled adjudication, not arbitrary backdating. |
| 21 Operational issues | **CONFIRMED:** REPORTED → IN_REVIEW → RESOLVED → CLOSED; scoped assignee, evidence and action history. | Full helpdesk remains unnecessary; transition guards still require specification. | **Medium:** EVIDENCE_PENDING and escalation are attributes, not proof of resolution; see §14.7. |
| 22 Violations | **CONFIRMED:** SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED; human review and limited consequences in §14.7. | No automatic fine, ban, towing or government reporting inferred from confirmation. | **High:** applicable published fees and corrections remain separate financial decisions. |
| 23 Appeals/support | **CONFIRMED:** appeal SUBMITTED → IN_REVIEW → ACCEPTED/REJECTED; support OPEN → IN_PROGRESS → WAITING_FOR_CUSTOMER → RESOLVED → CLOSED. | Escalation separately recorded; refund lifecycle remains independent. | **High:** ordinary appeals Operator; escalations/monthly cancellation Owner; plate binding disputes Admin. No monetary approval threshold. |
| 24 Emergency | Permission-gated audited release/action, affected-resource marking and recovery. Record actual departure separately and retain unsettled money. | Automatic forced relocation/admission: operationally impossible in some cases. | **Critical:** emergency does not erase debt or occupied capacity. |
| 25 Pricing | Preserve Owner time-block MVP and confirmed B02 ownership; apply confirmed B03/B04 and publish executable examples for midnight, free boundary, nearest-1,000-VND half-up rounding, monthly overlap and the restored per-session free rule. | Event/holiday/demand engine: extensible but explicitly future. | **Critical:** C-08 closes feature scope, not all arithmetic. |
| 26 Recommendations | Preserve deterministic backend ranking plus explanation and all options accessible; add tie/fallback fixtures. | Personalized ML: potentially useful later, needs data/consent and changes C-17. | **Medium:** close business scope; keep model-based future. |
| 27 Performance | **CORE TARGETS CONFIRMED:** 20 concurrent users, 50-user short burst, core read/write p95 ≤500 ms excluding provider work. | Approved recipe: §16.7; actual machine and endpoint/data mix still need to be recorded. Ten-minute steady run and one-minute burst are confirmed. | **High:** targets not measured; define endpoint/error mix and separate upload/AI/payment budgets. |
| 28 Reliability | **CONFIRMED:** restart recovery ≤5 min and tested backup/restore. Recommended: preserve acknowledged local transactions/recovery intents across ordinary restarts. | Daily backup, restore ≤30 min and backup RPO ≤24 h are now CONFIRMED (§16.7). | **High:** backup loss tolerance differs from crash durability; restore objectives must not disguise ordinary-restart loss. |
| 29 Scale | One instance per necessary service; one local PostgreSQL server may host separate DBs/credentials; document later horizontal scaling. | Cluster now: more realistic operations, higher laptop/team cost. | **Medium:** multiple DBs on one server still require API boundaries and ownership. |
| 30 Infrastructure | **CONFIRMED:** RabbitMQ + MassTransit; MQTT device scope; Kafka deferred; Redis optional. SignalR is confirmed for Operator occupancy/gate/slot, 3D, Admin user-management/approval and notification-bell views. | Polling is the live-view alternative; broker selection is settled. | **High:** none is the authority for capacity; clients recover current state after missed updates. |
| 31 Operations | Health/readiness, correlation IDs, redacted structured logs, money/capacity/audit events and recoverable failed work. Production on-call/SLOs labeled separately. | Full 24/7 operations: costly and not meaningful for local demo. | **High:** C-20 retention still applies to design; synthetic time tests can verify expiry logic without claiming years of operation. |
| 32 AI/LPR targets | **Detailed evaluation deferred after v1.0 (§16.8).** Later evaluate a named model/version on permitted labeled representative data. Historical unapproved threshold proposal: >90 eligible for automated matching, ≤90/manual or invalid result requires review; high-confidence mismatch still stops automatic passage. | ≥90 changes equality; fully manual fallback reduces reliance but does not demonstrate model accuracy. | **High:** scores are not calibrated probabilities by definition. Existing 98%/95% targets cannot silently become achieved or be lowered. Record revised gates if approved. |
| 33 IoT scope | Preserve C-15 fixture-only hardware integration; keep scoped device records/permissions needed by FR-LOT-05. | Real camera/sensor integration: new hardware/security workload. | **Medium:** virtual inventory is not proof of live telemetry. |
| 34 Observation conflict | **CONFIRMED:** uncertain/unsuccessful LPR needs manual review; conflicting observations cannot automatically free occupied capacity. Assigned Operator correction retains reason/history. | Core observation ordering/staleness need specification; detailed LPR confidence equality/integration is deferred after v1.0. | **High:** recognition correction does not transfer global vehicle binding. |
| 35 Gate | Define separate simulated authorize/open/entry/departure events and idempotent operational commands; no real adapter promised. | Real gate adapter: greater realism, additional physical safety and reliability scope. | **High:** LPR result or SYNCED_TO_PARKING is not proof of entry/departure. |

For the confirmed core local targets and remaining proposed test details, record hardware and reserved resources, service build, database size, image sizes, workload mix, warm-up, percentile calculation and external dependency mode. Real-provider, sandbox-provider and deterministic-fixture results must be labeled separately.

## 7. Every architecture deferred item

| ARCH-DEF | Recommendation | Alternative: pros / cons | Impact and future conflict |
|---|---|---|---|
| 01 Boundaries | **CONFIRMED — SB-01:** six deployable services in §1.1, with domain-owned data/policies and internal capability modules. | Historical unselected alternatives: session ownership in old Parking/Facility; one service per operational capability. | **Critical:** ownership resolved; API/schema/repository migration remains OPEN. |
| 02 Chatbot | **CONFIRMED:** Assistant module and conversations in IoT & Recognition; no separate mandatory deployment. | Dedicated AI deployment is future extraction only. | **Medium:** tool categories, Gemini/OpenAI provider direction and Admin-only knowledge publication are confirmed; exact schemas/models/integration/evaluation are deferred after v1.0 (§16.8). |
| 03 IoT/LPR | **CONFIRMED — SB-01:** one IoT & Recognition deployment with separate device/observation and recognition/evidence modules. | Separate deployments retained as an unselected/future alternative if justified. | **High:** observations never grant gate/admission/payment authority. Device-free image direction is retained; detailed input/provider/integration/evaluation contracts are deferred after v1.0; real IoT remains deferred. |
| 04 Search | **CONFIRMED:** Search/discovery module in Operations, consuming Facility and Payment contracts/projections as needed. | Independent Search deployment is future extraction only. | **Medium:** ranking fixtures, freshness and query contracts remain design work. |
| 05 Availability | **CONFIRMED — SB-01/B01:** Parking Operations owns authoritative bookable/admission availability and all competing operational claims/visits. | Facility-based authority and separate Capacity deployment were not selected. | **Critical:** query freshness, observation reconciliation and capacity-change implementation remain OPEN; protocol direction is confirmed; all consumers use the same authority. |
| 06 Pricing | **CONFIRMED — SB-01/B02:** Payment & Pricing owns tariff/pricing/financial rules; all unrelated policies stay with their business domains. | Facility tariff authority, separate Pricing deployment and centralized global Policy service were not selected. | **High:** B03/B04 and immutable accepted-version principles are confirmed; remaining policy bounds and detailed contracts need specification; §16.1–2 settles the stated defaults. |
| 07 Auth | **OWNERSHIP CONFIRMED:** User & Access owns identity/authentication/current grants; consuming services enforce applicable resource access. | Gateway-only authorization remains unsuitable; replicated grants or live lookup are still implementation alternatives. | **Critical:** gateway/service protocol and session/revocation implementation remain to specify. B08 token parameters and all-role email/phone OTP-login scope are confirmed in §16.5; TOTP is FUTURE; current protected-action checks are confirmed. |
| 08 Events | **PRINCIPLES CONFIRMED:** owner publishes outcomes; origin durably captures important audit facts, Notification & Audit projects them asynchronously; notification failure does not undo business state. | Fire-and-forget and synchronous central audit prerequisite were not selected. RabbitMQ + MassTransit is CONFIRMED; MQTT device scope and Kafka deferral retained. | **High:** event schemas, publisher/consumer matrix, delivery attempts and deduplication/replay remain OPEN. |
| 09 Consistency | **PRINCIPLES CONFIRMED:** local Operations transactional invariants; HTTP/OpenAPI immediate requests and durable outcomes; one coordinator per workflow, no synchronous cycles. | Distributed transactions or eventual-only competing capacity decisions were not selected by this handoff. | **Critical:** individual coordinators, timeouts, pending/unknown/compensation and reconciliation contracts remain OPEN. |

Architecture alignment: SB-01 supersedes old SA-21 session placement and selects Payment-owned tariffs. The ownership guide confirms Search in Operations, Assistant/conversations in IoT & Recognition, General Support in Notification & Audit, Operations occupancy/credentials and domain-owned reporting. Monthly remains split by rights versus money, now available-only. RabbitMQ + MassTransit and core local targets are confirmed. SA-20 remains editorial: three retries after initial attempt means four total; UNKNOWN requires reconciliation. Unselected detailed policies and provider/model choices remain open.

## 8. Every AI dependency / unconfirmed AI implementation choice

**12 October disposition:** retain confirmed general directions; detailed tool/provider/LPR contracts, integration and evaluation below are post-v1.0 work under §16.8. They are not blockers to publishing the requirements baseline. No historical score/accuracy proposal is newly approved.

| AI-DEP | Proposed decision | Alternative and benefit / cost | Impact, affected parties and remaining gate |
|---|---|---|---|
| 01 Intent scope | **CONFIRMED:** FAQ, search, authorized reads and reservation preparation; normal UI confirms domain commands. Cancellation/refunds/security changes open ordinary screens. | Broader autonomous conversational writes not selected. | **High:** exact endpoint allowlist and negative tests remain; ordinary application capabilities are not removed. |
| 02 API/security | Versioned tool schemas over domain APIs, user identity propagation, resource authorization, bounded responses, timeouts, idempotency and result lookup. | Direct database tools: faster prototype but breaks boundaries and authorization. | **Critical:** cross-account negative tests; model output is never a permission grant. |
| 03 Discovery | Availability from B01 authority, price/terms from B02, freshness/version/requested-versus-allocated distinctions. | Cached static facts: cheap but misleading for booking. | **High:** stale output may be displayed with warning; mutation always revalidates. |
| 04 Confirmed writes | Bind explicit UI confirmation to exact terms and command; changed terms require reconfirmation; normal payment checkout follows. | Free-text “yes” without bound terms: easy UX but ambiguous authorization. | **Critical:** duplicate, unknown, expired and stale-form scenarios; no LLM-side rule engine. |
| 05 Knowledge | **CONFIRMED:** Admin-only publication authority; Admin/dev team maintains simple versioned approved configuration. Live values from APIs; exclude unresolved proposals. | Advanced publication lifecycle and Owner content-submission workflow outside MVP. | **High:** no new Developer business role; content version/scope/publication evidence required, not a CMS. |
| 06 Provider/data | **CONFIRMED DIRECTION:** Gemini and OpenAI; provider adapters and bounded execution. Detailed chatbot document may follow v1.0. | Exact models, routing/fallback, data sharing and budgets are explicitly post-v1.0 design/acceptance work (§16.8). | **High:** provider direction does not authorize unrestricted personal-data transfer or prove integration complete. |
| 07 Human support | **CONFIRMED:** asynchronous support tickets in N; lot issues to assigned Operator, account/platform and global plate disputes to Admin. | Live staffed chat not selected; support hours/response targets remain to declare. | **Medium:** acknowledgement is not resolution; transfer only authorized context. |
| 08 LPR | Device-free image contract, raw/canonical plate, result category, confidence scale/model version, audited correction and separate passage events. | Device-dependent recognition contract: convenient for future telemetry, blocks device-free image-upload MVP even within the approved combined host. | **High:** recognizer/dataset and threshold equality are deferred after v1.0; neither fixtures nor confidence alone prove recognition accuracy. |
| 09 Evaluation | Named Vietnamese/English task suite, authorization/prompt-injection/error fixtures, latency/cost budget and separately measured LPR evaluation. Use deterministic replay for demo regression; label production shadow/A-B practices separately. | Keep all original AI gates locally: stronger evidence, potentially larger experiment/data burden. | **High:** QA/team approval required for changed thresholds; safe fallback must remain available. |
| 10 Future AI | Retain personalized ranking, camera occupancy, forecasting and anomaly detection as future with separate data/consent/response plans. | Implement now: richer demo, conflicts with approved scope and available evidence. | **Medium:** not prerequisite for FAQ, deterministic search or confirmed reservation assistance. |


## Additional original exclusions retained

Native mobile app; autonomous-vehicle integration; loyalty points; national digital-map integration; full complaint/ticketing suite beyond the approved minimal asynchronous cases. Source promotional notification category is retained without introducing a promotions engine.

## RC3 / PAY-01 implementation disposition — approved 13 October 2026

| Work | Disposition | Completion evidence |
|---|---|---|
| payOS QR adapter | Current MVP, first implementation priority | Controlled low-value production collection/reconciliation; signed webhook and query behavior; actual build/evidence. NOT RUN. |
| Native VNPay | Retained separate planned option; may be simulated/deferred if resources are insufficient | Mode explicitly declared. No separate sandbox adapter is required by PAY-01. |
| Native MoMo / ZaloPay | Retained separate planned options; existing deterministic fixtures remain | Native integrations unfinished unless supported by actual evidence. payOS scanning does not close native integration work. |
| Payer-app compatibility | Verify before advertising any app as supported | Exact app/version/flow and successful payOS transaction evidence; not a native-provider completion claim. |
| Refund execution | Current financial requirements retained; provider capability validation pending | Original-route support, eligible recipient correlation, actual payout proof and unknown-outcome reconciliation. Separate payout service is not automatically accepted as equivalent. No new manual online-refund workflow approved. |
| Technical configuration | Implementation/design review pending | Merchant/payee mapping, credentials, provider limits, test budget/amounts, timeouts/reconciliation scheduling, SDK/API version and callback hosting. No invented numeric business defaults. |

Do not downgrade money/capacity/refund requirements because a native provider is deferred. If the approved payOS acceptance or required refund execution cannot be demonstrated, record the concrete blocker under PM-04; do not claim completion from fixtures.
