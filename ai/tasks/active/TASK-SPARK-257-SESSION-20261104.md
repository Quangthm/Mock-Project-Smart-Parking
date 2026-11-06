# Session Review — 2026-11-04 (Updated 2026-11-06)

## 1. Session Information

- **Task:** SPARK-257 — [SEC-AUTH] Protect public authentication flows from automated abuse.
- **Jira:** SPARK-257 (Story, 5 SP, backlog, no owner, not in Sprint 2). Related: SPARK-234 (Driver registration), SPARK-235 (Owner registration).
- **Branch:** `docs/SPARK-257-bot-protection-api-design` (created from `develop`).
- **Date:** 2026-11-04 (Design & Planning), 2026-11-06 (Phases 1, 2, 3 & 4 Implementation).
- **Scope this session:** Phase 4 execution — Frontend Turnstile integration (`useTurnstile.ts`, `TurnstileWidget.tsx`), API payload updates, and token lifecycle management in `SignIn.tsx` and `SignUp.tsx`.
- **Decision status:** Phases 1, 2, 3 & 4 **COMPLETED & VALIDATED**; SPARK-257 end-to-end fullstack implementation complete.

## 2. Objectives

1. Produce a reviewable, cross-cutting API design for Cloudflare Turnstile bot verification on the three public auth flows. (Done)
2. Update the existing contract documents and indexes so the addition is discoverable and consistent. (Done)
3. Create an external planning document to track execution against the design. (Done)
4. Phase 1 Implementation:
   - Declare `IBotVerificationService` port in Application Layer.
   - Implement `TurnstileOptions` configuration & `TurnstileVerifyResponse` DTO in Infrastructure Layer.
   - Implement `TurnstileVerifier` adapter in Infrastructure Layer with fail-closed semantics, hardened HTTP client, and safe logging.
   - Register 4 bot verification error codes in `ApiExceptionMapper.cs`.
   - Configure typed `HttpClient` with 5s timeout, auto-redirect disabled, singleton options, and Startup Guard in `Program.cs`.
   - Write comprehensive unit tests in `TurnstileVerifierTests.cs` and `ApiErrorHandlingTests.cs` covering all 13 required scenarios + startup guard + error mapper.
5. Phase 2 Implementation:
   - Add `TurnstileToken` field to public DTOs & Command: `DriverRegistrationDto`, `OwnerRegistrationDto`, `LoginDto`, `LoginCommand`.
   - Ensure mapping in `LoginMapperProfile` from `LoginDto.TurnstileToken` to `LoginCommand.TurnstileToken`.
   - Inject `IBotVerificationService` and call `VerifyAsync` at the very beginning of the 3 public flows before any DB transaction:
     - `DriverRegistrationService.RegisterAsync` (action `"driver_register"`).
     - `OwnerRegistrationService.RegisterAsync` (action `"owner_register"`).
     - `LoginCommandHandler.Handle` (action `"password_login"`).
   - Configure separate rate limiting policies `RegistrationRateLimit` and `LoginRateLimit` (10 req/min/IP) with `UseForwardedHeaders` in `Program.cs` and decorate `AuthController.cs`.
   - Write unit and HTTP pipeline integration tests in `BotProtectionFlowTests.cs` covering all verification flows, bypass when disabled, failure before DB transactions, and HTTP pipeline integration.
6. Phase 3 Implementation:
   - Add Turnstile variables to root `.env.example` (`SMARTPARK_TURNSTILE_ENABLED`, `SMARTPARK_TURNSTILE_SECRET_KEY`, `SMARTPARK_TURNSTILE_EXPECTED_HOSTNAMES`).
   - Add public Turnstile site key to `FE/.env.example` (`VITE_TURNSTILE_SITE_KEY`).
   - Wire Turnstile environment variables into `user-service` in `compose.app.yml`.
   - Add default `Turnstile` section to `src/Services/UserService/SmartParking.UserService.API/appsettings.json`.
   - Forward Turnstile environment variables in `scripts/run-schema-service.ps1`.
   - Ensure zero committed real secrets or keys.
