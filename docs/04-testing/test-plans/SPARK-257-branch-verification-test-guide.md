# SPARK-257 Branch Verification — Test Guide for PM Approval to Merge into `develop`

**Branch:** `docs/SPARK-257-bot-protection-api-design`  
**Jira:** SPARK-257 — `[SEC-AUTH] Protect public authentication flows from automated abuse`  
**Prepared for:** Tester / QA → PM sign-off before merge  
**Date:** 2026-11-07  

---

## 1. Scope of this branch

This branch delivers end-to-end bot protection via Cloudflare Turnstile across all public authentication flows (`/api/auth/login`, `/api/auth/register/driver`, `/api/auth/register/owner`) for SPARK-257:

- **Phase 1 — Port, Adapter & Error Mapping:**
  - Application Port: `IBotVerificationService` cleanly isolated with no HTTP or infrastructure dependencies.
  - Infrastructure Adapter: `TurnstileVerifier` implementing fail-closed semantics, hardened typed `HttpClient` (5s timeout, redirect disabled), case-insensitive hostname allowlisting, action binding, and log sanitization.
  - Error Mapping: 4 error codes mapped in `ApiExceptionMapper`: `BOT_VERIFICATION_REQUIRED` (400), `BOT_VERIFICATION_FAILED` (400), `BOT_VERIFICATION_EXPIRED` (400), `BOT_VERIFICATION_UNAVAILABLE` (503).
  - Startup Guard: Enforces non-dev environments have Turnstile enabled with non-empty SecretKey and valid hostnames (strictly rejecting localhost, wildcards, ports, schemes in Production).
- **Phase 2 — Service Wiring & Rate Limiting:**
  - Token field `turnstileToken` added to `LoginDto`, `LoginCommand`, `DriverRegistrationDto`, `OwnerRegistrationDto`.
  - Invariant: `VerifyAsync` executes strictly at the start of each flow before any database transaction or identity side effect.
  - Layered Defense: Separate rate limiting policies `RegistrationRateLimit` and `LoginRateLimit` (10 req/min/IP) configured with `UseForwardedHeaders` on `AuthController`.
- **Phase 3 — Environment & Docker Configuration:**
  - Multi-environment templates added to `.env.example`, `FE/.env.example`, `compose.app.yml`, `appsettings.json`, and `scripts/run-schema-service.ps1`.
  - Zero committed secrets or credentials.
- **Phase 4 — Frontend Widget & Lifecycle Management:**
  - Types declared in `FE/src/vite-env.d.ts`.
  - Reusable hook `useTurnstile.ts` and component `TurnstileWidget.tsx` with imperative reset.
  - Integrated into `SignIn.tsx` (`password_login`) and `SignUp.tsx` (`driver_register`, `owner_register`).
  - Strict Token Lifecycle: Retain token on pre-submit client validation errors and 429 rate limit; reset widget on server validation, auth failure, bot verification rejection, or network/503 errors.
- **Phase 5 — Backend Integration Testing & Pipeline Hardening:**
  - `BotProtectionIntegrationTests.cs` (35 test cases) covering all Scenarios 1–18.
  - Stage 2 Defense: Request body size limit enforced at 64KB (`[RequestSizeLimit(65536)]` and early pipeline middleware returning HTTP 413 `PAYLOAD_TOO_LARGE`).
- **Phase 6 — Frontend Lifecycle Testing & Test Reporting:**
  - `turnstile-auth-lifecycle.test.cjs` (15 test cases) covering Scenarios 19–27.
  - Test Plan (`SPARK-257-bot-protection-test-plan.md`) and Test Report (`bot-protection-test-report.md`) published.

**Key Commits on branch (latest first):**
- `ca7573f3` `test(auth): add automated test plan, backend integration and frontend lifecycle tests [SPARK-257]`
- `fe86f608` `feat(fe-auth): integrate turnstile challenge widget and token lifecycle [SPARK-257]`
- `a9d7c54e` `chore(config): add turnstile multi-environment templates and compose wiring [SPARK-257]`
- `270754ec` `feat(auth): wire bot verification into registration, login flows and rate limiting [SPARK-257]`
- `18472a5b` `feat(auth): implement bot verification port, adapter and error mapping [SPARK-257]`

---

## 2. Prerequisites & Environment Setup

### 2.1 Docker Services Setup
Start the required User Service and Frontend application via Docker Compose:

```powershell
docker compose -f compose.app.yml up -d user-service frontend
```

