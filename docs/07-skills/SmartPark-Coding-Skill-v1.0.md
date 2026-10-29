---
name: smartpark-coding
description: Implement or review SmartPark backend and frontend features using the approved five-layer backend architecture, Account-based authorization, CQRS, uniform errors, reliable service integration, concurrency discipline, and responsive product UI conventions. Inspect relevant repository instructions and feature contracts first.
---

# SmartPark coding skill — approved final

Decision snapshot: 2026-10-29. This is the reusable shared coding guide for SmartPark implementation and PR review. For coding tasks, it is the primary implementation convention loaded by `ai/AGENTS.md`; it does not replace authoritative SRS/business rules, approved architecture/service ownership, more specific feature contracts, or nonconflicting scoped `AGENTS.md` instructions. Feature-specific rules remain feature-specific unless this guide explicitly promotes them to a shared convention. Examples are illustrative SmartPark examples, not universal product requirements or compiled repository code.

## 1. Authority and efficient context loading

Follow the current user task and explicit approved decisions over conflicting older project guidance. Preserve all nonconflicting AGENTS.md instructions. Record conflicts instead of silently rewriting history. Never promote an AI recommendation, sample implementation or passing test into a product decision.

### Architecture/direction approval gate

If a requested feature or code change requires an architectural, cross-service, security, data-ownership, integration, or shared implementation direction that is **not already established by this guide or another approved project contract**, do not silently choose a one-off design and continue.

Instead:

1. stop before implementing the unapproved architectural part;
2. propose a reusable/shared implementation strategy rather than a feature-only workaround;
3. explain the viable choices, costs, benefits, operational implications, and effects on code health/maintainability;
4. identify which existing services/modules/contracts would be affected;
5. distinguish work that can proceed independently from work blocked by the decision; and
6. require explicit user/project-lead approval before implementing that new direction.

Do not block unrelated work that can safely continue under existing decisions. Do not treat an agent recommendation as approved merely because it appears cleaner or more enterprise-like.

Read applicable root/nested AGENTS.md and relevant linked skills. Search the actual tree before assuming paths. Start with documentation indexes/headings, then read only sections needed for the feature. Read complete relevant methods and DI registrations to understand behavior. Expand reads when dependencies require it.

| Task | Read first | Trace in source |
|---|---|---|
| API/use case | Current requirement/module, permission matrix, API contract | Controller, input/command, validator, behaviors, handler, response |
| Authorization | Account/session model, permissions, applicable revocation rules | Token issue/validation, actor adapter, authoritative stores, invalidators |
| Persistence | Entity/schema/mapping, migration policy | Repository, tracking, UoW, transaction and competing writers |
| Errors/logging | Approved envelope/code catalogue, observability rules | MVC/auth result producers, exception handlers, logging registration |
| Integration event | Service ownership, event/audit contract | Scoped publisher, outbox context, topology, relay and consumers |

Inspect the **local Git checkout** before relying on repository state. At minimum record the current branch/HEAD and working-tree provenance (for example `git status --short --branch --untracked-files=all` and `git rev-parse HEAD`). Locate current requirement/architecture paths from the local tree rather than hard-coding stale versions. Do not fetch, switch, reset, clean, stage, commit or push merely to establish context unless the task explicitly authorizes it. Historical review reports establish leads, not current test evidence.

Ask only about unresolved material behavior/contracts. Continue independent work. Do not reset, overwrite, stage, commit, push, deploy, or mutate shared databases without task authorization. Preserve unrelated changes.

## 2. Layer responsibilities