7. Phase 4 Implementation:
   - Declare Turnstile types and `VITE_TURNSTILE_SITE_KEY` in `FE/src/vite-env.d.ts`.
   - Build reusable `useTurnstile.ts` hook and `TurnstileWidget.tsx` component with imperative reset and cleanup on unmount/action change.
   - Update API clients (`authApi.ts` and `ownerApi.ts`) with `turnstileToken?: string` payload.
   - Integrate widget into `SignIn.tsx` (`password_login`) and `SignUp.tsx` (`driver_register`, `owner_register`).
   - Enforce token lifecycle rules: preserve token on local validation/429/required; reset on server verification rejection, 400 validation, 401/403 auth errors, 503/network errors, or success; clear on expiry.

## 3. Work Completed

- Created branch `docs/SPARK-257-bot-protection-api-design` from `develop`.
- Wrote new cross-cutting design: `docs/03-design/api/bot-protection-spark-257-api-design.md` (Status: APPROVED v1.2.1).
- Updated existing API contracts: `driver-register-api-design.md`, `owner-register-api-design.md`, `v1.1-contracts.md`, and relevant indexes.
- **Phase 1 Implementation Completed:**
  - **Task 1.1 (Application Port):** Created `IBotVerificationService.cs` in `SmartParking.UserService.Application/Common/Interfaces/Services/` with no HTTP or infrastructure dependencies.
  - **Task 1.2 (Infrastructure Options & DTO):**
    - Created `TurnstileOptions.cs` in `SmartParking.UserService.Infrastructure/Configurations/` (`Enabled = true` by default).
    - Created `TurnstileVerifyResponse.cs` in `SmartParking.UserService.Infrastructure/Services/` with explicit JSON property mappings.
  - **Task 1.3 (Infrastructure Adapter):**
    - Created `TurnstileVerifier.cs` in `SmartParking.UserService.Infrastructure/Services/`.
    - Hardcoded endpoint `private static readonly Uri VerifyUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");`.
    - Local checks before network call: missing/whitespace token (`400 BOT_VERIFICATION_REQUIRED`), length > 2048 (`400 BOT_VERIFICATION_FAILED`), missing secret key when enabled (`503 BOT_VERIFICATION_UNAVAILABLE`).
    - Outbound `FormUrlEncodedContent` sending `secret`, `response`, and optional `remoteip`.
    - Strict result evaluation: non-2xx status $\to$ 503, `timeout-or-duplicate` $\to$ 400 `BOT_VERIFICATION_EXPIRED`, `invalid-input-response` $\to$ 400 `BOT_VERIFICATION_FAILED`, other provider errors $\to$ 503 `BOT_VERIFICATION_UNAVAILABLE`.
    - Flow binding: verifies `action == expectedAction` and `hostname` in `ExpectedHostnames` allowlist (case-insensitive) $\to$ 400 `BOT_VERIFICATION_FAILED` on mismatch.
    - Exception handling mirrors `ParkingDirectoryClient.cs`: cancellation clean propagation, provider timeout $\to$ 503, `HttpRequestException`/`JsonException`/`IOException` $\to$ 503.
    - Privacy logging: never logs `token` or `SecretKey`.
  - **Task 1.4 (API Error Mapping):**
    - Registered `BOT_VERIFICATION_REQUIRED` (400), `BOT_VERIFICATION_FAILED` (400), `BOT_VERIFICATION_EXPIRED` (400), and `BOT_VERIFICATION_UNAVAILABLE` (503) in `ApiExceptionMapper.cs`.
  - **Task 1.5 (DI Registration & Startup Guard):**
    - Registered singleton `TurnstileOptions` and typed `HttpClient<IBotVerificationService, TurnstileVerifier>` with 5s timeout and `SocketsHttpHandler.AllowAutoRedirect = false` in `Program.cs`.
    - Added environment startup guard enforcing enabled Turnstile, secret key, and non-empty `ExpectedHostnames` in Staging/Production, with strict rejection of localhost/loopback/staging/wildcards/ports/schemes in Production.
  - **Task 1.6 (Unit Tests):**
    - Created `TurnstileVerifierTests.cs` (48 test variations) testing all 13 core scenarios + remoteip + case-insensitivity + IO/network errors + Startup Guard + error mapper.
    - Updated `ApiErrorHandlingTests.cs` verifying error envelopes for all 4 bot verification error codes.
    - Verified `LayerBoundaryTests` passes (0 layer boundary leaks).
  - Committed Phase 1: `18472a5b feat(auth): implement bot verification port, adapter and error mapping [SPARK-257]`.
