# Session Review — 2026-11-08

## 1. Session Information
- **Session ID:** TASK-SPARK-273-SESSION-20261108
- **Date:** 8–9 November 2026 (Asia/Saigon)
- **Agent:** Main Agent
- **Branch:** feature/SPARK-273-driver-book-parking-ahead-of-arrival
- **Related Task:** SPARK-273 / reservation first slice
- **Status:** In Progress — first slice committed; build success reported by user; tests explicitly deferred. Next phase is booking through confirmed reservation, with producer implementations retained by teammates and finalization design decisions pending.

## 2. Objectives
- [ ] Implement the attached first-slice reservation decisions D1–D4c, including automatic recovery, cancellation and financial reconciliation.
- [ ] Direct MediatR workflows, aggregate repositories and IUnitOfWork throughout the affected path; remove redundant forwarding services/stores.
- [ ] Consolidate affected schema in the original owning script only, replacing removed database guarantees in C# and disclosing bypasses.
- [ ] Reconcile canonical requirements, API/dependency contracts, tests and deployment guidance.
- [ ] Capture fresh verification; do not reuse vehicle-registration or pinned-handoff results.

## 3. Work Completed
- Read router, explicit user instructions, canonical coding guide, previous active vehicle session, architecture reference/guardrails and session protocol.
- User explicitly selected implementation of the attached reservation first slice after the branch/session mismatch was surfaced.
- Inspected Operations host, entities/mapping, repository/UoW interfaces, gate handlers, structure store, UserService outbox precedent and authorization retry policy.
- Existing Operations has no Driver JWT authentication or reservation admission Saga. Legacy gate handlers call a workflow-owning store. The structure store relies on database triggers for its site guard.
- Removed the separate commercial quote-acceptance round trip from the reservation preparation path; accepted terms are retained in the durable booking snapshot and the real Payment order is provisioned only after the hold is committed. This keeps the workflow aligned with the approved short creation flow and event-driven confirmation design.

## 4. Technical Decisions
- User's current schema/architecture instructions supersede the handoff's proposed overlays, trigger retention and additive-upgrade mechanism. No new database trigger/function or noncircular ALTER is authorized.
- Attached D1–D4c product decisions are implementation requirements; proposed routes, storage and integration details are not automatically approved architecture.
- Preserve the strict pre-T commit requirement. A pre-commit C# check or the existing BEFORE-row confirmation trigger does not prove it.
- Continue independent requirements/contract work while unresolved implementation directions are reviewed.

## 5. Files Changed
| File | Change | Reason |
|---|---|---|
| This session | Added | Dedicated reservation provenance; preserve existing vehicle session |

## 6. Issues and Blockers
- **OPEN dependency approval:** Operations needs JWT bearer, narrow AutoMapper and MassTransit PostgreSQL/RabbitMQ dependencies. Ask to reuse UserService versions; no package mutation until approved.
- **OPEN cutoff evidence:** strict commit-before-T cannot be inferred from a row-write timestamp. Confirmation must remain gated until a reviewed mechanism proves the boundary; changing to state-write time requires an explicit product amendment.
- **OPEN integration:** inspect/publish exact current-access, scoped resource, quote/coverage, financial outcome and gate evidence contracts; remote reads alone do not prove atomic acceptance against competing owners.
- **OPEN rollout:** CREATE IF NOT EXISTS does not upgrade existing tables or remove deployed triggers. Existing-volume transition requires separate owner review. Do not reset or auto-migrate databases.

## 7. Validation Results
- Build/tests: NOT RUN for this task yet.
- Git and source inspection only; all implementation/live acceptance remains [UNVERIFIED].

## 8. Git Status
- **Entry HEAD:** 7c47755d83cbe4eea2d2e887a2abc49e6decd203.
- **Entry working tree:** modified ai/AGENTS.md and ai/tasks/active/TASK-SPARK-243-SESSION-20261107.md; untracked ai/context/user-coding-instructions.md. Preserve all three.
- Latest commit already present: fix(user-service): make SPARK-373 media upgrade backward-compatible.
- No staging, commit, push, branch switch, application/container startup or database mutation authorized/performed.

## 9. Handoff / Next Steps
- [ ] Record answers to the package/cutoff questions before dependent implementation.
- [ ] Publish dated product decision and reconcile superseded reservation guidance.
- [ ] Publish reviewable API/dependency contracts and identify unresolved cross-owner atomicity decisions.
- [ ] Implement and verify each available checkpoint; distinguish scaffolding, handler-only, PostgreSQL and live evidence.

## 10. Context for Next Session
- Source: C:/Users/qtang/StudyMaterials/OJT/Project/SmartParkingTempDoc/AISession/11_6/SmartPark_Reservation_First_Slice_Decisions_2026-11-05 (2).md.
- Enter via ai/AGENTS.md and ai/context/user-coding-instructions.md.
- The source reviewed 3d3def7; current HEAD differs. UserService now publishes JWKS and has vehicle binding resolution; do not repeat the old missing-JWKS assertion.
- Approved auth ADR §9: three evaluations, new attempt IDs, 5-second reply timeout, 10/30-second waits, 90-second original deadline; unknown business effects are not failed effects.

## Continuation checkpoint — 9 November

User explicitly approved package additions, R-ARCH-01 shared consumer JWT component and listed cache/error settings, and R-ARCH-02 shared message contracts/UserService decisions/Operations retries/restricted broker trust. User chose to publish quote/coverage producer contracts for teammate integration. D4c remains unchanged; confirmation must remain gated pending commit-evidence design.

Latest user instruction: database-backed task policies and a separate idempotent defaults seed are preferred; full policy catalogue is owned by a teammate. Existing operational_policies lacks global scope/history/horizon/buffer/F. Asked before extending its shape; answer pending. This is an explicit task-scoped seed exception, not permission for schema overlays or applying SQL.

Implemented so far (not yet fully compiled): pure interval/deadline/refund-right rules; shared asynchronous JWKS cache and bearer registration; versioned Account-current DTOs; Operations durable request aggregate/direct MediatR handlers/repository/retry worker/Bus Outbox; UserService current-Account decision handler/repositories/consumer/reply; fresh request/decision/outbox CREATE declarations in original scripts; own receipt controller, actor adapter, built-in exception handler. Operations admission is opt-in and an Allowed receipt currently closes FAILED_DEPENDENCY/RESERVATION_FINALIZATION_NOT_READY with zero booking/claim/payment effect. Wiring/configuration, tests, policy persistence and legacy gate/structure refactor remain unfinished.

Verification: two restricted focused test starts returned no usable results (SDK --info showed Windows Service Control Manager access denied). One normal-access --no-restore run reached compilation but failed due stale/missing local FluentValidation/MediatR references. Solution restore subsequently passed (28 projects at that point). No tests passed yet. Further checks should be one current compile plus focused offline tests; PostgreSQL/broker/live verification goes to user deployment instructions. No container/app/database started. Do not reuse earlier vehicle test results.

Preserve initial user changes. New requirement edits propagated D1–D4c to reservation SRS/business rules/overview/use cases/transitions/policy catalogue. A transient Windows default-encoding issue in three markdown files was repaired using original Unicode characters; verify no mojibake remains. API design/architecture proposal still need status updates for approvals and implemented gated paths; links/indexes/acceptance/deployment checklist remain to finish.

## Current continuation checkpoint — 9 November (supersedes earlier pending statuses)

- Approved: reservation-only platform/site policy extension and separate default seed; late tolerance requires an explicit site choice. Policies and immutable effective snapshots have Domain types. `OperationalPolicyRow` moved from Persistence into Domain; repository retains JSON/EF conversion only. No full policy-management APIs.
- User clarified audit is owned by another service and approved versioned durable events plus fail-closed Gate behavior. Removed new IAuditLogRepository/AuditLogRepository/AuditLogRow, EF audit mapping and original Operations script's audit_logs declaration. Added OperationsGateAuditV1 shared contract, Application publisher port, Infrastructure scoped MassTransit publisher and explicit unconfigured 503 publisher. All four Gate write handlers capture events before UoW save/commit; departure replay does not publish again. Audit consumer/retention is delegated. No deployed audit rows were removed.
- `Messaging:AuditEnabled` is independently mapped from SMARTPARK_OPERATION_AUDIT_ENABLED; it does not require enabling Driver admission. The same Operations bus/outbox is used, with conditional authorization consumer/worker. Restricted broker credentials now reject whitespace. Queue binding must precede outbox relay; durable exchange without a binding is not delivery evidence.
- Explained src/Shared as previously approved JWT component + wire DTO projects, not a shared database/Domain/service. Explained account_access_decisions as approved R-ARCH-02 durable per-attempt authorization replay, not reservation/audit ownership or an ongoing access grant. No purge design implemented.
- Removed production workflow stores/forwarding service. Gate and structure workflows now live in MediatR handlers; data reads/writes use repositories/UoW; state transitions use Domain methods. All supported commitment mutations take the site guard before reads. UoW enforces held/inactive/scope/history guards and concurrency versions. Removed Operations trigger/function declarations and destructive tombstone DROP from original schema. Direct SQL/bulk/DbContext/cascade bypasses remain disclosed. No additional schema overlay.
- Tests rewritten to dispatch current Gate handlers through DI, cover guard/read/save order, rejected entry, event failure and replay; structure HTTP tests now fake repository/guard/UoW boundaries instead of workflow stores. PostgreSQL fixtures dispatch actual handlers. Test-specific ALTER/function failure injection was removed; failed Domain projection verifies transaction rollback instead.
- Added current deployment instructions at docs/04-testing/reports/SPARK-273-deployment-checklist.md and product-only acceptance scenarios. Current admission still deliberately ends timely Allowed as FAILED_DEPENDENCY / RESERVATION_FINALIZATION_NOT_READY. Verified vehicle/Facility acceptance, commercial producers, full cancellation/allocation/refund orchestration and D4c commit proof are incomplete/gated. Policy snapshot columns are not proof of a live acceptance writer.

### Verification at this checkpoint

The requested current solution build did NOT execute: automatic approval review failed due usage limit (not an unsafe-action finding). Do not bypass/retry this escalation without resolution/user guidance. No current build or tests have passed. User-run compile/focused tests and deployment database/broker scenarios are documented. Earlier restore/build attempts are historical only.

Static checks: PowerShell parser reported no errors in setup.ps1 and scripts/run-schema-service.ps1; git diff --check passed after correcting duplicated CR line endings. Source search found no remaining production/test references to removed stores or audit repository. No Operations CREATE FUNCTION/TRIGGER, ALTER or DROP remains in its original schema. Further final static checks should be recorded below.

Git at continuation includes externally staged additions, including prior task files. Preserve the index and worktree; the agent did not stage, unstage, commit, push or switch branches. No service/container/database was started or mutated.

### Latest authorization correction — user confirmed 9 November

User selected exactly one evaluation per workflow, replay its result. This supersedes the earlier per-attempt implementation for this reservation slice. Changed AccountAccessDecision and original User schema to unique WorkflowId, removed persisted AttemptId/AttemptNumber, changed repository advisory guard/find to WorkflowId, fingerprint to logical actor/operation/admission/overall-deadline fields. Handler reads access only when no persisted workflow result exists; retains a completed result even if that delivery timed out. Later timely deliveries replay original DecisionId/DecidedAt/Allowed with current envelope attempt fields. UserService reply MessageId now uses AttemptId so Operations inbox does not discard a new reply envelope carrying the same business decision. Operations accepts original decision time from admission onward while still fencing current attempt and deadline. Global ADR documents this explicit reservation exception; no global behavior changed silently.

New AccountCurrentWorkflowTests cover Allow/Deny replay after authority changes, changed-workflow actor rejection, expired delivery and retaining a completed evaluation after delivery timeout. Admission tests cover old-envelope rejection and current-envelope original-decision acceptance. Not executed: current build approval failure still unresolved. The guarantee is one durable logical result; a crash/rollback before first commit may repeat the physical read. Deployment checklist adds workflow lock/uniqueness races and lost-ack replay checks.

