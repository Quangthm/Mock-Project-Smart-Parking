# SmartPark AI Development Current State

**Status:** WORKING
**Snapshot Date:** 2026-10-21 (Updated for Sprint 3 & SRS v1.1 Baseline)

## Current phase

The architecture foundation is established, core services are active, and Sprint 3 database integration and application services are operational.

**Verified implementation state (2026-10-21):**

- **UserService** — full 5-layer Clean Architecture; CQRS/MediatR, FluentValidation, AutoMapper, EF Core + PostgreSQL, JWT auth, BCrypt. Reference/template service.
- **ParkingService** — 5-layer structure (`API`, `Application`, `Domain`, `Infrastructure`, `Persistence`); Minimal APIs; raw Npgsql + PostgreSQL. Implements operational layout, gate synchronization, and slot state endpoints (`SPARK-188`).
- **ReservationService** — 4 projects (`API`, `Application`, `Domain`, `Persistence`); raw Npgsql; reservation safety and gate claim models.
- **IoTService** — Python AI service (`src/Services/IoTService/python-ai/`) implementing CCTV/camera stream ingestion and license plate recognition (LPR).
- **Frontend (`FE/`)** — React 19 + Vite 8 + Tailwind v4 + Three.js 0.186.1 + MapTiler 4.1.0; complete role-based dashboards (Admin, Owner, Operator, Driver) with live backend API integrations.
- **Database & Migration Stacks** — 6 microservice databases (`compose.schema-integration.yml`, `scripts/apply-workflow-migrations.ps1`, `scripts/database/`); automated setup and seed via `setup.ps1` and `scripts/seed-demo-site.ps1`.
- **Application Orchestration** — `compose.app.yml` containerized multi-app runner, `scripts/run-app-service.ps1`, and host schema runners.
- **Tests** — xUnit backend tests (`tests/Services/*` for UserService and ParkingService), node:test frontend tests (`tests/frontend/*`).

**Implemented / Active:**
- Clean Architecture, CQRS/MediatR, EF Core (UserService), raw Npgsql (ParkingService, ReservationService), PostgreSQL schema-per-service.
- Docker compose stacks (`compose.schema-integration.yml`, `compose.app.yml`, `compose.sonar.yml`, `compose.ai.yml`).
- 5 Core Skills for AI Agents (`ai/rule.md`, `docs/07-skills/`).

**Still planned / not implemented (do not assume these exist):**
- Asynchronous message broker (RabbitMQ/MassTransit planned, only stub constant exists).
- Kafka (not used, deferred).
- API Gateway (Ocelot/YARP planned, direct service calls in dev).
- MQTT / SignalR real-time push (polling / REST fallback in place; SignalR hub planned).
- Real payment gateway integration (mocked / PayOS integration guide ready).
- Commercial Cloudflare Turnstile integration.

## Parallel workstreams

- Business Rules and Policy reconciliation against SRS v1.1.
- API Contract refinement and OpenAPI schema generation.
- 3D Digital Twin visual telemetry and Three.js operational layout binding.
- Architecture alignment and outbox/event-driven message broker onboarding.

## Canonical Reference Rule

Before changing code or requirements, refer to `ai/AGENTS.md` and `ai/architecture-reference.md`. Canonical requirements are governed by **SRS v1.1** (`docs/01-requirements/srs/smartpark-srs-v1.1.md`).
