# Reservation first slice — implementation choices for review

**Current status: the user superseded shared-project packaging on 9 November in favor of service-local implementations. Approved authentication settings, once-per-workflow authorization, and broker trust remain. R-ARCH-03 producer integration is delegated; R-ARCH-04 remains OPEN.** Prepared against local HEAD 7c47755d on 8–9 November 2026. [D1–D4c](../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md) remain unchanged. The user approved JWT bearer 10.0.12, AutoMapper 16.2.0, MassTransit RabbitMQ/EF 8.5.7 dependency additions, but explicitly requires confirmation to remain gated pending commit-evidence design.

## Established implementation direction

Use the five existing Operations projects. API binds/maps/dispatches; direct MediatR handlers own use cases; aggregate repositories and UoW own reads/commits. Domain owns pure interval/state rules. Move existing gate/structure workflows out of stores and delete the forwarding StructureSafetyService; retain pure Domain policy. Shared authorization and external adapters remain legitimate ports.

Original `03b-reservation-operations-service-db.sql` only: declare final columns/constraints/indexes idempotently, no overlays, new functions/triggers or noncircular ALTER. Existing Operations trigger removal must land with C# replacement and complete competing-writer coverage; do not remove protection first. Do not change the separately approved UserService authentication binding trigger/role seed.

## R-ARCH-01 — authentication behavior retained; shared packaging superseded

**Historical approved proposal (packaging now superseded):** an isolated shared consumer authentication component, initially integrated into Operations only. It uses Microsoft JWT bearer middleware and a bounded asynchronous JWKS cache against the configured UserService public-key URL. UserService's local current-session validation is not copied into consumer TOKEN reads. Future Facility/Payment adoption is separate work.

Proposed defaults requiring security approval: 2-second download timeout; cache freshness 300 seconds matching the issuer header; zero stale-key grace after freshness expires; at most one shared refresh in flight per issuer; 30-second refresh-failure/unknown-key backoff; 30-second JWT clock skew. Cache a fully validated immutable RSA/RS256 set, never replace valid keys with malformed/empty data. Unknown key fails closed. No network in constructors/host startup; health remains live during outage. Before usable trusted keys, protected requests return sanitized dependency 503; invalid signatures/claims return 401 with bearer challenge. Never follow token-supplied URLs. Production trust transport must be HTTPS or an explicitly protected internal channel.

Alternative: keep code in Operations Infrastructure initially and extract after a second consumer adopts it. Smaller project footprint, but risks diverging retry/cache/error policy across services. Recommendation is a reusable component because the coding guide identifies the same consumer startup boundary for all three services. A project boundary/name and these numeric/error defaults require approval before implementation.

## R-ARCH-02 — current-access message contract and broker trust

**Historical approved proposal (packaging now superseded):** a small versioned, transport-independent shared contracts project, with Account-current request/decision DTOs owned by UserService. Operations owns its workflow, receipts and attempt scheduling; UserService owns durable per-workflow current-access evaluation/replay (as amended below). Consumers dispatch IMediator and use repositories/UoW, never put the workflow in a transport consumer or generic feature store.

