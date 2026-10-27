> **Main entry point:** [Consolidated SRS v1.1](smartpark-srs-v1.1.md) is the current overview. The content is the canonical v1.1 documentation review candidate based on approved decisions. Modules retain detailed requirement IDs.

# SmartPark SRS — v1.1 documentation review candidate

**Based on approved decisions, 20 October 2026; no product acceptance claimed.** Start with overview; read only the relevant domain module, then canonical rules/use cases. Implementation/evidence is not assessed.

- [00-overview](modules/00-overview.md)
- [01-identity-access](modules/01-identity-access.md)
- [02-facility-capacity](modules/02-facility-capacity.md)
- [03-discovery-visualization](modules/03-discovery-visualization.md)
- [04-reservations-allocation](modules/04-reservations-allocation.md)
- [05-sessions-operations](modules/05-sessions-operations.md)
- [06-pricing-payments](modules/06-pricing-payments.md)
- [07-monthly-passes](modules/07-monthly-passes.md)
- [08-notifications-reporting](modules/08-notifications-reporting.md)
- [09-file-media](modules/09-file-media.md)
- [10-ai-assistance](modules/10-ai-assistance.md)
- [11-quality-deployment](modules/11-quality-deployment.md)

- [Policy catalogue](../business-rules/policy-catalogue.md)
- [Action-by-role permissions](permissions.md)
- [State transitions and races](state-transitions.md)
- [Business rules](../business-rules/README.md)
- [Use cases and stories](../use-cases/README.md)
- [Traceability](../requirements-traceability/README.md)
- [Release manifest](baselines/v1.1/manifest.md)
- [Deferred register](../../05-project-management/decision-log/deferred-and-implementation-register.md)

Scope labels: CONFIRMED/implementation pending; confirmed direction with POST-v1.0 detail; FUTURE outside MVP; historical reference inactive. Approval of a requirement does not mean delivered implementation.


## RC2 security amendment

[Authentication/recovery contract](authentication-recovery.md) and [SEC-01/SEC-02 decisions](../../05-project-management/decision-log/2026-10-13-totp-account-recovery-sec-01-02.md) supersede the earlier TOTP deferral. TOTP/backup-code/Admin-recovery scope is now MVP; technical configuration and implementation evidence remain pending. Unchanged domain files retain RC1 revision labels intentionally.

## RC3 payment amendment

[PAY-01 decision](../../05-project-management/decision-log/2026-10-13-payos-priority-pay-01.md) approves payOS implementation priority and controlled low-value production testing plus deterministic failure/race fixtures in place of the sandbox requirement. See the [payment module](modules/06-pricing-payments.md), [adapter design](../../02-architecture/payment-integration/payos.md) and [test plan](../../04-testing/payment-integration/payos-test-plan.md). Unchanged files retain their prior revision labels.
