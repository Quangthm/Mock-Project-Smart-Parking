> **RC3 amendment:** PAY-01 in section 23 updates payment priority and acceptance; SEC-01/02 remains applied.

> **RC2 amendment:** SEC-01/SEC-02 now supersede the historical TOTP deferral throughout this retained review. See [current decision](2026-10-13-totp-account-recovery-sec-01-02.md). Earlier dated decisions below remain historical lineage.

# SmartPark v1.0 baseline — decision review

Original review: 7 October 2026. Latest decision update: **12 October 2026 (Asia/Ho_Chi_Minh)**, incorporating the user’s explicit approvals in this conversation. Supersedes conflicting 8–9 October choices; historical review findings and revision history remain traceable. **Status: WORKING decision review; approved choices incorporated, remaining ambiguities explicit. This is not the published SRS v1.0, an executed migration, or evidence of implementation/test completion.**

## 1. Scope and reading guide

This review compares the supplied SRS v0.9, business rules v0.8.5, use cases v0.9, user stories v0.8.5, `drawdb-schema.sql`, database architecture, data dictionary and database README with the originally reviewed `develop` architecture and relevant persistence code. Repository snapshot: `7d5e2656d286fc4acc0b423cba2efdb464b71b55`. This was a static review; the SQL was not executed, migrations were not validated against a live database, and no source documents or code were changed by that original review. This update changes only this decision review; it does not modify SRS/BR/UC/schema/code. §17 records the limited static repository observations already obtained in this conversation at newer pinned commits; it is not a new full repository audit.

Read **§16 for the 12 October approvals**, **§18 for the policy/permission/state worksheet, §19 for approved E01–E03 contracts, and §20 for project-manager closure actions**, **§15 for remaining decisions/tasks**, and §12 for the updated disposition register. §1.2 and §14 are synchronized summaries; §2–5 contain the decisions and historical findings with the greatest downstream effect. §6 covers every TESTER-DEF item, §7 every ARCH-DEF item, §8 every AI-DEP item, and §9 the remaining functional proposals. §10 proposes the documentation layout and closure criteria.

**Approved in this conversation:** D1 Cloudinary first evaluation behind a portable interface, R2 the future development direction; D2 one provider and service-owned metadata, no initial Media service; D3 backend uploads and restricted evidence access; D4 fixed 3D deployment assets; D5 modular business-domain SRS; D6 stable paths, manifest and preserved history; D7 local acceptance profile with production targets labeled separately. D8 now confirms SignalR for the views in §14.6; Redis remains optional. Duplicate D6 is one decision.

**Review priority:** Critical = can invalidate money, rights, security or capacity; High = affects several domains/contracts; Medium = localized implementation or operational consequence. An item can be essential to the document baseline while its implementation remains scheduled for a later sprint. Previously confirmed C-decisions remain authoritative unless an explicit change is approved.

### 1.1 Confirmed service-boundary decision — SB-01

Sources: user-supplied **SmartPark Microservice Boundary Decision Handoff** (`Pasted text.txt`), confirmed 7 October 2026, and supplied **smartpark-microservice-data-ownership-v1.0(1).md**, applied in this 8 October update. This section supersedes conflicting service-split proposals in the original review. Historical code/schema findings describe the previously reviewed commit; this document update does not implement repository changes.

| Confirmed deployable service | Authoritative ownership | Logical persistence boundary |
|---|---|---|
| **User & Access Service** | Identity, authentication/credentials/sessions, roles/current access, organization identity/membership, Operator grants/scopes, profiles and registered vehicles/binding. Facility validates referenced site existence. | UserDb |
| **Facility & Visualization Service** | Sites, floors/zones/slots, access paths, supported categories, configured physical capacity, layout/coordinates, maintenance/service configuration, open/closed state and fixed 3D deployment-asset references. React/Three.js renders the visualization. | FacilityDb |
| **Parking Operations Service** | Reservations and competing claims, accepted occupancy/uncertainty/manual verification/reconciliation, allocations, visits/admission/checkout/departure, tickets/QR credentials and reusable-card assignment/release/loss/disabling/history, incidents/appeals, monthly plan definitions/fee-coverage entitlements, Search and authoritative availability. | OperationsDb |
| **Payment & Pricing Service** | Versioned tariffs, pricing/accepted financial snapshots, payment orders/attempts/providers/cash records, verification/idempotency/reconciliation, financial refunds/invoices; monthly orders, payments and financial paid periods. | PaymentDb |
| **IoT & Recognition Service** | Device/readers, raw observations, recognition/evidence and audited corrections; separate Assistant module with provider integration, conversations and tool/confirmation history. Never grants domain rights. | IoTDb |
| **Notification & Audit Service** | Separate Notification delivery, Audit projection and General Support case/handoff modules. Source services retain authoritative business facts and reports. | NotificationAuditDb |

These are six main deployable services. **Search/discovery is an Operations module; Assistant and conversation storage are an IoT & Recognition module; General Support is a Notification & Audit module.** Parking incidents/appeals remain in Operations. No separate Media, Monthly, Policy, Search, Support or AI service is introduced. Each domain owns its reports: Operations visits/occupancy/reservation utilization; Payment collected money/refunds/revenue; Facility configuration/layout; IoT device/recognition status; Notification & Audit audit search/delivery and its own support metrics.

**Boundary:** Facility owns what resources exist and their structural/service configuration; Parking Operations owns rights, claims, visits and business availability. IoT & Recognition supplies observations and never grants admission, payment clearance or gate business authorization. Facility/3D projections do not become a second occupancy or availability authority. Combining IoT/Recognition does not bring deferred real hardware integration into MVP.

**Policy ownership is confirmed:** User & Access owns security/session/access rules; Facility & Visualization owns structure/service configuration; Parking Operations owns reservation hold/protection/allocation/late/reallocation/backup/admission/capacity rules; Payment & Pricing owns tariffs/financial rounding/refund calculation/provider rules; IoT & Recognition owns recognition/device configuration. Shared catalogue conventions do not create a shared policy database or require Payment for unrelated policy decisions. Preserve applicable Admin defaults/bounds → Owner/lot override → supported zone/specific scope → resolved booking context. Individual values, override permissions, effective-date and snapshot mechanics remain subject to existing approval gates.

**Monthly boundary is confirmed:** Operations owns plan definitions, accepted coverage periods, entitlements and visit admission; Payment owns linked financial terms, prices, orders, paid periods and refunds. The selected v1.0 product is **when-space-available only**: fee coverage with no capacity or named-slot guarantee. Earlier guaranteed-capacity/slot and delayed-slot-activation proposals are superseded for this baseline. Money, entitlement and physical occupation remain separate. See §1.2 and B05.

**Data/communication rules are confirmed:** Each service owns its logical persistence boundary and credentials; one local PostgreSQL server is permitted. Logical DB names above are naming guidance, not an executed migration. No cross-service joins, foreign keys, repository or DbContext access. Cross-service IDs are references validated through contracts/workflows/events or justified local projections. Prefer authenticated HTTP/OpenAPI for immediate requests and durable asynchronous outcomes for propagation. Important audit facts are committed durably with the originating operation/outbox, then projected into Audit; a synchronous central audit call is not a global prerequisite for business success. Notification failure does not roll back the original operation. Use one coordinator per workflow and avoid same-workflow synchronous dependency cycles. RabbitMQ + MassTransit is selected for inter-service asynchronous messaging; MQTT remains for IoT/device messaging, with real hardware still deferred; Kafka is deferred. Exact event contracts and recovery implementation remain open.

**Decision effect:** SB-01 and the supplied microservice data-ownership guide settle service/module ownership, including ARCH-DEF-02/04. The consolidated 8–9 October confirmations below additionally settle billing, checkout, monthly product, broker, facility-change protocol, version preservation, authentication direction, media-access principles and the core local acceptance targets. Remaining details are enumerated in §12. Approval of behavior never means code, schema or tests are complete.

### 1.2 Confirmed decisions — consolidated through 12 October 2026

Latest explicit user choices supersede conflicting proposals in this review. Existing IDs are retained for traceability; technical suggestions not explicitly accepted remain recommendations.

| Area | Confirmed decision | Limits / superseded text |
|---|---|---|
| B03 billing | Actual applicable interval overlap; sum unrounded segment charges, then round once **half up to nearest 1,000 VND**. Free threshold defaults to **15 minutes per session**; exceeding it makes the full applicable duration chargeable. | Latest explicit amendment restores the per-session rule: no daily aggregation, no 15-minute deduction, no duration inflation. Monthly-covered fees remain exempt; see §16.9 and confirmed D04 in §18. |
| B04 checkout | Verified checkout at the exit gate freezes billable end; state becomes pending payment and completes on payment. Gate then opens and physical departure is separately recorded. Drivers must leave; a new stay requires exit/re-entry. | Replaces pricing through later physical exit. Payment/gate-open is not proof of departure or permission to free capacity. |
| Checkout recovery | Authorized audited unpaid recovery retained. Assigned Operator handles system-mispricing corrections at the original accepted checkout cutoff; Payment & Pricing calculates/executes the difference; Owner handles escalation. | No paid-stay extension or full-refund/full-recharge route in MVP. Preserve transactions, adjustments and invoice history (§16.3). |
| B05 monthly product | **When-space-available only:** monthly purchase covers applicable fees during the accepted period for the bound vehicle/location/conditions; it reserves no capacity and promises no admission when full. | Supersedes both the proposed guaranteed pooled-capacity option and slot-binding/delayed-activation design. No monthly queue, permanent slot protection, automatic future-reservation displacement or fixed 50,000 VND denial compensation is approved. Ordinary reservation types remain unchanged. |
| Monthly fulfillment | Operations validates entitlement; Payment creates financial order; verified payment reaches Operations recoverably; Operations commits only if deadline/conditions hold; failed fulfillment remains visible for reconciliation/refund. | Temporary resource commitment is used only where actually required. This available-only pass creates **no parking-capacity hold**. Preserve strict monthly A<H, rolling anchors, [S,E), renewal and expiry rules unless expressly amended. |
| Monthly refund | Paid price × unused timestamp fraction × applicable accepted rate. Defaults: prestart 100%, voluntary midperiod 50% of unused value, facility failure 100% of eligible unused value. | Sum once, half-up to nearest 1,000 VND; cap cumulative refunds at eligible collected money. Owner handles monthly cancellation. Detailed override bounds remain to specify. |
| Facility changes | Planned change remains pending while Operations restricts conflicting new claims and evaluates existing commitments; finalize recoverably with affected outcomes. Emergency physical closure is recorded immediately with operational restriction and recovery. | Physical emergencies cannot wait for successful propagation; admission must be stopped operationally if communication is unavailable. Exact command/timeouts are implementation work. |
| Policy versions | Preserve accepted-booking terms using immutable Operations policy and Payment financial/tariff versions. | No retroactive rewriting by later Owner edits. Remaining catalogue entries/bounds/override permissions need specification; approved defaults are in §16.1–2. |
| B08 authentication | Password and verified flows retained; **passwordless OTP login for all four roles through email and phone**. Access 15 min; absolute refresh session 7 days; refresh inactivity 24 h. Turnstile and current authorization retained. | TOTP remains future. Login uses verified bound contact; either verified bound channel independently is confirmed; challenge, recovery and provider contracts need specification (§16.5). |
| B07 messaging | RabbitMQ + MassTransit inter-service messaging; MQTT device messaging; Kafka deferred. | Redis optional pending concrete approved use; SignalR scope is confirmed in §14.6. Broker selection does not prove reliable fulfillment. |
| B06/B09 media | Backend uploads, owner references, restricted proxy delivery, published facility media, acknowledged holds and accepted limits retained. Financial documents 7 years; conversations 90 days; hold review every 30 days and at case closure. | Existing image/audit/operational retention retained. Clock anchors are confirmed in §16.4; exceptional hold/evidence handling remains specification work. Provider capabilities remain unverified. |
| B10 local acceptance | Approved integration matrix and performance recipe in §16.6–7; daily backup/RPO ≤24 h, restore ≤30 min, restart recovery ≤5 min. | Local test targets are not results or production guarantees. Actual hardware, workload mix and provider evidence must be declared. |
| Operational policies | Refusal of a valid conforming alternative follows published voluntary cancellation; unusable/nonconforming alternative is distinct. Early entry only if physically feasible with disclosed extra charges. Reallocation allowance counts accepted submissions, excluding transport retries/invalid requests. Preserve Operator tie-break with reasons; human review before confirmed violation consequences; emergency departure does not erase financial facts. | Compatibility principles and refund defaults are settled; exact permissions, remaining bounds and transition tables need specification. |

**Monthly examples:** A paid pass holder arriving at a full lot cannot enter on the strength of the pass. An eligible free space allows ordinary admission and a new visit record; covered time is not charged again. While the vehicle is outside, monthly ownership subtracts zero capacity. Once inside, actual occupation counts once. Expiry ends coverage but never clears occupation. Ordinary fullness is not breach of a nonexistent capacity guarantee. Facility closure or failure of paid coverage is a distinct incident; compensation remains governed by approved terms.

**Checkout examples:** (a) Unpaid checkout at 10:00 may be invalidated through authorized recovery on the same session. (b) Paid 20,000 VND with a corrected charge of 15,000: refund 5,000; corrected charge 25,000: collect 5,000. (c) Continued parking after a paid checkout requires actual exit/re-entry and a new session, e.g. 10:10–10:30; do not reopen the paid visit to bill additional stay. Pricing adjustment does not erase the original payment. Original checkout cutoff and Operator authority are now confirmed; full-refund/full-recharge is excluded from MVP (§16.3).

**Propagation work:** amend active SRS §3.5.1 and monthly Appendix F/FR-MON-06; BR M-04/M-07 guaranteed-resource clauses, M-05 proposed floor/rates and M-08 actual-exit wording; corresponding UC/FR/test expectations and architecture/schema wording. Preserve the original v0.9 history. This review update does not perform those other edits.

## 2. Decision status and remaining priorities

| ID | Recommended decision | Alternative and trade-off | Impact, affected areas and future risk |
|---|---|---|---|
| B01 | **CONFIRMED — SB-01:** Parking Operations Service owns reservations, claims, allocation, admission, sessions, monthly resource commitments and authoritative availability in one transactional boundary. Facility & Visualization owns structure/service configuration; IoT & Recognition owns raw observations. | Historical alternatives, not selected: sessions in the old Parking/Facility boundary under SA-21; separate Reservation/Session/Capacity services; merging all facility and operational capabilities. | **Critical:** supersedes SA-21 at design level. Keep cohesive internal modules in Operations. Repository/API/schema migration and physical observation reconciliation remain implementation work; database atomicity does not control real movement. |
| B02 | **CONFIRMED — SB-01:** Payment & Pricing Service owns tariffs, pricing versions/calculation, financial snapshots, money/refund execution and monthly financial paid periods. Parking Operations owns monthly entitlement/resource commitments. Policies remain domain-owned; no generic Policy Service. | Historical alternatives, not selected: Facility-owned tariff authority, separate Pricing deployment, or Payment-owned global policies. | **High:** tariff ownership must be aligned in schema/contracts. Owner UI authority is unchanged. Refund defaults are confirmed in §16.2; override bounds, effective policy implementation and financial/entitlement recovery contracts remain to specify; the unused-period formula is confirmed. |
| B03 | **CONFIRMED:** actual applicable overlap; sum once and half-up round to nearest 1,000 VND; 15-minute per-session threshold with full applicable duration chargeable once exceeded. | Daily aggregation, nearest-whole-VND and duration increments superseded. Q01 resolved by explicit amendment. | **Critical:** update session-boundary and monthly-overlap fixtures (§16.9; D04). |
| B04 | **CONFIRMED:** checkout cutoff, difference-only mispricing correction by scoped Operator, Payment execution, Owner escalation; physical departure independent. | No paid-stay extension or full-refund/full-recharge. | **Critical:** specify adjustment ledger, retries and late outcomes; cutoff/authority are settled. |
| B05 | **CONFIRMED:** when-space-available fee-coverage pass only; Operations rights and Payment financial terms; recoverable fulfillment and unused-period refund formula. | Guaranteed pooled capacity, named monthly slot and waiting-for-slot activation superseded for v1.0. Refund defaults confirmed in §16.2. | **Critical:** no monthly capacity deduction while outside; normal admission/occupancy while inside. Revise guaranteed-plan FR/BR clauses without changing ordinary reservation modes. |
| B06 | **CONFIRMED:** backend uploads, stable references, restricted-evidence proxy, explicit facility-media publication, acknowledged holds and upload/failure rules in §14.4. | No new Media service or public evidence links. | **High:** retention classification, provider control/deletion validation and acceptance implementation remain. |
| B07 | **CONFIRMED:** RabbitMQ + MassTransit, owner outcomes, local durable audit/outbox and recoverable workflows; MQTT for devices, Kafka deferred. | SignalR is confirmed for the selected views; Redis remains optional. | **High:** exact event versions, deduplication, ordering, retries, recovery operators and contracts remain design work. |
| B08 | **CONFIRMED:** password and verified flows, all-role email/phone OTP login, 15-min access / 7-day absolute refresh / 24-h inactivity, current authorization and Turnstile. | TOTP FUTURE. OTP login is separate from contact/reset verification. | **High:** finalize permission/challenge/revocation contracts; booking restrictions remain separate from login. |
| B09 | **CONFIRMED:** retained C-20 classes plus financial 7 years, chat 90 days, scoped holds and 30-day/case-closure reviews (§16.4). | Clock anchors confirmed in §16.4; specify evidence verification/deletion mechanics without reopening durations. | **High:** hold/deletion races and provider validation remain implementation work; no automatic expiry of an active hold. |
| B10 | **CONFIRMED:** §16.6–7 local integrations, load/freshness/error recipe and backup/recovery objectives. | Actual machine, endpoint mix, data composition and results still required. | **High:** fixtures are not real integrations; local requirements are not achieved production SLOs. |

B03 threshold exemption is confirmed: the default is 15 minutes per session, inclusive at the threshold; once exceeded, the full applicable duration becomes chargeable. **No daily or per-vehicle aggregation; midnight does not restart the exemption. Reserved visits retain their disclosed booked-duration minimum even at/below 15 minutes (D01); monthly-covered ordinary fees are never charged again (D04).** Actual overlap and one final half-up rounding to nearest 1,000 VND apply; no duration inflation.