Use the existing one UserService bus and add Operations EF Bus Outbox on ReservationDbContext; EF consumer inbox/outbox for atomic deduplication/replies. Proposed durable endpoint names: `user-account-access-v1` (UserService), `operations-account-access-results-v1` (Operations). Request/reply payload fields are in the [API proposal](../../03-design/api/reservation-first-slice-api-design.md#cross-owner-interfaces-to-agree). Saga owns the three attempts and 90-second original deadline per ADR §9; no delayed-redelivery multiplier.

Require broker credentials/ACLs that restrict request and outcome publication to the responsible services and queue consumption to the owning service. A matching message type/correlation is not proof of a trusted sender. Do not reuse the binding-lookup shared-key exception as general approval for broker or financial trust. Provision durable queues/bindings before enabling relay; capture-only mode may retain outgoing messages but cannot be advertised as live authorization. Keep unfinished work/messages; no silent TTL drop. Outbox/inbox retention and error-queue operations need deployment ownership.

Alternative: owner-published versioned contracts as a separate package instead of a local shared project. Better independent release boundaries, but needs package publication/version governance that is absent here. Neither option permits Operations to reference UserService's whole Application assembly or invent a second custom outbox relay.

## R-ARCH-03 — authoritative facts and concurrent owner changes

**Recommended proposal:** keep the existing structure prepare/complete handshake as the starting point; make Operations acquire the site guard before state reads and require every Facility closure/retirement writer to participate. Add versioned quote/coverage acceptance contracts with their owners; do not translate the checkout-price calculator into a guaranteed reservation quote.

Within Operations: proposed lock order is Account restriction → stable vehicle → site → reservation, with deterministic ordering for multiple IDs. Readers reload tracked state after waiting. All creation/admission/recovery/expiry/cancel/structure writers must share the relevant protocol; structure-only work must never acquire Account/vehicle after site. Physical occupancy remains blocking regardless of projection horizon. Interval exclusion covers known vehicle identities across sites and active unpaid/confirmed/allocated claims; nullable legacy identities require explicit enablement review. The physical allocation exclusion remains.

Cross-owner facts cannot be made atomic merely by local version comparisons. Needed decisions: who issues/binds/consumes a quote, when it expires, how coverage cancellation serializes with dependent booking acceptance, and how binding removal/transfer prevents a stale accepted CAR. Missing producer/handshake means the affected sale/finalization stays gated, not a guessed default. No temporary fake/free quote in a live path.

## R-ARCH-04 — D4c commit evidence, explicitly unresolved

The approved business boundary is Operations confirmation **commit strictly before T**, not provider time, C# clock check, row update, or BEFORE trigger timestamp. A transaction can pause after all these checks and commit after T. Removing the existing confirmation trigger must preserve immutable first-confirmation evidence in C#; that alone does not prove D4c.

Two directions merit a separate focused design/proof:

1. A durable candidate confirmation plus a post-commit observation/fence protocol. Keep the candidate inadmissible and do not publish confirmation/order-entitlement effects until authoritative evidence proves its qualifying commit. A read of the committed candidate observed before T can provide conservative positive evidence under a defined clock model. Lost acknowledgement or recovery after T without retained evidence remains unknown, not proof of failure or permission to backdate. Must specify the logical confirmation point, effect gating, recovery, claim release and false-negative behavior before approval.
2. PostgreSQL commit metadata plus a durable evidence capture protocol. PostgreSQL documents `track_commit_timestamp` and `pg_xact_commit_timestamp`, with data available only for transactions after tracking is enabled and subject to cleanup. This adds deployment configuration/retention/failover obligations and still needs verification of whether the timestamp represents the exact approved boundary (including a pause during commit/WAL flush). It is **not** a demonstrated solution. See [PostgreSQL committed transaction information](https://www.postgresql.org/docs/17/functions-info.html#FUNCTIONS-COMMIT-TIMESTAMP).

Changing D4c to a guarded row-write cutoff would simplify implementation but changes product meaning and is **not approved**. Do not do it. No database configuration is changed by this proposal. Confirmation (including zero-price final confirmation) remains gated pending review; pure time arithmetic and preconditions must not be mistaken for proof.

## Rollout and bypass disclosure

On a fresh aligned schema, C# replacements must enforce site/slot acceptance guards and first-confirmation immutability via the coordinated repository/UoW paths. Direct SQL, bulk EF updates, direct DbContext saves, database cascades and any uncoordinated legacy writer can bypass application-only protection. Preserve database FK/check/uniqueness/exclusion constraints for enforceable invariants. Inventory and test all competing production writers before enabling the new path.

CREATE IF NOT EXISTS neither upgrades old definitions nor removes installed triggers. No existing-volume transition/reset is authorized. Supply the exact declaration diff and a schema inventory to the deployment owner; deployment testing uses an isolated aligned environment. Keep historical evidence separate from the post-refactor tests.

## Work that can proceed independently

Approved requirements/traceability, pure interval/deadline/eligibility rules and their unit tests, source audit, reviewable interfaces and deployment test instructions can proceed. Shared JWT parameters/component, new message trust/contracts and owner acceptance handshakes wait for approval. D4c remains an explicit separate gate even after those choices are approved.

## Subsequent user decisions — 9 November

Implement the approved shared authentication and Account-current foundations. Publish commercial producer contracts for their owners; do not implement fake quote/coverage providers. Keep confirmation gated under unchanged D4c. Persist this task’s configurable business policies with a separate idempotent default seed; do not build the teammate-owned full catalogue. Policy table extension was presented for approval separately.

## Scope clarification and subsequent corrections — 9 November

R-ARCH-02 approved UserService-owned durable decisions. The subsequent one-evaluation amendment below now makes `account_access_decisions` one row per workflow, not per attempt. UserService evaluates its own User/Account state once durably and replays the result through its outbox. Operations owns the reservation request and delivery deadlines. The workflow fingerprint binds actor/operation/original admission and overall deadline; delivery attempt fields are deliberately excluded. This table is not an audit store or booking aggregate.

No decision-record deletion policy is implemented. Broker inbox duplicate detection alone does not establish durable business replay for a separately redelivered logical attempt. A future retention design must coordinate the original deadline, replay horizon and pending outbox records before purging evidence. This task does not add general authorization management APIs.

The user approved the reservation-only extension of `operational_policies`: platform/site revisions, effective periods, explicit override bounds and accepted snapshots. The separately authorized default seed leaves late-arrival enablement unset; each site must explicitly choose. No full catalogue management API, invented site configuration or in-memory business-default fallback is added. The policy entity lives in Operations Domain; JSON/EF conversion remains in Persistence.

The user corrected audit ownership: audit history belongs to another service. Remove the newly introduced Operations audit repository and publish durable events; the consuming teammate owns ingestion and retention. The user approved the versioned event and fail-closed behavior: unconfigured audit publishing returns 503 for Gate writes; configured publishing captures events in the owning PostgreSQL outbox even during broker outages. The consumer and retention implementation remain delegated. The later service-independence amendment below removes the previously approved shared projects.

## One evaluation per reservation workflow — user amendment, 9 November

The user explicitly selected **“Exactly one evaluation per workflow; replay its result.”** This reservation-specific decision supersedes the earlier per-attempt implementation and the global ADR Profile B fresh-evaluation rule for this slice. Other services/workflows retain their existing policy.

- UserService uses a transaction-scoped WorkflowId lock and a unique `workflow_id` constraint. The table retains DecisionId, WorkflowId, trusted User/Account, role, operation, logical-request fingerprint, Allowed and original DecidedAt. AttemptId/AttemptNumber are no longer stored in this table.
- A delivery with no persisted decision evaluates and records it. Later deliveries replay that same result even if the Account has since changed. A workflow ID with a different actor, operation or original time budget is rejected.
- If an evaluation finishes after the current delivery window, retain the completed decision; a later timely attempt can retrieve it. An already expired incoming attempt does not start an evaluation. The original 90-second deadline still prevents late business progression.
- Operations retains delivery retries (up to three envelopes, 5-second reply windows and 10/30-second waits). It accepts only the current envelope before its deadline, but the original decision timestamp may precede that delivery attempt. An old envelope cannot advance the workflow.
- Reply MessageId is the delivery AttemptId, while payload DecisionId/DecidedAt stay fixed. Otherwise inbox deduplication by the original DecisionId could suppress a valid later replay.
- This is **one durable logical result**, not a promise that a database read executes physically once under every failure. A crash/rollback before the first result is committed can cause the read to run again because no durable result exists. Once committed, later attempts must not re-evaluate it. Concurrent commit/restart behavior requires the deployment checks.

The table now has a direct purpose under the user's once-per-workflow requirement: transport inbox deduplication is keyed by MessageId, while the workflow spans different delivery IDs. Persisting the original decision prevents those deliveries from becoming new evaluations. No purge policy or generalized access-management API is introduced. Retaining evidence and handling an existing deployment of the older per-attempt schema require owner review; this task only edits the original fresh-schema declaration.

## Service-local integration — user amendment, 9 November

The user explicitly prioritizes service independence and requests removal of `src/Shared`. The authentication implementation now lives in Operations Infrastructure `Authentication/`; the API registers it. The previously approved JwtBearer 10.0.12 package moves into Operations Infrastructure without changing its version or security policy.

Operations Application owns local `Contracts/AccountAccess/AccountAccessV1.cs` and `Contracts/Audit/OperationsGateAuditV1.cs`. UserService Application owns a separate authorization wire-contract definition under its own `Contracts/AccountAccess/`. There are no shared projects, linked source files or inter-service production project references. The common `SmartParking.Contracts.*` C# namespace is retained only as the existing MassTransit wire identity; it does not denote a shared assembly. Changing that namespace/type name would require a deliberate message-version transition.

Each service compiles its own types and communicates through the existing RabbitMQ queues/events. The cross-service test harness checks wire identity/JSON compatibility and uses an assembly alias to distinguish identically named local types. This test-only reference arrangement does not couple production builds. The audit consumer owner implements its own compatible wire type; no dependency on Operations Application is required.

The solution removes the shared project entries. Docker restore/publish copies only the selected service source; all its project references stay under that service directory. No package feed, schema generator, new shared replacement project or Facility adoption is introduced. Independently maintained DTO definitions now require compatibility checks when changing message contracts. Live broker compatibility and isolated publish still require deployment verification.
