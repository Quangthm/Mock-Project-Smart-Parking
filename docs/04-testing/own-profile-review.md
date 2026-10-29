# US-U01 / SPARK-248 — implementation review

26 October 2026 · working-tree review, no commit or deployment. Contract decisions were approved by the user on 24 October. Executed verification evidence is maintained in the [task record](../../ai/tasks/completed/TASK-SPARK-248-SESSION-20261024.md).

The code-flow descriptions below include the subsequent direct-role/current-access consolidation. The 26 October results remain historical SPARK-248 evidence; verification of the consolidation is tracked separately in the [current task record](../../ai/tasks/active/SESSION-20261027-account-role-authorization.md).

## Review first

1. [API contract](../03-design/api/own-profile-api-design.md): exact routes, input/output, Unicode semantics, legacy-session compatibility and changed-only event.
2. [CurrentAccessStore](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/CurrentAccessStore.cs), [CurrentAccessService](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessService.cs) and [CurrentAccessPolicy](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessPolicy.cs): SQL synchronization is in Persistence; reusable current-access orchestration and policy are in Application.
3. [UpdateOwnProfileCommandHandler](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Commands/UpdateOwnProfileCommand.cs): narrowly change the tracked User and record the event before one unit-of-work save/commit.
4. [Outbox deployment guide](own-profile-outbox.md): schema, routing, capture-only operation, retries and operational limits.

## Request to response

| Step | Actual file / symbol | Responsibility and outcome |
| --- | --- | --- |
| Composition | [Program.cs / UserHost.BuildAsync](../../src/Services/UserService/SmartParking.UserService.API/Program.cs) | Registers Application, scoped Persistence, messaging and HTTP adapters; middleware order is routing → completion/exception/status handling → authentication → authorization → limiter → controllers. No startup schema migration. |
| Bearer verification | [ApiAuthenticationExtensions.AddJWTAuthentication](../../src/Services/UserService/SmartParking.UserService.API/Extensions/ApiAuthenticationExtensions.cs), [CurrentAccountBearerEvents](../../src/Services/UserService/SmartParking.UserService.API/Services/CurrentAccountBearerEvents.cs) | Verifies signed token, then the DI events adapter passes `sub`, `account_id`, `jti` and role to `ICurrentAccessService.ValidateAsync`. The shared Application policy checks persisted session/account/user/role. Ineligible credentials fail the Bearer challenge. |
| Current caller | [HttpCurrentActor](../../src/Services/UserService/SmartParking.UserService.API/Services/HttpCurrentActor.cs) | Adapts only the authenticated principal to the inward-owned `ICurrentActor`; carries server trace/request correlation. No body identifier chooses the target. |
| HTTP input | [ProfileController.Get / Update](../../src/Services/UserService/SmartParking.UserService.API/Controllers/ProfileController.cs) | GET/PATCH `/api/auth/me`; explicit `UpdateOwnProfileRequest.FullName` → command mapping; dispatch through `IMediator` with request cancellation. No repository/DbContext dependency. |
| Validation | [ValidationBehavior.Handle](../../src/Services/UserService/SmartParking.UserService.Application/Common/Behaviors/ValidationBehavior.cs), [UpdateOwnProfileValidator](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Commands/UpdateOwnProfileValidator.cs) | Runs `ValidateAsync` sequentially with cancellation and a separate validation context per validator. This avoids concurrent scoped DbContext use and duplicate failure accumulation. |
| Read | [GetOwnProfileQueryHandler.Handle](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Queries/GetOwnProfileQuery.cs) | Calls `ICurrentAccessService.RequireAsync`, which reads fresh state through `ICurrentAccessStore.LoadCurrentAsync` and applies `CurrentAccessPolicy`; maps the exact Account's role and shared User profile. |
| Guarded update | [UpdateOwnProfileCommandHandler.Handle](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Commands/UpdateOwnProfileCommand.cs) | Opens UoW transaction and calls `ICurrentAccessService.LockAsync`, which reads locked fresh state and applies the same policy, then calls `User.UpdateFullName`. No provider SQL in the handler. |
| Domain invariant | [ProfileName](../../src/Services/UserService/SmartParking.UserService.Domain/ValueObjects/ProfileName.cs), [User.UpdateFullName](../../src/Services/UserService/SmartParking.UserService.Domain/Entities/User.cs) | Requires a nonblank valid scalar sequence, rejects controls, trims edges and counts at most 255 Unicode scalars. Internal spacing and Unicode form remain unchanged. Non-HTTP callers of the mutation are protected too. |
| Event capture | [MassTransitEventPublisher.PublishAsync](../../src/Services/UserService/SmartParking.UserService.Infrastructure/Messaging/MassTransitEventPublisher.cs), [ProfileOutboxRegistration.AddProfileOutbox](../../src/Services/UserService/SmartParking.UserService.Persistence/ProfileOutboxRegistration.cs) | Application's publisher port resolves a scoped `IPublishEndpoint` replaced by the EF Bus Outbox provider for the same `AppDbContext`. This call captures the outgoing event and performs no broker call while the profile guard is held. |
| Commit and response | `IUnitOfWork.SaveChangesAsync` → transaction commit → `OwnProfileResponse.From` → `ApiResponseFactory.Success(...).ToActionResult()` | Changed name plus event are durable together. The response reports the persisted supported fields. A normalized no-op performs no event publication. |

