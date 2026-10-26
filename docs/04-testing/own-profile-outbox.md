# Own-profile audit outbox — deployment and verification

SPARK-248 / US-U01 · 25 October 2026 · implementation guide. API and event decisions were approved by the user on 24 October; see [feature API design](../03-design/api/own-profile-api-design.md). This change does not deploy schema or implement Notification & Audit.

## What success means

`PATCH /api/auth/me` resolves the affected User from the signed Account/session, takes the authorization guard, and changes only the tracked User's name. The Application handler publishes `UserProfileNameChanged` through `IEventPublisher` before the unit of work saves and commits. Infrastructure's `MassTransitEventPublisher` uses the **scoped `IPublishEndpoint`**, replaced by Persistence's EF Bus Outbox provider for the same scoped `AppDbContext`. That publish call records an outgoing message; it does not contact RabbitMQ while the profile locks are held.

The User mutation, `OutboxMessage`, and `OutboxState` commit in one PostgreSQL transaction. An event or business-write database failure rolls the transaction back. API success means durable profile/event recording, not successful Audit projection. A normalized name equal to the current persisted name returns the current profile without an event. Rejected requests emit none.

User & Access owns the versioned event; Notification & Audit owns the eventual projection. The approved payload contains `eventId`, `schemaVersion:1`, `actorAccountId`, `affectedUserId`, `occurredAtUtc`, and `correlationId`. It contains no name, email, password, token, or session identifier. The event ID also becomes MassTransit's `MessageId`; relay retry preserves it. The transport header `correlation-id` carries the same request correlation as the payload.

## Schema proposal and deployment order

The repository uses reviewed SQL feature scripts; there is no existing EF migration/snapshot workflow to extend. `AppDbContext` calls MassTransit's `AddTransactionalOutboxEntities()`. The proposed SQL is generated from that exact model with **MassTransit 8.5.7, EF Core 10.0.12, and Npgsql EF Core 10.0.3**. The equality test detects incompatible table/index drift.

1. Stop/restrict the old application version's writes during a coordinated deployment. Review and back up the explicitly selected User & Access database. Do not use a default connection or another service database.
2. Apply [13-own-profile-session-account.sql](../../scripts/database/microservices/13-own-profile-session-account.sql), then [14-user-profile-outbox.sql](../../scripts/database/microservices/14-user-profile-outbox.sql), to that selected database through the team's normal manual deployment process. The latter is a one-time, transactional create script; a second application is expected to fail rather than silently accept a different schema.
3. Deploy this application with `Messaging__OutboxDeliveryEnabled=false`. Legacy sessions without immutable AccountId must sign in again. No startup migration or schema fallback exists.
4. Provision and verify the durable RabbitMQ topology independently of any consumer, as described below. Review queue policies and capacity.
5. Set the broker configuration, then enable `Messaging__OutboxDeliveryEnabled=true` and restart/recreate the User Service through the documented runner. Verify pending records drain and messages accumulate in the durable audit queue while the consumer is absent.
6. Deploy a future idempotent Audit consumer independently. It must consume this queue and persist deduplication and audit records atomically. It is not part of this task.

| Framework table / index | Purpose and storage implications |
| --- | --- |
| `OutboxState`, PK `OutboxId` | Batch ownership, delivery progress, and lock coordination. An index on `Created` lets the relay select pending batches. |
| `OutboxMessage`, identity PK `SequenceNumber` | Serialized event and delivery metadata. Unique `(OutboxId,SequenceNumber)` orders producer batches; nullable Inbox keys and their unique sequence index support the framework's consumer path. Indexes on `EnqueueTime` and `ExpirationTime` are part of the exact framework model. |
| `InboxState`, identity PK `Id`, unique `(MessageId,ConsumerId)` | Included because the supported `OutboxMessage` model has a foreign key to it. This producer does not create inbox records or run an inbox cleanup worker. The `Delivered` index is retained for model compatibility. It is not a custom audit table. |

The model also includes framework `LockId` and nullable byte-array `RowVersion` fields, all addresses/content fields, both OutboxMessage foreign keys, and the framework nullability. Do not replace it with a reduced two-table schema. `workflow_deliveries` remains the existing encrypted OTP/email workflow storage and is unrelated.