*Note: If running in standalone developer mode, start the backend via `scripts/run-schema-service.ps1 -Service User` and frontend via `npm run dev --prefix FE`.*

### 2.2 Service URLs & Ports
- **UserService API:** `http://localhost:5035`
- **Frontend Web Application:** `http://localhost:5173`
- **Health Check Endpoint:** `http://localhost:5035/health`

### 2.3 Environment Configuration (.env & FE/.env)
Ensure the environment files contain valid dummy test keys or live Cloudflare credentials:
- **Root `.env` (UserService):**
  ```bash
  SMARTPARK_TURNSTILE_ENABLED=true
  SMARTPARK_TURNSTILE_SECRET_KEY=1x0000000000000000000000000000000AA
  SMARTPARK_TURNSTILE_EXPECTED_HOSTNAMES=localhost,127.0.0.1,::1
  SMARTPARK_USER_CORS_ORIGINS=http://localhost:5173;http://127.0.0.1:5173
  ```
- **Frontend `FE/.env`:**
  ```bash
  VITE_TURNSTILE_SITE_KEY=1x0000000000000000000000000000000AA
  ```
> **Security Note:** `1x0000000000000000000000000000000AA` is Cloudflare's official testing key (always passes). Never commit real production secret keys to version control.

### 2.4 Seed Accounts
- **Demo Driver:** `driver@gmail.com` / `Password@123`

---

## 3. Automated Verification (Must All Be Green)

Run the following commands from the repository root:

| # | Command | Expected Result |
|---|---|---|
| **A1** | `dotnet build SmartParking.slnx --nologo -v q` | `0 Warning(s), 0 Error(s)` |
| **A2** | `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --filter "FullyQualifiedName~BotProtection" --no-build --nologo` | `Passed! 45 passed, 0 failed` |
| **A3** | `npm run build --prefix FE` | `✓ built in ~2-3s, exit code 0` |
| **A4** | `node --test FE/tests/turnstile-auth-lifecycle.test.cjs` | `15 pass, 0 fail` |
| **A5** | Newman Postman run (see Section 5) | `12 requests, 33 assertions, 0 failed` |

---

## 4. Manual Verification — Screens (Tester Opens Browser)

### 4.1. Sign-In Screen (`http://localhost:5173/auth/login`)
- [ ] Open `http://localhost:5173/auth/login`. Verify that the Cloudflare Turnstile challenge box renders cleanly directly below the Password input field.
- [ ] **Client Validation Retention Test:** Leave the Password field blank and click **Sign In**.
  - Form displays inline validation error: *"Password is required"*.
  - **The Turnstile widget is NOT reset** (checkbox/state remains intact without triggering re-verification).
- [ ] **Auth Failure Reset Test:** Enter `driver@gmail.com` and an incorrect password `WrongPassword!999`, then click **Sign In**.
  - Server returns HTTP 401 `AUTH_FAILED`.
  - **The Turnstile widget automatically resets** (spinner spins briefly to acquire a fresh token).
- [ ] **Happy Path Login Test:** Enter `driver@gmail.com` and `Password@123`, then click **Sign In**.
  - Login succeeds with HTTP 200.
  - User is navigated directly to the Driver Dashboard.

### 4.2. Sign-Up Screen (`http://localhost:5173/auth/register`)
- [ ] Open `http://localhost:5173/auth/register`.
- [ ] **Tab Driver Verification:**
  - Ensure the Turnstile widget is rendered with action `driver_register`.
  - Enter test data. On local client validation error (e.g. invalid phone format), the widget token is preserved.
- [ ] **Tab Owner Verification:**
  - Switch to the **Owner** tab.
  - Ensure the Turnstile widget renders with action `owner_register`.
- [ ] **Tab Switching Isolation:**
  - Switch back and forth between Driver and Owner tabs. Confirm that widget state and token are properly isolated and reset per form.

### 4.3. DevTools Network & Console Audit (F12)
- [ ] Open DevTools **Network** tab (Filter: `Fetch/XHR`).
- [ ] Inspect the payload of `POST /api/auth/login`:
  - Request body contains: `"turnstileToken": "..."`.
  - Response status is 200 OK with JWT token.
- [ ] **Secret Leak Audit:**
  - Search Network payloads and Console logs for `SecretKey`.
  - Verify that neither Cloudflare SecretKey nor raw internal tokens are ever logged or exposed to the client.

---

## 5. Postman / Newman Verification

An automated verification suite covering all 3 public auth flows and rejection rules is located in `docs/04-testing/postman/`.