- **Phase 2 Implementation Completed:**
  - **Task 2.1 (DTOs & Command):**
    - Added `public string? TurnstileToken { get; set; }` to `DriverRegistrationDto.cs`, `OwnerRegistrationDto.cs`, `LoginDto.cs`, and `LoginCommand.cs`.
    - Updated `LoginMapperProfile.cs` to map `TurnstileToken` explicitly.
  - **Task 2.2 (Driver Registration Service):**
    - Injected `IBotVerificationService? botVerifier = null` into `DriverRegistrationService`.
    - Placed `VerifyAsync(request.TurnstileToken, "driver_register", ct)` at line 1 of `RegisterAsync`, strictly before validation and `db.BeginTransactionAsync`.
  - **Task 2.3 (Owner Registration Service):**
    - Injected `IBotVerificationService? botVerifier = null` into `OwnerRegistrationService`.
    - Placed `VerifyAsync(body.TurnstileToken, "owner_register", ct)` at line 1 of `RegisterAsync`, strictly before validation and `db.BeginTransactionAsync`.
  - **Task 2.4 (Login Command Handler):**
    - Injected `IBotVerificationService? botVerifier = null` into `LoginCommandHandler`.
    - Placed `VerifyAsync(request.TurnstileToken, "password_login", cancellationToken)` at line 1 of `Handle`, strictly before `unitOfWork.BeginTransactionAsync`.
  - **Task 2.5 (Rate Limiting & Forwarded Headers):**
    - Registered `RegistrationRateLimit` and `LoginRateLimit` (10 req/min/IP, Status 429) in `Program.cs`.
    - Added `app.UseForwardedHeaders()` before `app.UseRouting()`.
    - Replaced `[EnableRateLimiting("DriverOtp")]` on `RegisterDriver` and `RegisterOwner` with `[EnableRateLimiting("RegistrationRateLimit")]` on `AuthController.cs`.
    - Decorated `Login` with `[EnableRateLimiting("LoginRateLimit")]`.
    - Updated `DriverRegistrationHttpTests.cs` and `OwnerRegistrationHttpTests.cs` test fixtures.
  - **Task 2.6 (Comprehensive Unit & HTTP Tests):**
    - Created `BotProtectionFlowTests.cs` (10 tests) covering:
      - Driver registration missing token, invalid token, valid token.
      - Owner registration missing token, invalid token, valid token.
      - Login missing token, invalid token, valid token.
      - Invariant test: verification failure throws BEFORE DB transaction begins.
      - HTTP pipeline integration for missing token (returns 400 `BOT_VERIFICATION_REQUIRED` bypassing `SignInErrors.ForCode`).
      - HTTP pipeline integration for valid token (returns 200 OK).
      - Rate limiting quota isolation between registration and login.
    - Verified `LayerBoundaryTests` passes (0 layer boundary leaks).
    - Full test suite execution: 279 passed, 0 failed, 54 skipped.
  - Committed Phase 2: `270754ec feat(auth): wire bot verification into registration, login flows and rate limiting [SPARK-257]`.