Missing outbox tables cause the real endpoint to fail safely; there is no direct publish, dropped-event, or fake-success fallback. Missing session binding schema also prevents the new code from operating correctly. Deployment of both scripts is a prerequisite.

**Rollback is not routine deletion.** Disable delivery and stop writes as needed, preserve schema and pending events, and choose a compatible application rollback. Dropping outbox tables or deleting pending rows destroys undelivered facts and requires a separate reviewed recovery/export decision. Never run a destructive rollback merely to uninstall this feature.

## Durable routing without a consumer

The stable topology is:

| Component | Name / settings |
| --- | --- |
| Event exchange | `smartpark.user-access.user-profile-name-changed.v1`; fanout, durable, no auto-delete |
| Audit queue | `smartpark.notification-audit.user-profile-name-changed.v1`; durable, non-exclusive, no auto-delete |
| Binding | Event exchange → audit queue, empty routing key |

Run the provisioning script against the explicitly selected broker's management API, supplying credentials interactively:

```powershell
$auditCredential = Get-Credential
.\scripts\messaging\provision-profile-audit-topology.ps1 `
  -ManagementUri 'http://127.0.0.1:15672' -Credential $auditCredential -VirtualHost '/'
.\scripts\messaging\provision-profile-audit-topology.ps1 `
  -ManagementUri 'http://127.0.0.1:15672' -Credential $auditCredential -VirtualHost '/' -VerifyOnly
```

Use the approved broker address and secure credential transport for your deployment. The script declares and verifies the exchange, queue, and binding; it never starts a consumer, purges a queue, or deletes messages. Review policies separately: expiry, message TTL, overflow/drop, or later queue deletion can defeat retention. Set capacity alerts and avoid policies that discard pending audit facts. RabbitMQ publisher confirmation alone does not prove an unrouted publish was retained. Keep delivery disabled until this topology exists; creating a queue after an unrouted publish cannot recover it.

The normal producer contains no receive endpoint. The framework may create its own temporary bus endpoint; that is not the durable audit queue and does not substitute for provisioning it.

## Configuration, availability, and health

| Setting | Default / meaning |
| --- | --- |
| `Messaging__OutboxDeliveryEnabled` | `false`: supported capture-only mode; scoped EF outbox remains active, its delivery hosted service is not registered |
| `Messaging__RabbitMq__Host` / `Port` | `localhost` / `5672` for host execution; configure an address reachable from the User Service container |
| `Messaging__RabbitMq__VirtualHost` | `/` |
| `Messaging__RabbitMq__Username` / `Password` | Local development defaults only; use deployment credentials privately |

The implementation calls MassTransit 8.5.7's actual `DisableDeliveryService()` method in capture-only mode. It does not replace the bus with memory storage or bypass the database. `WaitUntilStarted=false` allows process/API startup without a running broker; the bus reconnects asynchronously. Profile writes remain available after schema deployment while broker delivery is degraded. Capture-only operation still needs PostgreSQL and consumes disk until delivery is enabled.

The existing `/health` endpoint is process liveness, not proof that the database, RabbitMQ routing, or Audit projection works. MassTransit registers its transport health check independently. Operator checks must distinguish: process responding; User database/schema writable; intentional capture-only mode; broker transport connected; durable topology verified; pending delivery age; consumer projection progress. A healthy bus does not prove a consumer has completed processing.

The keys are wired through `.env.example`, `compose.app.yml`, and `setup.ps1` user-secrets sync. They use ASP.NET double-underscore names so the existing host runner passes them through. No broker credential belongs in frontend configuration. After changing local values:

- Host runner: restart `scripts/run-schema-service.ps1 -Service User`.
- Visual Studio/direct run: `setup.ps1 -SkipDatabase -SkipRestore` syncs known keys, then restart the process.
- App container: `scripts/run-app-service.ps1 -Service User -NoBuild` recreates configuration; set a reachable broker host (for example `host.docker.internal` for a host broker). The root template's `localhost` refers to the container itself when passed unchanged.

Do not run database-reset/setup deployment options just to enable message delivery.