### 5.1 One-Line Newman Execution Command
Run the following PowerShell command from the repository root:

```powershell
npx.cmd --yes --cache .cache/npm --package newman newman run `
  "docs/04-testing/postman/SPARK-257-Turnstile-Bot-Protection.postman_collection.json" `
  -e "docs/04-testing/postman/SPARK-257.postman_environment.example.json"
```

### 5.2 Structure & Assertions Breakdown

| Folder | Request | Method & Path | Assertions | Expected Status & Code |
|---|---|---|:---:|---|
| **01 - Happy Path** | 1.1 Login with Valid Token | `POST /api/auth/login` | 4 | `200 OK` (JWT returned) |
| | 1.2 Register Driver with Valid Token | `POST /api/auth/register/driver` | 2 | `201 Created` |
| | 1.3 Register Owner with Valid Token | `POST /api/auth/register/owner` | 2 | `201 Created` |
| **02 - Bot Rejections** | 2.1 Reject Missing Token | `POST /api/auth/login` | 3 | `400 BOT_VERIFICATION_REQUIRED` |
| | 2.2 Reject Whitespace Token | `POST /api/auth/login` | 3 | `400 BOT_VERIFICATION_REQUIRED` |
| | 2.3 Reject Invalid Token | `POST /api/auth/login` | 3 | `400 BOT_VERIFICATION_FAILED` |
| | 2.4 Reject Expired/Duplicate Token | `POST /api/auth/login` | 3 | `400 BOT_VERIFICATION_EXPIRED` |
| | 2.5 Reject Token > 2048 Chars | `POST /api/auth/login` | 3 | `400 BOT_VERIFICATION_FAILED` |
| | 2.6 Reject Action Mismatch | `POST /api/auth/register/driver` | 3 | `400 BOT_VERIFICATION_FAILED` |
| | 2.7 Reject Payload > 64KB | `POST /api/auth/login` | 2 | `413 PAYLOAD_TOO_LARGE` |
| **03 - Rate Limiting** | 3.1 Burst Consumption of Quota | `POST /api/auth/login` | 2 | Completed burst |
| | 3.2 Rate Limit Exceeded | `POST /api/auth/login` | 3 | `429 Too Many Requests` |

**Total Expected Summary:** `12 requests, 33 assertions, 0 failed`.

---

## 6. Acceptance Criteria Checklist (PM Sign-Off)

| AC | Acceptance Criteria Description | Verification Evidence | Status |
|:---:|---|---|:---:|
| **AC1** | **Mandatory Bot Verification:** All 3 public endpoints (`/login`, `/register/driver`, `/register/owner`) reject missing/whitespace tokens with `400 BOT_VERIFICATION_REQUIRED`. | Unit Tests (`BotProtectionFlowTests`), Integration Tests (Scenarios 1–3), Newman Tests 1.1–1.3, 2.1–2.2 | ✅ PASSED |
| **AC2** | **Provider Error Handling:** Cloudflare token failure (`invalid-input-response` $\to$ `400 BOT_VERIFICATION_FAILED`; `timeout-or-duplicate` $\to$ `400 BOT_VERIFICATION_EXPIRED`). | Unit Tests (`TurnstileVerifierTests`), Integration Tests (Scenarios 5–6), Newman Tests 2.3–2.4 | ✅ PASSED |
| **AC3** | **Action & Hostname Binding:** Token action bound to flow (`password_login`, `driver_register`, `owner_register`); hostname validated against allowlist. Token is single-use; frontend resets on backend error. | Unit Tests, Integration Tests (Scenarios 7–8), Frontend Tests (Scenarios 19–27), Manual UI (§4.1) | ✅ PASSED |
| **AC4** | **Fail-Closed Provider Fault:** Provider timeout (>5s), network error, HTTP 429/500/502/503, or malformed JSON yields `503 BOT_VERIFICATION_UNAVAILABLE`. | Unit Tests, Integration Tests (Scenarios 10, 12, 13, 14) | ✅ PASSED |
| **AC5** | **Layered Defense & Payload Bounds:** Rate limiting (10 req/min/IP) yields 429 before bot verification. Request payloads exceeding 64KB rejected with `413 PAYLOAD_TOO_LARGE`. | Integration Tests (Scenarios 9, 15), Newman Tests 2.7, 3.1–3.2 | ✅ PASSED |
| **AC6** | **Security & Privacy Audit:** Cloudflare SecretKey and client tokens are never logged; startup guard prevents deployment with disabled/insecure configuration in Production. | Unit Tests (`TurnstileVerifierTests`), Integration Tests (Scenario 16), Code Audit | ✅ PASSED |

