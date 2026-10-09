# SPARK-192 — Create Operator

> Historical contract/review (06/10/2026). See [current workflow contract and mapping, 08/10/2026](workflows-2026-10-08.md) for the implemented changes.

Implements FR-AUTH-04 / UC-AUTH-03 / SRS §3.7.5 in UserService. The endpoint outline from API Design is retained: `POST /api/users`. This document completes the missing body, response and errors. Field/credential policy is an implementation choice for team review, not a new confirmed SRS rule.

## Request

Bearer authentication with current role `owner` is required. No corporate email domain restriction.

```json
{
  "fullName": "Parking Attendant",
  "email": "attendant@example.com",
  "password": "Password@123",
  "siteIds": ["22222222-2222-2222-2222-222222222222"],
  "permissions": ["DEVICE_MANAGE", "DEVICE_STATUS_VIEW"]
}
```

- `fullName`: required, trimmed, maximum 255 characters.
- `email`: required valid email, maximum 255 characters, trimmed/lowercased; unique among non-deleted users. Existing identities are rejected; this is account creation, not account linking or password replacement.
- `password`: 8–15 characters, uppercase, lowercase, number and special character; uses the existing registration/login policy and BCrypt implementation. Owner supplies an initial password and communicates it privately. Invitation, delivery, forced first-login password change and reset are separate work.
- `siteIds`: 1–100 distinct non-empty UUIDs. Every site must exist, be active/non-deleted and belong to an active tenant in which the caller has a current active `OWNER` tenant account (`site_id IS NULL`). A pending/locked/deleted Owner, suspended membership or suspended/deleted tenant cannot provision staff.
- `permissions`: 1–4 distinct, case-sensitive entries from `DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`. The same explicit permission set is stored for each selected site. Arbitrary role/admin/owner/financial powers cannot be delegated.

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
    "status": "active",
    "createdBy": "11111111-1111-1111-1111-111111111111",
    "siteIds": ["22222222-2222-2222-2222-222222222222"],
    "permissions": ["DEVICE_MANAGE", "DEVICE_STATUS_VIEW"]
  }
}
```

Passwords, hashes and authentication tokens are never returned. The new identity can sign in using the existing Login endpoint. Creation grants no session for the new Operator and preserves the Owner's session.

## Errors

| HTTP | Code/behavior | Condition |
| --- | --- | --- |
| 400 | MVC validation problem / `VALIDATION_FAILED` for direct service calls | Missing/invalid fields, duplicate sites/permissions, unsupported permissions |
| 401 | Authentication rejection | Missing, invalid, expired or revoked session; current user role/access no longer matches |
| 403 | Authorization rejection / `FORBIDDEN` | Wrong role or no current eligible tenant Owner membership |
| 403 | `SITE_ACCESS_DENIED` | At least one site is missing, inactive, deleted or outside owned tenants |
| 409 | `EMAIL_EXISTS` | Active identity already has that email, including a concurrent create |

Business exceptions use the existing `{ success: false, code, message }` middleware envelope. ASP.NET authorization responses may have no body; validation uses the existing MVC problem response. No error discloses a foreign site's details.

## Persistence and permission checks

Apply `scripts/database/05.7-Operator-Provisioning.sql` after the base schema and `05.4` (nullable phone and normalized email index). It is re-runnable and does not create demo users, tenants or sites. No automatic startup migration is performed.

Creation is one transaction: identity + one `BUSINESS_OPERATOR` account per site + `OPERATOR` role + `operator_grants` metadata. Each grant persists creator/time and an explicit permission set. Shared row locks on the Owner, membership, role, tenant and sites serialize creation with scope/status changes. Duplicate email races are enforced by the database unique index; failed creation rolls back all rows.

`IOperatorProvisioningService.HasPermissionAsync(operatorId, siteId, permission, ct)` checks current user/account/role/grant/site/tenant state. Future operational endpoints must call it using the authenticated user ID and authoritative target site before acting. JWT `role=operator` alone grants no site access. The permission check is current-state read authorization; operational mutations must additionally follow their own transaction/revocation contract.

## Integration boundaries and acceptance

Owner approval currently does not create tenant membership. A real `OWNER` membership in the site's tenant must already exist, consistent with SPARK-188. Provisioning must not infer ownership from frontend fixtures or automatically create a tenant.

The existing frontend `CreateOperatorForm`/`OperatorManagement` remain local-only. They use `name`, `operatorRole`, `operatorSiteId` (including `all`) and fixture site IDs. Before connecting them, load persisted site UUIDs, map `name` to `fullName`, and let users select explicit permissions and sites. Financial presets are not an agreed delegable permission set; do not silently grant Owner financial authority. This backend task does not implement Owner staff list/edit/lock/delete APIs or staff activity workflows.

Manual/API acceptance:

1. Sign in as an approved Owner with active tenant membership and existing active sites.
2. Send the example with real owned site IDs; expect 201 and inspect persisted user/accounts/grants.
3. Sign in with the new Operator credentials on another client; expect role `operator`.
4. Retry the email, including different capitalization; expect 409 and no additional rows.
5. Include an inactive/foreign/nonexistent site; expect 403 and no partial creation.
6. Try anonymous, Driver, Operator and Admin credentials; expect 401/403.
7. Check granted permission at an assigned site; check it at a foreign site and check ungranted/Owner-only permissions; only the first succeeds.
8. Suspend the Operator account, remove the permission, disable the site or suspend the tenant; a fresh permission check must deny access.
