# SPARK-192 — Implementation and review (2026-10-06)

## Result

Backend Create Operator is implemented at `POST /api/users`. Current active tenant Owners can create a new Operator identity with explicit active owned sites and supported lot-scoped permissions. Passwords use the existing BCrypt service. Each site receives its own account/role/grant; all rows commit atomically. Creator and creation time are persisted per grant. A fresh permission check uses current database state rather than trusting a JWT role for parking-site access.

Task changes:

- Added `CreateOperatorDto`, `OperatorDto`, `IOperatorProvisioningService`, `OperatorGrant` and `OperatorProvisioningService`.
- Added the Owner-only POST action to the existing local `UsersController`; moved Admin authorization to each existing GET/PATCH action and renamed its caller-ID helper to `ActorId`. Admin-only read/status operations remain covered by regression tests.
- Added grant mapping/DbSet and DI registration to the existing locally modified persistence files.
- Added `05.7-Operator-Provisioning.sql`, API contract and two test scenarios (HTTP and real PostgreSQL).
- Preserved all pre-existing registration, approval, user-management, ParkingService and frontend changes. No frontend files were edited in this task.

## Review findings and decisions

1. Existing frontend creation only stored plaintext fixture credentials in localStorage and did not create a backend identity. Backend now persists a real BCrypt-protected identity; the frontend remains a separate integration step.
2. JWT `owner` role alone is insufficient to establish parking-site ownership. Creation reads current active tenant membership, Owner role, tenant status and selected sites, with locks to serialize concurrent scope changes.
3. Arbitrary client role/owner/tenant fields and the frontend `all` wildcard must not control authority. Server determines Operator role and tenant/account assignment; every selected site is explicit.
4. Financial/cashier/operation UI presets are not a defined delegable permission contract. This implementation accepts only explicit SRS-supported Operator permissions. Owner financial powers, pricing, refund approval and staff management are excluded.
5. Email checks alone are vulnerable to concurrent creation. The normalized database unique index enforces uniqueness; a losing transaction rolls back the user, accounts and grants.
6. Creator identity is retained as provisioning metadata. This does not implement general staff activity logs, notifications or credential delivery.
7. `HasPermissionAsync` denies locked users, suspended/deleted accounts, missing Operator role, ungranted permissions, unassigned/inactive/deleted sites and suspended/deleted tenants. Operational mutations must implement their own transaction and revocation semantics when integrating this check.

## Verification

- `dotnet test SmartParking.slnx --no-restore --verbosity minimal` with the existing isolated PostgreSQL test server at `127.0.0.1:65433`: **73 passed, 0 failed, 0 skipped** (62 UserService + 11 ParkingService).
- New HTTP test: anonymous/Driver/Operator/Admin rejection; Owner accepted; Owner cannot use Admin list; malformed and missing input; empty/duplicate/invalid sites; unsupported/duplicate/null permissions; password policy; authenticated creator ID; 201/no-store/secret-free response; current caller lock invalidates session.
- New PostgreSQL test uses the actual base schema and migrations, applies 05.7 twice, validates case-normalized arbitrary-domain email, trimmed name, real stored BCrypt hash, login as Operator, two-site account/grant creation, foreign/missing/inactive-site rejection and no partial writes, unsupported permission rejection, email duplicate/concurrent race, creator metadata, user/account locks, permission removal, site deactivation, tenant suspension and pending Owner rejection.
- Tests create/remove only their own `smartpark_auth_test_<guid>` databases; the project database was not migrated or modified.
- `git diff --check`: passed. Reviewed touched source and newly added files against their task scope; no frontend/dependency/configuration edits.

One intermediate test invocation collided with a still-running regression test's DLL lock. It failed before executing tests; verification was rerun sequentially after that process completed. A test harness initially passed the full SQL script through EF raw formatting, which interpreted JSON braces; using Npgsql to execute the unchanged script resolved it.

## Remaining integration requirements

- Apply migration 05.7 before calling the endpoint in the project database; 05.4 is a prerequisite for email-only Operator identities and normalized uniqueness.
- Owner approval currently does not create a tenant membership. An approved Owner still needs an active `OWNER` account in the actual site's tenant to provision staff, consistent with SPARK-188.
- Frontend Create Operator still uses local fixtures. It needs persisted site IDs and explicit permission inputs before wiring this contract. No end-to-end browser acceptance is claimed.
- Owner staff listing/editing/locking/removal, activity views, invitations/password change and notifications are outside Create Operator. Operational APIs must integrate the current permission check; this task does not claim enforcement in endpoints that do not yet exist.
- Provisioning fields/initial-password policy and the explicit permission contract are implementation choices documented for team confirmation; the existing endpoint path is retained.

Contract and manual acceptance: [Create Operator](../02-architecture/api/operator-provisioning.md).