| Layer | Owns | Must avoid |
|---|---|---|
| API | HTTP binding/routes/status, authenticated adapter, uniform error integration, request logging, DI composition | Repository/DbContext calls in controllers; business orchestration |
| Application | DTOs, commands/queries, IMediator handlers, FluentValidation pipeline, use cases, inward-owned ports | HttpContext, SQL, concrete broker/database dependencies |
| Domain | State, pure business invariants and meaningful mutations | HTTP/EF/MassTransit dependencies or JSON serialization of snapshots |
| Persistence | EF Core/PostgreSQL, repositories/UoW, mappings, guards/transactions, outbox storage | Deciding entire use cases or Admin policy in repository services |
| Infrastructure | External integrations and port adapters, MassTransit/RabbitMQ publishing | Owning business policy or reaching into another service's DB |

API composition-root references to implementation assemblies are allowed for wiring. Dependency inversion concerns behavior ownership as well as interface placement. Keep packages scoped to their role. Infrastructure may wire Persistence through the established composition arrangement, but do not create circular dependencies. Framework-required outbox persistence types/configuration (for example MassTransit EF outbox entities, mappings and DbContext registration) belong in Persistence beside the owning DbContext, not Domain. Domain may represent business facts but must not reference transport-specific outbox entities or broker storage concerns.

Repository/service ports may be defined inward, but **Application performs I/O orchestration**. Domain receives already-resolved facts and evaluates business truth; Domain entities/services must not reach outward to repositories, HTTP services, brokers or DbContext to fetch those facts.

Use IMediator by project choice; do not substitute ISender unless an explicit project decision changes this convention. MediatR is in-process; RabbitMQ is cross-service. Use SignalR only for an actual approved realtime use case. Payment owns authoritative tariffs; serialize financial snapshots outside Domain while preserving the approved contract when such work is in scope.

### Validation and rule ownership

| Validation/rule | Example | Owner |
|---|---|---|
| Input validation | `sessionId` required, image size valid | FluentValidation/Application |
| Authorization/policy | Actor may checkout this Site | Application |
| Business invariant | Completed session cannot checkout again | Domain |
| Persistence/concurrency guarantee | Two requests cannot complete the same session concurrently | Persistence/DB |

A useful summary is:

> **Application orchestrates facts; Domain evaluates business truth; Persistence guarantees that the truth was not invalidated by concurrent storage changes.**

### External facts and transition placement

| Situation | Direction |
|---|---|
| State belongs to the same aggregate | Load aggregate → Domain transition → save |
| Rule depends on an external fact | Application retrieves fact → passes encapsulated fact/DTO to Domain |
| Cross-service fact | Application port → Infrastructure HTTP adapter → authoritative owner |
| Domain entity needs DB/service itself | Avoid |
| Same transition rule is reused in multiple flows | Put the rule/state machine in the aggregate or Domain policy, not individual handlers |
| Race-sensitive state change | Domain validation + Persistence concurrency enforcement |
| Immediate authoritative answer required | Synchronous internal service API |
| Long-running/durable downstream reaction | RabbitMQ/MassTransit |
| UI wants realtime update | SignalR |
| Long-running multi-service transaction | Saga/process-manager pattern only when complexity justifies it |
| CORS origins | Configuration per environment, not hardcoded service code |

When a domain rule depends on information outside the aggregate, prefer passing a small named fact/value object/DTO into the Domain rather than passing a repository/service abstraction into the Domain.

### Environment-owned CORS and service endpoints

CORS allowed origins and service/frontend base URLs are environment configuration, not hard-coded business/service source. A development port such as `http://localhost:5173` must not become a permanent code assumption. Do not “fix” CORS globally with unrestricted origins when credentials or sensitive APIs are involved. Remember that CORS is a browser boundary; backend-to-backend HTTP and RabbitMQ communication are not governed by browser CORS.

## 3. Inputs, mapping and validation

Keep cohesive request/response DTOs and commands in Application. API maps client-controlled input to the use-case request. Separate request and command is an approved/useful pattern where the transport contract and use-case contract have distinct responsibilities; it is not a universal Clean Architecture law. Follow the established AutoMapper convention when the service/base API controller provides IMapper; prefer configured AutoMapper mappings over repetitive explicit request/response mapping and verify mapping configuration in tests. Keep maps narrow. Never map an unrestricted request onto a tracked security-bearing entity or let mapping decide authorization.

