# SPARK-248 / US-U01 approved follow-up

Current implementation and deployment guide, updated 30 October 2026. Historical reports in this directory retain evidence for their original revisions only.

## Provenance
Worktree: `C:/Github/smart-parking-system`, branch `feat/SPARK-248-manage-own-profile`.
Initial reviewed HEAD: `5dfd1e01d7465b8f7773451495561210ab824478`; current base: `b9b502b292382327ef5d9b27df10ac3dd7d3d16d`.
Shared broker source inspected: local/available origin `feat/SPARK-228-3D_Model_BE` at `e3a47fe1ff54a24c9f47f2249fe7b9232933dc51`. No fetch or whole-branch merge. Base b9b502b2 already incorporated compatible RabbitMQ sections in Compose, `.env.example`, and password generation in setup. This follow-up adapts their configuration bridges and resource lifecycle; no 3D code or older AI instructions copied. PostgreSQL 16 and RabbitMQ 3.13-management-alpine stay unchanged.

## Request to response
1. API authenticates the exact immutable Account/User/session binding. Unknown JSON fields are ignored.
2. `ProfileController` uses the Application AutoMapper profile to map the name-only request, then calls `IMediator.Send`. Application validator checks missing/null names and approved Unicode scalar rules.
3. `RequireAsync` checks fresh authoritative User, requesting Account, role and exact session. Another active Account cannot rescue invalid authority. Eligibility remains role-only for Driver, Operator, Owner and Admin.
4. The UoW repository loads the authorized User ID as tracked in the same scoped AppDbContext. It reloads an unchanged existing identity-map entry, rejects pending target mutations instead of discarding them, and rejects missing/deleted targets.
5. Domain `UpdateFullName` makes a narrow normalized name mutation. No entity mapping or DbSet.Update occurs.
6. A changed name publishes the minimal event through scoped IEventPublisher/IPublishEndpoint before one SaveChangesAsync. Supported EF Bus Outbox captures OutboxMessage/OutboxState in the same context and implicit database transaction as the name. No explicit handler transaction, authorization lock, wrapper transaction, direct bus publication or write retry.
7. Response returns the profile. Success means durable name/event recording, not completed Audit processing.

An invalidation observed at the fresh check rejects. An Account lock, role change or revocation committed after that check may precede a successful name save. This exception is name-only; security writers and shared lock APIs remain intact. Concurrent ordinary names use last-write-wins. No-op means equal to the loaded normalized name; it is not a globally serialized history and can race another edit. No ETags/version conflicts/idempotency keys are added.

The existing event namespace is retained despite moving its file, preserving MassTransit message identity. EventId equals MessageId and remains stable on relay retry. ActorAccountId identifies requester; AffectedUserId identifies shared profile. No personal name/contact/session payload. Outbox rows are delivery state, not permanent audit history; no exactly-once processing claim.

## Handler dependencies
- IUnitOfWork: tracked repository and single business/outbox save.
- ICurrentAccessService: fresh authoritative Application eligibility policy.
- ICurrentActor: request correlation only. Existing CurrentAccessContext contains User, Account, Session, RoleCode and Role; no suitable correlation field exists, so this dependency remains.
- IEventPublisher: supported scoped outbox publication.
- TimeProvider: real UTC event timestamp. Existing TimeProvider.System production registration remains; fakes are test configuration only.

## One-command fresh local setup
Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File setup.ps1` from repository root when ready to initialize local services. This task does not itself authorize running that command against existing resources.

Stage 2 generates missing local credentials while preserving configured values. Stage 3 validates ports. Stage 4 starts the shared schema project, provisions/verifies the profile durable fanout exchange, durable audit queue and binding without a consumer, initializes each fresh database through Compose and runs schema verification. User initialization runs consolidated base01 once; no scripts 13/14 execute afterward. Repeated setup verifies compatible state and skips initialization. Stage 5 synchronizes host user-secrets via stdin; stage 6 prepares frontend environment; stage 7 restores dependencies.

`-SkipDatabase` skips database/broker startup and topology provisioning. `-SkipUserSecrets` skips synchronization; `-SkipRestore` skips restore; existing Sonar/AI flags retain their meaning. `-RecreateDb` is an explicit destructive database option: only six database containers and volumes are selected; RabbitMQ storage/network remain. Never use project-wide down -v for database recreation.

Existing/legacy/partial databases require reviewed migration or an explicitly approved reset. The full verifier checks session binding, immutable ownership and installed framework outbox schema; a base sentinel alone is insufficient. No guessed NULL-session backfill, pending-message discard or application-startup migration. Historical test SQL is not a production upgrade plan.

## Configuration and delivery
One local source: SMARTPARK_RABBITMQ_USER/PASSWORD/VHOST/PORT/MGMT_PORT. Broker receives those values; host app uses 127.0.0.1 and published AMQP port; app container joins smartpark-schema_default and uses rabbitmq:5672; provisioning uses loopback management port. Existing broker volumes retain their initialized credentials: recover those credentials instead of resetting storage. Shared broker does not imply shared service databases or queues.

Messaging__OutboxDeliveryEnabled remains false by default. Enable only after full schema verification and durable routing verification, including broker policy review. Capture-only mode and broker outage retain pending database events for later delivery. No consumer is required for durable queue retention.

SMARTPARK_USER_CORS_ORIGINS supplies semicolon-separated exact origins; blank permits no cross-origin requests. Services:Parking must be an HTTP(S) URL without embedded credentials. Host setup/runner uses SMARTPARK_SERVICE_PARKING_URL (local development default supplied by setup); app Compose supplies internal service DNS. Existing CORS credential behavior is unchanged.

Errors include server-generated opaque traceId, shared with completion and unexpected-error logs and event correlation. Incoming correlation headers are not reflected. Distributed Activity remains intact. Cancellation/started responses, Bearer challenge, bodyless HEAD/204/304 and successful files/streams retain their behavior.

## Verification
See the task session for exact commands/results. Runtime checks need an explicitly authorized isolated PostgreSQL connection (SMARTPARK_AUTH_TEST_CONNECTION) and isolated broker fixture port (SMARTPARK_PROFILE_RABBITMQ_PORT). Never point these at development/shared services. Run `dotnet test SmartParking.slnx --no-restore --nologo`; without those variables the runtime tests explicitly skip. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1` and `scripts/test-profile-reset-selection.ps1` for scratch/double setup checks. Compose validation uses `docker compose --env-file .env.example -f compose.schema-integration.yml config --quiet` and the equivalent compose.app.yml command; it does not start services.

