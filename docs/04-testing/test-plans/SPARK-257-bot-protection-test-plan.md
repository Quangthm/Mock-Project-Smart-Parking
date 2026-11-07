# Test Plan: SPARK-257 — Public Authentication Bot Protection (Cloudflare Turnstile)

| Metadata | Value |
|---|---|
| **Document ID** | `TP-SPARK-257-v1.0` |
| **Jira Task** | **SPARK-257** — `[SEC-AUTH] Protect public authentication flows from automated abuse` |
| **Parent Epic** | Identity & Access Hardening |
| **Status** | **APPROVED — EXECUTION READY** |
| **Author** | Test Automation & Quality Assurance Agent |
| **Created / Updated** | 2026-11-06 |
| **Reference Architecture** | `docs/03-design/api/bot-protection-spark-257-api-design.md` (v1.2.1 APPROVED) |
| **Planning Document** | `SPARK-257-planning.md` (Section 10: 30 Test Scenarios) |
| **Coding Skill** | `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` |

---

## 1. Executive Summary & Objectives

### 1.1 Objectives
This test plan defines the testing strategy, test scenarios, execution environment, test data, and acceptance criteria for **SPARK-257**. The primary objective is to verify that public authentication endpoints in `UserService` (`/api/auth/register/driver`, `/api/auth/register/owner`, `/api/auth/login`) are strictly protected against automated credential stuffing, bot scraping, and registration flooding using Cloudflare Turnstile bot verification while ensuring zero regression to valid human users.

### 1.2 Verification Goals
1. **Server-Side Verification Gate:** Ensure `TurnstileVerifier` calls Cloudflare `siteverify` before any business logic or database transaction runs.
2. **Fail-Closed Resilience:** Confirm that network errors, timeouts (>5s), or provider outages return `503 BOT_VERIFICATION_UNAVAILABLE` and do not bypass bot defense.
3. **Strict Flow & Hostname Binding:** Ensure tokens minted for one action (e.g. `driver_register`) or unapproved hostnames are rejected with `400 BOT_VERIFICATION_FAILED`.
4. **Token Lifecycle on Frontend:** Verify that the frontend challenge widget preserves tokens on client-side validation errors or HTTP 429 rate limits, while resetting tokens upon server consumption, business errors, or authentication failures.
5. **Security & Privacy Isolation:** Assert that secrets (`SecretKey`) and tokens never leak into logs, client bundles, or error envelopes.

---

## 2. Scope of Testing

### 2.1 In-Scope Endpoints & Components
| Component | Route / Target | Protocol / Type |
|---|---|---|
| **Driver Registration** | `POST /api/auth/register/driver` | HTTP Pipeline / Integration |
| **Owner Registration** | `POST /api/auth/register/owner` | HTTP Pipeline / Integration |
| **Password Login** | `POST /api/auth/login` | HTTP Pipeline / Integration |
| **Turnstile Adapter** | `TurnstileVerifier.cs` | Infrastructure Unit & Integration |
| **Startup Guard** | `UserHost.cs` (`Program.cs`) | Host Configuration & Validation |
| **Rate Limiting** | `RegistrationRateLimit`, `LoginRateLimit` | ASP.NET Core Middleware |
| **Frontend Hook** | `FE/src/hooks/useTurnstile.ts` | React Hook Lifecycle |
| **Frontend Widget** | `FE/src/components/common/TurnstileWidget.tsx` | UI Challenge Component |
| **Sign-In Page** | `FE/src/roles/authentications/Sign In/SignIn.tsx` | React Component Lifecycle |
| **Sign-Up Page** | `FE/src/roles/authentications/Sign Up/SignUp.tsx` | React Component Lifecycle |

### 2.2 Out-of-Scope Items
- Password recovery / forgot-password flow (deferred until backend endpoint exists).
- Authenticated endpoints (`/api/auth/refresh`, `/api/auth/logout`, `/api/auth/me`).
- Cloudflare Enterprise Bot Management or WAF rule modifications.

---

## 3. Test Environments & Tooling

