# SmartPark — AI Agent Instructions

> **Thin operational router.** Routes agents to the right context for the current task. Architecture facts live in `ai/architecture-reference.md`; operational workflow & 5 core skills in `ai/rule.md`. Read them when directed — do not duplicate them here.

## Core operating principles

1. **Inspect before acting.** Run `git status --short --branch` at session start. Never assume the branch, state, or working directory.
2. **Lazy-load context progressively (Tiered Loading).** Use the routing table below. Do not preload every file into context.
3. **Single source of truth.** Each fact has one canonical owner. If sources disagree, consult the Conflict Register in `ai/architecture-reference.md`.
4. **Verify before claiming.** Mark unverified work `[UNVERIFIED]`. Classify unknowns as `OPEN`, `DEFERRED`, or `PROPOSED`.
5. **Follow 5 Core Skills.** Commit, PR, Session logging, .env configuration, and Coding practices must strictly follow `ai/rule.md`.
6. **Minimal footprint.** Do only the requested task. No unrequested packages, abstractions, or "while I'm here" refactors.
7. **Protect user work & secrets.** Never revert uncommitted user changes or commit/print secrets (`.env`, connection strings).

## Project Topology (Quick Map)

- **Root Solution:** `SmartParking.slnx` (.NET 10 Web API microservices under `src/Services/`).
- **Backend Services:**
  - `src/Services/UserService`: 5-layer Clean Architecture (API, Application, Domain, Infrastructure, Persistence) + EF Core 10 + MediatR + PostgreSQL.
  - `src/Services/ParkingService` (evolving to **FacilityService** in `Fix2FA`): 5 projects (API, Application, Domain, Infrastructure, Persistence) + raw Npgsql (Minimal APIs & endpoints).
  - `src/Services/ReservationService` (domain authority aligned to **Parking Operations** in SRS v1.1): 4 projects (API, Application, Domain, Persistence) + raw Npgsql.
  - `src/Services/IoTService`: Python AI service (`python-ai/`) for license plate recognition & CCTV stream ingestion.
  *(Note: ASP.NET Core microservices feature service-specific application and persistence patterns; consult `ai/architecture-reference.md` before making service-specific assumptions).*
- **Frontend App:** `FE/` (React 19, Vite 8, TypeScript 5.7, Tailwind CSS v4, Three.js 0.186.1, MapTiler 4.1.0; see `FE/AGENTS.md`).
- **Database & Persistence:** PostgreSQL with service-owned schemas (`docker-compose.yml`, `compose.schema-integration.yml`, `scripts/database/`).
- **Test Projects:** Backend tests under `tests/Services/` (xUnit), Frontend tests under `tests/frontend/` (node:test).
- **Instruction & Skills System:**
  - Operational Router: `ai/AGENTS.md`
  - 5 Core Skills & Execution Rules: `ai/rule.md`
  - Architecture Reference & Conflict Register: `ai/architecture-reference.md`
  - Detailed Skill Guides: `docs/07-skills/`
  - Task Sessions: `ai/tasks/active/` and `ai/tasks/completed/`

## Context routing table (Tiered Loading)

Loading context follows a 3-tier strategy:
- **Tier 1 (Quick routing):** Read `ai/AGENTS.md` to identify task category and minimal scope.
- **Tier 2 (Core rules):** Read the specific rule in `ai/rule.md` for standard tasks (Commit, PR, Session, .env, Coding).
- **Tier 3 (Detailed references):** Load detailed skills in `docs/07-skills/`, architecture reference, or session history only when the task is complex, ambiguous, or high-risk.

| When your task involves… | Tier 2: You MUST read… | Tier 3: Detailed reference (when needed) | Skip reading… (unless boundary crossed) |
|---|---|---|---|
| **Git Commit** | Rule 1 in `ai/rule.md` | `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` | Architecture docs, code files |
| **Pull Request (PR)** | Rule 2 in `ai/rule.md` | `docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md` | Unrelated backend/frontend code |
| **Session Tracking / Task Record** | Rule 3 in `ai/rule.md` | `docs/07-skills/SmartParking_Session_SKILL_v1.0.md`; active session in `ai/tasks/active/` | Deep implementation code |
| **Environment / .env / Compose / Ports** | Rule 4 in `ai/rule.md` | `docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md`; `.env.example` | Unrelated business logic docs |
| **Coding (Backend / Service / Database)** | Rule 5 in `ai/rule.md` | `ai/architecture-reference.md`; `ai/context/architecture-guardrails.md`; affected source & tests | Frontend-specific docs |
| **Frontend-only changes** | `FE/AGENTS.md` | Relevant FE components, stores, styles & tests | Backend architecture docs, DB scripts |
| **Architecture or API contract changes** | `ai/architecture-reference.md` | `docs/02-architecture/`; `docs/03-design/api/` (template: `docs/03-design/api/api-design-template.md`); `ai/context/architecture-guardrails.md`; affected contracts | Unrelated service code |
| **Build / test / CI failures** | Verification commands below | Failing build/test output; affected project `.csproj` or config | Architecture docs, unless diagnosing root cause |
| **Security / auth / secrets** | Safety rules below; `ai/context/architecture-guardrails.md` | Affected auth implementations & policies | Unrelated service docs |
| **Unknown or cross-cutting tasks** | Start with smallest context | Investigate repo; load `ai/context/project-context.md` & `ai/architecture-reference.md` | Nothing is unconditionally skipped |

