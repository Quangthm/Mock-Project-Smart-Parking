# MFA QA recheck — 2026-10-20

Baseline: `4aef22e946009c71b8aea794bc82da7332058a19` plus the new TOTP regression in `MfaLifecycleTests.cs`. This is a focused verification record, not QA sign-off or a replacement for the tester's historical reports.

## Findings and correction

- FE-01 was present at `141fa94`; commit `4aef22e` repairs the SignIn catch block. `npm run build` passed during the preceding review. Browser UAT remains outstanding.
- The earlier review claim that the recovery-code branch was missing was incorrect. Git HEAD already looks up an unused code for the user, marks it used, resets attempts and returns successfully. No production recovery-code patch was needed.
- The tester's MFA HTTP 500 was not reproduced with the current code and an isolated PostgreSQL fixture. Its original cause remains unknown; passing fixture tests do not establish that the tester's environment is healthy.

## Executed checks

- UserService suite before adding the regression: 96 passed, 0 failed, 26 skipped without external test dependencies.
- MFA lifecycle suite with PostgreSQL 16: 7 passed, 0 failed, 0 skipped after adding the regression. This includes recovery single use/concurrency, replacement/regeneration/disable, lockout, and OTP MFA enforcement.
- New HTTP pipeline regression covers trailing-space TOTP (400), valid TOTP login and authenticated `/me` (200), same-step replay (401), and exact challenge expiry (401). Recovery codes remain unused by the TOTP flow.
- Local TRX: `.cache/qa-mfa-review/mfa-review.trx`. This ignored local artifact must be attached separately if sharing this report.
- Two independent GPT-6.1 Sol medium reviewers inspected the implementation and the new regression. No actionable finding remained in this focused review.

The fixture uses a dedicated localhost PostgreSQL container and creates/drops randomly named test databases. No shared application database was used. HTTP requests run through ASP.NET TestServer with real PostgreSQL persistence; this is not a browser UAT or deployed-environment test.

## Tester handoff

1. Record the exact deployed backend/frontend SHA and database migration baseline. Retest FE build and Owner/Admin/Login/MFA/Logout browser flows on the new build.
2. For MFA, use a fresh password challenge and a valid authenticator code from a new time step after enabling MFA. Do not reuse the code already consumed by setup.
3. If HTTP 500 recurs, capture UTC timestamp, endpoint, response `traceId`, and the corresponding server exception. Redact challenge tokens, access/refresh tokens, TOTP seeds, recovery codes and credentials.
4. Check migration alignment and the availability of the original Data Protection key ring (`Workflow:KeyDirectory`, application name and process identity). These are investigation targets, not confirmed causes. Do not reset existing keys or delete MFA data as a workaround.
5. Keep MFA-500 open pending correlation with the original environment. Keep Frontend UAT and missing refresh/integration evidence unapproved until executed.

This change contains only regression tests and verification notes. No production source changes, Jira transition or PR merge were made during this recheck.
