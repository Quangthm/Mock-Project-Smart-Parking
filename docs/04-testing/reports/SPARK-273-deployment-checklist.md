# Reservation first slice — deployment verification

> **SPARK-273 architecture alignment (11–12 November 2026).** This document includes historical implementation notes. For the approved workflow and confirmation boundary, see the [11 November decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md). Existing Facility confirmation checks remain required until the aggregate-only fast-path invariant gate is verified.

Status update on 12 November 2026: **Operations source is an integration baseline, not end-to-end ready.** The request workflow terminates at a durable `PENDING_PAYMENT` hold using receipt state `HOLD_CREATED` (contract version 2). A hold-changed consumer dispatches order provisioning or zero-price acceptance; a MassTransit reservation-deposit consumer, immutable binding validation and guarded paid/zero-price confirmation path are present and registered. The five-second due-work worker remains unchanged for expiry and recovery. The commercial adapter still fails closed; Payment producer/adapter compatibility and live delivery are unverified. Build results are recorded in the SPARK-273 session. All test, database, broker, provider and manual acceptance remains `NOT RUN — USER VERIFICATION REQUIRED`.

## Current scope and gates

Implemented source includes Operations-local JWT validation; admission/receipt and Account-current replay; database policy revisions; direct Gate/structure handlers and durable audit; preview/review/own-booking routes; one-time canonical-plate binding; plate-before-site Gate guards; the short creation receipt; durable order/expiry state; hold-event dispatch; a reservation-deposit consumer; persisted collection-ID idempotency; and guarded paid/zero-price confirmation with the Facility dependency. The current Payment commercial adapter is intentionally unavailable, so no successful Payment order is fabricated. The Payment producer, jointly agreed event identity/topology/ACL, a general EF Inbox on the Payment endpoint, reliable confirmation/decline outcome delivery, and complete late/additional-collection reconciliation remain unimplemented or unverified.

Live booking remains blocked at the fail-closed commercial adapter (`OWNER_UNAVAILABLE`). Remote order calls are staged outside guarded Operations transactions. Normal creation no longer polls Payment collections or waits for confirmation. Preserve the existing Facility confirmation dependency and strict November 10 `evaluatedAt < T` cutoff; the aggregate-only fast path is not proven. Do not invent fee defaults or treat `PENDING_PAYMENT` as proof that a hold remains valid. The local reservation-deposit consumer's business idempotency uses the locked reservation state and `CollectionId`; it does not currently configure MassTransit EF Inbox middleware.

## 1. Compile and focused tests

The source implementation and Postman artifacts can be statically checked independently of unavailable services. Do not call a local build or unit test evidence of live Payment, PostgreSQL, RabbitMQ or provider acceptance.

From the repository root on a machine with .NET SDK 10 and restored package access:

```powershell
dotnet restore SmartParking.slnx --nologo
dotnet build SmartParking.slnx --no-restore --nologo
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --nologo --filter 'FullyQualifiedName~ReservationFirstSliceRuleTests|FullyQualifiedName~ReservationAdmissionRuleTests|FullyQualifiedName~ReservationPolicyTests|FullyQualifiedName~ConsumerJwksCacheTests|FullyQualifiedName~ReservationGateUseCaseTests|FullyQualifiedName~ReservationEfModelMapsExistingOperationsTablesAndKeys'
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-build --nologo --filter 'FullyQualifiedName~AccountCurrentAuthorityTests|FullyQualifiedName~AccountCurrentWorkflowTests|FullyQualifiedName~ServiceLocalAccountAccessContractTests|FullyQualifiedName~StructureHttpContractTests'
```

Add `FullyQualifiedName~ReservationEvaluationCheckpointTests` to the focused Operations/Facility test filter for the new checkpoint rules. Run the updated Gate handler tests too: they now expect plate guard before site guard. These tests are authored/updated but NOT RUN; pure Domain tests cannot establish guarded checkpoint/commit recovery or cross-process locking.

Also include `ReservationFacilityDependencyTests`, `ReservationAvailabilityTests` and `FacilityStructureEndpointTests` in the focused filter. The Facility v1 producer and Operations adapter are in the Commit 1 draft. Use the dedicated service JWT and verify exact-token abort fencing, lost replies, concurrent deletion and recovery. See [the Facility and event handoff](../../05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md).

### Manual checklist for current Operations source and Payment handoff

