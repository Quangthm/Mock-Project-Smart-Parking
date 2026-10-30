# Identity/backend review for the 16 October demo

Reviewed 15 October 2026 against working tree at HEAD `6165024c634316d15525fe48bdc5a3ddd8193c1a`. Scope: password login/logout, Owner/Driver onboarding, Admin approval and Owner provisioning Operator. No production source edited. Existing uncommitted MFA work was preserved and reviewed as current implementation, not assumed to match the attached report.

## Authority and verification

The human request defines demo scope. Supplied Downloads/AGENTS.md and review package 01/02/03 explain intended direction and historical findings; their installation suggestions are not authorization to replace repository instructions or rewrite the service. Confirmed requirements checked: `docs/01-requirements/business-rules/01-identity-access.md`, particularly BR-AUTH-01/02/03/04/07. Only nested repository AGENTS found is FE/AGENTS.md, outside this backend review.

SDK is available: `dotnet --version` => `10.0.302`, contrary to historical audit environment. Ran:

```text
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthSessionTests|FullyQualifiedName~OwnerRegistrationTests|FullyQualifiedName~DriverRegistrationTests" --logger "console;verbosity=minimal"
30 passed, 2 skipped, 0 failed; compiled User, Parking and Reservation API projects.
```

Skipped Owner/Driver PostgreSQL tests require `SMARTPARK_AUTH_TEST_CONNECTION`; no database fixture was run. Parent reviewer also ran whole project: 65 passed, 18 skipped, 0 failed. These outcomes prove build/unit behavior, not live OTP delivery or end-to-end acceptance.

## Confirmed findings

### ID-01 · P1 · Operator becomes operational before contact verification

Evidence: `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs:76-85`; `.../Repositories/AccountWorkflowService.cs:98-101,187-203`; `src/Services/UserService/SmartParking.UserService.Application/Usecase/Login/LoginCommandHandler.cs:37-65`.

Create sets both User and site Account ACTIVE without setting a verified contact. Password login accepts the Owner-supplied password immediately. The onboarding link changes password optionally and never records EmailVerifiedAt. OTP login's verified-channel guard applies only to Driver/Owner, so Operator/Admin also bypass that prerequisite. This violates BR-AUTH-01/02 even if the happy-path demo works. Reproduction: create Operator, do not open email, authenticate with initial password, perform an authorized site action. Fix by keeping new Operator pending until purpose-bound contact proof, then activating assigned accounts; apply verified-channel eligibility consistently for OTP across roles. Add HTTP test: before proof no ordinary session, after proof access only granted sites.

### ID-02 · P1 · Permission on tenant A grants operation scope on tenant B

Evidence: `.../Repositories/AccountWorkflowService.cs:35-46`; `.../Repositories/OperatorProvisioningService.cs:59-72`; `src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs:19-23`.

RequirePermissionAsync only proves that some membership grants an action and discards the matching IDs. The following queries return all Owner tenant memberships without the same permission predicate. An Owner with OPERATOR_MANAGE on A and explicitly empty permissions on B can create an Operator for B; the same composition exists for parking owner-scope. This scenario requires multiple memberships; no runtime exploit claimed. Return/filter the exact authorized memberships and bind resource tenant to those same rows. Test allow A/deny B, including explicit empty permissions and revoked account.

### ID-03 · P1 · Session lifetime disagrees with approved baseline

Evidence: `src/Services/UserService/SmartParking.UserService.Application/Services/AccessTokenService.cs:13`; `.../Services/AuthSessionService.cs:19-23,34-44`; `.../Common/Models/JwT/AuthenticationPolicy.cs`; `tests/Services/SmartParking.UserService.Tests/AuthSessionTests.cs:23,28`.

Access lifetime remains 86,400 seconds rather than approved 900. Refresh checks seven-day absolute expiry but no 24-hour inactivity deadline. Existing passing tests explicitly assert the wrong access lifetime, so green tests do not establish requirement compliance. Preserve absolute lifetime/CAS behavior; introduce approved lifetime and refresh activity tracking, then test equality at 15 minutes/24 hours/7 days. This does not inherently block a short demo but must not be described as accepted identity security.

### ID-04 · P1 · Admin user management omits ACCOUNT_ADMIN permission

Evidence: `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserManagementService.cs:31-41,66-82`, contrasted with `.../Repositories/OwnerRegistrationService.cs:53-59`.

An active Admin membership with explicit empty permissions still lists/details/locks users because RequireAdmin checks role and status only. Owner approval correctly invokes ACCOUNT_ADMIN first. Use the same current permission semantics on all user-management actions, preserving self/Admin target protections. Test explicit denial for empty grants and current revocation. This is beyond the narrow demo's approval endpoint but material to the leader's authorization review.

### ID-05 · P2 · Password phone login fails for accepted equivalent phone forms

Evidence: `.../Usecase/Login/LoginCommandHandler.cs:18`; `.../Repositories/UserRepository.cs:26-32`; `.../Repositories/AccountWorkflowService.cs:26-33`; `.../Common/Validation/SignInContact.cs:11-12`.

Registration normalizes +84/84 to local 0 form. Password login only trims and compares exact phone while validation accepts those equivalent forms. A Driver registered as +84901234567 is stored as 0901234567; password login with original +84 input returns AUTH_FAILED. OTP login canonicalizes correctly. Reuse the same pure canonicalization before password lookup and test equivalent accepted inputs. For demo rehearsal, use email login until fixed; that is a workaround, not acceptance.

