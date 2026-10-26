# SmartPark US-U01 implementation prompt

Copy the instructions below into the coding agent with access to the local SmartPark checkout. This prompt is self-contained; the separate coding-skill draft is reserved for later human review and must not be installed or applied as a new repository rule during this task.

---

Act as a senior C#/.NET engineer. Implement and correct the backend normal-profile slice for US-U01 / SPARK-248, including its API design, account-based authorization, shared error handling/logging, and reliable audit-event publication. Work in reviewable phases and teach me how the resulting code flows. Do not merely produce a plan.

## 1. Read instructions and establish evidence

- Repository: https://github.com/Quangthm/Mock-Project-Smart-Parking. Integration baseline: current develop. Work in the user's selected checkout/branch; do not switch/reset it or overwrite existing changes.
- Read applicable root and nested AGENTS.md files and follow their relevant linked coding skills. Locate files with targeted searches. Read relevant sections/methods and registrations completely, not every large document in full.
- These explicit user decisions supersede conflicting repository coding preferences, old AI session recommendations, and mentor demonstration code. Keep other applicable rules. Report conflicts with file/section evidence; do not silently rewrite instructions.
- Inspect current develop v1.1 requirements/architecture/API/event/audit documentation. Verify the actual baseline version and links; do not assume old v1.0 paths still exist. Record local/remote SHA and whether the remote baseline could be verified.
- Read mentor-vs-smartpark-architecture-review.md and SESSION-20261023-mentor-architecture-review.md if available. Their findings and old test results are review inputs, not proof of current behavior. Recheck relevant findings in source.
- Inventory working-tree changes before editing. Preserve unrelated work and distinguish previous edits from yours. Do not commit, push, or apply changes to a shared database.
- Ask before making a materially unresolved product/API/security-contract decision. Continue independent work. Routine implementation details within the approved direction do not require repeated approval.

## 2. Approved architecture and coding direction

- .NET 10; ASP.NET Core; five-layer Clean Architecture: API, Application, Domain, Persistence, Infrastructure.
- API: HTTP binding, routes, authenticated context adapter, error/status translation, request logging, composition root. Controllers must not use repositories, DbContext, or integration implementations. Composition-root references to implementation assemblies are allowed.
- Application: input/response DTOs, commands/queries, MediatR handlers, FluentValidation pipeline, use-case orchestration, inward-owned ports.
- Domain: business state/invariants; no HttpContext, HTTP statuses, EF, RabbitMQ/MassTransit, or snapshot JSON serialization.
- Persistence: database reads/writes, EF mappings, transaction/locking mechanics and outbox storage integration.
- Infrastructure: external integrations, including MassTransit/RabbitMQ adapter implementing Application's IEventPublisher.
- Use IMediator, not ISender, for the selected project convention. Use an Application input DTO mapped to a command. Prefer explicit mapping for this tiny input; preserve useful existing AutoMapper elsewhere. Do not inject unused IMapper just to match a base class.
- JSON unknown properties are ignored. Do not introduce JsonUnmappedMemberHandling.Disallow for this slice or weaken unrelated existing contracts globally.
- Validation pipeline must execute ValidateAsync with cancellation; avoid concurrent validator execution when validators share scoped DbContext. Domain invariants still protect non-HTTP callers.
- Prefer narrow tracked mutations and SaveChangesAsync through the unit of work. Do not call DbSet.Update on an already tracked User for a name edit. Avoid client-to-entity AutoMapper updates.
- A single SaveChangesAsync does not by itself justify explicit transaction boilerplate. This task DOES require meaningful authorization/save coordination, described below.

## 3. Approved profile behavior and remaining discovery gates

- Applies to Driver, Operator, Owner, Admin. Backend only.
- Authorization belongs to the authenticated Account and exact session. A User may have multiple Accounts. A name change updates the shared User profile visible through those Accounts.
- Resolve the User through the authenticated Account; never trust body/route AccountId or UserId to choose the own-profile target. A different active Account must not authorize a request through a locked Account.
- FullName: required nonblank, trim surrounding whitespace, preserve internal spacing and Unicode names, reject control characters. Verify current DB capacity and propose matching length validation, including Unicode counting semantics. Do not silently assume 255 .NET UTF-16 code units or change the schema. Ask for approval of the concrete maximum/counting policy before finalizing it if no approved contract exists.
- Read the existing approved avatar representation where available. Preserve documented representation and stored values; no invented { reference } shape, URL-to-asset conversion, syntax-only ownership claims, or arbitrary avatarUrl writes.
- Defer BOTH avatar replacement and removal. Remove an already-added avatar mutation route/command from this slice where present; preserve unrelated media work. No fake upload or removal success.
- Email/phone verified changes and password/security changes remain separate slices, with their own verification, session-revocation and later 2FA tests. Do not implement them here.
- Role, account status, permissions and ownership relationships are never self-editable.
- Preserve last-write-wins for competing authorized name edits; no mandatory client ETags or name-version conflicts.
- Inspect existing read/update routes and v1.1 contracts. Reuse approved suitable routes. Present exact routes and response shapes for approval before introducing a new or conflicting public contract. Ask if no authoritative avatar read representation exists. Do not treat previous uncommitted code as approval.
- Unknown fields may be ignored, but must never change role/status/contact/avatar/target identity. A successful name response describes the persisted supported fields; do not claim unsupported fields were applied.