**On the current branch**

- [ ] Build the current checkout and run the focused tests above. Record the SHA plus working-tree diff and any skipped checks.
- [ ] Follow [local setup sections 5.6–5.7](../../../LOCAL-SETUP.md). Use isolated aligned schemas, the existing policy seed, an explicit site late-tolerance choice, and an active CAR quota for the test site. Bind test subscriber queues before enabling the Operations outbox relay.
- [ ] Start User, Facility and Operation with `scripts/run-schema-service.ps1 -Service <name>` in separate terminals. Prepare an active tenant/site, live unit, two compatible operational slots and an active Driver with an eligible CAR.
- [ ] From an authorized service test client, call Facility's v1 read and acquire routes. Verify active and unavailable slot facts, wrong credentials returning 401/403, exact-token replay, changed payload returning 409, edit blocked while HELD, complete/abort replay and delayed acquire after abort staying ABORTED. Use the [route, request and response examples](../../03-design/api/reservation-facility-dependency-api-design.md).
- [ ] Make a compatible Facility structure change. Verify one Operations applied event reaches a bound queue. Replay the same completion and verify no second fact. Pause delivery, make another compatible edit, then resume and verify retained delivery.
- [ ] With admission enabled, call `POST /api/reservation-price-previews` and `POST /api/reservation-terms` with the [valid Driver payload](../../03-design/api/reservation-first-slice-api-design.md). The checked-in commercial adapter returns 503 `OWNER_UNAVAILABLE`; this is the current fail-closed result.
- [ ] With an approved real Payment adapter, create using reviewed terms and a stable `Idempotency-Key`. Expect the receipt to reach terminal `HOLD_CREATED`/contract version 2 at hold commit while the reservation remains `PENDING_PAYMENT`; verify hold-event order/zero-price dispatch and confirm the five-second worker is only the existing recovery path, not a durable scheduled command.

**After Payment producer delivery and owner contract agreement**

- [ ] Review the Payment team's actual implementation, including PR #98 / SPARK-391, and agree exact versioned quote, `EnsureOrder`, zero-price, collection-event, identity/topology/authentication, lookup, outcome and reconciliation contracts. Wire the real Operations adapter before attempting a real payment.
- [ ] With the same Driver, preview the price, review terms, submit exact terms to `POST /api/reservation-requests` with a fresh `Idempotency-Key`, and poll the returned Location. Expect 202, then receipt `HOLD_CREATED` and own reservation `PENDING_PAYMENT`. Read `GET /api/me/reservations/{reservationId}` for real order-provisioning state and an order ID only after Payment returns one.
- [ ] Before paying, submit an overlapping request from another eligible Driver/plate for the same selected slot. Expect one committed hold and one `SLOT_TIME_CONFLICT` even with another free slot. Repeat separately for quota, cross-site plate conflict and a payment hold that has expired. Compare durable events with committed state.
- [ ] Pay only a real returned order through the configured test provider. Payment status alone is not Operations confirmation. Verify the producer emits the locally expected V1 DTO/envelope and identity to the configured endpoint, then verify Operations reports `CONFIRMED` only after guarded pre-T evaluation commits. Exercise duplicate delivery, no browser return, expiry and late/additional collection using the cases below.

### Through-confirmation deployment acceptance additions

Use an isolated, aligned database and actual restricted application role. Preserve existing-volume data; applying the original CREATE declarations does not upgrade old tables, remove deployed triggers or repair previous test fixtures. No reset or overlay is authorized. The deployment owner must first reconcile the schema inventory and original-script diff.