Final static checks for this continuation: git diff --check passed; changed project/solution XML parsed (9 files); changed text scan found no replacement-character/mojibake markers; new reservation/shared document relative links resolved. Source scan confirms the User decision entity/repository no longer stores or keys by AttemptId, while response envelopes retain AttemptId intentionally. Operations original schema contains no audit_logs declaration, CREATE FUNCTION/TRIGGER, ALTER TABLE or DROP. These checks do not establish C# compilation or runtime correctness; deployment instructions remain the current verification handoff.

## Deployment/shared-code and frontend readiness clarification — 9 November

User asked whether shared projects undermine microservices, whether Facility already has equivalent code, and whether a Driver can book through frontend/Postman now. Read current Facility composition/adapters, project references, Dockerfile/Compose and Driver booking/payment/history sources. Facility in this checkout uses HTTP identity/scope adapters (including /api/auth/me), not the shared JWT/JWKS middleware; teammate branches/deployments remain uninspected. Shared project references are source/build dependencies; per-service publish images contain their own assemblies and do not require Shared at runtime. Separate source checkout without referenced code will not build; independently versioned packages or schema contracts are future options, not silently implemented changes.

Confirmed source still deliberately gates Allowed admission; no real confirmed booking is currently possible through either client. Current frontend loads vehicles but uses local lot/booking sources and does not submit/poll new admission endpoints; payment screen waits for a real order. Created docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md with priorities, backend prerequisites and frontend acceptance backlog; linked handoff index and expanded Shared README. No frontend/backend source/config changes made this turn, no builds/services/databases started. Preserved observed unrelated deletion docs/04-testing/postman/login-demo-guide.md and all existing staged/worktree changes.

## Service independence and first-slice commit checkpoint — 9 November

User explicitly requests deleting src/Shared, placing reservation necessities inside Operations and stopping at a Git summary/description before implementing the remaining workflow. This supersedes the shared-project portions of R-ARCH-01/02; approved authentication behavior, once-per-workflow result, broker trust and durable audit remain unchanged.

Preflight: current HEAD is 1ad2f929812a3f7629442d2500e24e14b4563614 (external merge since earlier checkpoint). Preserve staged/worktree changes. Deletion is explicitly authorized for C:/Github/smart-parking-system/src/Shared only, after copying JWT implementation into Operations Infrastructure, message types into Operations Application and independent authorization DTOs into UserService Application. Preserve wire namespaces/type names for MassTransit compatibility; the common namespace is no longer shared source or assembly. No inter-service project reference is introduced. The test harness references both services and uses an explicit assembly alias where necessary. JWT bearer 10.0.12 moves to Operations Infrastructure; no new package/version introduced.

Service-local relocation completed in the working tree: removed src/Shared, shared solution/project references and Docker shared-source copy. Docker now restores/publishes only selected service source. Added ServiceLocalAccountAccessContractTests for independent assembly wire identity/property shape/JSON round-trip; updated User test assembly alias and existing auth test reference. No frontend edits or remaining-workflow implementation. User happy path and unresolved post-payment/cash/zero-price/terms questions are recorded in the frontend handoff for the next phase, after user commit.

Fresh static validation: all 27 production ProjectReferences across four services resolve within their own service directory; solution project targets and edited project XML resolve; independent Account-current source definitions match; no Shared project/path references remain in source/tests/Docker/solution; local handoff links resolve; git diff --check passes; no FE diff. These are not build/runtime results. Current C# tests, service publish and PostgreSQL/broker behavior remain unverified; follow the deployment checklist, including the new contract test. No build escalation was retried after the earlier approval-system failure.

Stopped at requested commit-summary boundary. Agent did not stage/unstage/commit/push. Existing index still has old Shared additions (AD entries), so user must stage the current deletions/moves as part of the reviewed commit rather than committing the stale index. Preserve unrelated changes. Next phase begins only when the user resumes after committing.

## Local setup documentation — 9 November

User requested documenting the reservation environment settings for teammates. Added LOCAL-SETUP.md section 5.6 in the guide's existing Vietnamese language: root .env/manual key merge; each flag and credential; isolated personal-development broker credential reuse with literal values (no loader interpolation); shared/deployed service permissions; UserService whole-bus credential/relay implications; authorization queues and separately provisioned Gate audit topology; host/user-secrets/Docker reload commands; schema prerequisites; expected admission-only responses and unverified finalization limits. Updated section 5.5's outdated blanket statement to identify Operations-local JWT implementation without implying Facility/Payment coverage.

Documentation only; no .env secrets read or changed, no runtime/config/source/schema modifications, no service/container starts or database writes, no staging/commit. Current build/runtime verification remains UNVERIFIED; user-run deployment checklist is linked. Reviewed commands and keys against current runner/setup/Compose and messaging registrations. Documentation whitespace/link checks are recorded in the turn outcome; no C# tests needed for this documentation change.

## Booking-through-confirmation phase — user resumed 9 November

User reports a successful build and explicitly defers test execution until the reservation workflow can be tested through frontend/Postman. This supersedes the prior build-pending status for the first slice only; no command/output was supplied, and the agent does not claim to have run it. Test/runtime behavior remains unverified, now intentionally deferred. Observed HEAD is 0a2383bdfedf34058c4a2bae99c1ae9087162da0 (`feat(reservation): add gated admission foundation [SPARK-273]`). Initial dirty files are ai/AGENTS.md, the SPARK-243 session and untracked ai/context/user-coding-instructions.md; preserve them.

User resumed implementation and answered scope questions: next deliverable is **booking through confirmed reservation**; **producers remain with teammates**. This is not authorization to implement Payment/Facility/coverage producer APIs or change the earlier no-frontend-edit instruction. Full post-confirmation lifecycle remains subsequent work. Required hold expiry, late-money handling and reconciliation cannot be omitted from a safe booking/payment path; owner boundaries and completion gates must be explicit.

Reviewed current Payment pricing/order/status/webhook and success event, existing User vehicle-binding lookup, Operations request/Domain/UoW and policy catalogue/seed. Existing generic Payment order handler uses a fixed 15-minute expiry and client-requested amount; its event lacks accepted terms version. It is not the published reservation producer contract. Do not wire it as a substitute or use demo-pay as payment evidence. Missing owner implementations and unresolved D4c proof remain material dependencies.

Prepared a concrete continuation review in reservation-first-slice-implementation-proposal.md: owner integration prerequisites, proposed browser/Payment responsibilities, phase scope, and the limits of post-commit observation versus full lost-ack recovery. No runtime/schema/frontend changes or tests started. User's repeated constraints remain: direct MediatR/repositories/UoW, Domain entities, database policy revisions plus separate seed, durable audit events with consumption elsewhere, original schemas only, no shared source project.

PostgreSQL 16 source confirms commit timestamp recording precedes WAL flush and transaction-status marking; commit metadata alone is not proof of durable completion before T. Asked the user to resolve that specific boundary, provide delivered owner contracts or approve consumer implementation against the proposed interfaces, and approve receipt-based post-payment progression/own booking routes. These answers are pending; no dependent architecture silently implemented. Documentation diff whitespace check and proposal relative-link check pass. Runtime/test execution remains deferred at the user's request.

## Approved cutoff/binding amendments and implementation in progress — 9 November

User approved PostgreSQL-recorded commit timestamp strictly before T, with durable commit/evidence verified before exposing confirmation. This replaces the earlier durable-flush-before-T interpretation. User approved owner-contract recommendations but producer implementations remain with teammates. Form must display calculated amount as slot/time changes before payment-method submission; backend independently calculates/validates it. Frontend remains handoff-only. Vehicle binding is evaluated once per workflow and persisted; concurrent changes may compromise the accepted state, explicitly acknowledged by the user. Crash before first persisted result may repeat the physical lookup, as with Account-current. User then corrected identity: use canonical PLATE for conflict checks, including unregistered guests; vehicle ID remains registration reference. Explicitly approved extending UserService binding response with canonicalPlate.

Found stale src/Shared and two audit repository files in committed baseline (old staged additions had survived). Removed those remnants under existing authorization, without staging. Added Domain reviewed terms and reservation booking/candidate fields, Application owner ports and review/preview/progress/read handlers, original Operations schema fields/table, repository commit-metadata reader, API routes, worker progression and event publisher. Operations database Compose command now sets track_commit_timestamp=on; no container restarted. Candidate uses synchronous_commit=on and full transaction identity plus cluster ID; later transaction checks committed status and stored timestamp before exposing CONFIRMED. Missing metadata retains CONFIRMATION_PENDING/claim, not a guessed outcome. Candidate included in structure commitments. Booking facts use durable outbox, not an audit repository.

Implemented User lookup canonicalPlate extension, Operations typed HTTP lookup and once-per-workflow binding checkpoint. Gate writes take plate then site guards; active physical plate check is cross-site. Booking repository checks cross-site plate intervals/occupancy. Terms/pricing are immutable accepted snapshots, policies remain database-backed with existing separate seed. Added price-preview/terms and own-booking routes; default commercial/Facility/capacity implementations remain explicit unavailable, so live booking remains disabled. **Work in progress:** capacity/protection/quota acceptance still needs its real implementation (not a teammate Payment producer); late/additional-collection notifications/reconciliation need completion; updated API/owner/frontend/deployment handoffs and static checks still to finish. Do not describe this as finished or runtime-tested.

Build attempt: `dotnet build src/Services/OperationService/SmartParking.OperationService.API/SmartParking.OperationService.API.csproj --no-restore --nologo` returned exit 1 with "Build FAILED", zero warnings/errors and no compiler diagnostics. No test execution. Do not reuse the user's earlier first-slice build as proof of these edits. No app/DB/broker started; no secrets read; no staging/commit.

### Latest walk-in/scan decisions and pending questions

User explicitly permits walk-in entry despite a future reservation for the same plate; handle conflict when due. If physical check-in is before T and scan finds the car on another slot, request reallocation, release the unused original protected slot/claim and recover affected future reservations within allocation lead time. If it parks in the intended slot, standard reservation plus early-use pricing applies. Preserve actual occupancy; no forced displacement. Post-T different-slot parking should trigger reallocation plus a wrong-parking fee per user's proposal; amount/configuration/exceptions are not yet specified. Do not infer malicious intent from timing. New post-confirmation behavior expands the discussion beyond the selected booking-through-confirmation phase; keep exact implementation scope clear.

Asked two unresolved financial questions: (1) 4 booked hours at 1,000/hour, 2,000 deposit, one early + one booked hour used: is checkout 3,000 (unpaid booked balance + early use) or 1,000 (waive unpaid booked balance)? (2) wrong-parking fee amount/configuration authority, exceptions for unusable assigned slot/authorized change, and exact-T branch. Await answers before implementing amounts/waivers/penalties. User repeated these decisions during continuation; preserve them rather than reopening allow-walk-in or plate identity.

## Latest answers and durable Facility direction — 9 November

The financial questions above are answered: checkout is **3,000 VND**, full booked price remains due. Wrong-parking is a separate session fee, Admin default/bounds with lot-scoped Owner override; waive if assigned slot unusable or Operator/system authorized relocation. Exact T arrival is on time, enabled late tolerance retains minimum 15 minutes (existing 15–30 range). Numeric fee/formula remains unspecified; no fee seed is authorized. User explicitly kept **booking through confirmation** in this phase; scan/reallocation/checkout go in next-phase handoff. Repeated older example wording does not revoke these explicit answers.

Added dated confirmation/arrival decision, detailed current Payment producer findings, refreshed frontend/API/local-setup/deployment handoffs. Price form/review instructions, canonical plate lookup and recorded commit cutoff now distinguish target/source/runtime. Current source still unavailable for real booking; retain task in active. Fixed draft order UNKNOWN versus fenced CLOSED_NO_ORDER, collection identity contradictions/order stability, own-list CreatedAt cursor, effective policy disclosure, shared reservation repository DI instance, disabled messaging DI fallbacks and Gate test lock-order assertion. No tests/build rerun; no frontend changes.

R-ARCH-05 initially proposed a complete local Facility resource projection. User instead directed inspecting/reusing Facility's durable slot-management path for Operations dependencies. Inspected RemoveSlot handler, StructureUnitOfWork, PostgresParkingStructureRepository, StructureSynchronizationService/store/worker: durable preparing token precedes remote hold; ready outcome commits with local change; fenced recovery distinguishes abort from committed replay; worker and foreground recover. This is the approved pattern to reuse, **not permission to copy legacy workflow services/stores or 24-hour retry cadence**. Full mirrored catalogue is NOT approved. New reverse protocol must provide complete token-bound resource facts, immutable replay, exact-token completion/abort tombstones, no network inside Operations transaction/site guard, and no circular waiting with Facility's existing reverse calls. See 2026-11-09-reservation-facility-durable-dependency.md.

