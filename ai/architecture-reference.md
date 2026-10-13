# SmartPark — Architecture Reference

**Status:** WORKING
**Last Updated:** 2026-10-12
**Purpose:** Verified architecture facts for AI agents — tech stack (implemented vs planned), service boundaries, layer responsibilities, CQRS flow, function placement, and definition of done.
**Read when:** planning or implementing any cross-cutting change, or when you need to know what actually exists in the repo.
**Companion files:** `ai/context/architecture-guardrails.md` (rules), `ai/context/current-state.md` (state), `ai/context/project-context.md` (shared context).

> This file records **verified repo facts** (checked against code). Where docs and code disagree, the Conflict Register at the end is authoritative for "what actually exists".

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
- `payment-db`, `notification-db`, `iot-db` schemas exist in the database but have **no service project** yet.

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

Dependency direction:

```text
Application → Domain
Persistence → Application + Domain
Infrastructure → Application + Domain
API → Application + implementations (composition/wiring only)
```

Layer responsibilities:

| Layer | Responsibility | Must not |
|---|---|---|
| API | HTTP endpoints, request/response mapping, DI composition | contain business logic; depend on DbContext/repositories directly |
| Application | Commands/queries, handlers, validation, DTOs, interfaces | depend on infrastructure implementations |
| Domain | Entities, value objects, business rules, domain events | depend on any other layer |
| Infrastructure | External integrations (email, OTP transport, etc.) | own persistence |
| Persistence | EF Core DbContext / raw Npgsql, repositories, migrations | contain business logic |

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

- Controllers talk to the Application layer via MediatR/`ISender`.
- Controllers must not depend on `DbContext`, repository implementations, PostgreSQL, or other infrastructure implementations.

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

| # | Topic | Docs say | Repo reality | Handling |
|---|---|---|---|---|
| 1 | Messaging | Kafka | RabbitMQ is the current direction; no Kafka/MassTransit code (only a stub) | Follow RabbitMQ; flag Kafka references as stale |
| 2 | `ai/context/current-state.md` | CQRS/EF/Postgres deferred | Implemented in UserService/ParkingService | Verify against code |
| 3 | UI library | Material UI | Not present (Tailwind only) | Do not add MUI |
| 4 | Gateway / SignalR / MQTT / Redis / payment / LPR / AI | Present | Not implemented (UI mockups / scaffold only) | Do not assume they exist |
| 5 | CI | — | `.gitlab-ci.yml` uses `dotnet/sdk:7.0`; project targets `net10.0` | Flag mismatch; do not "fix" without approval |
| 6 | ReservationService | 5 layers | Only API + Persistence | Do not assume missing layers exist |
| 7 | UserService namespaces | — | Mixed `UserService.*` vs `SmartParking.UserService.*` | Match the surrounding file |

---

## 9. Canonical sources

- Requirements: `docs/01-requirements/srs/smartpark-srs-v0.9.md`
- Use cases: `docs/01-requirements/use-cases/smartpark-use-cases-v0.9.md`
- Business rules: `docs/01-requirements/business-rules/smartpark-business-rules-v0.8.5.md`
- Solution architecture: `docs/02-architecture/solution-architecture/SmartPark_Solution_Architecture_v0.9.md`
- Service boundaries: `docs/02-architecture/service-boundaries/`
- Database: `docs/02-architecture/database/`

---

**End.** Update this file when the verified stack, service boundaries, or layer structure changes.