## 4. Atomic authorization and persistence

The user confirms Accounts and session authority are in the same database; verify source/schema/runtime configuration without exposing secrets. If false, pause the dependent design and explain the implications.

Required ordering: if account locking/session revocation wins the coordinated transaction order, refuse the profile write. If an authorized save commits first, subsequent revocation takes effect afterward. Do not promise retroactive cancellation of a committed write.

Before implementing, enumerate all relevant account lock, session revoke/logout, account permission/role and global User restriction writers. Define a compatible guard/lock protocol, authoritative freshness, and deterministic lock order. Include multi-Account-to-one-User cases. Patch necessary participating writers within the bounded guarantee; ask if completing it requires a major unrelated migration.

Do not assume that BEGIN + a SELECT + SaveChanges provides this guarantee. A conditional write is acceptable only with a demonstrated concurrency protocol; checking an EXISTS predicate alone may use a stale snapshot. Merely locking users does not coordinate account/session changes. EF tracking queries may return already-tracked stale entities, even after a locking SQL query. Use fresh authoritative reads/reloads under the guard and avoid indiscriminate ChangeTracker.Clear().

Keep transactions short. No external broker/media calls while holding database locks. Lock/permission decisions belong to Application policy; SQL/EF synchronization belongs to Persistence. Do not expose provider SQL in controllers or handlers. Use bounded handling of retryable database conflicts; never blindly retry after an unknown commit outcome. Do not add profile-version conflicts to solve security races.

## 5. Error handling, public contract and OCP

- Use ASP.NET Core IExceptionHandler with a known-error mapper and safe fallback. Retain the mentor's dictionary-based mapping idea; do not copy unsafe authentication stubs or raw exception-message exposure.
- One mapping yields both internal HTTP status and approved public error details. A dictionary and ErrorHandlingResult are complementary. Keep HTTP status out of the JSON body while setting the actual response status correctly.
- Public error codes are stable readable strings, e.g. VEHICLE_TYPE_INVALID, not invented numeric 600-style codes. Reuse approved codes/envelope fields where possible. Do not silently replace the existing envelope with ProblemDetails or invent a full error catalogue.
- Keep mapping/writing responsibilities separate. A small registry is sufficient. Feature registration providers may be used when justified; do not add one abstraction/class per exception merely to claim OCP.
- Define exact-type versus inherited exception matching and fallback deterministically. Reject duplicate mappings at registration. Do not maintain divergent status/message switches. Keep registry immutable and avoid singleton captures of scoped services.
- Unknown exceptions => safe 500; never expose stack traces, SQL, Source, or arbitrary exception.Message. Classify known conflicts separately; not every DbUpdateException is HTTP 400.
- Preserve and test: JWT challenge and forbid customization; WWW-Authenticate: Bearer; automatic MVC binding/model-state errors; safe generic 500; cancellation semantics; response-started propagation; bodyless HEAD/204; unchanged file/image/streaming success behavior.
- A Response.HasStarted check must not swallow a caught exception. Do not append another JSON document or turn partial output into false success.
- Cover non-exception producers: relevant 404/405/415 and configured 429, plus binding 400/auth 401/403. Do not assume exception handlers see these. Use the same approved error-writing policy at appropriate framework integration points.
- Preserve correct HTTP status for failure-as-value results; do not wrap every result in Ok(...).
- Account for .NET 10 handled-exception diagnostics suppression. Define one unexpected-error logging owner; avoid duplication with framework diagnostics.

## 6. Logging and reliable integration events

- Use Microsoft ILogger<T> and configured providers, not a custom reimplementation of ILogger and not RabbitMQ for ordinary diagnostic logs.
- Implement/configure request completion logging for method, route template, duration, status and correlation. Actor identifiers require the existing privacy policy. Never log passwords, tokens, OTPs, raw bodies, or profile names by default.
- Implement Application IEventPublisher through Infrastructure using the installed compatible MassTransit/RabbitMQ setup. Verify package versions and conventions; do not upgrade unrelated dependencies.
- Inspect current v1.1 documents for the profile/audit event owner, intended consumer, contract, identity semantics and other documented uses. Do not invent consumers or events. If missing/contradictory, propose a minimal versioned contract and ask before finalizing the cross-service contract. Ensure audit attribution distinguishes requesting Account from affected User; include approved event/correlation identity without leaking unnecessary PII. Clarify no-op-save event semantics from the contract or ask.
- Use MassTransit's supported EF transactional Bus Outbox, not a homemade relay or in-memory substitute. The scoped publisher must actually participate in the same DbContext/transaction as the profile write. An interface wrapper alone is insufficient.
- Persist the profile mutation and required outgoing event atomically. Failure rolls both back. API success means durable change/event recording, not completed Audit processing.
- No consumer implementation is required in this task. User Service must start and the API must operate without one, after required schema deployment. Do not make business success wait for the Audit consumer.
- Broker outage must leave committed messages pending for later delivery. Configure startup/readiness deliberately: distinguish process startup/API availability from degraded broker delivery health.
- Establish durable queue/binding provisioning independently of consumer startup. If topology is not ready, provide an explicit supported delivery-disabled mode that still durably captures messages. Do not publish into unrouted topology and claim events are retained for future consumers. Verify actual framework configuration, not a fictitious flag.
- Document retry, pending-message observability, cleanup, and at-least-once delivery. Require future consumers to be idempotent. Never claim global exactly-once processing. Outbox rows are delivery state, not permanent audit records.