User explicitly approved **dedicated Operations credential restricted to new Facility handshake endpoints**. Do not reuse Services:Key or infer vehicle-lookup exception. Producer implementation remains with teammate; exact transport endpoint/header details not yet delivered. Operations-side durable state/repository/MediatR continuation is being implemented; missing producer must fail closed. No full resource projection table, new business defaults, audit repository or SQL overlay.

Static evidence so far: git diff --check passed; six revised handoff/API documents had no missing relative file links; 22 production project XML files parsed. One broad source scan included a nonexistent root Dockerfile and must not be counted as a clean scan; actual backend Dockerfile is docker/Dockerfile.backend. No compiler/runtime proof. Existing dirty router, SPARK-243 session and user instruction file preserved; no staging/commit/push.

## Durable Facility implementation checkpoint — 10 November

Added Domain ReservationFacilityDependency with PREPARING/HELD/READY/ABORTING/COMPLETED, immutable workflow/purpose/scope/fingerprint, per-workflow resource evidence, ready outcome, resolution, version and due time. Repository uses existing ReservationDbContext/UoW; MediatR handlers perform remote acquire/complete/abort outside transactions, then lock/reload/fence replies. Dedicated independent worker keeps cleanup alive after receipt completion and reschedules failures. Original Operations script gains only the final idempotent dependency table/index; no new seed/default, full mirror, audit repository or overlay. Missing Facility producer remains unavailable. Dedicated credential approved but exact remote endpoints/header not delivered; no secrets or permissive transport invented.

Integrated creation of admission/confirmation dependency intents into BookingProgressHandler. HELD facts replace the unsafe Facility read under the site guard; local hold/candidate and READY outcome commit together. Terminal rejected/expired receipt fences unused dependencies. Vehicle binding lookup moved outside SQL transaction with workflow-specific session advisory guard (separate from plate/site entitlement guards). Same workflow remains serialized through its checkpoints. Changed quote validation to reject inconsistent FULL/PERCENTAGE deposits; FIXED may exceed booked charge. Explicit request-backed SQL currency non-null check. Fixed retained-work starvation with a fresh-scope retry checkpoint and balanced scalar connection opens/closes.

Added ReservationFacilityDependencyTests (NOT RUN) for abort vs delayed acquire, immutable replay/deadline, committed outcome recovery. Existing commit-evidence and Gate tests also remain NOT RUN. No build retry, no runtime/DB/broker start, no frontend changes, staging or commit. Latest static diff check passed before final documentation checkpoint; recheck after edits. Active session stays active.

**Remaining own implementation, do not mislabel as only producer blockers:** complete capacity/quota/protection/backup and Account restriction enforcement; retire expired holds coherently before strengthening global plate SQL exclusion; move commercial acceptance/order/collection/reconciliation calls out of guarded transactions with durable effect intent/cleanup; independent financial reconciliation/notifications after terminal receipts; bounded unknown-outcome scheduling and current integration/compile evidence. Current acceptance can otherwise leak a remotely accepted commercial effect if local deadline/capacity rejects, and terminal receipts miss later collections. Default unavailable adapters prevent live progression; never replace them with success to claim readiness. Producer handoff and Facility handoff list required owner contracts. Full workflow remains unfinished.

Final static checkpoint: git diff --check clean; no frontend diff; five current handoff/decision/checklist documents have no missing relative file targets. Corrected source scan (actual docker/Dockerfile.backend) found no src/Shared path, removed audit-repository reference, or Operations CREATE FUNCTION/TRIGGER, ALTER TABLE or DROP TABLE. Git reports 67 changed/untracked paths including preserved pre-existing user work; this is not a count of solely agent edits. Static checks do not validate compilation or runtime; no new build/test pass claimed.

## Approved cutoff replacement — 10 November, latest

User explicitly approved final backend eligibility checkpoint time and requested replacing the prior implementation/docs, then a remaining-task checklist. This supersedes every historical recorded-commit/strict-commit-before-T statement above. Capture one database clock_timestamp after required guards/current booking/Facility facts; time-sensitive confirmation checks use that value, strictly <T. Persist CONFIRMED, confirmation_evaluated_at/confirmed_at (same decision time), effect ID, receipt, Facility READY and outbox in one transaction. Durable commit before visibility remains; no deadline recheck at commit. Lost acknowledgement recovers committed identity; rollback requires a fresh evaluation.

Removed candidate state, transaction/cluster evidence fields, metadata readers and Compose track_commit_timestamp command/comment. Kept SET LOCAL synchronous_commit=on. Domain ConfirmAtEvaluation replaces StageConfirmation/VerifyConfirmation; UoW enforces checkpoint/effect/first-confirmation consistency and immutability. Original Operations schema declares final timestamp/checks directly; no overlay/ALTER/migration. Capacity port now receives evaluatedAt explicitly; its missing implementation must not use another clock. Removed candidate-specific structure/plate branches. Replaced ReservationCommitEvidenceTests with ReservationEvaluationCheckpointTests; tests NOT RUN.

Updated canonical reservation requirements/SRS/state transitions/scenarios, API, architecture, local setup, producer/Facility/frontend handoffs and deployment checklist. New current decision: docs/05-project-management/decision-log/2026-11-10-reservation-evaluation-checkpoint.md. New owned completion checklist: docs/05-project-management/handoffs/2026-11-10-reservation-completion-checklist.md. Historical dated decisions explicitly point to the superseding rule. Monthly/yearly strict commit rules are outside this amendment and unchanged.

Verification: diff whitespace check passes (Git emits only normal LF/CRLF conversion notices); source/tests/original SQL/Compose scan has no old candidate state, commit-proof methods or metadata lookup/settings. Current new document links resolve; broader changed-doc check found two pre-existing missing README targets (OWNER_ONBOARDING_BE_HANDOFF.md and 2026-10-16-fe-v2.1-recovery.md), not introduced by this change. No fresh build/test result, no DB/runtime start, no frontend edit, no staging/commit. User's test deferral remains in force. Existing volumes/candidate rows, if any, need deployment inventory/reconciliation; no conversion/reset was attempted. Full reservation remains active/gated for the remaining tasks; this checkpoint replaces the cutoff implementation only.


## Commit 1 review and validation handoff — 11 November

Branch feature/SPARK-273-driver-book-parking-ahead-of-arrival, base fff33f8f after develop merge. Commit 1 draft covers Facility acquire/complete/abort with dedicated Operations JWT, guarded CAR availability/quota/plate and cancellation-history checks, transactional hold/availability/confirmation/Facility-applied facts, and Facility/Operations error and request logging. Selected-slot exclusivity, authoritative operational_capacity_configs, and explicit legacy cancellation reconciliation were approved. Facility Gate writes use repository/UoW, with structure edit/synchronization workflows in Application handlers. The index already contained restored draft paths. The agent did not stage or commit.

Reviewed current Payment producer source after the merge. A teammate can work from the published producer handoff. Operations still registers UnavailableReservationCommercialOwner. Price preview and terms fail closed before booking creation. Even with reviewed terms, quote/coverage acceptance precedes AttachBooking. A paid hold/order is therefore not waiting for a provider callback on this checkout. Real confirmation requires Payment delivery plus Operations Commit 2 wiring and durable commercial effect/reconciliation work. Keep SPARK-273 active. Validation deferred to the user's isolated environment. See the deployment checklist for current-branch versus post-integration observations.

Static validation on 11 November: git diff HEAD --check passed. Setup and host runner PowerShell syntax and the Facility Infrastructure project XML parsed. Reservation handoff, Facility API and deployment-checklist relative file targets resolved. No Payment or frontend source diff. Build, tests and live PostgreSQL/RabbitMQ behavior remain UNVERIFIED. Review the restored index alongside the working tree before committing, including new untracked files.

Local setup follow-up: documented a Windows PowerShell OpenSSL fallback and manual PEM copy for host runner, Docker and Visual Studio. Verified an ephemeral 3072-bit OpenSSL pair imports through the service's .NET RSA PEM API and has matching public parameters. No local user keys or settings changed.

## Session Review — 2026-11-11 (documentation alignment; implementation outstanding)

**Repository reference reviewed:** `feature/SPARK-273-driver-book-parking-ahead-of-arrival` at `8adf317304988098c51a948faad28a940b21667b`. **No GitHub/Jira writes, implementation, build, tests or deployment performed by this documentation update.**

**User-approved architecture direction:** [11 November decision](../../../docs/05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) and [cross-team handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md).

1. Successful creation request terminates after the durable Operations `PENDING_PAYMENT` hold/claim/outbox; actual reservation remains pending until a separate Payment-collection consumer confirms. Preserve existing authorization and vehicle-binding workflow. Propose versioned request result token `HOLD_CREATED`; client compatibility still requires agreement.
2. Retain versioned, immutable reviewed pricing/coverage terms. Merge commercial acceptance with one idempotent post-hold `EnsureOrder`, outside guarded Operations transaction. Payment owns tariff/deposit calculation and provider verification; Operations owns H, capacity and confirmation. Owner endpoint/wire names and quote storage remain contract details to agree.
3. Payment publishes each verified collection through a transactional outbox; Operations consumes with inbox deduplication and guarded confirmation. Replace continuous pending-payment polling with targeted recovery after missing/late/additional/unknown events and after terminal receipt.
4. For live protected claims, eventual aggregate-only confirmation is allowed **only after** Facility/Operations/Gate writer serialization and timely invalidation are proved. Until then, retain the existing Facility confirmation dependency and capacity checks. Expired/invalidated holds require fresh guarded pre-T claim reacquisition or Payment reconciliation; T cutoff remains the November 10 single DB evaluation timestamp.
5. Pending release gates: Payment reservation producers and machine authorization; Facility fast-path proof; Operations order stages and event consumer; frontend integration; actual build, deployment and cross-service tests. Current commercial adapter still fails closed. Do not mark ticket Done based solely on handoff approval.

**Next steps:** owners agree OpenAPI/event DTOs, compensation/coverage handshake and topology; implement under the shared coding skill, verify test cases in the updated SPARK-273 deployment checklist, and only then consider replacing the Facility confirmation dependency with the fast path. This session appendix does not erase earlier working history.

## Continuation checkpoint — 11 November 2026 (implementation)

The earlier four-file change was preparatory only: it removed the standalone quote-acceptance call but did not make creation short, provision orders independently, add verified-event confirmation, or provide the required Postman deliverables. This continuation adds the Operations-side workflow stages that can be implemented without inventing Payment's missing wire contracts.

### Implemented in source

- `ReservationRequest.AttachBooking` now terminates the receipt as `HOLD_CREATED`; admission responses expose `contractVersion: 2`. The same guarded Operations UoW commits the `PENDING_PAYMENT` reservation, accepted review, dependency completion and hold fact. `ReservationRow.Status` remains `PENDING_PAYMENT`. `ProgressReservationHandler` no longer looks up collections or confirms paid reservations.
- `ReservationRow` persists order provisioning, zero-price acceptance, hold expiry and confirmation recovery state. A distinct background worker dispatches order, expiry and guarded zero-price handlers. Stable operation identity is the original reservation request ID. The worker persists exact accepted terms and H; `EnsureOrderAsync` runs only after its intent transaction commits. Unknown outcomes retry the same operation; created order IDs and closed-no-order outcomes are persisted. Terms are not silently repriced.
- Known no-effect `PENDING` order/zero-price acceptance work is closed when its accepted terms or H expire; an `UNKNOWN` outcome is not misrepresented as absence and remains available for same-ID owner recovery.
- Genuine zero-price terms use a separate owner acceptance operation and no synthetic order. Confirmation still requires owner acceptance and the existing Facility confirmation dependency, availability/plate guards, and the strict database evaluation timestamp before reservation start. Paid confirmation remains unavailable without a verified collection consumer.
- The Operations commercial adapter still returns `OWNER_UNAVAILABLE` for preview, review and order/zero-price acceptance. This is deliberately fail-closed; there is no fabricated quote/order or successful Payment result.
- Created the SPARK-273 Postman Collection v2.1, blank local environment, and manual guide. The collection covers actual Operations routes, stable create replay, exact hold deadline/order state, conditional real Payment checkout/status, overlapping-capacity conflict and expiry checks. It does not invent an event or API endpoint.
- Updated the API design, deployment checklist and testing index. Existing schema SQL is the original owning Operations schema script; no database was changed. A reviewed migration/reconciliation is still required for already-created databases.