### 3.1 Environments
1. **In-Memory Integration TestHost (`Microsoft.AspNetCore.TestHost`):**
   - High-speed, isolated execution of ASP.NET Core middleware, rate limiters, filters, controllers, and services.
   - Mocked outbound HTTP via `MockHttpMessageHandler` to simulate Cloudflare `siteverify` responses (success, expired, invalid, timeouts, HTTP 5xx, malformed JSON).
2. **Node.js Test Runner (`node:test` + `node:assert/strict`):**
   - Frontend lifecycle contract tests transpiled via TypeScript compiler API.
   - DOM / Component mocks verifying widget mounting, token retention, imperative reset, and event handling.
3. **Docker Multi-Container Integration (Live Smoke):**
   - Live `user-service` container in `compose.app.yml` configured with Turnstile environment variables.

### 3.2 Tooling & Execution Commands
- **Backend Tests:**
  ```powershell
  dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --filter "FullyQualifiedName~BotProtection" --no-build --nologo
  ```
- **Frontend Tests:**
  ```powershell
  node --test FE/tests/turnstile-auth-lifecycle.test.cjs
  ```

---

## 4. Test Data & Cloudflare Dummy Keys

Cloudflare provides standard deterministic testing keys used across unit and integration tests:

| Key Type | Key Value | Expected Cloudflare Behavior |
|---|---|---|
| **Secret Key (Pass)** | `1x0000000000000000000000000000000AA` | Always passes (`success: true`) |
| **Secret Key (Fail)** | `2x0000000000000000000000000000000AA` | Always fails (`invalid-input-response`) |
| **Secret Key (Duplicate)** | `3x0000000000000000000000000000000AA` | Token already spent (`timeout-or-duplicate`) |
| **Site Key (Always Pass)** | `1x00000000000000000000AA` | Widget passes immediately without user interaction |
| **Site Key (Always Fail)** | `2x00000000000000000000AA` | Widget fails immediately with client challenge error |
| **Site Key (Interactive)** | `3x00000000000000000000AA` | Widget presents interactive checkbox / puzzle challenge |

---

## 5. Traceability Matrix & Acceptance Criteria (AC1–AC6)

| AC ID | Acceptance Criteria Summary | Verification Method |
|---|---|---|
| **AC1** | Valid token allows request to proceed; server verification occurs before business operations. | Integration Tests (Scenarios 1, 20) |
| **AC2** | Missing, blank, oversized, or invalid tokens are rejected with appropriate 400 error codes. | Integration Tests (Scenarios 2, 3, 4, 5, 7, 8) |
| **AC3** | Expired/consumed tokens return 400 `BOT_VERIFICATION_EXPIRED`; frontend prompts/resets widget. | Backend Test (Scenario 6) & Frontend Tests (Scenarios 23, 24, 25, 26) |
| **AC4** | Secret keys and tokens never leak in bundles, logs, or responses; startup guard validates config. | Audit & Backend Tests (Scenarios 16, 17, 18, 19, 25, 26) |
| **AC5** | Outbound provider failures (HTTP errors, timeouts, malformed JSON) fail closed with 503. | Backend Tests (Scenarios 10, 12, 13, 14) |
| **AC6** | Automated test suite passes 100% across backend integration and frontend lifecycle suites. | End-to-End Suite Execution |

---

## 6. Master 30 Test Scenarios Matrix

### Nhóm 1: Backend Integration & HTTP Pipeline (Phase 5)

