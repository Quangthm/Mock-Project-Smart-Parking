# Public Auth Bot Protection (Cloudflare Turnstile) — API Design

| Metadata | Value |
| --- | --- |
| Status | **APPROVED — IMPLEMENTATION READY** — final contract with complete security, resilience, and operational hardening |
| Contract version / date | `v1.2.1` / 2026-11-04 |
| Owning service / author | UserService / SPARK-257 (finalized & approved) |
| Jira design / implementation / verification | SPARK-257 (design approved); related SPARK-234, SPARK-235; implementation **READY**; verification **NOT RUN** |
| Requirements baseline | SRS v1.1 — [identity & access](../../01-requirements/srs/modules/01-identity-access.md), [authentication/recovery](../../01-requirements/srs/authentication-recovery.md); SPARK-257 decision (Turnstile MVP) |
| Implementation | **NOT IMPLEMENTED** (contract approved; implementation pending in next phase) |

## Overview

Cross-cutting, additive bot-verification guard for the **public, anonymous authentication flows** of UserService. The frontend renders a Cloudflare Turnstile challenge and submits the resulting single-use token with the request; UserService validates the token **server-side** through a reusable Application abstraction before executing any protected operation.

Bot verification is an **additional** anti-automation control. It does **not** replace authentication, authorization, HTTP rate limiting, account lockout, OTP attempt limits or MFA. Provider or network failures **fail closed** (the protected operation does not execute).

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | SPARK-257 AC1–AC6; SRS v1.1 identity & access; authentication/recovery pre-login abuse controls |
| Permission / service ownership | UserService owns all three public auth operations; Turnstile is an outbound dependency only |
| Approved decisions | Cloudflare Turnstile as MVP provider (SPARK-257); protected scope locked to the three operations below; per-environment enable/disable; fail-closed on provider failure; OQ-1..OQ-7 and P0-01..P1-06 resolved |

### Scope of this contract

| In scope (protected) | Out of scope (unchanged this round) |
| --- | --- |
| `POST /api/auth/register/driver` | Forgot-password / password-recovery request — **deferred**; no backend endpoint exists yet |
| `POST /api/auth/register/owner` | OTP verify/resend, OTP login, MFA login, refresh, logout, `/auth/me` |
| `POST /api/auth/login` (password login) | Any authenticated API request, internal service-to-service traffic, WAF/DDoS configuration |
| | Google reCAPTCHA, Cloudflare Enterprise Bot Management |