Suggested commit: `fix(profile): simplify authorized name updates and consolidate durable setup [SPARK-248]`.


Historical pre-merge verification: solution build 0 warnings/errors; solution tests 293 passed, 43 skipped, 0 failed. These results do not verify the merged tree. See [pre-merge session](../../../../ai/tasks/completed/TASK-SPARK-248-SESSION-20261029.md) for that evidence. Current reconciliation results are recorded separately.

## Develop merge reconciliation

Reconciled against HEAD `fce8a2638e068404319aa54712a776323fef2d85`. Direct `Account.RoleCode` remains authoritative; incoming operator activation, owner registration, queries and feature SQL use it rather than restoring `account_roles`. Session creation and test clones retain the immutable AccountId. Login account selection belongs to Application; signing and JWT validation remain Infrastructure responsibilities. Operator login retains its verified-contact requirement; own-profile eligibility remains role-only.

There is one bearer registration, using the SPARK-248 exact-account events/error formatter, the real registered clock, and the incoming SignalR query-token support restricted to `/hubs/operators`. Protected management writers retain transaction/authorization locks. Only the name handler uses the approved fresh-check timing.

There is one MassTransit registration. `SMARTPARK_MESSAGING_ENABLED` maps to `Messaging:Enabled` and enables the distinct incoming access projection/SignalR path. It does not enable profile delivery: `Messaging:OutboxDeliveryEnabled` remains a separate explicit switch. The existing access-event staging workflow hands off through the same scoped EF Bus Outbox; its acknowledgment and framework capture save in one transaction. Its historical `published_at` now means durable handoff, not broker acceptance or completed notification. Retries may duplicate events; the access projection retains its event-ID deduplication/version checks. Profile updates do not use that access staging table or worker.

Host user-secrets now use the current `SMARTPARK_RABBITMQ_HOST/PORT/USER/PASSWORD/VHOST` keys. Run setup with `-SkipDatabase -SkipRestore` to synchronize configuration without starting databases/broker. The runner and app Compose use those same keys; the container host/port remain `rabbitmq:5672`.

Fresh User setup executes consolidated base01, then the distinct incoming 05.13 MFA, 05.14 identity-hardening and 05.15 onboarding migrations. Scripts13/14 from the original profile feature are not restored or executed. An existing User base must pass the complete session/outbox verifier before incremental feature writers execute; incompatible/partial state fails without a fresh-schema reset. The incremental scripts remain rerunnable and may update legacy feature data; review them before applying to a populated database.

## Quick verification guide

Without runtime resources:

```powershell
dotnet build SmartParking.slnx --no-restore --nologo
dotnet test SmartParking.slnx --no-restore --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-profile-reset-selection.ps1
```

For manual HTTP checks, start your configured local UserService with `scripts/run-schema-service.ps1 -Service User`. Sign in as each supported role, GET `/api/auth/me`, then PATCH it with `{"fullName":"New Name"}`. GET again to verify persistence. Repeat the normalized name to verify no new profile event. Include unknown fields such as `role` or `avatarUrl` and verify they do not change protected fields. Test missing/null/blank names and an expired/revoked session; errors should carry a safe traceId matching the request log.

After explicitly preparing isolated runtime resources, privately set `SMARTPARK_AUTH_TEST_CONNECTION` to an isolated PostgreSQL administrative connection (tests create/drop uniquely named databases), and set `SMARTPARK_PROFILE_RABBITMQ_PORT` plus `SMARTPARK_RABBIT_TEST_PORT` to the isolated broker's published AMQP port. Supply `SMARTPARK_RABBITMQ_USER/PASSWORD/VHOST` consistently; no guest/default-vhost assumption is required. Run the solution tests again. The broker test uses a temporary queue and a local connection gate; it does not stop the broker. `scripts/test-workflow-upgrade.ps1` separately creates/removes isolated Compose resources, so run it only when you authorize that lifecycle. These runtime checks remain deferred until those resources are provided; skips are not passes.


Current merge verification (30 October): solution build passed with zero warnings/errors; solution tests passed 324 with 66 runtime skips. After the final workflow-error/configuration changes, UserService rebuilt and passed 205 tests with 55 runtime skips. Setup/reset-selection tests, script parsing, Compose configuration and diff checks passed. See the [completed reconciliation record](../../../../ai/tasks/completed/SESSION-20261029-SPARK-248-merge-reconciliation.md) for commands, changed-file reasons and deferred checks. No staging/commit or database/broker lifecycle operation was performed.
