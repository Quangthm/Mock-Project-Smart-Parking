# SmartPark AI Development Important Decisions

This file is a compact working snapshot. Detailed decisions should be stored as individual records under `ai/decisions/` and linked back to their canonical project sources.

| Topic | Status | Current direction | Source / authority |
|---|---|---|---|
| AI authority | TEAM-CONFIRMED / WORKING | AI is assistive; backend/domain remains authoritative | BA context + architecture context |
| AI write operations | TEAM-CONFIRMED / WORKING | AI prepares/assists; UI shows actual action; user confirms; backend executes | BA context |
| AI payment | TEAM-CONFIRMED / WORKING | AI must not autonomously perform payment | Architecture context |
| AI read tools | WORKING | Read-oriented API/tool calls are allowed for authoritative information | BA context |
| AI form filling | WORKING | AI may help fill an existing form; correct UI form must be shown for manual confirmation | BA context |
| Product AI scope | MVP / WORKING | Chatbot, recommendation, and LPR/CV are in current MVP scope; model/provider details remain deferred. Repo reality: chatbot is a disabled scaffold; LPR has initial Python service in `src/Services/IoTService/python-ai/` | Review + Reservation specification |
| AI service boundary | OPEN / DEFERRED | Requires architecture review | BA architecture backlog |
| Normative baseline | CONFIRMED | Baseline upgraded to SRS v1.1 and Solution Architecture v1.1 | Team decision review |
| Business-rule provenance | CONFIRMED | Do not silently promote proposals/open items to requirements | BA context |
| Status vocabulary | CONFIRMED | Use project status labels consistently | BA context |
| Messaging broker | CONFIRMED | RabbitMQ is the current direction; Kafka is not used | Conflict Register (`ai/architecture-reference.md`) |
| UI library | CONFIRMED | TailwindCSS v4 only; Material UI is not used | Repo reality |
| Backend stack | CONFIRMED | .NET 10; CQRS/MediatR; EF Core (UserService) / raw Npgsql (FacilityService, OperationService); PostgreSQL | Repo reality |
| 5 Core Skills | CONFIRMED | Commit, PR, Session, .env, Coding skills mandated in `ai/rule.md` | Agent Architecture Refactor |

## Non-negotiable interpretation rule

When sources do not answer a question, classify it as `OPEN`, `DEFERRED`, or `PROPOSED` rather than inventing a solution.