### Remaining requirements and blockers

- Payment's authenticated reservation `EnsureOrder`/zero-price producer contracts and implementation are unavailable. The adapter therefore blocks live preview/review and prevents end-to-end order provisioning.
- The November 11 decision leaves the versioned verified-collection event schema, identity, topology and trust contract for cross-owner agreement. No paid reservation consumer, durable collection inbox/deduplication, paid confirmation command, late-money recovery or targeted reconciliation dispatcher has been added. Do not repurpose legacy `PaymentSucceededV1` (gate settlement) as collection evidence. `CollectionsAsync` remains a port for future targeted recovery and is not polled in normal creation.
- Preserve Facility confirmation and fresh capacity/plate/site checks. Writer-safety evidence for aggregate-only confirmation has not been produced; optimization remains pending. Paid expired-hold reacquisition/reconciliation depends on the event/recovery contract.
- `OWNER_UNAVAILABLE` means the current Postman run is blocked at preview/review; it is not evidence that the later flows passed. No PostgreSQL, RabbitMQ, provider or browser acceptance was run.

### Files created for this continuation

- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationOrderProvisioning.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationConfirmationHandlers.cs`
- `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationOrderProvisioningWorker.cs`
- `tests/Services/SmartParking.FacilityService.Tests/ReservationCreationLifecycleTests.cs`
- `docs/04-testing/postman/SPARK-273-Reservation-Flow.postman_collection.json`
- `docs/04-testing/postman/SPARK-273-Local.postman_environment.example.json`
- `docs/04-testing/postman/SPARK-273-manual-test-guide.md`

### Validation

- **PASSED:** focused `ReservationCreationLifecycleTests` plus `ReservationEvaluationCheckpointTests` (9 tests total); Operations API project build; Postman collection and environment JSON parsing; `git diff --check`.
- **NOT RUN:** PostgreSQL/RabbitMQ integration, provider checkout/webhook, cross-service contract tests, collection confirmation/reconciliation, and Postman live run. The current fail-closed adapter blocks the last group.

## Develop merge checkpoint — 11 November 2026

- Resolved the six conflict files in the working tree against develop at `0cc18fa6`. Preserved the SPARK-273 MediatR/repository/UoW architecture and removed the incoming legacy gate stores. Consolidated the checkout consumer into the existing Operations bus registration.
- User-approved behavior: check-in opens the session while its reservation allocation remains RESERVED. Simulated scan records occupancy. PaymentService settles the checkout order; a matching payment event records settlement and triggers simulated gate departure at the event timestamp. Operations closes the session, releases the local allocation, and requests Facility release for a scanned slot. See the [scan API design](../../../docs/03-design/api/parking-session-scan-api-design.md).
- User deferred Facility claim-scoped release. A retry after a lost Facility release response can release a newer occupant. The Facility claim and release contract and schema were left unchanged. Existing Operations databases need a reviewed schema transition for the scan and checkout columns.
- Solution build succeeded with `dotnet build SmartParking.slnx --nologo -m:1 --no-restore -v:q`. Frontend regression suite passed. Backend tests remain [UNVERIFIED] because VSTest could not connect to its local test host, and the focused out-of-sandbox run was declined. No live database, broker, or provider validation ran.
- The sandbox blocked writing the Git index and staging approval was declined. Git still lists six unmerged paths. Next step is to review and stage the working-tree resolution and its supporting changes, then complete the merge. No commit or push was made.

## SPARK-273 reservation-slice finalization checkpoint — 12 November 2026

### Source review and current integration state

- Reviewed current local versions of `BookingProgressHandler`, `ReservationOrderProvisioning`, `ReservationConfirmationHandlers`, `BookingContracts`, `ReservationBooking`, `ReservationMessaging`, `ReservationOwners`, `ReservationLifecycleEvents` and `ReservationBookingRepository`, plus the application/infrastructure/persistence registrations and the existing architecture/API/Payment handoffs.
- Confirmed the source path preserves committed `PENDING_PAYMENT` hold → terminal `HOLD_CREATED` receipt → same-context outbox hold fact → hold consumer dispatch to positive-price order provisioning or zero-price acceptance. The order request is made outside the guarded Operations transaction and records Payment's returned `OrderId`; it does not manufacture an order.
- Confirmed `ReservationDepositCollectedV1` is consumed by the existing MassTransit registration and dispatched to guarded binding validation/confirmation. The pre-T evaluation, Facility dependency, plate/capacity guards and committed-only `CONFIRMED` behavior remain. Payment's generic `PaymentSucceededV1` consumer remains the separate Gate-settlement path.
- Operations owns the local `IReservationCommercialOwner` port, but the registered `UnavailableReservationCommercialOwner` still blocks preview, quote, order, zero-price acceptance, collection lookup and reconciliation. The local Payment event consumer has booking-state/`CollectionId` idempotency but no EF Inbox middleware; reliable Payment-side outcome delivery remains blocked. Facility/vehicle adapters and the Operations MassTransit Bus Outbox are real source registrations; deployment readiness is not established.
- Reviewed/updated the existing API design, 11 November implementation-alignment handoff, Payment producer handoff and deployment checklist. Added the four-item **Deferred Implementation Backlog — Post Reservation/Payment Integration**. No architecture decision, Payment implementation, Jira item, scheduler, worker, schema, or test artifact was added.
- The pre-existing worktree edit to `ReservationMessaging.cs` (hold consumer/endpoint registration) and other unrelated dirty files were preserved and not staged or committed. `ReservationOrderProvisioningWorker.cs` remains unchanged.

### Validation and status

- **PASS:** `dotnet build SmartParking.slnx --nologo -m:1 --no-restore` — build succeeded, 0 errors, 4 warnings, all pre-existing NuGet advisories: NU1902 MessagePack 2.5.302 and NU1903 SignalR StackExchangeRedis 10.0.12. No dependency or unrelated source changes were made.
- **PASS:** `git diff --check` — no whitespace errors; Git reported the existing LF-to-CRLF working-copy warning for the alignment handoff.
- Unit, integration, smoke, Postman, Docker and manual verification: **NOT RUN — USER VERIFICATION REQUIRED**, per user instruction.
- PaymentService/Facility live integration and end-to-end reservation/payment flow: **blocked/unverified**; do not claim readiness until the Payment team implementation and cross-owner contracts are reviewed.

### Git checkpoint

- Branch: `feature/SPARK-273-driver-book-parking-ahead-of-arrival`; HEAD at start: `532b09eaf89a2bfa43b00492a16e79cde5160e02`.
- The existing worktree contained unrelated instructions/session/test changes and an uncommitted `ReservationMessaging.cs` hold-consumer change. They remain unstaged and untouched by this documentation slice. No stage, commit, push, merge, Jira or deployment operation was performed.

### Next actionable step

- User reviews the source-level integration handoff and backlog. When the PaymentService implementation is available (including the user's review of PR #98 / SPARK-391), agree the exact producer DTO, topology/authentication, immutable binding, and durable outcome behavior before wiring a real adapter. Scheduler, worker removal and additional recovery remain deferred.

## PaymentService integration checkpoint — 12 November 2026

This checkpoint supersedes the preceding statement that the registered commercial adapter is entirely unavailable. The Operations→Payment adapter is now real for preview, quote and post-hold order provisioning. Zero-price acceptance and reconciliation remain deliberately fail-closed, and the current paid-confirmation path consumes the existing generic `PaymentSucceededV1` rather than the separately proposed `ReservationDepositCollectedV1`. No live cross-service, database, broker, provider or browser verification was run.

### Implemented source changes

- Replaced `UnavailableReservationCommercialOwner` with `ReservationCommercialClient` as the registered `IReservationCommercialOwner`. It calls Payment's `POST /api/v1/pricing/reservations/preview`, `POST /api/v1/pricing/reservations/quote` and `PUT /internal/reservation-deposit-orders/{operationId}`; it validates the success/data envelope, Payment `errorCode` responses, echoed order request fields and the returned authoritative order ID.
- `AcceptZeroPriceAsync` remains `503 OWNER_UNAVAILABLE` because Payment has no zero-price acceptance endpoint. `ReconcileAsync` remains `503 OWNER_UNAVAILABLE` under the no-refund-infrastructure scope. `CollectionsAsync` maps Payment's lookup, but fails `OWNER_CONTRACT_INVALID` when Payment omits the quote/terms binding (`QuoteId` empty or terms version blank), which the current `PaymentOrder` does not persist.
- `IReservationCommercialOwner.QuoteAsync` now takes the authenticated tenant ID; `ReviewReservationTermsHandler` supplies the persisted reservation tenant so the quote is issued for the correct tenant. The client also forwards the incoming bearer plus actor Account/tenant/site headers and the configured `Services:Key` as `X-Internal-Service-Key`.
- Added `ReservationPaymentEvidence`, `RecordReservationPaymentCommand` and `RecordReservationPaymentHandler`. The handler resolves the locked reservation, validates Payment order/transaction identity, amount, currency and accepted order/quote terms against persisted state, then dispatches `RecordReservationDepositCollectedCommand` for the existing guarded confirmation path.
- Added a dedicated `ReservationPaymentSucceededConsumer` registered only when reservation admission is enabled. It consumes the existing `PaymentSucceededV1` from queue `smartpark-operation-reservation-payment-succeeded-v1` and filters `ReferenceType=RESERVATION`; the existing Gate consumer now filters `PARKING_SESSION`. `ReservationDepositCollectedV1` and its consumer registration remain present but are not the active reservation-confirmation path.
- Confirmed the local `PaymentSucceededV1` URN with MassTransit 8.5.7: `MessageUrnAttribute` prepends `urn:message:`, so `[MessageUrn("SmartParking.PaymentService.Domain.Events:PaymentSucceededV1")]` matches Payment's default message identity. Payment publishes this event for reservation webhook settlement and for the demo/cash confirmation path.
- Payment compatibility fixes only: reservation preview/quote now fall back to `Guid.Empty` when tenant is absent instead of rejecting the request; `EnsureReservationDepositOrder` idempotency comparison now includes tenant/site/reference type/order type/expiry/final amount; quote binding validates tenant, site, currency and terms version. Existing Payment handler tests were checked for payload compatibility; they were not executed.

### Deliberate deviation and open contract review

- The 11 November decision says not to repurpose `PaymentSucceededV1` as collection evidence and proposes `ReservationDepositCollectedV1` with accepted quote/terms binding. The implementation uses `PaymentSucceededV1` because it is the producer actually available and because Operations already has the immutable accepted quote/order snapshot persisted. The event itself still lacks `QuoteId`, Operations terms ID/version and collection ID; those are derived/validated against the persisted reservation/order after delivery.
- This is an implementation integration choice, **not** an amendment to the approved event contract. The decision log now records the deviation and required owner review. A future dedicated event may close the schema gap without changing the guarded confirmation semantics.
- `PaymentSucceededV1` delivery has no Payment-side EF Inbox and the Operations endpoint has no general event-ID inbox; business idempotency still relies on Payment order/transaction identity plus locked reservation state. Reliable confirmation/decline outcome delivery, late/additional-collection reconciliation and production machine authentication remain open.
- Production Operations→Payment authentication is unresolved: the `X-Internal-Service-Key` shortcut is honored only by Payment's Development authentication handler. A production machine JWT/key contract is still required.

### Validation and repository state

- **PASS:** `dotnet build SmartParking.slnx --nologo -m:1 --no-restore` — 0 errors, 6 warnings: `NU1902` ×2, `NU1903` ×2 and `CS9107` ×2.
- **PASS:** `git diff --check` — no whitespace errors.
- Unit/integration/manual/Postman/PostgreSQL/RabbitMQ/payOS/Facility verification: **NOT RUN — USER VERIFICATION REQUIRED**.
- Branch `feature/SPARK-273-driver-book-parking-ahead-of-arrival`; entry HEAD `063d9a7e94ff3b23662c4029f81a73f9f9e260d3`. Existing unrelated worktree changes remain unstaged and preserved. No stage, commit, push, merge, deployment or database/broker action was performed.

### Documentation follow-up completed

- Updated the 11 November alignment handoff, producer handoff, API design, deployment checklist, Postman manual guide/collection and decision log to distinguish the real preview/quote/order adapter from the fail-closed zero-price, collection-recovery and reconciliation paths.
- Added current-status notes to the 10 November completion checklist and events/Facility handoff, and corrected the stale facility-dependency statement that the unavailable commercial adapter prevents live sales.
- Final validation after documentation edits: **PASS** `git diff --check` (only the existing LF-to-CRLF working-copy warnings); Postman collection and environment JSON parse successfully.
- Tests/live integration remain **NOT RUN — USER VERIFICATION REQUIRED**. Open blockers are production Operations→Payment authentication, quote-bound collection lookup, zero-price acceptance, reconciliation, and architecture-owner review of the generic `PaymentSucceededV1` deviation.

## 18 November 2026 — Slice 1 architecture and contract baseline

Slice 1 documentation is complete. SPARK-273 remains active for the separately authorized quote, admission and confirmation implementation slices.

Inspected feature/SPARK-273-driver-book-parking-ahead-of-arrival at 3b60787d06f3452fff276f622219de1659ee49d0 with pre-existing staged, modified and untracked work. Read Jira SPARK-273 and reconciled earlier overlap and zero-price language with the newly approved amendment. The older decision records remain historical. Source inspection found worker-based admission and the paid adapter/consumer wiring. Zero-price acceptance remains unavailable and the covered branch, reservation admission saga and progress hub are target design.

Documentation checks passed for added relative links, JSON examples, FR/BR headings, Mermaid branch balance and Git whitespace. Application builds, tests and runtime/manual checks are deferred. No stage, commit, branch, deployment or database action was performed. For later implementation, resolve owner wire compatibility, verify paid replay binding, prove Facility protection and audit acknowledged deferred commercial failures before retiring recovery paths.

### Exact Slice 1 documentation files

- Created [docs/05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md](<../../../docs/05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md>)
- Created [docs/05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md](<../../../docs/05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md>)
- Modified [docs/01-requirements/business-rules/04-reservations-allocation.md](<../../../docs/01-requirements/business-rules/04-reservations-allocation.md>)
- Modified [docs/01-requirements/business-rules/06-pricing-payments.md](<../../../docs/01-requirements/business-rules/06-pricing-payments.md>)
- Modified [docs/01-requirements/business-rules/07-monthly-passes.md](<../../../docs/01-requirements/business-rules/07-monthly-passes.md>)
- Modified [docs/01-requirements/business-rules/policy-catalogue.md](<../../../docs/01-requirements/business-rules/policy-catalogue.md>)
- Modified [docs/01-requirements/requirements-traceability/04-reservations-allocation.md](<../../../docs/01-requirements/requirements-traceability/04-reservations-allocation.md>)
- Modified [docs/01-requirements/requirements-traceability/06-pricing-payments.md](<../../../docs/01-requirements/requirements-traceability/06-pricing-payments.md>)
- Modified [docs/01-requirements/srs/authorization-operation-classification.md](<../../../docs/01-requirements/srs/authorization-operation-classification.md>)
- Modified [docs/01-requirements/srs/modules/04-reservations-allocation.md](<../../../docs/01-requirements/srs/modules/04-reservations-allocation.md>)
- Modified [docs/01-requirements/srs/modules/06-pricing-payments.md](<../../../docs/01-requirements/srs/modules/06-pricing-payments.md>)
- Modified [docs/01-requirements/srs/modules/07-monthly-passes.md](<../../../docs/01-requirements/srs/modules/07-monthly-passes.md>)
- Modified [docs/01-requirements/srs/permissions.md](<../../../docs/01-requirements/srs/permissions.md>)
- Modified [docs/01-requirements/srs/smartpark-srs-v1.1.md](<../../../docs/01-requirements/srs/smartpark-srs-v1.1.md>)
- Modified [docs/01-requirements/srs/state-transitions.md](<../../../docs/01-requirements/srs/state-transitions.md>)
- Modified [docs/01-requirements/use-cases/uc-res-01.md](<../../../docs/01-requirements/use-cases/uc-res-01.md>)
- Modified [docs/02-architecture/architecture-decisions/authorization-freshness-and-current-access.md](<../../../docs/02-architecture/architecture-decisions/authorization-freshness-and-current-access.md>)
- Modified [docs/02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md](<../../../docs/02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md>)
- Modified [docs/02-architecture/service-boundaries/ownership-and-events.md](<../../../docs/02-architecture/service-boundaries/ownership-and-events.md>)
- Modified [docs/03-design/api/reservation-facility-dependency-api-design.md](<../../../docs/03-design/api/reservation-facility-dependency-api-design.md>)
- Modified [docs/03-design/api/reservation-first-slice-api-design.md](<../../../docs/03-design/api/reservation-first-slice-api-design.md>)
- Modified [docs/04-testing/postman/SPARK-273-manual-test-guide.md](<../../../docs/04-testing/postman/SPARK-273-manual-test-guide.md>)
- Modified [docs/04-testing/reports/SPARK-273-deployment-checklist.md](<../../../docs/04-testing/reports/SPARK-273-deployment-checklist.md>)
- Modified [docs/04-testing/requirements-traceability/reservation-first-slice-scenarios.md](<../../../docs/04-testing/requirements-traceability/reservation-first-slice-scenarios.md>)
- Modified [docs/05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md)
- Modified [docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md](<../../../docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md>)
- Modified [docs/05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md)
- Modified [docs/05-project-management/handoffs/2026-11-10-reservation-completion-checklist.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-completion-checklist.md)
- Modified [docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md)
- Modified [docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md)
- Modified [docs/05-project-management/handoffs/SPARK-273 — Reservation/PaymentServiceIntegrationHandoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/SPARK-273%20%E2%80%94%20Reservation/PaymentServiceIntegrationHandoff)

## 18 November 2026 — Corrective Slice 1 review

Rechecked the four-slice baseline on feature/SPARK-273-driver-book-parking-ahead-of-arrival at 594ee7f10ef642467b70e9eaa6d5dd5d772499ed. That commit already tracks the SPARK-244 occupancy-outbox handoff. The file is separate work and is excluded from this corrective diff and reservation scope. Branch-level separation requires a separately authorized Git or delivery action.

Paid collection remains a Payment/Operations contract gate. The generic event lacks explicit collection identity and quote binding; the current Payment lookup has placeholder quote fields. Covered acceptance requires matching owner evidence and a protected claim at the guarded checkpoint strictly before H. Expiry requires fresh review and submission. The quote cutover retains legacy replay, history and unsettled obligations until owner-approved data retirement.

Passed Git whitespace, changed relative links and anchors, JSON examples, FR/BR identity checks, exact restoration of the historical proposal body, Mermaid branch balance and protected-path diff verification. Builds, tests, Postman and deployment were not run.

### Exact corrective documentation files

- Created [docs/02-architecture/architecture-decisions/reservation-workflow-target.md](<../../../docs/02-architecture/architecture-decisions/reservation-workflow-target.md>). Document current workflow, sequence and states separately.
- Created [docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-confirmation-handoff.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-confirmation-handoff.md). Separate paid collection evidence from future covered HTTP acceptance.
- Updated [docs/01-requirements/business-rules/04-reservations-allocation.md](<../../../docs/01-requirements/business-rules/04-reservations-allocation.md>). Separate paid recovery from covered expiry.
- Updated [docs/01-requirements/business-rules/07-monthly-passes.md](<../../../docs/01-requirements/business-rules/07-monthly-passes.md>). Bind coverage to canonical plate and preserve Account context.
- Updated [docs/01-requirements/srs/modules/04-reservations-allocation.md](<../../../docs/01-requirements/srs/modules/04-reservations-allocation.md>). Separate paid recovery from covered expiry.
- Updated [docs/01-requirements/srs/modules/07-monthly-passes.md](<../../../docs/01-requirements/srs/modules/07-monthly-passes.md>). Bind coverage to canonical plate and preserve Account context.
- Updated [docs/01-requirements/srs/smartpark-srs-v1.1.md](<../../../docs/01-requirements/srs/smartpark-srs-v1.1.md>). Align summary with coverage ownership and confirmation deadlines.
- Updated [docs/01-requirements/srs/state-transitions.md](<../../../docs/01-requirements/srs/state-transitions.md>). Record covered expiry and preserved plate coverage.
- Updated [docs/02-architecture/architecture-decisions/authorization-freshness-and-current-access.md](<../../../docs/02-architecture/architecture-decisions/authorization-freshness-and-current-access.md>). Link to the current reservation target.
- Updated [docs/02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md](<../../../docs/02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md>). Restore the original proposal body with historical scope.
- Updated [docs/02-architecture/service-boundaries/ownership-and-events.md](<../../../docs/02-architecture/service-boundaries/ownership-and-events.md>). Clarify service authority and coverage after registration changes.
- Updated [docs/03-design/api/reservation-facility-dependency-api-design.md](<../../../docs/03-design/api/reservation-facility-dependency-api-design.md>). Link the Facility contracts to the current workflow.
- Updated [docs/03-design/api/reservation-first-slice-api-design.md](<../../../docs/03-design/api/reservation-first-slice-api-design.md>). Align normalized input, paid event gate and covered expiry.
- Updated [docs/04-testing/reports/SPARK-273-deployment-checklist.md](<../../../docs/04-testing/reports/SPARK-273-deployment-checklist.md>). Separate legacy verification and add event/expiry release checks.
- Updated [docs/04-testing/requirements-traceability/reservation-first-slice-scenarios.md](<../../../docs/04-testing/requirements-traceability/reservation-first-slice-scenarios.md>). Specify distinct paid event and covered expiry cases.
- Updated [docs/05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md](<../../../docs/05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md>). Record the paid gate, covered expiry and slice split.
- Updated [docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md](<../../../docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md>). Mark legacy execution guidance superseded and link the active contract.
- Updated [docs/05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md). Mark legacy execution guidance superseded and link the active contract.
- Updated [docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md). Mark legacy execution guidance superseded and link the active contract.
- Updated [docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md). Mark legacy execution guidance superseded and link the active contract.
- Updated [docs/05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md](<../../../docs/05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md>). List Slice 2 retirement touchpoints and migration gates.
- Updated [docs/05-project-management/handoffs/SPARK-273 — Reservation/PaymentServiceIntegrationHandoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/SPARK-273%20%E2%80%94%20Reservation/PaymentServiceIntegrationHandoff). Mark legacy execution guidance superseded and link the active contract.
- Updated this session record to capture verification and correction scope.

## 18 November 2026 — Slice 2 implementation

Baseline `ec9fdc38ca0a4c0e786f5cc646709e48a55d9d3b` on the expected feature branch. Existing user changes in ai/AGENTS.md, ai/context/user-coding-instructions.md, the SPARK-243 session and scripts/seed-site-car-tariff.ps1 are separate.

Facility resolves a slot using its existing guarded facts query. The plate review coexists with the vehicle-based terms route used by the current checkout. Payment has no plate-bound issuance or authenticated proof read. Admission, provisioning and paid recovery still consume persisted legacy data. Coordinated booking cutover and deployed data retirement are blocked.

### ReservationTerms retirement

| Symbol or storage | Disposition | Consumer and removal condition |
| --- | --- | --- |
| Local commercial record on the new plate review | REPLACED | `ReviewReservationQuoteHandler` returns Payment's checked ID/version and disclosure with Operations policy. It has no terms repository, booking write, UoW or Facility acquisition. |
| `DepositOrderRequest.OperationsTermsId`, `.OperationsTermsVersion`, `.AcceptedTerms` and duplicate vehicle/interval fields | REMOVED | Paid provisioning now passes the exact validated Payment quote ID/version, payable amount, method, currency, scope, H and operation/booking IDs. The current Payment wire still calls its quote version `termsVersion`. |
| `ZeroPriceAcceptanceRequest.OperationsTermsId` and `.OperationsTermsVersion` | REMOVED | These fields had no owner consumer. The legacy handler still needs accepted commercial evidence. Its unavailable owner operation and existing lifecycle remain in place. |
| `POST /api/reservation-terms`, `ReviewedTerms`, `ReviewReservationTermsCommand/Handler`, `BookingSelection.VehicleId/SiteId` | TEMPORARILY RETAINED | Both existing frontend reservation clients use this contract. Stop new issuance when the paired plate-based review/create flow supports exact proof and replay and deployed clients have switched. Let outstanding unused reviews expire. |
| `ReservationTerms`, `Issue/CanAccept/Accept`, `IReservationTermsRepository.Add/LockAsync`, `ReservationTermsRepository`, EF mapping and DI | BLOCKED | Legacy admission locks by ID, checks Account, fingerprint, expiry and single-request acceptance. Retire after external proof, guarded quote single-use, paired client cutover and drainage of old requests. |
| `CreateReservationRequest.TermsId/TermsVersion`, `ReservationRequest.IntentJson/Fingerprint` | TEMPORARILY RETAINED | Admission and worker deserialize the old intent. Preserve exact Account/key replay and conflict behavior for existing receipts. Version the new intent before enabling quote-based creates. |
| `BookingTermsSnapshot`, `ReservationTerms.SnapshotJson` | BLOCKED | Progress consumes Facility version, operational-policy revisions and commercial terms. Replace its commercial content with Payment proof, preserve independent Operations policy and compare current Facility/coverage under guards. |
| `ReservationRow.TermsId/TermsVersion`, `AcceptedPricingSnapshot`, `BookingContracts.AcceptedTerms`, UoW immutability check | BLOCKED | Provisioning, zero-price legacy recovery, paid collection binding, confirmation and reconciliation require original values. Move new writes to first-class quote references only with authoritative proof. Keep historic financial evidence until orders and late collections reconcile. |
| `ReservationDepositEvidence`, `VerifiedReservationCollection` legacy terms fields | TEMPORARILY RETAINED | The present confirmation checks bind booking, collection, quote and local terms. Retire only with compatible, independently verified Payment evidence and old message drainage. |
| `reservation_terms`, FK `reservations.terms_id`, `terms_version`, `accepted_pricing_snapshot`, booking completeness CHECK, demo history seed | BLOCKED | The original SQL requires these fields for request-backed rows. The seed inserts referenced reviews. Owner-approved inventory and migration must cover pending orders, confirmed history and unsettled collections before physical retirement. No deployed data was inspected or changed. |

The operational-policy snapshot, original Account and plate, capacity claim, H/T and legitimate booked/deposit amounts remain required. Own booking history projects durable booking amounts and plate without fetching current tariffs. The confirmed notification fact uses reservation/Account/tenant/site/time. Preserve these reads and in-flight serializers through the transition. See the [Payment owner's implementation handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-quote-handoff.md).

### Verification checkpoint

The sandboxed VSTest runner timed out connecting to its test host. An outside-sandbox run reached the suites. Intermediate builds overlapped extraction of the owner interfaces and API host, and the initial Facility endpoint fixture failed authentication/configuration. The revised fixture installs explicit ephemeral signing configuration before the actual Facility host registers its authentication scheme. Final verification follows below.

The signed end-to-end test exposed a cached IdentityModel signature provider holding a request-scoped RSA key after disposal. Both Operations outbound HTTP adapters now disable signature-provider caching for that short-lived key. This preserves the existing algorithms, service identity, audiences and permissions.

### Validation and next-slice readiness

| Check | Actual outcome |
| --- | --- |
| `dotnet build SmartParking.slnx --no-restore --nologo -m:1 -v:q` | Passed with 0 errors and 6 existing NuGet advisory warnings. An intermediate rebuild encountered a DLL locked by a still-running test host. The retry after its exit passed. |
| `dotnet test SmartParking.slnx --no-build --nologo -m:1` | Final run passed across seven projects. 1,088 passed, 0 failed and 103 conditional skips. The slot-specific PostgreSQL case was also executed separately below. |
| `dotnet test tests/Services/SmartParking.OperationService.Tests --no-build` | 68 passed. Includes plate input, changed intent/coverage, mismatched quote/money, stale expiry, unknown owner route, signed issuance and existing order/history checks. The review test registers no booking persistence or acquisition service. |
| `dotnet test tests/Services/SmartParking.FacilityService.Tests --no-build` | 206 passed, 30 conditional skips without database configuration. Includes the anonymous rejection and signed lookup using the real Operations HTTP client, Facility host, auth scheme and mediator with fixture facts. |
| `dotnet test tests/Services/SmartParking.FacilityService.Tests --no-build --filter FullyQualifiedName~SlotOnlyReviewResolvesLiveFacilityFactsWithoutCreatingDependency` | 1 passed on an isolated loopback PostgreSQL 18 cluster with the real schema and repository. Verified authoritative site/tenant, unknown slot, deleted ancestry, version change and zero dependency rows. Cluster stopped and temporary files removed. |
| `node --test FE/tests/driver-reservation-api.test.cjs FE/tests/reservation-history.test.cjs` | 10 passed, including legacy accepted terms/idempotency-key reuse and Account-scoped history. |
| `node --test tests/frontend/reservation-happy-path.test.cjs` | 2 passed, 3 failed. Its checkout fixture cannot resolve the component's existing import of `../../../lib/driverReservationApi`. The failing tests stop during module loading. Both the fixture and component match HEAD. |
| `git diff --check`, documentation checks | Passed. Parsed 11 JSON examples. Checked handoff link targets and the 48-file implementation inventory against Git. |

Live plate-quote issuance, immutable proof, quote-based booking, live order replay, deployed legacy reconciliation, Postman and browser acceptance are unverified. Slice 3 can reuse the plate normalizer, slot authority, coverage policy, expected commercial binding and quote validation. Payment proof and replay-safe consent must precede guarded quote-based booking. Preserve the current booking route until that transition is exercised.

### Exact changed files

All paths are repository-relative. The four pre-existing changes listed above are excluded.

- `ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md`
- `docs/03-design/api/reservation-facility-dependency-api-design.md`
- `docs/03-design/api/reservation-first-slice-api-design.md`
- `docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-quote-handoff.md`
- `docs/05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md`
- `src/Services/FacilityService/SmartParking.FacilityService.API/Controllers/ReservationDependenciesController.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/BookingFacts.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/IReservationDependencyRepository.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/ReadBookingFactsQuery.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/ReservationDependencyContracts.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/ResolveReservationSlotHandler.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/ResolveReservationSlotQuery.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Application/Reservations/ResolveReservationSlotValidator.cs`
- `src/Services/FacilityService/SmartParking.FacilityService.Persistence/ReservationDependencyRepository.cs`
- `src/Services/OperationService/SmartParking.OperationService.API/Controllers/ReservationBookingController.cs`
- `src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs`
- `src/Services/OperationService/SmartParking.OperationService.API/OperationHost.cs`
- `src/Services/OperationService/SmartParking.OperationService.API/Program.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/ApplicationDependencyInjection.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/AcceptZeroPriceTermsHandler.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/BookingWorkflowContracts.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/DepositOrderRequest.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/FacilityBookingFacts.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/IReservationCommercialOwner.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/IReservationFacilityOwner.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/IReservationSubscriptionEligibility.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/PaymentReservationQuote.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/PlateCoverage.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ProvisionReservationOrderHandler.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationOrderProvisioning.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationQuoteBinding.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationSubscriptionEligibility.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReviewReservationQuoteCommand.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReviewReservationQuoteHandler.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReviewReservationQuoteValidator.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReviewedOperationalPolicy.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReviewedReservationQuote.cs`
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ZeroPriceAcceptanceRequest.cs`
- `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationCarPlate.cs`
- `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationCommercialClient.cs`
- `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationFacilityClient.cs`
- `tests/Services/SmartParking.FacilityService.Tests/EmptyReservationDependencies.cs`
- `tests/Services/SmartParking.FacilityService.Tests/PostgresStructureWorkflowTests.cs`
- `tests/Services/SmartParking.FacilityService.Tests/ReservationDependencyTestDoubles.cs`
- `tests/Services/SmartParking.FacilityService.Tests/ReservationSlotEndpointTests.cs`
- `tests/Services/SmartParking.OperationService.Tests/PlateQuoteReviewTests.cs`
- `tests/Services/SmartParking.OperationService.Tests/ReservationCommercialClientTests.cs`
- `tests/Services/SmartParking.OperationService.Tests/StubHttpMessageHandler.cs`

