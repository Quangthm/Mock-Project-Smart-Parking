# Session Review — 2026-11-04

## 1. Session Information

- **Task:** SPARK-257 — [SEC-AUTH] Protect public authentication flows from automated abuse.
- **Jira:** SPARK-257 (Story, 5 SP, backlog, no owner, not in Sprint 2). Related: SPARK-234 (Driver registration), SPARK-235 (Owner registration).
- **Branch:** `docs/SPARK-257-bot-protection-api-design` (created from `develop`).
- **Date:** 2026-11-04.
- **Scope this session:** documentation and planning only — no application code, no configuration values, no dependency changes, no tests executed.
- **Decision status:** design **APPROVED** — direction and OQ-1..OQ-7 resolutions approved by user/lead (Coding Skill §1); ready for implementation.

## 2. Objectives

1. Produce a reviewable, cross-cutting API design for Cloudflare Turnstile bot verification on the three public auth flows.
2. Update the existing contract documents and indexes so the addition is discoverable and consistent.
3. Create an external planning document to track execution against the design.
4. Create this session file per `ai/rule.md` Rule 3.
5. Do **not** implement backend/frontend or change runtime configuration in this session.

## 3. Work Completed

- Created branch `docs/SPARK-257-bot-protection-api-design` from `develop` (clean tree at creation).
- Wrote new cross-cutting design: `docs/03-design/api/bot-protection-spark-257-api-design.md` (Status: APPROVED).
  - Scope locked to `POST /api/auth/register/driver`, `POST /api/auth/register/owner`, `POST /api/auth/login`.
  - Documents the additive body field `turnstileToken`, the Application port, Infrastructure adapter, client-recovery behavior and fail-closed semantics.
  - Resolved Cloudflare siteverify protocol bug (backend does not POST `action`; verifies `action` returned in Cloudflare response).
  - Clarified database lock safety: `VerifyAsync` runs before `BeginTransactionAsync`.
  - Clarified error propagation: throws `AuthException` directly to bypass `SignInErrors` default 401.
  - Forgot-password/recovery protection explicitly deferred (no backend endpoint exists).
- Updated `docs/03-design/api/driver-register-api-design.md`:
  - Header notes the SPARK-257 design.
  - Register row includes `turnstileToken` when verification is enabled.
  - New "Bot verification (SPARK-257)" section.
  - Errors paragraph lists the four new codes and their ordering.
- Updated `docs/03-design/api/owner-register-api-design.md` with the same four changes.
- Updated `docs/02-architecture/api/v1.1-contracts.md`:
  - Password-login row includes `turnstileToken` and bot verification ordering.
  - New "Bot verification (SPARK-257)" paragraph.
- Added index rows in `docs/03-design/api/README.md` and `docs/02-architecture/api/README.md`.
- Created and updated external planning doc `D:\My file\FA26 - OJT FSoft\Planning\SPARK-257-planning.md`.
- Evaluated design, resolved OQ-1..OQ-7 with user approval, and updated API design to v1.0 APPROVED.
- Finalized contract to v1.2.1 (APPROVED — IMPLEMENTATION READY) addressing all targeted security & resilience points:
  - P0-01: Production hostname allowlist enforces strict production-only hostnames, crashing on `localhost`, `127.0.0.1`, `::1`, staging domains, wildcards, ports, or schemes.
  - P0-02: Sửa dứt điểm frontend token lifecycle: nếu lỗi validation local trước submit thì giữ token; nếu đã submit lên backend (dù fail `VALIDATION_FAILED` ở business layer, `401`, hay `503`) thì token đã bị tiêu thụ và bắt buộc phải reset widget.
  - P0-03 & P0-04: Chốt quota dùng chung có chủ đích cho registration (`RegistrationRateLimit`), tách biệt với `LoginRateLimit`; mô hình proxy trust qua `UseForwardedHeaders` và fallback "unknown" được ghi nhận rõ.
  - P1-01: Khóa cứng hằng số `VerifyUri` trong Adapter; xóa bỏ biến config `Turnstile:VerifyUrl`.
  - P1-02: Bổ sung trọn vẹn exception handling cho `HttpRequestException`, `JsonException`, `IOException` (đồng bộ mẫu `ParkingDirectoryClient.cs`); sạch lỗi 500 ngoài ý muốn.
  - P1-03: Body request vượt 64KB trả HTTP 413 Payload Too Large.
  - P1-04: `TurnstileOptions.Enabled` mặc định là `true` (không vô tình disable).
  - P1-05: Chốt vị trí gọi verifier tại dòng đầu tiên của 3 handlers/services, không gọi trùng, không DB transaction trước verify.
  - P1-06: Runbook secret rotation 2 giờ với `invalidate_immediately = false`; tách metric `auth.rate_limited` về đúng middleware callback.
  - Mở rộng trọn vẹn bộ Acceptance Checklist lên 30 kịch bản kiểm thử.

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