```csharp
// Application types; names/response shape must match the feature contract.
public sealed record UpdateOwnProfileRequest(string? FullName);
public sealed record UpdateOwnProfileCommand(string? FullName)
    : IRequest<OwnProfileResponse>;

// API action excerpt, inside an authenticated controller with the established Mapper:
var command = Mapper.Map<UpdateOwnProfileCommand>(body);
var result = await Mediator.Send(command, cancellationToken);
// Translate through the project's approved success/error response mechanism.
```

The own-profile example above illustrates two general rules: do not trust client-selected actor/owner identifiers as authorization authority, and do not use unrestricted `mapper.Map(body, trackedEntity)` writes that can overwrite protected fields. For an own-resource endpoint specifically, `body.UserId`/`body.AccountId` must not select who the caller is acting as. Neither `sealed` nor `init` is an authorization or validation boundary. Missing/null semantics must be explicit for partial updates.

SmartPark API default: ignore unknown JSON properties using normal ASP.NET Core binding behavior. Do not enable strict unmapped-member rejection (for example JsonUnmappedMemberHandling.Disallow) unless an approved endpoint contract explicitly requires it. Ignoring unknown fields must not lead to mass assignment or assertions that unsupported writes succeeded.

JSON parsing/model-state errors occur before MediatR; handle them at API. Application validators enforce command rules; Domain maintains invariants. Use ValidateAsync with cancellation; serialize validator execution when shared scoped dependencies are not thread-safe. Keep writes out of input validators. Test actual DI dispatch, not only direct handler calls.

## 4. Trusted Account identity and shared User data

Authorize using the authenticated Account, exact session and current applicable authority. Resolve User from that trusted relationship. User data is shared across its Accounts; another active Account does not legitimize an operation through a locked Account.

Application defines a small current-actor port. API implements it from authenticated request context. Actual claim names and identifier types must match verified token/session contracts; do not assume sub means AccountId or jti means a GUID session primary key.

```csharp
// Illustrative shape, subject to actual identifier conventions:
public interface ICurrentActor
{
    Guid? AccountId { get; }
    Guid? SessionId { get; }
}
```

Fail closed when required identity is absent. Never trust raw JWT decoding, body identifiers or spoofable headers. Token claims establish asserted identity; fresh stores determine current Account/session eligibility. Background jobs use explicit trusted execution context, not captured HttpContext.

## 5. Tracked writes, UoW and atomic authorization

Load a tracking entity in the intended scoped context, apply a narrow domain mutation, then save through the UoW. Do not call DbSet.Update merely to save one changed field. A no-tracking object will not persist by mutation alone; use a deliberate attached/targeted update design if chosen.

```csharp
// Only after authoritative access has been guarded:
user.ChangeFullName(approvedName);
await unitOfWork.SaveChangesAsync(ct);
// Avoid: repository.Update(user); // broad stale-field writes
```

Ordinary single-SaveChanges operations do not need explicit transaction management or resource locking solely to repeat EF Core's guarantees when the operation is simple and correctness does not depend on serializing against competing writers. Use the lightest mechanism that preserves the approved business meaning.

Use this decision rule:

```text
Simple state change
+ no scarce/shared invariant
+ concurrency outcome is acceptable
        ↓
SaveChangesAsync
No manual transaction/lock

Check-then-claim scarce/shared resource
or financial/security invariant
or duplicate state would be invalid
        ↓
Deliberate concurrency protection
+ real concurrency tests
```

For example, a normal own-profile name change may load the authorized target, apply the narrow mutation and call SaveChangesAsync. Last-write-wins between concurrent name edits is acceptable unless the feature contract says otherwise. A concurrent Account status/permission writer taking a lock does not automatically protect a name update unless both operations participate in the same locking/concurrency protocol or contend on the same database row; do not claim implicit coordination.

