# Reservation first slice — deployment verification

Status on 9 November 2026: **UNVERIFIED after the current refactor.** No application, database or broker was started by the agent. The last requested build did not execute because automatic approval review hit its usage limit. Earlier compilation and unrelated vehicle tests are not evidence for this checkout. Run the inexpensive compile/offline checks first, then use an isolated deployment for database/broker checks.

## Current scope and gates

Implemented source: Operations-local JWT validation; durable reservation admission/own receipt; bounded Account-current attempts and UserService durable decisions; database-backed policy resolution; direct Gate/structure handlers with repositories/UoW; durable Gate audit events.

Not implemented or not enabled as a complete booking workflow: authoritative vehicle/Facility acceptance; versioned quote/coverage producers; cancellation workflow; allocation/recovery/F/T scheduling; cross-site stable-vehicle conflict enforcement; durable refund intents; actual confirmation commit evidence. Pure rule tests do not establish these integrations. A timely Allowed decision deliberately ends `FAILED_DEPENDENCY / RESERVATION_FINALIZATION_NOT_READY` with no booking, claim or payment effect. Zero-price confirmation is also gated. Do not enable a booking-finalization path until D4c and owner integrations are reviewed.

## 1. Compile and focused tests

From the repository root on a machine with .NET SDK 10 and restored package access:

```powershell
dotnet restore SmartParking.slnx --nologo
dotnet build SmartParking.slnx --no-restore --nologo
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --nologo --filter 'FullyQualifiedName~ReservationFirstSliceRuleTests|FullyQualifiedName~ReservationAdmissionRuleTests|FullyQualifiedName~ReservationPolicyTests|FullyQualifiedName~ConsumerJwksCacheTests|FullyQualifiedName~ReservationGateUseCaseTests|FullyQualifiedName~ReservationEfModelMapsExistingOperationsTablesAndKeys'
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-build --nologo --filter 'FullyQualifiedName~AccountCurrentAuthorityTests|FullyQualifiedName~AccountCurrentWorkflowTests|FullyQualifiedName~ServiceLocalAccountAccessContractTests|FullyQualifiedName~StructureHttpContractTests'
```

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
| Operations publishes Gate audit events, durable fanout exchange | `smartpark.operations.gate-audit.v1` |
| Audit owner predeclares durable queue bound to that exchange | `smartpark.notification-audit.operations-gate.v1` |

Also account for MassTransit endpoint exchanges, bindings and error/skipped queues in ACLs. Operations may send requests/read decisions/publish its audit exchange; UserService may read requests/send decisions; the audit consumer may read its own queue. UserService's existing profile/access topology must remain authorized. Verify the actual ACLs with unauthorized credentials; merely providing different usernames is not proof of isolation.

**The audit queue must be bound before publishing/relay starts.** A durable exchange without a bound queue does not retain messages for a future consumer. The event contains only EventId, OccurredAt, ActorUserId, TenantId, SiteId, Action, nullable ReservationId and ParkingSessionId. No plate/image is included. The audit owner deduplicates by EventId, tolerates redelivery and owns long-term retention; Operations outbox cleanup is not audit retention. Event ID equals broker MessageId; correlation is ReservationId or the walk-in session ID. No audit consumer is added here.

Configure through the existing `.env`/setup/host runner or Compose mapping, keeping secrets out of shell transcripts:

- `SMARTPARK_RESERVATION_ADMISSION_ENABLED=true` for the admission experiment on UserService and Operations.
- Separate `SMARTPARK_RESERVATION_USER_BROKER_USER/PASSWORD` and `SMARTPARK_RESERVATION_OPERATION_BROKER_USER/PASSWORD`; blank credentials fail startup when required.
- `SMARTPARK_OPERATION_AUDIT_ENABLED=true` for effectful Gate writes. It can be enabled without reservation admission.
- `Messaging__OutboxDeliveryEnabled=true` only after topology is ready. False captures durable messages but intentionally pauses delivery; monitor backlog and disk capacity.
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
| Timely current Allowed | Terminal `FAILED_DEPENDENCY / RESERVATION_FINALIZATION_NOT_READY`; zero reservation/session/claim/order/refund effects |
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
6. Confirm requested-slot fallback cannot admit a vehicle or show it as an observed location. Live Facility compatibility and cross-site verified identity remain unresolved integration gates.

## Safeguard bypasses and release conditions

Direct SQL, bulk EF updates, direct DbContext saves, cascades and any future writer that does not use the guarded UoW bypass the C# replacements. Database checks/FKs/uniqueness still apply, but these bypasses do not acquire the site guard, protect first-confirmation/snapshot history, or emit audit events. Restrict service write access and audit competing writers; no claim of database-wide enforcement is made.

`confirmed_at` is historical state-write observation, **not commit evidence**. Before lifting D4c, the reviewed design must handle a transaction held across T, acknowledgement loss, crash/restart, time precision/skew and failover/metadata availability. A pre-commit check cannot establish strict commit-before-T. Do not use a passing pure deadline test as release approval.

Record checkout SHA plus dirty diff, test summaries, deployment versions and scenario outcomes in a report. Do not include credentials, JWTs, plates, images or raw provider errors.

## Service-local packaging checkpoint

The user removed the shared-project direction. Operations now contains the JWT/JWKS implementation and local message contracts; UserService contains its own compatible Account-current DTOs. The production project references stay inside each service folder. The test harness's cross-service references are not production dependencies.

Restore after this relocation; stale obj/assets files from the removed projects are not build evidence. In deployment CI, publish each API project separately (or use the existing Dockerfile with SERVICE=OperationService / UserService) with only that service's source available. No src/Shared copy or shared assembly should be needed. Run ServiceLocalAccountAccessContractTests and real request/reply replay before release. Preserved wire namespace/type names are intentional; schema/property changes still require consumer compatibility review.