## Retry, backlog, cleanup, and duplicates

The framework relay waits for a healthy bus, polls/gets notifications for committed batches, and uses PostgreSQL locks to coordinate delivery workers. This configuration polls at one second when idle. MassTransit 8.5.7 bounds each send attempt to ten seconds by default and retries delivery; its delivery loop uses the framework's exponential retry policy and continues on later cycles. The profile request does not wait for those retries. A restarted process resumes committed outbox state.

Observe both pending batch age and count in the **owning database** through approved operational tooling. These examples are read-only operational queries, not an instruction to run them against a shared database during development:

```sql
SELECT COUNT(*) AS pending_batches, MIN("Created") AS oldest_pending_created
FROM "OutboxState" WHERE "Delivered" IS NULL;
SELECT COUNT(*) AS pending_messages, MIN("SentTime") AS oldest_pending_message
FROM "OutboxMessage" WHERE "OutboxId" IS NOT NULL;
```

Also monitor MassTransit relay failures, broker queue depth, disk capacity, and consumer lag. Request completion logs are diagnostic `ILogger` output, never RabbitMQ events or profile bodies. Enabling delivery after a long capture-only interval requires capacity planning; do not delete backlog to make an alert disappear.

After confirmed transport sends, the framework removes delivered messages and completed batch state. An interrupted transaction may cause the same EventId to be delivered again. This is **at-least-once delivery**, not global exactly-once processing. The future consumer must enforce a unique EventId and commit deduplication with its audit projection. Outbox rows are transient delivery state; permanent audit retention belongs to Notification & Audit. Producer `InboxState` remains unused, and inbox cleanup is deliberately disabled because there are no consumers here.

## Reproducible verification

Use only an explicitly provisioned, isolated PostgreSQL server. `UserServiceDatabaseFixture` creates and drops a randomly named `smartpark_auth_test_<guid>` database; never configure an existing application database as its target. Broker tests require a separate loopback-only disposable RabbitMQ instance. The broker test provisions a uniquely named durable test queue, holds the producer connection unavailable through a local TCP gate, then restores it; no business consumer is implemented.

Set `SMARTPARK_AUTH_TEST_CONNECTION` privately for that isolated server and `SMARTPARK_PROFILE_RABBITMQ_PORT` to the isolated AMQP port, then run:

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --nologo `
  --filter 'FullyQualifiedName~ProfileOutboxTests|FullyQualifiedName~ProfileOutboxBrokerTests'
```

Coverage: generated model/SQL equality; supported delivery-disabled registration; capture-only host startup and durable recording without an AMQP server; name/event commit and rollback in one scope; real business-column/outbox-storage failures; actual profile handler attribution, normalized no-op suppression and revoked-session rejection; broker-unavailable host startup with delivery enabled; queue present with zero consumers; recovery using the unchanged EventId/DestinationAddress; persistent routing and framework cleanup.

Actual executed results belong to the [task record](../../ai/tasks/completed/TASK-SPARK-248-SESSION-20261024.md). On 26 October 2026 all 7 outbox tests passed against isolated PostgreSQL and RabbitMQ, including broker outage/recovery without a consumer. Both topology provisioning and VerifyOnly also passed. Real broker checks are skipped with an explicit reason when their environment is absent.

## Framework evidence

- [MassTransit transactional outbox explanation](https://masstransit.io/documentation/patterns/transactional-outbox): scoped publication and database transaction participation.
- [8.5.7 bus-outbox configurator source](https://github.com/MassTransit/MassTransit/blob/v8.5.7/src/Persistence/MassTransit.EntityFrameworkCoreIntegration/Configuration/Configuration/EntityFrameworkBusOutboxConfigurator.cs): `DisableDeliveryService` skips only the delivery worker while retaining the scoped provider replacement.
- [8.5.7 delivery-service source](https://github.com/MassTransit/MassTransit/blob/v8.5.7/src/Persistence/MassTransit.EntityFrameworkCoreIntegration/EntityFrameworkCoreIntegration/BusOutboxDeliveryService.cs): transport health gating, retry, progress, and cleanup behavior. Current online documentation also describes v9; the pinned source and equality tests govern this implementation.