| # | Scenario Title | Severity | Input / Precondition | Expected Behavior & Status | AC / Target |
|---|---|---|---|---|---|
| **01** | **Valid Token Flow** | Critical | Valid token matching action and hostname | `VerifyAsync` completes $\to$ 200 OK / 201 Created $\to$ business logic executed. | AC1 |
| **02** | **Missing Token** | Critical | Request without `turnstileToken` or null | 400 `BOT_VERIFICATION_REQUIRED` (Cloudflare is NOT called). | AC2 |
| **03** | **Whitespace-Only Token** | Critical | `turnstileToken: "   "` | 400 `BOT_VERIFICATION_REQUIRED` (Cloudflare is NOT called). | AC2 |
| **04** | **Token Length > 2048 Chars** | Critical | `turnstileToken` length = 2049 chars | 400 `BOT_VERIFICATION_FAILED` (rejected before outbound call; does not throw `VALIDATION_FAILED`). | AC2 |
| **05** | **Invalid Token Format** | Critical | Malformed token; Cloudflare returns `invalid-input-response` | 400 `BOT_VERIFICATION_FAILED`. | AC2 |
| **06** | **Expired / Consumed Token** | Critical | Reused/expired token; Cloudflare returns `timeout-or-duplicate` | 400 `BOT_VERIFICATION_EXPIRED`. | AC3 |
| **07** | **Action Mismatch** | Critical | Token minted for `driver_register` sent to `password_login` | 400 `BOT_VERIFICATION_FAILED` (flow binding enforcement). | AC2 |
| **08** | **Hostname Mismatch** | Critical | Token validated for hostname not in `ExpectedHostnames` | 400 `BOT_VERIFICATION_FAILED` (allowlist enforcement). | AC2 |
| **09** | **Rate Limiting Enforcement** | Critical | 11 requests in 1 minute window | Request 1..10 pass; Request 11 returns 429 Too Many Requests (Registration & Login quotas isolated). | Layered Defense |
| **10** | **Provider Timeout (> 5s)** | Critical | Cloudflare outbound call takes > 5 seconds | 503 `BOT_VERIFICATION_UNAVAILABLE` (fail-closed; operation aborted). | AC5 |
| **11** | **Client Cancellation** | Critical | Client aborts request (`ct.Cancel()`) during verification | `OperationCanceledException` propagated cleanly; does NOT return 503. | Resilience |
| **12** | **Provider HTTP Non-2xx** | High | Cloudflare returns HTTP 429, 400, 403, 500, 502, 503 | 503 `BOT_VERIFICATION_UNAVAILABLE`. | AC5 |
| **13** | **Provider Malformed JSON** | High | Cloudflare returns invalid JSON or HTML error body | 503 `BOT_VERIFICATION_UNAVAILABLE`. | AC5 |
| **14** | **Provider Infrastructure Error** | High | Cloudflare returns `missing-input-response` or `bad-request` | 503 `BOT_VERIFICATION_UNAVAILABLE` (treated as infra failure, not user error). | AC5 |
| **15** | **Request Body Size Limit** | Medium | Request body payload > 64KB | 413 Payload Too Large / Bad Request rejection. | Security |
| **16** | **Log Sanitization** | Critical | Outbound and verification error logging | Assertion that `SecretKey` and `turnstileToken` NEVER appear in log text. | AC4 |
| **17** | **DB Transaction Safety** | Critical | Verification sequence in handlers/services | Assertion that `VerifyAsync` executes strictly before `BeginTransactionAsync`. | AC1 |
| **18** | **Bypass SignInErrors** | Critical | Login endpoint fails bot verification | Returns exact 400/503 bot error code; never coerced into 401 `AUTH_FAILED`. | Contract |

### Nhóm 2: Frontend Contract & Token Lifecycle (Phase 6)

