# SmartPark — AI Agent Instructions

> **Thin operational router.** Routes agents to the right context for the current task. Architecture facts live in `ai/architecture-reference.md`; session protocol in `ai/rule.md`. Read them when directed — do not duplicate them here.

## Core operating principles

1. **Inspect before acting.** Run `git status --short --branch` at session start. Never assume the branch, state, or working directory.
2. **Lazy-load context.** Use the routing table below. Do not preload every file.
3. **Single source of truth.** Each fact has one canonical owner. If sources disagree, consult the Conflict Register in `ai/architecture-reference.md`.
4. **Verify before claiming.** Mark unverified work `[UNVERIFIED]`. Classify unknowns as `OPEN`, `DEFERRED`, or `PROPOSED`.
5. **Minimal footprint.** Do only the requested task. No unrequested packages, abstractions, or "while I'm here" refactors.

## Context routing table (lazy loading)

| When your task involves… | You MUST read… | Skip reading… (unless the task crosses a boundary) |
|---|---|---|
| **Frontend-only changes** | `FE/AGENTS.md`; relevant source files, components, tests | Backend architecture docs, SRS, database docs |
| **Backend / cross-service / database changes** | `ai/architecture-reference.md`; `ai/context/architecture-guardrails.md`; affected source, contracts, migrations, tests | Frontend-specific docs |
| **Session init, continuation, or closure** | `ai/rule.md`; `ai/activity-log/_CURRENT_SESSION.md` | Architecture and SRS docs unless the task requires them |
| **Architecture or API contract changes** | `ai/architecture-reference.md`; relevant canonical source (§ Source of truth below); affected tests and decision records | Unrelated service code |
| **Documentation-only changes** | Target document and its canonical source | Application code, unless verifying described implementation |
| **Build / test / CI failures** | Verification commands below; failing output; affected project config | Architecture docs, unless needed to diagnose the failure |
| **Security / auth / secrets** | `ai/context/architecture-guardrails.md` (AI authority boundary); affected implementation | Unrelated service docs |
| **Unknown or cross-cutting tasks** | Start with smallest useful context; expand when dependencies or risks require it | Nothing is unconditionally skipped |

> **Conditional skips are not prohibitions.** Always load additional context when evidence shows the task crosses a previously assumed boundary.

## Source of truth

| Topic | Canonical source |
|---|---|
| Requirements | `docs/01-requirements/srs/smartpark-srs-v0.9.md` |
| Use cases | `docs/01-requirements/use-cases/smartpark-use-cases-v0.9.md` |
| Business rules | `docs/01-requirements/business-rules/smartpark-business-rules-v0.8.5.md` |
| Architecture | `docs/02-architecture/solution-architecture/SmartPark_Solution_Architecture_v0.9.md` |
| Service boundaries | `docs/02-architecture/service-boundaries/` |
| Database | `docs/02-architecture/database/` |
| Git / commit convention | `docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md` |
| Verified architecture & Conflict Register | `ai/architecture-reference.md` |
| Session lifecycle & checkpointing | `ai/rule.md` |

The SRS is authoritative. Context and decision records interpret it; they do not replace it.

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

## Session state

Session lifecycle, checkpointing, crash recovery, and handoff rules: → `ai/rule.md`
Current live session snapshot: → `ai/activity-log/_CURRENT_SESSION.md`