Escalate to explicit transactions, row/advisory locks, optimistic concurrency, database constraints, or another deliberate concurrency mechanism when a stale check can violate a business invariant. Typical examples include claiming/reserving the same slot for an overlapping time, consuming limited capacity, creating duplicate active sessions, settlement/financial state transitions, or security-sensitive operations whose contract requires revocation/permission changes to serialize with the write.

Prefer database uniqueness/exclusion constraints and atomic conditional updates where they can enforce the invariant directly. When serialization is required, identify every competing writer, define a consistent locking/concurrency protocol, use fresh authoritative state inside a short transaction, and test real conflicting orders. A SELECT predicate or isolated lock that competing writers do not share is not proof of atomicity.

Avoid a split "check then mutate elsewhere" pattern such as:

```csharp
if (!await repository.IsActive(sessionId, ct))
    ...

await repository.Complete(sessionId, ct);
```

unless there is a specific approved reason. That shape typically performs a status read and a later state update with a race window between them.

Prefer loading the aggregate, invoking the business transition, and then persisting it:

```csharp
var session = await repository.GetByIdAsync(id, ct);

session.Complete();

await unitOfWork.SaveChangesAsync(ct);
```

Protect the actual write with optimistic concurrency, a conditional update, a database constraint, row/advisory locking, or another deliberate mechanism **only when the invariant requires it**.

For processes with many meaningful state transitions, centralize the state machine in the aggregate or a reusable Domain policy rather than scattering transition checks across controllers, handlers, repositories and consumers. Keep transport/retry state outside that business state machine unless it is itself a business concept.

Tracking queries can reuse stale entities without overwriting scalar values. A FOR UPDATE query acquires a lock but need not refresh the EF identity map. Use authoritative projections/reloads when a guarded flow requires fresh state; do not clear a context containing unrelated pending work. Do not hold DB locks during external calls. Bound retries and distinguish known rollbacks from unknown commit outcomes; never blindly repeat external effects.

## 6. Frontend responsiveness and shared UI implementation rules

Frontend work must remain responsive on mobile even when the current MVP is primarily web. Use the existing design system and responsive primitives; do not create a separate mobile-only product unless a later approved requirement asks for one.

Shared implementation requirements:

- support desktop, tablet and mobile layouts without horizontal page overflow;
- grids/cards/tables must collapse, wrap, stack or provide an intentional responsive alternative instead of becoming unreadable;
- keep primary actions reachable and touch-friendly on smaller screens;
- preserve information hierarchy when content is condensed; do not hide authoritative status/critical actions merely to fit a breakpoint;
- use existing spacing, typography, component and breakpoint conventions before inventing a parallel responsive system;
- keep feature-specific layout/content requirements in the applicable product/task/FE documentation rather than promoting them into this global coding guide.

The shared coding guide defines **responsive implementation quality**, not the exact layout of individual SmartPark pages.

## 7. Uniform errors and practical OCP

Use the **built-in ASP.NET Core exception-handling middleware** via `UseExceptionHandler(...)` together with DI-registered `IExceptionHandler` implementations (`AddExceptionHandler<T>()`). `IExceptionHandler` is the framework-supported handling strategy invoked by the exception-handler middleware; it does **not** remove middleware from the request pipeline.

For SmartPark, do not maintain a parallel custom catch-all `ExceptionHandlingMiddleware` that duplicates this framework mechanism unless a new approved requirement demonstrates a need the built-in pipeline cannot satisfy. This direction uses the existing .NET 10 capability and keeps exception classification/translation extensible without making one custom middleware own every exception rule.

Use API-owned `IExceptionHandler` integration, dictionary-based known-exception mapping and safe fallback. Keep core response-writing stable as mappings are extended. Small central registration is acceptable; introduce feature mapping providers when they earn their complexity. Do not require one class per exception.

