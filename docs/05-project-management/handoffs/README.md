# Handoffs

Handoffs identify baseline, current evidence, known gaps and owning next actions; they do not replace canonical requirements or imply implementation acceptance.

- [Partner onboarding FE/BE handoff — v1.1](OWNER_ONBOARDING_BE_HANDOFF.md): initial combined application, scoped credentials, simulation, agreement upload/versioning, provisioning and additional-lot scope. A v1.1 documentation review candidate based on approved decisions.
- [FE v2.1 recovery, 16 October 2026](2026-10-16-fe-v2.1-recovery.md): historical recovery handoff; retain its dated evidence.
- [Approved v1.1 decisions](../decision-log/2026-10-20-approved-v1.1-changes.md) and [pending technical actions](../decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions).

- [Reservation frontend handoff and readiness — 9 November 2026](2026-11-09-reservation-frontend-handoff.md): current admission-only boundary, frontend backlog and backend prerequisites; no frontend modifications.
- [Payment producer handoff — refreshed 10 November 2026](2026-11-09-reservation-producer-handoff.md): Payment source findings, required commercial contracts and owner delivery requirements. Facility producer implementation is now part of this task.
- [Operations → Facility durable dependency](2026-11-09-reservation-facility-durable-dependency.md): source-verified Facility pattern and proposed reverse token/hold/commit/release protocol; full resource projection remains unapproved.

- [Reservation completion checklist — 10 November](2026-11-10-reservation-completion-checklist.md): current confirmation blockers, owning teams and full lifecycle follow-up.
- [Availability, Facility integration and event/SignalR handoff](2026-11-10-reservation-events-and-facility-integration.md): first-write rules, dedicated service JWT, durable events, client behavior and post-confirmation backlog.

- [**11 November SPARK-273 cross-team implementation alignment**](2026-11-11-SPARK-273-implementation-alignment.md): **current** workflow boundary, Payment quote/order integration, independent payment-event confirmation, conditional Facility fast path and targeted recovery. Supersedes conflicting earlier handoff flow guidance; does not claim code/test readiness.
- [**11 November SPARK-273 approved architectural decision**](../decision-log/2026-11-11-reservation-event-driven-confirmation.md): normative implementation direction and explicit owner-contract/verification gates; the 10 November evaluation-time cutoff remains authoritative.