- **Phase 3 Implementation Completed:**
  - **Task 3.1 (Root .env.example):** Added `SMARTPARK_TURNSTILE_ENABLED=true`, `SMARTPARK_TURNSTILE_SECRET_KEY=` (empty placeholder), and `SMARTPARK_TURNSTILE_EXPECTED_HOSTNAMES=localhost,127.0.0.1,::1`.
  - **Task 3.2 (FE/.env.example):** Added `VITE_TURNSTILE_SITE_KEY=` (empty placeholder) for frontend injection.
  - **Task 3.3 (compose.app.yml):** Forwarded `Turnstile__Enabled`, `Turnstile__SecretKey`, and indexed `Turnstile__ExpectedHostnames__0..2` into `user-service` environment.
  - **Task 3.4 (appsettings.json):** Added default `Turnstile` configuration section with `Enabled: true` and `ExpectedHostnames: ["localhost", "127.0.0.1", "::1"]`.
  - **Task 3.5 (run-schema-service.ps1):** Added environment variable forwarding for `SMARTPARK_TURNSTILE_ENABLED` and `SMARTPARK_TURNSTILE_SECRET_KEY` in the `User` service runner block.
  - **Configuration Hardening:** Sanitized `ExpectedHostnames` in `Program.cs` to filter empty/whitespace entries, and updated `TurnstileVerifierTests.cs` to override base appsettings array during isolated unit tests.
  - **Security Audit:** Verified 0 secrets committed; all keys use empty placeholders.
  - **Test Suite:** Full UserService tests passed (279 passed, 0 failed, 54 skipped).
  - Committed Phase 3: `a9d7c54e chore(config): add turnstile multi-environment templates and compose wiring [SPARK-257]`.
- **Phase 4 Implementation Completed:**
  - **Task 4.1 (Types):** Declared `VITE_TURNSTILE_SITE_KEY` in `ImportMetaEnv` and full `window.turnstile` / `TurnstileRenderOptions` interfaces in `FE/src/vite-env.d.ts`.
  - **Task 4.2 (Hook & Component):**
    - Created `FE/src/hooks/useTurnstile.ts` for safe single script loading, widget rendering, token state, error/expire callbacks, and clean unmount removal.
    - Created `FE/src/components/common/TurnstileWidget.tsx` exposing imperative `reset()` and `getToken()` via `forwardRef`.
  - **Task 4.3 (API Clients):**
    - Updated `FE/src/lib/authApi.ts`: added `turnstileToken?: string` to `registerDriver` body and `login` parameters/body.
    - Updated `FE/src/lib/ownerApi.ts`: added `turnstileToken?: string` to `register` body.
  - **Task 4.4 (SignIn Integration):**
    - Embedded `TurnstileWidget` with `action="password_login"` in `SignIn.tsx`.
    - Passed `turnstileToken` to `authApi.login`.
    - Implemented token lifecycle: local validation and 429 rate-limit preserve the token; server rejection, 401/403, 400, 503, network errors, and successful login reset the widget.
  - **Task 4.5 (SignUp Integration):**
    - Embedded `TurnstileWidget` for Driver tab (`action="driver_register"`) and Owner tab (`action="owner_register"`).
    - Wired `turnstileToken` into `authApi.registerDriver` and `ownerApi.register`.
    - Implemented tab isolation and token lifecycle: local validation errors do not reset the widget; server validation/bot/auth rejections reset the widget.
  - **Validation:** Frontend `npm run build` and `npx tsc --noEmit` pass with 0 errors; Backend `dotnet build` succeeds with 0 errors and 0 warnings.

## 4. Technical Decisions