| # | Scenario Title | Severity | Input / Precondition | Expected Behavior & Status | AC / Target |
|---|---|---|---|---|---|
| **19** | **Widget Render & Action Binding** | High | Mount SignIn and SignUp forms | SignIn renders with `action="password_login"`; SignUp renders Driver with `driver_register` and Owner with `owner_register`. | P6 |
| **20** | **Payload Attachment** | High | Submit valid form with token | `turnstileToken` string field attached to `authApi.login`, `registerDriver`, and `ownerApi.register`. | AC1 |
| **21** | **Pre-submit Client Validation** | High | Submit with missing/invalid form fields (empty password, etc.) | Validation error displayed; **TOKEN IS PRESERVED; WIDGET IS NOT RESET**. | AC3 |
| **22** | **Rate Limit 429 Retention** | Medium | API returns HTTP 429 Too Many Requests | Rate limit message displayed; **WIDGET IS NOT RESET**; token retained for cooldown retry. | AC3 |
| **23** | **Business Validation (400) Reset** | High | Server returns 400 `VALIDATION_FAILED` | Token was consumed by server; **WIDGET MUST BE RESET** to allow fresh token generation. | AC3 |
| **24** | **Auth Failure (401/403) Reset** | High | Server returns 401 `AUTH_FAILED` or 403 `ACCOUNT_INACTIVE` | Error displayed; **WIDGET MUST BE RESET**. | AC3 |
| **25** | **Bot Verification Failure Reset** | High | Server returns 400 `BOT_VERIFICATION_FAILED` or 503 | Error displayed; **WIDGET MUST BE RESET**. | AC3 |
| **26** | **Expired Callback Handling** | High | Turnstile widget fires `expired-callback` | Form state clears `turnstileToken` to null; submit disabled or prompts fresh solve. | AC3 |
| **27** | **Tab Switching Isolation** | Medium | User switches between Driver and Owner tabs in `SignUp.tsx` | Tokens and widget instances remain completely isolated per tab. | P6 |

### Nhóm 3: Cross-Cutting Hardening & Configuration Scenarios

| # | Scenario Title | Severity | Input / Precondition | Expected Behavior & Status | AC / Target |
|---|---|---|---|---|---|
| **28** | **Shared Registration Quota** | Medium | 6 driver registrations + 5 owner registrations from same IP | 11th request receives 429 Too Many Requests under shared quota. | Layered Defense |
| **29** | **Disabled Mode (Dev/Test)** | Medium | `Turnstile:Enabled = false` in Development/Testing | Verification is bypassed; requests without tokens succeed. | Dev Experience |
| **30** | **Startup Guard Enforcement** | Critical | Missing secret or invalid hostnames in Staging/Production | Application crashes at startup (`InvalidOperationException`). | AC4 |

---

## 7. Pass / Fail Criteria

- **Pass Criteria:**
  1. 100% of the 30 defined test scenarios pass without error.
  2. Zero secrets or raw tokens appear in test outputs or logs.
  3. No regressions in existing UserService unit, integration, or layer boundary tests.
- **Fail Criteria:**
  1. Any bot verification bypass when enabled.
  2. Any 500 Internal Server Error returned for expected bot rejection scenarios.
  3. Premature token reset on client-side validation errors.
  4. Failure to reset token after server validation failure or authentication failure.

---

## 8. Test Execution Notes & Operational Troubleshooting

1. **Cloudflare Real Key vs. Dummy Token Dynamics:**
   - When `user-service` runs with live Cloudflare credentials (`0x4...`), sending Cloudflare dummy test tokens (`1x...`, `3x...`) via Postman/Newman results in `400 BOT_VERIFICATION_FAILED`. Cloudflare treats dummy test tokens as `invalid-input-response` unless paired with Cloudflare test secret keys (`1x...`).
   - Browser testing against `challenges.cloudflare.com` generates genuine signed tokens bound to `action` and `hostname`, completing Happy Path flows with 200/201.
   - For automated headless API verification with dummy tokens, configure `SMARTPARK_TURNSTILE_SECRET_KEY=1x0000000000000000000000000000000AA` or bypass verification via `SMARTPARK_TURNSTILE_ENABLED=false`.
2. **Rate Limiting 60s Window (10 req/min/IP):**
   - The rate limit burst test (Scenario 09 / Postman Folder 03) exhausts the 10-request permit limit for `127.0.0.1`.
   - Subsequent automated or manual login requests within the same 60-second window will be rejected with HTTP 429 (`TOO_MANY_REQUESTS`).
   - Reset the limiter by waiting 60 seconds or executing `docker restart smartpark-app-user-service-1`.
3. **DTO Strict Unmapped Member Policy:**
   - `DriverRegistrationDto` and `OwnerRegistrationDto` enforce `[JsonUnmappedMemberHandling(Disallow)]`.
   - Payloads containing undeclared fields (e.g. `vehiclePlate`, `lotAddress`) trigger `400 VALIDATION_FAILED` before reaching bot verification. Always adhere strictly to declared DTO contract fields.
