# SPARK-206 — Fix and review (2026-10-06)

## Result

Implemented baseline user management end to end. Admin dashboard now opens User Management backed by `/api/users`, replacing local-only Driver controls. All roles can be searched/filtered/paged; account detail shows persisted successful login-session history. Admin can lock/unlock active or locked non-Admin users.

Original defects corrected:

- Local status edits did not affect backend identity/session authorization.
- Original Driver screen omitted other user roles and search/filter/pagination.
- UI lock expired after 30 minutes; explicit administrative locks now persist until unlock.
- Activating pending/rejected accounts could bypass registration/Owner approval when implemented as unrestricted status changes; API rejects those transitions.
- Unlock must not revive previously issued sessions; lock atomically revokes them in PostgreSQL.
- Local profile/password reset controls implied changes to real credentials; replacement screen exposes the supported backend operations only.

Review also corrected a UI cancellation issue: clearing selection while details are loading must clear the loading state. Filters/selection are disabled during a status mutation, failed details can be retried, stale list/detail responses are ignored after effect cleanup, and API failures never fall back to local fixtures.

## Executed validation

| Check | Result |
| --- | --- |
| `dotnet test SmartParking.slnx --no-restore` with isolated PostgreSQL test connection | 60 passed, 0 failed, 0 skipped |
| SPARK-206 HTTP + real PostgreSQL integration tests | Included in the above: 2 passed |
| `npm run build` in FE | Passed; existing large-bundle warning remains |
| `git diff --check` | Passed |

SPARK-206 tests cover anonymous/non-Admin access, current role revocation and actor lock, input validation, no-store responses and secret-free DTOs, actual EF/PostgreSQL search/filter/paging, deleted user exclusion, pagination overflow, self/Admin protection, pending account transition rejection, indefinite locks, session revocation, blocked login, invalid old refresh tokens even after unlock, successful new login after unlock, clearing timed locks/failure counters and concurrent status changes.

Tests create and remove their own random `smartpark_auth_test_<id>` databases on the existing test container at port 65433; they do not change the project database. Pre-existing working-tree changes to auth and registration were preserved.

## Scope and remaining limits

- Browser interaction/responsive visual QA was not executed; frontend build and source review were executed. Follow manual acceptance in the contract.
- Activity is the latest 50 successful login sessions. Parking/payment/support history and a persisted audit trail of Admin status actions are outside this implementation.
- Disable/suspend, profile updates and password reset need separate agreed contracts. Admin-target blocking is a conservative implementation guardrail.
- Request/response details complete the endpoint outline; the team should confirm the contract before additional clients integrate.

Contract and manual acceptance: [Admin user management](../02-architecture/api/admin-user-management.md).