The approved public error envelope is based on `success`, `code`, `message`, `errors`, and `traceId` as applicable. The actual HTTP status belongs to the HTTP response status and is **not duplicated in the JSON body**.

```csharp
// Internal API translation model; never serialize this whole object.
public sealed record ErrorHandlingResult(int HttpStatusCode, ErrorMessage Error);
// Lookup mechanism and result shape serve different purposes:
IReadOnlyDictionary<Type, Func<Exception, ErrorHandlingResult>> mappings;
// Write actual HTTP status; serialize only the approved public envelope.
```

Use stable readable public codes such as VEHICLE_TYPE_INVALID. Keep public messages safe and intentional. FE branches on code, not English wording. Omit duplicated HTTP status from JSON; still set the correct actual HTTP status. Do not return HTTP 200 for an unsuccessful Result.

Define exact-type/base-type matching explicitly; duplicate registration fails early; fallback is deterministic. Keep status/message together rather than divergent switches. Immutable singleton registries/handlers must not capture scoped DbContext/services. Exact dictionary matching does not automatically handle subclasses. Do not justify dictionary choice with unmeasured claims that it is always faster than conditionals.

Register known handling before safe unexpected-error fallback. Unknown exceptions produce sanitized 500, never arbitrary exception.Message/Source/stack/SQL. Known DB conflicts may have deliberate mappings; generic database failures are not automatically HTTP 400. Avoid overlapping catch-all systems.

Integrate non-exception producers separately through shared error formatting: MVC binding/validation, JWT challenge/forbid, applicable route/client/status errors such as 404/405/415/429. Preserve WWW-Authenticate: Bearer. Adopting IExceptionHandler does not automatically cover those responses or authorize changing the envelope to ProblemDetails.

Once Response.HasStarted, propagate failure appropriately; do not swallow it or append another JSON envelope. Keep HEAD and 204 bodyless, and binary/streaming success responses intact. Distinguish client cancellation from unexpected failures. Configure .NET 10 handled-exception diagnostics deliberately and assign one unexpected-error logging owner.

## 8. Logging and RabbitMQ/MassTransit

Use Microsoft `ILogger<T>` with providers; do not implement an unnecessary custom logger or route routine diagnostics through business messaging. Request completion logging records safe metadata, duration/status and correlation. Do not log passwords, tokens, OTPs, raw request bodies, PII, or other sensitive user-provided data by default. Redact/structure sensitive identifiers according to the applicable security/audit contract. Separate operational logs from business audit records.

### Service integration decision rule

Use **synchronous internal service APIs** for immediate authoritative decisions required to complete the current command.

Use **RabbitMQ/MassTransit** for durable facts, asynchronous commands, downstream reactions and eventually consistent integration.

Do not use publish/subscribe merely as RPC in disguise.

A business dependency does not automatically require a synchronous network dependency. For example, Parking Operations may business-depend on successful payment without keeping an HTTP request open while waiting for payment confirmation.

Use HTTP when the caller needs the authoritative result now to continue the current request, for example:

- recognize plate when recognition is a required immediate dependency;
- find/validate the current parking session;
- calculate authoritative price;
- create/retrieve a payment intent.

Use a RabbitMQ integration event when a durable business fact has already occurred and other services need to react eventually and reliably, for example:

- `PaymentSucceeded`;
- `ReservationExpired`;
- `VehicleEntered`;
- `CheckoutCompleted`;
- `RefundCompleted`.

Prefer fact-style integration events for downstream reactions. Do not publish a cross-service command merely to tell another bounded context exactly how to mutate its owned aggregate unless an explicitly approved asynchronous command contract requires that behavior.

SignalR is for realtime client notification, not durable backend integration and not proof that the business transaction committed.

### Messaging implementation boundary

Define Application `IEventPublisher`/integration ports and implement them using Infrastructure MassTransit/RabbitMQ. Verify installed versions and scoped dependencies. Keep Domain independent of transport packages. Use approved event ownership, schema/version, identifiers, intended consumers and minimal payload. Do not invent downstream uses. Audit identity must distinguish actor Account from affected User. Resolve no-op event semantics explicitly.

