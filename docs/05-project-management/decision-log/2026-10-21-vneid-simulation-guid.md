# V11-06 clarification — persisted simulated VNeID GUID

**Approved source:** project lead's explicit clarification in the SPARK-296 conversation, 21 October 2026 (Asia/Saigon). This clarifies [V11-06 in revision 0.2](2026-10-20-approved-v1.1-changes.md#v11-06--mandatory-simulated-vneid-sole-seeded-admin-exemption); it does not replace the earlier register's historical wording or introduce a new role.

The fake VNeID API returns a **GUID substitute**. SmartPark ties that returned GUID to the user and persists it in the database. User & Access owns this user-bound simulated identity data. A transient browser-only value does not satisfy the requirement.

The GUID is a simulated identifier, not a real government identity number or a login/access credential. Preserve explicit sharing consent, successful simulation evidence, the sole seeded Admin exemption, Operator activation timing and all contact/review/provisioning/MFA gates. Failed or declined simulation cannot be represented as completed solely because a value was received or generated.

The exact endpoint/property names, schema representation, retry/replay/conflict handling, other mock fields and retention controls remain design work under [V11-DES-02](deferred-and-implementation-register.md#v11-design-and-implementation-actions). No new deployed column or completed fake API is asserted by this documentation change.

Canonical integration: [identity requirements](../../01-requirements/srs/modules/01-identity-access.md), [shared API contracts](../../02-architecture/api/v1.1-contracts.md), [database alignment](../../02-architecture/database/alignment-backlog.md#v11-schema-and-seed-alignment-pending), and [acceptance AC-113](../../04-testing/requirements-traceability/acceptance-scenarios.md). Implementation and verification remain pending; AC-113 is NOT RUN.