| File | Change |
| --- | --- |
| `docs/03-design/api/bot-protection-spark-257-api-design.md` | **Added & Updated** — cross-cutting APPROVED & IMPLEMENTATION READY design (v1.2.1). |
| `docs/03-design/api/driver-register-api-design.md` | Modified — header, register row, new section, errors. |
| `docs/03-design/api/owner-register-api-design.md` | Modified — header, register row, new section, errors. |
| `docs/02-architecture/api/v1.1-contracts.md` | Modified — header, login row, new paragraph. |
| `docs/03-design/api/README.md` | Modified — index row. |
| `docs/02-architecture/api/README.md` | Modified — index row. |
| `D:\My file\FA26 - OJT FSoft\Planning\SPARK-257-planning.md` | **Added & Updated** — external planning doc (P0 complete, OQ-1..OQ-7 resolved). |
| `ai/tasks/active/TASK-SPARK-257-SESSION-20261104.md` | **Added & Updated** — this session file. |

## 6. Issues and Blockers

- **Direction approval gate (UNBLOCKED):** Direction and OQ-1..OQ-7 resolutions approved by user/lead. Ready for Phase 1 implementation.
- **Forgot-password deferred:** SPARK-257 lists it, but there is no backend endpoint; the FE flow is a client-side mock. Protect it only after a real endpoint exists.
- **Login has no HTTP rate limiting today:** Turnstile is not a replacement; rate limiting remains a separate follow-up task.

## 7. Validation Results

- **No code, build, or test execution this session.** No `dotnet build`, `dotnet test`, or `node --test` was run.
- Documentation reviewed for internal consistency and template alignment only.
- `git status` confirms only the intended documentation files changed (see §8).

## 8. Git Status

```
## docs/SPARK-257-bot-protection-api-design
 M docs/02-architecture/api/README.md
 M docs/02-architecture/api/v1.1-contracts.md
 M docs/03-design/api/README.md
 M docs/03-design/api/driver-register-api-design.md
 M docs/03-design/api/owner-register-api-design.md
?? docs/03-design/api/bot-protection-spark-257-api-design.md
```

`ai/tasks/active/TASK-SPARK-257-SESSION-20261104.md` is also new but lives outside the tracked documentation set above; it will appear as untracked after creation.

## 9. Handoff / Next Steps

1. Review `bot-protection-spark-257-api-design.md` and resolve OQ-1…OQ-7 (or explicitly defer them).
2. Obtain project-lead approval of the direction (port/adapter, code names, fail-closed behavior, config keys).
3. After approval, implement in lanes:
   - L1: options + `IBotVerificationService` + `TurnstileVerifier` + DI + `ApiExceptionMapper` codes.
   - L2: wire verification into `LoginCommandHandler`, `DriverRegistrationService.RegisterAsync`, `OwnerRegistrationService.RegisterAsync`; add `TurnstileToken` to the three DTOs and `LoginMapperProfile`.
   - L3: configuration surface (`compose.app.yml`, `.env.example`, `FE/.env.example`, `scripts/run-schema-service.ps1`, user-secrets).
   - L4: FE hook, widget, `SignUp.tsx` (driver + owner), `SignIn.tsx`, `authApi`/`ownerApi`, widget reset.
   - L5/L6: backend and frontend tests for valid, missing, invalid, expired/reused, provider failure, success→normal auth.
   - L7: verification commands.
4. Only after implementation: run `dotnet build SmartParking.slnx --nologo`, `dotnet test SmartParking.slnx --nologo`, `node --test tests/frontend/*.test.cjs`.

## 10. Context for Next Session

- Turnstile tokens are single-use and short-lived; `timeout-or-duplicate` maps to expired/reused.
- Verification runs first and fails closed; `503` is a retryable temporary failure, not a bypass.
- Secret key is backend-only; the site key is the only `VITE_*` value.
- `ApiExceptionMapper` uses an explicit whitelist, so every new code must be added with its status.
- Registration DTOs validate manually; login uses FluentValidation. Do not introduce `[Required]` model-state errors.
- Frontend rules: no new dependency without asking; do not run verification unless the user asks.