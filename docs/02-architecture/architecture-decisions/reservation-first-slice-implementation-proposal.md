# Reservation first slice — implementation choices for review

**Current status: WORK IN PROGRESS, not ready for live bookings.** The user approved the final database evaluation checkpoint on 10 November; see [current cutoff](../../05-project-management/decision-log/2026-11-10-reservation-evaluation-checkpoint.md). This replaces the candidate/commit-metadata design. Service-local code, once-per-workflow binding, durable Facility dependency and producer ownership remain. [Remaining tasks](../../05-project-management/handoffs/2026-11-10-reservation-completion-checklist.md) distinguish unfinished Operations work from teammate producers.

## Established implementation direction

### R-ARCH-05 — approved durable Facility dependency and availability

Facility and Operations now implement the reverse acquire/complete/abort protocol in this draft. The user selected Facility's durable slot-management pattern, **not a full mirrored resource catalogue**. Each workflow stores immutable resource evidence for admission and a separate confirmation dependency. Facility owns resource configuration; Operations owns claims, occupancy, quotas and confirmation. See [the protocol](../../05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md) and [event/SignalR handoff](../../05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md).

The dedicated Operations-signed RS256 service JWT validates issuer, sender, subject, Facility audience, expiry and reservation-handshake permission. UserService's private signing key and the old shared service key are not reused. Setup is documented in [LOCAL-SETUP section 5.7](../../../LOCAL-SETUP.md#57-dedicated-operations--facility-service-jwt-reservation). The user approved Facility JwtBearer 10.0.12 and MassTransit 8.5.7 dependencies; only JwtBearer is needed in Facility for this implementation. Facility's existing durable ready outcome bridges to the Operations transactional outbox, so no second Facility bus is added.

MVP eligibility requires active tenant/site, existing non-deleted unit ancestry and a compatible operational slot. It does not calculate entrance/path reachability. Closure, structure edits and physical slot claims coordinate through the Facility site gate. A HELD dependency prevents incompatible writes until exact completion or fenced abort; elapsed time alone cannot release an unknown effect. Operations makes these remote calls outside its database transactions/site/plate guards. Persisted READY outcomes recover independently of terminal booking receipts.

Operations evaluates interval boundaries using distinct resource identities, deduplicates linked visits and occupied backups, applies explicit site/category quota from `operational_capacity_configs` and any narrower limits, and enforces global canonical-plate conflicts. The first committed payment hold protects its selected slot against overlap even when other slots are free. Later failure/reallocation does not become an exact-slot guarantee. Account cancellation restrictions use database policy and business evidence; unclassified legacy cancellations require explicit deployment reconciliation. There is no guessed quota or cancellation reason.

Application MediatR handlers own the workflows; Domain owns dependency/availability rules; repositories and UoW own persistence. Existing structure synchronization and edit orchestration are Application handlers, not forwarding services in Persistence. Gate slot claims now use a repository and handler-owned IUnitOfWork transaction. Only original service schema declarations change; no full projection, SQL overlay, new trigger/function or audit repository.

Status: **implemented draft, build/runtime UNVERIFIED**. Payment producer delivery and the next Operations commercial-stage/recovery commit still prevent live booking. Paid reallocation/refund/scan/checkout and the SignalR relay remain handoffs, not claims of implemented behavior.

Use the five existing Operations projects. API binds/maps/dispatches; direct MediatR handlers own use cases; aggregate repositories and UoW own reads/commits. Domain owns pure interval/state rules. Move existing gate/structure workflows out of stores and delete the forwarding StructureSafetyService; retain pure Domain policy. Shared authorization and external adapters remain legitimate ports.

Original `03b-reservation-operations-service-db.sql` only: declare final columns/constraints/indexes idempotently, no overlays, new functions/triggers or noncircular ALTER. Existing Operations trigger removal must land with C# replacement and complete competing-writer coverage; do not remove protection first. Do not change the separately approved UserService authentication binding trigger/role seed.

## R-ARCH-01 — authentication behavior retained; shared packaging superseded

**Historical approved proposal (packaging now superseded):** an isolated shared consumer authentication component, initially integrated into Operations only. It uses Microsoft JWT bearer middleware and a bounded asynchronous JWKS cache against the configured UserService public-key URL. UserService's local current-session validation is not copied into consumer TOKEN reads. Future Facility/Payment adoption is separate work.

