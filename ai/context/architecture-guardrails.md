# SmartPark AI Agent Architecture Guardrails

**Status:** WORKING
**Last Updated:** 2026-10-29
**Purpose:** Rules AI coding/review agents must follow unless a newer confirmed project decision explicitly changes them.

## Architecture

SmartPark is currently directed toward a microservice architecture with event-driven integration.

Each microservice owns its business logic and persistence boundary.

### Service structure

For sufficiently complex services, the intended structure is:

```text
Service/
├── API
├── Application
├── Domain
├── Infrastructure
└── Persistence
```

### Dependency rules

```text
Application → Domain
Persistence → Application + Domain
Infrastructure → Application + Domain
API → Application + implementation projects for composition/wiring
```

Controllers should normally interact with the Application layer through MediatR/`IMediator` by current project convention. Do not substitute `ISender` unless an explicit project-level decision changes that convention. Controllers should not directly depend on `DbContext`, repository implementations, PostgreSQL, RabbitMQ, payment providers, or other infrastructure implementations.

### Data ownership

- Each microservice owns its persistence boundary.
- Do not share a database context across microservices.
- Do not directly query another service's database.
- Use synchronous internal service APIs when an **immediate authoritative answer is required to complete the current command**.
- Use RabbitMQ through MassTransit, with approved Outbox/Inbox patterns where required, for **durable facts, asynchronous commands, downstream reactions and eventually consistent integration**.
- Do not use publish/subscribe merely as RPC in disguise. A business dependency does not automatically require a synchronous network dependency.
- SignalR is for realtime client notification, not durable backend integration and not proof of committed business state.

### API design and contracts

- API designs must use the canonical template at [`docs/03-design/api/api-design-template.md`](../../docs/03-design/api/api-design-template.md).
- Contract changes must specify route, HTTP method, authentication/authorization, request/response models, validation errors, and observable state/retry outcomes without exposing live secrets.
- API exception handling follows the built-in ASP.NET Core exception-handling middleware (`UseExceptionHandler`) with registered `IExceptionHandler` implementations. `IExceptionHandler` is invoked by the framework middleware; it does not remove middleware from the request pipeline.
- Do not create or retain a parallel custom catch-all `ExceptionHandlingMiddleware` that duplicates the approved `IExceptionHandler` pipeline unless a new architecture decision explicitly requires it.
- Keep the approved public error envelope (`success`, `code`, `message`, `errors`, `traceId` as applicable) consistent; set the actual HTTP status on the response and do not duplicate HTTP status in the JSON body.

## CQRS direction

CQRS + MediatR is the intended application pattern.

Target flow:

```text
HTTP Request
  ↓
Controller
  ↓
IMediator / MediatR
  ↓
Command / Query
  ↓
Validation Pipeline
  ↓
Handler
  ↓
Application Interface
  ↓
Persistence / Infrastructure implementation
```

Do not add deferred infrastructure merely to make a milestone appear larger.

## Messaging rule (RabbitMQ)

RabbitMQ is the current messaging direction; Kafka is not used. A broker is not automatically required for every operation.

Decision shortcut:

```text
Immediate authoritative answer needed
        ↓
Internal service API

Durable fact / downstream reaction
        ↓
MassTransit + RabbitMQ + Outbox
```

For any proposed event-driven interaction, record:

1. Why asynchronous communication is needed.
2. Which service owns the event.
3. Which services consume it.
4. Failure behavior when the broker or a consumer is unavailable.
5. Whether eventual consistency is acceptable.
6. Whether a simpler synchronous interaction is sufficient for the MVP.

## AI authority boundary

The AI assistant must consume authoritative backend information.

```text
User
 ↓
AI Chatbox
 ↓
Tool / Function / API Call
 ↓
Backend Authentication + Validation
 ↓
Business Service / Domain Rules
 ↓
Authoritative State / Database
 ↓
Result
 ↓
AI Response
```

AI must not:

- directly access the database;
- invent availability, pricing, reservation status, payment state, refund status, or policy rules;
- execute arbitrary SQL;
- independently authorize payment;
- directly change authoritative business state.

For write operations:

```text
AI prepares / assists
 ↓
UI displays actual action/form
 ↓
USER CONFIRMS
 ↓
Backend executes
 ↓
AI reports actual backend result
```

Payment always follows the normal payment/checkout confirmation flow.

## Product AI foundation scope

The current Chatbox foundation can proceed in parallel with unresolved business-rule work. Current foundation priorities are:

- natural-language conversation;
- conversation state/context;
- system prompt and safety/boundary rules;
- generic tool/API calling pipeline;
- UI action/confirmation handoff;
- fallback and integration testing.

Likely initial retrieval tools include parking search, active reservations, reservation details, current parking session, payment status, refund status, and saved vehicles. Exact contracts remain provisional where the BA baseline is still open.

## Change discipline

`docs/07-skills/SmartPark-Coding-Skill-v1.0.md` is the primary shared implementation convention for coding/review work. These guardrails provide architecture boundaries and must be read as additional context, not as a competing coding standard.

Do not silently resolve architecture inconsistencies that the project has explicitly deferred for architecture review. If implementation requires an architectural/cross-service/security/data-ownership direction not already approved, follow the shared coding skill's architecture/direction approval gate: propose a reusable strategy, explain costs/benefits/maintainability impact, and obtain explicit approval before implementing the new direction.