1. No commit-timestamp tracking or special metadata privileges are required. Use the normal Operations database role and confirm durable transaction behavior (`synchronous_commit=on`). Check final schema contains confirmation_evaluated_at and stable effect ID, with no candidate lifecycle. Inventory/reconcile any previously installed candidate schema/rows; CREATE declarations do not migrate them.
2. Verify accepted reservation policy rows/default seed and explicit site late-tolerance choice. Read-only form review must return actual policy values/versions; missing rows must return unavailable. Change effective policy/quote between review and acceptance and require renewed terms. No in-memory fallback or fabricated quote.
3. Log in as a real Driver. Preview a valid CAR slot/time before selecting method; complete the form, review, submit exact terms with one key, and poll receipt/own booking. Use the real resulting Payment order. Verify paid confirmation and a genuine owner-issued zero-price case. Do not use demo-pay, fake QR values or fabricated terms as evidence.
4. For T=11:00, pause after successful guarded evaluation at 10:59:59 and commit at 11:00:02: confirmation must succeed and remain invisible until commit. Separately hold a competing lock until T so evaluation starts at/after T: reject. Verify exact equality and microsecond boundaries. No second deadline check or commit metadata lookup may be needed.
5. Roll back a pre-T evaluation and retry after T: reject using a fresh timestamp. Lose the successful commit acknowledgement and restart after T: recover the same committed receipt/effect, with one outgoing fact and no extra charge. Verify booking, receipt, Facility READY and outbox rollback/commit atomically.
6. Test paid and genuine zero-price paths with commit tracking off. Gate/read APIs must not expose uncommitted confirmation. After successful commit, expiry must preserve the confirmed booking. Test database outage and ambiguous commit recovery; never retry a new effect solely because the original response was lost.
7. Race two sites for the same canonical plate, two holds, an existing guest visit, structure changes and physical entry. Verify guard order and database/UoW paths; a future reservation by itself must **not** reject walk-in. Validate capacity by distinct resource identities and half-open interval segments using the implemented R-ARCH-05 Facility snapshot and actual claims/visits.
8. Exercise created/unknown/definitively-no-order outcomes at H; exact-payload replay; contradictory collection payload; additional collection after receipt completion; late collection at/after T; producer outage during reconciliation. The current terminal-notification gap must be closed before this check can pass. Reconciliation acceptance is not refund payout evidence.

Record Git SHA plus uncommitted diff identity, commands, pass/fail/skip counts, actual producer versions, server settings and redacted evidence. Raw SQL, bulk updates, direct DbContext saves, cascades and uncoordinated writers can bypass C# guard/history checks; include every supported writer, and disclose any unverified bypass. Frontend is not modified by this task; [frontend handoff](../../05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md) is the client backlog.

Stop on any build/test failure; report the first error and test summary without secrets. These tests verify pure rules, middleware adapters where present, and in-process handler behavior. They do not prove PostgreSQL transactions, broker delivery or live JWT challenge handling. Do not interpret skipped tests as passes.

## 2. Fresh isolated schema and policy defaults

Only use newly created disposable databases or a deployment-owner-confirmed aligned schema. **Do not run this as an existing-volume upgrade.** CREATE IF NOT EXISTS does not upgrade existing tables or remove old triggers. No reset, DROP/ALTER upgrade overlay or production transition is supplied. Existing audit rows remain untouched in an existing database; their migration belongs to the owners.

Use PostgreSQL client connection configuration through your secure environment/pgpass, with the database host/port/user pointing to your isolated deployment. The following database names are examples of service databases, not an instruction to recreate them:

```powershell
psql -X -v ON_ERROR_STOP=1 -d smartpark_user -f scripts/database/microservices/01-user-access-service-db.sql
psql -X -v ON_ERROR_STOP=1 -d smartpark_operations -f scripts/database/microservices/03b-reservation-operations-service-db.sql
psql -X -v ON_ERROR_STOP=1 -d smartpark_operations -f scripts/database/seeds/03b-reservation-policy-defaults.sql
psql -X -v ON_ERROR_STOP=1 -d smartpark_operations -f scripts/database/seeds/03b-reservation-policy-defaults.sql
```

Run each owning declaration script a second time on the same disposable database. Expected: no declaration error, no data loss, one fixed global seed revision, identical activation timestamp and values after seed replay. Verify `operational_policies` has platform/site scope, revision/effective dates and bounds; `reservations` has accepted snapshot and active-claim columns; inbox/outbox tables match EF mappings. Fresh Operations must have no `guard_structure_edit` or `record_reservation_confirmation` trigger/function and no `audit_logs` table. UserService must retain its approved session-account immutability trigger and idempotent role seed.

Create a site policy revision through your controlled test-data process using actual tenant/site IDs and an explicit `arrival_late_tolerance_enabled` choice. **No site seed or default enablement is supplied.** Null/missing choice must fail policy resolution. Other unset site values inherit. The seed only supplies approved arrival tolerance bounds (15–30 minutes); other changed values require explicitly configured approved platform bounds. Test platform revision changes leave already accepted snapshots unchanged; direct SQL remains a bypass and is not a supported policy-management API. This task does not implement the full catalogue or purge old referenced revisions.

