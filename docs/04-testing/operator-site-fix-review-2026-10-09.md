# Owner site / operator fix — review and team test

Date: 2026-10-09

## Changes

- Owner site selectors, site count/capacity and employee count use ParkingService/UserService data.
- Sites and My parking lots display backend site records; create/edit profiles are saved through ParkingService.
- With no active sites, New Operator opens site creation and then continues to operator creation.
- Operator creation uses POST /api/users, including real site IDs, permissions and the backend password policy.
- Parking Structure reads saved units and slots, and supports creating floor/zone/block units and slots, plus removal through backend operations. It no longer seeds local demo layouts.
- Site profile codes are normalized to uppercase consistently with backend behavior.
- PostgreSQL test helpers can find feature migrations in SMARTPARK_SCHEMA_ROOT when absent from the code checkout.

## Team prerequisites

Run UserService and ParkingService. Structure reads/updates also require ReservationService because the backend checks reservations and structural commitments. Configure service keys, connection strings and schema migrations using the team's existing local setup. Never commit .cache credentials or local key rings.

## Manual acceptance

1. Sign in as an approved Owner.
2. If there are no active sites, press New Operator. Create a site with a unique code, name and address. Confirm the screen continues to operator creation.
3. Create an operator with a fresh email, an 8–15 character password containing uppercase, lowercase, a digit and a special character, and at least one permission.
4. Reload the page. Confirm the site appears in Sites/My parking lots and the operator appears in Operators.
5. Open Parking Structure, select the site and add a floor/zone with capacity, then a slot under that unit. Reload and confirm both persist.
6. Open Dashboard and check site/capacity/employee totals. After filtering one site, open Structure and verify selecting another site works.
7. Sign out and sign in with the operator's email/password. Confirm the assigned site/permissions are returned on the operator assignments screen.
8. Confirm duplicate email/site-code and invalid passwords display errors without reporting success. Test an unreachable API and retry after restoring it.
9. Test removal of an unused unit/slot. For occupied/protected slots or downstream operations needing resolution, confirm backend rejection/pending status is shown rather than reporting success.

## Automated verification

Frontend, from repository root (after installing FE dependencies):

```powershell
node --test tests/frontend/*.test.cjs
```

Frontend build, from FE:

```powershell
npm run build
```

Backend tests require SMARTPARK_AUTH_TEST_CONNECTION and SMARTPARK_SCHEMA_ROOT. Use a local PostgreSQL test server where the test user can create databases. Tests create randomly named databases and clean them afterward.

```powershell
dotnet test tests/Services/SmartParking.ParkingService.Tests/SmartParking.ParkingService.Tests.csproj --artifacts-path .cache/parking-review-artifacts
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --artifacts-path .cache/operator-review-artifacts --filter 'FullyQualifiedName~OperatorProvisioning'
```

Results on this machine: frontend build passed; 48 frontend tests, 14 Parking tests including PostgreSQL, and 2 operator tests including PostgreSQL passed. Browser interactions have not been executed automatically; use the manual acceptance flow above.

## Login validation / approval follow-up

Password and Email/SMS OTP now share email/phone validation. Malformed contacts display a validation error, and backend validation rejects direct requests before attempting authentication. The previous status checks grouped unapproved accounts with failed authentication; pending approval, pending verification, rejected, inactive and missing access now have specific messages. Password sign-in only discloses these statuses after checking the password. Blocked accounts never receive sessions or OTP deliveries.

Restart UserService to load the backend changes and reload the frontend before testing:

1. In both Password and Email/SMS code modes, try `invalid`, `a@@example.com`, `a@example` and an email with internal spaces. Confirm an inline validation message and that a valid retry remains possible.
2. With a PendingApproval account, enter the correct password. Expect HTTP 403 / `ACCOUNT_PENDING_APPROVAL` and a message asking the user to wait for administrator approval. A wrong password must still show invalid credentials.
3. Request an OTP for the same account. Expect the pending approval message without sending a code. Approve the account and retry; OTP login should work for a verified contact.
4. Change an account from Active to PendingApproval after requesting a code. Completing OTP must show pending approval and create no session. Restore Active and retry the still-valid code.
5. Verify normal approved accounts can still sign in and wrong credentials remain rejected.

Verification: 69 frontend tests and frontend build passed. The selected UserService suite passed all 97 tests, including real PostgreSQL workflow/operator tests; the expanded OTP status-change test also passed separately. The reported process crash was not reproduced: the malformed password request returned HTTP 400, and HTTP tests verify a subsequent valid login succeeds. Live browser testing remains a team acceptance step.

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --artifacts-path .cache/login-review-artifacts --filter 'FullyQualifiedName~AuthSessionTests|FullyQualifiedName~OperatorProvisioningHttpTests'
```

## Limits and commit scope

At the user's request, the standalone `SignInValidationTests.cs` and `RegistrationEmailValidationTests.cs` files were removed after verification. The previously committed `PostgresStructureTests.cs`, `AuthHttpTests.cs`, `DatabaseIntegrationTests.cs`, `OperatorProvisioningTests.cs`, `ServiceSchema.cs` and `WorkflowFeatureTests.cs` files were also removed. Shared schema/directory helpers were moved into the existing `WorkflowTestSupport.cs` to keep remaining tests compilable. Test counts and workflow coverage recorded above describe the runs before this cleanup; the deleted tests are no longer part of the current suite.

Final checks after cleanup: 73 frontend tests and the frontend build passed. The 33 remaining selected backend tests (`AuthSessionTests`, `OperatorProvisioningHttpTests`, `DriverRegistrationHttpTests`, `OwnerRegistrationHttpTests`) passed. Both UserService.Tests and ParkingService.Tests build successfully. `git diff --check` passed after removing a trailing blank line in the combined helper. Live browser acceptance and the entire backend test project have not been verified after cleanup.

Email validation follow-up: account creation for Operator, Owner and Driver, plus development Admin bootstrap, now uses the same email rule as Password/OTP login. The Operator form also checks that rule before sending a request. Phone-only Driver registration remains supported. Invalid examples such as `a@example`, `a..b@example.com` and `a@example..com` must be rejected without creating an operator, grant, challenge or delivery. Verify uppercase emails, plus addressing and surrounding spaces still work.

Updated verification: 73 frontend tests and frontend build passed; the selected UserService suite passed 111 tests, including registration DTO validation and actual operator provisioning followed by login. The separate integration test using real UserService, ParkingService and ReservationService hosts and isolated PostgreSQL databases also passed (112 backend tests in total across these runs). Two older migration-upgrade tests (`DriverRegistrationTests` and `OwnerRegistrationTests`) could not run because migrations `05.4-Driver-Registration-OTP.sql` and `05.5-Owner-Registration-Approval.sql` are absent from both this checkout and the configured schema checkout. Current registration/OTP workflows are covered by `WorkflowFeatureTests`; do not interpret the selected-suite pass as a pass of the entire test project.

- Operator Lock/Remove/site reassignment remain unavailable; they no longer pretend to persist local changes.
- Revenue and booking reports remain based on local booking data. Payment/reservation reporting integration is outside this fix.
- New site capacity starts at zero and increases as physical slots are created. The Structure screen now exposes the supported backend layout actions rather than the old local demo editor.
- Local demo sites/operators are not automatically migrated to PostgreSQL.
- Include the new SiteForm.tsx, frontend regression test and this guide when staging. Review unrelated setup-local.ps1 changes and the deletion of LOCAL-SETUP.md separately; they were present in the working tree before this fix.
