# SPARK-179 / SPARK-196 implementation and review — 2026-10-06

## Delivered

Owner partnership registration and Admin Applications now persist through PostgreSQL APIs. Owner registration creates a hashed-password user in `PENDING_APPROVAL`, suspended business account and pending application. Approval activates both user and account atomically; rejection keeps access disabled. Review evidence stores real authenticated reviewer ID, decision time and note.

Contract: [owner-registration-approval.md](../02-architecture/api/owner-registration-approval.md).

## Review and fixes

- Matched the existing FE fields and lot types (`outdoor`, `basement`, `multi-storey`). Business name, email, phone and terms acceptance are required on the server.
- Fixed the role mismatch between canonical database codes (`PLATFORM_ADMIN`, `OWNER`, `OPERATOR`) and JWT/FE roles (`admin`, `owner`, `operator`). Existing short auth aliases remain supported; unsupported roles cannot create a session.
- Pending/rejected users cannot log in or obtain a protected session. Admin operations check current active database permissions, including rejection of tokens after a role change.
- Case-insensitive email uniqueness and phone uniqueness reject competing duplicate registrations without overwriting identity/password.
- Row locks serialize competing approvals/rejections. A final decision cannot be overwritten or activate a deleted/ineligible user.
- FE uses separate notes per application, disables actions during requests, reports errors and supports explicit refresh. Removed local account creation/review and hardcoded reviewer ID from these screens.
- Removed unsupported email-delivery/SLA promises from the submission screen.
- Fixed file encoding during editing and duplicate rendering of the review note before final build.
- Preserved the pre-existing Login/Driver OTP working tree changes. No commit/push was made.

## Verification

- `dotnet test tests/Services/SmartParking.UserService.Tests --no-restore --verbosity quiet` against the isolated PostgreSQL test container: **58 passed, 0 failed, 0 skipped**.
- Owner PostgreSQL test covers schema upgrade and repeat application, normalized contacts, bcrypt storage, pending login denial, approved Owner login, rejected login denial, active Admin enforcement, concurrent registration, concurrent opposing reviews, repeat review conflict and recorded review evidence. It creates/removes only its generated `smartpark_auth_test_<UUID>` database.
- HTTP test covers invalid input/terms/password/lot type, 201 response without credentials or token, 401 anonymous, 403 non-Admin, successful canonical Admin review, invalid review status, authenticated reviewer identity and rejection after role change.
- `npm run build --prefix FE` passed; existing bundle-size warning remains.
- `git diff --check` passed.
- Migration `05.5-Owner-Registration-Approval.sql` was applied successfully to the local `smartparking_db` container. Existing users/permissions were preserved.
- No browser walkthrough was performed. The local database currently has only its existing Driver account; using the Admin screen requires provisioning an active Admin through the team's authorized account setup.

## Scope limits

Manual rejection criteria remain a product refinement. Review does not send an email, provision a tenant/site, or enable unrelated fixture dashboards to use real APIs. List pagination and a resubmission workflow for rejected identities are not included. Other environments must apply the migration before using the endpoints.