- **Additive contract only:** new optional-when-disabled request field plus four new error codes; existing success responses, authentication, ownership and validation are unchanged.
- **Reusable abstraction:** `IBotVerificationService.VerifyAsync(token, action, ct, remoteIp)` in Application; `TurnstileVerifier` adapter in Infrastructure; no provider HTTP in controllers/handlers (Coding Skill §2).
- **Fail-closed:** provider/network/configuration failure returns `503 BOT_VERIFICATION_UNAVAILABLE`; never silently bypasses.
- **Verification before side effects:** no identity/application write, OTP dispatch, login-attempt mutation or token issuance until verification succeeds; no DB lock held during the outbound call (§5).
- **Layered defense (P0-03):** Rate limiting (429) precedes bot verification, preventing outbound verification exhaustion.
- **Non-dev startup guard (P1-04, P0-01):** `Turnstile:Enabled` is mandatory outside Dev/Testing; application crashes at startup if disabled, missing secret, or `ExpectedHostnames` contains invalid entries in Production.
- **Token transport:** JSON body field `turnstileToken` (OQ-1 resolved).
- **Action & Hostname binding (P0-01):** per-flow `action` (`driver_register`, `owner_register`, `password_login`) and environment-specific `ExpectedHostnames` allowlist validation enforced (OQ-2 resolved).
- **Expired/duplicate status:** HTTP 400 `BOT_VERIFICATION_EXPIRED` (OQ-3 resolved).
- **Remote IP:** default `null` / omitted to prevent Docker bridge private IP issues (OQ-4 resolved).
- **HTTP Client (P1-01, P1-02):** 5-second timeout, cancellation cleanly propagated, 0 automatic retries in MVP, redirect disabled, domain locked to Cloudflare.
- **Frontend (P0-02, P0-04):** no new dependency; native script + hook + Tailwind widget; single-use token lifecycle; reset widget on submission failure or backend error, keep token on pre-submit local validation error (AC3).
- **Docs strategy:** both (a) in-place updates to existing contracts and (b) a cross-cutting SPARK-257 design, cross-linked.

## 5. Files Changed

| File | Change | Reason |
| --- | --- | --- |
| `src/Services/UserService/SmartParking.UserService.Application/DTOs/DriverRegistrationDto.cs` | Modified | Added `TurnstileToken` property |
| `src/Services/UserService/SmartParking.UserService.Application/DTOs/OwnerRegistrationDto.cs` | Modified | Added `TurnstileToken` property |
| `src/Services/UserService/SmartParking.UserService.Application/DTOs/LoginDto.cs` | Modified | Added `TurnstileToken` property |
| `src/Services/UserService/SmartParking.UserService.Application/Usecase/Login/LoginCommand.cs` | Modified | Added `TurnstileToken` property |
| `src/Services/UserService/SmartParking.UserService.Application/Common/MapperProfile/LoginMapperProfile.cs` | Modified | Explicit mapping for `TurnstileToken` |
| `src/Services/UserService/SmartParking.UserService.Application/Services/DriverRegistrationService.cs` | Modified | Injected `IBotVerificationService` and verified before DB transaction |
| `src/Services/UserService/SmartParking.UserService.Application/Services/OwnerRegistrationService.cs` | Modified | Injected `IBotVerificationService` and verified before DB transaction |
| `src/Services/UserService/SmartParking.UserService.Application/Usecase/Login/LoginCommandHandler.cs` | Modified | Injected `IBotVerificationService` and verified before DB transaction |
| `src/Services/UserService/SmartParking.UserService.API/Program.cs` | Modified | Added rate limiting policies `RegistrationRateLimit` and `LoginRateLimit`, forwarded headers |
| `src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs` | Modified | Applied `[EnableRateLimiting("RegistrationRateLimit")]` and `[EnableRateLimiting("LoginRateLimit")]` |
| `tests/Services/SmartParking.UserService.Tests/DriverRegistrationHttpTests.cs` | Modified | Updated rate limit policies in test fixture |
| `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs` | Modified | Updated rate limit policies in test fixture |
| `tests/Services/SmartParking.UserService.Tests/BotProtectionFlowTests.cs` | Created | Comprehensive unit & pipeline tests for 3 auth flows, invariants, and rate limiting |
| `.env.example` | Modified | Added Turnstile configuration placeholders |
| `FE/.env.example` | Modified | Added `VITE_TURNSTILE_SITE_KEY` placeholder |
| `compose.app.yml` | Modified | Wired Turnstile environment variables into `user-service` |
| `scripts/run-schema-service.ps1` | Modified | Forwarded Turnstile environment variables to UserService |
| `src/Services/UserService/SmartParking.UserService.API/appsettings.json` | Modified | Added default Turnstile section |
| `src/Services/UserService/SmartParking.UserService.API/Program.cs` | Modified | Sanitized `ExpectedHostnames` to strip empty entries |
| `tests/Services/SmartParking.UserService.Tests/TurnstileVerifierTests.cs` | Modified | Overwrote appsettings array indices for clean test isolation |
| `ai/tasks/active/TASK-SPARK-257-SESSION-20261104.md` | Updated | Session log tracking Phases 1, 2, 3 & 4 completion |
| `FE/src/vite-env.d.ts` | Modified | Declared Turnstile window and env types |
| `FE/src/hooks/useTurnstile.ts` | Created | Custom hook for Cloudflare Turnstile script loading & widget lifecycle |
| `FE/src/components/common/TurnstileWidget.tsx` | Created | Reusable Turnstile challenge widget component |
| `FE/src/lib/authApi.ts` | Modified | Added `turnstileToken` to `registerDriver` and `login` |
| `FE/src/lib/ownerApi.ts` | Modified | Added `turnstileToken` to `register` |
| `FE/src/roles/authentications/Sign In/SignIn.tsx` | Modified | Integrated Turnstile widget & token lifecycle for password login |
| `FE/src/roles/authentications/Sign Up/SignUp.tsx` | Modified | Integrated Turnstile widget & token lifecycle for driver and owner registration |

