# Owner onboarding regression checks

Run the component-render, storage migration, and API contract checks from `FE`:

```sh
node tests/owner-onboarding.test.mjs
```

For browser interaction checks, start the existing Vite development server and open:

```text
http://localhost:5173/tests/owner-onboarding.browser.html
```

Click **Run browser regression checks**. This dev-only harness renders the real
components with deterministic API fixtures; it does not send requests to the backend.
It covers recovery from legacy storage, wrong OTP, expired setup token without losing
entered lot data, contact verification after saving, competing Driver/Business state,
and automatic continuation from normal password or email/SMS sign-in. The harness restores the authentication and
registration storage keys it temporarily replaces. Run it in a dedicated browser tab.

Backend PostgreSQL/HTTP tests live in `OwnerSetupRecoveryTests.cs`. They verify token
rotation, single-use codes, purpose isolation, resend cooldown, attempt budget across
resends, expiry, concurrent verification, and legacy verification state. Run them with
the local test settings described in the repository's `tests/README.md`.

Recovery endpoints:

- `POST /api/auth/register/owner/setup-recovery/request` with `{ contact: email }` queues an OTP and returns challenge timing only.
- `POST /api/auth/register/owner/setup-recovery/complete` with `{ challengeId, code }` consumes the OTP and returns a replacement setup token, valid for 24 hours.

The recovery OTP does not submit the application for approval. After saving the lot,
the normal owner email verification must complete. Email delivery uses the existing
configured SMTP workflow; the regression harness does not prove real email delivery.
