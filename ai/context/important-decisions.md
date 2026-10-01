# SmartPark AI Development Important Decisions

This file is a compact working snapshot. Detailed decisions should be stored as individual records under `ai/decisions/` and linked back to their canonical project sources.

| Topic | Status | Current direction | Source / authority |
|---|---|---|---|
| AI authority | TEAM-CONFIRMED / WORKING | AI is assistive; backend/domain remains authoritative | BA context + architecture context |
| AI write operations | TEAM-CONFIRMED / WORKING | AI prepares/assists; UI shows actual action; user confirms; backend executes | BA context |
| AI payment | TEAM-CONFIRMED / WORKING | AI must not autonomously perform payment | Architecture context |
| AI read tools | WORKING | Read-oriented API/tool calls are allowed for authoritative information | BA context |
| AI form filling | WORKING | AI may help fill an existing form; correct UI form must be shown for manual confirmation | BA context |
| Product AI scope | MVP / WORKING | Chatbot, recommendation, and LPR/CV are in current MVP scope; model/provider details remain deferred | Review + Reservation specification |
| AI service boundary | OPEN / DEFERRED | Requires architecture review | BA architecture backlog |
| Legacy AI SRS cleanup | DEFERRED | Reconcile older AI requirements with Chatbox architecture | BA context |
| Business-rule provenance | CONFIRMED | Do not silently promote proposals/open items to requirements | BA context |
| Status vocabulary | CONFIRMED | Use project status labels consistently | BA context |

## Non-negotiable interpretation rule

When sources do not answer a question, classify it as `OPEN`, `DEFERRED`, or `PROPOSED` rather than inventing a solution.
