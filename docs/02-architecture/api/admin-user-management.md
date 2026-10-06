# SPARK-206 — Admin user management

Implemented 2026-10-06 against SRS v0.9 §3.7.4 and FR-AUTH-06. These request/response details complete the endpoint outline from API Design; confirm them with the team before another client integrates.

All endpoints require a valid Bearer session and current active Admin permissions. Responses use `{ "success": true, "data": ... }`, are not cached, and never expose password hashes, access tokens, refresh hashes or session identifiers. Deleted users are excluded. No schema migration is required beyond the existing auth/registration schema.

| Method / path | Input | `data` |
| --- | --- | --- |
| `GET /api/users` | Optional `search` (max 200 characters), `role`, `status`, `page` (default 1), `pageSize` (default 20, max 100) | `{ items: ManagedUser[], total, page, pageSize }` |
| `GET /api/users/{id}` | UUID user ID | `{ user: ManagedUser, loginActivity: LoginActivity[] }` |
| `PATCH /api/users/{id}/status` | `{ "status": "locked" }` or `{ "status": "active" }` | Updated `ManagedUser` |

Search matches name/email case-insensitively and phone by substring. Role filter: `driver`, `owner`, `operator`, `admin`. Status filter: `active`, `locked`, `pendingVerification`, `pendingApproval`, `rejected`. Ordering is full name then ID; pagination is one-based. Filters combine with AND.

`ManagedUser` contains `id`, `fullName`, nullable `email`/`phone`, `roles`, `status`, nullable `createdAt`/`lockedUntil`. Roles include non-deleted account memberships, including suspended memberships, so pending Owners remain discoverable. Membership listing does not grant access.

`LoginActivity` contains `createdAt`, `expiresAt` (refresh session expiry), `isRevoked`. Detail returns at most the latest 50 persisted successful login sessions, ordered newest first. This is login-session history, not parking/payment/support activity or a complete audit trail of Admin actions. Expired/revoked sessions remain history. Refresh rotates the same session record.

## Access transitions

- Lock/unlock applies only to users currently `active` or `locked`.
- Admin lock sets `lockedUntil = null`, persists until explicit unlock, clears the login-failure counter and atomically revokes every current session for that user.
- Unlock clears `lockedUntil` and the failure counter. It does not revive revoked tokens; the user must sign in again.
- Repeating lock/unlock is safe. A timed login lock can be converted to an indefinite Admin lock or explicitly unlocked.
- Target row locks serialize simultaneous status updates and login attempts. Session revocation and status update share one transaction.
- Self changes and any Admin-account target are rejected to protect Admin access. This is an implementation guardrail, not a new SRS rule.
- Pending/rejected accounts cannot be activated or locked here: registration/verification and Owner approval remain separate workflows.
- `disabled`/`suspended`, profile edits and password reset are not exposed by this API: their contracts are not defined in the SRS baseline. The former local-only Driver controls are replaced by User Management in the Admin dashboard.

## Errors

| HTTP | Code / case |
| --- | --- |
| 400 | Validation failure (invalid filters, paging, missing/unsupported status) |
| 401 | Invalid, expired, revoked session; locked/deleted actor or changed role claim |
| 403 | Authenticated non-Admin; service-level `FORBIDDEN` if Admin access is no longer active |
| 404 | `USER_NOT_FOUND` for absent/deleted user |
| 409 | `ADMIN_ACCOUNT_PROTECTED` or `INVALID_STATUS_TRANSITION` |

API input validation follows existing validation middleware; service errors use `{ success: false, code, message }`.

## Manual acceptance

Use an active backend Admin account, not the frontend's local fixture. Open Admin dashboard → User management. Search by name/email/phone, filter role/status and move across pages. Select an active Driver/Owner/Operator and view real login sessions. Lock that account while it is logged in in another browser: its next protected request and refresh must fail, and a new login must be rejected. Unlock and sign in again: login succeeds; the old tokens remain invalid. Pending Owners must still use Applications for review. Disconnect the API to check errors/retry; it must not show local users as a fallback.
