# Session Review — 2026-11-12

## 1. Session Information
- **Latest review decision (17 November 2026):** User instructed temporary P4 closure and execution of P5/P6. P4 is CONDITIONAL HANDOFF; deferred tests are not PASS. P5 review completed with CHANGES REQUESTED (F6 remains in-branch High). P6 verification and local draft PR prepared. See Section 28 and delivery reports.
- **Session ID:** TASK-SPARK-340-SESSION-20261112
- **Date:** 17 November 2026 (Asia/Saigon; earlier sections retain historical dates)
- **Agent:** Lead Software Architect & Multi-Agent Task Orchestrator
- **Branch:** feat/SPARK-340-admin-platform-policies
- **Related Tasks:** SPARK-340 (Parent Story), SPARK-368 (Backend), SPARK-369 (Frontend), SPARK-370 (QA & UAT)
- **Status:** P1 contracts approved; P2/P3 implemented; P4 TEMPORARILY CLOSED by user direction with U06/U11/U20/tester sign-off deferred and U19 blocked by SPARK-273; P5 REVIEW EXECUTED / CHANGES REQUESTED; P6 VERIFICATION AND DRAFT PR PREPARED / NOT MERGE-READY. Parent task remains active, working tree uncommitted. No Jira status changed.

## 2. Objectives
- [x] Phase 0: Repository, architecture, and task discovery.
- [x] Verify initial baseline (Git branch develop, clean working tree, dotnet build succeeded, node frontend tests 229/229 passed).
- [x] Inspect existing policy catalogue, EF Core models, DB schema (`operational_policies`, `policy_revisions`), and UI components.
- [x] Phase 1: Architecture review and technical design for Admin policy catalogue, effective policy resolution, revision history, and integration configuration contracts.
- [x] Phase 2: Approve API contracts for Backend (SPARK-368), Frontend (SPARK-369), and Test Automation (SPARK-370).
- [x] Phase 2 (SPARK-368): Implement Backend — 6 OperationService admin policy endpoints, 2 PaymentService PayOS integration endpoints, 1 NotificationAuditService fixture endpoint.
- [x] Phase 2 (SPARK-368): Independent Code Review — verify Clean Architecture 5 layers, API Design v1.6 compliance, security/secrets masking, optimistic concurrency, and automated test execution.
- [x] Phase 3: Multi-agent implementation coordination (Frontend SPARK-369) — FE API clients, client-side validation, four admin UI components, tabbed SystemConfig, and 26 frontend tests.
- [x] QA test implementation coordination (SPARK-370), evidence and independent round2 report delivered.
- [x] Phase 4 execution handed off under temporary closure by user; pending cases remain explicitly deferred/blocked, not passed.
- [x] Phase 5 review performed, scoped findings/dispositions documented; zero-High gate NOT satisfied.
- [x] Phase 6 verification, session update and local PR draft prepared; commit/push/submission and merge readiness remain pending.

