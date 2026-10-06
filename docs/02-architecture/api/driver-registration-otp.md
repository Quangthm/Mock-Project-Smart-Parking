# SPARK-176 — Driver registration and OTP

Implemented contract, 2026-10-06. Basis: SRS §3.1.1 / FR-AUTH-01 and UC-AUTH-01.

## Endpoints

All endpoints are anonymous under `/api/auth`, return `Cache-Control: no-store`, and never return passwords, OTP codes, hashes or session tokens.

| Method/path | Request | Success |
| --- | --- | --- |
| `POST /register/driver` | `fullName`, `password`, optional `email`, optional `phone` | 201, pending challenge |
| `POST /register/driver/verify` | `registrationId` UUID, `code` six digits | 200, account activated |
| `POST /register/driver/resend` | `registrationId` UUID | 200, updated challenge timings |

Registration example:

```json
{"fullName":"Driver Example","email":"driver@example.com","password":"Password@123"}
```

201/resend response example (timestamps are illustrative):

```json
{"success":true,"data":{"registrationId":"b5f2a724-601b-40e0-bb08-0e0b18006dd1","channel":"email","expiresAt":"2026-10-06T10:05:00Z","resendAvailableAt":"2026-10-06T10:01:00Z"}}
```

Verify success: `{"success":true,"message":"Account verified. You can now sign in."}`.
Errors use `{"success":false,"code":"OTP_INVALID","message":"Incorrect verification code."}`; DTO validation uses the existing ASP.NET validation problem response, supported by the FE error parser. IP rate limiting may return an empty 429 response.

| Status/code | Meaning |
| --- | --- |
| 400 validation / `VALIDATION_FAILED` | Missing full name/contact, invalid email/phone/password/code |
| 409 `CONTACT_EXISTS` | Active or pending contact already reserved |
| 404 `REGISTRATION_NOT_FOUND` | Unknown challenge |
| 409 `REGISTRATION_CLOSED` | Verified, deleted or no longer pending |
| 400 `OTP_INVALID` / `OTP_EXPIRED` | Incorrect code / expired code |
| 423 `OTP_LOCKED` | Three failed attempts; verification/resend locked for 15 minutes |
| 429 `OTP_COOLDOWN` | Resend requested within 60 seconds |
| 503 `OTP_DELIVERY_FAILED` | Provider unavailable or unconfigured |

## Validation and state

- Full name: nonblank, max 255 characters. At least one contact required; email is trimmed/lowercased, phone is 9–15 digits with optional leading `+`. Phone formats are compared literally; international/local aliases are not canonicalized.
- Password matches existing FE rules: 8–15 characters, upper/lowercase, digit and special character. Passwords and OTP codes use bcrypt cost 12.
- If both contacts are supplied, OTP goes to email. This verifies the selected channel, not both contacts.
- Creates `users` in `PENDING_VERIFICATION`, a `DRIVER` account and the `DRIVER` role link in one transaction. Existing auth rejects pending users. Verify changes the user to `ACTIVE`; the next login creates the session.
- The opaque random registration ID identifies the OTP challenge, independently of the user ID. FE keeps it and timings in localStorage to resume after reload; passwords and codes stay out of storage. Keep this ID private.
- Each code lasts exactly 5 minutes. Resend replaces the previous code, with a 60-second cooldown, without resetting failed attempts. After three failures, both verify and resend are locked for 15 minutes. After lock expiry request a new code.
- PostgreSQL row locks serialize verify/resend and activation. Unique active-contact indexes handle concurrent registration. Provider failures roll back registration/resend changes. Verified challenges cannot be reused.
- Combined registration/verify/resend traffic is limited to 10 requests/minute/IP per API process. For multiple instances or reverse proxies, shared gateway limits and trusted proxy address handling are still required.
- Existing login field `email` now accepts an email or phone string. URL and response remain compatible.

## Local setup and delivery

Apply `scripts/database/05.4-Driver-Registration-OTP.sql` after `05.2-Auth-Design-Alignment.sql`. It permits absent phone, adds a case-insensitive email uniqueness index and creates persistent challenges. Existing mixed-case duplicate emails must be resolved if the index cannot be created; the migration fails atomically instead of deleting data.

Development enables `Otp:DevelopmentLogCodes` in `appsettings.Development.json`. Codes appear in the backend console only; no actual email/SMS is sent in this mode. Production ignores this flag and requires a provider.

Configure through environment variables/secrets:

| Key | Purpose |
| --- | --- |
| `Otp__DevelopmentLogCodes=false` | Enable actual delivery in Development |
| `Otp__Smtp__Host`, `Otp__Smtp__Port` (default 587) | SMTP server with TLS |
| `Otp__Smtp__Username`, `Otp__Smtp__Password`, `Otp__Smtp__From` | SMTP credentials/sender |
| `Otp__Sms__Url`, `Otp__Sms__ApiKey` | HTTPS notification adapter accepting `{to,message}` with Bearer authentication |

The SMS endpoint is an adapter contract, not a direct integration with a specific vendor. SMTP delivery has no persistent outbox; a crash after delivery but before transaction commit can send an unusable code. Retry/resend handles ordinary failures, but provider deduplication/outbox and pending-account recovery without the original challenge ID remain follow-up work. No real provider credentials were supplied or verified.

Owner registration and existing login OTP/MFA are outside SPARK-176.
