# Handoffs

Handoffs identify baseline, current evidence, known gaps and owning next actions; they do not replace canonical requirements or imply implementation acceptance.

- [Partner onboarding FE/BE handoff — v1.1](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/7b5475d29344a992540ba6710f3aa753eff2edbe/docs/05-project-management/handoffs/OWNER_ONBOARDING_BE_HANDOFF.md): initial combined application, scoped credentials, simulation, agreement upload/versioning, provisioning and additional-lot scope. A v1.1 documentation review candidate based on approved decisions.
- [FE v2.1 recovery, 16 October 2026](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/d63d5d40824369ba8636fe084a316c70c813d683/docs/05-project-management/handoffs/2026-10-16-fe-v2.1-recovery.md): historical recovery handoff; retain its dated evidence.
- [Approved v1.1 decisions](../decision-log/2026-10-20-approved-v1.1-changes.md) and [pending technical actions](../decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions).

## SPARK-273 reservation

- [Driver frontend](./2026-11-09-reservation-frontend-handoff.md) — reviewed client cutover, realtime progress, checkout handoff and browser acceptance.
- [Historical payment-return defect (E2-01)](<./SPARK-273 — Reservation/SPARK-273-E2-payment-return-frontend-handoff.md>) — frontend-owned distinct request/reservation ID handoff; no production code changed in Checkpoint E.
- [Reservation owners](./2026-11-18-SPARK-273-reservation-owner-handoffs.md) — quote and Saga contracts, guarded capacity, recovery and rollout.
- [Payment authority](./2026-11-19-SPARK-273-payment-authority.md) — quote/order ownership, payOS settlement, reconciliation and verification.

The [decision log](../decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) governs the contract. The [Postman manual guide](../../04-testing/postman/SPARK-273-manual-test-guide.md) records the local manual run.

## Discovery

- [Discovery implementation catalogue](./2026-11-19-discovery-refactor-catalogue.md) — publication, public availability and migration follow-ups.