## 7. Database changes: proposal and script only for user database

- Inspect existing tables, mappings and migration conventions first. Reuse compatible outbox support rather than adding competing tables.
- If tables/indexes are absent, propose required schema and generate a reviewable PostgreSQL SQL script aligned with installed MassTransit/EF mappings. Explain each table/index, storage/cleanup implications, transaction participation and deployment order.
- Do not invent a reduced schema incompatible with framework queries. Distinguish producer outbox and consumer inbox requirements for the installed version.
- Keep migrations/model snapshots and script consistent with the established workflow. Do not apply migrations or scripts to the user's existing/shared database and do not enable startup auto-migration. Mark destructive rollback explicitly; do not delete pending messages as routine rollback.
- Disposable, explicitly isolated test databases may be provisioned for tests; verify isolation before applying schema there. Never use an unspecified/default shared connection.
- Explain that the real outbox-enabled endpoint requires schema deployment. Missing tables must not trigger fallback to direct publishing, dropped events, or fake success.

## 8. Work in resumable phases

Phase 1: Inspect baseline, instructions, existing implementation and v1.1 contracts. Report differences from this direction, exact affected paths and unresolved contract questions. Ask targeted questions only where required; continue independent work.

Phase 2: Correct error handling/logging and the approved normal-profile API/handler/authorization path. Reuse good work. Implement necessary atomic guard participants; do not advertise the guarantee while some paths bypass it.

Phase 3: Add reliable publisher/outbox integration, deployment topology guidance and proposed schema script. Do not create the Audit consumer or apply the user database script.

Phase 4: Run meaningful focused unit, real-database and HTTP integration tests, then required repository gates. Update feature API design and task handoff docs. Leave the separate coding-skill draft and unrelated AGENTS rules untouched in this task; record proposed future instruction changes instead.

After each phase, update the established task/session record with approved decisions, remaining blockers, exact files, executed checks and next steps. Continue automatically when no material decision blocks work. If interrupted, resume from this checkpoint instead of restarting.

## 9. Required verification

- Four roles: own-profile read/update; locked Account, revoked session, missing/invalid token; another active Account must not rescue the requesting locked Account.
- Shared User name visible through its other Accounts. Narrow mutation preserves avatar, contacts, credentials, roles, statuses and permissions.
- Approved name rules; DB-compatible Unicode boundaries once agreed. Unknown extra fields ignored without mass assignment.
- Real PostgreSQL two-connection races: preload stale tracked state, then commit revocation; competing save/revoke in both orders; Account lock and relevant permission invalidation; multiple Accounts sharing User; ordinary name last-write-wins. Use deterministic synchronization, not arbitrary sleeps.
- Real DI dispatch executes validator; malformed JSON differs from valid JSON with wrong type. Verify failure results/status/envelope and all applicable framework producers.
- Response-started failure propagates; HEAD has no body; 204/file streams remain valid; cancellation not mislabeled as generic 500; sensitive information absent from responses/logs.
- Profile commit creates required outbox entry; rejected or rolled-back write creates neither. Verify database failure atomicity.
- Consumer absent with queue present; broker unavailable; relay recovery without changing event identity; durable routing provisioned before delivery. If real broker tests cannot run, mark them unverified and supply reproducible commands.
- No-op event behavior follows the agreed contract. No fake evidence or unsupported exactly-once guarantees.
- Report actual commands/results, skipped tests, missing schema/runtime dependencies and remaining risks. Passing old tests that assert deferred avatar deletion is not acceptance.

## 10. Deliverables and final explanation

Deliver actual code changes, updated feature API design, schema proposal/SQL script, focused tests, setup instructions and a concise review report in existing repository locations. Explain the full request-to-response call path using actual filenames/symbols, including error paths and outbox delivery. Include what I should review first, why key choices were made, and known constraints. Provide a proposed Conventional Commit message following repository conventions; do not commit/push.

Clearly distinguish: implemented and tested; implemented but awaiting manual schema deployment; blocked pending a named decision; and deferred consumer/media/security work. Never claim this is fully complete if the atomic protocol, event contract, or real-database evidence is still missing.
