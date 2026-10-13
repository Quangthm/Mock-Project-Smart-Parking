# SmartPark AI Development Current State

**Status:** WORKING
**Snapshot Date:** 2026-10-12

## Current phase

The architecture foundation is established and the first services are implemented. The team is now building features on top of the working baseline.

**Verified implementation state (2026-10-12):**

- **UserService** — full 5-layer Clean Architecture; CQRS/MediatR, FluentValidation, AutoMapper, EF Core + PostgreSQL, JWT auth, BCrypt. This is the reference/template service.
- **ParkingService** — 5-layer structure; Minimal APIs; raw Npgsql (no EF Core).
- **ReservationService** — only API + Persistence layers; raw Npgsql.
- **Frontend** — React 19 + Vite + Tailwind v4 + Three.js; auth and operator-provisioning flows wired to the backend.
- **Tests** — xUnit backend tests (`tests/Services/*`), node:test frontend tests (`tests/frontend/*`).
- **Docker** — `docker-compose.yml` present (PostgreSQL + services).

No longer deferred (previously listed as deferred): CQRS/MediatR, validation pipeline, EF Core/PostgreSQL implementation, Docker implementation.

Still not implemented: messaging broker (RabbitMQ planned), API Gateway, SignalR/MQTT/Redis, payment/LPR/AI integrations, GitHub CI/CD (GitLab CI present but uses a mismatched SDK image).

## Parallel workstreams

Current work may proceed in parallel against the working baseline:

- Business Rules
- API Contract
- AI Chatbox foundation
- Architecture alignment
- Testing/review

## AI Chatbox status

The AI team may build the conversational/tooling foundation without waiting for every future reservation business-rule value to be finalized.

The AI foundation should preserve provisional contracts where BA decisions are still open.

## Known AI cleanup backlog

The current SRS contains legacy AI sections that need reconciliation with the newer Chatbox architecture. Review the older chatbot use cases, refund/dispute language, recommendation scope, implementation phases, accuracy/performance requirements, and any assumption that AI can directly execute actions.

This cleanup is a deferred AI-specific task, not a reason to block the foundation work.

## Known architecture alignment backlog

- Validate User Service boundary and auth ownership.
- Validate Parking Service ownership.
- Validate Reservation Service ownership of allocation/conflict logic.
- Determine Pricing ownership.
- Determine Search/Recommendation boundary.
- Determine LPR/camera/sensor ownership.
- Determine AI/Chatbox service boundary.
- Validate event publisher/consumer ownership.
- Validate Gateway vs service-level authorization.
- Review synchronous vs asynchronous communication for critical reservation/payment workflows.
- Review consistency, idempotency, retry, outbox, and compensation strategy.

## Rule

Before changing code or requirements, read this file together with `project-context.md` and `architecture-guardrails.md`, then locate the canonical source document relevant to the task.
