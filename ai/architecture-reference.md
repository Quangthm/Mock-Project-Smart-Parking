# SmartPark — Architecture Reference

**Status:** WORKING
**Last Updated:** 2026-10-12
**Purpose:** Single source of truth for verified architecture facts — tech stack (implemented vs planned), service boundaries, layer responsibilities, CQRS flow, function placement, and definition of done.
**Read when:** planning or implementing any backend, cross-service, database, architecture, or API contract change, or when you need to know what actually exists in the repo.
**Canonical owner of:** Technology inventory, service boundaries, Clean Architecture rules, Conflict Register.
**Companion files:** `ai/context/architecture-guardrails.md` (detailed guardrails), `ai/context/current-state.md` (current state + backlogs), `ai/context/project-context.md` (shared domain context), `ai/context/important-decisions.md` (locked/working decisions).

> This file records **verified repo facts** (checked against code). Where docs and code disagree, the Conflict Register (§8) is authoritative for "what actually exists".

---

## 1. Tech stack

### Implemented (verified in repo)

| Layer | Technology | Evidence |
|---|---|---|
| Runtime | .NET 10 (`net10.0`) | all `*.csproj` |
| Backend framework | ASP.NET Core Web API | `Microsoft.NET.Sdk.Web` |
| UserService | Clean Architecture (5 projects) + EF Core 10.0.12 + MediatR 14.2.0 + FluentValidation 12.1.1 + AutoMapper 16.2.0 + BCrypt.Net-Next 4.2.0 | `src/Services/UserService/*` |
| ParkingService | Clean Architecture (5 projects) + raw Npgsql 10.0.3 + Minimal APIs | `src/Services/ParkingService/*` |
| ReservationService | 2 projects (API + Persistence) + raw Npgsql 10.0.3 | `src/Services/ReservationService/*` |
| Database | PostgreSQL (service-owned schemas) | `docker-compose.yml`, `docs/02-architecture/database/` |
| Frontend | React 19 + react-router-dom 7.18.4 + Vite 8.0.5 + TypeScript 5.7 + Tailwind CSS 4.0 + Three.js 0.186.1 + @maptiler/sdk 4.1.0 + recharts 3.10.1 | `FE/package.json` |
| Backend tests | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.14.1 + coverlet 6.0.4 | `tests/Services/*` |
| Frontend tests | node:test (`*.test.cjs`) | `tests/frontend/*` |

### Planned / not implemented (do not assume these exist)

| Item | Status |
|---|---|
| RabbitMQ / MassTransit | Direction only; only a stub constant exists |
| Kafka | Not present |
| MQTT / SignalR / Redis | Not present |
| API Gateway | Not present |
| Material UI | Not present (Tailwind only) |
| LPR / camera / sensor integration | UI mockup only |
| Payment integration | UI mockup only |
| AI chatbot | Scaffold present but disabled |
| Cloudflare Turnstile | Not present |

---

## 2. Service boundaries & ownership

| Service | Owns | Projects |
|---|---|---|
| UserService | Identity, auth, users, roles, driver registration, OTP, owner registration/approval, operator provisioning, admin user management | API, Application, Domain, Infrastructure, Persistence |
| ParkingService | Parking structures, slots, sites, operator/site management | API, Application, Domain, Infrastructure, Persistence |
| ReservationService | Reservations, allocation/conflict logic | API, Persistence |

- Each service owns its persistence boundary. No cross-service database access.
- `payment-db`, `notification-db`, `iot-db` schemas exist in the database design documentation but have **no service project** yet. Do not assume these databases are operationally available.

---

## 3. Clean Architecture layers & dependency rules

Intended structure for sufficiently complex services:

```text
Service/
├── API
├── Application
├── Domain
├── Infrastructure
└── Persistence
```

Dependency direction (MUST be followed):

```text
Application → Domain
Persistence → Application + Domain
Infrastructure → Application + Domain
API → Application + implementations (composition/wiring only)
```

Layer responsibilities:

| Layer | Responsibility | MUST NOT |
|---|---|---|
| API | HTTP endpoints, request/response mapping, DI composition | Contain business logic; depend on DbContext/repositories directly |
| Application | Commands/queries, handlers, validation, DTOs, interfaces | Depend on infrastructure implementations |
| Domain | Entities, value objects, business rules, domain events | Depend on any other layer |
| Infrastructure | External integrations (email, OTP transport, etc.) | Own persistence |
| Persistence | EF Core DbContext / raw Npgsql, repositories, migrations | Contain business logic |

---

## 4. CQRS + MediatR flow

```text
HTTP request
  ↓
Controller / Minimal API endpoint
  ↓
ISender / MediatR
  ↓
Command / Query
  ↓
Validation pipeline (FluentValidation)
  ↓
Handler
  ↓
Application interface
  ↓
Persistence / Infrastructure implementation
```

