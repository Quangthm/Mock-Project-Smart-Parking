# SPARK-176 implementation review — 2026-10-06

## Implemented

Driver Sign Up now calls the backend, displays a real verification form and resumes its pending challenge after reload. The backend persists a pending user, Driver account/role and salted OTP hash in PostgreSQL. Verify activates the user; sign-in accepts email or phone. Owner registration remains on its existing local flow.

Contract and provider setup: [driver-registration-otp.md](../02-architecture/api/driver-registration-otp.md).

Migration `05.4-Driver-Registration-OTP.sql` was successfully applied to the project's local `smartparking_db` container / `smartpark_db` database. No existing users were removed. Other environments must apply the same migration after 05.2.

## Review and fixes

- Kept pending users separate from password-login timed locks, so expiry of an OTP lock cannot activate an unverified account.
- Resend preserves failure counts and rejects locked accounts; a fresh code invalidates the prior code.
- Row locks serialize verification, resend and user activation. Case-insensitive email uniqueness handles concurrent registrations.
- Passwords and OTPs use bcrypt cost 12; API responses and browser storage contain no plaintext credentials or OTPs.
- Provider failure rolls back user creation or new challenge state. Production refuses delivery when no provider is configured; Development console delivery is gated by environment and configuration.
- Added backend sign-in by phone so phone-only registration can use the existing authentication flow.
- Added HTTP DTO validation, 10 requests/minute/IP rate limiting and a way to leave a restored challenge to register a different contact.
- Preserved pre-existing SPARK-173 working tree changes; no commit or push was made.

## Verification

- Final `dotnet test SmartParking.slnx --no-restore` with the PostgreSQL test connection: **56 passed, 0 failed, 0 skipped**.
- `npm run build` in FE passed. Existing bundle-size warning remains.
- Real PostgreSQL test uses a fresh `smartpark_auth_test_<random UUID>` database, then removes only that database. It covers migration idempotence, pending sign-in rejection, activation/sign-in, phone-only registration, expiry, three-failure lock, resend cooldown, old-code rejection, preserved failure count, replay rejection, concurrent failures/duplicates and delivery rollback.
- HTTP tests cover request validation, response contract, OTP format validation and rate limiting. The HTTP host's initial dependency setup issue was fixed before final verification.
- `git diff --check` passed.
- No browser walkthrough or real SMTP/SMS delivery was performed.

## Remaining integration limits

1. Development prints OTPs to the backend console. Configure SMTP or the documented HTTPS SMS adapter to deliver real codes; credentials and provider compatibility are unverified.
2. Pending recovery currently requires the original opaque challenge ID retained in the same browser. Losing storage or switching browsers before verification needs a future authenticated recovery flow. A duplicate registration does not overwrite a pending account/password.
3. Provider send and database commit are not one atomic operation; a durable notification outbox is needed to cover crashes between them.
4. IP rate limits are local to each API instance; production behind a proxy or multiple instances needs gateway enforcement and trusted proxy configuration.
5. When both email and phone are supplied, registration verifies email only. Phone aliases are not normalized. Dedicated contact re-verification is outside this task.

These limits mean the implemented local flow is testable, while real notification delivery and production deployment still need environment configuration and the integration decisions above.
