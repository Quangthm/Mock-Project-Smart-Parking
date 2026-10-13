# SmartPark — AI Agent Instructions

> **Thin operational entry point.** This file tells AI coding agents what to read and which rules must not be broken. Deep architecture, state, and decisions live in the companion files under `ai/` and `docs/` — read them; do not duplicate them here.

## Project snapshot

- **Repo root:** the repository root that contains this file. All paths in this document are relative to it.
- **Solution:** `SmartParking.slnx`
- **Backend:** ASP.NET Core / .NET 10 microservices — Clean Architecture + CQRS/MediatR
- **Services present:** `src/Services/UserService`, `src/Services/ParkingService`, `src/Services/ReservationService`
- **Frontend:** `FE/` — React 19 + Vite 8 + Tailwind CSS 4 + Three.js (see `FE/AGENTS.md`)
- **Database:** PostgreSQL, one schema per service
- **Messaging:** RabbitMQ (current direction) — see Conflict Register
- **Current working branch:** `docs/agents-and-digital-twin-spec` (base `develop-v2`)

## Required context workflow

Read these before changing anything:

1. `ai/context/project-context.md` — minimum shared context
2. `ai/context/current-state.md` — current state + backlogs
3. `ai/context/architecture-guardrails.md` — architecture rules
4. `ai/context/important-decisions.md` — locked / working decisions
5. `ai/architecture-reference.md` — verified architecture facts, service map, layer responsibilities, DoD
6. `ai/rule.md` — session progress tracking rule (live log)
7. The canonical source document(s) for your task (see Source of truth)

## Source of truth

Use the most specific applicable source. Do not silently resolve conflicts.

| Topic | Canonical source |
|---|---|
| Requirements | `docs/01-requirements/srs/smartpark-srs-v0.9.md` |
| Use cases | `docs/01-requirements/use-cases/smartpark-use-cases-v0.9.md` |
| Business rules | `docs/01-requirements/business-rules/smartpark-business-rules-v0.8.5.md` |
| Architecture | `docs/02-architecture/solution-architecture/SmartPark_Solution_Architecture_v0.9.md` |
| Service boundaries | `docs/02-architecture/service-boundaries/` |
| Database | `docs/02-architecture/database/` |
| Git / commit convention | `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` |

- The SRS is authoritative. Context, handoff, and decision records explain how to interpret it; they do not replace it.
- When a question is unanswered, mark it `OPEN`, `DEFERRED`, or `PROPOSED`. Do not invent values.
- Do not silently resolve conflicts between docs and code — surface them (see Conflict Register).

## Safety rules

- Do not print, commit, or copy secrets (`.env`, `*.user`, user-secrets, connection strings).
- Do not revert user/uncommitted changes unless explicitly requested.
- Before risky operations (delete, drop DB, force push, destructive migration) → log pre-flight and ask the user.
- Only commit, amend, push, or create PRs when explicitly requested.
- Do not start/stop Docker containers or the app unless asked.

## Architecture guardrails (summary)

Full rules: `ai/context/architecture-guardrails.md`.

- Microservices; each service owns its business logic and persistence boundary.
- Clean Architecture dependency direction: `Application → Domain`; `Persistence/Infrastructure → Application + Domain`; `API → Application + implementations` (composition/wiring only).
- Controllers talk to the Application layer via MediatR/`ISender`. Controllers must not depend on `DbContext`, repository implementations, PostgreSQL, or other infrastructure implementations.
- No cross-service database access. Cross-service calls use synchronous APIs when an immediate response is required, and messaging only when async/loose coupling gives a real benefit.
- AI authority boundary: **AI assists → backend decides → user confirms writes → backend executes → AI reports actual result.** AI must not access authoritative databases, invent business state, or authorize payment.

## Known conflicts / do-not-trust (Conflict Register)

Docs and code disagree in places. Do not act on the "Docs say" column without confirming against code.

| # | Topic | Docs say | Repo reality | Handling |
|---|---|---|---|---|
| 1 | Messaging | Kafka | RabbitMQ is the current direction; no Kafka/MassTransit code (only a stub) | Follow RabbitMQ; flag Kafka references as stale |
| 2 | `ai/context/current-state.md` | CQRS/EF/Postgres deferred | Implemented in UserService/ParkingService | Treat current-state as stale; verify against code |
| 3 | UI library | Material UI | Not present (Tailwind only) | Do not add MUI |
| 4 | Gateway / SignalR / MQTT / Redis / payment / LPR / AI | Present | Not implemented (UI mockups / scaffold only) | Do not assume they exist |
| 5 | CI | — | `.gitlab-ci.yml` uses `dotnet/sdk:7.0`; project targets `net10.0` | Flag mismatch; do not "fix" without approval |
| 6 | ReservationService | 5 layers | Only API + Persistence | Do not assume missing layers exist |
| 7 | UserService namespaces | — | Mixed `UserService.*` vs `SmartParking.UserService.*` | Match the surrounding file |

## Verification commands

Run from repo root unless noted.

```powershell
git status --short --branch --untracked-files=all
```

```powershell
dotnet build SmartParking.slnx --nologo
```

```powershell
dotnet test SmartParking.slnx --nologo
```

Frontend tests (from repo root):

```powershell
node --test tests/frontend/*.test.cjs
```

## Config wiring (OpenCode)

- This file lives at **`ai/AGENTS.md`** (inside the AI workspace), not the repo root.
- The OpenCode config is kept at **`ai/config/opencode.json`**, also not the repo root, because not every developer uses OpenCode.
- It sets `"instructions": ["ai/AGENTS.md"]`.
- To enable it: copy `ai/config/opencode.json` to the repo root as `opencode.json` (OpenCode auto-loads the root config), or point your tool at it directly.
- Because this file is not at the repo root, tools that only auto-discover a root `AGENTS.md` (Claude Code, Cursor, etc.) will not find it automatically — point them at `ai/AGENTS.md`, or add a root stub that references it.

## Productivity guidance

- Prefer incremental edits to focused files.
- For broad discovery, search before reading large files.
- For multi-step tasks, keep todos current and update the session log (`ai/rule.md`) after meaningful edits, commands, decisions, errors, or milestones.
- Do not add packages, abstractions, or "while I'm here" refactors — do only the requested task.
- Frontend work: read `FE/AGENTS.md` first.