---

## 7. Known Limitations & Boundary Conditions (QA / Tester Troubleshooting Guide)

### 7.1. Cloudflare Live Secret Key vs. Dummy Test Tokens
1. **Behavior with Real Cloudflare Keys:**
   - In live browser testing, the client communicates with `challenges.cloudflare.com`, solves an interactive challenge, and generates a cryptographically signed token bound to `action` (`password_login`, `driver_register`, `owner_register`) and `hostname` (`localhost`). This token passes live verification 100%.
   - In Newman / Postman, requests send the static dummy token `1x0000000000000000000000000000000AA`. When `user-service` is running with a **real Cloudflare Secret Key** (`0x4...`), Cloudflare's server strictly rejects the dummy token as `invalid-input-response`, returning `400 BOT_VERIFICATION_FAILED`.
   - **Security Invariant:** This is intended security behavior. If backend accepted dummy tokens while running in live production mode with real keys, bots could bypass verification by simply sending dummy strings.
2. **How to Run Postman Happy Path (1.1, 1.2, 1.3) Successfully:**
   - **Option A (Manual Token Injection - Recommended for Demos):** Open browser at `http://localhost:5173/auth/login`, solve the captcha, open DevTools F12 Network tab, copy the generated `turnstileToken`, and paste it into Postman's `dummyPassToken` environment variable $\to$ Returns `200 OK` / `201 Created`.
   - **Option B (Automated Testing Mode):** Set `SMARTPARK_TURNSTILE_SECRET_KEY=1x0000000000000000000000000000000AA` in `.env` and restart `user-service`.
   - **Option C (Functional Dev Bypass):** Set `SMARTPARK_TURNSTILE_ENABLED=false` in `.env` to bypass bot verification for pure business logic testing.

### 7.2. Rate Limiting Cooldown & IP Window (10 req/min/IP)
- The rate limiter enforces 10 requests per minute per IP on `/api/auth/login` and `/api/auth/register/*`.
- **Folder 03 (3.1 Burst Consumption)** deliberately fires 10 rapid requests to verify that the 11th request is rejected with `429 Too Many Requests`.
- **Side Effect:** Once Folder 03 executes, the IP `127.0.0.1` has exhausted its quota for the current 60-second window. Any subsequent Newman run or manual login test performed within 60 seconds will immediately receive `429 Too Many Requests`.
- **Resolution for Testers:**
  - Wait 60 seconds for the fixed window to expire.
  - OR restart the container to reset in-memory rate limit counters immediately:
    ```powershell
    docker restart smartpark-app-user-service-1
    ```

### 7.3. DTO Schema Discipline (Unmapped Members Rejection)
- Both `DriverRegistrationDto` and `OwnerRegistrationDto` are decorated with `[JsonUnmappedMemberHandling(Disallow)]`.
- Submitting unknown properties (such as legacy fields `vehiclePlate`, `vehicleType`, `lotAddress`, `totalSpots`, etc.) causes ASP.NET Core model validation to reject the request with `400 VALIDATION_FAILED` before bot verification executes. All Postman request bodies must strictly follow the DTO properties defined in `src/Services/UserService/SmartParking.UserService.Application/DTOs/`.

### 7.4. Single-Use Token Enforcement
- Every token consumed by `VerifyAsync` is marked spent by Cloudflare. Any subsequent submission with the same token returns `400 BOT_VERIFICATION_EXPIRED` or `BOT_VERIFICATION_FAILED`. The frontend automatically resets the challenge widget upon server response via `turnstile.reset()`.

---

## 8. Sign-off

- [ ] **Tester Confirmation:** All automated checks in Section 3 are GREEN (`0 Warnings, 0 Errors`, 45 .NET tests, 15 Node tests, FE build clean).
- [ ] **Manual UI Confirmation:** Sign-In and Sign-Up screen verifications in Section 4 pass as expected.
- [ ] **Newman / Postman Confirmation:** Newman automated run passes 100% assertions (`12 requests, 33 assertions, 0 failed`).
- [ ] **PM Approval:** PM approves merging branch `docs/SPARK-257-bot-protection-api-design` into `develop`.

---

**Branch PR:** [docs/SPARK-257-bot-protection-api-design](https://github.com/Quangthm/Mock-Project-Smart-Parking/tree/docs/SPARK-257-bot-protection-api-design)