## 3. Messaging and audit provisioning

Before enabling outbox delivery, the deployment/audit owners must create durable, non-auto-delete topology and least-privilege service credentials in the chosen RabbitMQ vhost. No end-user can publish authorization decisions or audit evidence.

| Purpose | Destination |
|---|---|
| Operations sends Account-current requests; UserService consumes | `user-account-access-v1` |
| UserService sends decisions; Operations consumes | `operations-account-access-results-v1` |
| Operations hold trigger; Operations consumes | `smartpark.operations.reservation-hold-changed.v1` → `smartpark-operation-reservation-hold-changed-v1` |
| Payment verified reservation collection; Operations consumes | `smartpark.payment.reservation-deposit-collected.v1` → `smartpark-operation-reservation-deposit-collected-v1` |
| Operations committed booking confirmation; subscribed consumers | `smartpark.operations.reservation-confirmed.v1` |
| Operations publishes Gate audit events, durable fanout exchange | `smartpark.operations.gate-audit.v1` |
| Audit owner predeclares durable queue bound to that exchange | `smartpark.notification-audit.operations-gate.v1` |

Also account for MassTransit endpoint exchanges, bindings and error/skipped queues in ACLs. Operations may send requests/read decisions/publish its audit exchange; UserService may read requests/send decisions; the audit consumer may read its own queue. UserService's existing profile/access topology must remain authorized. Verify the actual ACLs with unauthorized credentials; merely providing different usernames is not proof of isolation.

**The audit queue must be bound before publishing/relay starts.** A durable exchange without a bound queue does not retain messages for a future consumer. The event contains only EventId, OccurredAt, ActorUserId, TenantId, SiteId, Action, nullable ReservationId and ParkingSessionId. No plate/image is included. The audit owner deduplicates by EventId, tolerates redelivery and owns long-term retention; Operations outbox cleanup is not audit retention. Event ID equals broker MessageId; correlation is ReservationId or the walk-in session ID. No audit consumer is added here.

Configure through the existing `.env`/setup/host runner or Compose mapping, keeping secrets out of shell transcripts:

- `SMARTPARK_RESERVATION_ADMISSION_ENABLED=true` for the admission experiment on UserService and Operations.
- Separate `SMARTPARK_RESERVATION_USER_BROKER_USER/PASSWORD` and `SMARTPARK_RESERVATION_OPERATION_BROKER_USER/PASSWORD`; blank credentials fail startup when required.
- `SMARTPARK_OPERATION_AUDIT_ENABLED=true` for effectful Gate writes. It can be enabled without reservation admission.
- `Messaging__Enabled=true` to register the reservation-deposit consumer (and the existing gate-payment consumer); reservation delivery also requires `SMARTPARK_RESERVATION_ADMISSION_ENABLED=true` so the Operations Bus Outbox and hold consumer are registered.
- `Messaging__OutboxDeliveryEnabled=true` only after topology is ready. False captures durable messages but intentionally pauses delivery; monitor backlog and disk capacity.
- `SMARTPARK_RABBITMQ_HOST`, `SMARTPARK_RABBITMQ_PORT`, `SMARTPARK_RABBITMQ_VHOST` and least-privilege broker credentials (`SMARTPARK_RESERVATION_OPERATION_BROKER_USER/PASSWORD` mapped to `ReservationAdmission:BrokerUser/Password`, or the supported `SMARTPARK_RABBITMQ_USER/PASSWORD`). These settings select credentials; they do not provision broker users, queues, bindings or ACLs. Never include secret values in the checklist or command transcript.
- Ensure the Operations database has the schema for the same `ReservationDbContext`, including the MassTransit EF outbox entities. Enabling relay without durable bound consumer topology can lose delivery to future subscribers; `Messaging__OutboxDeliveryEnabled=true` is a deployment gate, not a topology creator.
- Existing broker host/port/vhost and owning database connections.
- Existing JwtValidation issuer/audience/JWKS URI mappings. Only use `SMARTPARK_RESERVATION_ALLOW_HTTP_JWKS=true` for an explicitly protected/local HTTP environment; otherwise HTTPS is required. Never distribute UserService's signing private key to Operations.
- `SMARTPARK_OPERATION_CORS_ORIGINS` for the actual frontend origins.

