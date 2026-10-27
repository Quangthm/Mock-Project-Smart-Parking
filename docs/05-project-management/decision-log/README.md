# Decision Log

Record significant project decisions, their status, source, and affected documents.
Approved decisions must be integrated into canonical requirements, rather than left only in this log.

- [Approved v1.1 change register, revision 0.2](2026-10-20-approved-v1.1-changes.md): project-lead decisions dated 20 October 2026 (Asia/Saigon), wording preserved from the handoff, with Markdown trailing whitespace normalized for diff checks. Its completion statements describe preparation of that register, not SPARK-296 execution.
- [21 October VNeID GUID clarification](2026-10-21-vneid-simulation-guid.md): the fake API returns a GUID substitute persisted against the user; implements the lead clarification under V11-06.
- [25 October reservation/allocation state clarification](2026-10-25-reservation-allocation-state-clarification.md): 1-hour Allocation Lead Time is the default effective policy value; `OCCUPIED` does not identify the reservation holder; same/different occupant-claimant relationships require Operations linkage; RA-06 makes the listed reservation/monthly numeric values configurable defaults under Admin default/bounds → authorized Owner scope; 2D/3D keeps mutually exclusive `RESERVED`/`PROTECTED` projection values without schema redesign; no missing numeric bounds are invented.
- [v1.1 decision coverage](../../01-requirements/requirements-traceability/v1.1-decision-coverage.md): canonical clauses, API/architecture contracts, NOT RUN acceptance and remaining work for each V11/D decision.
- [Design and implementation backlog](deferred-and-implementation-register.md).
- [Release review and ownership actions](pm-release-actions.md).

Earlier dated decision/review records remain historical sources. v1.0 was an unsigned review candidate. The current package is a **v1.1 documentation review candidate based on approved decisions**; requirements approval does not prove API approval, implementation or product acceptance.
