# SmartPark AI Development Workspace

This folder contains the shared context, task records, handoffs, decision records, prompts, and activity history used by AI agents during SmartPark development.

## Scope

This folder documents **AI-assisted development of SmartPark**.

It is intentionally separate from the SmartPark product's own AI feature documentation:

- `docs/03-design/AI/` = what AI does **inside the SmartPark product**.
- `ai/` = how AI agents **help the team develop SmartPark**.

## Current AI boundary

```text
AI assists
Backend decides
User confirms writes
Backend executes
AI reports actual result
```

AI agents must not silently become the source of truth for business requirements, authorization, payment, or authoritative application state.

## Folder structure

| Folder | Purpose |
|---|---|
| `agents/` | Agent registry and agent-specific working instructions |
| `context/` | Current project context and development guardrails |
| `decisions/` | Individual AI-related decisions / ADR-style records |
| `tasks/` | AI-assisted task records, separated into active/completed |
| `handoffs/` | Session-to-session or agent-to-agent handoffs |
| `activity-log/` | Concise record of significant AI-assisted work |
| `prompts/` | Reusable project-specific prompts |
| `templates/` | Templates for the records above |

## Rules

1. Do not store API keys, access tokens, passwords, or other secrets.
2. Do not store entire chat transcripts by default. Record durable context, decisions, changes, and handoffs instead.
3. Do not duplicate the SRS, architecture documents, or source code here. Reference the canonical project documents instead.
4. Never convert `OPEN`, `DEFERRED`, `PROPOSED`, or `BA-PUSHED` items into confirmed requirements without explicit confirmation.
5. Record provenance for AI-created recommendations or changes.
6. AI-generated code and documentation remain subject to human review.
7. Product business rules remain authoritative in the backend/domain model and canonical requirements documents.
8. When an AI session changes an important decision or architecture constraint, update the relevant context/decision record.

## Status vocabulary

Use the project status vocabulary where applicable:

- `CONFIRMED`
- `TEAM-CONFIRMED`
- `WORKING`
- `PROPOSED`
- `BA-PUSHED`
- `OPEN`
- `DEFERRED`
- `FUTURE`
- `OUT OF SCOPE`
- `CONFLICT`

## Recommended workflow

```text
Read context
   ↓
Identify task + canonical sources
   ↓
Perform AI-assisted work
   ↓
Record important changes / findings
   ↓
Human review
   ↓
Update decisions or handoff
   ↓
Commit
```

## Naming

Follow the repository convention for project Markdown records:

```text
smartpark-<topic>.md
```

Use a version suffix only when versioning is meaningful:

```text
smartpark-ai-decision-<topic>-v1.0.md
```