## 19 November 2026 — Slice 3 gated implementation

The branch remains at `960aaef8742740f567008a8bf97a0133be6d950f`. See the [Slice 3 owner handoff and file inventory](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-19-SPARK-273-slice-3.md) for the Saga, admission isolation, same-site overlap, committed SignalR progress, recovery dispositions, test commands and remaining deployment gates.

Operations has 74 distinct passing cases across the final suite and its one targeted restart-test rerun. Facility passed 71, UserService passed 12 and frontend passed 20. Fresh PostgreSQL, RabbitMQ and an authenticated test WebSocket exercised the Operations path. Owner replies were controlled. Hard-crash boundaries, deployed ACLs and a real provider transaction remain unverified.

Production quote submission remains gated. Payment proof and consumption semantics, guarded quote-based hold creation, and the historical terms/data transition keep the branch open. The existing frontend review/create pair and post-hold recovery remain available. No staging, commit, push or deployment.

## 19 November 2026 — post-merge quote holds and guarded confirmation

Baseline `f06ff1a9662d301e313f4846cc257814d3f87b22` on the SPARK-273 feature branch. The receipt and protected local hold now commit with the Operations EF outbox. Normal confirmation requires a checkpoint strictly before H. Verified paid money can compete again before T. Late zero-value acceptance goes to reconciliation. Production quote ingress and commercial evidence adapters remain gated. The [Slices 2 and 4 handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-19-SPARK-273-slices-2-and-4.md) owns the current file inventory, checks, compatibility requirements and open owner decisions.

