# SmartPark — AI Agent Instructions

> **Thin operational router.** Routes agents to the right context for the current task. Shared coding implementation conventions live in `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`; architecture facts live in `ai/architecture-reference.md`; operational workflow rules live in `ai/rule.md`. Read them when directed — do not duplicate them here.

## Core operating principles

1. **Inspect before acting.** Run `git status --short --branch` at session start. Never assume the branch, state, or working directory.
2. **Lazy-load context progressively (Tiered Loading).** Use the routing table below. Do not preload every file into context.
3. **Single source of truth.** Each fact has one canonical owner. If sources disagree, consult the Conflict Register in `ai/architecture-reference.md`.
4. **Verify before claiming.** Mark unverified work `[UNVERIFIED]`. Classify unknowns as `OPEN`, `DEFERRED`, or `PROPOSED`.
5. **Use the correct canonical skill.** Commit, PR, Session logging and `.env` workflows follow the applicable rules in `ai/rule.md`. **All coding/implementation/review tasks MUST load `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` as the primary implementation convention.** Rule 5 in `ai/rule.md` may supplement execution workflow, but must not override a newer approved coding convention in the shared skill.
6. **Minimal footprint.** Do only the requested task. No unrequested packages, abstractions, or "while I'm here" refactors.
7. **Protect user work & secrets.** Never revert uncommitted user changes or commit/print secrets (`.env`, connection strings).

## Project Topology (Quick Map)

- **Root Solution:** `SmartParking.slnx` (.NET 10 Web API microservices under `src/Services/`).
- **Backend Services:**
  - `src/Services/UserService`: 5-layer Clean Architecture (API, Application, Domain, Infrastructure, Persistence) + EF Core 10 + MediatR + PostgreSQL.
  - `src/Services/FacilityService`: 5 projects (API, Application, Domain, Infrastructure, Persistence) + raw Npgsql (Minimal APIs & endpoints).
  - `src/Services/OperationService` (domain authority aligned to **Parking Operations** in SRS v1.1): 5 projects (API, Application, Domain, Infrastructure, Persistence) + EF Core 10 + MediatR + PostgreSQL.
  - `src/Services/IoTService`: Python AI service (`python-ai/`) for license plate recognition & CCTV stream ingestion.
  *(Note: ASP.NET Core microservices feature service-specific application and persistence patterns; consult `ai/architecture-reference.md` before making service-specific assumptions).*
- **Frontend App:** `FE/` (React 19, Vite 8, TypeScript 5.7, Tailwind CSS v4, Three.js 0.186.1, MapTiler 4.1.0; see `FE/AGENTS.md`).
- **Database & Persistence:** PostgreSQL with service-owned schemas (`compose.schema-integration.yml`, `compose.schema-integration.yml`, `scripts/database/`).
- **Test Projects:** Backend tests under `tests/Services/` (xUnit), Frontend tests under `tests/frontend/` (node:test).
- **Instruction & Skills System:**
  - Operational Router: `ai/AGENTS.md`
  - Shared Coding Implementation Convention: `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`
  - Operational Skills & Execution Rules: `ai/rule.md`
  - Architecture Reference & Conflict Register: `ai/architecture-reference.md`
  - Detailed Skill Guides: `docs/07-skills/`
  - Task Sessions: `ai/tasks/active/` and `ai/tasks/completed/`

## Context routing table (Tiered Loading)

Loading context follows a 3-tier strategy:
- **Tier 1 (Quick routing):** Read `ai/AGENTS.md` to identify task category and minimal scope.
- **Tier 2 (Core rules):** For coding, load `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` first; for Commit, PR, Session and `.env`, load the applicable `ai/rule.md` rule. Coding tasks may also load Rule 5 for execution workflow, but the shared coding skill owns implementation conventions.
- **Tier 3 (Detailed references):** Load architecture/service/feature references, scoped `AGENTS.md`, detailed skills, or session history when the task requires them.

| When your task involves… | Tier 2: You MUST read… | Tier 3: Detailed reference (when needed) | Skip reading… (unless boundary crossed) |
|---|---|---|---|
| **Git Commit** | Rule 1 in `ai/rule.md` | `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` | Architecture docs, code files |
| **Pull Request (PR)** | Rule 2 in `ai/rule.md` | `docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md` | Unrelated backend/frontend code |
| **Session Tracking / Task Record** | Rule 3 in `ai/rule.md` | `docs/07-skills/SmartParking_Session_SKILL_v1.0.md`; active session in `ai/tasks/active/` | Deep implementation code |
| **Environment / .env / Compose / Ports** | Rule 4 in `ai/rule.md` | `docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md`; `.env.example` | Unrelated business logic docs |
| **Coding (Backend / Service / Database)** | **`docs/07-skills/SmartPark-Coding-Skill-v1.0.md`**; Rule 5 in `ai/rule.md` for execution workflow | `ai/architecture-reference.md`; `ai/context/architecture-guardrails.md`; applicable SRS/use-case/API/service-boundary docs; affected source & tests | Frontend-specific docs |
| **Frontend-only changes** | **`docs/07-skills/SmartPark-Coding-Skill-v1.0.md`** + `FE/AGENTS.md` | Relevant FE requirements/components/stores/styles/tests | Backend architecture/DB docs unless the frontend change crosses that boundary |
| **Architecture or API contract changes** | **`docs/07-skills/SmartPark-Coding-Skill-v1.0.md`** + `ai/architecture-reference.md` | `docs/02-architecture/`; `docs/03-design/api/` (template: `docs/03-design/api/api-design-template.md`); `ai/context/architecture-guardrails.md`; affected contracts | Unrelated service code |
| **Build / test / CI failures** | Verification commands below | Failing build/test output; affected project `.csproj` or config; coding skill when a code change is required | Architecture docs, unless diagnosing root cause |
| **Security / auth / secrets** | **`docs/07-skills/SmartPark-Coding-Skill-v1.0.md`** + Safety rules below | `ai/context/architecture-guardrails.md`; affected auth implementations & policies | Unrelated service docs |
| **Unknown or cross-cutting tasks** | Start with smallest context; load the shared coding skill before making code changes | Investigate repo; load `ai/context/project-context.md`, `ai/architecture-reference.md` and relevant contracts | Nothing is unconditionally skipped |

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
| Shared coding implementation convention | `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` |
| Verified architecture & Conflict Register | `ai/architecture-reference.md` |
| Operational workflow rules | `ai/rule.md` |

**Precedence principle:** The SRS is authoritative for product requirements. `ai/architecture-reference.md` and approved architecture/service-boundary documents govern architecture/ownership facts. `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` is the primary shared implementation convention for coding/review work. `ai/rule.md` governs operational workflow. Scoped `AGENTS.md` and approved feature/API contracts add task-specific constraints. If these sources materially conflict, do not silently choose a direction: use the Conflict Register and the coding skill's architecture/direction approval gate.

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

Shared coding implementation convention: → `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`
Operational rules and task logging protocol: → `ai/rule.md`
Active task sessions: → `ai/tasks/active/`
Completed task sessions: → `ai/tasks/completed/`