> **Deferred item.** SPARK-257 lists the forgot-password request as an initial flow, but this repository has **no** forgot/reset endpoint (the current FE forgot-password UI is a client-side mock). It is explicitly deferred until a real backend endpoint exists; see [Decisions and open questions status](#decisions-and-open-questions-status) (DQ-1).

---

## Defense In Depth: Layered Processing Order (P0-03, P1-03)

To prevent resource exhaustion attacks and unwanted outbound HTTP load on Cloudflare, request processing strictly follows this five-stage pipeline:

```
Incoming Request
  │
  ▼ [1. ASP.NET Core Rate Limiting Middleware]
     ├── Limit Exceeded ──► 429 Too Many Requests (Terminates immediately; no downstream code executed)
  │
  ▼ [2. Framework Syntax & Size Validation]
     ├── Request Body > 64KB ──► 413 Payload Too Large
     ├── Malformed JSON ────────► 400 Bad Request / VALIDATION_FAILED
  │
  ▼ [3. Bot Verification Guard (IBotVerificationService)]
     ├── Missing / Whitespace Token ───────────────► 400 BOT_VERIFICATION_REQUIRED (No Cloudflare call)
     ├── Length > 2048 / Invalid / Action Mismatch ─► 400 BOT_VERIFICATION_FAILED
     ├── Expired / Consumed (timeout-or-duplicate) ──► 400 BOT_VERIFICATION_EXPIRED
     ├── Timeout / 5xx / Provider Communication ───► 503 BOT_VERIFICATION_UNAVAILABLE
  │
  ▼ [4. Authentication & Business Evaluation]
     ├── Invalid Credentials / Account Inactive ──► 401 / 403 (Evaluated only after valid bot proof)
  │
  ▼ [5. Database Transaction & Side Effects]
     └── Commit Account / Issue Challenge / Create Session ──► 200 / 201 Success
```

### Rate Limiting Policy & IP Trust Model (P0-03, P0-04, P1-03, P1-05)

1. **Thresholds & Window:**
   - `RegistrationRateLimit`: 10 requests per 1-minute fixed window per IP partition, `QueueLimit = 0`. Applied to `POST /api/auth/register/driver` and `POST /api/auth/register/owner`.
   - `LoginRateLimit`: 10 requests per 1-minute fixed window per IP partition, `QueueLimit = 0`. Applied to `POST /api/auth/login`.
   - **Shared Registration Quota (P0-04):** Driver and Owner registration share a combined `RegistrationRateLimit` quota (10 requests/min per IP partition) to constrain overall public registration volume. Login has an isolated `LoginRateLimit` quota partition, preventing login traffic from starving registration or vice versa.
   - **Fixed Window Boundary Trait (P1-03):** The standard ASP.NET Core `FixedWindowLimiter` resets counters at fixed minute boundaries. Requests concentrated across a minute boundary (e.g. 10 at 12:00:59 and 10 at 12:01:00) may yield a transient burst; this is an accepted characteristic for the MVP.
2. **IP Trust Model & Reverse Proxy Forwarded Headers (P0-03, P1-05):**
   - In `Program.cs`, `app.UseForwardedHeaders()` must run **before** `app.UseRouting()` and `app.UseRateLimiter()`.
   - `ForwardedHeadersOptions` must restrict `KnownNetworks` / `KnownProxies` to controlled reverse proxy / Docker network CIDRs (e.g. `172.16.0.0/12` or container gateway). Spoofed `X-Forwarded-For` headers from untrusted clients are stripped.
   - **Fallback Policy (P0-03):** If `RemoteIpAddress` is unresolvable (null), the partition key defaults to `"unknown"`. Clients falling back to `"unknown"` pool into a shared 10 req/min quota; this is an accepted MVP trade-off for unroutable callers.
3. **Multi-Instance Scale-out:**
   - In-memory ASP.NET Core RateLimiter provides local rate limiting per UserService instance for the MVP. Distributed rate limiting (e.g. Redis-backed) is tracked as a future scale-out enhancement.

---

## Shared bot-verification contract

### Transport

The token is carried as a JSON body field named **`turnstileToken`**, alongside each endpoint's existing fields. It is requested by the frontend from the Turnstile widget and is single-use and short-lived.

- Existing fields and their validation are **unchanged**; `turnstileToken` is purely additive.
- In DTOs (`DriverRegistrationDto`, `OwnerRegistrationDto`, `LoginDto`, `LoginCommand`), `TurnstileToken` is declared as `public string? TurnstileToken { get; set; }`.
- `DriverRegistrationDto` and `OwnerRegistrationDto` disallow unmapped members (`JsonUnmappedMemberHandling.Disallow`), so `TurnstileToken` must be explicitly declared on the DTO.
- **No DataAnnotation Validation on Token (P0-01):** `TurnstileToken` must NOT have `[Required]` or `[StringLength]` attributes on the DTO. All presence, whitespace, and length checks ($\le 2048$ chars) are performed inside `IBotVerificationService` so that bot-specific error codes (`BOT_VERIFICATION_REQUIRED`, `BOT_VERIFICATION_FAILED`) are returned instead of the generic `VALIDATION_FAILED`.
- The public widget site key is delivered to the browser (`VITE_TURNSTILE_SITE_KEY`); the Turnstile **secret key is backend-only** (`Turnstile:SecretKey`) and never appears in the frontend bundle, source, documentation or logs.

### Server-side validation (reusable Application abstraction)

Validation is owned by an Application-layer port and implemented by an Infrastructure adapter; controllers and handlers never contain provider-specific HTTP logic.

1. **Port (Application):**
   ```csharp
   public interface IBotVerificationService
   {
       Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null);
   }
   ```
2. **Adapter (Infrastructure):**
   - Posts `secret`, `response` (token), optional `remoteip`, and optional `idempotency_key` to Cloudflare `siteverify` endpoint using `FormUrlEncodedContent`.
   - *Protocol Note:* Cloudflare `siteverify` **does not receive `action` in the POST body**. The widget passes `action` to Cloudflare on the client side, and Cloudflare returns `action` in its response JSON.
   - Cloudflare response model uses explicit property naming:
     ```csharp
     public sealed class TurnstileVerifyResponse
     {
         [JsonPropertyName("success")] public bool Success { get; set; }
         [JsonPropertyName("challenge_ts")] public string? ChallengeTs { get; set; }
         [JsonPropertyName("hostname")] public string? Hostname { get; set; }
         [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
         [JsonPropertyName("action")] public string? Action { get; set; }
         [JsonPropertyName("cdata")] public string? CData { get; set; }
     }
     ```
   - The adapter inspects `Success`, verifies `Action == expectedAction`, and validates that `Hostname` belongs to `ExpectedHostnames`.
3. **HTTP Client Hardening (P1-01, P1-02):**
   - **Hardcoded Endpoint (P1-01):** The adapter uses a constant `private static readonly Uri VerifyUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");`. `Turnstile:VerifyUrl` is removed from configuration, eliminating configuration tampering and credential exfiltration surface.
   - **Timeout:** Explicitly configured as **5 seconds** (`TimeSpan.FromSeconds(5)`).
   - **Disable Auto-Redirects:** `SocketsHttpHandler` / `HttpClientHandler` must set `AllowAutoRedirect = false` to prevent leaking `SecretKey` through unexpected HTTP 301/302 redirects.
   - **Comprehensive Exception Handling (P1-02):**
     Following the pattern of `ParkingDirectoryClient.cs`:
     ```csharp
     try
     {
         using var response = await httpClient.PostAsync(VerifyUri, content, ct);
         if (!response.IsSuccessStatusCode)
         {
             // Cloudflare non-2xx (429, 400, 403, 5xx) -> fail closed
             throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification provider communication error.", 503);
         }
         var result = await JsonSerializer.DeserializeAsync<TurnstileVerifyResponse>(
             await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
         if (result is null)
         {
             throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Invalid provider response format.", 503);
         }
         // evaluate result...
     }
     catch (OperationCanceledException) when (ct.IsCancellationRequested)
     {
         // Client disconnected or host application is shutting down -> propagate cancellation cleanly
         throw;
     }
     catch (OperationCanceledException)
     {
         // Outbound call to Cloudflare timed out (5s elapsed) -> 503 Provider unavailable
         throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Bot verification timed out. Please try again.", 503);
     }
     catch (HttpRequestException)
     {
         // DNS failure, TLS negotiation failure, or connection drop
         throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Cannot reach bot verification provider.", 503);
     }
     catch (JsonException)
     {
         // Provider returned malformed JSON
         throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Malformed bot verification response.", 503);
     }
     catch (IOException)
     {
         // Connection reset / stream read interruption
         throw new AuthException("BOT_VERIFICATION_UNAVAILABLE", "Communication interrupted during verification.", 503);
     }
     ```
   - **Retries:** **0 automatic retries** in MVP to avoid cascading delays; fail closed with 503 on transport failure.
4. **Ordering, Flow Placement & Database Lock Safety (P1-05):**
   - **Driver Registration:** First statement in `DriverRegistrationService.RegisterAsync`, before `AccountWorkflowService.Validate(request)` and before `db.BeginTransactionAsync(ct)`.
   - **Owner Registration:** First statement in `OwnerRegistrationService.RegisterAsync`, before `AccountWorkflowService.Validate(request)` and before `db.BeginTransactionAsync(ct)`.
   - **Password Login:** First statement in `LoginCommandHandler.Handle`, before `unitOfWork.BeginTransactionAsync(cancellationToken)`.
   - **Single Invocation Guarantee:** Controller actions do NOT call verifier; handlers/services own verification so there is no duplicate verification.
   - **Crucial Invariant:** `VerifyAsync` MUST be awaited **before** opening any database transaction (`db.BeginTransactionAsync` / `unitOfWork.BeginTransactionAsync`). No database transaction or lock is held during the outbound call to Cloudflare.
5. **Error Handling Architecture (Bypassing `SignInErrors` default):**
   - On verification failure, the verifier throws `AuthException(code, message, statusCode)` (e.g., `400 BOT_VERIFICATION_REQUIRED`, `400 BOT_VERIFICATION_FAILED`, `400 BOT_VERIFICATION_EXPIRED`, `503 BOT_VERIFICATION_UNAVAILABLE`).
   - Throwing `AuthException` ensures that:
     - In registration flows, `ApiExceptionHandler` catches it and `ApiExceptionMapper` renders the exact error envelope.
     - In the login flow, throwing `AuthException` directly bypasses `SignInErrors.ForCode` (which would otherwise incorrectly force unknown codes to 401 `AUTH_FAILED`).
6. **Availability & Environment Startup Guard (P0-01, P1-04):**
   - Bound from configuration as:
     - `Turnstile:Enabled` (bool, default `true` to prevent accidental disablement, P1-04)
     - `Turnstile:SecretKey` (string)
     - `Turnstile:ExpectedHostnames` (string array)
   - **Environment Invariant (P0-01, P1-04):** Turnstile may only be disabled in `Development` or `Testing` environments:
     ```csharp
     if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
     {
         if (!turnstileOptions.Enabled || string.IsNullOrWhiteSpace(turnstileOptions.SecretKey) ||
             turnstileOptions.ExpectedHostnames is not { Length: > 0 })
         {
             throw new InvalidOperationException(
                 "Non-development environments (Production, Staging) require Turnstile to be enabled with a valid SecretKey and non-empty ExpectedHostnames.");
         }
         // Strict Environment Allowlist Validation (P0-01)
         if (builder.Environment.IsProduction())
         {
             var forbiddenInProd = new[] { "localhost", "127.0.0.1", "::1", "staging" };
             if (turnstileOptions.ExpectedHostnames.Any(h => string.IsNullOrWhiteSpace(h) ||
                 forbiddenInProd.Any(f => h.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                 h.Contains('/') || h.Contains(':') || h.Contains('*')))
             {
                 throw new InvalidOperationException(
                     "Production ExpectedHostnames contains forbidden entries (localhost, 127.0.0.1, ::1, staging, ports, schemes, or wildcards).");
             }
         }
     }
     ```

### Token `action` and `hostname` binding

1. **Action Binding:** Each flow passes a distinct `action` to the widget, and the server validates that the returned `action` matches the executing flow:
   - Driver registration: `driver_register`
   - Owner registration: `owner_register`
   - Password login: `password_login`
   Mismatches trigger `BOT_VERIFICATION_FAILED` (400).

2. **Hostname Validation (P0-01):** The server verifies that `cfResponse.Hostname` matches an entry in `Turnstile:ExpectedHostnames` (case-insensitive `StringComparison.OrdinalIgnoreCase`).
   - Development: `["localhost", "127.0.0.1", "::1"]` (port number is omitted by Turnstile).
   - Staging: `["staging.smartpark.vn"]`.
   - Production: `["smartpark.vn", "app.smartpark.vn"]` (strictly no localhost, loopback, or staging domains).
   Mismatches trigger `BOT_VERIFICATION_FAILED` (400).

### Provider result → application outcome mapping (P0-01, P0-02, P1-02)

| Provider / local condition | Application outcome | HTTP status | Public code |
| --- | --- | --- | --- |
| `turnstileToken` missing, null, or whitespace-only | Reject locally (no provider call) | 400 | `BOT_VERIFICATION_REQUIRED` |
| Token present but length > 2048 chars | Reject locally (no provider call) | 400 | `BOT_VERIFICATION_FAILED` |
| `success=false` with `invalid-input-response` | Reject (user token invalid) | 400 | `BOT_VERIFICATION_FAILED` |
| `success=false` with `timeout-or-duplicate` | Reject (expired **or** already consumed) | 400 | `BOT_VERIFICATION_EXPIRED` |
| `action` mismatch OR `hostname` not in `ExpectedHostnames` | Reject (token origin/flow mismatch) | 400 | `BOT_VERIFICATION_FAILED` |
| `success=false` with `missing-input-response` or `bad-request` | **Fail closed** (backend integration/encoding error) | 503 | `BOT_VERIFICATION_UNAVAILABLE` |
| `missing-input-secret` / `invalid-input-secret` / `internal-error` | **Fail closed** (provider/config error) | 503 | `BOT_VERIFICATION_UNAVAILABLE` |
| `success=false` with empty `error-codes` or **unknown error codes** | **Fail closed** | 503 | `BOT_VERIFICATION_UNAVAILABLE` |
| Provider returns non-2xx status (HTTP 429, 400, 403, 500, 502, 503) | **Fail closed** (provider communication error) | 503 | `BOT_VERIFICATION_UNAVAILABLE` |
| Network timeout (> 5s) / DNS / TLS / unparsable provider response | **Fail closed** | 503 | `BOT_VERIFICATION_UNAVAILABLE` |
| Verification enabled but `SecretKey` absent, or misconfigured | **Fail closed** | 503 | `BOT_VERIFICATION_UNAVAILABLE` |

**Fail-closed invariant.** When verification is enabled, no protected operation may execute unless `success=true` **and** the flow `action` and `hostname` match. Provider unavailability never degrades to "skip verification". `BOT_VERIFICATION_UNAVAILABLE` is a temporary failure the client may retry after obtaining a new token.

**Disabled mode.** `Turnstile:Enabled=false` skips verification entirely. It is permitted in **local/CI/test** environments only. Production and Staging must never run with verification disabled.

### Frontend Token Lifecycle & State Management (P0-02, P0-04)

Turnstile tokens are single-use with a maximum lifetime of 300 seconds (5 minutes). The frontend observes this state machine:

1. **Strict Single-Use:** A token is consumed upon first verification attempt. Frontend must never submit the same token twice.
2. **Widget Reset Scenarios (P0-02, P0-04):**
   - **Local form validation errors (BEFORE submit):** **DO NOT RESET** the widget. The token was never sent to the network and remains valid within its 300-second window.
   - **Rate Limiting Rejection (HTTP 429):** The request was blocked at the gateway before the verifier was called. The token was NOT sent to Cloudflare and may be submitted when the rate limit window resets (if $< 300$s).
   - **Missing Token Rejection (HTTP 400 `BOT_VERIFICATION_REQUIRED`):** No token was submitted; the user must solve the widget before submitting.
   - **Submissions That Reached Verification:** Call `turnstile.reset(widgetId)` immediately on:
     - `BOT_VERIFICATION_FAILED` or `BOT_VERIFICATION_EXPIRED`.
     - Business validation failure (`400 VALIDATION_FAILED` from service layer, e.g. `AccountWorkflowService.Validate`), because verification already succeeded and consumed the token! (P0-02)
     - Credential failure (`401 AUTH_FAILED`, `403 ACCOUNT_LOCKED`) or successful auth (token was verified and consumed by Cloudflare).
     - Any HTTP 503 or network failure during submission (the token may have been consumed upstream).
   - **Token expiration:** Handle the widget's `expired-callback` by clearing local token state so the user re-challenges before submitting.

### Secret Key Rotation Procedure (P1-06)

- `Turnstile:SecretKey` is delivered exclusively via environment variables (`Turnstile__SecretKey`) or secure secret store. Never committed to git or stored in plaintext `appsettings.json`.
- **Zero-Downtime Rotation Runbook (Cloudflare 2-hour Grace Window):**
  1. In Cloudflare Dashboard / API, initiate secret key rotation with `invalidate_immediately = false`. Both old and new secrets are valid on Cloudflare simultaneously for up to 2 hours.
  2. Update `Turnstile__SecretKey` in UserService configuration / secret store.
  3. Redeploy or restart UserService instances.
  4. Verify that `siteverify` succeeds with the new secret key.
  5. After the 2-hour transition window elapses, Cloudflare deactivates the old secret key automatically.

### Observability & Metrics (P1-06)

To distinguish malicious bot traffic from configuration or Cloudflare outages, structured logs and application metrics are recorded:
- **Turnstile Adapter Metrics:**
  - `turnstile.verify.success` (Counter)
  - `turnstile.verify.failed` (Counter, tagged with `reason: invalid|expired|action_mismatch|hostname_mismatch`)
  - `turnstile.verify.unavailable` (Counter, tagged with `reason: timeout|5xx|http_status|config`)
  - `turnstile.verify.cancelled` (Counter)
  - `turnstile.verify.duration_ms` (Histogram)
- **Middleware Rate Limiter Metric (P1-06):**
  - `auth.rate_limited` (Counter, tagged with `endpoint: driver_reg|owner_reg|login`) emitted by `options.OnRejected` callback in ASP.NET Core RateLimiter middleware.
- **Privacy Rule:** Never include email, IP address, raw token, secret key, or raw provider payload in metric tags or log messages.

---

## API Specification

The three operations below are existing endpoints. This contract adds one request field and four error codes and changes nothing else about their success responses, authentication or ownership.

### 1. Driver registration

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/auth/register/driver` |
| Availability | Existing route; bot guard implemented behind `IBotVerificationService` |
| Authentication | Anonymous |
| Permission / account state | Not applicable — creates a pending identity |
| Resource scope | Public registration context |
| Content type | `application/json` |
| Headers | No additional required headers (token is in body) |

**Request sample**

```json
{
  "fullName": "Driver Example",
  "email": "driver@example.test",
  "turnstileToken": "<turnstile-token>"
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `turnstileToken` | body | string | Required when `Turnstile:Enabled=true`; optional when disabled | Opaque provider token; single-use; trimmed non-empty and ≤ 2048 chars | `<turnstile-token>` |
| existing fields | body | as current contract | unchanged | See [driver registration](driver-register-api-design.md) | — |

**Response / errors.** Success, registration result and existing error names are unchanged. Additions: `BOT_VERIFICATION_REQUIRED`, `BOT_VERIFICATION_FAILED`, `BOT_VERIFICATION_EXPIRED` (400) plus `BOT_VERIFICATION_UNAVAILABLE` (503), evaluated before any identity/OTP side effect.

### 2. Owner registration

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/auth/register/owner` |
| Availability | Existing route; bot guard implemented behind `IBotVerificationService` |
| Authentication | Anonymous |
| Permission / account state | Not applicable — creates a pending identity |
| Resource scope | Public registration context |
| Content type | `application/json` |
| Headers | No additional required headers |

**Request sample**

```json
{
  "fullName": "Owner Example",
  "businessName": "Example Parking Co.",
  "email": "owner@example.test",
  "phone": "0900000000",
  "lotType": "outdoor",
  "agreedToPolicy": true,
  "turnstileToken": "<turnstile-token>"
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `turnstileToken` | body | string | Required when `Turnstile:Enabled=true`; optional when disabled | Opaque provider token; single-use; trimmed non-empty and ≤ 2048 chars | `<turnstile-token>` |
| existing fields | body | as current contract | unchanged | See [owner registration](owner-register-api-design.md) | — |

**Response / errors.** Success and existing error names are unchanged. Additions are the same four codes as above.

### 3. Password login

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/auth/login` |
| Availability | Existing route; bot guard implemented behind `IBotVerificationService` |
| Authentication | Anonymous |
| Permission / account state | Not applicable — pre-authentication |
| Resource scope | Public login context |
| Content type | `application/json` |
| Headers | No additional required headers |

**Request sample**

```json
{
  "email": "driver@example.test",
  "password": "<password>",
  "turnstileToken": "<turnstile-token>"
}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `turnstileToken` | body | string | Required when `Turnstile:Enabled=true`; optional when disabled | Opaque provider token; single-use; trimmed non-empty and ≤ 2048 chars | `<turnstile-token>` |
| `email`, `password` | body | as current contract | unchanged | See [shared login contract](../../02-architecture/api/v1.1-contracts.md#authentication-and-normal-login) | — |

**Response / errors.** Successful login, MFA-challenge and existing failure codes are unchanged. The four bot-verification codes are returned **before** credential evaluation and before `unitOfWork.BeginTransactionAsync`, and therefore do not reveal whether the credential was valid. Existing login failures remain distinguishable only after a valid token is presented.

### Validation and error responses

All three endpoints emit the existing error envelope (`success`, `code`, `message`, optional `errors`, and a `traceId` added by the API error writer). The actual HTTP status is not duplicated in the body.

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| 400 | `BOT_VERIFICATION_REQUIRED` | Token missing, null, or whitespace-only while verification is enabled. | `{"success":false,"code":"BOT_VERIFICATION_REQUIRED","message":"Bot verification is required."}` | Solve the challenge and submit with a token. |
| 400 | `BOT_VERIFICATION_FAILED` | Token malformed, length > 2048, rejected by provider, or action/hostname mismatch. | `{"success":false,"code":"BOT_VERIFICATION_FAILED","message":"Bot verification failed. Please try again."}` | Reset the widget, solve again, resubmit. |
| 400 | `BOT_VERIFICATION_EXPIRED` | Token expired or already used (`timeout-or-duplicate`). | `{"success":false,"code":"BOT_VERIFICATION_EXPIRED","message":"Bot verification expired. Please complete the challenge again."}` | **Reset widget to obtain a new token**, then resubmit. |
| 503 | `BOT_VERIFICATION_UNAVAILABLE` | Provider unreachable, timeout (> 5s), non-2xx status, provider internal error, or backend misconfiguration. | `{"success":false,"code":"BOT_VERIFICATION_UNAVAILABLE","message":"Bot verification is temporarily unavailable. Please try again later."}` | Treat as temporary; obtain a **new** token and retry after a delay. Do not auto-bypass. |

Field-level messages must never expose provider secrets, internal URLs, configuration keys or raw provider error payloads.

---

## Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant FE as Frontend (Turnstile widget)
    participant GW as ASP.NET Core RateLimiter Middleware
    participant API as UserService API
    participant APP as Application handler/service
    participant BOT as IBotVerificationService (Infrastructure adapter)
    participant CF as Cloudflare siteverify

    FE->>CF: Load widget with site key + action (driver_register / owner_register / password_login)
    User->>FE: Solve challenge
    FE->>FE: Receive single-use turnstileToken
    FE->>GW: POST register driver/owner or login (+ turnstileToken)
    alt Rate Limit Exceeded
        GW-->>FE: 429 Too Many Requests (Terminates early; auth.rate_limited metric emitted)
    else Rate Limit Permitted
        GW->>API: Route to Controller
        API->>APP: Map DTO -> command/service call
        Note over APP,BOT: Verification runs BEFORE BeginTransactionAsync
        APP->>BOT: VerifyAsync(token, action, ct)
        BOT->>CF: POST /siteverify (secret, response) [Timeout: 5s, AllowAutoRedirect: false]
        alt Provider Network Timeout (> 5s) OR Provider non-2xx OR Socket/DNS Failure
            BOT-->>APP: throw AuthException(BOT_VERIFICATION_UNAVAILABLE, 503)
        else Client Aborts Request (ct cancelled)
            BOT-->>APP: propagate OperationCanceledException (no 503)
        else Provider 200 OK
            CF-->>BOT: JSON { success, action, hostname, error-codes }
            alt success = true AND action match AND hostname match
                BOT-->>APP: verification succeeds
                APP->>APP: Begin DB transaction & execute protected auth flow
                APP-->>API: Result
                API-->>FE: Existing success/MFA/failure response
                FE->>FE: turnstile.reset() (token consumed)
            else verification failed / expired / action mismatch
                BOT-->>APP: throw AuthException(BOT_VERIFICATION_*)
                APP-->>API: Exception (bypasses SignInErrors)
                API-->>FE: 400/503 error envelope
                FE->>FE: turnstile.reset() to obtain a new token
            end
        end
    end
```

---

## Decisions and open questions status

| ID | Topic / Decision | Resolution | Status |
| --- | --- | --- | --- |
| DQ-1 | Forgot-password recovery protection | **Deferred** until a real backend endpoint exists (FE flow is currently a client-side mock). | Deferred (SPARK-257 follow-up) |
| OQ-1 | Token transport format | **Resolved:** JSON body field `turnstileToken` (camelCase in JSON, `string? TurnstileToken` in C# DTOs). Aligns with existing JSON payload structure without requiring custom header plumbing. | **APPROVED** |
| OQ-2 | Action & Hostname binding (P0-01) | **Resolved:** Frontend widget renders with specific `action`. Adapter enforces `cfResponse.Action == expectedAction` and `cfResponse.Hostname` in `ExpectedHostnames` allowlist. Production strictly rejects `localhost`, `127.0.0.1`, `::1`, and staging domains. | **APPROVED** |
| OQ-3 | HTTP status for expired / reused token | **Resolved:** HTTP 400 with code `BOT_VERIFICATION_EXPIRED`. Frontend intercepts this code to trigger widget reset and inform the user. | **APPROVED** |
| OQ-4 | `remoteip` parameter forwarding | **Resolved:** Omitted (`null`) by default to prevent Docker bridge/NAT IP false positives and maintain privacy minimization. | **APPROVED** |
| OQ-5 | Scope expansion to OTP/MFA | **Resolved:** Out of scope for MVP. Protected flows are strictly Driver registration, Owner registration, and Password login. | **APPROVED** |
| OQ-6 | Login HTTP rate limiting policy (P0-03, P0-04) | **Resolved:** Segregated rate-limiting policies (`RegistrationRateLimit` shared between driver/owner, and dedicated `LoginRateLimit`) with reverse-proxy forwarded header awareness. | **APPROVED** |
| OQ-7 | Non-development startup protection (P1-04) | **Resolved:** Fail-fast startup guard in `Program.cs`. In `Production` and `Staging`, system refuses to start if `Turnstile:Enabled=false`, `SecretKey` is missing, or `ExpectedHostnames` contains invalid entries. | **APPROVED** |

---

## Acceptance Test Checklist (30 Scenarios)

The implementation must verify the following 30 test cases before being deemed complete:

| # | Test Scenario | Severity | Expected Outcome |
|---|---|---|---|
| 1 | **Valid token** | Critical | Token valid, action matches, hostname matches $\to$ continues normal auth flow. |
| 2 | **Missing token** | Critical | Null or omitted when enabled $\to$ 400 `BOT_VERIFICATION_REQUIRED` (no Cloudflare call). |
| 3 | **Whitespace-only token** | Critical | Token containing only whitespace $\to$ 400 `BOT_VERIFICATION_REQUIRED` (no Cloudflare call). |
| 4 | **Token length > 2048 chars** | Critical | Token length > 2048 rejected by verifier $\to$ 400 `BOT_VERIFICATION_FAILED` (not `VALIDATION_FAILED`). |
| 5 | **Invalid token format** | Critical | Malformed or unrecognized token $\to$ 400 `BOT_VERIFICATION_FAILED`. |
| 6 | **Expired / Duplicate** | Critical | Token expired or already consumed (`timeout-or-duplicate`) $\to$ 400 `BOT_VERIFICATION_EXPIRED`. |
| 7 | **Action mismatch** | Critical | Token minted for `driver_register` submitted to `password_login` $\to$ 400 `BOT_VERIFICATION_FAILED`. |
| 8 | **Hostname mismatch** | Critical | Token hostname not in `ExpectedHostnames` allowlist $\to$ 400 `BOT_VERIFICATION_FAILED`. |
| 9 | **Rate limit exceeded** | Critical | Incoming requests exceeding limit $\to$ 429 Too Many Requests without calling Cloudflare. |
| 10 | **Cloudflare timeout (> 5s)** | Critical | Provider call exceeds 5 seconds $\to$ 503 `BOT_VERIFICATION_UNAVAILABLE`. |
| 11 | **Client cancellation** | Critical | Client aborts request $\to$ `OperationCanceledException` propagated cleanly; does NOT return 503. |
| 12 | **Provider HTTP 429 / 4xx** | High | Cloudflare returns 429, 400, or 403 $\to$ 503 `BOT_VERIFICATION_UNAVAILABLE`. |
| 13 | **Provider HTTP 5xx** | High | Cloudflare returns 500, 502, or 503 $\to$ 503 `BOT_VERIFICATION_UNAVAILABLE`. |
| 14 | **Malformed provider response** | High | Provider returns invalid JSON or empty error codes with `success=false` $\to$ 503 `BOT_VERIFICATION_UNAVAILABLE`. |
| 15 | **Provider missing-input-response** | High | Cloudflare returns `missing-input-response` or `bad-request` $\to$ 503 (treated as infrastructure error, not user error). |
| 16 | **Request body > 64KB** | Medium | Request body exceeding limit $\to$ 413 Payload Too Large. |
| 17 | **Fixed Siteverify endpoint** | Critical | VerifyUri is a hardcoded constant and cannot be altered or redirected. |
| 18 | **Startup guard (Prod & Staging)**| Critical | When not in Dev/Testing, if disabled or missing secret/hostname $\to$ application crashes at startup. |
| 19 | **Production localhost rejection** | Critical | In Production, `ExpectedHostnames` containing `localhost` or `127.0.0.1` $\to$ startup crash. |
| 20 | **Transaction ordering** | Critical | Assert database transaction is NOT opened before or during `VerifyAsync`. |
| 21 | **Concurrent submissions**| High | Two concurrent requests using identical token $\to$ at most one passes Siteverify and executes business operation. |
| 22 | **Frontend token preservation** | Medium | Local form validation failure before submit keeps token; does not reset widget prematurely. |
| 23 | **Forwarded headers resolution** | Medium | Trusted proxy headers populate client IP; untrusted spoofed `X-Forwarded-For` is ignored. |
| 24 | **Disabled mode (dev/test)** | High | When `Turnstile:Enabled=false` in dev/test $\to$ request succeeds without token. |
| 25 | **Production staging rejection** | Critical | In Production, `ExpectedHostnames` containing `staging.smartpark.vn` $\to$ startup crash. |
| 26 | **Production invalid hostname format**| Critical | In Production, hostname containing `::1`, wildcard `*`, scheme `https://`, or port `:8443` $\to$ startup crash. |
| 27 | **RemoteIpAddress unresolvable** | Medium | Fallback to `"unknown"` partition functions as documented with pooled quota. |
| 28 | **Shared registration quota** | Medium | Driver and Owner registration share the 10 req/min/IP registration bucket. |
| 29 | **Business validation failure** | High | `AccountWorkflowService.Validate` failure after `VerifyAsync` forces frontend to reset widget. |
| 30 | **Fixed Window boundary test** | Low | Boundary requests across minute change observe expected FixedWindow counter reset. |

---

## Revision History & Traceability

| Revision | Date | Focus / Additions |
| --- | --- | --- |
| `v0.1` | 2026-11-04 | Initial DRAFT proposal. |
| `v1.0` | 2026-11-04 | Direction approval: OQ-1..OQ-7 resolved, Cloudflare protocol bug fixed, DB transaction ordering established. |
| `v1.1` | 2026-11-04 | Security hardening: ExpectedHostnames allowlist, layered rate limiting, 5s timeout, 14-point test checklist. |
| `v1.1.1` | 2026-11-04 | Resilience & implementation precision: FIX-01..FIX-10 (DTO consistency, cancellation propagation, staging guard, 18-point checklist). |
| `v1.2` | 2026-11-04 | Implementation contract: P0-01 (Prod hostname localhost rejection), P0-02 (`missing-input-response` & `bad-request` mapped to 503), P0-03 (Rate-limit quotas & IP trust model), P0-04 (Frontend reset on redeemed only), P1-01 (VerifyUrl locked), P1-02 (non-2xx & timeout fail-closed), P1-03 (Body limit 413), P1-04 (Enabled defaults true), P1-05 (Single invocation placement), P1-06 (2-hour rotation runbook with `invalidate_immediately=false`), and 24-point Acceptance Checklist. |
| `v1.2.1` | 2026-11-04 | Final implementation-ready freeze: Fixed P0-01 (Strict environment allowlist rejecting staging/::1/wildcards/ports in Production), P0-02 (Frontend token lifecycle distinction between pre-submit local validation vs post-submit consumed token), P0-03 & P0-04 (Explicit shared registration quota and proxy fallback semantics), P1-01 (VerifyUri hardcoded constant; config removed), P1-02 (HttpRequestException, JsonException, IOException handling mirroring ParkingDirectoryClient), P1-06 (Metric auth.rate_limited cleanly assigned to middleware OnRejected callback), and 30-point Acceptance Checklist. |