## 19 November 2026 · Payment authority and paired settlement

The Operations and Facility source is committed at `41dd3e752`, directly above `f06ff1a96`. Payment implements signed reviewed issuance and scoped proof, atomic paid/zero provisioning, a linked zero invoice, verified transfers on the existing PaymentSucceededV1 outbox, financial lookup and idempotent disposition. Operations applies quote expiry at hold commit and uses H for new commercial effects. Subscription eligibility and new production admission remain gated.

The [commercial handoff](../../../docs/05-project-management/handoffs/2026-11-19-SPARK-273-payment-authority.md) records wire contracts, the impact matrix, file inventory, exact test commands, fixture permissions, observed failures and manual merchant/browser checks. Fresh disposable stores only. The existing-data transition, live provider test and production activation remain open. The four pre-existing repository changes were preserved.

## 20 November 2026 — Workflow audit and Postman readiness

Read the approved reservation requirements, ownership decisions and current working tree at `41dd3e752`. The normal Operations host still resolves `UnavailableQuoteAdmissionStarter`, so a fresh plate/quote submission rolls back with 503 before a Saga start. Payment order and zero receipt are implemented in the prior commercial slice, but production coverage returns uncovered. The frontend still submits legacy terms. Phase B is blocked and the three existing Postman artifacts were preserved.

The [workflow audit](../../../docs/04-testing/reports/SPARK-273-reservation-workflow-readiness-audit.md) records all ten stage findings, risk and regression trace, exact verification, deferred Postman assertions and owner priorities. Current audit results were 39 UserService tests passed and 1 PostgreSQL fixture skipped, plus 7 mocked frontend tests passed. Earlier PostgreSQL/RabbitMQ results are retained separately. The next paid-path acceptance step is approval and verification of real transactional admission under normal host composition.

## 20 November 2026 — Reviewed admission activation

The normal Operations host now resolves the transactional `AdmissionMessages` starter when admission, Saga consumers and signed reviewed settlement are enabled. Compose and the host runner map the existing flags and Facility's distinct broker identity. Payment's signed quote, proof and order implementation from the previous slice was available without further Payment changes. All opt-ins default to false.