## 3. Storage references across services

### 3.1 Ownership and reference model

Store bytes in Cloudinary and authoritative asset metadata in the business service that owns the asset's lifecycle. Other services use an opaque reference, not the owner's database or an expiring URL.

| Asset | Proposed owner | Cross-service use |
|---|---|---|
| Avatar, registered-vehicle image | User & Access Service | Consumers receive an authorized representation/reference. Personal profiles need not have a parking tenant. |
| Lot photo, uploaded diagram | Facility & Visualization Service | Search/UI consumes approved public or restricted representation. Fixed 3D models stay deployment assets under D4. |
| LPR source image and recognition result | Recognition/Evidence module within IoT & Recognition Service | Session/incident stores `{ownerService, assetId}` and a reference to recognition evidence. Store once; recognize again without changing original evidence. |
| Operator-uploaded incident or session evidence | Parking Operations Service | Appeal references the evidence; retention owner acknowledges holds. Proxy access, acknowledged holds and limits are confirmed under B06/§14.4; implementation mapping remains. |
| Generated financial document | Payment & Pricing Service | Customer receives an authorized document endpoint. Retention class is financial-document policy, not automatically the 30-day image policy. |

Suggested metadata: local `assetId`; business owner/resource; tenant/site where applicable; purpose and sensitivity; provider; provider environment/account locator; provider asset/object identifiers; resource/delivery type and version; validated MIME type, byte size and hash; creation time; retention deadline; hold records; lifecycle state; deletion/retry metadata. Provider secrets never belong in these records.

For Cloudinary, retain sufficient identifiers to retrieve/delete the precise resource (including public ID, resource type and delivery type, plus asset ID/version where applicable). An R2 adapter would use bucket/object/version details. Keep these details behind the adapter rather than forcing every business table to understand both providers.

Example business reference:

```json
{"ownerService":"iot-recognition","assetId":"opaque-uuid","purpose":"entry-evidence"}
```

Example opaque provider path: `smartpark/{environment}/{service}/{purpose}/{assetId}`. Do not put plates, email addresses, names or access tokens in paths. Folders are organization, not authorization. Do not assume one shared provider account provides isolation between service credentials; document the actual account capabilities during evaluation.

### 3.2 Request flow

1. Client uploads to the owning business API through the gateway. The owner checks current permission, tenant/site/resource, allowed purpose, type, size and content, and creates an idempotent pending asset record.
2. The owner's storage adapter uploads using server-held credentials and an opaque provider identifier. On success, the owner finalizes metadata and attaches the asset to the business record.
3. Restricted reads use a stable application route, such as `GET /api/sessions/{sessionId}/evidence/{assetId}/content`. Parking Operations authorizes the viewer. When the Recognition/Evidence module owns the image, it retrieves via a narrowly authorized internal request to IoT & Recognition; the asset owner verifies service identity, scope and asset binding. Do not accept arbitrary remote URLs supplied by clients.
4. Other services carry `{ownerService, assetId}`; they do not copy expiring links into durable events. Read-time delivery may be a proxy or an expiring access grant according to approved sensitivity.
5. Owner performs scheduled deletion after retention expiry unless a hold is acknowledged. An appeal must obtain the hold acknowledgement before claiming evidence is preserved. Hold release and deletion must coordinate against races.

