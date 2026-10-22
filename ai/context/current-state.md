# SmartPark AI Development Current State

**Status:** WORKING
**Snapshot Date:** 2026-10-15

## Current phase

The team has completed the architecture foundation and is implementing feature slices across microservices using the established CQRS/MediatR pipeline.

The first template microservice was User Service. Payment Service, Parking Service, and Reservation Service are under active development.

The current codebase already includes: CQRS/MediatR pipeline, FluentValidation, EF Core/PostgreSQL, global exception handling middleware. Currently deferred: RabbitMQ/MassTransit integration (Outbox/Inbox), Docker orchestration, and GitHub CI/CD until the relevant milestone.

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