Proposed defaults requiring security approval: 2-second download timeout; cache freshness 300 seconds matching the issuer header; zero stale-key grace after freshness expires; at most one shared refresh in flight per issuer; 30-second refresh-failure/unknown-key backoff; 30-second JWT clock skew. Cache a fully validated immutable RSA/RS256 set, never replace valid keys with malformed/empty data. Unknown key fails closed. No network in constructors/host startup; health remains live during outage. Before usable trusted keys, protected requests return sanitized dependency 503; invalid signatures/claims return 401 with bearer challenge. Never follow token-supplied URLs. Production trust transport must be HTTPS or an explicitly protected internal channel.

Alternative: keep code in Operations Infrastructure initially and extract after a second consumer adopts it. Smaller project footprint, but risks diverging retry/cache/error policy across services. Recommendation is a reusable component because the coding guide identifies the same consumer startup boundary for all three services. A project boundary/name and these numeric/error defaults require approval before implementation.

## R-ARCH-02 — current-access message contract and broker trust

**Historical approved proposal (packaging now superseded):** a small versioned, transport-independent shared contracts project, with Account-current request/decision DTOs owned by UserService. Operations owns its workflow, receipts and attempt scheduling; UserService owns durable per-workflow current-access evaluation/replay (as amended below). Consumers dispatch IMediator and use repositories/UoW, never put the workflow in a transport consumer or generic feature store.