The [integration report](../../../docs/04-testing/reports/SPARK-273-reviewed-admission-integration.md) records real Driver login, signed quote and order calls, actual owner consumers on RabbitMQ, 202 and same-key replay, one PostgreSQL booking, and a genuine unpaid `PENDING_PAYMENT` order. A separate booking committed before quote expiry and recovered its real order after expiry and an Operations restart, before H. Both orders had zero transactions. The solution build passed. Selected Operations, Payment and Facility regression suites passed 85, 70 and 12 cases respectively. The three existing Postman files now stop at order readiness. The covered folder stays disabled until an authoritative plate entitlement is available. Existing-data transition and deployed verification remain open.

## 20 November 2026 — Reviewed payment settlement and evidence

The project lead approved settlement-only publication, acceptance of exact financial settlement after H, direct event column removal for fresh development data, and collection-keyed disposition with no automatic payout. Entry HEAD was `0262567c78f3b1d2de9f2ade1547cf5b4757b5f7` on the SPARK-273 feature branch. The provider-neutral order constraint, provisioning fence and zero-receipt work were already present in the working tree and were preserved.

Payment's reviewed callback now requires an exact match to the durable PayOS checkout. A callback arriving before link persistence returns retryable HTTP 503. The existing order lock, provider-reference unique index and provider-reference EF concurrency token guard acceptance and stale checkout writes. The successful collection, paid order and EF outbox commit together with the transaction ID as the event ID. Additional and cancelled-order money commits with one existing collection-keyed disposition. Bound amount mismatches remain identifiable as individual collections. Operations and NotificationAudit production consumers retain their event contract and existing rules.