When a business state change and its integration event must not diverge, use the framework-supported transactional Bus Outbox with the **same owning DbContext** so the business mutation and outbox rows commit atomically. A single `SaveChangesAsync` does not need an extra manual transaction solely for the outbox; introduce an explicit transaction only when the use case spans multiple saves/database steps that must commit as one unit. Do not substitute direct publication, an in-memory buffer or a bespoke relay without a new decision.

The following **profile mutation is only an illustrative SmartPark example** of the general atomic-outbox rule; it does not make profile publication mandatory for unrelated features:

```csharp
// Illustrative ordering: publisher is scoped and configured for the EF Bus Outbox.
user.ChangeFullName(name);
await publisher.PublishAsync(approvedEvent, ct); // captured by the configured Bus Outbox
await work.SaveChangesAsync(ct);                 // business change + outbox rows commit together
// Background delivery later publishes to RabbitMQ.
```

The example requires verified outbox integration; it is unsafe with a direct broker publisher. When a feature contract requires an event, API success means the required business change and outbox capture are durably recorded; it does **not** mean downstream consumers have finished.

Producer and consumer delivery may be implemented/deployed separately only when the approved rollout explicitly allows it. Before enabling delivery, make sure the required durable topology/consumer ownership is defined, or use an explicitly supported mode that retains outbox capture while delivery is intentionally disabled. Broker acceptance alone does not guarantee storage for subscribers that did not have the necessary durable topology. Define topology ownership, retention, cleanup, pending-backlog monitoring and retry/error behavior. No expiry/overflow policy may silently contradict required delivery.

Expect duplicate delivery; future consumers need idempotency and atomic processing/deduplication appropriate to their store. Do not promise end-to-end exactly-once. Outbox state is not a permanent audit log. RabbitMQ durability is not event sourcing, and no event-replay system is implied.

## 9. Schema and rollout constraints

Inspect existing EF mappings/migrations/SQL scripts before proposing tables. Use installed MassTransit mappings for compatible outbox entities and indexes; distinguish required producer and consumer state. Do not create overlapping custom tables.

Generate a reviewable PostgreSQL script and explain tables/indexes, cleanup, permissions, deployment order and risky rollback. Keep migration/model snapshot consistent where applicable. Never auto-apply to existing/shared DB or enable startup migrations under the current approval. Disposable verified isolated test DBs are separate from user schema deployment.

When a feature's correctness depends on the transactional outbox, missing required outbox schema means that feature is not deployment-ready. Never hide missing schema with direct publishing, dropped events or fake success. Distinguish code completion from deployment readiness.

## 10. Verification and handoff

Review behavior, not merely coverage percentages. Execute focused tests and required gates; do not repeat broad testing without a concrete reason. Report actual commands/outcomes and environment limitations. Never inherit old session results as current evidence.

Use the lightest verification level that can actually prove the behavior.

For RabbitMQ/MassTransit work, prefer in this order as appropriate:

1. unit tests for pure Domain/Application behavior;
2. integration tests for outbox, consumer idempotency, database concurrency and adapter behavior;
3. API tests for exposed contracts;
4. targeted manual cross-service verification when it is materially quicker and the remaining risk is understood.

For local broker-backed verification, prefer reusing one development broker/container across many tests rather than starting a broker per function/test method:

```text
docker compose up rabbitmq
        ↓
many tests reuse it
```

A real RabbitMQ integration test is required when the claim depends on broker topology, routing, retry/redelivery, durability, or outbox recovery. An in-memory bus test does not prove those properties.

Select verification categories according to the behavior changed; do not mechanically run irrelevant suites:

