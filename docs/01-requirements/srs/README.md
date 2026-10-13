# SmartPark SRS — v1.0 RC1

**Review candidate, 13 October 2026.** Start with overview; read only the relevant domain module, then canonical rules/use cases. Implementation/evidence is not assessed.

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
- [Release manifest](baselines/v1.0/manifest.md)
- [Deferred register](../../05-project-management/decision-log/deferred-and-implementation-register.md)

Scope labels: CONFIRMED/implementation pending; confirmed direction with POST-v1.0 detail; FUTURE outside MVP; historical reference inactive. Approval of a requirement does not mean delivered implementation.


## RC2 security amendment

[Authentication/recovery contract](authentication-recovery.md) and [SEC-01/SEC-02 decisions](../../05-project-management/decision-log/2026-10-13-totp-account-recovery-sec-01-02.md) supersede the earlier TOTP deferral. TOTP/backup-code/Admin-recovery scope is now MVP; technical configuration and implementation evidence remain pending. Unchanged domain files retain RC1 revision labels intentionally.