Source, EF and the owning initializer contain no separate collection event ID. The [Payment handoff](../../../docs/05-project-management/handoffs/2026-11-19-SPARK-273-payment-authority.md#settlement-and-compatibility) describes the new development initializer, verification boundaries and current provider recovery gap. The [decision log](../../../docs/05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md#settlement-amendment-and-open-decisions--20-november-2026) records the cancelled-order and notification wording backlog. Legacy settlement behavior, permanently unbound provider transfers, and populated-database rollout remain separate decisions. No active database or messaging state was changed.

The fresh solution build passed. The first PostgreSQL fixture run passed Payment 273, Operations 83, and NotificationAudit 202 tests, with zero failures and six RabbitMQ-dependent skips. Payment exercised real competing writes, stale checkout concurrency, and SQL rollback of the collection and actual EF outbox or disposition. Prior regression reports are independent of this run.

With the disposable RabbitMQ fixture, Payment passed 276/276 and the Facility reservation gate filter passed 8/8. The full Operations run passed 85/86 and timed out waiting for the broker delivery in `SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation`. That test passed on an isolated rerun with the committed paid-state assertion and MassTransit warnings enabled. All 86 distinct Operations tests passed across the two runs. The isolated test exercised real broker redelivery, lookup retry, guarded confirmation, post-H financial settlement and additional disposition rollback/replay with a PayOS HTTP double. Merchant callback behavior and manual Postman remain unverified in this session. The disposable PostgreSQL and RabbitMQ services were stopped.

## Reservation handoff consolidation

Current handoffs are indexed in the [handoff README](../../../docs/05-project-management/handoffs/README.md). The three entry points cover the Driver frontend, reservation owners and Payment authority. Dated snapshots above preserve the context of earlier decisions and test runs.

## 24 November 2026 — Checkpoint A recovery and verification

The user approved A on branch `feature/SPARK-273-driver-book-parking-ahead-of-arrival`, based on `cc01213f1` and architecture decisions v0.2. At recovery there were seven unstaged edits and nine new A files. The four previously staged files belong to the user. Review covered both diffs, the changed source and tests, the AI router, explicit coding instructions, coding skill, session rules and A approvals.

- Operations now queries effective reservations for canonical-plate overlap at the same site. Both repository queries use the accepted claim end, strict half-open overlap and the same active-status and payment-hold expiry rules. Physical occupancy has a separate read.
- The existing Domain evaluator matches simultaneous claims, visits and backup requirements to distinct compatible resources at each change in the requested interval. Active claims and allocations identify the occupied resource. The original target is the fallback when neither supplies a slot. An allocation that extends past its booking still consumes physical capacity. The site booking limit is enforced independently.
- Changed production files are `RequireReservationAvailabilityHandler.cs`, `ReservationAvailability.cs`, `ReservationAvailabilityRepository.cs` and `ReservationRepository.Bookings.cs`. Domain types in individual files are `AvailabilityDecision`, `CapacityAllocation`, `CapacityBackup`, `CapacityDemand`, `CapacityResource`, `CapacityVisit`, `OperationalCapacityConfig` and `ReservationCancellationEvidence`. Tests are in `ReservationAvailabilityTests.cs`, `QuoteBookingDatabaseTests.cs`, its new `CapacityConcurrency` partial and `ReservationPlateGuardTests.cs`.
- The quoted and reviewed-terms production writers both require a specific slot. Operations has reservation and allocation identities, visit facts and aggregate capacity configuration. It lacks a complete source for ordinarily bookable compatible slots by zone/floor. A relevant unallocated commitment with insufficient evidence produces `SCOPED_CAPACITY_EVIDENCE_REQUIRED`. The two/three-slot positive and negative results use controlled supply. The race fixture uses real PostgreSQL rows, transaction guards and EF outbox writes.
- Initial sandbox verification could restore only with a separate restore invocation, and a sandboxed VSTest run aborted during test-host startup. The interrupted PostgreSQL run has no recoverable result. A fresh SCRAM-authenticated PostgreSQL 18 run passed 89 Operations tests with 2 broker-dependent skips. The availability filter passed 17 tests. Final source verification follows below.
- For B, the admission token currently supplies fixed-slot resource facts. Before removing it, account for each fixed-slot physical/backup input. For production zone/floor decisions, the smallest proposal for separate approval is to bind compatible ordinary slot identities and scope membership to Operations' existing capacity configuration, with explicit completeness for the requested interval. Aggregate totals alone cannot certify an assignment.

### Final A evidence

| Affected path | Prior behavior and risk | After-change evidence |
|---|---|---|
| Quoted admission, reviewed-terms admission and shared plate check | High. A physical visit could veto a future booking and a PARKING reservation could escape the overlap rule. | Database checks cover cross-site acceptance, same-site contention, physical visits, PARKING, exact expiry and accepted buffers. The late-paid conflict fixture now uses a genuine competing reservation. |
| Availability and allocation | High. An ancestor maximum or a whole-interval choice of allocation could give the wrong decision. | Controlled inputs cover two and three ordinary slots, excluded backups, nested scopes, linked visits and timed reallocation. A real admission test verifies that a lingering allocation blocks its own slot through its own end, even when the same plate's reservation has already ended. |
| Concurrent local hold and outbox writes | High. Two independent accepted quotes can compete for the same remaining resource. | Two PostgreSQL connections observed actual blocking through `pg_blocking_pids`. After commit only one hold and one correlated outbox fact survived. After rollback the waiter could acquire the capacity and produce the sole surviving fact. Direct SQL writers outside the guard protocol are outside this guarantee. |

Final verification used a disposable loopback PostgreSQL 18 cluster with SCRAM authentication. Initialize a fresh test database with `scripts/database/microservices/03b-reservation-operations-service-db.sql`. Set `SMARTPARK_QUOTE_TEST_DB` to that database and `SMARTPARK_STRUCTURE_TEST_CONNECTION` to a disposable server login with database/role creation rights. The verification cluster was stopped and removed after the run.

```powershell
dotnet build SmartParking.slnx --no-restore --nologo -m:1
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj --no-build --no-restore --nologo -m:1
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --no-restore --nologo -m:1 --filter "FullyQualifiedName~Reservation|FullyQualifiedName~Gate|FullyQualifiedName~SlotOnlyReviewResolvesLiveFacilityFacts"
```

Build passed with 0 errors and 3 warnings in existing IoT/repository code. Operations passed 90, failed 0, skipped 2. Facility selection passed 87, failed 0, skipped 0. `git diff --check` passed. The skipped Saga restart and paired settlement transport tests need the isolated broker and multi-owner fixtures identified by `ReservationTransportFactAttribute` and `PairedSettlementFactAttribute`. Operations/Payment integration owners can rerun the same Operations command with those fixtures. Live provider, UI and unrelated suites were outside this A run.

A is ready for review. The existing confirmation handler still calls availability on a valid hold until the separately approved C cutover. The next implementation gate is explicit approval for B. Suggested commit subject for later use: `fix(operations): preserve scoped reservation capacity [SPARK-273]`.

## 24 November 2026 — v0.3 approved nine-file correction

The user approved corrections to A after the D04a/D04b audit and clarified that a walk-in scan records physical occupancy. Entry HEAD was `cc01213f1ba31cfcabaa393b539e3909864c74d3`. The four user-staged files and the other existing A edits were outside this correction. The earlier 177 passing cases covered the pre-correction source.

| Changed file | Responsibility |
|---|---|
| ReservationAvailability.cs | Compute a continuous, compatible assignment for the affected component. Resolve evidenced substitutions, independent occupancy, local booking quotas, and temporal exclusions. |
| CapacityAllocation.cs | Carry effective status, ownership, session and corroborating scan time in the existing calculation input. |
| CapacityResource.cs | Accept optional interval exclusions in controlled resource evidence. |
| RequireReservationAvailabilityHandler.cs | Map authoritative local rows and pair scan evidence by matching reservation, session and slot. Forward cancellation while evaluating under the transaction guard. |
| ReservationAvailabilityRepository.cs | Expand the guarded read through full intervals of overlapping flexible bookings. Always read open sessions independently of allocations. |
| ReservationRepository.Bookings.cs | Share explicit time bounds and the effective-status filter. Keep canonical-plate conflicts local to the candidate site and interval. |
| ReservationAvailabilityTests.cs | Cover staggered blockers, indirect constraints, scoped future pins, scans, backups, exclusions and occupancy projection. |
| QuoteBookingDatabaseTests.CapacityConcurrency.cs | Exercise persisted assignment history, physical-session independence after projection, competing orders, rollback and surviving holds/outbox facts. |
| TASK-SPARK-273-SESSION-20261108.md | Record the corrected scope, evidence and handoffs. |

For a named booking, a conflicting assignment requires corroboration. A reservation-linked occupied assignment and a matching scanned session can supersede the original named claim during the effective interval. A separately conflicting active claim, competing assignments or an uncorroborated different reserved slot remain indeterminate. An existing independently advanced active claim also identifies its resource. Preserve the original target and released allocation rows. The existing schema does not prove every past supersession reason or usability change.

The solver keeps one resource choice per unallocated reservation across its interval, except at an evidenced actual transition. It intersects feasible slots, tries the smallest candidate sets first, and uses simultaneous matching to prune impossible choices and cover transient unlocated visits and dynamic backups. Disconnected time/resource components cannot veto a request. Fixed-only cases use a direct conflict check. Worst-case search grows exponentially with the number of interacting flexible bookings; no branch limit converts an unfinished search to a business conflict. Cancellation propagates to the caller. Search creates no database assignment.

Only the configured site/category advance-booking ceiling limits booking counts. The 5 November first-slice decision D2 preserves commercial capacity quotas; the Operations database contract describes the maximum advance bookings and its physical upper bound. Count each active quota-bearing reservation once during the proposed effective interval. Enforce a valid site configuration. Descendant aggregate maxima are not additional vetoes. Fixed backups and configured backup counts apply independently, with overlap counted once. The policy catalogue specifies evaluation time plus effective occupancy validity, default four hours. An allocation tail and the accepted reservation buffer are separate from this horizon.

### Walk-in producer handoff for the gate and simulation owner

- `ParkingSessionsController` dispatches `ScanParkingSessionCommand`. `CheckInWalkInCommandHandler` creates the admitted session. `ScanParkingSessionCommandHandler` obtains a Facility slot lease, then under the Operations plate/site guards creates a session-only `SlotAllocationRow`, sets its end to `DateTimeOffset.MaxValue`, calls `Occupy` and records the scan on `ParkingSessionRow`. Verified or simulated paid departure closes the session and releases linked assignments through `GateDeparture`.
- The slot-allocation exclusion, structure snapshot/impact reads, session-history and active-walk-in slot fallbacks, vehicle-location read, and departure cleanup currently see these rows. The existing `BoundCheckoutPaymentRecordsSettlementAndSimulatesDepartureAtEventTime` test asserts a released allocation for a walk-in. That assertion describes the legacy producer. Rework it with the producer to keep its settlement, departure, outbox and release guarantees.
- The producer is `ScanParkingSessionCommandHandler.cs` (walk-in branch at line 60). Its entry point is `ParkingSessionsController.cs`. The session and allocation entities are `ParkingSessionRow.cs` and `SlotAllocationRow.cs`. Trace the read and release dependencies through `ParkingSessionRepository.cs`, `VehicleLocationRepository.cs`, `GetCurrentVehicleLocationQueryHandler.cs`, `StructureRepository.cs`, `SlotAllocationRepository.cs`, `GateWriteHandlers.cs` and `SimulatePaidGateDepartureCommandHandler.cs`. The physical slot lease and active-allocation exclusion currently participate in exclusivity.
- For random simulation, select an appropriate available physical slot for an admitted, unlocated vehicle. For explicit simulation, validate the chosen slot. Both paths should use the same observation transition, update session current slot/scan time and applicable slot occupancy state, and preserve compatibility and single physical occupancy under competing scans. Keep verified departure and its occupancy notification. Replace any dependence on the allocation-table exclusion for physical exclusivity before removing the walk-in insert. Retain the reservation-linked gate allocation and fulfillment checks.
- Support legacy occupied session-only rows and session-only observations during the handoff. Inspect actual environments before proposing any data cleanup. Test same-slot concurrent scans, repeat scans, unlocated accounting, old/new representations, verified/replayed departure, slot release, projections, history and genuine reservation check-in/fulfillment. The corrected reservation evaluator can be reviewed before the teammate changes the producer.

### Remaining integration dependencies

The current fixed-slot workflow still uses the existing Facility snapshot. Its data does not prove complete local hierarchy or interval maintenance for mixed-scope production admission. Preserve distinct missing evidence when material. B must identify authoritative fixed-slot compatibility, backup exclusions and alternative capacity for unlocated visits before retiring the token. Future D08/D09 work must persist versioned maintenance and structural facts under the same site guard so events received before hold creation still constrain it, and events received later trigger the same state-dependent reconciliation. Cross-service contracts, projection storage, recovery infrastructure, payment and worker changes require their own approvals.

### v0.3 verification scope and commands

The tests use an isolated SCRAM-authenticated PostgreSQL 18 cluster on loopback, the original Operations initializer `03b-reservation-operations-service-db.sql`, `SMARTPARK_QUOTE_TEST_DB` and `SMARTPARK_STRUCTURE_TEST_CONNECTION`. These settings enable the actual database fixtures. Owner responses and the complete scoped resource inventory in the capacity races are controlled. The admission tests dispatch the normal command and inspect committed rows. The isolated Saga and paired settlement broker fixtures are separate from this setup.

```powershell
dotnet build SmartParking.slnx --no-restore --nologo -m:1
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj --no-build --no-restore --nologo -m:1
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --no-restore --nologo -m:1 --filter "FullyQualifiedName~Reservation|FullyQualifiedName~Gate|FullyQualifiedName~SlotOnlyReviewResolvesLiveFacilityFacts"
```

| Verification area | Named cases |
|---|---|
| Continuous matching, compatibility and eligibility | `NamedBookingPreservesUnassignedZoneDemand`, `StaggeredBlockersNeedOneContinuousSlot`, `OverlapChainOutsideRequestCanDetermineItsOnlyCompatibleSlot`, `JointAssignmentCanBacktrackAndIgnoreDisconnectedInfeasibility`, `FloorSpareCanServeFloorButCannotServeAnotherZone`, `UnknownMotorcycleCannotConsumeAnIncompatibleCarOnlySlot` |
| Assignment evidence and occupancy | `AllocationSupersedesOriginalTargetOnlyWhileAssigned`, `OrdinaryFutureAssignmentConstrainsTheWholeScopedBooking`, `WalkInUsesOneProjectedUnitWithOrWithoutLegacyScanRow`, `AllocationOutlastingItsBookingStillOwnsItsSlotWithoutUsingBookingQuota`, `AlreadyAllocatedBackupConsumesOnlyThatBackup`, `LinkedVisitAndReservationConsumeOneResource`, `MissingScopedEvidenceOnlyBlocksAnOverlappingUnallocatedUnit` |
| Production-shaped Operations/DB flow | `AdmissionUsesScannedReplacementAndPreservesOriginalAndReleasedHistory`, `FutureQuoteLeavesLegacyWalkInPhysicallyOpenAfterProjection`, `AdmissionHonorsAllocationPastBookingEndUntilItsOwnEnd`, `SpecificSlotAdmissionRefusesUnprovenScopedCapacityButIgnoresExpiredScopedHolds`, `HistoricalVehicleTermsHoldProvisionsAndConfirmsFromTheLegacyPaymentWire` |
| Time, cancellation and canonical plate | `NamedSlotUsesEachAcceptedBufferedEnd`, `AdjacentIntervalsDoNotConflict`, `SameSiteOverlapsSerializeAndPhysicalVisitsDoNotCreateReservationConflicts`, `PlatePredicateUsesAcceptedBufferAndStopsCountingUnpaidHoldAtH`; the production scoped-expiry case also seeds a cancelled same-slot/same-plate row. |
| Commit, rollback and outbox | `CompetingNamedSlotHoldsHaveOneWinner`, `DifferentSlotsCompeteForLastCompatibleUnit`, `RolledBackHoldReturnsLastCompatibleUnitToWaitingWriter`, `StaggeredEarlyWriterCommits`, `StaggeredLateWriterCommitsFirst`, `StaggeredWriterRollsBack`, `BothStaggeredWritersCanCommitWithAContinuousSpare`, `RollbackAndConcurrentQuoteUseCommitOneHoldAndOneOutboxTrigger` |
| Gate and fulfillment regression | `WalkInAcquiresGuardBeforeReadAndCommitsOnce`, `DuplicateVehicleDoesNotWriteOrCommit`, `PreferredSlotWithoutAllocationCannotBecomeActualParking`, `ReservedSlotBecomesOccupiedOnlyAtScanAndIsReleasedAtDeparture`, `BoundCheckoutPaymentRecordsSettlementAndSimulatesDepartureAtEventTime` |

The staggered writers take different named slots in adjacent halves of a scoped reservation. With two ordinary slots, either first commit wins because committing both would force the existing scoped reservation to switch. Rolling back that attempt frees its capacity and rolls back its outbox fact. With a third ordinary slot the scoped reservation can stay there and both writers can commit. The fixture observes actual blocking via `pg_blocking_pids`, compares persisted hold IDs to all correlated outbox IDs, and verifies hypothetical assignments did not insert allocation rows.

**Final result: A VERIFIED for the approved correction scope.** On the final C# source the solution build succeeded with 0 errors and 3 existing warnings (IoT NU1510 and two repository CS9107 warnings). Operations passed 96, failed 0, skipped 2. The Facility selection passed 93, failed 0, skipped 0. Combined result: 189 passed, 0 failed, 2 skipped. The isolated PostgreSQL process stopped and the command completed with exit code 0. The skipped tests were `DurableCaptureOwnerRepliesTerminalFencingAndRestart` and `SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation`. Broker recovery, live provider and manual Postman evidence remain separate. No shared database was reset. The correction added or deleted no files and created no commit. B awaits separate approval.

Suggested commit subject: `fix(operations): enforce continuous reservation capacity and distinguish walk-in occupancy [SPARK-273]`.

## 24 November 2026 — v0.5 B1 development implementation

**Scope and baseline.** User explicitly approved B1 and withdrew the deployed legacy-data inventory prerequisite. Automatic allocation/reallocation is deferred and does not gate the development reservation checkpoints. Started from `7e1414601a341e6b72836b730b4e84f45008e7c6` on `feature/SPARK-273-driver-book-parking-ahead-of-arrival`. Applied root and AI router instructions, `ai/rule.md`, explicit user coding instructions, SmartPark Coding Skill v1.0 (November 4, 12, 16 and 20 decisions/amendments), Session Tracking Skill v1.0, v0.5 architecture and the reservation/Facility rules. Checkpoint A verification remains historical.

**Implemented.** `ReservationAvailability.Evaluate` checks one named slot across the half-open effective interval. Allocation and scan boundaries preserve authoritative reassignment, contradictory-evidence fencing and finite tails. The calculation uses observed occupancy and the accepted projection horizon, associates linked visits with their reservation and never reserves a speculative slot for an unlocated walk-in. The current session-only occupied allocation expires for projection purposes without releasing the live session. The repository retains the guarded local reads, including owners of allocations that outlast their booking, and reads the genuine site/category quota. Canonical plate checks, exact H and hold/outbox persistence continue through their existing writers. The Application handler takes only the selected-slot eligibility facts from the existing Facility snapshot, including fixed backup exclusion. Its three callers remain quoted admission, reviewed-terms admission and confirmation.

**Retired and replaced.** Removed anonymous mixed-scope matching, backtracking, nested-scope options, flexible interval expansion, hypothetical unlocated visits, dynamic backup demand and `SCOPED_CAPACITY_EVIDENCE_REQUIRED`. Deleted reservation-domain `CapacityBackup.cs` and `CapacityResource.cs`; the discovery-domain resource remains. Replaced zone/floor and staggered shared-supply fixtures with named whole-interval checks and competing/independent named holds. Replaced the former assertion that an unlocated visit consumes a spare resource with acceptance until a slot is observed. Replaced backup count veto assertions with acceptance for ordinary slots and rejection for an explicitly designated backup. Kept commercial quota, buffer, current producer accounting, actual recovery evidence and shared confirmation-token coverage.

**File inventory.** Modified Operations Domain `ReservationAvailability.cs`, `CapacityDemand.cs`, `CapacityVisit.cs`, `AvailabilityDecision.cs`; Application `RequireReservationAvailabilityCommand.cs` and `RequireReservationAvailabilityHandler.cs`; Persistence `ReservationAvailabilityRepository.cs`; tests `ReservationAvailabilityTests.cs`, `QuoteBookingDatabaseTests.CapacityConcurrency.cs`, `ReservationPlateGuardTests.cs`; and this session. Deleted the two reservation-domain files above. No files created. The index remained empty. Existing user edits to the AI router and SPARK-243 session plus the untracked explicit instructions and walk-in handoff were preserved.

### Verification

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj --no-build --nologo -m:1
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-build --nologo -m:1 --filter "FullyQualifiedName~Reservation|FullyQualifiedName~Gate|FullyQualifiedName~Maintenance|FullyQualifiedName~PostgresStructureWorkflow"
```

- Build succeeded with 0 errors and 67 warnings. The output includes NU1900 because the sandbox could not reach the NuGet vulnerability feed, plus existing source/package/analyzer warnings outside the edited B1 paths.
- Operations: 96 passed, 0 failed, 2 skipped. Facility selection: 114 passed, 0 failed, 0 skipped. Combined: 210 passed, 0 failed, 2 skipped. Broker-dependent `DurableCaptureOwnerRepliesTerminalFencingAndRestart` and `SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation` were skipped.
- Tests used a fresh isolated loopback PostgreSQL 18 fixture initialized from `scripts/database/microservices/03b-reservation-operations-service-db.sql`, with `SMARTPARK_QUOTE_TEST_DB` and `SMARTPARK_STRUCTURE_TEST_CONNECTION`. The sandbox blocked loopback initially. The isolated fixture and tests succeeded with escalated execution. Real independent database connections showed contention through `pg_blocking_pids`, fresh guarded reads, one winning same-slot or same-site plate hold, two independent or adjacent named holds, quota enforcement across slots, and rollback letting a waiting writer proceed. Surviving rows matched the correlated hold outbox facts exactly. The test owner replies are controlled. Manual Postman remains user-owned.

**B1 VERIFIED.** Automatic scope suggestion, automatic allocation/reallocation, Facility backup configuration retirement, walk-in producer/simulation, D08 reconciliation, B2–B4 and C–E remain deferred or separately approval-gated. Shared Facility eligibility/token handling remains in use by the current callers. There is no development database reset or migration required for B1.

Suggested commit subject: `refactor(reservations): simplify availability to named-slot claims [SPARK-273]`.