Run UserService and Operations in separate terminals using your existing runner:

```powershell
./scripts/run-schema-service.ps1 -Service User
./scripts/run-schema-service.ps1 -Service Operation
```

This document gives user-run instructions; the agent has not executed these starts.

## 4. Admission and JWT checks

Use a real Driver JWT and future booking instants. The API design contains the create body; vehicle/site/slot/terms IDs remain unverified intent at this gated checkpoint. POST `/api/reservation-requests` with a new UUID `Idempotency-Key`, then GET the returned Location with the same Account.

| Scenario | Required observation |
|---|---|
| Admission disabled | 503 `RESERVATION_ADMISSION_DISABLED`; no receipt |
| Missing/invalid/expired bearer, wrong issuer/audience/signature or malformed required claims | 401 envelope + Bearer challenge; no receipt |
| Valid non-Driver | 403; no receipt |
| Cold JWKS outage | Process stays live; protected request fails closed with 503, then recovers without restart |
| JWKS fresh-cache outage / expiry / unknown kid / concurrent refresh | Respect 300-second freshness, zero stale grace, 2-second download bound and 30-second backoff; no refresh stampede or arbitrary token URL download |
| Malformed body/key, wrong mode, empty IDs, end ≤ start or past start | 400; no receipt/outbox message |
| Valid intent | 202 only after receipt and authorization-request outbox commit |
| Same Account/key/body, including concurrent replay | Same receipt; one initial attempt; changed body under same key is 409 |
| Other Account reads receipt | Same 404 as an absent ID |
| Timely current Allowed | PREPARING, then one persisted binding result; missing/invalid/expired terms or owner dependencies cannot produce booking/claim/order effects with current unavailable integrations |
| Timely current Denied | `DENIED / CURRENT_ACCESS_DENIED`; no business effects |
| Timeout/late or old attempt/wrong correlation response | No late progression; at most 3 distinct attempts, 5-second reply windows, 10/30-second waits, original 90-second deadline |
| Restart after receipt commit/before delivery | Receipt survives; outbox and due work resume without duplicate booking effects |

For UserService, restart and request the same workflow using a new delivery attempt: exactly one `account_access_decisions` row and the same DecisionId, DecidedAt and Allowed result. Change Account state between deliveries; neither an original Allow nor an original Deny may be re-evaluated. Changed actor/operation/original deadline under the same workflow must be rejected. The current response envelope echoes the new AttemptId and uses it as broker MessageId so inbox deduplication does not suppress replay. An old envelope remains rejected by Operations; a timely current envelope carrying the original decision is accepted. Race two delivery attempts on separate connections: only one durable decision may commit. Crash after decision/outbox commit and before acknowledgement: replay must not re-evaluate. A rolled-back/uncommitted first evaluation may be read again because there is no durable result yet. Do not purge decision rows as an ad hoc test cleanup on shared databases.

## 5. PostgreSQL and audit atomicity

The existing structure suite creates and drops **only its generated disposable databases**, but needs an isolated server account with CREATE DATABASE permission. Set `SMARTPARK_STRUCTURE_TEST_CONNECTION` securely in your environment, then run:

```powershell
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --nologo --filter 'FullyQualifiedName~PostgresStructureWorkflowTests'
```

Expected: hold races yield one winner and a clean conflict, completed receipt replay cannot release a newer hold, supported UoW writes are blocked while a hold exists, failed capacity projection rolls back hold/state/receipt together. No test result has yet established this for the refactor.

Deployment checks that still require real PostgreSQL/RabbitMQ:

1. With audit disabled, attempt valid Gate entry/departure: 503 and unchanged persisted reservation/session/allocation state. Reads and idempotent completed departure replay remain available.
2. With capture enabled and relay paused, perform valid entry: one matching outbox event and one committed transition. Force transaction failure after event capture but before commit: neither persists. Inspect using deployment-owner tools, without publishing sensitive rows.
3. Stop the broker after successful startup and repeat a write: configured capture should succeed durably. Restart Operations and broker; the same EventId must reach the prebound audit queue. Consumer deduplication must yield one audit record after redelivery.
4. Replay departure: no additional transition or audit event. Rejected/mismatched plate/absent allocation must produce neither.
5. Race Gate writes and structure holds on separate connections. The site guard must precede reads; immutable tenant/site and original confirmation/snapshot fields must be enforced through UoW. Verify optimistic-version conflicts do not get silently retried as new actions.
6. Confirm requested-slot fallback cannot admit a vehicle or show it as an observed location. Verify current Facility compatibility and global canonical plate conflicts on both participating write paths.