- Controllers MUST talk to the Application layer via MediatR/`ISender`.
- Controllers MUST NOT depend on `DbContext`, repository implementations, PostgreSQL, or other infrastructure implementations.

---

## 5. Function placement

| Function | Where it belongs |
|---|---|
| Business rule / invariant | Domain |
| Use-case orchestration | Application handler |
| Input validation | Application (FluentValidation) |
| Persistence / queries | Persistence |
| External integration (email, OTP, HTTP) | Infrastructure |
| HTTP endpoint / routing | API |
| Cross-service call | Application interface, implemented in Infrastructure |

---

## 6. Repository placement

| Artifact | Location |
|---|---|
| Service code | `src/Services/<Service>/SmartParking.<Service>.<Layer>/` |
| Backend tests | `tests/Services/SmartParking.<Service>.Tests/` |
| Frontend | `FE/` |
| Frontend tests | `tests/frontend/` |
| Canonical docs | `docs/` |
| AI workspace | `ai/` |

---

## 7. Definition of done (validation before completion)

Before reporting a change complete:

- [ ] `dotnet build SmartParking.slnx` passes (0 errors).
- [ ] `dotnet test SmartParking.slnx` passes.
- [ ] No secrets added to tracked files.
- [ ] No cross-service database access introduced.
- [ ] Dependency direction respected (no business logic in API, no infra dependency in Application).
- [ ] Relevant canonical doc / context record updated if behavior or decisions changed.
- [ ] Changed files listed; unresolved questions flagged as `OPEN` / `DEFERRED` / `PROPOSED`.

---

## 8. Conflict Register (docs vs code)

> **This is the canonical location** for the Conflict Register. Other files may point here but MUST NOT reproduce this table.

| # | Topic | Docs say | Repo reality | Business/technical risk | Required agent behavior | Resolution status |
|---|---|---|---|---|---|---|
| 1 | Messaging | Kafka (Solution Architecture doc) | RabbitMQ is the current direction; no Kafka/MassTransit code exists (only a stub constant) | Using Kafka APIs/contracts when only RabbitMQ is planned would create dead code and wrong dependencies | Follow RabbitMQ direction; flag any Kafka references as stale | Unresolved — Kafka references remain in architecture docs |
| 2 | `ai/context/current-state.md` | CQRS/EF/Postgres listed as deferred | Implemented in UserService and ParkingService | Treating implemented features as deferred wastes effort re-implementing them | Treat current-state deferred list as stale for these items; verify against code | Partially resolved — current-state.md updated 2026-10-12 |
| 3 | UI library | Material UI (Solution Architecture doc) | Not present in `FE/package.json` (Tailwind CSS only) | Adding MUI would conflict with existing Tailwind setup | Do not add MUI; use Tailwind CSS | Unresolved — MUI references remain in architecture docs |
| 4 | Gateway / SignalR / MQTT / Redis / payment / LPR / AI | Present in architecture and SRS documents | Not implemented; UI mockups and scaffold only | Assuming these exist leads to broken integrations | Do not assume they exist; verify before depending on them | Unresolved — planned components |
| 5 | CI SDK version | Not specified | `.gitlab-ci.yml` uses `dotnet/sdk:7.0`; project targets `net10.0` | CI builds will fail on target framework mismatch | Flag mismatch; do not "fix" CI without explicit approval | Unresolved |
| 6 | ReservationService layers | 5 layers (architecture docs) | Only API + Persistence projects exist | Assuming Application/Domain/Infrastructure layers exist leads to import errors | Do not assume missing layers exist; check project structure | Unresolved |
| 7 | UserService namespaces | Not specified | Mixed `UserService.*` vs `SmartParking.UserService.*` namespace conventions | Inconsistent namespaces cause build errors when adding references | Match the namespace convention of the surrounding file | Unresolved |

---

## 9. Canonical sources & Precedence

Canonical source definitions, document hierarchy, and precedence rules are maintained centrally in [ai/AGENTS.md](AGENTS.md) (§ Source of truth).
- **API Design Specification & Template:** [docs/03-design/api/api-design-template.md](../docs/03-design/api/api-design-template.md) governs endpoint contracts, request/response models, and error codes.

**Core Precedence Principle:**
- The SRS is authoritative for requirements. Context files, handoffs, and decision records interpret it; they do not replace it.
- If code and documentation disagree, consult §8 (Conflict Register) above for verified repository reality.
- When an architectural question is unanswered in canonical documents, classify it as `OPEN`, `DEFERRED`, or `PROPOSED` rather than inventing an assumption.

---

**End.** Update this file when the verified stack, service boundaries, or layer structure changes.
