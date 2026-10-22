# SPARK-192 — Create Operator

> Historical implementation evidence, updated 15 October 2026 for the R06 CQRS refactor; non-authoritative for v1.1. Current target: [Operator invitation and activation](../../03-design/api/owner-create-operator-api-design.md) and [shared contracts](v1.1-contracts.md). The body records Fix2FA behavior, not completed v1.1 acceptance.

Implements the Operator creation endpoint in UserService: `POST /api/users`. The 15/10 leader handoff confirms pending onboarding, Operator-selected password at activation, and handler-owned CQRS orchestration.

## Request

Bearer authentication with current role `owner` is required. No corporate email domain restriction.

```json
{
  "fullName": "Parking Attendant",
  "email": "attendant@example.com",
  "siteIds": ["22222222-2222-2222-2222-222222222222"],
  "permissions": ["DEVICE_MANAGE", "DEVICE_STATUS_VIEW"]
}
```

- `fullName`: required, trimmed, maximum 255 characters.
- `email`: required valid email, maximum 255 characters, trimmed/lowercased; unique among non-deleted users. Existing identities are rejected; this is account creation, not account linking or password replacement.
- `password` is not accepted. Supplying it is an unknown-field validation error (HTTP 400). A pending Operator has no usable password credential; the Operator chooses a password during email activation.
- `siteIds`: 1–100 distinct non-empty UUIDs. Every site must exist, be active/non-deleted and belong to an active tenant in which the caller has a current active `BUSINESS_OWNER` tenant account (`site_id IS NULL`). A pending/locked/deleted Owner, suspended membership or suspended/deleted tenant cannot provision staff.
- `permissions`: 1–5 distinct, case-sensitive entries from `DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`, `SLOT_OVERRIDE`. The same explicit permission set is stored for each selected site. Arbitrary role/admin/owner/financial powers cannot be delegated.

`ownerId`, `tenantId`, `role`, user status and account IDs are not request fields; authoritative scope comes from the authenticated identity and database. There is no `all` wildcard. An explicit list captures the selected sites at creation time; new sites are not automatically granted. Per-site differences can be implemented later through a separately agreed grant-management contract.

## Response

HTTP **201**, `Cache-Control: no-store`:

```json
{
  "success": true,
  "data": {
    "id": "33333333-3333-3333-3333-333333333333",
    "fullName": "Parking Attendant",
    "email": "attendant@example.com",
    "role": "operator",
    "status": "pending_verification",
    "createdBy": "11111111-1111-1111-1111-111111111111",
    "siteIds": ["22222222-2222-2222-2222-222222222222"],
    "permissions": ["DEVICE_MANAGE", "DEVICE_STATUS_VIEW"],
    "deliveryId": "44444444-4444-4444-4444-444444444444",
    "deliveryStatus": "pending"
  }
}
```

Passwords, hashes and activation tokens are never returned. Creation grants no session and preserves the Owner's session. A one-time email invitation expires after 24 hours. The Operator submits challenge ID, token and their own password to `POST /api/auth/bootstrap-password`; successful activation verifies email and changes pending accounts to active. Only then can the identity sign in. Before activation no password credential exists, so password login fails with invalid credentials.

An authorized Owner can call `POST /api/operators/{id}/invitation` to replace an unused/expired invitation. The handler checks current tenant scope and a one-minute cooldown; it invalidates prior proofs and queues a new encrypted delivery. No password field is added to this request. Activation still requires its own password field.

## Errors

| HTTP | Code/behavior | Condition |
| --- | --- | --- |
| 400 | MVC validation problem / validation pipeline response | Missing/invalid fields, obsolete password field, duplicate sites/permissions, unsupported permissions |
| 401 | Authentication rejection | Missing, invalid, expired or revoked session; current user role/access no longer matches |
| 403 | Authorization rejection / `FORBIDDEN` | Wrong role or no current eligible tenant Owner membership |
| 403 | `SITE_ACCESS_DENIED` | At least one site is missing, inactive, deleted or outside owned tenants |
| 409 | `EMAIL_EXISTS` | Active identity already has that email, including a concurrent create |

Business exceptions use the existing `{ success: false, code, message }` middleware envelope. ASP.NET authorization responses may have no body; validation uses the existing MVC problem response. No error discloses a foreign site's details.

## Persistence and permission checks

For the service schema, use the canonical baseline and alignment, then workflow 05.8, MFA 05.13 and identity 05.14 through `scripts/apply-workflow-migrations.ps1 -Service User -SchemaRoot .`. Do not apply old monolith 05.7 over the service schema. This CQRS/no-password change adds no migration beyond the existing 05.14; pending credentials use the existing empty-password representation.

The command handler owns creation: current Owner authorization, external Facility site validation, then a local Unit of Work transaction with identity authorization rechecked under shared row locks. It persists identity + pending site accounts/roles/grants + challenge + encrypted delivery + access outbox atomically. Facility resources are validated through an external port; no cross-service database locks or distributed transaction are claimed. Duplicate email races are enforced by the database unique index.

The broad `IOperatorProvisioningService` is removed. Dedicated query handlers coordinate current grants and external active sites; `OperatorSlotScopeQuery` checks the matching site's `SLOT_OVERRIDE` permission. JWT `role=operator` alone grants no site access. Operational mutations must additionally follow their own transaction/revocation contract. Standalone assign/revoke mutation APIs are not added in phase 1.

## Integration boundaries and acceptance

A real active `BUSINESS_OWNER` membership in the site's tenant must exist before provisioning. Owner approval is a separate workflow. Operator creation must not infer ownership from frontend fixtures or create a tenant implicitly.

The frontend `CreateOperatorForm` sends `fullName`, `email`, one selected backend `siteId` and its existing default five delegable permissions through `operatorsApi.create`. There are no password/confirmation inputs. `OperatorManagement` reads the backend roster and supports invitation reissue. Assignment edits/revocation remain unsupported in phase 1. Site creation elsewhere still has known local-fallback limitations; verify that the chosen site exists in the backend.

Manual/API acceptance:

1. Sign in as an approved Owner with active tenant membership and existing active sites.
2. Send the example with real owned site IDs; expect 201 and inspect persisted user/accounts/grants.
3. Confirm password login fails before activation. Open the delivered invitation, set the Operator's own password and verify email; then sign in on another client and expect role `operator`.
4. Retry the email, including different capitalization; expect 409 and no additional rows.
5. Include an inactive/foreign/nonexistent site; expect 403 and no partial creation.
6. Try anonymous, Driver, Operator and Admin credentials; expect 401/403.
7. Check granted permission at an assigned site; check it at a foreign site and check ungranted/Owner-only permissions; only the first succeeds.
8. Suspend the Operator account, remove the permission, disable the site or suspend the tenant; a fresh permission check must deny access.

## v1.1 implementation gaps

The implementation below the historical banner requires an Operator-selected password at bootstrap activation and activates the pending identity after invitation proof. The v1.1 target instead requires consented simulated VNeID during activation and optional password setup, preserving OTP login eligibility. These are pending implementation changes, not acceptance evidence. The existing 24-hour invitation lifetime is a runtime fact; final activation credential lifetime/composition remains V11-DES-02/03 design work. The FacilityService rename does not establish completion of the Operations ownership split.