## Safeguard bypasses and release conditions

Direct SQL, bulk EF updates, direct DbContext saves, cascades and any future writer that does not use the guarded UoW bypass the C# replacements. Database checks/FKs/uniqueness still apply, but these bypasses do not acquire the site guard, protect first-confirmation/snapshot history, or emit audit events. Restrict service write access and audit competing writers; no claim of database-wide enforcement is made.

`confirmed_at` and `confirmation_evaluated_at` record the same guarded decision time for new workflow bookings; neither claims the actual commit time. The old candidate/metadata design is removed. Verify post-checkpoint delayed commit, lock-wait crossing T, rollback/retry and lost acknowledgement; pure Domain tests alone do not establish transactional behavior.

Record checkout SHA plus dirty diff, test summaries, deployment versions and scenario outcomes in a report. Do not include credentials, JWTs, plates, images or raw provider errors.

## Service-local packaging checkpoint

The user removed the shared-project direction. Operations now contains the JWT/JWKS implementation and local message contracts; UserService contains its own compatible Account-current DTOs. The production project references stay inside each service folder. The test harness's cross-service references are not production dependencies.

Restore after this relocation; stale obj/assets files from the removed projects are not build evidence. In deployment CI, publish each API project separately (or use the existing Dockerfile with SERVICE=OperationService / UserService) with only that service's source available. No src/Shared copy or shared assembly should be needed. Run ServiceLocalAccountAccessContractTests and real request/reply replay before release. Preserved wire namespace/type names are intentional; schema/property changes still require consumer compatibility review.

## 11 November architecture acceptance — additional cases (NOT EXECUTED)

**Status:** unverified; this is an updated **test plan**, not a run report. [Architecture decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md), [cross-team handoff](../../05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md). Test the new workflow as designed before declaring SPARK-273 end-to-end ready. Retain and re-run the existing 10 November checkpoint tests.

### Request completion and payment preparation

- [ ] On an authorized reservation with confirmed capacity, record creation request terminal **at durable `PENDING_PAYMENT` hold**, with matching request receipt, exclusive claim, H and hold/outbox fact. Ensure `PENDING_PAYMENT` is not lifecycle `COMPLETED` or `CONFIRMED`. Preserve once-per-workflow Account-current auth and exact same-key replay.
- [ ] Immediately after hold commit, stop/restart Operations. The request remains terminal, and a stable independent order intent resumes without a multi-minute Saga or lost effect. Payment HTTP call occurs **outside** Operations guarded UoW/plate/site locks. Outage does not silently extend H.
- [ ] Review a specific versioned tariff/deposit, change active tariff before order preparation, then fail closed/release hold/request fresh review; do not silently charge new tariff. Test genuine configured zero amount, FULL/PERCENTAGE/FIXED with policy-catalogue bounds, changed/expired coverage evidence and missing tariff (not free).
- [ ] `EnsureOrder` identical concurrent `operationId`+payload returns one order/accepted quote. Same key with amount/account/quote/reservation/H mismatch → conflict. Lost response → `UNKNOWN`/same-ID recovery; at H with no order → durable `CLOSED_NO_ORDER`; already-created order remains replayable after H, with **no new checkout**. Payment order expiry exactly Operations H, never 15 minutes for reservations.
- [ ] Driver can initiate real payOS checkout only for their own order; wrong Account/role/order ID rejected. Persist attempted provider payment before external effect and replay genuine saved provider QR/URL. UNKNOWN cannot initiate another charge; only definitive retryable FAILED counts toward four total attempts. Check equality `now == H`.

### Event-driven confirmation and money handling