Use the existing one UserService bus and add Operations EF Bus Outbox on ReservationDbContext; EF consumer inbox/outbox for atomic deduplication/replies. Proposed durable endpoint names: `user-account-access-v1` (UserService), `operations-account-access-results-v1` (Operations). Request/reply payload fields are in the [API proposal](../../03-design/api/reservation-first-slice-api-design.md#cross-owner-interfaces-to-agree). Saga owns the three attempts and 90-second original deadline per ADR §9; no delayed-redelivery multiplier.

Require broker credentials/ACLs that restrict request and outcome publication to the responsible services and queue consumption to the owning service. A matching message type/correlation is not proof of a trusted sender. Do not reuse the binding-lookup shared-key exception as general approval for broker or financial trust. Provision durable queues/bindings before enabling relay; capture-only mode may retain outgoing messages but cannot be advertised as live authorization. Keep unfinished work/messages; no silent TTL drop. Outbox/inbox retention and error-queue operations need deployment ownership.

Alternative: owner-published versioned contracts as a separate package instead of a local shared project. Better independent release boundaries, but needs package publication/version governance that is absent here. Neither option permits Operations to reference UserService's whole Application assembly or invent a second custom outbox relay.

## R-ARCH-03 — authoritative facts and concurrent owner changes

**Recommended proposal:** keep the existing structure prepare/complete handshake as the starting point; make Operations acquire the site guard before state reads and require every Facility closure/retirement writer to participate. Add versioned quote/coverage acceptance contracts with their owners; do not translate the checkout-price calculator into a guaranteed reservation quote.

Within Operations: proposed lock order is Account restriction → stable vehicle → site → reservation, with deterministic ordering for multiple IDs. Readers reload tracked state after waiting. All creation/admission/recovery/expiry/cancel/structure writers must share the relevant protocol; structure-only work must never acquire Account/vehicle after site. Physical occupancy remains blocking regardless of projection horizon. Interval exclusion covers known vehicle identities across sites and active unpaid/confirmed/allocated claims; nullable legacy identities require explicit enablement review. The physical allocation exclusion remains.

Cross-owner facts cannot be made atomic merely by local version comparisons. Needed decisions: who issues/binds/consumes a quote, when it expires, how coverage cancellation serializes with dependent booking acceptance, and how binding removal/transfer prevents a stale accepted CAR. Missing producer/handshake means the affected sale/finalization stays gated, not a guessed default. No temporary fake/free quote in a live path.

## R-ARCH-04 — final evaluation checkpoint, approved 10 November

Use one database evaluation timestamp under the required guards, strictly before T, then persist confirmed state/receipt/dependency outcome/outbox together. Expose only after durable commit; no second deadline check at commit. Read committed state to recover lost acknowledgement. Rollback requires a fresh evaluation. The [dated decision](../../05-project-management/decision-log/2026-11-10-reservation-evaluation-checkpoint.md) supersedes the former recorded-commit proof and candidate designs; their implementation has been removed.

## Rollout and bypass disclosure

On a fresh aligned schema, C# replacements must enforce site/slot acceptance guards and first-confirmation immutability via the coordinated repository/UoW paths. Direct SQL, bulk EF updates, direct DbContext saves, database cascades and any uncoordinated legacy writer can bypass application-only protection. Preserve database FK/check/uniqueness/exclusion constraints for enforceable invariants. Inventory and test all competing production writers before enabling the new path.

CREATE IF NOT EXISTS neither upgrades old definitions nor removes installed triggers. No existing-volume transition/reset is authorized. Supply the exact declaration diff and a schema inventory to the deployment owner; deployment testing uses an isolated aligned environment. Keep historical evidence separate from the post-refactor tests.

## Work that can proceed independently

Approved requirements/traceability, pure interval/deadline/eligibility rules and their unit tests, source audit, reviewable interfaces and deployment test instructions can proceed. Service-local JWT settings, service identity, Facility handshake, quota source and selected-slot protection are approved. Payment wire delivery and durable commercial recovery remain required before live booking.

## Subsequent user decisions — 9 November

Implement the approved shared authentication and Account-current foundations. Publish commercial producer contracts for their owners; do not implement fake quote/coverage providers. Historical cutoff gating was replaced by the 10 November evaluation-checkpoint decision. Persist this task’s configurable business policies with a separate idempotent default seed; do not build the teammate-owned full catalogue. Policy table extension was presented for approval separately.

## Scope clarification and subsequent corrections — 9 November

R-ARCH-02 approved UserService-owned durable decisions. The subsequent one-evaluation amendment below now makes `account_access_decisions` one row per workflow, not per attempt. UserService evaluates its own User/Account state once durably and replays the result through its outbox. Operations owns the reservation request and delivery deadlines. The workflow fingerprint binds actor/operation/original admission and overall deadline; delivery attempt fields are deliberately excluded. This table is not an audit store or booking aggregate.

No decision-record deletion policy is implemented. Broker inbox duplicate detection alone does not establish durable business replay for a separately redelivered logical attempt. A future retention design must coordinate the original deadline, replay horizon and pending outbox records before purging evidence. This task does not add general authorization management APIs.

The user approved the reservation-only extension of `operational_policies`: platform/site revisions, effective periods, explicit override bounds and accepted snapshots. The separately authorized default seed leaves late-arrival enablement unset; each site must explicitly choose. No full catalogue management API, invented site configuration or in-memory business-default fallback is added. The policy entity lives in Operations Domain; JSON/EF conversion remains in Persistence.

The user corrected audit ownership: audit history belongs to another service. Remove the newly introduced Operations audit repository and publish durable events; the consuming teammate owns ingestion and retention. The user approved the versioned event and fail-closed behavior: unconfigured audit publishing returns 503 for Gate writes; configured publishing captures events in the owning PostgreSQL outbox even during broker outages. The consumer and retention implementation remain delegated. The later service-independence amendment below removes the previously approved shared projects.

## One evaluation per reservation workflow — user amendment, 9 November

The user explicitly selected **“Exactly one evaluation per workflow; replay its result.”** This reservation-specific decision supersedes the earlier per-attempt implementation and the global ADR Profile B fresh-evaluation rule for this slice. Other services/workflows retain their existing policy.

- UserService uses a transaction-scoped WorkflowId lock and a unique `workflow_id` constraint. The table retains DecisionId, WorkflowId, trusted User/Account, role, operation, logical-request fingerprint, Allowed and original DecidedAt. AttemptId/AttemptNumber are no longer stored in this table.
- A delivery with no persisted decision evaluates and records it. Later deliveries replay that same result even if the Account has since changed. A workflow ID with a different actor, operation or original time budget is rejected.
- If an evaluation finishes after the current delivery window, retain the completed decision; a later timely attempt can retrieve it. An already expired incoming attempt does not start an evaluation. The original 90-second deadline still prevents late business progression.
- Operations retains delivery retries (up to three envelopes, 5-second reply windows and 10/30-second waits). It accepts only the current envelope before its deadline, but the original decision timestamp may precede that delivery attempt. An old envelope cannot advance the workflow.
- Reply MessageId is the delivery AttemptId, while payload DecisionId/DecidedAt stay fixed. Otherwise inbox deduplication by the original DecisionId could suppress a valid later replay.
- This is **one durable logical result**, not a promise that a database read executes physically once under every failure. A crash/rollback before the first result is committed can cause the read to run again because no durable result exists. Once committed, later attempts must not re-evaluate it. Concurrent commit/restart behavior requires the deployment checks.

The table now has a direct purpose under the user's once-per-workflow requirement: transport inbox deduplication is keyed by MessageId, while the workflow spans different delivery IDs. Persisting the original decision prevents those deliveries from becoming new evaluations. No purge policy or generalized access-management API is introduced. Retaining evidence and handling an existing deployment of the older per-attempt schema require owner review; this task only edits the original fresh-schema declaration.

## Service-local integration — user amendment, 9 November

The user explicitly prioritizes service independence and requests removal of `src/Shared`. The authentication implementation now lives in Operations Infrastructure `Authentication/`; the API registers it. The previously approved JwtBearer 10.0.12 package moves into Operations Infrastructure without changing its version or security policy.

Operations Application owns local `Contracts/AccountAccess/AccountAccessV1.cs` and `Contracts/Audit/OperationsGateAuditV1.cs`. UserService Application owns a separate authorization wire-contract definition under its own `Contracts/AccountAccess/`. There are no shared projects, linked source files or inter-service production project references. The common `SmartParking.Contracts.*` C# namespace is retained only as the existing MassTransit wire identity; it does not denote a shared assembly. Changing that namespace/type name would require a deliberate message-version transition.

Each service compiles its own types and communicates through the existing RabbitMQ queues/events. The cross-service test harness checks wire identity/JSON compatibility and uses an assembly alias to distinguish identically named local types. This test-only reference arrangement does not couple production builds. The audit consumer owner implements its own compatible wire type; no dependency on Operations Application is required.

The solution removes the shared project entries. Docker restore/publish copies only the selected service source; all its project references stay under that service directory. No package feed, schema generator, new shared replacement project or Facility adoption is introduced. Independently maintained DTO definitions now require compatibility checks when changing message contracts. Live broker compatibility and isolated publish still require deployment verification.

## Booking-through-confirmation continuation review — 9 November

Status: **PROPOSED implementation details; approval/owner contracts required before dependent code.** The user resumed after committing `0a2383bdfedf34058c4a2bae99c1ae9087162da0`, reported a successful build, and deferred test execution. The user selected booking through confirmed reservation for this phase and retained producer implementations with teammates. This section narrows delivery scope, not product requirements. No new frontend changes are authorized.

### Delivery boundary and current dependencies

Operations can own review/admission, immutable accepted terms, guarded vehicle/resource claims, pending payment, hold expiry, consumption of verified money, confirmation and own receipt/detail. Cross-service I/O belongs in Infrastructure adapters; commands and workers dispatch direct MediatR handlers using repositories/UoW. All stateful business types belong in Domain. PostgreSQL owns persisted policy revisions/snapshots, claims and workflow progress; existing original Operations schema plus the approved separate defaults seed are the only schema/seed locations. No in-memory business-policy fallback, extra SQL overlay, new trigger/function or shared source project is proposed. Audit evidence uses the outbox and owner-consumed events, not an Operations audit repository.

The earlier full cancellation, allocation/recovery, facility-failure and no-show lifecycle is outside this next delivery's user-selected scope. Payment hold expiration and handling verified money that cannot produce entitlement remain necessary here; cancellation/refund producer execution still belongs to its owner. A confirmed record alone is not proof that gate entry, parking allocation or the later lifecycle is delivered.

| Needed owner contract | Current source evidence / missing guarantee | Operations integration boundary |
|---|---|---|
| User verified own-CAR binding | `POST /internal/vehicle-bindings/resolve` exists with the specifically approved service-key trust. A lookup is not a lease against concurrent binding removal/transfer. | Reuse exact contract; establish which competing binding writers exist and how acceptance is fenced before claiming atomic ownership checks. |
| Facility eligibility/configuration | Structure prepare/complete coordination exists. Public layout and gate immediate-claim APIs do not by themselves establish a reservation-specific versioned eligibility snapshot or participation by every closure writer. | Owner supplies authoritative site/slot/type/status/version and coordinates changes through the Operations guard. Never reserve by calling the physical gate claim API. |
| Payment quote/coverage acceptance | Pricing calculation exists; no delivered versioned reservation quote/acceptance contract found. Coverage dependencies need coordinated cancellation. | Retain published quote/coverage proposal; implement only an agreed owner interface, with unavailable/error distinct from valid zero price or explicit no coverage. |
| Reservation deposit order | Generic `CreatePaymentOrderCommandHandler` accepts the request amount and uses `now + 15 minutes`, not supplied H; current idempotent replay does not compare the requested payload. | Owner must bind order to accepted quote/terms, reservation, Account/tenant/site, method, amount/currency and exact H; mismatched replay conflicts. Do not adapt these missing guarantees away in Operations. |
| Verified collection/reconciliation | Existing `PaymentSucceededV1` has transaction/order, amount and scope but no accepted terms version. Status lookup is by transaction ID; unknown order creation needs lookup by durable operation ID. | Agree versioned event and lookup, identity/ACLs, deduplication and recovery. Browser redirect/demo-pay cannot supply authoritative evidence. |
| Late or non-entitling money | Requires stable financial effect IDs and owner reconciliation/refund acceptance. | Persist one disposition/intent per distinct verified collection; never invent paid/refunded status. Missing owner blocks live collection enablement. |

No cross-owner database reads or producer implementation is authorized. If owners have newer contracts on another branch/document, inspect them before designing adapters. If not, the achievable independent deliverable is the Operations workflow with explicit integration gates and a producer handoff; a real confirmed paid booking cannot be claimed until those producers are available.

### Proposed browser/payment interaction

1. Before proceeding to payment, obtain server-issued terms for the complete vehicle/time/slot/method intent. Loading terms/availability is compatible with the user's two principal submission points. A displayed browser estimate is not accepted pricing. Changed money, material terms or selected inputs require renewed review/acceptance.
2. The first submission uses the existing durable request endpoint and stable idempotency key, including accepted terms and selected payment method. Exactly one persisted actor authorization result governs this workflow; delivery retries do not re-evaluate it. No claim/order before Allowed.
3. After guarded acceptance, return pending progress plus the real owner-issued order/checkout information when ready. Do not keep an HTTP request open across the authorization Saga or label 202 as a reservation confirmation. Receipt polling/resume handles refresh and ambiguous network responses.
4. After the payment interaction, the browser reads the same own receipt/status. Payment's verified event or trusted server reconciliation advances Operations independently of that browser call; closing the tab cannot strand an otherwise valid paid workflow. A redirect's success flag is ignored as payment proof. If payment/commit evidence is still pending, return pending and poll; redirect/show confirmed only on authoritative confirmation.
5. Genuine owner-issued zero-price terms skip collection but still require resource acceptance and D4c. A positive deposit requires verified collection, regardless of method name. Cash collection is not inferred from selecting CASH; a teammate-owned, authenticated collection contract must support it before it is offered. No deferred-payment bypass is introduced.

This proposal keeps the existing form input order. Frontend teammates will receive the exact endpoint/payload/status handoff after backend contracts are fixed. Own booking routes must avoid the existing Operator `/api/reservations` route; proposed own routes are `/api/me/reservations` and `/api/me/reservations/{id}` with Account filtering, while current receipt routes remain unchanged.

### Confirmation persistence and recovery — current

The final checkpoint time decides viability; successful durable commit decides visibility. No candidate transaction, commit metadata or post-commit activation worker is required. Persist the timestamp and stable effect once; after unknown acknowledgement resolve the same workflow before attempting again. Database/worker fault injection must prove rollback, lost-response and concurrent expiry behavior.

### Implementation sequence after decisions

1. Agree owner contracts and service authentication; document actual endpoints/events and readiness. No HTTP shared-key exception beyond the existing vehicle lookup is inferred.
2. Extend only the original Operations schema for persisted accepted terms, claims and workflow/effect evidence; map business entities in Domain and repositories in Persistence. Retain existing task policy revisions/default seed and explicit site late-tolerance choice. No full policy-management catalogue.
3. Implement review and guarded admission progression, all competing claim writers, resumable order creation, own receipt/detail and expiry with known-vs-unknown outcomes. Missing producer is explicit unavailable, never fake success.
4. Consume trusted financial outcomes idempotently and implement the approved D4c evaluation-checkpoint path. Reconciliation cannot create a second charge or refund. Existing Gate/structure guards must respect committed and uncertain outcomes.
5. Update API/Postman examples, frontend handoff and deployment checklist; no frontend implementation. Test execution stays deferred as requested. Record a fresh build for subsequent code; the user's first-slice build does not validate new code.

Later deployment acceptance must include a successful real paid and genuine zero-price booking; duplicate/unknown order effects; transaction held across T; lost commit acknowledgement and restart; atomic confirmation/outbox write failure; same vehicle across sites and competing holds; authority-version changes; provider success after H/T; and late verified money without entitlement. Frontend/Postman can drive the flow, but database/worker/broker fault injection is needed to establish cutoff and concurrency guarantees. Document these checks for the user rather than launching local services now.