- HTTP success plus applicable binding/type/content errors; correct status/envelope; challenge/forbid headers; 404/405/415/429 or other framework producers when relevant.
- Authorization for every role/scope affected by the feature, including current Account/session validity, own-resource/tenant/site boundaries, revocation/lock behavior, and service-to-service identity where applicable.
- Input and Domain boundary cases, including unknown JSON fields being ignored without mutating protected/unsupported fields.
- Concurrency tests proportional to the actual invariant: simple accepted last-write-wins updates do not require lock/race harnesses solely for ceremony; flows that depend on serialization require real PostgreSQL conflicting interleavings, fresh-state verification and every participating writer/protocol.
- When messaging/outbox is in scope: business/outbox atomicity, no event after rollback, stable event identity across delivery retries, duplicate/redelivery safety, broker outage/recovery, durable routing/topology and consumer idempotency as applicable.
- Error-path behavior including response-started failures, HEAD/204/stream success, cancellation and redacted/nonduplicated logs when affected.
- Frontend responsiveness across representative desktop/tablet/mobile widths when UI code changes.

Mocked locks/transactions do not prove database concurrency. In-memory bus tests do not prove real RabbitMQ durability. A missing environment is a disclosed validation gap, not a pass. Use production-like DI to verify behaviors/adapters are actually registered.

Deliver actual files changed, request-to-response call path, decisions versus proposals, SQL/setup instructions, tests and residual risks. For PR review, lead with concrete defects and file/symbol evidence; distinguish requirements violations from style preferences. Include suggested Conventional Commit text without committing unless authorized. Checkpoint phases in the established task/session document to support resumption.

## 11. Quick rejection checklist for agent-generated changes

- Reject client-selected actor/owner identifiers used as authorization authority for own-resource operations, and reject authorization that ignores the authenticated/current Account/session/scope required by the contract.
- Reject transactions/locks added without an identified concurrency invariant, and reject atomic-authorization claims when competing writers are not coordinated through the same protocol and fresh state.
- Reject broad tracked-entity Update or request-to-entity mapping that can overwrite security fields.
- Reject unapproved client-controlled references, ownership claims, media/security-field mutations, or invented read conversions that are not supported by the authoritative contract.
- Reject strict unknown-JSON rejection unless an approved endpoint contract explicitly requires it; reject ISender substitution against the approved IMediator convention; reject duplicated HTTP status in the public error body.
- Reject safe-error claims that miss framework response producers, swallow started-response exceptions or expose unknown messages.
- Reject a new custom catch-all exception middleware that duplicates the approved `UseExceptionHandler` + `IExceptionHandler` direction without explicit architecture approval.
- Reject direct broker publication presented as atomic outbox, silent unrouted events or claims that a future consumer receives historical publications automatically.
- Reject RabbitMQ/pub-sub used as synchronous RPC in disguise when an immediate authoritative service API is the approved fit.
- Reject Domain code that fetches repositories/services instead of receiving already-resolved facts from Application.
- Reject repeated business-state transition logic scattered across handlers/repositories/consumers when a shared aggregate/domain state machine should own it.
- Reject FE code that treats SignalR or client-calculated financial values as authoritative business state.
- Reject hardcoded CORS origins/service URLs that belong to environment configuration.
- Reject new cross-service/architecture direction implemented without the architecture approval gate when the guide/approved contracts do not already decide it.
- Reject scripts auto-applied to the user DB, guessed schema, fabricated test passes, or concealed pending contract approvals.

## 12. Reference entry points

Use version-matched official sources when implementation details are uncertain:

- ASP.NET Core error handling: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0
- EF tracking behavior: https://learn.microsoft.com/en-us/ef/core/querying/tracking
- PostgreSQL locks: https://www.postgresql.org/docs/current/explicit-locking.html
- MassTransit outbox: https://masstransit.io/documentation/patterns/transactional-outbox
- .NET integration-event architecture: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/asynchronous-message-based-communication

These references explain mechanisms, not project approvals. Resolve redirects and verify the installed version before copying API calls.
