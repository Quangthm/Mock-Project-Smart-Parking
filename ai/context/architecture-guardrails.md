# SmartPark AI Agent Architecture Guardrails

**Status:** WORKING
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

Controllers should normally interact with the Application layer through MediatR/`ISender`. Controllers should not directly depend on `DbContext`, repository implementations, PostgreSQL, Kafka, payment providers, or other infrastructure implementations.

### Data ownership

- Each microservice owns its persistence boundary.
- Do not share a database context across microservices.
- Do not directly query another service's database.
- Cross-service communication uses synchronous APIs when an immediate response is required and Kafka when asynchronous processing/loose coupling provides a meaningful benefit.

## CQRS direction

CQRS + MediatR is the intended application pattern.

Target flow:

```text
HTTP Request
  ↓
Controller
  ↓
ISender / MediatR
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

## Kafka rule

Kafka is not automatically required for every operation.

For any proposed event-driven interaction, record:

1. Why asynchronous communication is needed.
2. Which service owns the event.
3. Which services consume it.
4. Failure behavior when Kafka or a consumer is unavailable.
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

Do not silently resolve architecture inconsistencies that the project has explicitly deferred for architecture review.