### ID-06 · P2 · Logout can lose the active session when refresh wins concurrently

Evidence: `.../Usecase/Session/LogoutCommand.cs:23-24`; `.../Repositories/PostgresAuthSessionStore.cs:31-35,39-44`.

Refresh mutates access ID and refresh hash on the existing row. Logout revokes only the exact current access/hash pair. If authenticated logout starts with old pair while another request rotates, its revoke affects zero rows and the replacement session survives. Requires a coordinated PostgreSQL concurrency test; not a reproduced exploit. Define revocation by stable logical session with authenticated ownership and replay rules, then verify concurrent refresh, logout and lost refresh response.

## Architecture findings relevant to the leader

Many working flows still bypass CQRS: OwnerApplicationsController/OperatorsController/AuthController call workflow services directly; workflow validation, business decisions, SQL and transaction ownership remain in Persistence services. IdentityScopeController directly queries AppDbContext. Historical U11 remains valid. Correct security behavior first, then extract each reviewed workflow into Application handlers through ports. A mass rename or moving the same orchestration unchanged does not establish Clean Architecture.

Owner review calls Parking HTTP while holding local transaction/user/application locks (`OwnerRegistrationService.cs:75-106`). Deterministic tenant ID supports a retry but cannot make remote provisioning and local commit atomic. Parking unavailability makes approval fail, and remote success/local failure needs reconciliation. Preserve idempotency and introduce explicit recoverable provisioning/outbox workflow rather than claiming distributed atomicity.

## Historical audit findings that must not be repeated unchanged

- U02 is partly repaired in the working tree: password verification now precedes MFA lookup and returns restricted challenge; MfaWorkflowService completes it. Historical statement “no password-to-TOTP continuation” is stale. `IMfaPolicy? mfa = null` still allows fail-open construction outside actual production DI; current production DI registers it.
- U04 is partly repaired: new MFA enrollment/challenge/backup-code implementation and tests exist. A complete baseline lifecycle acceptance claim still needs dedicated review, especially Admin recovery/security versions; absence of all MFA code is no longer true.
- U01, U03, U06, U07, U09 and U10 remain demonstrable from current source as described above.
- The historical audit's lack of SDK no longer applies. The missing isolated PostgreSQL/runtime verification remains an actual evidence limit.

## Demo rehearsal prerequisites

1. Owner registration creates PendingVerification; verify emailed Owner OTP before Admin approval. Review refuses non-PendingApproval/unverified users. On approval ParkingService must be reachable for tenant provisioning.
2. Driver registration creates PendingVerification; queue worker dispatches OTP; verification activates Driver. Prepare unique contacts and test actual inbox delivery before presenting.
3. SMTP configuration is a real dependency: WorkflowTransport throws without SMTP host. SMS needs configured HTTPS provider; a queue acceptance is not SMS delivery evidence. Do not assume test fakes are production adapters.
4. Operator creation may return success while onboarding delivery fails. Onboarding adapter additionally requires HTTPS `Onboarding:PublicOrigin` for its password link (`Infrastructure/Services/WorkflowTransport.cs:19-20`); ordinary http://localhost configuration fails that mail before SMTP send. Inspect delivery status in rehearsal and configure approved HTTPS origin if the email/link itself is part of the demo.
5. Login/logout basic unit paths pass. None of the skipped database tests proves live registration, approval or concurrency here. Show current Operator behavior honestly as a known requirement gap, or fix it before claiming compliant onboarding.

## Follow-up runtime recheck, 15 October

The user reported Docker databases running. Root reviewer coordinates database checks; this reviewer did not run duplicate integration tests or change application source.

Read-only HTTP requests against the running User API returned:

| Request, without bearer token | Result |
|---|---|
| GET http://localhost:5035/openapi/v1.json | 200 |
| GET http://localhost:5035/api/auth/me | 401 |
| GET http://localhost:5035/api/owner-applications | 401 |
| GET http://localhost:5035/api/operators | 401 |

This establishes API availability and unauthenticated route protection. It does not establish authenticated permissions, database schema completeness or real mail delivery. No login, registration, OTP dispatch, password mutation or external email was performed for these checks.

Presence-only configuration inspection, without printing credentials: repository `.env` includes SMTP host/port, while its SMTP username/password/from entries are empty. However, the User API's configured Development user-secrets **do contain** SMTP host, username, password and from. It would be incorrect to conclude SMTP is entirely unconfigured from `.env` alone. The normal runner preserves those user-secrets when corresponding `.env` values are empty. Credential validity/provider access remains untested.

`Onboarding:PublicOrigin` is absent from inspected `.env`, current reviewer process environment, Development user-secrets and tracked User API appsettings. The HTTPS-origin requirement for Operator onboarding remains a concrete rehearsal concern. Another already-running process may have independent environment overrides; its effective configuration was not introspected.

Current source still contains ID-01 through ID-06; starting databases does not change those source-level conditions.

For isolated database verification, `SMARTPARK_AUTH_TEST_CONNECTION` must point to the local test server using an account permitted to CREATE DATABASE. `UserServiceDatabaseFixture` creates a fresh `smartpark_auth_test_<32-hex>` database and drops only that generated database after checking its name; `PostgresAuthTests` follows the same pattern. Schema initialization applies repository User base/alignment scripts plus 05.8 and 05.13 within the generated test DB. The checked-in schema files exist, so no external `SMARTPARK_SCHEMA_ROOT` override is required. Connection strings/passwords should stay in process environment and never be included in review output.