The entity is tracked and changed narrowly; there is no `DbSet.Update` for this name edit or client-to-entity AutoMapper assignment. The legacy `avatar_url` is mapped as an unchanged nullable string; previous URL-filtering `AssetReference`, avatar response wrapper and avatar-removal command are excluded from this slice.

New session issuance uses `AuthSessionService.CreateAsync` → `AccessTokenService.SelectLoginAccount`, selecting a stable eligible Account by ID. `RotateAsync` preserves that binding. Script 13 and EF after-save behavior make session AccountId immutable. Legacy NULL binding is intentionally not backfilled.

## Authorization/save coordination

The required meaning is serial ordering: an invalidator that wins the relevant database order prevents the profile write; a previously committed authorized name change is not retroactively cancelled.

`CurrentAccessStore.LockCurrentAsync` requires a ReadCommitted transaction and acquires:

```text
User FOR UPDATE → exact Account FOR UPDATE → referenced Role FOR SHARE → exact session FOR UPDATE
```

The first Account-to-User lookup is only a hint. Ownership is checked again after User and Account locks. Authoritative Account, role and session values are read without tracking; an already-tracked unchanged User is explicitly reloaded. A dirty tracked User is rejected rather than cleared or silently overwritten. No blanket `ChangeTracker.Clear()` is used.

`CurrentAccessPolicy.RequireCurrentAccess` verifies matching User/Account/session identity, active/deleted/lock state, session expiries and the single supported role referenced by `accounts.role_code`. The role must match the authenticated claim; the claim does not grant authority by itself. `Accounts.Permissions` is intentionally not consulted for this shared eligibility check or own-profile access. Operational actions evaluate their existing role/permission and resource-scope rules separately in Application.

Multiple Accounts sharing a User serialize name mutations on the same User lock. Current values are reloaded before mutation, preserving last-write-wins without ETags or name-version conflicts. Lock wait is bounded at five seconds. Known lock/deadlock/serialization failures become `ProfileAccessConflictException`; they are not blindly retried, and no retry follows an unknown commit outcome.

### Participating writer inventory

| Writer / authoritative change | Coordination with the profile guard |
| --- | --- |
| `LoginCommandHandler.Handle` through `UserRepository.GetByEmailForLoginAsync`: failed-login/timed global User lock/unlock | User row lock precedes mutation; targeted reload avoids stale scalar state. |
| `UserManagementService.SetStatusAsync`: global User status and all-session revoke | User row lock then session updates; targeted User reload. |
| `AccountWorkflowService.RequestLoginAsync`: timed User unlock | User row lock/reload before state change. |
| `AccountWorkflowService.LoadAsync` / `ChangeBootstrapPasswordAsync`: current User and session revocation | User lock before challenge/session changes, targeted User reload. Existing password workflow remains separate from the normal-profile feature. |
| `PostgresAuthSessionStore.RevokeAsync`: exact logout | Atomic UPDATE locks the exact session row. It acquires no later User/Account lock, so it is a compatible suffix of the profile lock order. |
| `PostgresAuthSessionStore.RotateAsync`: token rotation | Compare-and-swap UPDATE of the exact session row; no later parent lock. Rotation-first makes the old jti unusable; profile-first forces rotation to wait. |
| `OwnerRegistrationService.ReviewAsync`: pending User/account activation or rejection | Application row → User → Accounts. Profile does not lock application rows and cannot authorize a pending User. User reload added. Existing external provisioning inside this pending-only workflow is not part of the profile transaction. |
| Driver/Owner registration verification/recovery and provisioning/seed | Operate on new/pending identities or acquire the User row before activation; cannot grant a pending identity profile access. They do not bypass active-profile security invalidation. |
| Direct Account status/ownership/deletion or role reassignment | UPDATE/DELETE contends on the exact locked Account row, including `role_code` changes. The referenced Role is held FOR SHARE, so different accounts using that role may proceed concurrently while catalogue mutation/deletion waits. The required FK prevents missing assignments and deletion of a referenced role. No role-management endpoint is added. Future multirow writers must preserve the same parent-to-child order. |