- [ ] Verified provider webhook creates one provider-correlated collection record + outbox event atomically. Duplicate same event/collection has no duplicate entitlement; distinct extra collections after order already PAID, terminal receipt or H expiry are separately recorded and notified; conflicting ID/payload fails closed.
- [ ] Provision RabbitMQ publisher/consumer ACLs so only the approved Payment producer can publish the agreed versioned message. Verify the producer's actual MassTransit message identity, envelope and event DTO against the Operations consumer; the checked-in consumer validates immutable order/reservation/Account/tenant/site/quote/terms/amount/currency binding before confirmation. Mismatches must not mutate entitlement. Consumer redelivery must yield at most one `CONFIRMED` transition/effect/outbox fact; verify the current `CollectionId`-based business idempotency and any separately approved durable inbox requirement.
- [ ] Confirm no per-second backend polling for normal pending payment. During broker interruption or lost event, targeted collection lookup replays authoritative financial facts. Unverified redirect/QR/frontend event never confirms.
- [ ] H expires without worker running: `Status=PENDING_PAYMENT` alone does not authorize late unguarded confirmation. Expired-H money at `evaluatedAt < T` attempts fresh Facility+Operations claim; success if compatible, reconciliation if a competitor owns it. At/after T no ordinary new confirmation, even if provider reports earlier payment. Distinguish collection accepted/reconciliation accepted/refund payout.
- [ ] Zero-price valid owner terms confirm without Payment order or simulated provider collection, under the same 10 November guarded checkpoint and capacity guarantees.

### Live-hold aggregate-only fast-path proof gate (must pass **before** omitting Facility confirmation acquire)

- [ ] Enumerate **every** supported claim-affecting mutation path: Operations claim/plate/site/quota/expiry/cancellation, Facility structural/site/unit/slot activation/closure/deletion, Gate physical entry and releases, maintenance activation, tenant writers, direct SQL and bulk EF calls. Record each writer's guard/coordination and supported deployment write policy.
- [ ] Under a **live** `PENDING_PAYMENT` hold, race Facility closure/deletion and Gate occupancy against the proposed aggregate-only confirmation. Prove no successful confirmation against silently invalidated eligibility. A delayed `FacilityStructureApplied` event must not make stale aggregate status appear authoritative. Test crash/replay, two-way network isolation, opposite-direction synchronization and token abort fencing.
- [ ] If and only if all supported writer tests/coverage succeed, validate locked aggregate-only confirmation with persisted claim/invariants, one DB evaluation timestamp and atomic outbox. Until then, leave current confirmation Facility dependency and full capacity/plate checks enabled. Record a signed-off gate result; documents alone are not evidence.
- [ ] Test expiry/reacquisition separately: expired hold cannot inherit prior slot exclusivity; a competing claim wins by existing guards. Before T a newly compatible claim may confirm with correct Facility dependency; after T must reconcile, not confirm.

### Cutoff, rollback and deploy proof

- [ ] At T=11:00, guarded DB `evaluatedAt=10:59:59`, durable commit=11:00:02 qualifies but only exposes committed state afterward. Lock wait reaching T before evaluation, equality at T, rollback then retry after T all fail. Lost commit acknowledgment uses the same effect/receipt identity, not a second collection/confirmation.
- [ ] Obtain actual build, focused integration tests, schema inventory and live payOS fixture evidence. Required Payment routes/event schema and separate machine trust must be deployed; configured environment alone does not prove it. Do not modify production/shared data or reset existing volumes as a shortcut.

## Deferred Implementation Backlog — Post Reservation/Payment Integration

The [implementation handoff](../../05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md) owns the full backlog entry for each item: current limitation, concrete failure/impact, existing protection, minimum proposal, owner, dependencies/approval, acceptance criteria and later verification. These items are **not implemented**:

- **BACKLOG A — Durable hold-expiry scheduling:** no durable command is scheduled at H; current authoritative deadline plus periodic due-work scan remains. Requires scheduler approval and restart/duplicate/equality verification.
- **BACKLOG B — Event-driven confirmation continuation and recovery:** due work is resumable by the database-backed worker, but Facility readiness has no complete independent durable event-driven continuation. Requires Facility transition review and agreement on the minimum durable trigger/recovery.
- **BACKLOG C — Deprecated worker removal:** keep `ReservationOrderProvisioningWorker`, its DI registration and `Due*Async` methods unchanged until every required responsibility has a safe replacement. Depends on A and B.
- **BACKLOG D — PaymentService confirmation-outcome integration:** real Payment adapter and agreed durable decline/outcome delivery are incomplete. Depends on review of PR #98 / SPARK-391 and the Payment team's actual implementation.

All scheduler/recovery/Payment/live acceptance and manual checks remain `NOT RUN — USER VERIFICATION REQUIRED`.