Cloudinary distinguishes public, private and authenticated delivery. A signed transformation URL is not automatically a short-lived access grant, and some token-based controls depend on account features. For strict evidence access, evaluate authenticated originals and proxying; verify transformed copies and caches are also restricted. See [Cloudinary access-control documentation](https://cloudinary.com/documentation/control_access_to_media).

### 3.3 Failures and portability

An upload and a business-database commit are not one transaction. Use pending/finalized/deletion-pending states, idempotent provider keys and a reconciliation job. Handle upload-success/database-failure, abandoned uploads, duplicate requests, provider outage, deletion failure and expired access. Do not report evidence as attached until finalization succeeds. Never delete held evidence merely because its original device or business row is removed.

Portable interface responsibilities: store validated content, read/stream content, issue temporary access only when supported, delete, and obtain metadata. Authorization and retention policy remain in business services. A shared infrastructure package may contain adapters; it should not contain a shared media database or business rules. Avoid making Cloudinary transformation syntax part of the domain contract.

Migration to R2 still requires copying bytes, verifying hashes, switching locators and retaining a recovery window. The interface reduces code coupling; it does not eliminate migration, access-control or egress work. One provider remains active for the initial evaluation; do not add dual writes now.

## 4. Service boundaries and communication findings

### 4.1 What current code actually couples

The `develop` Parking persistence repository directly queries identity tables (`accounts`, `users`, `account_roles`, `tenants`) and reservation/session/capacity tables. User's operator-provisioning repository directly queries `parking_sites`. The `05.6-Parking-Structure.sql` guards inspect reservations, allocations, sessions and capacity pools and lock the site row. These are shared-database dependencies, even though the architecture and supplied DB overview describe independent databases.

This is an architectural mismatch, not evidence that the current local implementation never works. Splitting connection strings later will remove the joins and cross-domain trigger protection. APIs/events and an explicit invariant owner must replace them first. Service-owned data and cross-service contracts are consistent with [Microsoft's microservice data-ownership guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice).

At the originally reviewed commit, Owner approval activates the account but does not provision a tenant according to the repository handoff. **SB-01 confirms User & Access owns organization identity/membership**, while Facility & Visualization owns site existence. Idempotent provisioning and an explicit completion outcome before lot creation remain implementation recommendations. Facility may maintain a scoped organization projection. Historical Facility-owned organization authority was not selected.

### 4.2 Proposed invariant and communication map

| Workflow | Authority and communication | Failure handling / why it matters |
|---|---|---|
| Search availability | Search module reads a Parking Operations snapshot with timestamp and relevant version. It is not a separate mandatory deployment. | Search can be stale; create/admit always revalidates. A displayed free slot is not a reservation. |
| Reservation and walk-in admission; monthly eligibility check without capacity guarantee | Same authoritative claim ledger and database transaction under B01. Physical accommodation checked before admission. | Atomic compatibility/capacity decision; duplicate commands return the previous result. No separate counters that independently grant the same unit. |
| Layout closure/capacity reduction | Facility & Visualization owns the change; confirmed protocol asks Parking Operations to fence affected resources before finalizing a planned change. | Voluntary changes stay pending on failure. An emergency physical closure is recorded immediately, blocks new admission and starts reconciliation even if existing rights exceed new capacity. Protocol details remain open; never pretend a physical emergency can be rolled back. |
| Actual arrival/entry/departure | Authenticated commands to Parking Operations; IoT & Recognition publishes observations, while Facility supplies structural/service-state facts. | Duplicate event IDs do not repeat effects. Arrival, gate-open, entry, occupied slot and departure are different facts. Late/out-of-order observations require reconciliation. |
| Payment success | Provider → Payment & Pricing verification/local commit → durable financial outcome → Parking Operations entitlement decision. | Money and entitlement have separate states. Unknown result is reconciled, not retried as failure. Reservation C-09 and monthly A<H use different acceptance rules. |
| Refund | Parking Operations operational approval/case → Payment & Pricing financial execution → outcome event. | Approval is not payout. Repeated delivery cannot pay twice; failed payout retains a recoverable balance. Detailed workflow remains open. |
| Current authorization | User & Access owns identity/grants; gateway authenticates; resource service checks current scope/state. | Current authorization checks for protected actions are confirmed; exact lookup/revocation contracts remain implementation work. Fail closed for protected operations when access cannot be established; later caches need explicit revocation semantics. A valid JWT alone is insufficient. |
| Notification/audit/report/SignalR | Originating service commits business fact and durable event; Notification & Audit consumes into separate delivery/audit modules. SignalR scope is confirmed in §14.6. | Audit projection or notification failure cannot roll back the originating business result. Delivery retries/deduplication/replay and any live-view resynchronization require detailed contracts. |

Every event contract should identify event ID, schema version, aggregate ID/version, tenant/site when applicable, occurred/recorded times, correlation/causation IDs and minimal payload. The authoritative owner publishes it. Consumers must tolerate duplicate and out-of-order delivery. Do not broadcast sensitive evidence links or full plate data to all live-view clients.

SB-01 forbids same-workflow synchronous request cycles: e.g., Parking Operations → Facility & Visualization → Parking Operations, or Operations → Payment → Operations. Separate identity membership checks from site ownership, and define one command coordinator per workflow. Use bounded timeouts and result lookup after ambiguous outcomes; distributed retries must not multiply each other.

## 5. Schema and documentation conflicts

**Historical snapshot findings:** These are design findings about the originally supplied export unless a repository implementation is explicitly named. §17 records selected newer observations (including now-present monthly tables/exclusions); do not treat old absence claims as current without rechecking. Absence from this SQL is not proof that no other implementation exists. The six deployment scripts named by the attached overview were not supplied; the combined export is not proof of six independently deployable databases.

| ID / priority | Evidence and conflict | Recommended response; alternative / downstream cost |
|---|---|---|
| DB01 Critical | Overview claims strict service databases; repositories/triggers at the reviewed commit cross domain tables. | SB-01/B01 confirms six service-owned persistence boundaries. Replace cross-service data access and guards with approved contracts/invariant ownership. These code/migration gaps remain OPEN; an acknowledged temporary shared implementation is not the target architecture. |
| DB02 High | Sessions in attached Reservation DB conflict with old proposed SA-21 Parking ownership; tariffs in old Parking differ from financial-domain ownership. | **Ownership resolved by SB-01/B01/B02:** sessions/claims → OperationsDb; tariffs/financial paid periods → PaymentDb; structures → FacilityDb. Updating architecture/schema/code is still OPEN. Historical alternatives remain traceable. |
| DB03 Critical | No monthly plans, anchored paid periods, entitlement commitments or renewal/cancellation records; payment reference types also omit monthly despite monthly order type. | SB-01 confirms Payment & Pricing financial ownership and Parking Operations entitlement ownership. Add the available-only M-01–09 model; ordinary reservations/visits retain the common capacity ledger; schema/workflow gaps remain OPEN. FR-MON-01–14 cannot be implemented by `order_type` alone. |
| DB04 Critical | RLS wording overclaims protection. No `ENABLE ROW LEVEL SECURITY` or `CREATE POLICY` in export. | Composite FKs constrain relationships, not who may read/write. Document application authorization and, if selected, actual RLS policies/roles and tenant context. Do not claim “impenetrable.” |
| DB05 Critical | Overview claims GiST range exclusion; export contains no exclusion constraint. Counts have no booking-time dimension. | Specify temporal claim model and concurrency algorithm. Slot exclusion can protect exclusive slot claims; pooled capacity needs a separate transactional algorithm across relevant intervals. A counter CHECK alone is not the complete invariant. |
| DB06 High | Enum CHECK validates permitted labels, not legal transitions. | Specify transition commands, allowed prior state/version and atomic update predicates. Keep enum CHECK as defense, not workflow proof. |
| DB07 High | Mutable `parking_slots.reservation_state` cannot describe several future bookings or distinguish backup configuration from current protection. | Treat it only as a versioned display projection, or derive it; authoritative claims live in the ledger. Distinguish backup, protection and emergency capacity. |
| DB08 Critical | Allocations allow both owner refs absent; expected end does not cover actual overstay. No complete interval compatibility/history model. | Require a valid claim owner; allow reservation+session linkage when fulfillment intentionally has both. Preserve allocation revisions and current physical occupation beyond expected end until departure. |
| DB09 High | Session states mix physical, financial and dispute dimensions. Ticket is nullable/nonunique; no visible duplicate-open-visit constraint. | Separate visit state, settlement state and case state. Enforce ticket identity and applicable open-session uniqueness. Preserve nullable account/plate for walk-ins/non-plated categories. |
| DB10 High | Session category values do not explicitly represent the approved non-plated categories; reservation mode/requested target, confirmation time and allocation revision are incomplete. | Define a single compatibility/category contract and explicit Specific Slot/Zone/Capacity requests. Add facts needed for confirmed allocation ranking. Do not fabricate plates. |
| DB11 High | Policy rows require tenant scope; no clear platform defaults/bounds, optional-policy activation or immutable accepted operational snapshot. | Typed versioned policy catalogue and effective-date resolution, with booking snapshot references. Zero, unset and disabled need separate meanings. SQL defaults such as 30-minute allocation are not automatically approved business values. |
| DB12 Critical | Tariff JSON lacks a defined interval/version/effective-time contract; site timezone is unspecified. | Implement confirmed B03/B04, add named site timezone and immutable tariff versions. Backend calculates local intervals; frontend display conversion alone is insufficient. |
| DB13 High | Operator permission list restricted to four permissions, omitting required operational overrides/emergency actions. | Versioned permission catalogue mapped to approved matrix. Do not enable arbitrary privilege assignment or give Owners lot-device mutation contrary to C-02. |
| DB14 High | Account status and lock semantics differ between overview/schema and current implementation; owner application `lot_type` is mandatory though C-23 only confirms company/email/phone. | Define account/role/lock mappings and provisioning state. Make extra onboarding field optional or explicitly approve it. Apply B08 direction and settle remaining token/auth details. |
| DB15 Medium | Dictionary claims `lower(email)` uniqueness absent from export; both email/phone nullable. UUID extension/bootstrap not shown. | Correct dictionary or add intended constraints/migration prerequisites. Validate required contact per registration workflow; do not assume every historical/guest record needs both. |
| DB16 High | Dictionary omits `UNAVAILABLE`, SQL includes it; layout/vehicle enums and columns differ from `develop` code (`operational_status`, `is_physically_occupied`, `coordinates_3d`, etc.). | Establish canonical state/category mapping and migration plan. Export is not a drop-in replacement. Flexible hierarchy also needs cycle prevention and path maintenance. |
| DB17 High | Capacity-only/zone lots and zero/unconfigured capacity semantics are unclear; current code derives some capacity from slots. | Declare capacity mode and units. Do not force numbered slots for every supported lot or interpret zero inconsistently. Pooled capacity and individual-slot claims need explicit compatibility rules. |
| DB18 High | URL-only image columns in user, vehicle, session, refund, LPR tables do not express ownership, retention or holds. | Apply §3; preserve legacy URL only as migration/source metadata where necessary. Add asset lifecycle and held-evidence references. |
| DB19 High | LPR requires device and normalized/original plate even for device-free upload or failed recognition; device cascade can erase evidence. | Optional device; explicit no-plate/unreadable/multiple/timeout/manual-review outcome; model version and correction history. Retain evidence independently of device removal. |
| DB20 Critical | `ON DELETE SET NULL` on composite references includes NOT NULL tenant/site columns. | Such referenced-row deletion can fail. Prefer historical RESTRICT/soft deletion, or deliberately null only the optional reference column. Do not null scope to make deletion pass. |
| DB21 Critical | Incomplete refund/transaction amount guards, currency contract, cumulative refund protection and provider transaction uniqueness. | Enforce financial invariants and provider/account/amount/currency matching; reconcile unknown outcomes. Webhook-event uniqueness alone does not guarantee unique money movement. |
| DB22 High | Invoice requires session ID; reservation/monthly payments can exist before any session. | Define financial-document subjects separately, or explicitly name this table session-only and model other required invoices. Avoid fake sessions. |
| DB23 Medium | Notification metadata is useful but nullable dedupe keys and overwritten attempt count can lose meaningful delivery history. | Define semantic dedupe key and per-attempt evidence; distinguish accepted by provider from delivered. Multiple valid templates for one event must not accidentally collide. |
| DB24 High | Incident/appeal/audit/hold/media/outbox concepts are absent from export. | Add domain-owned design/contracts for confirmed scope. SB-01 places central audit projections with Notification in NotificationAuditDb; originating services retain durable audit facts/outbox. Search remains a module. No new deployment per missing table. |

PostgreSQL supports exclusion constraints and RLS, but they have to be declared and used with the appropriate roles; CHECK constraints do not establish a general cross-row business invariant. Relevant references: [constraints](https://www.postgresql.org/docs/16/ddl-constraints.html) and [row security](https://www.postgresql.org/docs/16/ddl-rowsecurity.html).

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

## 9. Remaining functional proposals and embedded unresolved rules

The SRS has 50 functional-index rows with A, X or F in their status. Mixed rows contain both existing requirements and proposed clauses. The disposition below applies to the unresolved clause, not a blanket rewrite of the entire row. Retain original IDs and traceability when updating status after approval.

| Requirement IDs | Proposed disposition | Alternative, pros/cons and downstream risk |
|---|---|---|
| FR-VEH-02 | **CONFIRMED:** duplicate binding rejected without disclosing the other account; Driver may dispute; Operator supplies scoped findings; Admin adjudicates a minimal evidence-backed support case; User & Access executes guarded change. Recognition/session correction is assigned-Operator scoped with reason/history. | Historical visits/invoices/evidence keep original associations. Evidence sufficiency/retention for global rebinding remains to define; no blanket identity-document collection approved. |
| FR-LOT-02, FR-LOT-03 | **CONFIRMED:** outdoor, multi-floor building and multi-floor basement 3D template types; configurable floors/zones/rows; floorless outdoor lots; individually represented spaces and category-specific pools. | Fixed deployment model assets do not fix every lot layout. Mixed car/motorcycle conversion is not an approved MVP capability. Exact dimensions and pool compatibility contract still need specification. |
| FR-LOT-06 | SB-01/ARCH-03 confirms the IoT & Recognition host; retain separate platform integration settings and lot-device authority. Detailed permissions/API remain open. | Owner-managed provider secrets easier locally but expands authority/security exposure. **High.** |
| FR-LOT-07 | **CONFIRMED AMENDMENT TO C-02:** Owner read-only device diagnostics at owned lots; assigned Operator retains device management. | Does not enable Owner mutation or bring real IoT hardware into MVP. |
| FR-POL-06 | Retain backend-resolved terms and renewed confirmation for changes; exact active clause disposition remains part of §15.2 traceability review. | Accept stale terms automatically: fewer prompts, incorrect acceptance contract. **High.** |
| FR-POL-09 | Retain bounded/audited threshold configuration direction; exact threshold/equality and integration deferred after v1.0 with DEF-32. Multi-frame remains future. | Fixed threshold: simpler configuration, less tuning flexibility. **High:** recognition safety/evaluation. |
| FR-CAP-04, FR-CAP-05, FR-CAP-06 | Approve atomic compatible claims, idempotency and claim-to-occupancy conversion, no impossible walk-in admission. | Independent counters/simple eventual updates: cheaper but duplicate allocation risk. **Critical:** B01/DEF-03/06. |
| FR-CAP-01, FR-MAP-01 | **CONFIRMED:** manual location selection when GPS is denied/unavailable. Authoritative availability and filters retain baseline scope. | Map provider/acceptance details remain; GPS-only flow not selected. |
| FR-RES-01 | Approve reviewed request exposing enabled canonical modes; account remains required for reservation. | One mode only reduces implementation but changes confirmed scope. **High.** |
| FR-RES-02 | **CONFIRMED:** Admin-only global cancellation anti-abuse defaults, event counting and booking-only restrictions in §14.1. Advance horizon defaults to 7 days; active bookings/account has no maximum by default (§16.9). D02 locks unspecified ranges at approved defaults and defines any enabled lot-scoped cap. | Does not lock login, payment, existing cancellation or appeals. Monitor unpaid-hold abandonment separately. |
| FR-RES-04 | Retain payment-hold flow for positive-price reservations. **E03 exception confirmed:** genuinely zero-price reservations after valid monthly coverage confirm without monetary payment, subject to atomic capacity and eligibility checks. | General pay-on-arrival reservations remain outside the selected flow; E03 is not permission to bypass positive-price prepayment. **High.** |
| FR-RES-05, FR-RES-10 | Approve booking detail/history with requested versus allocated resource, deadlines and independent payment/refund status. | Minimal status only: easier UI, poor support and user understanding. **Medium.** |
| FR-RES-06 | Resolve stale conflict in favor of C-05: unpaid hold EXPIRED; late-arrival NO_SHOW only under enabled policy. | A single expiry label is simpler but semantically wrong. **High.** |
| FR-RES-07 | **CONFIRMED:** early arrival only after current physical/capacity validation, with separately disclosed extra interval charges. | Disallow early admission until start: simpler, less flexible. **High:** early occupation must not steal another right. |
| FR-RES-08, FR-RES-09 | Approve execution-time cancellation checks and facility recovery/refund workflow; release unused claims once, preserve occupancy. | Immediate full cancellation/refund in every failure: simple but changes cost/entitlement rules. **Critical.** |
| FR-RES-11, FR-RES-12 | Approve audited arrival event and guarded lifecycle/race commands under DEF-01/20. | Infer arrival from QR recognition or rely on enum CHECK: easy, insufficient. **Critical.** |
| FR-PAY-01, FR-PAY-02 | B02 ownership is confirmed; B03/B04 arithmetic and checkout are confirmed; transparent breakdown and DEF-25 fixtures remain implementation/acceptance work. | Preserve competing formulas: no coherent acceptance result. **Critical.** |
| FR-PAY-04 | Approve amount/currency/order/payee verification, provider transaction dedupe and reconciliation. | Trust callback success label: less code, financial error exposure. **Critical.** |
| FR-PAY-08 | Approve overstay notice and visible approved charges, while actual occupancy continues. | Automatically create penalties from notification failure or elapsed estimate: conflicts with authority. **High:** tariff bands still require policy approval. |
| FR-BAS-16 | Preserve four-total-attempt interpretation and PDF invoices; define supported invoice subjects and replay-safe issuance. | Drop PDF or one payment channel: explicit scope change, not infrastructure simplification. **High:** UNKNOWN is not failed. |
| FR-GATE-01, FR-BAS-09 | Preserve image-input LPR/manual fallback/evidence direction; detailed recognizer/integration/evaluation deferred after v1.0. Core simulated Operations entry/departure remains specified separately. | Real gate/camera mandate adds deferred scope; fixtures alone do not prove model accuracy. **High.** |
| FR-INC-04 | **CONFIRMED:** human-reviewed findings and limited permitted consequences in §14.7. | Confirmation alone authorizes no invented fine, ban, forced removal or government reporting. |
| FR-RPT-03 | **CONFIRMED:** scoped report access in §14.7, protected audit history, current authorization and preserved C-20 retention. | Exports and live channels inherit the same restrictions; implementation remains. |
| FR-SIM-01 | Preserve deterministic fixtures; product simulator remains outside MVP unless separately approved and isolated. | Product simulation UI adds build/test scope and must not mutate operational resources. **Medium.** |
| FR-AI-01, FR-AI-02, FR-AI-03 | **CONFIRMED:** FAQ/clarification, Admin-published versioned knowledge and API-grounded search. | Exact prompts/retrieval/ranking fixtures are design work; no draft policy as truth. |
| FR-AI-05, FR-AI-10 | **CONFIRMED:** authorized reads with requesting-user/resource scope; domain APIs enforce permissions. | Detailed tool schemas still needed; no direct broad database tools. |
| FR-AI-06, FR-AI-07, FR-AI-08 | **CONFIRMED:** prepare reservation → normal UI confirmation → backend command → actual result/payment checkout. | Cancellation/refund/security requests route to ordinary UI; autonomous writes not selected. |
| FR-AI-09, FR-AI-11 | Approve explicit empty/error/unknown outcomes and backend-calculated charge explanations. | Generic “success” or model-recomputed price is simpler but false. **High.** |
| FR-INC-02, FR-OPS-04, FR-OPS-05 | Keep multi-frame violation AI, offline authorization and offline replay future. | Adding offline operation creates a second authority/reconciliation/security problem. **High scope expansion.** |
| FR-BAS-12, FR-BAS-13, FR-BAS-14, FR-BAS-15 | Keep personalized ranking, AI occupancy, forecasting and anomaly detection future. Preserve deterministic MVP ranking. | More AI features may improve demonstration breadth but need datasets, consent and response workflows. **High scope expansion.** |

Additional unresolved text outside those rows:

1. **Monthly M-05 rates (Critical).** Unused-time formula is CONFIRMED: paid price × unused fraction × approved rate, using actual paid-period timestamps. Sum applicable amounts and round once half up to nearest 1,000 VND; floor and nearest-whole-VND rounding are superseded. Voluntary/prestart/facility-failure defaults are approved in §16.2; old examples are no longer merely illustrative for those specific rates. Owner monthly refund approval and Payment payout remain distinct. Available-only fullness does not trigger guaranteed-capacity compensation. Cumulative refund safeguards and no automatic offset of unrelated debt remain required design constraints.
2. **Payment providers (High).** C-10 already confirms VNPay, MoMo and ZaloPay plus Operator cash collection. PAY-01 amends the provider implementation priority and acceptance profile: controlled low-value payOS production tests plus deterministic failure/race fixtures and tested Operator cash. Native VNPay/MoMo/ZaloPay remain separately identified; unfinished integrations may remain simulated/deferred. Record collection/refund capabilities and evidence; fixtures cannot establish complete provider integration. API outage/UNKNOWN must not authorize blind new charges.
3. **Policy values (High).** Confirm every required default/bound rather than accepting schema/demo seeds. Holds, protection/allocation lead, late-tolerance activation, occupancy projection, reallocation limit, notifications, tariffs, refunds, monthly holds/prepayment and security settings have different owners/semantics. Preserve known defaults (ordinary hold 5 minutes, occupancy projection 4 hours, three payment retries, two Driver reallocation requests). Projection expiry never means physical departure.
4. **Retention/holds (High).** C-20 retained; media access, scoped hold/release, acknowledgement, failure behavior and upload limits confirmed in §14.4. Financial documents 7 years, conversations 90 days, and hold reviews every 30 days/at case closure are confirmed (§16.4); clock anchors now confirmed in §16.4; exact provider verification remains to specify. Free-tier constraints do not silently shorten approved retention.
5. **Authentication/onboarding (High).** B08 direction and Turnstile confirmed; TOTP explicitly deferred. Token lifetimes and all-role email/phone OTP login are confirmed (§16.5); organization provisioning completion and challenge/security implementation remain to specify. Registration/contact/reset verification is distinct from OTP login.
6. **Legacy BR examples (High).** Retire active use of stale snippets for Owner-only ordinary refunds, superseded actual-physical-exit billing rules, fixed reallocation counts, outdated free-boundary wording and universal rejection of late ordinary payments. Preserve them in history with supersession links; do not import them into active FAQ or test expected results. M-05 formula and the specified rate defaults are confirmed; synchronize old example/proposal labels with §16.2.
7. **Permissions and scope (High).** Approve a complete MVP permission catalogue and who may grant it; lot device management stays assigned Operator, not Owner/Admin inheritance. General Support ownership in Notification & Audit is confirmed; define scoped handlers without inventing a fifth actor. Implementing an emergency endpoint without its corresponding scoped permission leaves a contract gap.
8. **Evidence failure policy (CONFIRMED).** Evidence-dependent decisions remain pending while required evidence is missing. Approved emergency operations may proceed with an audited missing-evidence incident. There is no blanket storage-outage free-exit rule. Implement action-specific guards and recovery.

## 10. Documentation plan using existing categories

Keep `docs/01-requirements`, `02-architecture`, `03-design`, `04-testing` and `05-project-management`; no new top-level document taxonomy is needed. Business modules are reading boundaries and need not map one-to-one to services.

| Proposed stable path | Contents and ownership |
|---|---|
| `docs/01-requirements/srs/README.md` | Current approved baseline, reading order, status legend, module links and historical entry points. |
| `docs/01-requirements/srs/modules/00-overview.md` | Scope, actors, glossary, exclusions and normative precedence. |
| `.../modules/01-identity-access.md` | Registration/auth/profile/vehicle/organization and authorization requirements. |
| `.../modules/02-facility-capacity.md` | Structure, physical facts, capacity and configuration hierarchy. |
| `.../modules/03-discovery-visualization.md` | Search/maps, deterministic ranking, live views and fixed 3D assets. |
| `.../modules/04-reservations-allocation.md` | Holds, reservation lifecycle, requested/allocation distinction and conflicts. |
| `.../modules/05-sessions-operations.md` | Arrival/entry/departure, walk-ins, incidents, appeals and emergency actions. |
| `.../modules/06-pricing-payments.md` | Pricing, collection, reconciliation, refunds and invoices. |
| `.../modules/07-monthly-passes.md` | Anchored periods, entitlement/coverage, renewal and cancellation; reference single normative monthly rules. |
| `.../modules/08-notifications-reporting.md` | Delivery outcomes, scoped reports and audit visibility. |
| `.../modules/09-file-media.md` | Upload/access/ownership, sensitivity, retention/holds, deletion and failure acceptance. Provider-specific SDK details live in architecture/design. |
| `.../modules/10-ai-assistance.md` | LPR/chatbot, tool permissions, confirmation, support, evaluation and future boundaries. |
| `.../modules/11-quality-deployment.md` | Local acceptance profile, security, observability, recovery and separately labeled production goals. |
| `docs/01-requirements/business-rules/` | Stable domain rule files and one policy catalogue; retain BR/M identifiers and a single definition per rule. |
| `docs/01-requirements/use-cases/` | Stable domain use cases and a clearly indexed user-story file/subfolder, without needlessly changing top-level categories. |
| `docs/01-requirements/requirements-traceability/` | FR ↔ rule ↔ user story ↔ use case ↔ owning module mapping. |
| `docs/02-architecture/service-boundaries/` | Service/data ownership, command/event catalogue and consistency/failure contracts. |
| `docs/02-architecture/database/` | Canonical schema/migrations, dictionary, constraint coverage and known gaps. Export versus deployable migration explicitly labeled. |
| `docs/02-architecture/architecture-decisions/` | ADRs for B01/B02, storage interface/access, messaging, auth boundary and live updates. |
| `docs/03-design/` | UI, API detail where existing conventions place it, AI model/tool detail and 3D design. Keep API architecture contracts in existing `02-architecture/api/`. |
| `docs/04-testing/requirements-traceability/` | FR ↔ acceptance case ↔ actual test evidence. Separate purpose from requirements traceability. |
| `docs/05-project-management/decision-log/` | Decision record: proposal, selected alternative, approver/date, affected IDs, superseded choice and follow-up owner. |
| `docs/01-requirements/srs/baselines/v1.0/manifest.md` | Baseline module paths, document revisions, immutable commit/tag, file hashes, approval/status and referenced BR/UC/schema/ADR versions. |

Stable paths point to evolving active modules; the immutable manifest pins the exact v1.0 contents by commit and hash. A manifest that only links to current `develop` files is not a preserved baseline. Retain v0.9/v0.8.5 documents and their historical links; do not rename them into a fictional v1.0 history. Archive/move only with redirects or updated historical indexes.

Current index cleanup: repository documentation indexes still describe older SRS versions; business-rule navigation is incomplete; the user-story file is under `use-cases` while several SRS links point elsewhere. Correct the indexes and relative links when making the approved split. Preserve original FR/BR/US/UC IDs and add backlinks rather than renumbering for chapter aesthetics.

### Closure criteria before calling the documents v1.0

1. Retain SB-01, the ownership-guide refinements and §1.2 confirmations. Resolve or explicitly defer the remaining §12 choices; every Critical active-scope ambiguity needs a normative result and example.
2. Give each deferred item a status: resolved decision, retained implementation task with approved behavior, or explicitly future/out-of-scope. Do not mark implementation complete because prose is approved.
3. Agree the monthly schema/entitlement model, monetary arithmetic, evidence ownership and authorization boundaries.
4. Remove contradictions between active SRS, BR, UC, architecture and database dictionary; annotate unimplemented constraints honestly.
5. Apply the approved §16.6–7 profile; declare actual provider/fixture coverage and outstanding execution evidence. Keep real-IoT, offline, personalized AI and product simulator outside MVP unless separately changed.
6. Publish traceability, stable navigation and immutable baseline manifest. Record outstanding implementation risks without leaving contradictory normative rules.

## 11. Evidence references

> [!NOTE]
> **Historical Baseline Reference:** Inputs listed below (e.g. smartpark-srs-v0.9.md, smartpark-business-rules-v0.8.5.md, Solution architecture v0.9) reflect the historical repository inputs audited at the review date (2026-10-13). Active normative requirements and architecture have since progressed to **SRS v1.0** (docs/01-requirements/srs/smartpark-srs-v1.0.md) and **Solution Architecture v1.0** (docs/02-architecture/solution-architecture/smartpark-solution-architecture-v1.0.md). Earlier files are archived in docs/archive/.

Local inputs: `smartpark-srs-v0.9.md` (especially §3.1–3.9, §4, §6.8, Appendix D/E/F); `smartpark-business-rules-v0.8.5.md` (policy register, monthly M-01–09 and retained conflict notes); `smartpark-use-cases-v0.9.md`; `smartpark-user-stories-v0.8.5.md`; supplied SQL/dictionary/DB architecture/README.

Repository references at the reviewed commit:

- [Solution architecture v0.9](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/docs/02-architecture/solution-architecture/SmartPark_Solution_Architecture_v0.9.md)
- [Parking structure service handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/docs/02-architecture/service-boundaries/parking-structure-spark-188.md)
- [Auth design alignment](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/docs/02-architecture/api/auth-design-alignment-2026-10-06.md)
- [Parking persistence repository](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/src/Services/ParkingService/SmartParking.ParkingService.Persistence/PostgresParkingStructureRepository.cs)
- [Operator provisioning repository](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs)
- [Owner registration repository](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs)
- [Parking structure SQL guards](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7d5e2656d286fc4acc0b423cba2efdb464b71b55/scripts/database/05.6-Parking-Structure.sql)

Provider/account features and quantitative acceptance results remain evaluation work. This review does not claim the export is migration-ready, Cloudinary free-account controls were tested, or performance/AI accuracy targets were achieved.

## 12. Remaining choices and baseline closure register

This register distinguishes **OPEN business/scope selection**, **SPECIFY implementation or acceptance detail**, and **FUTURE already deferred**. Sections 5–9 retain all original IDs; recommendations there are not automatically approved merely because the related service boundary or general principle is confirmed. The latest confirmed choices in §16 take precedence over older discussion. Only explicitly adopted examples become defaults; all other proposals retain their status. §15 lists the remaining selections.

### 12.1 Decision disposition — updated 12 October 2026

| ID / related register | Current disposition | Remaining action / limit |
|---|---|---|
| R01 — B03 / M-05 | **RESOLVED:** one final half-up rounding to nearest 1,000 VND; cumulative refund cap. | Implement decimal arithmetic and charge/refund/correction fixtures (§16.3). |
| R02 — B05 / DEF-09 / M-05 | **DEFAULTS RESOLVED:** refund bands and monthly rates in §16.2; available-only product retained. | Temporary/permanent closure treatment is confirmed (§16.9). Apply D02: unspecified ranges locked; facility-failure rate remains 100%. Specify accepted versions and payout contracts. |
| R03 — B04 / DEF-01/24 | **RESOLVED:** original checkout cutoff, scoped Operator correction, Payment execution, Owner escalation, difference-only adjustment. | Implement permission, receipt/invoice adjustment, deduplication and multiple-adjustment reconciliation. Full-refund/recharge excluded. |
| R04 — DEF-07/08/10/11 / DB11/17 | **DEFAULTS RESOLVED:** §16.1/§16.9 include 1-hour allocation, 4-hour protection, 15-minute per-session threshold and booking defaults. | D01/D02 approved as amended (§18.4); implement deposit modes and locked defaults. E01–E03 payout/coverage/onboarding decisions are confirmed in §19; no blanket approval of demo seeds. |
| R05 — DEF-16/20 / FR-RES | **RESOLVED:** timely at/before deadline; no-show only after it without authoritative arrival; offer default 5 min. | Define atomic offer expiry/acceptance and late arrival-evidence reconciliation. Do not reopen refusal/compatibility/entitlement rules. |
| R06 — B08 / DB13/14 | **SCOPE/VALUES RESOLVED:** 15-min access, 7-day absolute refresh, 24-h refresh inactivity; email and phone passwordless OTP for Driver, Operator, Owner and Admin. | Either verified channel independently is confirmed. Complete permission matrix and challenge/recovery contracts; current authorization remains mandatory; TOTP future. |
| R07 — B06/B09 / DEF-23 | **DURATIONS/DIRECTION RESOLVED:** §16.4 financial/chat retention, hold review, access and deletion evidence direction. | Clock anchors confirmed in §16.4; implement provider validation and lifecycle/hold races. |
| R08 — B10 / DEF-27/28 | **PROFILE RESOLVED:** §16.6–7 integration, performance and backup recipe approved. | Fill actual machine/workload details; configure the approved payOS test integration and execute controlled low-value production tests plus fixtures under PAY-01. No provider/result claims without evidence. |
| R09 — AI-DEP-01/04 | **BUSINESS SCOPE RESOLVED:** FAQ/search/authorized reads/preparation; ordinary UI confirmation. | Detailed tool integration/evaluation deferred after v1.0; preserve safety/authority/fallback requirements. |
| R10 — AI-DEP-05/06/07/09 | **DIRECTION RESOLVED / DETAIL DEFERRED:** Gemini/OpenAI, Admin-published versioned corpus, asynchronous support. | Models/routing/data-sharing/evaluation after v1.0. Declare support availability/context; do not promise staffed live chat. |
| R11 — DEF-32/34/35 / AI-DEP-08 | **DIRECTION RESOLVED / DETAIL DEFERRED:** failed/uncertain outcomes, manual review, Operator correction history, no conflict-to-free shortcut. | Model/dataset/score/equality/accuracy/latency and exact integration after v1.0. Simulated core entry/departure contract still needed. |
| R12 — D8 / DEF-30 | **RESOLVED:** scoped SignalR views, reconnect refresh; local healthy-profile freshness p95 ≤2 s. | Subscription authorization/revocation, payloads and measurements; Redis optional. |
| R13 — FR-VEH-02 | **AUTHORITY/EVIDENCE DIRECTION RESOLVED:** duplicate binding rejected; Admin adjudicates; verified contact + relevant registration/transfer evidence and scoped findings. | Notify existing holder; no automatic transfer on silence; unresolved cases pending/escalated. Formalize checklist and audited Admin decision (§16.9). OTP/plate photo alone insufficient. |
| R14 — remaining FR/DEF details | **MOST SCOPE RESOLVED:** layouts, pools, diagnostics, manual location, case states/consequences/report scopes. | Permission/state tables, invoice subjects, categories and remaining proposed FR clauses need explicit disposition. |

### 12.2 Design/implementation work that does not reopen confirmed product choices

- **Operations concurrency and temporal claims:** choose transaction/locking strategy and demonstrate no incompatible double commitment. Site-level serialization is a recommended local starting point, not a mandated engine design. Available-only monthly passes add no capacity hold; ordinary reservations, backup/protection and visits still need the ledger.
- **State and recovery contracts:** enumerate transitions/actors/guards/idempotency, event schemas, correlation, unknown outcomes, retry/error queues and reconciliation operators. RabbitMQ/MassTransit selection does not complete this work. Include checkout attempts and monthly money-without-entitlement recovery.
- **Facility-change coordination:** implement pending restriction/commit/recovery; record emergency physical restrictions immediately. Define behavior during communication loss without pretending emergency facts can roll back.
- **Policy preservation:** link immutable operational and financial terms, record effective times/timezone and distinguish accepted snapshots from current search quotes. Do not rewrite accepted transactions on policy updates.
- **Observation/manual verification:** define trusted timestamps, ordering, stale/UNKNOWN handling and audit. Operations owns accepted truth, IoT raw observations; no timeout-to-free shortcut.
- **Acceptance specification:** document machine, build, seeded data/image sizes, warm-up, steady/burst duration, endpoint/error mix and provider exclusions. The full §16.7 recipe is approved but not tested; the ten-minute steady run is now confirmed. Endpoint proportions and actual resource/data measurements are still test-plan work.
- **Schema/code alignment:** historical DB01–DB24 findings remain a checklist against the reviewed snapshot, not a claim about current develop. Follow the supplied `smartpark-microservice-data-ownership-v1.0(1).md` for confirmed owners and newer schema context; limited pinned static observations from this conversation are recorded in §17; this edit does not execute or re-audit the repository.
- **Document propagation:** update active SRS/BR/UC/FR and tests, preserve IDs and historical sources, finalize stable paths/manifest, then record each item as resolved, implementation pending or explicitly future. A finalized review is not itself an SRS release.

### 12.3 Already deferred / not required to reopen now

TOTP enrollment/challenges/recovery; advanced knowledge-publication lifecycle and Owner content-submission workflow; Kafka; Redis without an approved use; real IoT/gate hardware; offline authorization/replay; personalized AI ranking, AI camera occupancy, forecasting and anomaly detection; multi-frame violation AI; product simulator; production clustering/HA targets. Guaranteed monthly capacity/slots are unselected future alternatives, not v1.0 obligations. Cloudinary evaluation with portable ownership/access remains selected; R2 is the future direction, not a parallel provider implementation.

## 13. Revision change log

### 8 October 2026 — prior review

- Incorporated detailed service ownership from the supplied guide, including occupancy/credentials, monthly plan definitions, Search, Assistant/conversations, Support and reports.
- Replaced open B03 arithmetic with actual overlap, threshold exemption and final nearest-VND rounding; retained only half-tie detail as open.
- Replaced prior physical-exit repricing recommendation with confirmed checkout cutoff and audited abort/recovery.
- Selected when-space-available monthly fee coverage only and removed guaranteed monthly inventory, permanent slot activation and compensation proposals from active scope.
- Confirmed monthly recoverable fulfillment and unused-period refund formula while keeping rates open.
- Confirmed planned/emergency facility-change handling, immutable accepted policy versions, authentication direction and RabbitMQ/MassTransit.
- Confirmed restricted-evidence proxy/acknowledged holds and core local acceptance targets without claiming tests or provider evaluation were completed.
- Recorded accepted refusal/early-entry/reallocation-count/tie-break/violation/emergency directions; kept detailed permissions/parameters explicit.
- Preserved original DB, tester, architecture, AI and functional identifiers; added a remaining-choice register rather than erasing unresolved work or marking all proposals approved.


### 9 October 2026 — consolidated user confirmations

- Replaced TOTP-in-MVP wording with explicit future scope; recorded Cloudflare Turnstile and retained current authorization requirements.
- Replaced paid-session reopening with mispricing adjustment and genuine exit/re-entry for a new stay.
- Recorded individually represented spaces/category-specific pools, three configurable 3D template types, occupied-backup single deduction, Owner read-only diagnostics and manual location fallback.
- Recorded Owner-configurable versioned refunds, ordinary appeal versus escalation/monthly authority without a monetary threshold, ordinary day/night monthly coverage and removal of MONTHLY_SALES_LIMIT.
- Added exact global cancellation restriction defaults, trigger/counting semantics, daily timezone, later-expiry composition and subsequent-event extension; retained suspicious-history alerts.
- Recorded Admin plate-dispute adjudication separately from assigned-Operator recognition/session correction; preserved historical associations.
- Incorporated accepted media limits/access/holds/failure recommendations, AI/tool/provider/publication/ticket scope, SignalR views, case states, consequences and report access.
- Reclassified R01–R14 instead of treating resolved scope as still open. All original DB/FR/tester/architecture/AI identifiers remain present. No code or original SRS/BR files were changed; implementation is not claimed complete.

### 12 October 2026 — accepted defaults, local profile and remaining questions

- Adopted final nearest-1,000-VND half-up arithmetic and capped refunds; updated stale active rounding summaries.
- Adopted one-hour allocation, four-hour protection, five-minute offers, timely-at-deadline arrival and stated monthly defaults.
- Adopted refund examples as defaults, original-cutoff Operator pricing correction, difference-only adjustment and no full-refund/recharge route.
- Adopted financial/chat retention, hold review, evidence direction and deletion-proof direction while identifying clock/sufficiency details.
- Adopted all-role email/phone passwordless OTP login and token values; retained TOTP deferral and local SMS-fixture distinction.
- Adopted concrete local integration matrix, performance recipe and backup/recovery objectives; no tests/integrations claimed complete.
- Deferred detailed LPR/Assistant integration/evaluation after v1.0 while preserving confirmed general direction and authority boundaries.
- Replaced stale §15 choices with Q01–Q07 and ordered work; daily-free aggregation remains explicitly unapproved. Preserved historical findings, source IDs and unrelated decisions. Only this review was edited.

### 12 October 2026 — subsequent explicit user decisions

- Superseded daily aggregation with the 15-minute per-session threshold; retained full-duration charging above the inclusive threshold.
- Approved FULL/PERCENTAGE/FIXED Owner prepayment modes, default FULL; left numerical validation and settlement edge cases explicitly open as D01.
- Adopted reservation defaults 15 minutes / 24 hours / 7 days / zero buffer / unlimited active bookings by default.
- Closed Q04–Q07: temporary/permanent closure, either verified OTP channel, retention anchors and Admin-reviewed binding disputes without automatic transfer on silence.
- Added §18 with an actionable policy catalogue, scoped permission matrix, transition/race specification direction and six focused remaining decision points. Technical recommendations are labelled separately from approvals.
- Preserved historical revision entries, register identifiers and pinned repository findings. No SRS, schema, application or repository changes performed.

### 12 October 2026 — D01–D06 approval and uncapped fixed-deposit amendment

- Adopted percentage 0<p≤100%, positive fixed deposit without policy upper cap, booked-duration minimum even for short reserved visits, single prepayment credit, collected-money cancellation/no-show refunds and no unpaid-balance penalty.
- Adopted locked unspecified settings, lot-scoped active cap counting pending-payment/confirmed/allocated, and failure refund 100% eligible unused value.
- Adopted Admin Owner-registration approval, Owner scoped eligible-Operator assignment/revocation and Operator audited emergency/unpaid recovery. Did not infer unrestricted Admin role/secret administration.
- Adopted whole-session monthly-overlap metric, strict offer expiry and reviewed delayed-arrival recovery.
- Synchronized §§12/15/16/18 and added §19 residual edge cases and implementation-ready contracts. Existing IDs and historical findings preserved; no companion source or code edits.

### 12 October 2026 — E01–E03 closure and Operator account creation

- Confirmed automatic settlement-surplus refund initiation; commercial checkout may complete while refund payout remains separately pending.
- Confirmed Owner creates Operator accounts and assigns/revokes own-organization/owned-lot access; no Admin approval per hire. Preserved contact verification and current authorization.
- Confirmed monthly coverage precedes booked-minimum calculation and genuinely zero-price reservations confirm without monetary payment under normal capacity/eligibility guards.
- Updated stale no-prepayment wording, permission matrix, state contracts and remaining-choice register; added §20 PM closure assessment. No companion documents/code changed and no implementation/testing certification inferred.

### 12 October 2026 — reconciliation of reattached 9 October copy

The attachment named `smartpark-v1-baseline-decision-review-2026-10-09(4).md` contained the earlier 9 October text ending at §15, despite its newer filename. Reconciled that source with the explicit approvals already given in this conversation; retained prior identifiers, findings and historical decisions while superseding stale open labels/values. Incorporated the repeated E01–E03 approval and the explicit requirement that Owner creates and assigns Operator accounts. §§16–20 consolidate those approvals and distinguish remaining specification/release work. No fresh repository audit or companion-document edit was performed.

## 14. Detailed confirmed contracts — synchronized through 12 October 2026

This section retains the 9 October decisions, including restriction expiry, and synchronizes affected statements with the 12 October approvals in §16. Unselected refinements remain recommended or OPEN.

### 14.1 Global cancellation abuse and booking restrictions

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

**Recommended ownership implementation:** Operations owns cancellation facts and atomic booking eligibility/restriction enforcement; Admin configures global policy through authorized Operations APIs. Notification & Audit projects/delivers alerts and audit history. Keep enforcement authoritative in Operations even if an alert projection is delayed. User & Access owns actual authentication and current grants; do not misuse account-disabled for this restriction. UI shows the reason and effective unlock time.

### 14.2 Pricing correction, refunds and monthly coverage

- Accepted checkout freezes billable end; payment completes commercial checkout; physical exit remains separate.
- Before payment, approved audited recovery can invalidate checkout/unfreeze billable end on the same session. Preserve attempts and no new grace period.
- After payment, correction is for system pricing miscalculation only. Correct charge lower than paid: refund the difference without another payment. Higher: collect the difference. Equal: no adjustment. Keep original successful payment, old/new calculation, reason, actor, linked adjustment/refund and invoice history.
- A paid checkout cannot be reopened simply to allow a longer stay. Driver must physically leave and re-enter for a new session; no synthetic exit/entry. The former 10:00 paid → 10:05 abort → bill original visit through 10:30 example is superseded.
- **CONFIRMED 12 October:** scoped assigned Operator handles audited mispricing correction at the original accepted checkout cutoff; Payment & Pricing calculates/executes the difference and Owner handles escalations. No full-refund/full-recharge route in MVP (§16.3).
- Owner publishes versioned refund rates by reason for their own lot within Admin bounds. Ordinary appeals handled by Operator; Owner handles escalations and monthly cancellation. No monetary escalation threshold in MVP.
- Monthly pass covers ordinary daytime/nighttime parking fees only, not charging or other service fees. When-space-available only; no capacity protection, permanent slot or MONTHLY_SALES_LIMIT. Ordinary fullness does not automatically create compensation. Facility closure remains a separate service-failure case.
- Published ordinary nighttime prices depend on category, location and time regardless of account/guest status. Supersedes guest-only overnight surcharge wording. Actual overlap remains; one final half-up rounding to nearest 1,000 VND now applies (§16.3).

### 14.3 Facility, allocation and plate boundaries

**Capacity/layout:** support individually represented spaces and category-specific pools. A car unit and motorcycle unit are not interchangeable generic units. Specific-slot/zone/capacity reservation products remain unchanged; a capacity reservation may be allocated from individually represented spaces. Automatic mixed-category area conversion has not been selected for MVP.

Three 3D model/template types: outdoor lot, multi-floor building lot and multi-floor basement. Owner configures floors, zones and rows; outdoor lots have no artificial floor. Fixed model assets remain versioned deployment assets; facility configuration controls instances/layout within supported templates.

An occupied backup slot is deducted once (occupied takes precedence), while its backup designation/history remains. Example: 10 slots, 3 occupied, 2 backup of which 1 occupied → 4 distinct unavailable slots before other claims, leaving 6. Do not infer a physically occupied backup can satisfy another reservation.

Late tolerance is optional. When enabled, Owner selects 15–30 minutes, default 15. Disabled retains baseline reservation validity until end; no zero-minute value substitutes for disabled. Arrival at or before the cutoff is timely; no-show only after the cutoff without authoritative timely arrival. Alternative must meet mandatory vehicle/accessibility/explicitly promised facility constraints; exact preferred slot is not guaranteed. Offer expiry releases its temporary alternative claim, not automatically the underlying entitlement. Offer duration defaults to 5 minutes; acceptance/expiry concurrency remains design work.

Owner receives read-only diagnostics at owned lots; assigned Operator retains lot-device management. This amends deferred Owner reporting scope without enabling real IoT. Manual location selection is supported when GPS is denied/unavailable.

**Plate correction:** assigned Operator may correct recognition/session identification with reason, actor, original/corrected values and history. IoT & Recognition retains original recognition/evidence; Operations owns accepted visit identification. This does not automatically mutate global account/vehicle binding.

Duplicate global binding is rejected without exposing another account's personal information. Driver may submit a dispute; Operator verifies local incident and supplies findings. Admin adjudicates already-bound/ownership-change disputes with evidence through a minimal reviewed support case. N owns support-case coordination; U owns binding adjudication facts/guarded mutation; operational evidence stays with its owner. Historical visits/invoices/evidence keep original associations. Accepted evidence direction now includes verified contact and relevant vehicle-registration/transfer evidence in existing permitted image formats, with scoped Operator findings (§16.4). Notify the existing holder; never transfer automatically for nonresponse. Admin records evidence sufficiency and reasons; unresolved cases remain pending/escalated (§16.9). No routine national-ID collection is approved.

### 14.4 Accepted media access, holds and upload limits

Public only when facility photos/diagrams are explicitly published for public listing. LPR, vehicle/profile images, incident/appeal evidence and financial documents are restricted. Use backend uploads, service-owned stable asset references and authenticated proxy for evidence.

| Purpose | Accepted MVP types | Accepted limit |
|---|---|---|
| Facility photos | JPEG, PNG, WebP | 5 MiB each; 10 per lot |
| Avatar / vehicle image | JPEG, PNG, WebP | 5 MiB each |
| LPR / incident / appeal image | JPEG, PNG | 10 MiB each; 5 per submission |
| User video / SVG / HTML / archive | Not accepted in initial upload scope | No general upload endpoint for these types |
| Generated invoice PDF | Server-generated | Separate financial-document lifecycle; not arbitrary user PDF upload |

Validate content, signature/decoding and size, not client filename/MIME alone. Additional scanned-document/PDF evidence requires explicit accepted scope. No upload of the three fixed 3D deployment assets through this media flow.

Scoped Operator may request a hold for handled incident/appeal; Owner for escalated/monthly case; Admin for authorized platform investigation. Asset owner acknowledges before another service claims preservation. Authorized handler releases only the originating hold when its need ends; other active holds remain. Audit place/release and coordinate deletion races.

Missing required evidence keeps evidence-dependent decisions pending; a case can exist with EVIDENCE_PENDING. Approved emergency operations may proceed with an audited missing-evidence incident. Never display failed upload as successfully attached.

Accepted recommended operational target: provider deletion confirmation within 24 hours after deletion becomes due, with retry/alert if overdue. This does **not** extend a hard maximum-retention deadline: schedule provider work accordingly, restrict ordinary delivery when due/no hold, and record unresolved failures honestly. Confirm originals/derived delivery copies according to evaluated provider capabilities. Financial documents: 7 years from issuance, subject to holds; conversations: 90 days from last message, unaffected by administrative reads; hold review every 30 days and at case closure (§16.4). C-20 categories otherwise retained.

### 14.5 Authentication, AI and support

**Security:** Cloudflare Turnstile selected; password and verified registration/contact/reset flows retained; current authorization enforced on protected operations. **TOTP, including enrollment/challenges/recovery, is FUTURE and not required for MVP acceptance.** No role may require a deferred TOTP flow as a current gate. Confirmed token values: 15-minute access / 7-day absolute refresh session / 24-hour refresh inactivity. Separate passwordless OTP login is approved for all roles via email and phone (§16.5); Turnstile is not a substitute for authorization or rate limiting.

**Assistant:** FAQ, search, authorized reads and reservation form preparation. Final commands require ordinary UI confirmation and authorized domain APIs. Cancellation/refund/security requests route to their normal screens; no autonomous financial/security writes. Gemini and OpenAI are provider directions; exact models/routing/budgets/data-sharing and evaluation are later design details. Detailed chatbot document need not be fully completed for SRS v1.0, but scope/authority/error fallback must be explicit.

**Knowledge:** Admin-only publication authority. Admin/dev team configures a simple versioned approved corpus; no new Developer business role, advanced CMS or mandatory Draft/Approved/Superseded workflow. Live prices/availability/accepted terms come from authoritative APIs. Owner content can be supplied to site Admin outside the MVP submission workflow; no Owner publication permission is granted. Exclude unresolved proposals and stale historical terms.

**Support:** asynchronous tickets, not staffed live chat. Lot matters routed to assigned Operator; platform/account/global plate disputes to Admin. Scoped context references only; no whole-conversation duplication in Audit. Hours/response targets and allowed contextual fields remain to specify; no implied 24/7 response.

**LPR:** explicit unsuccessful outcomes and manual review at uncertain boundary. Conflicting observations never automatically free an occupied slot. Exact recognizer/dataset/confidence scale/equality, integration and accuracy/latency evaluation are explicitly deferred after v1.0 (§16.8).

### 14.6 SignalR scope

Confirmed live views: Operator occupancy/gate and parking slots; 3D; Admin user management/approval; notification bell. Clients fetch current authorized state after reconnect. Missed messages never change authoritative capacity or business rights. Implement scoped subscriptions/revocation and minimal payloads; no cross-lot plate/evidence broadcasting. SignalR handles client updates, RabbitMQ durable inter-service outcomes. Redis scale-out remains optional, not prerequisite for single-instance local MVP. Approved healthy-local-profile freshness target: p95 ≤2 seconds (§16.7); measurement and recovery contracts remain to implement.

### 14.7 Cases, consequences and report access

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

## 15. Work remaining to publish SRS v1.0 — current actionable register

Approval of this review does not certify implementation. Close business ambiguity before freezing normative requirements; track schema/API/code/test delivery separately. The user approved the specific recommendations listed in §16, **not every earlier recommendation in §5–9**.

### 15.1 Disposition of clarification questions — latest user amendment

| ID | Current status | Decision / remaining boundary |
|---|---|---|
| Q01 | **RESOLVED — amended** | 15 minutes per session; at/below threshold free, above threshold full applicable duration chargeable. No daily aggregation. D04 confirms whole-session duration; bill only uncovered applicable intervals. D01 preserves the disclosed reservation minimum even for a short visit. |
| Q02 | **MODES/DEFAULT RESOLVED** | Owner may choose FULL, PERCENTAGE or FIXED prepayment. Default FULL reservation-duration price. Credit collected money at settlement; separately disclose additional usage. The old 30% example is not a default. D01 confirmed: 0<p≤100%; fixed deposit positive with no policy upper cap; reserved visit owes booked minimum plus eligible extras; cancellation/no-show uses collected money only. Excess payout handling is confirmed in §19 E01; pending refund does not block covered commercial checkout. |
| Q03 | **LISTED DEFAULTS RESOLVED** | Minimum 15 minutes; maximum 24 hours; advance horizon 7 days; buffer 0; no maximum active bookings/account by default. D02 confirmed: unspecified ranges locked at approved defaults; optional cap lot-scoped, counting pending-payment/confirmed/allocated; no default limit of 2. |
| Q04 | **RESOLVED** | Temporary closure: retain pass and refund verified unavailable intervals once. Permanent closure: cancel remaining coverage and refund eligible unused value. No automatic extension. |
| Q05 | **RESOLVED** | Either verified bound email or phone independently for every role; no dual OTP requirement; missing/unverified channels unusable. Phone acceptance uses SMS fixtures until separately integrated. |
| Q06 | **RESOLVED** | Conversations 90 days from last message, unchanged by administrative reads; financial documents 7 years from issuance, subject to holds. |
| Q07 | **RESOLVED** | Admin reviews verified contact, relevant registration/transfer evidence and Operator findings; notify existing holder; silence never triggers automatic transfer. Unresolved cases pending/escalated. |

**D01–D06 and E01–E03 are approved as amended in §§18.4/19.1.** Owner creates and assigns Operator accounts. No further selection is needed for those questions. §20 lists remaining release-management actions and specification work; approved decisions are not evidence of completed implementation.

Support remains asynchronous. **Recommended declaration to confirm in publication:** handled during stated project/demo availability, no guaranteed production response SLA; do not invent business hours. Collect only selected necessary context, not an unrestricted conversation export. Exact hours can be supplied by the team without expanding scope.

### 15.2 Specification and implementation tasks — do not reopen settled policy

| Priority / task | Direction and completion evidence | Related registers |
|---|---|---|
| P0 — Policy and arithmetic | Apply resolved Q01–Q04; apply confirmed D01/D02/D04 and E01/E03 contracts; catalogue defaults/bounds/owner/version; fixtures for per-session exemption, midnight, monthly overlap, 2,499.50/2,500 rounding, repeated refunds/corrections. | R01–04; TESTER-DEF-07–11/25; DB11/12/21 |
| P0 — Permission and identity | Publish action/resource/role/delegator matrix; define OTP login/channel/recovery and current grant revocation. Account block, booking restriction and challenge lock stay distinct. | R06/13/14; TESTER-DEF-21–24; ARCH-DEF-07; DB13–15 |
| P0 — Capacity and state | Publish reservation/allocation/arrival/session/checkout/payment/refund/case transitions with actor, prior version, guards, atomic effects and retries. Operations owns compatible temporal claims; occupied backup counted once; no timeout-to-free. | TESTER-DEF-01–06/12–20/34–35; ARCH-DEF-05/09; DB05–10/17 |
| P0 — Money and monthly | Define financial order/transaction/entitlement states, strict monthly A<H, accepted versions/anchors, prepayment credit, refunds/adjustment ledger, eligible remaining balance and invoices without fake sessions. Preserve original success; reconcile UNKNOWN. | R02/03; M-01–09; DB03/21/22 |
| P0 — Cross-service recovery | One workflow coordinator; immediate authenticated API decisions; local outbox/inbox with RabbitMQ/MassTransit outcomes, idempotency, late/out-of-order outcomes, timeouts and reconciliation. Planned Facility restrictions pending; emergency physical facts immediate with operational safety response. | ARCH-DEF-01/05/08/09; DB01/02/24 |
| P1 — Evidence/media | Implement owner metadata, backend validation, proxy access, holds/acknowledgement/release, pending evidence, lifecycle/deletion races and provider proof. Evaluate Cloudinary capabilities; no assumption of free-tier sufficiency. | R07/13; TESTER-DEF-23/31; DB18/19/24 |
| P1 — Live views/reporting | Scope subscriptions/downloads/reports, revoke access, recover by current-state fetch; measure p95 ≤2 s in local healthy profile. Each service owns its reports. | R12/14; TESTER-DEF-30/31; DB23 |
| P1 — Schema/code alignment | Reassess DB01–DB24 against pinned branches (§17); align ownership, plate uniqueness, available-only monthly model, category compatibility, financial constraints, migrations and dictionaries. SQL enum checks are not transition proof; comments are not RLS implementation. | DB01–DB24; ARCH-DEF-01–09 |
| P1 — Acceptance execution | Complete exact endpoint proportions/data/image composition and hardware manifest; run §16.6–7 tests and recovery drills; label fixtures, real sandboxes, failures and unexecuted tests. Never publish targets as measured results. | TESTER-DEF-27–31/33/35; R08/12 |
| P1 — Propagation and traceability | Update active SRS/BR/UC/stories, FR statuses, architecture, agent guidance and QA; preserve IDs/history. Clause-by-clause disposition of §9 proposals is required: no blanket promotion of A/X/F. Fix stale Owner-diagnostics, checkout, monthly-guarantee, rounding, Kafka and OTP wording; verify bidirectional FR/US/UC links. | All registers; existing source traceability defects |
| P2 — Detailed AI integration | Separate follow-up backlog for tool schemas, recognizer/models, dataset/thresholds, provider data-sharing, budgets, routing and evaluation. Keep general authority/fallback requirements in v1.0. | AI-DEP-01–10; TESTER-DEF-32 and detailed parts of 34/35 |

Design recommendations, not newly mandated engines: start local capacity enforcement with a simple transactional serialization/locking strategy under Operations; use an authoritative temporal claim model for pooled capacity rather than independent scalar counters. Review throughput only against the actual accepted workload. Map retained DB04/06/20 findings to executable migration/authorization/transition/delete tests, not documentation assurances.

### 15.3 Publication gates and order

1. Apply approved D01–D06 and E01–E03; approve or explicitly constrain required settings before they can be offered to users.
2. Complete normative permission/state/policy contracts and reconcile active source contradictions; preserve all 35 TESTER-DEF, 9 ARCH-DEF, 10 AI-DEP and 24 DB references.
3. Propagate amendments into modular SRS, BR, UC, story/FR traceability and acceptance specifications, retaining existing document categories and stable module paths.
4. Publish an immutable v1.0 requirements manifest with revision/hash references and approval status; retain old v0.9/v0.8.5 history. A reviewed requirements baseline may be approved while declared implementation tasks remain pending.
5. Separately report implementation readiness, migration execution, actual provider coverage and measured acceptance results. No source-code or database changes are part of this review edit.

### 15.4 Already deferred / outside current MVP

TOTP; advanced knowledge publishing/Owner submission portal; guaranteed monthly capacity/slots and monthly queue activation; monthly transfer/suspend-and-add-days; general monetary escalation threshold; automatic mixed-category capacity conversion; real hardware/gate integration; offline authorization/replay; personalized/occupancy/forecast/anomaly AI and multi-frame violation detection; product simulator; production clustering/HA; Redis without a concrete approved need; Kafka; R2 migration. Detailed LPR/Assistant design, integration and evaluation are deferred **after the v1.0 requirements baseline**, without deleting their general direction or representing them as implemented.

## 16. Approved amendments — 12 October 2026

### 16.0 Authority, scope and interpretation

Source: the user’s explicit messages in this conversation. Earlier message approves final nearest-1,000 half-up rounding, free-threshold principle, one-hour allocation, deadline equality, evidence/local/backup directions and LPR/Assistant deferral. Latest message explicitly accepts the proposed integration matrix, performance recipe, monthly defaults, refund examples, pricing correction, evidence/retention and **passwordless login for all roles/email and phone**.

The latest follow-up explicitly restores the **per-session** free threshold, approves Owner-selectable prepayment modes with FULL as default, booking defaults, closure treatment, either-channel OTP, retention anchors and reviewed binding-dispute handling (§16.9). It does not approve every unrelated demo seed, every technical recommendation, or measured provider/performance success. D01–D06 were subsequently approved with explicit amendments (§18.4), especially uncapped fixed deposits and the narrower D03 Admin authority. Technical recommendations remain design proposals; §19 now records approved E01–E03, including Owner-created Operator accounts; §20 tracks release-management closure. This section takes precedence over historical contrary wording; §13 retains chronological history.

### 16.1 Approved timing and monthly defaults

| Setting | Approved value / direction | Scope and qualification |
|---|---|---|
| Monetary unit / rounding | Nearest 1,000 VND, half up once after summation | Charges and refund magnitudes; §16.3 safeguards. |
| Free threshold | 15 minutes per session, inclusive boundary | Full applicable duration chargeable once exceeded; no daily aggregation or deduction. §16.9. |
| Allocation lead time | 1 hour | Allocation time = start − lead; replaces earlier 30-minute example. |
| Reservation protection window | 4 hours | Adopted timing example; separate from allocation and payment hold. |
| Ordinary reservation payment hold | 5 minutes | Existing approved default; capacity compatibility enforcement retained. |
| Alternative-offer duration | 5 minutes | Expiry releases temporary offer claim, not automatic loss of underlying entitlement. |
| Arrival equality | At or before deadline is timely | NO_SHOW only strictly after applicable deadline without authoritative timely arrival; optional late policy semantics unchanged. |
| Enabled late tolerance | 15 min default, Owner selection 15–30 min | Disabled means reservation valid until its end, not zero-minute tolerance. |
| Occupancy projection horizon | 4 hours | Projection only; never proof of departure or automatic release. |
| Payment retries | 3 after initial attempt (4 total) | Retryable failure only; UNKNOWN requires lookup/reconciliation. |
| Driver reallocation requests | Default 2 accepted submissions | Retries/invalid requests excluded; system recovery does not consume allowance. |
| Monthly payment hold | 15 minutes | Order/payment deadline, not parking-capacity hold for available-only passes. |
| Monthly renewal window | 7 days before final committed expiry | Consecutive renewal appends to final committed E. |
| Monthly expiry reminders | 7 days and 1 day before expiry | Delivery/retry semantics still specified by notification contract. |
| Maximum prepaid monthly terms | 3 consecutive calendar periods | Original calendar anchor and [S,E) preserved; precise limit validation is contract work. |
| Monthly price | Owner-configured | 1,200,000 VND remains fixture-only, not a universal approved price. |

Monthly product remains **when-space-available only**. No sales cap, guarantee, permanent slot, occupancy deduction while outside, automatic monthly charge, or next-period debt. Start-now S=A; future S local midnight; strict activation A<H including verified settlement and entitlement commit. Exactly H is too late. Late repurchase has new non-retroactive start/anchor. Pass expiry does not release actual occupancy. Covered ordinary day/night fees are not charged again; separate services remain separate.

### 16.2 Approved refund defaults

Defaults are versioned per applicable policy; Owner configurability remains within Admin bounds. Approval establishes these exact initial examples as defaults, not unrestricted additional fees or monetary approval thresholds.

| Reason | Approved default | Boundary / base |
|---|---|---|
| Ordinary voluntary cancellation ≥30 min before start | 100% | Eligible collected reservation money under accepted terms. |
| Ordinary voluntary cancellation ≥15 and <30 min before start | 50% | Exact 15 min belongs here; exact 30 min belongs to 100% band. |
| Ordinary voluntary cancellation <15 min before start | 0% | Only when cancellation remains permitted by lifecycle; this band does not authorize cancelling a parked visit. |
| No-show | 0% | Applicable enabled policy and authoritative arrival rules still govern classification. |
| Monthly voluntary cancellation before S | 100% | Eligible prepaid amount for not-started period. |
| Monthly voluntary cancellation during [S,E) | 50% of unused value | P × ((E−C)/(E−S)) × 0.5, actual timestamps; no fixed 30-day approximation. |
| Facility failure | 100% of eligible unused value | No voluntary penalty; ordinary fullness is not failure of an available-only capacity guarantee. Temporary closure retains pass with once-only verified unavailable-interval refund; permanent closure cancels remaining coverage (§16.9). |
| Pricing miscalculation | Correct overcollection / collect shortfall | Not reduced by voluntary-cancellation rate. |

At/after period E, unused-period value is zero. For multiple paid periods, calculate per-period eligible amount using accepted rates, sum unrounded values, round once half up to nearest 1,000 VND, then apply the remaining eligible-collected-money cap. No overlapping/double refund, no automatic offset against unrelated debt, no physical occupancy release merely because a refund/entitlement cancellation was recorded. Operator handles ordinary appeals; Owner escalations/monthly cancellation; Payment executes money. Accepted appeal/approved refund does not mean payout completed.

### 16.3 Monetary calculation and pricing corrections

For a nonnegative total x, conceptual rule: `rounded = 1000 × floor(x / 1000 + 0.5)`. Use exact decimal calculation; do not pre-round segments or convert negative accounting signs into a separate unapproved convention. Refunds are computed as nonnegative magnitudes with ledger direction represented separately.

| Unrounded amount | Final rounded amount |
|---|---|
| 2,412 VND | 2,000 VND |
| 2,499.50 VND | 2,000 VND |
| 2,500 VND | 3,000 VND |

The initial 2,499.50 → 3,000 example is explicitly superseded by the user’s later “2,500 boundary” clarification. Do not first round to whole VND and then to thousands.

Assigned, authorized Operator initiates/completes an audited correction **only for system pricing miscalculation**. Recompute at original accepted checkout cutoff; correction delay is not billed. Payment & Pricing calculates and executes the adjustment; Owner handles escalations. Compare corrected rounded charge with actual attributable money already collected, preserving previous refunds/adjustments in reconciliation; no second blind correction against an already-refunded original total. Positive shortfall is collected, excess refunded, equal amounts need no adjustment. Persist old/new calculation, accepted terms, reason, actor, transaction and invoice links. Exact multi-adjustment ledger contract remains implementation work.

No full-refund/full-recharge route in MVP. No reopening a paid visit for continued parking: actual exit and re-entry are required for a new session. Audited unpaid checkout invalidation remains supported, without a new grace period. Physical departure and capacity release remain separate from payment success/gate opening. Cumulative refunds cannot exceed eligible collected money; the cap has precedence over rounding if legacy/provider balances are not multiples of 1,000.

### 16.4 Evidence, retention and holds

| Data / action | Approved policy | Remaining precise specification |
|---|---|---|
| Images | Existing maximum 30 days, subject to authorized holds | Preserve existing class/scope; do not silently expand retention because an image is attached to a financial record. |
| Audit logs | Existing 7 years | Existing audit scope and protected access retained. |
| Operational logs | Existing 90 days hot + 1 year cold | Preserve class distinction and existing lifecycle; production duration cannot be “tested” by a short demo. |
| Financial documents | 7 years from issuance, subject to holds | Project-policy approval, not a conclusion about applicable law. Preserve issuance/version/adjustment links. |
| Assistant conversations | 90 days from the last message | Administrative reads do not reset the clock; necessary case evidence follows its own owner/class/hold, not indiscriminate full-chat copying. |
| Hold review | Every 30 days and at case closure | Review is not auto-release. Retain other active holds when one originating hold is released; overdue reviews alert authorized handler. |
| Deletion proof | Provider outcome plus asset/version/time, retries and capability-appropriate verification, including derived copies | Evaluate actual provider capabilities; record unresolved failure honestly. |
| Deletion operational target | Existing 24-hour confirmation target | Does not extend any hard retention deadline; start work accordingly and restrict ordinary access when due without hold. |
| Plate-binding evidence | Verified contact, relevant registration/transfer evidence where applicable, scoped Operator findings, Admin decision | OTP/plate photo alone insufficient. Existing accepted image types/limits; no routine national-ID collection or general PDF upload. Admin reviews sufficiency, notifies existing holder and records decision; no transfer on silence; unresolved cases pending/escalated. |

Preserve §14.4 upload limits, backend content validation, restricted proxy delivery, service-owned stable references and hold acknowledgement. Missing evidence keeps evidence-dependent action pending; approved emergency action can proceed with an audited missing-evidence incident. No blanket storage-outage free-exit or automatic binding-transfer policy.

### 16.5 Authentication — password and all-role passwordless OTP

- Access token: **15 minutes**. Refresh session: **7-day absolute lifetime**, with **24-hour refresh inactivity limit**. Rotation must not silently extend the absolute session maximum. Enforcement contracts must define current authorization and expiry boundaries.
- **Driver, Operator, Owner and Admin may use passwordless OTP through email and phone.** The staff-password-only recommendation was not selected. Existing password login and registration/contact/reset verification remain.
- The login feature does not create an account, approve an Owner, grant roles, bypass account/permission state, or merge two accounts because contact input matches loosely. Verified contact binding and challenge purposes must remain explicit.
- **Either verified bound channel independently is approved for every role.** Do not require both OTPs for one login. Missing/unverified channels cannot be used; neither channel implicitly creates or merges an account.
- TOTP enrollment/challenge/recovery remains FUTURE. Turnstile is selected and does not replace authorization, challenge controls or rate limiting.
- Local acceptance uses email capture and phone/SMS fixtures under §16.6. Both login paths must be tested end-to-end in that declared mode for every role. This proves application flow, not real email/SMS delivery; a real SMS provider is a separate integration decision/test.

### 16.6 Approved local acceptance and integration matrix

| Integration | Approved local profile | Evidence / limit |
|---|---|---|
| Payments | **payOS QR first:** controlled, low-value production integration tests plus deterministic failure/race fixtures; tested Operator cash collection | PAY-01 replaces the one-real-sandbox gate. VNPay/MoMo/ZaloPay retain separate planned identities and honest integration/fixture/deferred labels. Native VNPay may be simulated/deferred if resources are insufficient. Fixtures do not prove real collection/refund delivery; no claim that payOS implements all three providers. |
| Media | Evaluate Cloudinary adapter for backend upload, restricted reads, holds, deletion and recovery; fixtures for deterministic failures | Provider capability and account limits unverified until tested. No production/free-tier suitability claim. |
| Notifications | Local email capture and SMS fixtures; real delivery tests separately labelled if configured | No proof of external delivery from local capture. Applies to all-role OTP flows. |
| Hardware/gate | Deterministic events and Operator workflows | No real adapter or physical device acceptance. |
| Maps | Manual location fallback and deterministic coordinates for core tests; configured provider connectivity tested separately | No GPS-only dependency or claim of provider integration from coordinates alone. |
| Assistant/LPR | General contracts and manual/ordinary-UI fallback; detailed integration/evaluation deferred after v1.0 | No accuracy, model-completion or live-provider claim from fixtures. |
| Deployment | Docker local profile, one instance per required service, separate PostgreSQL databases/credentials, RabbitMQ and scoped SignalR | One PostgreSQL server permitted. No mandatory clustering or Redis scale-out. |

PAY-01 supersedes the earlier one-real-payment-sandbox requirement with controlled low-value payOS production tests plus deterministic failure/race fixtures. Three native real payment integrations, live SMS/email, real hardware and production HA are not required by this local acceptance profile. Local fixtures must be isolated from any real-money/live-device environment. UNKNOWN payment outcomes are reconciled rather than blindly recharged.

### 16.7 Approved performance and recovery recipe

| Aspect | Approved target / recipe | Required test-plan detail |
|---|---|---|
| Candidate reference machine | 4 CPU cores and 16 GB RAM, subject to actual team equipment | Declare actual CPU/RAM/OS/container allocations, build and commit; different hardware is reported explicitly. |
| Seed scale | 10 lots; 1,000 represented spaces/category-capacity units; 10,000 users; 100,000 historical reservation/session records | Define distribution, active claims, vehicle categories and representative image sizes; do not double-count a represented slot and its pool projection. |
| Warm-up | 2 minutes | Exclude warm-up from steady-state percentile reporting. |
| Steady workload | 20 concurrent users for 10 minutes | Declare search/availability, detail reads and operational/reservation command proportions and pacing. |
| Burst | 50 concurrent users for 1 minute | Report separately from steady state; do not average away burst failures. |
| Core API latency | p95 ≤500 ms | Core read/write endpoint classes reported separately; external-provider work separately measured. |
| Unexpected errors | <1% | Expected business conflicts separated; report their frequency rather than hiding them. |
| SignalR freshness | p95 ≤2 seconds under healthy local profile | Define origin-commit-to-authorized-client-update measurement and report reconnect recovery separately. |
| Restart recovery | ≤5 minutes | Preserve acknowledged local transactions and durable recovery intents; demonstrate reconciliation without duplicate financial/capacity effects. |
| Backup schedule / RPO | Daily backup; backup RPO ≤24 hours | Backup-loss tolerance is separate from ordinary-restart durability. Failed/overdue backup must be visible. |
| Restore | ≤30 minutes | Restore into declared clean local target and verify core data/relationships and recoverable workflows. Define recovery dataset/configuration scope. |

No incompatible double capacity commitment, duplicate collection/refund or unauthorized access is acceptable merely because aggregate latency/error targets pass. Scope provider upload/payment/AI latency separately. Record production, sandbox (if separately used) and fixture modes and actual timings; unexecuted tests stay NOT RUN. These are local mock-project objectives, not production SLOs or claims of achieved throughput.

### 16.8 Post-v1.0 LPR/Assistant details

Retain v0.9 general directions as amended by confirmed later decisions: device-free LPR/manual review, raw observations versus accepted Operations state, scoped correction history; Assistant FAQ/search/authorized reads/reservation preparation, normal UI confirmation and authoritative APIs; Admin-published approved corpus; Gemini/OpenAI direction and asynchronous support.

Defer exact recognizer/model, dataset, confidence scale/equality, detailed provider/tool integration, routing/data-sharing/budgets and evaluation after the v1.0 requirements baseline. Do not preserve contradictory older autonomous-write, guaranteed accuracy or staffed-live-chat statements as active acceptance obligations. Existing numerical AI targets remain historical/unverified references pending the later explicit evaluation baseline; no lowered/achieved target is inferred. General security, truthful failure reporting and manual fallback remain baseline requirements.

### 16.9 Latest explicit follow-up — session exemption, deposits and closure

These decisions supersede earlier conflicting recommendations, including the daily threshold and proposed two-active-booking default.

| Setting / rule | Confirmed direction |
|---|---|
| Session free threshold | Default 15 minutes. A session at or below the threshold is free for applicable ordinary parking; above it, the full applicable duration is chargeable, not just minutes beyond 15. No daily aggregation, no midnight reset and no new grace from unpaid checkout recovery. Monthly-covered fees and separately priced services retain their existing rules. |
| Reservation prepayment mode | Owner may configure FULL (default), PERCENTAGE 0<p≤100%, or positive FIXED amount with no policy upper cap. Fixed amount may exceed booking price. FULL is the disclosed reserved-duration charge under accepted pricing; no universal 30% seed. D01 applies. |
| Settlement | Credit attributable collected prepayment toward final settlement; separately disclose and collect eligible additional usage. Do not charge the same amount twice. Mode/value and financial terms must remain linked to accepted reservation terms. D01 confirms booked-duration minimum plus eligible extras, single credit and no cancellation/no-show unpaid-balance penalty. E01 confirms automatic excess refund initiation and independently tracked payout; covered commercial checkout may complete before refund payout (§19). |
| Reservation minimum / maximum | Defaults 15 minutes / 24 hours. |
| Advance booking horizon | Default 7 days. |
| Inter-booking buffer | Default 0. Compatibility, physical occupancy and competing claims still constrain acceptance. |
| Maximum active bookings/account | No maximum by default. A disabled cap is distinct from zero permitted bookings; global cancellation restrictions and incompatible vehicle/capacity claims still apply. If enabled, cap is per account within the lot and counts PENDING_PAYMENT, CONFIRMED and ALLOCATED reservations (D02); default remains disabled. |
| Temporary facility closure | Monthly pass remains active. Refund verified unavailable intervals once; retain original end date. Ordinary fullness does not qualify as closure. |
| Permanent closure | Cancel remaining monthly coverage and refund eligible unused value; deduct overlapping already-compensated value. No automatic extension. |
| OTP | Either verified email or verified phone independently for all roles. Missing/unverified channel cannot log in. SMS fixtures remain the phone acceptance mode until separate live integration. |
| Retention | Conversations: 90 days from last message; administrative reads do not reset. Financial documents: 7 years from issuance, subject to holds. |
| Vehicle-binding disputes | Admin reviews verified contact, relevant registration/transfer evidence and Operator findings; notify existing holder; never transfer automatically on nonresponse. Keep unresolved cases pending/escalated. Preserve historical associations and scoped disclosure. |

Non-reservation session examples: 15:00 elapsed is within the exemption; 15:01 exceeds it, with actual applicable duration charged without a 15-minute deduction. Two completed 8-minute sessions remain individually free. A 20-minute session spanning midnight exceeds the threshold once, without splitting into two free sessions. D04 uses whole-session duration for partial monthly coverage: 60 covered + 10 uncovered minutes means the uncovered 10 minutes is chargeable. D01 expressly preserves the disclosed booked-duration minimum for reserved visits, including visits at/below 15 minutes; it is not waived by these examples.

## 17. Repository alignment evidence retained from this conversation

Static reads obtained earlier on 12 October; no SQL/application execution. **Working branch:** `develop` at `304487ad0009be2de9be936dfca88d88658fe4fd`. **Database draft:** actual name `arch/database-schema` at `d510e4e7c3e3719e3e616496a56b99720f7ae5b5`. These pinned observations supplement, rather than silently replace, the original historical DB01–DB24 review.

| Observed finding | Direction |
|---|---|
| Database branch relaxes active plate uniqueness and its change log explicitly permits multiple accounts per plate. | Conflicts with approved duplicate rejection/Admin dispute workflow; restore the intended invariant with migration/conflict handling. |
| Older Parking boundary retains tariffs and operational incidents; architecture still places physical occupancy there. | Align tariff authority to Payment & Pricing; accepted occupancy/incidents to Operations; Facility owns structure/service configuration. |
| Monthly table and GiST exclusion constraints now exist; payment provider transaction uniqueness exists. | Do not repeat historical “absent” claims as current. Verify full anchored periods, rights/money separation, pooled capacity and cumulative refund constraints. |
| Monthly schema still allows GUARANTEED_CAPACITY_SLOT. | Align offered/accepted MVP data contracts with available-only scope; an enum is not approval of future functionality. |
| develop Parking persistence reads supported_vehicle_type, while database branch removes it for slot_compatibilities. | Coordinate API/repository/schema migration before integration; no drop-in compatibility claim. |
| develop Parking persistence still reads tenants directly. | Remove cross-service data access via the approved identity/organization contract or justified projection. |
| AI context files still name Kafka and older boundaries/state. | Synchronize agent guidance to the approved RabbitMQ/six-service/current-status baseline so future agents do not implement stale instructions. |

References: [database change log](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/d510e4e7c3e3719e3e616496a56b99720f7ae5b5/docs/02-architecture/database/db-update-log/DB-20261009-01.md); [database architecture](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/d510e4e7c3e3719e3e616496a56b99720f7ae5b5/docs/02-architecture/database/database-architecture.md); [Operations precursor SQL](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/d510e4e7c3e3719e3e616496a56b99720f7ae5b5/scripts/database/microservices/03-reservation-session-service-db.sql); [Payment SQL](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/d510e4e7c3e3719e3e616496a56b99720f7ae5b5/scripts/database/microservices/04-payment-service-db.sql); [develop Parking repository](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/304487ad0009be2de9be936dfca88d88658fe4fd/src/Services/ParkingService/SmartParking.ParkingService.Persistence/PostgresParkingStructureRepository.cs); [agent project context](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/304487ad0009be2de9be936dfca88d88658fe4fd/ai/context/project-context.md).

## 18. Policy catalogue, permission matrix and state/race closure worksheet

**Status:** working specification worksheet, not blanket user approval. “Confirmed” below means derived from explicit decisions already recorded in this review. D01–D06 have the approved dispositions in §18.4. Other “Recommended” implementation approaches remain design choices, not evidence of completed implementation. Implementation choices that preserve approved semantics can be completed by the development team without another product decision. Do not mark a whole proposed FR row in §9 approved merely because this worksheet covers one clause.

### 18.1 Policy catalogue — what can be completed now

Each entry must have: stable key, type/unit, owning service, scope, default, permitted range, enabled/disabled meaning, configuring role, override rules, effective time/timezone, version reference, and acceptance examples. Unset means inherit where permitted; zero is a real value only where valid; disabled is explicit. Preserve accepted operational and financial versions. Structural validation and implementation do not require choosing a new product policy.

| Policy group / traceability | Confirmed values / semantics | Domain owner | Still needs user selection? |
|---|---|---|---|
| Session exemption / B03, DEF-25 | 15 min/session; inclusive boundary; full applicable duration above threshold; no daily aggregation | Payment & Pricing calculation; Operations session facts | No: D04/D02 confirmed; E03 applies monthly coverage before calculating booked minimum |
| Rounding / B03, M-05 | One half-up to 1,000 VND; eligible-collected refund cap wins | Payment & Pricing | No; implement examples and repeated-adjustment fixtures |
| Reservation limits / DEF-07/08, FR-RES-02 | Min 15 min; max 24 h; horizon 7 days; buffer 0; active cap disabled by default | Parking Operations | No: unspecified ranges locked; enabled cap lot-scoped for pending-payment/confirmed/allocated |
| Prepayment / FR-RES-04 | FULL default; Owner-selectable PERCENTAGE or FIXED; credit settlement | Payment & Pricing financial terms; Operations confirmation | No: D01/E01/E03 confirmed; implement calculation, return and zero-price confirmation |
| Allocation / DEF-02/11/13/14 | Lead 1 h; protection 4 h; ordinary payment hold 5 min; offer 5 min | Parking Operations | No: D02 locked unspecified ranges; D05 strict acceptance before expiry |
| Arrival / DEF-15/16/20 | At/before deadline timely; no-show strictly after; late tolerance disabled or 15–30 min, default enabled value 15; Operator tie-break | Parking Operations | No: D06 scoped Operator correction/recovery confirmed; arrival equality unchanged |
| Reallocation / DEF-04 | Default 2 accepted Driver requests; no charge to allowance for transport retries/invalid requests/system recovery | Parking Operations | No new range approved: keep default locked under D02 |
| Occupancy / DEF-05/34 | Projection 4 h; never clears accepted occupancy on expiry; occupied backup deducted once | Parking Operations; IoT supplies observations | No product choice for timeout behavior; technical ordering/versioning required |
| Monthly / M-01–09 | Payment 15 min; renewal 7 days; reminders 7/1 days; 3 prepaid periods; strict A<H; no capacity hold; anchored periods | Operations entitlement; Payment financial terms | No: retain defaults unless an explicit existing range permits variation |
| Refunds / DEF-09, M-05 | Approved ordinary bands/no-show, monthly 100% prestart / 50% unused midterm; failure 100% eligible unused | Payment & Pricing; scoped handlers authorize cases | No new base/rate choice: collected eligible prepayment; failure 100%; unspecified ranges locked under D02 |
| Closure / Q04 | Temporary retains pass; permanent ends coverage; verified unavailable value refunded once, no extension | Facility closure facts; Operations coverage effects; Payment refund | No reapproval; specify interval union, overlap and paid-period valuation |
| Abuse / §14.1 | Existing Admin-only cancellation defaults, counts, restrictions and expiry composition | User & Access restriction facts with Operations enforcement | No; unlimited active booking default does not remove these controls |
| Authentication / §16.5 | All roles; either verified email/phone; 15-min access, 7-day absolute, 24-h inactivity; TOTP deferred | User & Access | No channel/role decision; implement purpose-bound OTP, throttle/recovery contracts under approved security scope |
| Retention / §14.4, §16.4 | Confirmed categories, anchors, holds, reviews, upload limits and deletion controls | Each data-owning service | No reapproval of selected values; specify timestamp arithmetic and provider proof |

**Confirmed catalogue closure rule (D02):** keep unbounded scalar settings at their approved default for v1.0 until an explicit configurable range is recorded; do not invent broad Owner override powers from the existence of a settings screen. Existing explicitly approved Owner choices (e.g. late tolerance range, deposit modes and versioned tariffs) remain available. Required tariff/category configuration must be supplied before selling the affected product, never silently sourced from demo data.

### 18.2 Scoped action-by-role matrix — confirmed authority versus missing grants

A business actor requests an action; the owning backend service validates and executes it. “Payment executes” is service authority, not a new human role. Every permission includes current account/grant state and resource scope. Admin is not an automatic superuser of lot operations or private evidence. Assignment and ownership are independent of login method.

| Action | Driver | Assigned Operator | Owner of lot | Admin | Status / condition |
|---|---|---|---|---|---|
| View own booking/visit/payment; submit eligible cancellation, appeal or dispute | Own records | Only own Driver records if separately acting as Driver | Same | Same | Confirmed; staff roles do not imply a Driver permission or broad customer access |
| Operational arrival, admission, allocation/recovery and checkout | Request/present credentials; no authoritative self-arrival | Scoped operational authority | No implied execution grant | No implied execution grant | Operator scope confirmed; command guards must be specified |
| Initiate ordinary appeal/refund handling and system-mispricing correction | Submit own case/request | Handle assigned-lot case; audited correction at original cutoff | Handle escalations | No general lot refund authority | Confirmed; Payment calculates/executes; no monetary threshold |
| Monthly cancellation / escalation | Submit own eligible request | Provide scoped findings | Handle owned-lot monthly cancellation / escalation | No inherited business authority | Confirmed |
| Configure published tariff/refund/prepayment terms | No | No implicit publication grant | Owned lot within explicit Admin bounds | Platform bounds, not implicit lot tariff editing | Confirmed D01/D02; fixed deposit has no policy upper cap; no blanket rate-range approval |
| Manage devices / inspect diagnostics | No management | Assigned-lot management | Owned-lot read-only diagnostics | Platform integration authority needs exact contract | Confirmed Operator/Owner split; D03 does not grant new broad platform-secret privileges |
| Correct recognition/session plate | Request correction | Scoped reasoned correction with history | No automatic grant | No automatic operational grant | Confirmed; does not rebind global vehicle ownership |
| Adjudicate global vehicle binding | Submit own dispute | Supply local findings | No general adjudication grant | Evidence-backed decision / guarded User & Access action | Confirmed; notify prior holder, no silence-based transfer |
| Hold evidence / release own originating hold | Request via case | Handled operational case | Handled escalated/monthly case | Authorized platform investigation | Confirmed; owning service acknowledges; other holds remain |
| Read/export reports, evidence or receive SignalR | Own authorized data | Assigned-lot need-to-know | Owned-lot business scope | Explicit platform/security/audit scope | Confirmed; no unrelated private chats or blanket evidence inheritance |
| Publish Assistant knowledge | No | No | May supply content outside publication workflow | Publish approved versioned corpus | Confirmed; no fifth Developer business role |
| Create Operator accounts / assign or revoke lot access / approve Owner registration | No self-grants | No implied delegation | Create Operator accounts within own organization and assign/revoke owned-lot access | Approve Owner registration request; no approval per Operator hire | **CONFIRMED D03/E02:** account creation is Owner-initiated, not merely assignment of pre-existing eligible accounts; verification/current access checks remain |
| Emergency departure / unpaid recovery | No self-override | Assigned-lot audited execution, with reason and retained money/debt facts | Handle escalations | No blanket lot override | **CONFIRMED D03:** unpaid recovery cannot reopen a paid visit; UNKNOWN money must first be reconciled |

Account blocking, booking-only restriction, OTP challenge lock and entitlement expiry are separate guards. An unlimited active-booking default is not unlimited conflicting capacity. Required permission must still be valid on retries and when serving cached results/evidence. Revocation stops future protected actions/subscriptions; it does not erase historical transactions or settled obligations.

### 18.3 State and race specification — work that can proceed without new product choices

Use a transition table per aggregate with command, actor, current state/version, guard, accepted timestamp, atomic local effects, emitted event, idempotency identity, rejected/conflicting outcome and recovery owner. Keep reservation entitlement, physical occupancy, checkout, payment/refund, monthly entitlement and support cases separate. A refund or appeal is not a physical session state; reallocation retains revision/history without erasing the original request.

| Workflow / race | Existing constraint and recommended specification | User decision? / traceability |
|---|---|---|
| Reservation normal path | PENDING_PAYMENT → CONFIRMED → ALLOCATED → PARKING → COMPLETED, with linked visit/checkout facts and guarded cancellation/expiry/no-show outcomes. Specify which command causes each transition; enum membership alone is insufficient. | No new policy; DEF-01/02. Exact aggregate mapping is design work. |
| Last compatible slot / pooled capacity | Operations atomically evaluates overlapping compatible claims and commits one feasible result. Loser receives conflict/current alternatives, not a duplicate entitlement. Use transaction/version/locking appropriate to the local profile. | No product decision; DEF-03/06/14, ARCH-05/09. Engine/lock choice is technical. |
| Backup and occupancy | Atomically convert compatible reservation claim into visit occupancy without double deduction. Keep backup designation/history; never release occupancy because a timer, cancellation or refund fired. | No; DEF-03/05/11. Gate opening is not departure. |
| Unpaid hold expiry versus verified ordinary payment | One ordered Operations decision decides whether the old claim survives or has expired. After expiry, atomically reacquire compatible capacity if possible; otherwise reconcile/refund the verified collection. Never resurrect a conflicting old claim. | Existing late-payment policy; DEF-12/14. Reuse existing ordinary-hold boundary where specified; do not copy monthly A<H into ordinary payment without evidence. |
| Alternative accept versus expiry | One idempotent versioned command consumes/releases temporary alternative claim once. Expiry does not itself cancel underlying entitlement. | D05 confirmed: authoritative acceptance time < expiry; equality expired. DEF-16. |
| Timely arrival versus no-show | Check authoritative arrival before no-show; same instant at arrival cutoff counts timely. Delayed evidence cannot evict a vehicle admitted meanwhile. | D06 confirmed: scoped Operator corrects with history, recovers compatible capacity without displacement, or invokes applicable failure handling. DEF-15/20. |
| Checkout versus unpaid recovery versus payment success | Accepted checkout freezes billable cutoff. Serialize recovery with known money state; a potentially successful UNKNOWN payment blocks blind unpaid recovery until reconciled. Paid visit is never reopened for continued stay. Preserve physical occupancy until actual departure. | Guards derive from B04; no new paid-stay policy. |
| Two cash/provider attempts or duplicate callbacks | Persist attempts and verify amount/currency/order/payee; dedupe external transaction identity and internal operation. Do not start another collection while prior result is UNKNOWN. Unexpected duplicate collection becomes visible for reconciliation, never a second entitlement. | Technical contract under existing money invariants; FR-PAY-04. |
| Refund/correction concurrency | Atomically reserve eligible refundable balance for pending execution; completed and unresolved in-flight refunds prevent a second allocation of the same balance. A definitive failed attempt may release its reservation; timeout alone cannot. Compare correction with attributable net money and prior adjustments. | D01 collected-money base and E01 automatic excess payout confirmed; pending/unknown money stays reserved against duplicate refund. |
| Monthly activation versus deadline | Operations commits entitlement only if verified settlement and commit satisfy strict A<H. Money collected without entitlement is reconciled/refunded; no backdated activation or monthly capacity hold. | Already confirmed; M-01–09. |
| Temporary closure then permanent closure/cancellation | Union verified unavailable intervals per paid period; exclude previously compensated overlaps from later eligible value. Preserve end date during temporary closure; cancel remaining coverage for permanent closure. | Policy confirmed; calculation/atomic ledger design remains. |
| Planned/emergency Facility change | Planned change pending until Operations protection/recovery commits; emergency physical restriction recorded immediately, with safe admission stop when communication unavailable. Durable coordination; never pretend physical emergency rolled back. | Already confirmed; ARCH-05/09. |
| Evidence hold versus deletion | Data owner serializes acknowledgement/deletion eligibility; do not acknowledge preservation after irreversible deletion. Missing evidence stays explicit; active other holds survive one release. | Already confirmed; B06/B09. |
| Case decision versus refund execution | Persist decision independently from payout. ACCEPTED/APPROVED does not mean COMPLETED. Reopen/assignment mechanics must preserve history and require scoped actor. | Technical transitions under §14.7; a new appeal deadline or loss of rights would require approval. |

**Recommended retry/unknown-outcome contract:** same logical command uses a stable idempotency key and payload fingerprint; same key with a changed payload is rejected, same completed command returns its authorized recorded outcome. Local commit and outbox are atomic; consumers dedupe durably and enforce aggregate versions/guards. Delivery is retryable; business effects must remain once-only. A network timeout is UNKNOWN, not evidence of failure. Lookup/reconcile before any new financial attempt. Existing three payment retries after initial attempt apply only to known retryable failures, not automatically to broker redelivery. After bounded infrastructure retry, keep durable pending work, surface failure and route to authorized reconciliation; do not silently drop work or declare business success.

The team can choose backoff, broker retry schedules, transaction strategy, outbox/inbox implementation and correlation field names without product reapproval, provided business deadlines and invariants remain unchanged. Do not make a technical retry TTL silently cancel rights or lose money. Idempotency/deduplication retention must cover supported recovery/replay windows; define that window in the technical contract.

Minimum concurrency acceptance cases: two buyers/one resource; occupied backup; callback replay; verified late ordinary payment with/without compatible capacity; simultaneous correction/refund; UNKNOWN plus cash request; monthly commit exactly H; arrival exactly cutoff; offer exactly expiry (D05); late authoritative arrival after reassignment (D06); temporary/permanent closure overlap; duplicate physical departure; hold/deletion race. These are required specification cases, not executed test results.

### 18.4 D01–D06 — approved dispositions, latest user message

This table supersedes the former recommendation table. Approval is limited to the user's stated choices; omitted wider platform-role powers and generic refund-rate ranges are not silently adopted.

| ID / where | Confirmed rule | Remaining work, not a reopening |
|---|---|---|
| **D01 — Deposit and settlement**; §16.9, FR-RES-04, DEF-09/25 | FULL default; PERCENTAGE **0<p≤100%**; FIXED positive with **no fixed policy upper cap**, including amounts above booked-duration cost. Use approved rounding; validate rather than silently create zero prepayment for a positive-price reservation. Completed reserved visits owe disclosed booked-duration minimum plus eligible extras, including visits ≤15 min. Credit prepayment once. Cancellation/no-show refund accepted percentage of eligible money actually collected; do not collect unpaid remainder as a penalty. | Preserve financial snapshot and distinguish deposit from final price. Technical numeric/provider limits must be surfaced honestly, not described as an approved business cap. E01 confirms automatic surplus return; E03 applies monthly coverage before minimum and permits genuine zero-price confirmation. |
| **D02 — Configurability**; DEF-07/08/09, FR-RES-02 | Unspecified ranges locked at approved defaults for MVP; preserve already-approved Owner choices. Optional active-booking cap is lot-scoped and counts PENDING_PAYMENT, CONFIRMED and ALLOCATED reservations; default no cap. Facility-failure refund fixed at 100% eligible unused value. | Do not infer a new universal 0–100% Owner refund-rate envelope from the earlier recommendation. Each editable rate uses its explicit existing bounds; otherwise its approved default stays locked. Validate active cap atomically; no retroactive cancellation when configuration changes. |
| **D03 — Permissions**; ARCH-07, §18.2 | Admin approves Owner registration requests. Owner assigns/revokes eligible Operators within owned lots. Assigned Operator executes audited emergency departure/unpaid recovery; Owner handles escalations. No blanket Admin operational override. | User & Access owns grants/current checks. Do not interpret Owner-registration approval as unrestricted platform-role/secret administration. E02 confirms Owner creates Operator accounts and assigns organization/lot access; no Admin approval per hire. |
| **D04 — Monthly overlap**; B03, DEF-25 | Whole session duration determines exceeding 15 min; charge only non-covered applicable ordinary intervals. 60 covered + 10 uncovered minutes → charge uncovered 10 minutes, never covered 60. | Use accepted checkout cutoff; no midnight reset. E03 confirms monthly coverage applies before calculating the disclosed booked minimum. |
| **D05 — Alternative expiry**; DEF-16 | Acceptance requires authoritative Operations decision time strictly **before** expiry. Equality means expired. Underlying entitlement follows its independent rules. | Serialize acceptance/expiry against same claim/version; client click time does not establish acceptance. |
| **D06 — Delayed arrival evidence**; DEF-20 | Operator reviews authoritative evidence of timely arrival and corrects mistaken no-show with history. Recover compatible capacity without displacing others; if recovery impossible, use approved applicable facility-failure handling. | Preserve evidence, original decision and correction; no arbitrary client backdating, occupied-vehicle displacement or duplicate compensation. |

**Cancellation example with a high deposit:** accepted booking price 100,000 VND; fixed deposit collected 300,000 VND. If accepted voluntary-cancellation terms grant 50%, refund 150,000 VND and retain 150,000 VND. Do not cap the cancellation base at the 100,000 booking price: the user explicitly selected the eligible collected-money base. At an approved 0% no-show rate, retained eligible collected prepayment is 300,000 VND and no additional unpaid-balance penalty is created. This consequence must be disclosed with the deposit and cancellation terms before acceptance. No new event surcharge, fine or damage-deduction feature is authorized by this deposit example.

**Completed-visit example:** booked minimum 100,000 VND; eligible extras 25,000 VND; prepayment 30,000 VND → collect 95,000 VND. With prepayment 300,000 VND, final charge remains 125,000 VND, leaving a 175,000 VND settlement excess; the deposit does not become the price. E01 specifies the return workflow for that excess.

### 18.5 Concrete next deliverables after dispositions

1. Finalize typed catalogue rows from confirmed D01/D02/D04 and exact existing source values; mark locked/default/configurable/required-before-sale explicitly.
2. Apply confirmed D03 in the action/resource/delegator matrix; implement approved E02 Owner-created Operator onboarding; implement no implied role inheritance. Map each action to its owning Application command and resource-level authorization check.
3. Produce guarded state/command tables using §18.3 and confirmed D05/D06. Domain entities enforce local invariants; Application orchestrates workflows and authorization; Persistence implements local atomic concurrency and durable records; Infrastructure provides external adapters; API maps transport contracts. No cross-service table access is introduced.
4. Propagate decisions into SRS/BR/UC/stories, API/event contracts and acceptance scenarios. Record implementation pending separately from normative closure; align schema/code against the pinned findings in §17 before claiming compatibility.

## 19. E01–E03 confirmed contracts and implementation closure

### 19.1 E01–E03 approved — including Owner-created Operator accounts

The user explicitly approved E01–E03 and clarified that **Operator accounts are created and assigned by Owner**. This supersedes the previous invite-only/pre-existing-eligibility wording. These are confirmed requirements, not pending recommendations.

| ID / affected location | Approved contract | Implementation qualification |
|---|---|---|
| **E01 — Excess-deposit return**; D01, FR-PAY, refund/checkout contracts | Payment automatically initiates returning surplus after accepted checkout fixes the final charge. Use original payment route where supported; audited assigned-Operator cash return for original cash. No cancellation percentage. Commercial checkout may complete when its charge is covered while refund remains separately pending/processing/completed. | Preserve durable refund obligation and visible status through failure/UNKNOWN; no duplicate payment, unrelated-debt offset or forfeiture. Provider payout completion is not presumed. Actual departure is independent. Example: 300,000 collected − 125,000 final charge = 175,000 refund due. |
| **E02 — Owner-created Operator accounts**; D03, ARCH-DEF-07, User & Access / §18.2 | Owner creates Operator accounts in the Owner's organization, assigns owned-lot access and revokes those scoped grants. No Admin approval per hire. Admin approves Owner registration requests. Owner cannot create Admin/Owner privilege for staff or grant access to another Owner's organization/lots. | User & Access performs guarded account/membership/grant writes. Existing verification and password/verified-channel OTP rules remain; Owner creation alone is not proof of contact verification. Activation/credential setup is a technical secure onboarding flow. Preserve audit and historical attribution on revocation. Do not silently overwrite/merge an existing account based on entered contact data. |
| **E03 — Monthly-covered reservation**; D01/D04, M-05, DEF-25, quote/confirmation | Apply valid monthly coverage before calculating disclosed booked minimum; never charge covered ordinary fees twice. Charge uncovered booked intervals and eligible separate services only. Genuinely zero-price reservation confirms without monetary payment, still subject to compatible capacity, active-cap/booking restrictions and current entitlement validation. | This is an explicit narrow exception to the prior no-prepayment exclusion, not pay-on-arrival for positive-price bookings. Record zero-charge reason/coverage version; do not manufacture a successful provider transaction. A positive-price deposit that rounds to zero still requires validation. No separate reservation fee is introduced. |

For E03, the zero-price branch takes precedence over ordinary deposit mode: no fixed deposit is collected for that approved zero-price branch. Positive-price reservations retain Owner-selected FULL/PERCENTAGE/FIXED rules. The pass itself still protects no capacity; an independently confirmed ordinary reservation acquires its normal compatible claim exactly once.

The fixed-deposit event example approves the deposit amount, not a new event tariff engine, damage bond, deduction discretion or separate reservation fee. Technical maximum numeric sizes and provider transaction limits must result in validation/clear failure, never silent amount truncation or an invented policy ceiling. Display booked minimum, deposit, possible balance/excess and cancellation refund terms separately.

### 19.2 Complete the catalogue without inventing new policy values

Use §16.1–2, §16.4–5, §16.9 and §18.1/4 as the value source. No second approval is needed to transcribe confirmed values into a typed catalogue. Per entry, record service owner, unit/type, scope, source decision, default, editable/locked status, allowed range, resolution order, effective version and boundary tests.

| Key / group | Type, scope and approved constraint | Configuration / validation contract |
|---|---|---|
| RESERVATION_MIN_DURATION / MAX_DURATION | Duration; lot; defaults 15 min / 24 h | Locked where no explicit override range exists. Validate min≤duration≤max. |
| ADVANCE_BOOKING_HORIZON | Duration; lot; default 7 days | Locked absent explicit range; specify timestamp anchor and boundary consistently in BR/API tests rather than accepting client-computed eligibility. |
| INTER_BOOKING_BUFFER | Duration; lot; default 0 | Zero valid; does not override physical occupancy or claim compatibility. |
| ACTIVE_BOOKING_CAP | Optional positive integer; account within lot; disabled by default | Owner may enable lot-scoped cap; no invented default of 2 or arbitrary upper business cap. Atomically count PENDING_PAYMENT/CONFIRMED/ALLOCATED; exclude PARKING and terminal reservations. Decreasing cap affects subsequent eligibility, not deletion of accepted bookings. |
| PREPAYMENT_MODE | Enum FULL/PERCENTAGE/FIXED; lot financial policy | Owner chooses; FULL default. Store mode/value on immutable accepted terms. |
| PREPAYMENT_PERCENT | Decimal percentage; used only for PERCENTAGE | Owner-configured 0<p≤100; not a universal 30% default. Validate rounded payable amount for positive-price orders. |
| PREPAYMENT_FIXED | Positive decimal VND; used only for FIXED | Owner-configured; no policy upper cap or cap at booked price. Apply approved rounding; validate technical/provider representability separately. |
| RESERVED_MINIMUM / FINAL_SETTLEMENT | Money/calculation; accepted reservation | Booked-duration minimum plus eligible extras; one credit of attributable prepayment. No second charge for an already-covered amount. E03 defines monthly interaction; E01 surplus workflow. |
| ORDINARY_FREE_THRESHOLD | Duration; session; default 15 min | Whole session to accepted checkout; covered intervals excluded from money, not elapsed-time comparison. Does not waive reserved minimum. |
| VOLUNTARY_REFUND_BANDS / NO_SHOW | Accepted financial policy and collected-money base | Retain approved bands; only explicitly authorized ranges editable, otherwise default locked. No unpaid-balance penalty. Snapshot at acceptance. |
| FACILITY_FAILURE_REFUND | Rate 100% of eligible unused value | Owner cannot reduce; prevent overlapping compensation/refund. |
| OFFER_DURATION / ACCEPTANCE | Duration default 5 min; Operations offer | Strict authoritative decision time < expiry; equality expired. No underlying entitlement cancellation solely from offer expiry. |
| ARRIVAL_DEADLINE | Timestamp resolved under accepted arrival policy | At/before timely; no-show strictly after without authoritative timely evidence; D06 audited correction/recovery. |
| Other timing, monthly, auth, media, abuse policies | Exact confirmed entries in §§14/16 | Preserve explicit existing ranges and Admin-only controls; otherwise locked defaults. No implicit authority from UI visibility. |

A technical catalogue should identify acceptance at exact minimum/maximum/horizon as testable boundaries. Where a source lacks a boundary, annotate the proposed interpretation in the contract review rather than silently importing monthly A<H. Do not turn every implementation timestamp or retry interval into an Owner setting. Missing required commercial configuration prevents activation of that product; fixture values are not production defaults.

### 19.3 Permissions — what is closed and what remains implementation

D03/E02 close the business actor choices for Owner approval, Owner-created Operator accounts and scoped assignment/revocation, emergency departure and unpaid recovery. Preserve existing separate platform security/administration permissions; this amendment does not broaden them.

Implementation can now map each §18.2 row to stable action/resource permissions, for example an Owner-registration review action, an Operator-lot assignment action, an emergency-departure action and an unpaid-checkout recovery action. Names are technical choices. Enforce current membership and assigned/owned lot in the resource service, not merely in API Gateway or JWT role text. No new fifth actor or unlimited Admin inheritance is required.

Every privileged command records actor, effective grant/scope, target, prior version, reason where required, timestamp and result. An Owner's assignment command cannot create a grant broader than that Owner's authority. An Operator losing access cannot execute a new recovery or view cached private results merely by reusing an old request key. Revocation preserves historical attribution and durable already-authorized financial work; worker execution is a service workflow, not a new human login.

### 19.4 Guarded state/command contracts ready to specify

The following state labels for technical money substates are a recommended design vocabulary, not a mandatory rewrite of existing domain enums. Keep existing minimal lifecycle IDs and traceability. Explicit unknown/pending representation may be a status attribute rather than a new top-level enum.

| Command / current state | Actor and guard | Atomic local effect / outcome | Retry or conflicting race |
|---|---|---|---|
| Create ordinary reservation → PENDING_PAYMENT | Authorized Driver; accepted quote/version; compatible capacity; active cap if enabled | Operations records reservation and incompatible-claim hold together | Same operation returns same result; concurrent cap/capacity loser rejected without another claim. E03 zero-price path bypasses monetary pending payment: record validated coverage/zero-charge basis and atomically confirm compatible reservation; no provider payment required. |
| Confirm paid ordinary reservation | Verified Payment outcome; correct order/amount/currency; Operations capacity guard | CONFIRMED with one compatible claim; convert hold without double deduction | After hold expiry use approved atomic reacquisition; if unavailable, money reconciliation/refund, never duplicate capacity. |
| Allocate CONFIRMED booking | Operations scheduled/authorized command at allocation time; compatible resource | ALLOCATED with immutable allocation revision and relevant claim updates | Late-created confirmed booking requires idempotent immediate evaluation; occupied vehicles never displaced. |
| Accept alternative | Authorized claimant; authoritative time < expiry; current offer/version and compatible claim | Consume offer and update allocation/claim atomically | At equality reject as expired; expiry/acceptance cannot both consume claim. Duplicate acceptance returns recorded outcome. |
| Record arrival / mark no-show | Assigned Operator/authoritative Operations evidence; applicable cutoff | Timely event prevents no-show; no-show only after cutoff | D06 correction preserves both history and valid competing claims; no automatic backdating from browser time. |
| Admit / record occupancy | Assigned Operator/authorized Operations workflow; applicable capacity and credentials | Transition reservation/visit as appropriate; accepted occupancy consumes capacity once | Gate authorization/open not equivalent to actual entry; repeated observation/command cannot create two visits. |
| Accept checkout → pending payment/settlement | Assigned Operator; valid open visit; authoritative cutoff and accepted financial terms | Freeze billable end; store calculation reference; Payment computes reserved minimum/extras/credit | Concurrent recovery/payment requires version checks and money reconciliation. Positive amount due collected; zero amount due requires no fake payment; excess path E01. |
| Invalidate unpaid checkout | Assigned Operator; reason; not paid; no unresolved possible payment success | Restore billable continuation on same visit with attempt history; no new grace | UNKNOWN payment blocks blind invalidation until reconciled. Never reopen paid visit for longer parking. |
| Complete financial checkout | Verified settled charge/credit | Record commercial settlement once | Refund payout is a separate lifecycle; E01 permits covered checkout to complete while surplus refund payout remains pending. Physical occupancy remains until departure. |
| Record emergency departure | Assigned Operator; explicit emergency permission/reason | Record actual movement, incident and retained financial facts | May permit physical departure with debt; gate opening alone cannot free capacity. Duplicate departure no second release. |
| Request/approve/process refund | Authorized case handler or E01 automatic rule; eligible collected balance | Persist reason and reserve refundable balance; Payment executes external payout | Concurrent refunds/corrections cannot reserve same money. Timeout stays UNKNOWN/processing; only definitive failure releases reservation for a new attempt. |
| Correct mistaken charge | Assigned Operator; system mispricing; original checkout cutoff | Record corrected calculation and linked difference adjustment | Compare attributable net money and pending adjustments; preserve original successful transactions; retries cannot duplicate refund/collection. |
| Commit monthly entitlement | Operations coordinator with verified financial settlement, strict A<H | Commit entitlement once; no parking-capacity claim | At H or after: do not activate; reconcile collected money. Retries cannot shift anchor/extend paid period. |
| Verify closure compensation | Facility authoritative closure; Operations affected coverage; Payment accepted periods | Record union of eligible unavailable intervals and monetary adjustment once | Temporary/permanent closure/cancellation overlap deducted; no duplicate payout or automatic term extension. |

**Durable recovery design:** one coordinator per workflow, local transaction/outbox, inbox deduplication, stable business-operation IDs and aggregate version checks. Recommended coordinator assignment: Operations for reservation/capacity/entitlement fulfillment, Payment for collection/refund execution, Facility for initiating its configuration-change workflow with Operations restriction acknowledgement. These are architecture specifications to validate, not new product choices.

**Retries:** retain three additional payment attempts only after definitive retryable failure. Broker redeliveries and command replays do not spend a fresh payment attempt or Driver reallocation allowance. UNKNOWN collection/refund means query/reconcile the same logical operation; do not switch to cash or create another provider request as if the first failed. An external provider may still produce duplicate or late effects: detect and reconcile them rather than claim the transport guarantees exactly-once delivery.

**Pending/unknown outcomes:** persist them durably, expose honest pending status and route exhausted technical retries to authorized recovery. Retry count/backoff/queue names are technical decisions; retry exhaustion alone must not erase money, automatically cancel rights or release accepted physical occupancy. Consumer lateness is not proof an external operation failed.

**Publication evidence:** complete API/event contracts and boundary/concurrency scenarios; record NOT RUN for unexecuted tests. Propagate the final contracts into active SRS/BR/UC/stories and architecture before publishing v1.0. This review does not certify implementation, migrations, real provider refunds or throughput.

## 20. Project-manager closure assessment after E01–E03

### 20.1 What is now closed

Q01–Q07, D01–D06 and E01–E03 have recorded user dispositions. No additional approval is needed to restate those decisions in the policy catalogue, action-by-role matrix or guarded state tables. Owner account approval versus Owner-created Operator onboarding is explicit; payment/refund versus physical departure is explicit; monthly-covered zero-price reservation has its own approved path.

This conclusion concerns the reviewed decision chains, not a claim that every source document or implementation is ready. Section 9 retains older proposed clauses and mixed A/X/F source statuses; those must be reconciled clause by clause against confirmed decisions before final v1.0 publication. Do not treat the word OPEN on a schema migration, event contract or test task as a demand for another business choice.

### 20.2 Decisions/actions still belonging to the project manager

| PM item / where | What still needs PM action | Recommended direction / when |
|---|---|---|
| **PM-01 — Final scope and requirements release**; §9, §10 closure criteria, §15.3 | Approve the synchronized v1.0 document set and disposition of any genuinely new clauses that remain after traceability review. This review alone is not the release. | BA/architect should first produce one concrete release candidate and a delta-only approval list. Keep all confirmed rules; map technical consequences to their approved parent decision; explicitly retain future exclusions. Do not reopen each historical “Approve” row as a new question if later decisions already settle its behavior. PM signs off the resulting manifest once the reviewable set exists. |
| **PM-02 — Delivery ownership, priority and dates**; §15.2, §§18–19 | Assign accountable people and target sprint/date for document propagation, schema alignment, implementation and acceptance evidence. Names/dates are not inferable from this review. | Sequence: synchronize requirements/permission/policy/state contracts → reconcile schema and develop → implement critical money/capacity/auth paths → execute local acceptance. Assign BA/architect to contracts, backend to domain workflows, QA to boundary/race cases; PM selects actual team members and dates. |
| **PM-03 — Published support availability**; §14.5, §15.1 | Supply actual project/demo support availability and responsible handler/rotation if this is exposed to users. | Retain asynchronous support; publish only availability the team can provide, without an invented production response SLA or 24/7 promise. This is an operational declaration, not a blocker to drafting domain contracts. |
| **PM-04 — Acceptance exception, only if a concrete blocker occurs**; §16.6–7 | Decide whether to change scope, defer a release gate or accept an explicitly documented deviation if the selected payOS/media integration or reference environment cannot meet the agreed acceptance profile. | Team first attempts and records evidence. Keep PAY-01 controlled low-value payOS production testing plus deterministic failure/race fixtures and honest mode labels. Do not preapprove fixture substitution, lower performance targets or claim real refund delivery from fixtures. No fresh PM decision is needed while the approved profile remains feasible. |

No new universal fee, numeric business limit, role override or financial penalty is needed merely to finish the current catalogue/matrix. If a technical author proposes a change to who can act, what money is owed, when rights expire, or which integrations count as accepted, escalate that concrete change to PM rather than burying it in an implementation detail.

### 20.3 Work the team can complete without another policy round

| Work | Required result | Escalate only if |
|---|---|---|
| Policy catalogue | Typed keys, service owner, scope, default/locked/configurable status, exact approved bounds, accepted versions and boundary examples | A new configurable range/default or altered commercial obligation is proposed |
| Permission matrix | Action/resource/actor/delegator mapping; Owner-created Operator account/activation flow; scoped current authorization and revocation | New privileges, cross-owner access or a change to approved account-creation authority is proposed |
| State/command tables | Actor, prior state/version, guard, authoritative timestamp, atomic effects, idempotency, emitted outcome and recovery owner | A new expiry/cancellation/appeal restriction changes rights, or a previously unspecified boundary has competing business outcomes |
| Money and capacity concurrency | Local atomic guards, durable workflow coordination, dedupe, refund-balance reservation, retries and reconciliation of UNKNOWN | Recovery would write off money, introduce penalties, displace occupants or cancel an entitlement outside approved rules |
| Source traceability | Align §9 proposals and FR/BR/UC/story clauses to confirmed decisions; report exact residual substantive differences | A clause cannot be derived from approved scope and creates a new feature/obligation |
| Acceptance recipe execution | Declare actual hardware, workload mix and provider/fixture modes; run and retain results | A required gate needs exemption or approved profile needs change |
| Repository alignment | Reconcile historical DB01–DB24 and pinned §17 findings against actual working branches, then validate migration/API compatibility | Team wants to change a confirmed ownership boundary or drop an in-scope rule |

Technical recommendations remain subject to normal architecture/code review; no per-lock, per-retry or per-event-name PM permission is required. Do not mistake approval of a product outcome for evidence that a chosen mechanism has been implemented or validated.

### 20.4 Specific acceptance additions from this approval

- Owner creates an Operator account and assigns own-lot access; contact verification/activation guards still apply. Cross-owner assignment and self-promotion are rejected. Revocation blocks future protected commands while historical actions remain attributable.
- A 300,000 VND collection against a 125,000 VND final charge creates exactly one 175,000 VND surplus refund obligation. Checkout can settle while payout is pending; repeated commands/callbacks cannot duplicate it. UNKNOWN remains visible until reconciliation.
- Original cash excess uses audited cash return; online original-route payout is tested in its actual declared integration mode. Unsupported provider refund capability remains an integration issue and cannot silently become a cash refund of an online payment.
- Valid monthly coverage is applied before the booked minimum. Fully covered zero-price reservations confirm without provider payment and acquire the ordinary compatible claim once; a pass alone acquires no claim. Concurrent last-capacity and active-cap checks still apply.
- A zero-charge confirmation records coverage/financial basis, not a fictitious successful transaction. A tiny deposit rounded to zero on a positive-price booking is rejected/validated under the separate D01 rule.

These are acceptance specifications, not executed test results. Final v1.0 publication follows PM-01 after document synchronization; implementation readiness and measured acceptance are reported separately.


## 21. A-01 — dependent reservations before voluntary monthly cancellation

**CONFIRMED by the project manager; propagated in RC1 on 13 October 2026.** Resolve dependent reservations first. Before completing voluntary monthly-pass cancellation, identify future reservations whose accepted price depends on coverage being cancelled and require Driver explicitly cancel those reservations first. Do not automatically cancel, reprice or charge reservations. Rebooking at ordinary prices is a separate Driver action subject to current capacity; no capacity guarantee. Recheck dependencies when accepting cancellation so a concurrent new dependent reservation cannot invalidate accepted pricing. Facility-failure/closure handling remains separate.

Propagation: FR-MON-11/14, M-05/M-07/M-09, UC-MON-05, US-D15, monthly module, policy catalogue and concurrency acceptance scenarios. This is an approved business rule; locking/coordinator/event details are technical design subject to implementation review.

RC1 is a reviewable requirements candidate, not a PM-signed v1.0 release or evidence of implementation. PM-01 final release, PM-02 named delivery ownership/dates, PM-03 published support availability and conditional PM-04 acceptance exceptions remain as in §20.2.


## 22. SEC-01 / SEC-02 — approved RC2 amendment

**PM CONFIRMED on13 October2026; RC2 document amendment.**

- TOTP enters current MVP, optional for Driver, Operator, Owner and Admin. Six-digit enrollment proof activates the factor; enabled accounts require second-step proof before normal session issuance.
- Backup recovery codes selected instead of emailed-code-only recovery. Restricted replacement requires primary proof plus an unused backup code; old factor survives until successful replacement.
- Admin-handled ACCOUNT_RECOVERY type and limited pre-login form approved. Generic acknowledgement grants no account access.
- Admin authorizes single-use password/TOTP recovery; holder chooses new password. Password-only preserves TOTP; factor reset requires verified replacement and revokes old sessions/codes.
- Different Admin handles Admin account recovery; no self-approval. No-other-Admin case uses controlled deployment-maintainer runbook.
- At least one existing verified contact plus independent evidence review required. OTP alone insufficient; no contact/insufficient evidence stays pending/escalated. No new routine national-ID collection.
- Current role/account/Owner approval/lot grants remain unchanged. Admin recovery grants no blanket operational override.

These decisions supersede all earlier TOTP-FUTURE statements in RC1, including baseline review14.5/16.5 and ARCH-DEF-07 commentary. Original history and RC1 comparison reports are retained as historical evidence, not current TOTP scope. Other deferred AI/hardware work remains deferred.

Technical elaborations explicitly disclosed: account security/factor-version serialization; purpose-bound restricted contexts; TOTP/backup replay protection; session invalidation on completed factor/password changes; separate reviewed case, authorized grant and executed result. Numeric challenge/window/backup-count/throttle parameters are security implementation-review items, not newly PM-approved values. No implementation or acceptance pass is asserted.

## 23. PAY-01 — approved payOS priority and acceptance amendment

Approved 13 October 2026 after the PM accepted the corrected wording and revised testing requirement. Earlier §16.6 and payment-provider references above have been synchronized; original source snapshots remain in history.

payOS QR receives implementation priority for MVP online payment. VNPay, MoMo and ZaloPay remain separate planned options, with integration or fixture status explicitly identified. If resources are insufficient, payOS QR provides the working online-payment path; unfinished native provider integrations remain explicitly simulated or deferred. payOS transactions must not be presented or recorded as native VNPay, MoMo or ZaloPay transactions. Payer-application compatibility is claimed only after verification. Assigned-Operator CASH_COLLECT remains unchanged.

Controlled, low-value payOS production testing plus deterministic payment failure/race fixtures replaces the one-real-sandbox-adapter requirement. VNPay native integration may remain simulated/deferred when resources are insufficient; MoMo/ZaloPay fixture arrangements remain. No separate working sandbox adapter is required. Real-money checkout exposes functioning routes only; demo fixtures are explicitly labelled and isolated from real-money data and effects. Fixture-only results cannot satisfy the required payOS real integration evidence.

See [full PAY-01 decision](2026-10-13-payos-priority-pay-01.md). Refund execution remains a provider-capability verification task; no new refund method, cash fallback, fee, role override or money/capacity rule is approved by this amendment. SEC-01/02 remains in force. Package RC3 is a review candidate, not final requirements release approval or implementation acceptance.