The guarantee is bounded to these authoritative User-service rows and audited writers; it is not a claim about arbitrary future migrations or external databases. The same `AppDbContext` owns Users, Accounts, Roles and AuthSessions and is wired to one service-owned User connection. Each Account has one Role; a User may retain several scoped Accounts. No shared deployed database was queried to infer configuration.

## Failure and logging paths

| Producer | Integration point |
| --- | --- |
| Validator exception | `ApiExceptionHandler` → immutable `ApiExceptionMapper` → `ApiErrorWriter` |
| MVC malformed JSON/type/model-state failure | `ApiErrorResponseExtensions.InvalidModelStateResponseFactory` → `ApiErrorActionResult` → same writer |
| Bearer challenge/forbid | Constructor-injected `CurrentAccountBearerEvents` delegates to the shared `ApiErrorWriter`, preserving `WWW-Authenticate: Bearer`; standalone hosts without that adapter retain the existing chained callbacks |
| Empty framework 404/405/415 and configured 429 | Status-code pages use the same status-to-envelope factory |
| Failure-as-value action | `ApiResponse.ToActionResult` preserves the selected HTTP status |
| Unknown failure | Safe 500 body; no exception message, SQL, stack or Source |
| Request-abort cancellation / started response | Framework cancellation semantics; started failures propagate, no appended second JSON document |

The mapper uses **exact runtime types**; derived types require explicit registration. Duplicate type registrations throw at construction. Known legacy `AuthException` code/status pairs use fixed reviewed public messages; unrecognized pairs fall back to safe 500. Database exceptions are not categorically bad-request errors. `ErrorHandlingResult` carries the internal response/status plus diagnostic classification without adding status to the JSON.

`RequestCompletionMiddleware` logs method, route template, elapsed time, actual status, server correlation, abort and propagated-failure flags through `ILogger`. It omits actor identifiers because no approved actor logging policy was found, and omits raw path/query/body/profile names/tokens. `ApiExceptionHandler` owns one sanitized diagnostic for handled unexpected errors; .NET 10 handled diagnostics suppression is configured explicitly. Unhandled started-response failures remain framework-owned and are propagated; this review does not claim that all framework/provider logs are rewritten or sanitized by the custom handler.

The consolidated `CurrentAccessService` separately logs meaningful denied access decisions using `ILogger<CurrentAccessService>`, the safe Account ID and decision code. It does not log session IDs, tokens, contacts, credentials or exception stacks. Operational authorization uses the same safe decision logging convention. Unexpected exceptions still flow to the centralized handler rather than being logged and rethrown at every layer.

The writer leaves success file/image/stream responses alone and produces no body for HEAD/204/304. It never treats `HasStarted` as a successful recovery from a caught exception.

## Event delivery and deployment

See [own-profile-outbox.md](own-profile-outbox.md) for exact configuration and reproducible commands.

- Manually review/deploy [13-own-profile-session-account.sql](../../scripts/database/microservices/13-own-profile-session-account.sql) after the current clean-install base and before account-bound code, then [14-user-profile-outbox.sql](../../scripts/database/microservices/14-user-profile-outbox.sql). Run [verify-user-access-schema.sql](../../scripts/database/verify-user-access-schema.sql) read-only before deployment. Ordinary Compose/setup initializes base01 only; its base check does not prove session/outbox readiness. No shared database is modified by this task and no startup auto-migration is added.
- The framework model retains all three supported tables and exact indexes; no custom relay or reduced schema. Existing `workflow_deliveries` is not reused as an audit outbox.
- Start in delivery-disabled capture mode until the durable event exchange, queue and binding are independently provisioned. The mode calls actual MassTransit `DisableDeliveryService()`; capture remains persistent.
- API success means name/event commit, not Audit completion. Broker outage leaves committed delivery state pending. Required outbox storage failure rolls back the mutation.
- Retry preserves EventId; delivery is at least once. A future consumer must deduplicate EventId atomically with its projection. Completed outbox rows are cleaned by the framework and are not permanent audit records.
- `/health` is process liveness. Broker connectivity, topology existence, oldest pending event and consumer lag require separate operational observation.

## Verification status and constraints

Executed on 26 October 2026 against the uncommitted feature working tree:

| Check | Actual result |
| --- | --- |
| `dotnet build SmartParking.slnx --nologo --no-restore` | PASS; zero warnings/errors |
| `dotnet test SmartParking.slnx --no-restore --nologo` with isolated User PostgreSQL and RabbitMQ variables, TRX logger | PASS; UserService 162 passed/0 skipped; ParkingService 48 passed/9 skipped; no failures |
| Focused authorization/persistence suite | 12 passed/0 skipped: real PostgreSQL both-order races for revoke, rotation, Account lock, role delete and User lock; stale tracking; immutable binding; shared User; Unicode boundary |
| Focused outbox suite | 7 passed/0 skipped: exact DDL, durable commit/rollback/faults, no-op/rejection, unavailable broker capture, recovery/unchanged identity/persistent queue with zero consumers |
| HTTP error boundary suite | 40 passed/0 skipped: binding/auth/status producers, exact mappings, safe logging, cancellation, started response, HEAD/204/files/streams |
| Setup/configuration | PowerShell parse checks, scratch-only `scripts/test-setup.ps1`, Compose `config --quiet`, and actual topology provision plus VerifyOnly against the isolated broker all passed |

The nine unrelated ParkingService tests require `SMARTPARK_STRUCTURE_TEST_CONNECTION`; it was not configured. Their password-authentication case explicitly rejects a trust-only server, so the User-service test endpoint was not repurposed. No required profile/database/broker test was skipped in the final solution run. TRX evidence is retained under `.cache/SPARK-248-test-results/` (generated, not committed).

The first focused run exposed test setup issues: MassTransit async-only disposal and .NET 10's key-ring warmup loading developer DPAPI keys. The tests now dispose providers asynchronously and isolate the JWT-only error host's Data Protection warmup; the final full run passed. Product key management was unchanged. The framework behavior was checked against [ASP.NET Core's warmup source](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/DataProtection/DataProtection/src/Internal/DataProtectionHostedService.cs).

Coverage is implemented in:

| Test area | Files |
| --- | --- |
| All roles, input rules, unknown-field isolation, legacy avatar preservation, event/no-op behavior | `OwnProfileTests.cs`, `OwnProfilePersistenceTests.cs` |
| Account binding/legacy rejection/rotation | `AuthAccountBindingTests.cs`, `AuthSessionTests.cs` |
| Fresh tracked state, revocation/rotation/account/role/global restrictions in both orders, shared User last-write-wins | `ProfileAuthorizationRaceTests.cs` |
| HTTP producers, propagation, cancellation, privacy, HEAD/files and validation dispatch | `ApiErrorHandlingTests.cs`, `ValidationBehaviorTests.cs` |
| Exact framework SQL model, commit/rollback/fault atomicity, no-op/rejection | `ProfileOutboxTests.cs` |
| Queue without consumer, outage and recovery retaining identity | `ProfileOutboxBrokerTests.cs` |

The integration runs used only verified SPARK-248-labelled disposable containers, loopback ports and tmpfs storage; every test database had a generated guarded name. No shared database or application was changed. Production usability still requires manual schema deployment and intentional topology/delivery configuration. Avatar mutations, Audit consumer, account switching, verified contact/security changes and future 2FA verification remain deferred.

Pre-existing security alignment outside this slice: `AccessTokenService.LifetimeSeconds` still uses the legacy 24-hour value, and `AuthSessionService.Response` still includes the old User payload in login responses. The v1.1 baseline describes different lifetime/minimal-login requirements; this review does not present the normal-profile change as completing those separate authentication requirements.

## Instruction conflicts and future maintenance

No AGENTS rule or separate coding-skill draft is rewritten. Explicit user direction governs this task where sources differ:

| Existing source | Task direction / future instruction proposal |
| --- | --- |
| `ai/architecture-reference.md` §4 and `ai/context/architecture-guardrails.md` CQRS examples use ISender | IMediator is the selected project convention here. Future instruction update should reconcile the example. |
| Architecture reference §1 describes RabbitMQ/MassTransit as not implemented | This authorized change adds pinned 8.5.7 adapters and supported EF outbox; update inventory only in a separate requested instruction/document maintenance task. |
| 2026-10-22 profile session accepted opaque avatar conversion/deletion and a User-only locking argument | Superseded by approved raw avatar read/no mutation and exact-account/session guard. Preserve that session as historical evidence. |
| Previous mentor recommendations proposed scoped unknown-JSON rejection | Explicit direction ignores unknown fields while narrow DTO mapping prevents mutation. |

Suggested Conventional Commit for the combined feature review, without performing a commit:

```text
feat(user): guard own-profile updates and persist audit events [SPARK-248]
```

If commits are split for review, keep each logical code, test and documentation change separate under the repository convention.