> **Conditional skips are not prohibitions.** Always load additional context when evidence shows the task crosses a previously assumed boundary.

## Source of truth

| Topic | Canonical source |
|---|---|
| Requirements (SRS) | `docs/01-requirements/srs/smartpark-srs-v1.1.md` (Module breakdown under `docs/01-requirements/srs/modules/`) |
| Use cases | `docs/01-requirements/use-cases/` |
| Business rules | `docs/01-requirements/business-rules/` (and `policy-catalogue.md`) |
| Architecture & Solution Design | `docs/02-architecture/solution-architecture/smartpark-solution-architecture-v1.1.md` |
| Service boundaries | `docs/02-architecture/service-boundaries/` |
| Database ownership & schemas | `docs/02-architecture/database/` |
| API design specifications & template | `docs/03-design/api/` (template: `docs/03-design/api/api-design-template.md`) |
| Git & Jira convention | `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` |
| Pull Request guide | `docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md` |
| Session tracking | `docs/07-skills/SmartParking_Session_SKILL_v1.0.md` |
| Environment config | `docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md` |
| Verified architecture & Conflict Register | `ai/architecture-reference.md` |
| Operational rules & 5 Skills | `ai/rule.md` |

**Precedence principle:** The SRS is authoritative for requirements. Context files, handoffs, and decision records interpret it; they do not replace it. If code and documentation disagree, check `ai/architecture-reference.md` (§8 Conflict Register) to determine verified repo reality.

## Safety rules

- **Secrets:** NEVER print, commit, or copy secrets (`.env`, `*.user`, user-secrets, connection strings).
- **User work:** Do NOT revert user/uncommitted changes unless explicitly requested.
- **Risky operations:** Before any destructive action (delete, drop DB, force push, destructive migration) → record a pre-flight checkpoint and ask the user.
- **Git:** Only commit, amend, push, or create PRs when explicitly requested.
- **Docker/runtime:** Do NOT start/stop Docker containers or the app unless asked.
- **Dependencies:** Do NOT add or change packages without explicit authorization.
- **Business logic:** Do NOT make unrequested changes to business logic, database schemas, or runtime configuration.

## AI authority boundary

**AI assists → backend decides → user confirms writes → backend executes → AI reports actual result.**

AI MUST NOT: access authoritative databases directly, invent business state, authorize payment, or execute arbitrary SQL. Full rules: `ai/context/architecture-guardrails.md`.

## Verification commands

Run from repo root:

```powershell
git status --short --branch --untracked-files=all
dotnet build SmartParking.slnx --nologo
dotnet test SmartParking.slnx --nologo
node --test tests/frontend/*.test.cjs          # frontend tests
```

## Tool configuration (OpenCode & cross-tool compatibility)

- This file lives at **`ai/AGENTS.md`** (inside the AI workspace), not the repo root.
- **OpenCode:** Config at `ai/config/opencode.json` sets `"instructions": ["ai/AGENTS.md"]`. Copy it to the repo root as `opencode.json` to enable auto-loading, or point OpenCode at it directly.
- **Claude Code / Cursor / other tools:** These tools auto-discover a root `AGENTS.md`. A minimal root stub redirects to this file. If a tool does not support file includes, configure its native instruction mechanism to load `ai/AGENTS.md`.
- **Directory-scoped instructions:** `FE/AGENTS.md` provides frontend-specific rules and applies only to `FE/` work.

## Session state & Task records

Operational rules, 5 Core Skills, and task logging protocol: → `ai/rule.md`
Active task sessions: → `ai/tasks/active/`
Completed task sessions: → `ai/tasks/completed/`