## 3. Work Completed
- Inspected repository topology, Clean Architecture boundaries across all 6 services (`UserService`, `FacilityService`, `OperationService`, `PaymentService`, `NotificationAuditService`, `IoTService`), and React 19 frontend in `FE/`.
- Validated baseline build: `dotnet build SmartParking.slnx` passed with 0 errors.
- Validated baseline frontend tests: `node --test tests/frontend/*.test.cjs` passed (229 tests passing).
- Traced authoritative requirements: SRS v1.1, FR-LOT-06, FR-POL-02/03/04/05/07/08, UC-POL-01, UC-POL-02, and `policy-catalogue.md`.
- Analyzed existing domain models: `OperationalPolicyRow`, `ReservationPolicyResolver`, `ReservationPolicyRevision`, `PolicyBound`, `ReservationPolicyRepository`, and PostgreSQL schema `operational_policies` in `03b-reservation-operations-service-db.sql`.
- Discovered existing integration adapters: `PayOsAdapter` with Sandbox/Live modes in `PaymentService`, and Delivery Fixture Adapters in `NotificationAuditService`.
- Formulated dependency-aware execution plan and architectural guardrails.
- Created external planning document at `D:\My file\FA26 - OJT FSoft\Planning\SPARK-340-planning.md` for team-wide tracking, milestone supervision, and phase-by-phase execution.
- Prepared and standardized delegation prompt for Phase 1 API Design Document at `D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-340-API-DESIGN.md`.
- Conducted independent architectural review of API Design v1.6 (`docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md`); verified 100% resolution of findings (F1 webhook collection validation, F2 hold & claim validity, sequence SQL synchronization, receipt envelope); formally approved contract as SSOT (`docs/03-design/api/admin-platform-policies-and-integrations-spark-340-review-v1.6-final.md`).
- Prepared and standardized delegation prompt for Phase 2 Backend Implementation (SPARK-368) at `D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-368-BACKEND-IMPLEMENTATION.md`.
- Completed independent Architectural & Code Review of Phase 2 Backend (SPARK-368) implementation: verified Clean Architecture 5 layers, 14 catalogue keys, optimistic concurrency with `GREATEST(...) + 1 microsecond`, transactional draft activation with row locks, PayOS mode-isolated secrets masking, allowlist validation, and zero outbound HTTP in FIXTURE mode. All backend test suites passed 100%. Formally approved SPARK-368.
- Prepared and standardized delegation prompt for Phase 3 Frontend Implementation (SPARK-369) at `D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-369-FRONTEND-IMPLEMENTATION.md`.
- Completed Phase 1 API Design & Contracts document at `docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md` specifying all 8 endpoints across OperationService, PaymentService, and NotificationAuditService, with full traceability, secret redaction, GiST exclusion invariants, snapshot immutability, and 3 sequence diagrams.
- Conducted independent quality gate review; received REJECTED verdict with 2 Critical findings (C1, C2) and 7 Major findings (M1-M7).
- Authored revised API Design v1.1 and addressed initial review findings.
- Received Round 2 independent review with specific conditions on C2 (mode-isolated credentials, FIXTURE zero outbound calls), M1 (consistent example labeling), M2 (exhaustive bound keys and validation rules), M3 (contiguous half-open interval math and exact GiST sentinel UUID), M4 (optimistic locking with expectedUpdatedAt on PUT draft), M5 (three temporal snapshot scenarios across T), M6 (DEGRADED/UNTESTED statuses and UC-POL-02 audit with configVersion), and M7 (unified wire error codes).
- Authored certified API Design v1.2 resolving Round 2 findings.
- Received Round 3 independent review (`admin-platform-policies-and-integrations-spark-340-review-v1.2.md`) focusing on: C1 (re-opened for PUT draft predicate: SQL update must enforce platform scope `tenant_id IS NULL AND site_id IS NULL`), M3 (target draft row locking `FOR UPDATE` to prevent concurrent duplicate activation race, missing predecessor handling, single `serverNow`), M5 (3-stage snapshot lifecycle: Review -> Hold Commit -> Confirmation, decoupling synchronous Admin 409 from async Booking Request Receipt `TERMS_CHANGED_OR_EXPIRED`), M2 (integer representability validation table: hourly fields multiple of 60, daily multiple of 1440, explicit site value equal to default when bound is null resolves with `source: SITE_OVERRIDE` and QA fixture), M4 (concurrency token missing/mismatch rules, microsecond precision, token invalidation on write, 3 QA scenarios), M6 (complete payment health matrix across FIXTURE/SANDBOX/LIVE, 1-hour grace window for in-flight checkouts with `PreviousChecksumKey`, mode binding invariant), and M7 (scoped exception handling delta in `ReservationExceptionHandler.cs`, unified wire code `INVALID_PAYMENT_BASE_URL`).
- Authored final certified API Design v1.3 in `docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md`, 100% verified against all 16 JSON blocks, Clean Architecture boundaries, and Round 3 conditions.
- Received Round 4 independent review (`admin-platform-policies-and-integrations-spark-340-review-v1.3.md`) highlighting: Critical M5 (Booking lifecycle alignment with 11/11 D11-A/D11-B decisions, decoupling $T_{policy}$ from $H$ and $T_{start}$, eliminating synchronous 409 choice for Scenario C), Major M2 (integer bounds representability $\lfloor max / U \rfloor \ge \lceil min / U \rceil$, removal of arbitrary 168h/30d caps, validating [61, 121] min valid and [61, 119] min invalid, 0 days advance booking), Major M4 (concurrent draft update with `RETURNING updated_at` and zero-rows branch handling returning `409 CANNOT_MODIFY_ACTIVE_POLICY` if activated or `409 RESOURCE_VERSION_CONFLICT` if modified), Major M6 (Active Keyring retention >= 24h protecting checkouts across consecutive rotations $K_0 \rightarrow K_1 \rightarrow K_2$, order-level key versioning, late callback reconciliation under D11-D, and 100% health state consistency between PUT and GET across FIXTURE, SANDBOX, LIVE).
- Completed and polished API Design v1.4 in `docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md` addressing 100% of review items with 17 valid JSON blocks, updated sequence diagrams, and error catalogue.
- Received Round 5 independent review (`admin-platform-policies-and-integrations-spark-340-review-v1.4.md`) highlighting: Critical M6 (Order-Key Binding Invariant: for a known order, exclusively verify signature using order's stored key version and mode; reject with 400 INVALID_WEBHOOK_SIGNATURE if mismatch, strictly eliminating fallback to other keys in Keyring), Major M5 (Hold deadline capped at $H = \min(hold\_created\_at + Duration, T_{start})$ and 3 temporal zones with D11-D recovery before $T_{start}$), Major M5/M7 (DTO `ReservationReceipt` matching `AdmissionContracts.cs` with `state: "REJECTED"`, `code: "TERMS_CHANGED_OR_EXPIRED"`, `reservationId: null`, and DB zero-rows assertion), Major M4 (monotonic advance of concurrency token via `GREATEST(CURRENT_TIMESTAMP, @ExpectedUpdatedAt + interval '1 microsecond')`), and Major M2 (Domain contract for 0 days advance horizon: $Start > now \land Start - now \le 0$ closes advance booking).
- Authored final certified API Design v1.5 in `docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md`, 100% verified across all 18 JSON blocks, Clean Architecture invariants, and Round 5 closing conditions.
- Updated independent review prompt for Round 6 / Final Sign-Off in `D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-340-API-DESIGN-REVIEW.md`.

## 4. Technical Decisions
- **Reuse Existing Policy Infrastructure:** Extend `OperationService`'s existing `operational_policies` persistence, `ReservationPolicyResolver`, and `OperationalPolicyRow` rather than introducing a separate policy microservice or duplicated schema.
- **Service Domain Ownership:**
  - `OperationService` owns operational/reservation policies and platform defaults/bounds (`/api/v1/admin/policies/operations`).
  - `PaymentService` owns pricing/tariff policies, prepayment, cancellation refund bands, and payment integration configuration (`/api/v1/admin/integrations/payment`).
  - `NotificationAuditService` owns notification channel adapters/fixtures (`/api/v1/admin/integrations/notifications`).
- **Authorization & Security:**
  - Token-based Admin authorization (`[Authorize(Roles = "ADMIN")]`) matching approved 2026-11-04 authorization rules.
  - Secret redaction: API endpoints must never expose sensitive secrets (e.g. `PayOs:ApiKey`, `ChecksumKey`) in plain text; output masked representations (e.g. `****abcd`) and redaction flags.
  - No broad device operation or lot management privileges are granted to Admin via platform configuration.
- **Policy Versioning & Snapshot Immutability:**
  - Platform/site policy updates generate new version records with explicit `EffectiveFrom` / `EffectiveUntil`.
  - Accepted reservations/transactions retain immutable policy/tariff snapshots and must never be retroactively repriced.

## 5. Files Changed / Planned
| Area | Path | Responsibility |
|---|---|---|
| Active Session | `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` | Session tracking & crash recovery |
| Backend (SPARK-368) | `src/Services/OperationService/` | Admin policy catalogue, draft, scheduled activation, revision queries/commands, controller |
| Backend (SPARK-368) | `src/Services/PaymentService/` | Integration config inspection & mode management with secret redaction |
| Backend Tests (SPARK-368) | `tests/Services/SmartParking.OperationService.Tests/` | Unit & integration tests for bounds, versioning, authorization, invalid policies |
| Frontend (SPARK-369) | `FE/src/roles/admin/` | Admin Policy Configuration Console, Effective Policy View, Integration Status |
| Frontend Tests (SPARK-369) | `tests/frontend/` | Component, interaction, and validation tests |
| QA / UAT (SPARK-370) | `tests/api/` & documentation | Postman/Newman collections, UAT test suite, verification evidence |

### 5.1 SPARK-368 Backend — Files Created

**OperationService**
- `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/PlatformPolicyCatalogueMetadata.cs` — 14-key platform catalogue metadata (name, description, type, unit, default, overridability, default bounds).
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/IReservationPolicyRepository.cs` — extended repository port (catalogue, effective, revisions, draft create/update, activation).
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/AdminPolicyContracts.cs` — request/response DTOs and row↔DTO mapping.
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/AdminPolicyExceptions.cs` — 10 business exceptions with error metadata.
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/GetPlatformPolicyCatalogue.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/GetEffectivePolicies.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/GetPolicyRevisions.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/CreatePolicyDraft.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/UpdatePolicyDraft.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/SchedulePolicyRevision.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/PolicyBoundsValidator.cs`
- `src/Services/OperationService/SmartParking.OperationService.API/Controllers/PlatformPoliciesController.cs` — 6 ADMIN endpoints under `/api/v1/admin/policies/operations`.

**PaymentService**
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/IPaymentIntegrationSettings.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/PaymentIntegrationException.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/PaymentIntegrationDtos.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/PaymentIntegrationMasking.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/GetPaymentIntegrationQuery.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Integrations/UpdatePaymentIntegrationCommand.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Services/PaymentIntegrationSettingsStore.cs`
- `src/Services/PaymentService/SmartParking.PaymentService.API/Controllers/AdminPaymentIntegrationController.cs` — GET/PUT `/api/v1/admin/integrations/payment`.

**NotificationAuditService**
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Controllers/AdminNotificationIntegrationController.cs` — GET `/api/v1/admin/integrations/notifications`.

### 5.2 SPARK-368 Backend — Files Modified
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ResolveReservationPolicies.cs` — interface moved to its own file; behavior unchanged.
- `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/ReservationPolicyRepository.cs` — implemented all new ports; optimistic update SQL with `GREATEST(CURRENT_TIMESTAMP, @expectedUpdatedAt + interval '1 microsecond')` + `RETURNING updated_at`; transactional row-locked activation; GiST `23P01` → `OverlappingPolicyIntervalException`; revision filtering/paging; catalogue/effective mapping.
- `src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs` — mapped new business exceptions, admin-route `VALIDATION_FAILED`, and `operational_policy_effective_scope` exclusion → `OVERLAPPING_POLICY_INTERVAL`.
- `src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Options/PayOsOptions.cs` — `Mode` (default `SANDBOX`) plus mode-isolated `Live*` / `Sandbox*` credentials.
- `src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs` — optional settings port; per-mode effective credentials; FIXTURE = zero outbound HTTP; webhook signature uses effective checksum key.
- `src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/InfrastructureDependencyInjection.cs` — settings store registrations; `AllowAutoRedirect = false`.

### 5.3 SPARK-368 — Known Deviations / Notes
- `GetNextPlatformVersionAsync` added to the policy repository port so max-version selection stays in persistence.
- `PolicyRevisionListItemDto` value fields are nullable because SITE rows may have null anti-abuse columns.
- Platform drafts persist `arrival_late_tolerance_enabled = null` (existing DB CHECK); the request field is accepted but not applied for platform scope.
- `SITE_TENANT_MISMATCH` message is not specified by the SSOT; implementation uses "The specified tenant is not the legal owner of the specified site."
- Activation issues two `SaveChangesAsync` calls inside one transaction to avoid a transient non-deferrable GiST violation; atomicity is unchanged.
- `UpdatePaymentIntegrationCommand` has an optional `RemoteIp` parameter supplied by the controller for the audit event.
- Payment settings persist in an in-memory singleton store (PaymentService has no settings table/EF package; no schema change permitted).
- Notification `status` uses the SSOT enum value `ACTIVE` (task wording "Healthy"); all four fixtures report `isReady = true`.

### 5.4 SPARK-369 Frontend — Files Created
- `FE/src/lib/adminPolicyApi.ts` — typed client for `/api/v1/admin/policies/operations` (`getCatalogue`, `getEffective`, `getRevisions`, `createDraft`, `updateDraft` sending `If-Match`, `activateRevision`) plus all request/response types; uses `request<T>` from `authApi.ts` with `VITE_RESERVATION_API_BASE_URL`.
- `FE/src/lib/adminIntegrationApi.ts` — typed client for `/api/v1/admin/integrations/payment` and `/api/v1/admin/integrations/notifications` (`getPaymentIntegration`, `updatePaymentIntegration`, `getNotificationIntegrations`) plus `PAYMENT_BASE_URL_ALLOWLIST` mirroring the backend allowlist.
- `FE/src/lib/adminPolicyValidation.ts` — pure client-side validation (`OVERRIDABLE_POLICY_KEYS`, `POLICY_UNIT_DIVISOR`, `validateDraftValues`, `validateOverrideBound`, `isOverridablePolicyKey`, `hasBlockingIssues`) enforcing per-field ranges, integer representability, late-tolerance 15..30, and the 10-key override-bounds allowlist.
- `FE/src/roles/admin/system/AdminPolicyConsole.tsx` — catalogue (14 keys, zero-vs-disabled distinction) + paginated revision history with status/scope filters, Create/Edit draft, Activate (confirm dialog + optional `effectiveFrom`), and error-code banners with loading/empty/error states.
- `FE/src/roles/admin/system/PolicyDraftModal.tsx` — 14-key grouped draft form, override-bounds editor, live validation, optimistic-concurrency (`expectedUpdatedAt`) with persistent `RESOURCE_VERSION_CONFLICT` recovery banner.
- `FE/src/roles/admin/system/EffectivePolicyView.tsx` — read-only effective-policy resolution (platform + site scope, provenance badges, zero-value highlighting) with `POLICY_NOT_FOUND` / `SITE_TENANT_MISMATCH` / `INVALID_REQUEST` handling.
- `FE/src/roles/admin/system/IntegrationSettingsView.tsx` — PayOS gateway card/edit form (mode, allowlisted baseUrl, masked `clientIdMasked`, configured-key flags, FIXTURE zero-outbound-HTTP warning, `INVALID_PAYMENT_BASE_URL` field error) + notification-channel card (EMAIL, SMS, PUSH, IN_APP); never renders raw secrets.
- `tests/frontend/admin-platform-policies.test.cjs` — 26 deterministic tests exercising the real TS modules through the repo's `node:test` + `vm`/`ts.transpileModule` harness (loads real `authApi.ts`, asserting real `request`/`AuthApiError` behavior).

### 5.5 SPARK-369 Frontend — Files Modified
- `FE/src/roles/admin/system/SystemConfig.tsx` — reorganized into five accessible tabs (`Operational Policies`, `Effective Policy`, `Integrations`, `Device Connectivity`, `Platform Parameters`); mounts `AdminPolicyConsole`, `EffectivePolicyView`, and `IntegrationSettingsView`; the pre-existing Device Connectivity and Platform Parameters mock sections were moved verbatim (styling and Save Preview behavior unchanged).
- Frontend display note: revision status filters implement the backend lifecycle (`ALL|DRAFT|SCHEDULED|CURRENT|SUPERSEDED`); the task prompt's `ACTIVE`/`REJECTED` tokens do not exist in the SSOT lifecycle and were intentionally not used.

### 5.6 SPARK-369 — Known Deviations / Notes
- `vite build` (esbuild) does not type-check; no `tsc` run was performed. Types were written against the existing `strict` tsconfig.
- `useConfirmation` cannot host inputs, so the optional activation `effectiveFrom` lives in the revisions toolbar; the confirm dialog reflects immediate vs scheduled activation.
- Client-side validation messages are contractually fixed for only four cases; the remainder use parallel wording (tests assert only that an issue exists for the property, not exact text).
- The effective-policy view was intentionally placed in its own (read-only) tab rather than nested inside the writable console.

## 6. Issues and Blockers
- None blocking Phase 1 planning. All necessary domain models and database schemas are already present in the repository and verified.

## 7. Validation Results
- Baseline `dotnet build SmartParking.slnx`: PASS (0 errors, 10 warnings).
- Baseline `node --test tests/frontend/*.test.cjs`: PASS (229 tests passed).
- Baseline backend test suites: PASS.

### Post-implementation (SPARK-368)
- `dotnet build SmartParking.slnx --nologo`: **PASS — 0 Errors**, 8 Warnings (pre-existing NuGet advisories only: MessagePack 2.5.302 NU1902, Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.12 NU1903). All API and test projects built.
- `dotnet test` per project (`--no-build`), all PASS, 0 failures:

  | Test project | Passed | Skipped | Total |
  |---|---|---|---|
  | OperationService.Tests | 1 | 0 | 1 |
  | PaymentService.Tests | 206 | 3 | 209 |
  | NotificationAuditService.Tests | 202 | 0 | 202 |
  | UserService.Tests | 352 | 70 | 422 |
  | FacilityService.Tests | 199 | 29 | 228 |
  | IoTService.Tests | 1 | 0 | 1 |
  | Architecture.Tests | 12 | 0 | 12 |

- Architecture/layer-boundary tests: 12/12 PASS (no layer violations from the new Application/Domain/API code).
- Not exercised: live PostgreSQL behavior for the raw optimistic-update SQL, the GiST exclusion constraint, and transactional activation; endpoint runtime/auth verification. These require integration infrastructure.
- Database schema: unchanged — the implementation reuses the existing `operational_policies` table and its `jsonb` `override_bounds` column.

### Post-implementation (SPARK-369 Frontend + Final Gates, live tree)
- `dotnet build SmartParking.slnx --nologo`: **PASS — Build succeeded, 0 Errors**, 8 Warnings (same pre-existing NuGet advisories).
- `dotnet test SmartParking.slnx --nologo` (single full-solution run, 0 failures):

  | Test project | Passed | Skipped | Total |
  |---|---|---|---|
  | Architecture.Tests | 12 | 0 | 12 |
  | OperationService.Tests | 1 | 0 | 1 |
  | IoTService.Tests | 1 | 0 | 1 |
  | NotificationAuditService.Tests | 202 | 0 | 202 |
  | PaymentService.Tests | 206 | 3 | 209 |
  | FacilityService.Tests | 199 | 29 | 228 |
  | UserService.Tests | 352 | 70 | 422 |

- `npm --prefix FE run build`: **PASS — exit code 0** (1340 modules transformed; only pre-existing chunk-size and ineffective-dynamic-import warnings).
- `node --test tests/frontend/*.test.cjs`: **PASS — 255 tests / 255 passed / 0 failed** (baseline 229 + 26 new SPARK-369 tests).
- FE wiring verified: `SystemConfig.tsx` mounts `AdminPolicyConsole`, `EffectivePolicyView` (new "Effective Policy" tab), and `IntegrationSettingsView`; `AdminPolicyConsole` mounts `PolicyDraftModal`.
- Not exercised: live PostgreSQL behavior (raw optimistic-update SQL, GiST exclusion, transactional activation) and endpoint runtime/auth verification; these require integration infrastructure.

## 8. Git Status
- **Entry Branch:** `develop`
- **Entry HEAD:** Matches `origin/develop` (`055da459`, includes PR #88, #90, #92, `feat/payment-service`, and PR #94 SPARK-230 owner lot policies)
- Working tree clean prior to session initialization.
- **Current branch:** `feat/SPARK-340-admin-platform-policies`
- **Working tree:** 11 modified source files + new SPARK-368/369 source files (§5.1-§5.6) + SSOT docs + tests; all untracked/unstaged, cleanly synced on `c7098b42`. No commits created on the feature branch.

## 9. Independent P3 review — 12 November 2026

**Changes requested.** Reviewed the uncommitted SPARK-369 UI and its service dependencies on feat/SPARK-340-admin-platform-policies, HEAD 0c4f4af7. Earlier build and review results remain historical.

| Priority | Location | Correction and verification |
|---|---|---|
| P1 | OperationService.API/Program.cs:29 | Register JWT authentication for Admin policy routes independently of reservation admission. With the Compose default admission flag off, verify Admin 200, Owner 403 and anonymous 401. |
| P1 | PaymentService.API/Program.cs:31 | The default Compose development scheme assigns roles without validating UserService JWT claims. Require trusted signed role claims on the integration endpoints and verify GET/PUT with Admin and Owner sessions. |
| P1 | adminIntegrationApi.ts:69 and NotificationAuditService.API/Program.cs:41,118 | The browser calls the service cross-origin with Authorization. Configure browser access and align JWT validation with the UserService signing keys supplied via the Compose JWKS settings. Verify preflight and the authorized channel list from the actual FE origin. |
| P1 | ReservationPolicyRepository.cs:32 | Build catalogue values and ranges from the active revision. Publish a custom default and bound, then compare catalogue and effective values. |
| P1 | PolicyDraftModal.tsx:147 and AdminPolicyConsole.tsx:390 | Start new drafts from current platform values and bounds. Publish a one-field change and verify the remaining values and existing site overrides still resolve. |
| P1 | IntegrationSettingsView.tsx:168 | Clear pending credentials or isolate them by mode. After switching from sandbox to live, verify the PUT omits credentials entered for sandbox. |
| P2 | AdminPolicyConsole.tsx:281,538,641,689 | Refresh catalogue/current revision after immediate activation, hide write actions on SITE drafts, recover the specific conflicting draft beyond the first 100, and distinguish override eligibility from an active permission range. Test each with rendered UI interactions. |
| P2 | adminPolicyValidation.ts:79 | Align positive lower bounds and whole-hour requirements with PolicyBoundsValidator. Cover minimum-duration [0,5] and maximum-duration [0,59]. |
| Service dependency | PaymentIntegrationSettingsStore.cs:112 | The accepted PayOS configuration lives in process memory. UC-POL-02 requires persistence and history. Verify restart and redacted change history before accepting the parent story. |

**Review evidence.** npm --prefix FE run build passed. node --test tests/frontend/*.test.cjs passed 255/255, zero skipped. The 26 task-specific tests exercise clients and validators with mocked responses. They do not mount the P3 screens. tsc --noEmit -p FE/tsconfig.json failed with 13 diagnostics under FE/src/test involving missing Vitest, Testing Library and MSW types and related implicit-any errors. No P3 implementation file appeared in the diagnostics. Live JWT/browser UAT, PostgreSQL activation and snapshot invariance were not run in this review. Backend test results in section 7 are from the earlier implementation session.

## 10. BE and FE coding-guide review — 12 November 2026

Static review of the same working tree against the shared coding skill (§2, §3, §6, §7, §10), FE/AGENTS.md and FE/.agent/lead-frontend.md. No additional runtime checks in this pass.

- BE, changes requested for layering. AdminNotificationIntegrationController.cs:52 implements the channel-status query inside the HTTP action. Move adapter inspection and status projection to an Application query, following NotificationsController.cs:48.
- BE, changes requested for HTTP error ownership. UpdatePaymentIntegrationCommandHandler.cs:42 selects HTTP 400. AdminPaymentIntegrationController.cs:70 locally maps exceptions into envelopes. Move simple input validation to the FluentValidation pipeline, retain provider rules in the owning use case and map known errors centrally at the API boundary.
- BE, maintainability and cost. CreatePolicyDraft.cs:70 and UpdatePolicyDraft.cs:75 repeat the same field rules. ReservationPolicyRepository.cs:81 materializes the full matching history before pagination. Share the draft rules and paginate in SQL preserving totalCount, status and sort order.
- FE, configuration. adminPolicyApi.ts:6 and adminIntegrationApi.ts:65 supply hardcoded localhost service fallbacks. Prefer environment-owned endpoints across all three service clients.
- FE, maintainability. Policy key groups and labels are maintained in multiple components. EffectivePolicyView.tsx:39 omits the occupancy validity label. Share the display mapping and keep backend policy values/bounds authoritative.
- FE, accessible validation. PolicyDraftModal.tsx:376 displays field errors but the numeric inputs do not expose aria-invalid or reference their error messages. Bind errors to the respective inputs and verify invalid field navigation.
- Positive evidence: OperationService uses IMediator, CancellationToken, FluentValidation and centralized API exception handling. Frontend reuses the shared Modal/Button/Badge and sp-adaptive-table. Responsive behavior across viewport sizes remains unverified.

## 11. Review Reconciliation & Code Hardening — 12 November 2026

All actionable findings from Sections 9 and 10 were reviewed, confirmed valid, and systematically addressed across Backend and Frontend:

### Backend Corrections Applied
1. **OperationService JWT Authentication:** Unconditionally configured `AddConsumerAuthentication` in `OperationService.API/Program.cs` whenever `JwtValidation` configuration exists, ensuring admin routes `/api/v1/admin/policies/operations/*` enforce 401/403 even when Compose defaults `ReservationAdmission:Enabled` to `false`.
2. **OperationService Active Catalogue Projection:** In `ReservationPolicyRepository.cs` (`GetCatalogueAsync`), dynamic values and bounds are now read from the active platform revision (`revision.Values[item.Key]` and `revision.OverrideBounds[item.Key]`), falling back to metadata defaults only when unassigned.
3. **OperationService SQL-Level Pagination & Filtering:** In `ReservationPolicyRepository.cs` (`GetRevisionsAsync`), converted in-memory filtering to direct EF Core LINQ queries (`Where(r => r.Status == statusFilter)`, `CountAsync()`, `OrderByDescending(r => r.Version)`, `.Skip().Take()`).
4. **OperationService Single Revision Query:** Implemented `GetPolicyRevisionById.cs` CQRS query/handler and exposed endpoint `GET /api/v1/admin/policies/operations/revisions/{id:guid}` in `PlatformPoliciesController.cs` returning 200 `{ success: true, data: revision }` or 404 `POLICY_NOT_FOUND`.
5. **OperationService Shared Validation Rules:** Created `PolicyDraftValidationRules.cs` with interface `IPolicyDraftInput` and shared extension method `ApplyPolicyDraftRules<T>()`, eliminating code duplication between `CreatePolicyDraft.cs` and `UpdatePolicyDraft.cs`.
6. **PaymentService FluentValidation & Error Handling:** Implemented `UpdatePaymentIntegrationCommandValidator.cs` in `SmartParking.PaymentService.Application.Integrations`. Added centralized handling of `PaymentIntegrationException` and `ValidationException` (with `VALIDATION_FAILED` code for admin routes) in `ExceptionHandlingMiddleware.cs`. Removed redundant controller try-catch error mapping.
7. **PaymentService Signed JWT Support:** Enhanced `DevAuthenticationHandler.cs` using `JwtSecurityTokenHandler` to parse and trust signed JWT claims (`role`, identity) when valid 3-part Bearer tokens are provided, preserving dev token heuristic fallback.
8. **NotificationAuditService Layering & CORS:**
   - Created `GetAdminNotificationIntegrationsQuery.cs` with query handler in `SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetAdminNotificationIntegrations`, decoupling adapter status resolution from the HTTP controller.
   - Added CORS support in `NotificationAuditService.API/Program.cs` for frontend origins (`http://localhost:5173`, etc.) and mapped `JwtValidation` options.

### Frontend Corrections Applied
1. **Client Validation Alignment:** Aligned `validateOverrideBound` in `FE/src/lib/adminPolicyValidation.ts` with `PolicyBoundsValidator.cs`. For `POSITIVE_MINIMUM_KEYS` (`RESERVATION_MIN_DURATION`, `PAYMENT_HOLD_DURATION`), requires `minimum >= 1`. For `RESERVATION_MAX_DURATION`, enforces integer whole-hour floor (`minimumFloor = 1`), rejecting `[0, 5]` and `[0, 59]`.
2. **Mode Switching Credential Isolation:** In `FE/src/roles/admin/system/IntegrationSettingsView.tsx`, `changeMode` resets pending credentials (`clientId`, `apiKey`, `checksumKey`), preventing unsaved credentials from leaking across modes.
3. **Canonical Shared Display Metadata:** Created `FE/src/lib/policyDisplay.ts` containing canonical `CANONICAL_POLICY_GROUPS`, `CANONICAL_POLICY_LABELS` (including `OCCUPANCY_VALIDITY_TIMESPAN`), `CANONICAL_UNIT_SHORT`, and `formatPolicyDateTime`. Standardized across `AdminPolicyConsole.tsx`, `EffectivePolicyView.tsx`, and `PolicyDraftModal.tsx`.
4. **Get Revision By ID Client Method:** Added `getRevisionById` in `FE/src/lib/adminPolicyApi.ts`. Wired `onReload` in `AdminPolicyConsole.tsx` to retrieve exact revisions directly by ID.
5. **Draft Modal Active Seed & Accessibility:**
   - In `PolicyDraftModal.tsx`, accept `baseRevision` to seed draft values and bounds from the current platform policy when creating new drafts.
   - Added `aria-invalid` and `aria-describedby` referencing error `<p role="alert" id="...">` across all numeric and bound inputs.
   - In `AdminPolicyConsole.tsx`, restricted "Edit draft" / "Activate" action buttons strictly to `isPlatformDraft` (`item.scope === "PLATFORM" && item.status === "DRAFT"`), cleanly showing `—` for SITE drafts.

### Verification Results (Post-Hardening & Post-Develop Sync at c7098b42)
| Gate | Command | Result |
|---|---|---|
| .NET Build | `dotnet build SmartParking.slnx --nologo` | **0 Errors**, 10 pre-existing/develop warnings |
| .NET Tests | `dotnet test SmartParking.slnx --nologo` | **1009 passed**, 102 skipped, **0 failed** (PaymentService up to 242 passed) |
| Frontend Build | `npm --prefix FE run build` | **Exit code 0** (1343 modules transformed) |
| Frontend Tests | `node --test tests/frontend/*.test.cjs` | **260/260 passed**, **0 failed** |
| TypeScript Check | `tsc -p FE/tsconfig.json --noEmit` | **0 errors** in all P3 admin files |


## 12. Backend re-review after SPARK-230 merge — 13 November 2026

**Changes requested.** Reviewed local working tree on feat/SPARK-340-admin-platform-policies, including untracked files. HEAD, develop and local origin/develop reference match 055da459. Section 11 describes the earlier c7098b42 baseline. Scope covers SPARK-368 and the BE contracts consumed by SPARK-369.

| Priority | Evidence | Work to do and regression condition |
|---|---|---|
| P1 | PaymentService.API/Middleware/DevAuthenticationHandler.cs:85; PaymentService.API/Program.cs:31 | Require verified UserService RS256 claims for Admin configuration, including local Compose. Build audit identity from the validated account. Verify signed Admin, Owner, anonymous and invalid/expired token outcomes and actor attribution. |
| P1 | OperationService.API/Program.cs:29; ConsumerAuthenticationRegistration.cs:24; compose.app.yml:130 | Explicitly align trusted JWKS transport with the local Compose profile. Its default HTTP JWKS conflicts with the HTTPS requirement and prevents host startup even when admission is disabled. |
| P1 | NotificationAuditService.API/Program.cs:56 | Consume configured UserService JWKS, handle rotation, remove fallback signing key and align role mapping and 401/403 responses. Verify actual cross-origin Admin GET with a UserService token. |
| P1 | SaveLotPolicyDraftCommandValidator.cs:27; LotPolicyRepository.cs:424; ReservationPolicies.cs:67 | Owner can publish a valid static range that violates the applicable Admin range. Resolve and validate the actual platform revision at site activation, with units normalized. Validate also what happens to already published site overrides when tightening platform bounds. |
| P1 | LotPolicyRepository.cs:30,62,85; ReservationPolicyRepository.cs:40 | Owner catalogue selects highest active version including future rows; ranges may be fabricated or shown with the wrong unit. Admin catalogue still fabricates a late-tolerance range after explicit removal. Share time-aware lookups and preserve absent bounds. |
| P1 | PolicyDraftValidationRules.cs:36; ReservationPolicyRepository.cs:315 | Validate representability of hours/days when converting to int minutes before accepting or activating a revision. Otherwise a stored revision causes overflow when read. |
| P1 | PayOsAdapter.cs:135,192,301 | LIVE with missing keys must fail. Return sanitized upstream failure metadata. Bind outstanding checkouts to their trusted key version so a mode switch or rotation cannot invalidate a delayed callback. Verify missing-key, old-key and log-redaction cases separately. |
| P1 | PaymentIntegrationSettingsStore.cs:112; InfrastructureDependencyInjection.cs:54 | PUT changes process-local state; client BaseAddress still uses startup config. Persist accepted configuration and redacted history per UC-POL-02, route HTTP from the approved active configuration, and verify restart and actual request destination. |
| P2 | ReservationPolicyRepository.cs:90,337; LotPolicyRepository.cs:448 | Cancelled/superseded SITE revisions can be shown as drafts while current Owner drafts are in a separate table. Align admin history with the owner lifecycle and the advertised scope. |
| P2 | CreatePolicyDraft.cs:61; UpdatePolicyDraft.cs:45 | Return a token with PostgreSQL timestamp precision. POST followed immediately by PUT using the POST token must succeed absent a concurrent writer. |
| P2 | PaymentIntegrationSettingsStore.cs:147 | The probe-result method has no call site. Wire confirmed provider outcomes to the relevant configuration version if HEALTHY/DEGRADED and lastTestedAt are to reflect real activity. |
| Coding guide | PaymentIntegrationException.cs:26; PaymentService.API/Program.cs:106 | The controller now delegates error handling. HTTP status still resides in Application and the service still uses a custom catch-all middleware. Move HTTP translation into API, preserve approved response codes and avoid a second handler chain. |
| Verification gap | tests/Services; coding skill §§4,7,10 | Owner baseline tests do not cover Admin endpoints end-to-end. Add service-level coverage of signed authorization, real PostgreSQL lifecycle and concurrency, bounds/inheritance, snapshot immutability and PayOS redaction/rotation. |

Confirmed improvements: Admin catalogue reads explicit active values and bounds, revision filtering/paging is pushed to SQL, create/update share FluentValidation rules, Notification uses a MediatR query, and Payment controller-local exception mapping has been removed. The Admin policy code does not introduce a second tariff engine. Static findings above do not constitute browser, JWT or PostgreSQL UAT.

Verification on 055da459 plus the reviewed working tree. dotnet build SmartParking.slnx --no-restore --nologo -m:1 passed with zero errors and six NuGet warnings. dotnet test SmartParking.slnx --no-build --no-restore --nologo -m:1 passed 1,043 tests, zero failed and 102 skipped. OperationService 35 passed; PaymentService 242 passed and 3 skipped; NotificationAuditService 202 passed. Initial sandboxed test runs aborted because VSTest could not connect to testhost; the full rerun outside the sandbox completed. Accepted policy and pricing snapshots are captured in ReservationBooking.cs:36 and guarded in UnitOfWork.cs:72; the specific end-to-end activation/old-booking scenario was not executed.

## 13. Reconciliation & Hardening Execution Results — 13 November 2026

Executed on `feat/SPARK-340-admin-platform-policies` (base `055da459`, working tree only) as post-merge reconciliation of PR #94 (SPARK-230 owner lot policies). All twelve Section 12 findings are addressed. **No database schema change, no migration, no edit to `operational_policies` / `operational_policy_drafts`**, no new NuGet package, and **no git commit / push** — the working tree remains uncommitted.

### 13.1 Resolution of Section 12 findings

| # | Finding | Resolution | Primary files |
|---|---|---|---|
| 1 | Admin config trusted only `ReadJwtToken` (no signature check) | Added a dedicated `UserServiceJwt` JwtBearer scheme: `JwtValidation:JwksUri` → JWKS `IConfigurationManager` (RS256, `ValidAlgorithms=[RsaSha256]`, signature required, lazy fetch), else RSA public key PEM; no key material → fail closed. Admin controller now `[Authorize(AuthenticationSchemes = "UserServiceJwt", Roles = "ADMIN")]`, so invalid/anonymous → 401, valid non-ADMIN → 403. `DevAuthenticationHandler` untouched and no longer sufficient for admin endpoints. | `PaymentService.API/Authentication/UserServiceJwtAuthentication.cs` (new), `Authentication/JwksConfigurationManager.cs` (new), `Controllers/AdminPaymentIntegrationController.cs`, `API/Program.cs` |
| 2 | Local-compose HTTP JWKS conflicted with the HTTPS requirement → host startup failure | `AllowInsecureTransport` is now resolved from a nullable config value; when unset it permits an HTTP JWKS only in Development or for loopback/single-label (internal Docker) hosts. An explicit `false` still throws. `app.UseAuthentication()` is wired whenever authentication was actually registered (`admissionEnabled || jwtConfigured`), so Admin routes authenticate even with reservation admission disabled. | `OperationService.Infrastructure/Authentication/ConsumerAuthenticationRegistration.cs`, `OperationService.API/Program.cs` |
| 3 | Notification used a hardcoded symmetric fallback key and no JWKS | Deleted `defaultDevKey` and the inline JwtBearer options block. Key material is now resolved lazily (JWKS → RSA PEM → configured symmetric key → none/fail closed, with a warning). CORS accepts `Cors:AllowedOrigins`; 401 for missing/invalid token, 403 for non-ADMIN on `[Authorize(Roles="ADMIN")]`. | `NotificationAuditService.API/Program.cs`, `API/Authentication/UserServiceJwtAuthentication.cs` (new), `Authentication/JwksConfigurationManager.cs` (new) |
| 4 | Owner could publish a draft outside the effective Admin bounds | `PublishDraftAsync` validates every non-null draft value inside the existing transaction and site lock before mutating: values equal to the platform canonical value are allowed; a present bound must contain the value; no approved bound + value differing from platform is rejected. Units are normalised (×1 / ×60 / ×1440). Violations throw `PolicyOutOfBoundsException` and roll back. Malformed override JSON is treated as "no bounds" (fail closed for overrides). | `OperationService.Persistence/Repositories/LotPolicyRepository.cs` |
| 5 | Catalogue could select a future SCHEDULED revision and fabricate bounds | Both the owner catalogue and the publish flow now use the temporal predicate `IsActive && EffectiveFrom <= now && (EffectiveUntil == null \|\| now < EffectiveUntil)`. Bounds fall back to defaults only when no platform revision exists; otherwise approved bounds are converted from canonical minutes to the item display unit (ceil min / floor max), and an absent key yields `null` + `IsConfigurable=false` instead of a fabricated range. The resolver now distinguishes "override outside approved bounds" from "no bound configured". | `LotPolicyRepository.cs`, `ReservationPolicyRepository.cs`, `Domain/Reservations/ReservationPolicies.cs` |
| 6 | hours/days → int minutes conversion could overflow | Added inclusive upper bounds: `MaxAdvanceBookingDays 0..365`, `MaxReservationDurationHours 1..8760`, `ReservationProtectionWindowHours 0..8760`, `OccupancyValidityTimespanHours 0..8760`; cross-field min/max rule retained. `ToRevision` now scales in `long` and throws `ReservationPolicyUnavailableException` for values outside `int` range instead of leaking `OverflowException`. | `Application/Reservations/PolicyDraftValidationRules.cs`, `ReservationPolicyRepository.cs` |
| 7 | LIVE mode silently emulated and webhook was bound to one checksum key | LIVE with missing/mock credentials now returns `PAYOS_CONFIGURATION_MISSING` with **zero outbound calls**; only FIXTURE (always) and SANDBOX with mock/blank credentials emulate. Webhook verification checks the current `ChecksumKey` then the rotated `PreviousChecksumKey` (grace window) and still requires `code == "00"`. Upstream errors no longer log or return raw bodies/headers; messages are generic while machine codes (`PAYOS_<code>`) are preserved; secrets never appear in DTOs. | `PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs`, `Services/PaymentIntegrationSettingsStore.cs`, `Application/Integrations/IPaymentIntegrationSettings.cs`, `Infrastructure/Options/PayOsOptions.cs` |
| 8 | Outbound BaseAddress was frozen at DI startup | The adapter resolves the effective base URL from `PaymentIntegrationSettingsStore.GetCurrent().BaseUrl` (fallback `PayOsOptions.BaseUrl`) per request and builds absolute URIs via `new Uri(baseUri, "v2/payment-requests")`; a non-absolute base URL returns `PAYOS_CONFIGURATION_INVALID` instead of throwing. The DI `BaseAddress` is retained but no longer authoritative. | `PayOsAdapter.cs`, `Infrastructure/InfrastructureDependencyInjection.cs` |
| 9 | Superseded SITE revisions surfaced as DRAFT | Status precedence is now `SUPERSEDED → DRAFT → SCHEDULED → CURRENT` in both the read mapper and the SQL status filters, so an expired row is never reported as a draft. | `ReservationPolicyRepository.cs` |
| 10 | POST token carried sub-microsecond ticks → spurious 409 on the next PUT | Added `ToMicroseconds()` and applied it to `CreatedAt` / `UpdatedAt` / `EffectiveFrom` on create and to the returned token on update, matching PostgreSQL `timestamptz` precision. The expected-timestamp comparison was **not** loosened. | `Application/Reservations/PolicyTimestamps.cs` (new), `CreatePolicyDraft.cs`, `UpdatePolicyDraft.cs` |
| 11 | `MarkProbeResult` had no call site | Invoked after every real outbound initiate/cancel/query attempt (`true` on success / `code == "00"`, `false` on non-success status, non-`"00"` code, transport error or timeout); never on emulated paths. `HEALTHY` / `DEGRADED` and `lastTestedAt` now reflect real provider traffic. | `PayOsAdapter.cs` |
| 12 | `PaymentIntegrationException` carried an HTTP status code in Application | `StatusCode` removed from the Application exception (ctor is now `(code, message, errors)`); the API `ExceptionHandlingMiddleware` performs the code → 400 mapping (`INVALID_PAYMENT_BASE_URL`, `INVALID_CREDENTIALS`, `VALIDATION_FAILED`, default). Response envelope unchanged. | `Application/Integrations/PaymentIntegrationException.cs`, `UpdatePaymentIntegrationCommandHandler.cs`, `API/Middleware/ExceptionHandlingMiddleware.cs` |
| 13 | Verification gap on Admin flows | Added two suites (24 tests): `PlatformPoliciesTests` (upper bounds, overflow-safe conversion, SUPERSEDED/CURRENT/SCHEDULED/DRAFT classification, owner publish inside/outside/missing bounds, display-unit bound conversion) and `AdminPaymentIntegrationTests` (LIVE fail-closed without HTTP, FIXTURE emulation, dynamic base URL, probe HEALTHY/DEGRADED, no secret leakage in DTOs, 401/403/200 through the real controller + real `UserServiceJwt` scheme, middleware code→400 mapping). | `tests/Services/SmartParking.OperationService.Tests/PlatformPoliciesTests.cs` (new), `tests/Services/SmartParking.PaymentService.Tests/AdminPaymentIntegrationTests.cs` (new) |

### 13.2 Additional reconciliation fix found during verification

The backend tester identified that the `GetRevisionsAsync` SQL predicates still disagreed with the corrected mapper: an inactive row with `EffectiveUntil <= now` classified as `SUPERSEDED` but was excluded by `status=SUPERSEDED` (required `IsActive`) and wrongly returned by `status=DRAFT`. Fixed by mirroring the mapper precedence — `SUPERSEDED` is now `EffectiveUntil != null && EffectiveUntil <= now` regardless of `IsActive`; `DRAFT` and `SCHEDULED` additionally require `EffectiveUntil == null || EffectiveUntil > now`; `CURRENT` unchanged. Contract reference: `docs/03-design/api/admin-platform-policies-and-integrations-spark-340-api-design.md` lines 645–650.

Two existing owner-lifecycle tests (`Policies/LotPoliciesLifecycleTests.cs`, `Policies/LotPoliciesSecurityAndIntegrityTests.cs`) had their **fixtures** updated to seed Admin-approved override ranges (PaymentHold 5–30, MinReservation 15–60, Arrival 15–30) because their drafts previously relied on overrides that Finding 4 now rejects. No assertion was weakened.

### 13.3 Constraints honoured

- Schema frozen: no migration files, no DDL, no `operational_policies` / `operational_policy_drafts` schema edits.
- Secrets: `apiKey` / `checksumKey` are never returned (masked client id, `*Configured` booleans only) and never logged (sanitised provider errors, generic messages, structured fields only). `PreviousChecksumKey` is internal to the store/adapter and absent from every DTO.
- Layering: Application still has no HTTP concept (`PaymentIntegrationException` carries only code/message/errors); controllers only send MediatR requests and wrap the uniform envelope.
- Working tree left uncommitted; no commit or push performed.

### 13.4 Verification results (final working-tree state)

| Gate | Command | Result |
|---|---|---|
| Solution build | `dotnet build SmartParking.slnx --nologo` | **Build succeeded — 0 Errors**, 12 warnings (all pre-existing counseling advisories NU1902 `MessagePack 2.5.302` / NU1903 `Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.12`) |
| Backend tests | `dotnet test SmartParking.slnx --nologo` | **1,067 passed, 102 skipped, 0 failed** |
| — Architecture | | 12 passed / 0 skipped |
| — IoTService | | 1 passed / 0 skipped |
| — PaymentService | | 256 passed / 3 skipped |
| — FacilityService | | 199 passed / 29 skipped |
| — OperationService | | 45 passed / 0 skipped (was 35; +10 new) |
| — NotificationAuditService | | 202 passed / 0 skipped |
| — UserService | | 352 passed / 70 skipped |
| Frontend build | `npm --prefix FE run build` | **Exit code 0** (`✓ built in 3.25s`) |
| Frontend tests | `node --test tests/frontend/*.test.cjs` | **260 / 260 passed**, 0 failed, 0 cancelled, 0 skipped |

Skipped tests remain the pre-existing environment-dependent sets (RabbitMQ live broker, PostgreSQL-backed facility/user scenarios) and are unchanged from the baseline.

### 13.5 Residual risks / follow-ups

- `[OPEN]` Admin payment endpoints now require a genuine RS256 UserService token; local Docker callers using the permissive `DevBearer` token will receive 401 by design. JWKS is fetched lazily and fails closed (401) while UserService is unavailable.
- `[OPEN]` The JWKS branch of the new authentication extensions is build-verified and shared with the reviewed OperationService implementation, but is not exercised by an automated test (no test host configures `JwtValidation:JwksUri`).
- `[DEFERRED]` Payment integration settings are still process-local (in-memory singleton) per the schema freeze; persistence/redacted history per UC-POL-02 is out of scope for this harden pass.
- `[DEFERRED]` Real PostgreSQL lifecycle/concurrency and browser JWT UAT for the Admin policy flows were not executed; the new tests use in-memory EF/TestServer.

## 14. Backend re-review — 14 November 2026

Changes requested on HEAD 055da459 plus current modified/untracked files. Reconciled section 13 against implementation. Static review confirms named JWT scheme on Admin Payment, LIVE missing-key rejection, dynamic outbound URL, shared draft rules, timestamp precision fix, corrected expired SITE status, and Notification query layering. Remaining findings follow.

| Priority | Current evidence | Required correction and regression |
|---|---|---|
| P1 | LotPolicyRepository.cs:488–496 | Scheduled Owner publication checks platform at now rather than effectiveFrom and skips validation if platform absent. Check all applicable platform intervals and fail closed without required platform configuration. |
| P1 | ReservationPolicyRepository.cs:228–266 | Platform activation does not validate intersecting published site policies. Narrowing bounds can make a previously valid lot return policy unavailable for future bookings. Define/enforce compatibility atomically across both writers. |
| P1 | PayOsAdapter.cs:408–419; PaymentIntegrationSettingsStore.cs:131,144 | Webhook verification uses active/one previous key without order-version binding or missing-key guard. Mode changes and repeated rotations still break in-flight callbacks. Use retained trusted per-order key version and reject absent verification key. |
| P1 | PayOsAdapter.cs:198–235; PaymentIntegrationSettingsStore.cs:171 | One operation reads mode, credentials and URL separately. Concurrent mode change can combine LIVE credentials with FIXTURE URL. Capture a single immutable configuration and condition probe writes on mode/version. |
| P1 | PayOsAdapter.cs:316,598 | Provider JSON desc/code is logged after truncation, which does not redact reflected secrets. Permit only safe mapped provider metadata and test logs with reflected credential markers. |
| P1 | PaymentIntegrationSettingsStore.cs:112 | Accepted changes remain process-local and disappear at restart. The implementation labels this deferred, while UC-POL-02 still requires persisted accepted configuration/history. Parent acceptance remains open. |
| P2 | SaveLotPolicyDraftCommandValidator.cs:27 | Static Owner ranges still reject otherwise Admin-approved wider ranges, e.g. 75 minutes under 15–90. Resolve approved ranges in the appropriate use case. |
| P2 | ReservationPolicyRepository.cs:40 | Missing ARRIVAL_LATE_TOLERANCE bounds still fall back to metadata. Explicitly removed bounds must remain null. |
| P2 | PolicyDraftValidationRules.cs:29–46 | Newly introduced 365-day/8760-hour ceilings are not representability limits or approved product bounds. Apply actual technical limits or obtain a product decision; keep FE validation consistent. |
| Security/guide | Payment and Notification API/Authentication/UserServiceJwtAuthentication.cs | Require explicit local opt-in for HTTP JWKS; validate required actor claims; restrict configured issuers/audiences (Notification adds demo trust); use uniform 401/403 and distinguish key dependency outage as 503. Verify actual JWKS rotation/outage, malformed actor claims and envelope rather than only PEM/status tests. |
| Guide | Payment API/Middleware/ExceptionHandlingMiddleware.cs | Application HTTP status has been removed. API still uses custom catch-all rather than the framework IExceptionHandler approach required by coding skill section 7. Migrate with response compatibility tests. |

Tests added in section 13 chiefly use in-memory EF or injected public PEM. Real PostgreSQL concurrent activation, full JWT/JWKS stack and old-booking snapshot preservation across policy activation remain unverified in this review. Source changes were not made.

Additional confirmed gaps. LotPolicyRepository.cs:47–57 checks each override separately but does not validate the composed inherited min/max pair before publish. A 120-minute platform minimum with a permitted 60-minute site maximum can publish then fail resolver. Cancelled future SITE revisions with null EffectiveUntil still map to DRAFT at ReservationPolicyRepository.cs:344 because Owner cancellation only flips IsActive (LotPolicyRepository.cs:513,542); the expired-row test does not cover this actual lifecycle.
Verification rerun 14 November: dotnet build SmartParking.slnx --no-restore --nologo -m:1 -v:q passed, 0 errors and 6 warnings. dotnet test SmartParking.slnx --no-build --no-restore --nologo -m:1 -v:q passed 1067, failed 0, skipped 102. Service counts: Operation 45, Payment 256 (3 skipped), Notification 202. These totals do not establish the missing real PostgreSQL/JWKS/concurrent rotation scenarios. Only this session log was edited in the review.


## 15. Round 2 Hardening Planning & Action Items — 14 November 2026

The review findings in Section 14 have been inspected, validated against code and contracts, and confirmed 100% actionable.
The prompt for executing Round 2 Hardening has been prepared and stored at:
D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-340-ROUND2-HARDENING.md.

### 15.1 Action Plan Summary
1. **OperationService:**
   - Enforce effectiveFrom query and fail-closed platform requirement in LotPolicyRepository.PublishDraftAsync.
   - Validate temporal compatibility with all active/scheduled site policies upon Admin platform activation in ReservationPolicyRepository.ScheduleAsync.
   - Implement composite post-inheritance invariants validation via ReservationPolicyResolver.Resolve(...) before committing site publication.
   - Relax static bounds in SaveLotPolicyDraftCommandValidator.cs.
   - Eliminate fallback default bound fabrication in ReservationPolicyRepository.GetCatalogueAsync.
   - Adjust upper validation limits in PolicyDraftValidationRules.cs to technical non-overflow representation limits.
   - Categorize cancelled/superseded SITE revisions correctly instead of labelling as DRAFT.
2. **PaymentService:**
   - Enforce Order-Key Binding with Keyring retention and missing key guard in PayOsAdapter.VerifyAndParseWebhookAsync.
   - Introduce single immutable PaymentConfigurationSnapshot per operation and version-gated MarkProbeResult.
   - Implement comprehensive credential log sanitization for provider JSON errors.
   - Fulfill UC-POL-02 persistence and change history using existing PaymentDbContext.PolicyRevisions table without DB schema changes.
   - Refactor ExceptionHandlingMiddleware to framework IExceptionHandler (PaymentExceptionHandler) per Coding Skill §7.
3. **NotificationAuditService & JWT Security:**
   - Remove demo audience (SmartParking.Postman.Demo), enforce HTTPS/opt-in checks, validate actor claims (sub,
ole), and return 503 on JWKS provider outages.
4. **Verification Gates:**
   - Build 0 errors, backend tests >= 1,067 passed, frontend build and tests (260/260) passing.


## 16. Round 2 Hardening Execution Results — 14 November 2026

Executed on branch `feat/SPARK-340-admin-platform-policies` (base `055da4591ee9a1822eb9870dc261c9685ca3258b`). Working tree intentionally left uncommitted. A partially-executed earlier in-progress Round 2 attempt was present in the working tree (it did not compile); the execution lanes verified that code against Section 14, completed the remaining gaps, fixed the compile defect, and added the missing regression coverage.

### 16.1 Per-item resolution

**OperationService (Section 14 P1/P2 + line-366 gaps)**
- 1.1 Scheduled/Immediate Owner publish — platform revision is resolved at `effectiveFrom = (isScheduled && scheduledAt) ? scheduledAt : now` with the temporal predicate `IsActive && EffectiveFrom <= effectiveFrom && (EffectiveUntil == null || effectiveFrom < EffectiveUntil)`; fail-closed with `ReservationPolicyUnavailableException("Platform policy is required to publish site policy.")` and no publish when none is effective. (`LotPolicyRepository.PublishDraftAsync`)
- 1.2 Admin platform activation temporal compatibility — inside the existing activation transaction, intersecting active SITE policies are compared against the new `target.OverrideBounds`; an out-of-range or removed-bound override rolls back and throws `PolicyBoundsConflictException` (already mapped to HTTP 409 in `ReservationExceptionHandler`) naming the conflicting `SiteId` + policy key. (`ReservationPolicyRepository.ActivateRevisionAsync`)
- 1.3 Composite post-inheritance invariants — `ValidateComposedInvariants` merges site-over-platform effective values and rejects before any `Add/SaveChanges` (min <= 0, max < min, payment-hold <= 0, arrival tolerance outside 15–30) via `PolicyOutOfBoundsException`. Closes the confirmed gap where platform min 120 + site max 60 could publish. (`LotPolicyRepository`)
- 1.4 Owner draft validator relaxed to technical limits only; business bounds are enforced at Publish (1.3) and by the Resolver. (`SaveLotPolicyDraftCommandValidator`)
- 1.5 Catalogue no longer fabricates bounds once a platform revision exists — an omitted key yields `null`; metadata fallback is used only when no platform revision exists at all. (`ReservationPolicyRepository.GetCatalogueAsync`)
- 1.6 Upper limits set to technical non-overflow representability limits: `MaxAdvanceBookingDays ∈ [0, int.MaxValue/1440]`, `MaxReservationDurationHours ∈ (0, int.MaxValue/60]`, `ReservationProtectionWindowHours`/`OccupancyValidityTimespanHours ∈ [0, int.MaxValue/60]`. FE `adminPolicyValidation.ts` already matched these ceilings (no FE edit required). `ArrivalLateToleranceMinutes` intentionally stays 15–30 per SSOT FR-POL-04.
- 1.7 SITE cancelled/superseded classification — inactive SITE rows now classify as `SUPERSEDED` (never `DRAFT`); `GetRevisionsAsync` `status=DRAFT` selects platform drafts only. SSOT owner lifecycle vocabulary is `ALL/DRAFT/SCHEDULED/CURRENT/SUPERSEDED` (no `CANCELLED`); `SUPERSEDED` is defined as `effective_until <= now`, extended intentionally to cover owner cancellation.

**PaymentService (Section 14 P1)**
- 2.1 Webhook key binding / retained keyring / missing-key guard — non-verifying `ParseWebhookEnvelope` resolves the order first; `VerifyAndParseWebhookAsync` validates HMAC-SHA256 against exactly one trusted historical checksum key whose retention window covers `PaymentOrder.CreatedAt`; a blank/missing key rejects immediately (`IsValidSignature=false`, `PaymentStatus="FAILED"`) with no fallback. Order lookup flows through the existing `IPaymentOrderRepository` (no Infrastructure→Persistence coupling).
- 2.2 Immutable per-request snapshot + version-gated probe — `GetCurrentSnapshot()` (`PaymentConfigurationSnapshot`: Version/Mode/BaseUrl/ClientId/ApiKey/ChecksumKey) is captured once per adapter call; `MarkProbeResult(int configVersion, bool success)` ignores stale versions.
- 2.3 Provider-error sanitization — `code`/`desc` are redacted against current, previous and retained secrets (`[REDACTED]`) before logging or returning; raw bodies are never logged.
- 2.4 UC-POL-02 persistence without schema change — each accepted `Save` appends a `PolicyRevision` (`PolicyType="PAYMENT_INTEGRATION"`, `ScopeType="PLATFORM"`, deterministic `PolicyId`, monotonic `RevisionNumber`, `PolicyValuesJson` with Data-Protection-encrypted credentials, actor or `Guid.Empty`); the latest row hydrates on first use. Implemented via a new Application port `IPaymentIntegrationHistoryRepository` + Persistence adapter; no migration/DDL/column added.
- 2.5 Framework `IExceptionHandler` — new `PaymentExceptionHandler : IExceptionHandler` (`PaymentIntegrationException` mapped by code → 400, `ValidationException` → 400, `BadHttpRequestException` → 400, fallback → 500) with the unchanged envelope `{ success, errorCode, message, traceId, errors, timestamp }`; `Program.cs` uses `AddExceptionHandler<PaymentExceptionHandler>()` + `UseExceptionHandler()`; `ExceptionHandlingMiddleware.cs` deleted.

**Payment/Notification JWT security (Section 14 Security/guide)**
- 3.1.1 No demo trust — `SmartParking.Postman.Demo` / `SmartParking.UserService.Demo` removed from the Payment and Notification auth defaults (verified: no remnants in either file). One pre-existing adversarial test (`AdversarialMilestone3ChallengeTests.Probe2`) asserted the old demo trust and could not stay green without violating "do not weaken security"; its expectation was updated to the secure behaviour (now expects 401).
- 3.1.2 Explicit local opt-in for HTTP JWKS — `http` accepted only with `JwtValidation:AllowInsecureTransport == true` and Development/loopback; otherwise HTTPS required (OperationService `IsInsecureTransportAllowed` semantics).
- 3.1.3 Core actor claims — `sub` and `role` required (401 otherwise).
- 3.1.4 JWKS outage vs bad token — key-load failure surfaces 503 with `AUTHENTICATION_KEYS_UNAVAILABLE` via `OnChallenge`; genuine invalid/missing tokens remain 401. (Fixed a `.NET 10` compile defect: `ResultContext<T>.Result` is get-only — replaced with `context.Fail(error)`.)

### 16.2 Key deviations (schema freeze vs literal spec)
- 2.1: `PaymentOrders.signing_key_version`/`mode` columns do not exist and cannot be added; the equivalent guarantee is a retained keyring selecting the single trusted key for the order's `CreatedAt` (retention ≥ 24h).
- 2.4: persistence uses the existing `policy_revisions` table through an Application port in Persistence (Infrastructure must not reference Persistence); no DDL.

### 16.3 Constraint audit
- **Schema Freeze:** respected — no migration/DDL, no column added; persistence reuses existing tables/columns only.
- **Zero leaked secrets:** respected — no plaintext `apiKey`/`checksumKey` in responses or logs; provider-echoed credential markers are redacted (covered by test).
- **Clean Architecture:** respected — Application carries no HTTP status codes; API uses framework `IExceptionHandler`.
- **Uncommitted working tree:** respected — no `git commit`/`push` performed.

### 16.4 Verification gates (actual results, 14 November 2026)
| Gate | Command | Result |
| --- | --- | --- |
| Solution build | `dotnet build SmartParking.slnx --nologo` | Build succeeded, **0 Error(s)**, 12 Warning(s) (pre-existing NU1902/NU1903 advisories) |
| Backend tests (full) | `dotnet test SmartParking.slnx --no-build --nologo` | **1,113 passed / 102 skipped / 0 failed** |
| OperationService | (within full run) | 52 passed / 0 failed |
| PaymentService | (within full run) | 282 passed / 3 skipped / 0 failed |
| NotificationAuditService | (within full run) | 215 passed / 0 failed |
| FacilityService | (within full run) | 199 passed / 29 skipped |
| UserService | (within full run) | 352 passed / 70 skipped |
| Architecture / IoT | (within full run) | 12 passed / 1 passed |
| Frontend build | `npm --prefix FE run build` | exit 0 (`✓ built in 5.36s`) |
| Frontend tests | `node --test tests/frontend/*.test.cjs` | **260 pass / 0 fail** |

Baseline was 1,067 passed; now 1,113 (+7 Operation, +26 Payment, +13 Notification).

### 16.5 Residual risks
- No real PostgreSQL lifecycle/concurrency verification (schema freeze; in-memory/unit coverage only) — real atomic rollback of 1.2/1.3 and real JWKS rotation/outage remain unproven.
- Legacy `PreviousChecksumKey` seeding uses an unbounded retention interval (no rotation timestamp) until the first rotation.
- `PaymentIntegrationHistoryRepository.AppendAsync` computes `max+1` non-atomically; concurrent admin saves can race the unique `(PolicyId, RevisionNumber)` index (best-effort, swallowed).
- Singleton store hydration/persistence does synchronous-over-async on first use/save; a slow DB can briefly block a request thread (caught; startup unaffected).

## 17. P4 readiness re-review after Round 2 — 14 November 2026

Changes requested on HEAD 055da459 plus current working tree. P4 test implementation may start, while acceptance/UAT remains open. Source review confirms immutable PayOS request snapshot, version-gated probes, missing-key rejection, framework Payment IExceptionHandler, required platform at Owner effectiveFrom and SITE inactive-status correction.

| Priority | Evidence | Remaining defect and verification |
|---|---|---|
| P1 | PaymentIntegrationHistoryRepository.cs:13,14,59,63 vs scripts/database/microservices/04-payment-pricing-service-db.sql:33,37 | PAYMENT_INTEGRATION / PLATFORM are rejected by existing CHECK constraints. Store.cs:470 swallows the write error and reports success. Verify actual PostgreSQL PUT and restart; persist durably before returning accepted result. |
| P1 | PaymentIntegrationSettingsStore.cs:461,497,507 | Serialization produces PascalCase Mode/BaseUrl/CredentialsProtected while hydration requests lowercase property names. Round-trip hydration must restore exact saved values. Only the latest mode is loaded; retained keyring and other mode settings also need recovery. |
| P1 | PayOsAdapter.cs:361; PaymentIntegrationSettingsStore.cs:240 | Webhook lookup still uses current mode and order.CreatedAt rather than the snapshot used for checkout. Mode switch or rotation between order creation and checkout invalidates a legitimate callback. Bind issuance mode/key version and preserve it across restart. |
| P1 | LotPolicyRepository.cs:527–536 | Owner validates platform only at site start, while site interval remains open-ended and can cross an already scheduled incompatible platform interval. Check every intersecting platform interval. |
| P1 | ReservationPolicyRepository.cs:321–330 | Platform compatibility checks scalar bounds but not composed inherited invariants. Raising inherited minimum to 90 while site maximum remains 60 can activate then fail booking. Validate composed effective policy on Admin activation too. |
| P1 | LotPolicyRepository.cs:440–443,527; ReservationPolicyRepository.cs:248,314 | Owner site lock and Admin platform locks do not serialize shared compatibility decisions. Add shared coordination or serializable protocol with retry and verify opposing writes with two PostgreSQL connections. |
| P1 | ReservationPolicyRepository.cs:28; LotPolicyRepository.cs:57 | Explicit legacy DRIVER_REALLOCATION_REQUEST_LIMIT can conflict with every platform update because platform draft DTO/bounds do not support authoring this value. Reconcile established ownership and test existing site value 2. |
| P1 | .env.example:128; compose.app.yml:21,130 | Copied sample explicitly disables HTTP JWKS although local Compose uses HTTP. Validate actual local setup; do not treat default-when-unset true as correcting explicit false. |
| P2/security | Payment and Notification API/Authentication/UserServiceJwtAuthentication.cs | Enforce complete trusted actor claims, explicit bounded HTTP JWKS opt-in, uniform 401/403. Current tests accepting absent account_id do not match Operation's actor contract. |
| P2 | PaymentExceptionHandler.cs:44–51; FE/src/lib/authApi.ts:279 | Payment emits errorCode while FE consumes code; field-error branches such as INVALID_PAYMENT_BASE_URL fail to activate. Preserve existing callers and expose canonical code. |
| P1 | PayOsAdapter.cs:253–256,624; PaymentIntegrationSettingsStore.cs:272–288 | Sanitization reads current credentials instead of all credentials used by the in-flight snapshot. Rotating API key/client ID can leave echoed old credentials unredacted. Prefer safe error metadata or redact against the actual request snapshot too. |

Persistence follow-ups are also concrete: hydration marks completion before a failed DB query and never retries; Save persistence runs outside the configuration lock and reads mutable credentials; MAX+1 history allocation races; no stable Payment DataProtection key volume is configured. Test persistence failure, concurrent saves and container recreation with durable protected credentials.

No implementation files edited in this review. Real PostgreSQL/JWKS/UAT not executed.
Current verification completed: solution build --no-restore -m:1 passed, zero errors / six warnings. Full solution test --no-build --no-restore -m:1 passed 1113, failed 0, skipped 102. Operation 52, Payment 282 (3 skipped), Notification 215. Real PostgreSQL/JWKS/browser/concurrent writer scenarios remain NOT RUN.

## 18. Phase 4 Handover & QA / Latent Defect Verification — 14 November 2026

Phase 3.5 Backend Round 2 Hardening has passed all local verification gates (1,113 passed tests, 260 frontend tests, 0 build errors).
The Section 17 Review Findings identified 3 key clusters of latent defects and edge cases requiring rigorous automated testing in Phase 4 (SPARK-370):
1. **Cluster 1 (Payment Persistence & Schema Compatibility):**
   - PostgreSQL CHECK constraints on `policy_revisions` (`policy_type`, `scope_type`).
   - PascalCase vs camelCase serialization/hydration round-trip.
   - Multi-mode and retained keyring recovery across service restarts.
2. **Cluster 2 (Webhook Security & Order-Key Invariants):**
   - Webhook verification across mode switches and consecutive key rotations.
   - Guard missing/untrusted keys.
   - In-flight snapshot secret sanitization.
3. **Cluster 3 (Operation Policies: Temporal Validity, Composed Invariants & Concurrency):**
   - Open-ended site policy crossing future scheduled platform interval with narrower bounds.
   - Admin platform activation breaking active site policies (composed min > max or out-of-bounds).
   - Mutual concurrency between Admin platform activation and Owner site publish transactions.
   - Compatibility with legacy `DRIVER_REALLOCATION_REQUEST_LIMIT` field.
4. **Cluster 4 (Contract Unification):**
   - Unified error contract (`code` vs `errorCode`) for Payment API and Frontend clients.

Delegation prompt for executing Phase 4 has been authored at:
`D:\My file\FA26 - OJT FSoft\Planning\PROMPT-SPARK-370-QA-TEST-AUTOMATION.md`.
## 19. Phase 4 - QA / Test Automation & Latent Defect Verification (SPARK-370) - 14 November 2026

### 19.1 Scope
Phase 4 of Epic SPARK-340 / US-A02 (Admin: Configure platform policy bounds and authorized integration settings).
Deliverables: Postman/Newman E2E UAT collection + environment; xUnit suites covering the 4 latent-defect clusters; full verification; Newman evidence; this log update.
Constraints honoured: Schema Freeze (no migrations/DDL on operational_policies / operational_policy_drafts / policy_revisions); no plaintext apiKey/checksumKey in any artifact, log or report; Clean Architecture + Coding Skill section 7 (IExceptionHandler uniform envelope); no git commit / push; working tree left uncommitted.

### 19.2 Files created (Phase 4)
- docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json (Postman v2.1.0; 5 folders; 38 requests)
- docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json (22 variables; secret values left blank)
- tests/Services/SmartParking.PaymentService.Tests/PaymentIntegrationPersistenceAndCheckConstraintTests.cs (Cluster 1)
- tests/Services/SmartParking.PaymentService.Tests/PayOsWebhookSecurityAndRotationTests.cs (Cluster 2)
- tests/Services/SmartParking.PaymentService.Tests/PaymentExceptionHandlerTests.cs (Cluster 4 envelope)
- tests/Services/SmartParking.PaymentService.Tests/UserServiceJwtAuthenticationTests.cs
- tests/Services/SmartParking.PaymentService.Tests/AdminPaymentIntegrationTests.cs
- tests/Services/SmartParking.OperationService.Tests/PlatformPoliciesTests.cs
- tests/Services/SmartParking.OperationService.Tests/PlatformPolicyTemporalAndConcurrencyTests.cs (Cluster 3)
- tests/Services/SmartParking.NotificationAuditService.Tests/UserServiceJwtAuthenticationTests.cs
- tests/frontend/admin-platform-policies.test.cjs
- docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json

### 19.3 Latent-defect cluster coverage
- Cluster 1 (Payment persistence & schema compatibility): PaymentIntegrationHistoryRepository writes CHECK-conforming PolicyType=TARIFF / ScopeType=GLOBAL (allowed by 04-payment-pricing-service-db.sql), round-trip hydration (camelCase + historical PascalCase), and persistence-failure resilience (warning logged, request not crashed). PaymentIntegrationPersistenceAndCheckConstraintTests.
- Cluster 2 (Webhook security & order-key invariants): webhook for an order created pre-mode-switch still verifies with the issuance-mode key; consecutive rotations accept inside the grace window and reject beyond it; missing key fails closed; provider-error log sanitization redacts credentials. PayOsWebhookSecurityAndRotationTests (4/4).
- Cluster 3 (Temporal validity, composed invariants, concurrency): owner publish crossing a future scheduled platform revision with narrower bounds is rejected (PolicyOutOfBoundsException); admin activation composed-invariant violation is rejected (PolicyBoundsConflictException with composed message); legacy DRIVER_REALLOCATION_REQUEST_LIMIT site value 2 is tolerated; concurrent admin/owner conflict detected. PlatformPolicyTemporalAndConcurrencyTests (4/4).
- Cluster 4 (Contract unification): PaymentExceptionHandler emits BOTH code and errorCode in the uniform envelope {success:false, code, errorCode, message, traceId, errors, timestamp}; field-error branches (e.g. INVALID_PAYMENT_BASE_URL) surface correctly. PaymentExceptionHandlerTests (all pass).
- JWT hardening: UserServiceJwtAuthenticationTests for Payment and Notification services.

### 19.4 Defects fixed during Phase 4 execution
1. PaymentIntegrationSettingsStore.SeedKeyring seeded the initial keyring with RetainedFrom = DateTimeOffset.MinValue (unbounded trust), so a webhook for an order created long before the key was configured could still validate. Fixed: RetainedFrom = DateTimeOffset.UtcNow - KeyRetentionWindow (24h). Out-of-window orders are now rejected; in-window (e.g. now-10h) accepted.
2. PlatformPolicyTemporalAndConcurrencyTests composed-invariant scenario corrected: the active site minimum is now inherited (null) and the draft supplies a RESERVATION_MAX_DURATION bound, so the per-key override loop passes and the composed-invariant check raises the "composed maximum reservation duration" conflict (the earlier version fired the override-bounds branch first).
3. Repaired a malformed JavaScript assertion in the Postman collection during validation; both Postman files now pass full JSON + script-syntax validation.

### 19.5 Verification results (actual)
- dotnet build SmartParking.slnx: 0 errors, 12 warnings (pre-existing NuGet advisories NU1902 MessagePack 2.5.302, NU1903 Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.12).
- dotnet test SmartParking.slnx: 1,126 passed / 102 skipped / 0 failed.
  - OperationService 56/0 failed; PaymentService 291 passed / 3 skipped / 0 failed; FacilityService 199 / 29 skipped / 0; NotificationAuditService 215/0; UserService 352 / 70 skipped / 0; Architecture 12; IoT 1.
  - Two transient failures seen in an earlier run (Facility ConsumerJwksCacheTests; Notification AuditProjectionConsumerIntegrationTests) did NOT recur in the final run; classified as environment/network flakiness, not Phase 4 defects.
- Frontend: npm --prefix FE run build exit 0 (FE/dist/index.html regenerated 2026-11-14T17:22:13); node --test tests/frontend/*.test.cjs -> 261 passed / 0 failed.
- Postman: collection + environment JSON valid; 5 folders; 38 requests; 22 environment variables; ALL SCRIPTS SYNTAX OK.
- Newman: NOT EXECUTED - no local stack running (TCP 5055/5065/5068/5035 closed). Docker 29.8.2 + Compose v5.5.1 are available, but compose.app.yml requires the external network smartpark-schema_default plus required SMARTPARK_* secrets and 6 backend image builds. Evidence: docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json (status NOT_EXECUTED, with howToRun steps).

### 19.6 Postman UAT coverage
- 00 - RBAC Matrix (12): anonymous 401 / driver 403 / owner 403 / admin 200 for operations-policy catalogue, payment integration GET and notification integrations GET.
- 01 - Operation Policies Lifecycle (14): catalogue 14 keys with null-vs-configured bounds; create draft (201); revisions by status DRAFT / CURRENT / SCHEDULED / SUPERSEDED; SQL pagination; revision by id 200 and 404; PUT draft with If-Match / expectedUpdatedAt success and stale 409; activate revision (GiST / site-conflict rejection surfaced as 409 envelope); effective PLATFORM vs SITE provenance.
- 02 - Payment Integration (6): GET secret masking (boolean apiKeyConfigured / checksumKeyConfigured, masked clientId, no raw apiKey/checksumKey property); PUT FIXTURE mode; disallowed BaseUrl -> 400 INVALID_PAYMENT_BASE_URL (asserts code AND errorCode); invalid mode -> 400 VALIDATION_FAILED; blank credential -> 400; configure SANDBOX credentials (requires env secrets; never embedded).
- 03 - Notification Integration (1): 4 fixed channels IN_APP_FIXTURE / LOCAL_EMAIL_CAPTURE_FIXTURE / LOCAL_PUSH_FIXTURE / LOCAL_SMS_FIXTURE, all ACTIVE + isReady.
- 04 - Immutable Operational Policy Snapshot (5): accept terms capturing the platform revision + values; create + activate a changed platform revision; assert the effective platform moved; assert the accepted snapshot variables are unchanged. Documented boundary: BookingView does not expose the snapshot column; immutability is enforced by the persistence guard (UnitOfWork.cs) and covered by xUnit.

### 19.7 Constraints & git status
- Schema Freeze: no new migrations or DDL; no changes to operational_policies / operational_policy_drafts / policy_revisions schemas.
- Secret hygiene: no plaintext apiKey/checksumKey embedded or printed; assertions use masking / boolean flags.
- Git: no commit / push; all Phase 4 artifacts remain uncommitted on feat/SPARK-340-admin-platform-policies (HEAD 055da459).

### 19.8 Residual risks / follow-ups
- Newman UAT must be executed once the local stack is provisioned (see evidence howToRun).
- PaymentIntegrationHistoryRepository.AppendAsync max(RevisionNumber)+1 remains non-atomic (documented residual race).
- Composed activation / concurrency coverage is in-memory + targeted; no real-PostgreSQL concurrent run was performed in this phase.

## 20. Independent P4 verification on the Docker stack — 14 November 2026

**P4 acceptance remains open; changes requested.** Detailed report: docs/04-testing/reports/SPARK-370-P4-independent-verification-20261114.md.

Rebuilt current working tree and started existing Compose infrastructure/app with existing environment and volumes. All five backend services healthy, frontend running at http://localhost:5173. No volume deletion. All three genuine UserService logins succeeded. UserService emits lowercase admin/owner/driver roles; all three new Admin controllers require uppercase ADMIN. Direct authenticated Admin catalogue and Newman Admin GETs returned 403. No successful Admin mutations occurred.

Actual results: solution build zero errors/six warnings; full backend 1126 passed/102 skipped/zero failed; FE build passed; frontend Node 261 passed/zero failed. Newman 6 executed the unmodified 38-request collection twice: 37 assertions evaluated, 27 failed, six test-script runtime failures (Identifier data already declared). Evidence JSON replaced the historical NOT_EXECUTED marker with sanitized FAIL results; no raw tokens, headers or populated environments exported.

Source review confirms corrections for PostgreSQL CHECK values, casing hydration, future interval/composed invariants and snapshot-based initiation redaction. Remaining defects include Admin role casing, unsynchronized PostgreSQL Owner/Admin writers, time-inferred webhook key selection, mode/keyring loss on restart, acknowledged failed persistence/non-atomic history revisions, cancellation redaction during rotation and Owner legacy-field republish. Collection has wrong provenance expectations, false-positive snapshot checks, permissive SANDBOX 400 acceptance, missing write-RBAC/scheduled activation cases and missing owned booking fixtures. See detailed report for paths, scenarios and completion gates.

Real PostgreSQL concurrent writer interleavings and confirmed reservation snapshot immutability remain NOT PROVEN. Automated unit/in-memory tests do not establish those properties. No production code was changed in this review. Runtime helper was removed. Docker remains running for user FE+BE UAT.

## 21. P4 Defect Fixes and Re-Verification (SPARK-370) - 14 November 2026

**Status: independent-review blocking defects fixed and re-verified on the live Docker stack. P4 qualifies to close pending user UAT sign-off.**

### 21.1 Defects fixed
1. Admin authorization casing - three controllers required uppercase `ADMIN` while UserService issues lowercase `admin` (AccessRoles.Map). Fixed `PlatformPoliciesController.cs`, `AdminPaymentIntegrationController.cs`, `AdminNotificationIntegrationController.cs` to `[Authorize(Roles="admin")]`. Regression guard added (`AdminPaymentIntegrationTests` mints lowercase admin).
2. Policy concurrency - Owner publish (`LotPolicyRepository`) and Admin activation (`ReservationPolicyRepository`) now serialize on a shared transaction-scoped advisory lock `pg_advisory_xact_lock(hashtextextended('reservation-policy-compatibility/v1', 7))` before validating compatibility; retry on 40001/40P01 up to 3; 409 on persistent conflict. New real-PostgreSQL two-connection tests.
3. PayOS persistence/webhook - verified implemented in the working tree: multi-mode + full keyring persistence/restore (PersistedStoreState v2), hydration retry, honest Save (throws `PaymentIntegrationPersistenceException` + rollback), atomic MAX+1 via advisory lock, snapshot-aware cancellation redaction. New real-PostgreSQL atomic-allocation test.
4. Legacy `DRIVER_REALLOCATION_REQUEST_LIMIT` unified (0..5) across catalogue, Owner publish, and Admin activation.
5. Newman collection - fixed six duplicate-`data` runtime errors; `PLATFORM_DEFAULT` provenance; snapshot before/after with response-derived ids; SANDBOX skip vs required 200; added `05 - Scheduling & Write RBAC` (scheduled activation, overlap 409, Owner/Driver write 403); response-derived fixtures; notification channel contract corrected to EMAIL/SMS/PUSH/IN_APP with `*_FIXTURE` providers. **Fixed 5 revisions-list requests that lacked `url.query` arrays** (Newman dropped the query params, so status filters and pagination were ignored and the API returned defaults).

### 21.2 Real verification (Docker stack)
- Rebuilt and restarted operation-service (5055), payment-service (5065), notification-service (5068); volumes/data preserved.
- RBAC matrix with genuine UserService tokens (operations catalogue, payment GET, notifications GET): **admin 200 / owner 403 / driver 403 / anonymous 401**.
- Newman: **51 requests, 0 request failures, 51 test-scripts 0 failed, 4 prerequest-scripts 0 failed, 81 assertions 0 failed** (5s).
- Backend: `dotnet build SmartParking.slnx` 0 errors; `dotnet test SmartParking.slnx` **1,136 passed / 107 skipped / 0 failed**.
- Frontend: `npm --prefix FE run build` exit 0; `node --test tests/frontend/*.test.cjs` **261 passed / 0 failed**.
- Real PostgreSQL: Operation platform-concurrency tests 3/3; Payment atomic-allocation tests 2/2.

### 21.3 Evidence
- `docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json` (sanitized PASS; no tokens/headers/bodies/environment exported).
- `docs/04-testing/reports/SPARK-340-P4-fix-verification-20261114.md` (conclusion report).

### 21.4 Remaining dependencies (NOT applied; need approval)
- Durable mode+key-version webhook binding (payment_orders columns + migration, or policy_revisions, or a Redis fingerprint with TTL).
- Stable DataProtection key store (compose volume + `DataProtection__KeysDirectory`, or DB-backed, or external Key Vault/Redis/S3).

### 21.5 Constraints respected
Schema Freeze (no migration/DDL). No commit/push (HEAD 055da459; tree uncommitted). No secrets printed or exported.

## 22. Independent re-review — 16 November 2026

**Changes requested.** Detailed findings and gates are in docs/04-testing/reports/SPARK-370-independent-review-20261116.md. HEAD remains 055da459, reviewed modified/untracked working tree.

Executed verification: solution restore/build outside sandbox PASS (0 errors, 56 current NuGet advisory warnings); full BE suite 1136 passed /107 skipped /0 failed; real PostgreSQL Operation concurrency 3/3 PASS; real Payment atomic allocation 2/2 PASS; FE build PASS; frontend Node 261/261 PASS. Initial sandbox no-restore build failed on unavailable package references, corrected through outside-sandbox restore/build. Initial Operation PG runs omitted password in the connection string; corrected configuration passed 3/3. No implementation fix was needed for those setup failures.

Current collection execution: 51 requests, 75 passed assertions, 6 skipped, 0 failures; genuine UserService logins and uppercase/lowercase integration regression resolved. Existing historical evidence was preserved. New sanitized evidence is docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116.json and counts skips explicitly. Three SPARK-340 Docker images rebuilt from working tree; app remains running for user UAT, existing volumes preserved.

Source review confirms shared PostgreSQL advisory lock and real two-connection proof. Remaining P1 defects concern store-level concurrent Save rollback/order, Save proceeding after failed authoritative hydration, RAM-only checkout key binding after restart/replica changes and unwired stable DataProtection key persistence. Additional P2 findings concern legacy 0..5 exemption granting new unapproved overrides and int overflow in bound ceiling division. Snapshot assertion still compares effective SITE values to raw PLATFORM values and uses a separately supplied booking ID. Six Newman skips include critical booking snapshot and SITE_OVERRIDE cases. These properties remain unproven and cannot be inferred from the passing subset.

Only review report, sanitized evidence and session were changed. Temporary runtime helper removed after verification. Source edits remain with the implementation agent.

## 23. Implementation fixes and localhost verification — 16 November 2026

Implemented the user's requested fixes on the existing working tree, base HEAD 055da459. Handoff report: docs/04-testing/reports/SPARK-340-fixes-and-handoff-20261116.md.

Changes: serialize configuration candidate/persist/rollback under one gate; hydration fails closed and rolls back partial application; Application-owned persistence exception maps to safe API503; durable immutable per-order checkout signing binding through inward port/Persistence policy_revisions and Infrastructure DataProtection service; no timestamp fallback for store-backed unbound callbacks; named persistent Payment encryption-key volume; legacy reallocation exemption preserves prior accepted value only; overflow-safe bound arithmetic; account-bound claims for new Admin routes while preserving existing non-Admin Notification callers; linked-terms booking/snapshot collection; deterministic replay-test completion.

Existing Payment encryption keys were migrated from its old container directory into the new named volume before recreation, without printing keys or removing application data. Three SPARK-340 containers rebuilt; Payment force-recreated and API results remained correct. Existing local volumes are preserved. No schema migration/DDL or commit/push.

Actual final results:
- Full solution build passed, zero errors /28 advisory warnings on last complete build.
- Full BE regression passed 1156 /108 skipped /0 failed (Architecture12, IoT1, Facility199, User352, Notification220, Operation65/3skip, Payment307/6skip).
- Real PostgreSQL tests passed Operation3/3 and Payment3/3 (including immutable encrypted order binding after fresh DbContext/key provider).
- FE build passed; frontend Node261/261 passed.
- Updated collection defines54 requests; current missing-fixture run executed48 requests,75 assertion passed,3 skipped,0 failed. Sanitized evidence: docs/04-testing/reports/evidence/SPARK-370-fix-Newman-20261116.json.
- Runtime errors caught during development were fixed: relocated exception test import, new PG test's assumption of empty seed table, overly broad account validation on old Notification endpoints, and audit replay test's fixed sleep. Final completed suite is green.

Manual checklist: docs/04-testing/reports/SPARK-340-localhost-UAT-checklist.md.
Independent testing prompt: docs/04-testing/reports/SPARK-370-independent-test-agent-prompt.md.
The Docker stack remains running at http://localhost:5173; all five backend containers healthy.

Acceptance remains pending actual SITE_OVERRIDE, optional provider credentials and linked booking snapshot fixtures/user UAT. Real provider checkouts issued before durable binding need trusted reconciliation/backfill; their historical issuance cannot be inferred safely from timestamps. Multi-instance admin configuration cache invalidation is outside this singleton-store fix; test the target topology if deploying multiple Payment replicas.

Subagents stopped at usage limit during implementation; root completed missing docs/integration, built/tested final source and captured runtime evidence. Temporary runtime helper removed after verification.

## §24 — Independent verification pass 2 (2026-11-16, post-16/11 fixes)

Scope: independent verification of SPARK-340/370 after the 16/11 fix set, per `docs/04-testing/reports/SPARK-370-independent-test-agent-prompt.md`. HEAD `055da4591ee9a1822eb9870dc261c9685ca3258b` unchanged; working tree 100 porcelain entries; no commit/push; no source edits.

Executed (exact):
- Build `dotnet build SmartParking.slnx -m:1` → 0 Error / 56 Warning.
- `dotnet test SmartParking.slnx --no-build -m:1` → Passed 1156 / Failed 0 / Skipped 108 / Total 1264 (Arch12, IoT1, Facility199/29skip, User352/70skip, Notification220, Operation65/3skip, Payment307/6skip).
- `npm --prefix FE run build` → PASS; `node --test tests/frontend/*.test.cjs` → 261/261.
- Newman collection (54 defined): 48 executed, **75 assertions passed / 0 failed / 3 skipped**, testScripts 48/0, prerequest 9/0; **6 requests `skipRequest()`ed** (folder 04 booking chain). First run was invalid (expired tokens → 401s); re-ran with fresh tokens. Sanitized evidence `docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116-tokenfresh.json`.
- Real Postgres: Operation `PlatformPolicyPostgresConcurrencyTests` 3/3; Payment `PaymentIntegrationPostgresConcurrencyTests` 3/3 (fresh DbContext + fresh DataProtection over same key dir). Connection strings built from `SMARTPARK_DEV_PASSWORD`, never printed.
- Payment `restart` then `up -d --no-deps --force-recreate` → healthy; volume `payment-data-protection-keys` → `/app/.keys/payment`; key file persisted unchanged; config (FIXTURE, isConfigured, HEALTHY) retained; no key XML printed.
- Secret-marker: PUT marker creds → GET masked only (`****MARK`), no marker in GET body or 65 log lines; FIXTURE restored. initiate/cancel/mid-flight rotation marker checks NOT RUN (would require sending fake creds to real provider — forbidden).

HTTP-level proofs obtained: RBAC 401/403/200 (GET + writes); catalogue 14 keys + bounds; draft create/list/read/404; If-Match success 200 and stale 409 `RESOURCE_VERSION_CONFLICT`; immediate activate; CURRENT/SCHEDULED/SUPERSEDED; effective PLATFORM; payment masked + 400 `INVALID_PAYMENT_BASE_URL`/`VALIDATION_FAILED`; notification 4 channels; **scheduled lifecycle + overlap 409 `OVERLAPPING_POLICY_INTERVAL`**; owner/driver write 403. Also: admin site-scope draft → 403 `FORBIDDEN_SCOPE_WRITE` (correct by design).

Blockers (environment, not implementation defects):
- **F1 (P1)** `operations-db-1` missing `operational_policy_drafts` (42P01) → owner lot-policy draft/publish 500 → real `SITE_OVERRIDE` NOT RUN. DDL `scripts/database/microservices/03b-reservation-operations-service-db.sql:236-258` is tracked/additive; volume predates commit `e25354b2`. Repair = apply additive DDL (keeps data/volume).
- **F2 (P1)** Booking chain `503 OWNER_UNAVAILABLE` (`ReservationIntegration:Enabled=false` on operation+facility) → folder 04 booking snapshot NOT RUN / UAT U19 blocked.

Verdict: **P4 not eligible to close unconditionally** — core green, but booking-snapshot chain and real SITE_OVERRIDE remain unproven; U04/U09/U10/U11/U19/U20 NOT RUN. Unblock F1+F2 and re-run folder 04 + owner publish + user UAT. New report: `docs/04-testing/reports/SPARK-370-independent-verification-20261116b.md`.

## 25. UAT environment repair and exposed integration dependency — 16 November 2026

Repaired F1 by applying only the approved additive schema block in a transaction; policy row count36 before/after. Repaired signed Operation/Facility configuration using a matching local RSA pair in ignored .env and local HTTP opt-in. Existing admission/messaging was already enabled. Owner read/draft/publish now200, invalid draft400, effective arrival tolerance20 is genuine SITE_OVERRIDE. Owner's previous config preserved locally; other policy values retained.

Discovered and fixed repeated-request RSA signer ObjectDisposedException (500) in ReservationFacilityClient by using key parameters rather than a per-request disposed RSA reference. New regression test validates four signed calls and passed1/1. Notification ClockSkew aligned30sec, authentication tests18/18 pass. Rebuilt both services. Repaired malformed broker connection URI with encoded password setting (secret not printed); both runtime URIs validate.

Docker Desktop was manually paused mid-task. UI Resume could not be controlled; official Desktop restart recovered the engine. Existing volumes/data retained; all five backends healthy, frontend running.

Actual scoped Newman after repair: folders00/01/02/03/05,44 requests,71 passed assertions,1 optional SANDBOX credential skip,0 failures. Folder04 explicitly BLOCKED, not a full collection pass. Evidence: SPARK-370-environment-repair-20261116.json and SPARK-370-repaired-environment-Newman-20261116.json.

Remaining terms blocker is implementation, not environment: two repeated requests503 OWNER_UNAVAILABLE with message Reservation commercial producer is not integrated. Operation still registers UnavailableReservationCommercialOwner, whose six operations fail closed. Signed Facility facts itself200. No existing adapter provides the required quote/accept/order/collection/reconcile contract. A real adapter must be delivered under the owning integration contract; no fake commercial fallback was introduced.

Full report/retest instructions: docs/04-testing/reports/SPARK-340-UAT-environment-repair-20261116.md. P4 linked booking snapshot remains open. No commit/push or volume deletion.

## 26. Independent re-verification after environment repair — 17 November 2026

Independent agent re-ran the full verification on the repaired environment. HEAD unchanged `055da4591ee9a1822eb9870dc261c9685ca3258b`; all SPARK-340/370 artefacts still uncommitted; `git status --porcelain` 108 pre-existing → 109 (the 2 artefacts written by this pass).

Gates (exact):
- Build → 0 Error / 56 Warning.
- `dotnet test SmartParking.slnx --no-build -m:1` → Passed 1157 / Failed 0 / Skipped 108 / Total 1265 (Arch12, IoT1, Facility199/29skip, User352/70skip, Notification220, Operation66/3skip, Payment307/6skip).
- FE build PASS (13.55s, 1343 modules); `node --test tests/frontend/*.test.cjs` → 261/261.
- Real Postgres: Operation `PlatformPolicyPostgresConcurrencyTests` 3/3; Payment `PaymentIntegrationPostgresConcurrencyTests` 3/3 (temp DBs via CREATE DATABASE; connection strings built in-shell, never printed).
- RSA signer regression `~ReservationFacility` → 1/1 (`ConsecutiveRequests_SignValidTokensAfterEachRequestRsaIsDisposed`, 4 signed requests).

Newman — **full collection with real fixture ids** (vehicle `19b6220a…`, slot `b12892d2…`, site `b0000000…`, fresh real UserService tokens):
- 54 defined, **48 executed, 6 NOT RUN** (`pm.execution.skipRequest()`, folder 04 booking chain).
- Assertions **76 passed / 2 failed / 1 skipped**; request failures 0; test-script failures 0; pre-request-script failures 0.
- The 2 failures are both `Accept reservation terms` (`expected 200 got 503`; `Cannot read properties of undefined (reading 'termsId')`) — same root cause as the blocker below, reported as FAIL not green-washed.
- 1 skip = optional SANDBOX credentials not supplied (masking independently proven in the direct SANDBOX test).
- **`Effective policies - SITE provenance` now pass=4 skip=0** (pre-repair it self-skipped). Assertions include `overridden site values report source=SITE_OVERRIDE` → **real SITE_OVERRIDE proven** (ARRIVAL_LATE_TOLERANCE=20 SITE_OVERRIDE inherited=false; RESERVATION_MIN_DURATION=15 and PAYMENT_HOLD_DURATION=7 PLATFORM_DEFAULT).
- Also proven at HTTP level: temporal scheduled lifecycle, overlap 409 `OVERLAPPING_POLICY_INTERVAL`, stale If-Match 409 `RESOURCE_VERSION_CONFLICT`, catalogue 14 keys/bounds, payment 400 `INVALID_PAYMENT_BASE_URL`/`VALIDATION_FAILED`, notification 4 channels, full RBAC 401/403/200 for GET and writes.

Owner flow: `GET …/lots/{site}/policies/revisions` (owner 200) shows revision 3 ACTIVE `effectiveFrom 2026-11-16T15:41:45Z`, `publishNote "SPARK-340 local UAT environment verification"`; draft now `data:null` (consumed by publish).

Remaining BLOCKER (unchanged, implementation): `POST /api/reservation-terms` → `503 OWNER_UNAVAILABLE "Reservation commercial producer is not integrated."` Root cause source-verified: `InfrastructureDependencyInjection.cs:49` registers `UnavailableReservationCommercialOwner` (only impl, `ReservationOwners.cs:38`) for `IReservationCommercialOwner` (`BookingContracts.cs:44`), consumed by `BookingReviewHandlers.cs:40,53` and `BookingProgressHandler.cs:12`. Distinct from the repaired Facility transport flag. Therefore folder 04 / U19 **NOT RUN / NOT PROVEN**; booking snapshot must not be claimed from policy-revision reads.

Payment durability (user notified before interrupting; DB/volume preserved; key XML never printed): SANDBOX marker creds → `PUT 200`, `clientIdMasked=****7f3a`, apiKey/checksumKey configured; survives `restart` **and** `up -d --no-deps --force-recreate` with identical decrypted masked values; volume `smartpark-app_payment-data-protection-keys → /app/.keys/payment` intact; 1 key file (name redacted); raw markers absent from responses and logs; env restored to FIXTURE. Note FIXTURE stores no credentials by design (`PaymentIntegrationSettingsStore.cs:229,365`), so durability must be tested in SANDBOX.

Environmental: `smartpark-app-user-service-1` found `Exited (139)` at run start (residual from Docker Desktop pause/restart); restarted → healthy. Not a product defect.

Verdict: **P4 not closeable** — all gates green and SITE_OVERRIDE/temporal/RBAC/payment-durability proven, but the linked booking-snapshot case (folder 04 / U19) is blocked pending a real `IReservationCommercialOwner` adapter from the Operation↔Payment integration. U11 (Admin min→90 rejection) and U20 (UI responsive) NOT EXERCISED this pass.

Artefacts: report `docs/04-testing/reports/SPARK-370-independent-reverification-2026-11-17.md`; sanitized evidence `docs/04-testing/reports/evidence/SPARK-370-independent-reverification-2026-11-17.json` (tokens/auth headers/request bodies removed; raw Newman export kept outside repo). Prior reports preserved. No commit/push; no app DB/volume deleted.

## 27. Follow-up repairs and reviewer prompt — 17 November 2026

User reports most localhost FE+BE checks stable, pending another group-tester run. Requested remaining fixes and a reviewer prompt. Preserved HEAD055da459 and existing work. Final status snapshot127 entries, no commit/push, runtime restarts, application DB mutations or schema changes in this follow-up.

Implemented and validated:
- Added CheckoutBindingOutboundOrderingTests with four cases: await store, readable binding at send, store/readback error causes zero outbound, conflicting existing key preserved with zero outbound.
- Added real PostgreSQL independent-connection read/decrypt at the outbound boundary. External completion flag prevents assertion swallowed inside PayOsAdapter from becoming a false PASS.
- Terms Postman script now returns after failed non200 assertion before accessing termsId. Added two Postman regression/syntax tests.
- Added SPARK-340-review-followup-20261117.md with corrections for U04/U06 evidence and concrete proposed integration steps. Historical reports preserved.
- Added SPARK-370-reviewer-after-followup-20261117.md and linked it from the earlier reviewer prompt.

Actual verification:
- Focused Payment ordering4 passed/0failed/0skip.
- Full Payment suite311 passed/0failed/7skipped. Four PG skips were enabled separately and passed4/4. Other three require live RabbitMQ configuration.
- PG initial attempt failed28P01 before creating a temp DB due inherited/local password mismatch. Reran with local .env parsed by util.parseEnv, passed4/4. No secret printed, only fixture-generated temporary databases used.
- node --test tests/postman/spark340-collection.test.cjs passed2/2.
- Focused real Newman terms request, fresh Driver JWT, vehicle/slot fixture: HTTP503 OWNER_UNAVAILABLE, exactly1 failed assertion, zero script errors. Evidence SPARK-370-terms-guard-recheck-20261117.json. Still FAIL for business acceptance, guard regression fixed.
- git diff --check passed. No new whole-solution or FE run claimed for this test-only follow-up.

OPEN dependency refined from the earlier "missing adapter" description: approved11November SPARK-273 decision/implementation alignment require combined acceptance+EnsureOrder after committed hold, financial calls outside Operations transactions and event-driven confirmation. Current Prepare calls commercial.AcceptAsync under guarded transaction and retains prior continuing workflow. Payment transport/auth/quote-coverage race/persistence details are explicitly TO AGREE in owner documents. No separate acceptance endpoint, fake terms or success-returning adapter was added. Shared coding skill Architecture/direction approval gate requires approval of unresolved concrete contracts. Follow-up report supplies a reviewable proposal consistent with the approved architecture.

Next: independent reviewer can verify completed fixes now using the new prompt. Project lead/Payment/Operations owners must approve/align remaining concrete integration contracts before implementation. U19 stays BLOCKED, U11/U20 and final tester-group UAT remain unverified. P4 remains OPEN.

---

## 27. Independent review round 2 — 17 November 2026 (whole working tree, agreed test window)

Role: independent reviewer/verifier. HEAD `055da4591ee9a1822eb9870dc261c9685ca3258b`, branch `feat/SPARK-340-admin-platform-policies`, working tree 115 porcelain entries (31 M + 84 ??). Entire working tree reviewed, not only tracked diff. No commit/push, no reset/clean, no DB/volume deletion, no keypair change, no real provider credentials, no weakened assertions.

Read first: AGENTS.md -> ai/AGENTS.md -> coding skill -> this session -> SPARK-340-review-followup-20261117.md -> 17/11 report+evidence -> localhost UAT checklist -> 11/11 decision + SPARK-273 alignment. Reports used as an index, never as a substitute for requirements or source.

Gates (fresh run, no prior totals reused):
- dotnet build SmartParking.slnx -> exit0, 0 errors, 56 warnings (package advisories only), 20.41s.
- dotnet test SmartParking.slnx --no-build -> Passed1161 / Failed0 / Skipped109 / Total1270. Per project: Architecture12/0/0, Facility199/0/29, IoT1/0/0, Notification220/0/0, Operation66/0/3, Payment311/0/7, User352/0/70.
- PG Operation PlatformPolicyPostgresConcurrencyTests -> 3/3 passed (26.75s, real PostgreSQL, temp DB only).
- PG Payment PaymentIntegrationPostgresConcurrencyTests -> 4/4 passed (34.4s).
- node --test tests/postman/spark340-collection.test.cjs -> 2/2.
- npm --prefix FE run build -> exit0, built in 10.01s.
- node --test tests/frontend/*.test.cjs -> 261/261.

Newman full collection, real UserService tokens, inside the user-granted window. Fixtures derived from API responses: tenant a0000000-...0001 (from parking-lots response), site b0000000-...0001 (DEMO-HQ), slot b12892d2-2118-7542-3eb8-ebf992e6cb46 (first CAR/OPERATIONAL slot), vehicle 19b6220a-7d42-448b-ab9b-b302534dd6bd (driver GET /api/vehicles). payment_test_* left blank on purpose.

Result: 54 defined / 48 executed / 6 NOT RUN. Assertions 76 passed, 1 failed, 1 skipped. testScript failures 0, prerequest 9/0. Newman exit 1 solely due to the expected failed assertion.
- Only failure: "Accept reservation terms - capture accepted snapshot" HTTP503 -> "terms review succeeds: expected 200 but got 503".
- Only skip: "Configure SANDBOX credentials - masked (optional)".
- 6 NOT RUN all in folder 04: submit reviewed terms; read admission receipt; capture raw historical platform settings; read accepted booking before activation; read accepted booking after activation; compare raw settings after. Reason: OperationService registers UnavailableReservationCommercialOwner (InfrastructureDependencyInjection.cs:49) so POST /api/reservation-terms returns 503 OWNER_UNAVAILABLE "Reservation commercial producer is not integrated."; downstream requests skipRequest because their captured vars are absent.
- Side effect disclosed: collection created/activated PLATFORM revisions. Platform pointer after run = revision 8ec7d40b-231c-4796-82d3-1221acce7e5d, version 45, effectiveFrom 2026-11-17T03:13:37.905235+00:00.

Real SITE_OVERRIDE now PROVEN (upgrade from earlier skip): scope=SITE, ARRIVAL_LATE_TOLERANCE=20 source=SITE_OVERRIDE inherited=false, other 13 keys PLATFORM_DEFAULT. Collection assertion "Effective policies - SITE provenance" 4 passed/0 failed/0 skipped (previously 3+1 skip). Produced by the Owner lot-policy draft+publish path from the 16/11 repair; admin site-scope draft is rejected 403 FORBIDDEN_SCOPE_WRITE by design (itself a correct authorization result).

Payment persistence re-verified in-window: restart -> healthy 6s, config unchanged (FIXTURE). up -d --no-deps --force-recreate -> Recreated, healthy 6s, mode=FIXTURE, isConfigured=true, status=HEALTHY. Volume smartpark-app_payment-data-protection-keys -> /app/.keys/payment unchanged; 1 key file; no key XML printed or stored; no log errors. Returned state is decrypted from the persisted history revision via the key ring, so key retention + decrypt-after-restart are demonstrated. FIXTURE stores no credentials by design (PaymentIntegrationSettingsStore.cs:229,:365), so credential decryption was not re-demonstrated and was not mutated during the user's UAT.

Mutation sensitivity of the new ordering tests: all three CheckoutBindingOutboundOrderingTests are PROVEN. Binding moved after SendAsync, un-awaited store, and storage-failure-with-outbound each fail them. A swallowed assertion cannot become a false PASS: XunitException is not caught by the PayOsAdapter.cs:306-307 filter and the external flags are set after the assertion then asserted. PG CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound is PARTIALLY PROVEN: ordering+decryption at the boundary proven, but commit isolation/connection independence is not discriminated (reader rents from the same Npgsql pool; no pg_backend_pid inequality, no Pooling=false, no pre-opened independent connection). Recommend renaming to ...ReadableByFreshContext... or forcing a distinct connection.

Postman guard test verified live and by reading: 503 -> exactly one failed assertion, no termsId dereference, environment.set unreachable on the error path, 0 script failures. Booking snapshot stays BLOCKED and is never inferred from a policy-revision read.

U04/U06 corrections: U04's applicable 409 is stale If-Match -> RESOURCE_VERSION_CONFLICT (verified live); overlap 409 proves U07 not U04. U06 boundary values remain NOT PROVEN - no test exercises GetEffectivePolicies/provenance or before/at/after effectiveFrom; only revision classification from a fixed clock (PlatformPoliciesTests.cs:266) and owner publish scheduling (:441,:475) exist, and the live run proves only create+activate+SCHEDULED listing.

Code review findings recorded (full table in the report): F1 P1 pre-existing repo-committed symmetric JWT key (PaymentService appsettings.json:23, file unmodified - fixed as tracked content, Program.cs:56-72); F2 P2 deposit endpoints on the loose default scheme with dev-header auth; F3 P2 EnsureOrder does not bind full quote; F4 P2 concurrent same-idempotency-key -> 500; F5 P2 quote acceptance not serialized (double-order risk); F6 P2 pre-existing checkouts' webhooks rejected after deploy; F7 P3 bindings stored in pricing policy_revisions; F8 P3 arrivalLateToleranceEnabled silently discarded; F9-F13 P4 (check-then-insert version allocation, mixed clocks, past effectiveFrom commit, 500 when auth unconfigured, fixture notification channels reported ACTIVE). Note: Payment default scheme ClockSkew is 1 minute (Program.cs:82), contradicting the repair doc's "three services use 30s" for that scheme.

U19 verdict unchanged and refined: BLOCKED, owner SPARK-273. Not merely a missing adapter - PrepareAsync calls AcceptAsync inside the guarded transaction and before the hold commit (BookingProgressHandler.cs:117 vs commit :49); ProgressBookingAsync calls EnsureOrderAsync inside the same transaction under site/vehicle locks (:154,:91-93); the creation receipt is not terminal at the hold (ReservationRequest.cs:43-48, COMPLETED only at :241, no HOLD_CREATED). Still OPEN: contract route/authenticated transport, machine authentication, coverage evidence, event-driven confirmation (no ReservationDepositCollectedV1; generic PaymentSucceededV1 only), recovery, Operations-side durable order intent. All five orchestration proposals remain PROPOSED, not implemented. Forbidden shortcuts (fake producer, separate accept endpoint, dropping Facility confirmation) were not taken.

Corrections issued over earlier reports: (1) the PG "independent connection" claim must be narrowed to fresh-context; (2) SITE_OVERRIDE is PASS now, not skipped; (3) overlap 409 is not U04 evidence; (4) the followup's "durable order intent before the financial call" is not true of current source.

UAT mapping: PASS - U01, U02, U05, U07, U12, U13, U15, U17, U18, plus U04 via If-Match and U08 for provenance. PARTIAL - U03 (payment/integration 400s only; Admin bound min>max and min<=0 untested), U09 (inside-bounds path only; out-of-bounds 400 observed 16/11, not re-run), U14 (code-level zero-HTTP), U16 (unit-level 503). NOT PROVEN - U06. NOT EXERCISED - U11, U20. BLOCKED - U19. The user's general "FE+BE mostly fine" statement was not converted into per-case PASS; tester-group sign-off remains pending.

Verdict: P4 NOT closeable. U19 BLOCKED under SPARK-273; final tester-group UAT outstanding.

Artefacts (old reports/evidence preserved as history): docs/04-testing/reports/SPARK-370-independent-review-20261117-round2.md and docs/04-testing/reports/evidence/SPARK-370-independent-review-20261117-round2.json (sanitized; substring-scanned for eyJ / Bearer / Authorization / SMARTPARK_DEV_PASSWORD / Password= / RSAKeyValue - only prose matches). Session porcelain 115 before; +2 new documentation files after. No commit/push, no app DB/volume deleted.

## 28. User-directed temporary P4 closure; P5 review and P6 delivery preparation — 17 November 2026

User explicitly requested temporary P4 closure and execution of P5/P6 according to planning. Scope confirmed as SPARK-340 and children368/369/370; unrelated task implementations need not block this task's review work. This instruction supersedes the previous P4-in-progress recommendation as a phase handoff only. Original reports and test results stay unchanged. No Jira write was made.

P4 is now TEMPORARILY CLOSED / CONDITIONAL HANDOFF. U06 before/exact/after effective values, U11, U20 and group-tester sign-off are DEFERRED. U19 is BLOCKED by SPARK-273, while immutable accepted snapshot remains an acceptance criterion of SPARK-340/370. No BLOCKED/FAIL/NOT RUN was relabeled PASS.

P5 tasks5.1/5.2 executed: reviewed controller→MediatR→Application port→service-owned adapter/repository paths, scoped auth, FE shared-guide behavior, redaction and snapshot guard. Reconciled independent round2 findings against source and contract. F1 baseline and F2–F5 deposit-order/SPARK-273 delegated out of this repair scope. F6 legacy-checkout callback rejection remains an in-branch P2/High rollout issue; F7 binding-table retention and F8 ignored input remain open. F9–F12 low findings recorded. F13 not accepted as a defect solely from ACTIVE because contract explicitly describes registered local fixture providers, not production-delivery health. Thus P5 report delivered, zero-High gate NOT passed.

P6.1 actual new verification:
- dotnet build SmartParking.slnx --nologo -m:1: exit0,0errors/56warnings,17.26s.
- dotnet test SmartParking.slnx --no-build --nologo -m:1: exit0,1161passed/0failed/109skipped/1270total. Per project Architecture12/0skip, Facility199/29skip, IoT1/0skip, Notification220/0skip, Operation66/3skip, Payment311/7skip, User352/70skip.
- npm --prefix FE run build: exit0, existing chunk-size/ineffective-import warnings.
- node --test --test-reporter=dot tests/frontend/*.test.cjs tests/postman/spark340-collection.test.cjs: exit0,263passed (261FE+2focused Postman).
- Broader tests/postman wildcard probe had284pass/1fail/285total: generic baseline Payment webhook environment-example assertion, unrelated to changed SPARK-340 artifacts. No values copied into report; no unrelated fix made. Scoped command above passed. Do not report the broader wildcard as green.
- Targeted local-secret/JWT/PEM scan on128 changed/new files at checkpoint:0flags; limitations/hash inventory in source-audit JSON. No schema changes/staged files in checkpoint inventory. git diff --check passed.

P6.2 prepared files:
- docs/04-testing/reports/SPARK-340-P5-review-20261117.md
- docs/04-testing/reports/SPARK-340-P6-delivery-20261117.md
- docs/04-testing/reports/SPARK-340-PR-draft.md
- docs/04-testing/reports/evidence/SPARK-340-P5-source-audit-20261117.json
- docs/04-testing/reports/evidence/SPARK-340-P6-verification-20261117.json

Local develop...HEAD0/0; feature delta remains in working tree. Remote freshness NOT VERIFIED. Proposed commit grouping is documented, not executed. No git add/reset/clean/fetch/rebase/commit/push or remote PR. No production code/schema edits, no application DB changes, no container restart or Newman mutation in this follow-up. External planning file read-only; delivery report acts as the local phase-status addendum.

Current handoff: P5 review and P6 verification/draft preparation executed. NOT MERGE-READY because F6 and recorded dispositions/acceptance remain open. Parent session stays active. No claim of full story closure or completed group UAT.

## 29. Authorized tester-branch publication — preflight checkpoint

User requested progress on P5 while publishing the repository branch so testers can verify functionality. This explicitly authorizes task-scoped commits/push to feat/SPARK-340-admin-platform-policies, not merge/deploy or remote PR creation. Existing temporary P4 closure and P5 findings remain unchanged.

Prepared docs/04-testing/reports/SPARK-340-tester-handoff.md: fetch/switch instructions, isolated tester setup, existing guide/checklist links, U06/U11/U20 focus, truthful U19 blocker, F6/F7/F8 known issues, per-case evidence requirements and concurrent P5 follow-up work. No production logic changed since the P6 verification run; no repeat broad test run is needed solely for these documentation/Git operations.

Preflight: current feature branch verified, no pre-existing staged files, origin is Quangthm/Mock-Project-Smart-Parking. Remote ls-remote required network escalation after sandbox DNS failure; escalated read succeeded and found no existing branch with this name. No remote history will be overwritten. Selected task files are grouped into five Conventional Commits for Operation, Payment/Notification, FE, Postman/evidence and docs. Exact file hashes are checkpointed in ignored .cache manifest and checked before staging. Targeted local-secret/JWT/private-key scan plus JSON/environment-example validation found no flags. Only .env.example is selected; .env/.cache/keys/raw exports remain excluded.

Unrelated modified docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json is deliberately excluded and preserved in working tree. No git add -A, no force push, no checkout/reset/clean, no container or database operation. Git commit/push execution follows this checkpoint; actual results will be recorded after remote acknowledgement.