## 6. Issues and Blockers

- None. All deliverables across Phases 1, 2, 3, and 4 are completed and validated with 0 errors and 0 warnings.
- SPARK-257 fullstack implementation is ready for end-to-end verification and PR review.

## 7. Validation Results

- `dotnet build SmartParking.slnx -nr:false -m:1 --nologo`: **Build succeeded. 0 Warning(s), 0 Error(s)**.
- `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --filter "FullyQualifiedName~BotProtectionFlowTests|FullyQualifiedName~LayerBoundaryTests" --no-build --nologo`: **Passed! 22 passed, 0 failed**.
- Full UserService test suite: **Passed! 279 passed, 0 failed, 54 skipped (DB-container integration tests)**.

## 8. Git Status

```text
## docs/SPARK-257-bot-protection-api-design
 M ai/tasks/active/TASK-SPARK-257-SESSION-20261104.md
 M src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs
 M src/Services/UserService/SmartParking.UserService.API/Program.cs
 M src/Services/UserService/SmartParking.UserService.Application/Common/MapperProfile/LoginMapperProfile.cs
 M src/Services/UserService/SmartParking.UserService.Application/DTOs/DriverRegistrationDto.cs
 M src/Services/UserService/SmartParking.UserService.Application/DTOs/LoginDto.cs
 M src/Services/UserService/SmartParking.UserService.Application/DTOs/OwnerRegistrationDto.cs
 M src/Services/UserService/SmartParking.UserService.Application/Services/DriverRegistrationService.cs
 M src/Services/UserService/SmartParking.UserService.Application/Services/OwnerRegistrationService.cs
 M src/Services/UserService/SmartParking.UserService.Application/Usecase/Login/LoginCommand.cs
 M src/Services/UserService/SmartParking.UserService.Application/Usecase/Login/LoginCommandHandler.cs
 M tests/Services/SmartParking.UserService.Tests/DriverRegistrationHttpTests.cs
 M tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs
?? tests/Services/SmartParking.UserService.Tests/BotProtectionFlowTests.cs
```

## 9. Handoff / Next Steps

1. Commit Phase 4 changes:
   ```text
   feat(fe-auth): integrate turnstile challenge widget and token lifecycle [SPARK-257]
   ```
2. Prepare PR / review for SPARK-257 on branch `docs/SPARK-257-bot-protection-api-design`.

## 10. Context for Next Session


