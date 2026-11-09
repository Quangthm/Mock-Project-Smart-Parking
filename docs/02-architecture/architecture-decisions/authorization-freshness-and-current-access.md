# Authorization freshness and Account-current access

**Status:** APPROVED on 2026-11-04, including the CQRS authorization contract (§6) and numeric Saga retry defaults (§9), explicitly confirmed by the user during this documentation update. Conceptual code shapes and delegated use-case/wire details retain their stated limits; runtime implementation is not claimed.  
**Decision date:** 2026-11-04  
**Repository baseline reviewed:** `develop`  
**Purpose:** Canonical architecture decision for authentication, per-operation freshness, CQRS boundaries and durable authorization workflows.

**Provenance:** Adopted from the user-supplied `SmartPark_Authentication_Authorization_Architecture_Decision_Review_FINAL_2026-11-04.md` and companion classification matrix on 2026-11-04. Approval of the architecture is not evidence of runtime implementation. Examples remain conceptual; exact claims, DTOs, routes and use-case state machines require their own contracts.

**Supersedes:** Blanket requirements for exact-session/current-account checks on every resource request in older FR-AUTH-06, BR-AUTH-01/07, SEC-02 resource-enforcement prose and the shared coding guide. UserService local authoritative security checks and owning-service resource/domain guards remain mandatory.

---

# 1. Executive decision summary

SmartPark will use **authorization freshness as an explicit per-operation design choice** instead of requiring every protected operation to synchronously query UserService.

The approved architecture separates:

1. **Authentication** — the receiving API validates the JWT locally.
2. **Actor context** — trusted claims are extracted from the validated JWT.
3. **Authorization freshness** — the approved operation matrix and use-case design determine the required behavior; no code-level freshness declaration is required.
4. **Current access** — for `REQUEST` freshness, a durable Saga requests authoritative Account-current authorization asynchronously, in parallel with independently permitted speculative work.
5. **Resource authorization** — the domain-owning service evaluates ownership/scope from its own authoritative data.
6. **Domain invariants** — the domain-owning service evaluates business truth.
7. **Persistence/concurrency** — the owning service protects local write correctness.
8. **Distributed workflow** — important cross-service operations may use MassTransit Saga/process-manager coordination with bounded retry, parallel branch-state tracking, explicit continuation, compensation, forward recovery, and failure states.

Initial implementation scope:

- `TOKEN` freshness: **approved implementation direction; runtime adoption not established**;
- `REQUEST` freshness: **approved implementation direction, asynchronous Account-current authorization via Saga; runtime adoption not established**;
- `COMMIT`: deferred;
- `WORKFLOW`: deferred as a general security guarantee.

`AccessHandle` and `AccessVersion` are **not part of the initial model**.

The access-token lifetime is **15 minutes**.

---

# 2. Approved authorization freshness model

| Freshness | Meaning | Initial implementation |
|---|---|---|
| `TOKEN` | Claims from a locally validated JWT are sufficiently fresh for this operation. | **Yes** |
| `REQUEST` | The API locally validates the JWT and durably admits a pending workflow. The Saga obtains **Account-current** authorization asynchronously **while independent, explicitly approved reversible/speculative work may proceed in parallel**. An accepted `Allowed` outcome is required before authorization-gated finalization or irreversible effects; denial invokes state-specific cancellation, compensation, or reconciliation. | **Yes** |
| `COMMIT` | Current authority must be revalidated immediately before a protected commit. | Deferred |
| `WORKFLOW` | A long-running workflow must preserve/revalidate authorization over time and define authorization-related continuation/compensation across the whole workflow. | Deferred as a general security model; a selected Saga may still explicitly react to access-change events at defined states |

The exact classification is assigned **per use case/command**, not globally from HTTP method, service, or endpoint visibility.

## 2.1 `REQUEST` does NOT mean a synchronous UserService call or authorization-first Saga sequencing

For SmartPark, `REQUEST` freshness follows **parallel, speculative Saga orchestration** when the use case explicitly approves independent work:

```text
Client request
    ↓
Business API validates JWT locally; extracts trusted actor
    ↓
Persist durable pending workflow intent + outbox
    ↓
Return accepted/pending response
    ↓
Saga starts / records independent branch progress
    ├── Request Account-current access ──► UserService
    │                                      └── ALLOW / DENY / timeout
    └── Dispatch approved preparatory or compensable business work
                                           └── succeeded / rejected / unknown
    ↓
Evaluate combined persisted branch state (outcomes may arrive in either order)
    ├── ALLOW + required business prerequisites → authorized finalization
    ├── DENY + no effect → reject
    ├── DENY + in-flight/committed reversible effect → cancel/reconcile/compensate
    └── missing/unknown outcomes → bounded retry and reconciliation
```

**Async transport is not the same as sequential business dependencies.** The Saga need not wait for authorization before launching independent, approved speculative work. It must still enforce an explicit **finalization/irreversible-effect authorization gate**; an unknown access outcome never counts as `Allowed`.

A speculative effect can change real capacity or other state even if it is reversible. Every use case must explicitly approve such effects and prove how they are released or reconciled. Do not assume all financial or physical effects are compensable.

This design removes the current-access round trip from the initial HTTP latency path, and may also overlap it with independent workflow latency; it increases cancellation, contention, state-tracking, and compensation complexity.

---

# 3. Approved current-access model: Account-current, not exact-session continuity

`REQUEST` freshness uses **Model 1 — Account-current authorization**.

The workflow actor snapshot contains only trusted identity/authority context needed by the workflow, conceptually:

```text
WorkflowActorContext
- UserId
- AccountId
- RoleCode / RoleId according to the finalized token contract
```

The exact names and identifier types must follow the real JWT contract.

The Saga must **not** persist:

- raw access JWT;
- refresh token;
- password/MFA secrets;
- an invented AccessHandle.

## 3.1 What UserService decides

The asynchronous authorization request asks UserService a question conceptually equivalent to:

> Is this User/Account currently eligible, and does its current authoritative role/access state satisfy this workflow's required current-access policy?

UserService must use its authoritative state. It must not simply echo or trust the persisted role claim as current truth.

The role value saved from the validated JWT is a **token-time actor snapshot**. UserService compares/evaluates against current authoritative role/access state.

## 3.2 Exact session validity is intentionally not part of v1 asynchronous `REQUEST`

Once a valid JWT has passed ingress authentication and the pending workflow has been durably admitted, the Saga's later current-access decision is based on current User/Account/role authority.

Therefore:

- expiry of the original 15-minute JWT after workflow admission does **not by itself** invalidate the pending Saga;
- revocation of only the exact initiating browser/token session does **not automatically** invalidate the Saga if the User/Account/current role remain allowed;
- an Account/User disable/lock or relevant current role/access change can deny the Saga's pending authorization.

This is an intentional performance/complexity trade-off.

A use case that later requires the **exact initiating session** to remain valid must opt into a stronger future design (`WORKFLOW`, exact-session reference, or an AccessHandle-like contract).

---

# 4. JWT validation architecture

## 4.1 Local validation is the baseline

Every protected business API may validate the UserService-issued JWT locally using approved issuer, audience, signing-key, algorithm, and lifetime validation.

A valid token may establish:

```text
CurrentActor
- UserId
- AccountId
- RoleCode / RoleId
- TokenId/jti when present, even though v1 REQUEST does not require exact-session continuation
```

Business services must never trust:

- request-body UserId/AccountId as actor authority;
- spoofable identity headers;
- unverified/decode-only JWT contents.

## 4.2 Token claims are token-time assertions

For `TOKEN` operations, the project intentionally accepts claim freshness until the 15-minute access token expires.

For `REQUEST` operations, token claims authenticate/admit the initiating actor; the Saga evaluates UserService's current Account-current outcome asynchronously while approved independent preparatory/compensable branches may progress. Finalization and other explicitly authorization-gated effects still require an accepted `Allowed`.

## 4.3 Access-token lifetime

**Approved access-token lifetime: 15 minutes.**

Consequences:

- a `TOKEN` operation may observe up to approximately the remaining token lifetime of stale role/Account/User claims;
- shorter-lived authorization freshness is obtained only when the use case is classified `REQUEST` or stronger;
- refresh behavior must reject/stop renewal when the server-side session/Account/User is no longer eligible;
- SignalR may accelerate frontend logout, but missing SignalR notification does not shorten the `TOKEN` stale window by itself.

This 15-minute stale-claim window is an **acknowledged architectural trade-off and technical debt** accepted for performance.

---

# 5. Core decision flow

```text
Authenticated endpoint?
    ├── NO  → public/other explicit origin policy
    └── YES → validate JWT locally → trusted CurrentActor
                    │
          Use-case classification
              ├── TOKEN → local resource authorization + domain invariants → commit
              └── REQUEST → durable pending workflow intent + outbox
                               ↓
                            Saga runs
                               ├── Account-current check (async)
                               └── independent approved speculative work (async)
                               ↓
                        combine correlated branch outcomes
                               ├── ALLOW + prerequisites → authorization-gated finalization
                               ├── DENY → cancel/compensate by actual effect state
                               └── fault/unknown → bounded recovery/reconciliation
```

The owning use case must distinguish **eligible speculative effects** from **authorization-gated finalization or irreversible effects**. The workflow may commit reversible provisional business state before authorization if explicitly approved; it may not claim a confirmed final success or perform a forbidden irreversible action before `Allowed`.

---

# 6. Final CQRS Authorization Contract

**Status:** Approved architecture direction — initial implementation

## 6.1 Design decision

SmartPark uses authorization freshness classifications to determine which authorization workflow applies to each user-facing operation.

The initial supported classifications are:

| Freshness | Meaning | Enforcement |
|---|---|---|
| `TOKEN` | A locally validated JWT provides sufficiently fresh identity/role claims for this operation. | ASP.NET Core JWT authentication, followed by local authorization and domain rules |
| `REQUEST` | A current authoritative User/Account/role-access decision is required for defined authorization-gated effects; independently approved speculative/compensable steps may run before its outcome. | Durable pending workflow with parallel Account-current authorization and eligible business branches; state-dependent join, continuation, compensation, or reconciliation |

`COMMIT` and `WORKFLOW` remain deferred architectural categories and are not implemented as general authorization levels.

The approved operation classification matrix is the initial source of truth for choosing `TOKEN` versus `REQUEST`.

### No generic authorization marker interface initially

SmartPark will **not initially introduce**:

- `IAuthorizationRequirement`
- `IRequiresAuthenticatedActor`
- `IRequiresCurrentAccess`
- An executable `AuthorizationFreshness` enum
- A generic MediatR pipeline that dispatches authorization workflows based on marker metadata

Reason: the approved asynchronous `REQUEST` architecture requires use-case-specific workflow admission, Saga correlation, state transitions, and authorization gates. A marker alone does not enforce these guarantees.

The matrix and use-case documentation describe the required behavior; they do not require a freshness interface, marker, attribute, property, enum value, registry entry or equivalent declaration on a controller, command, query or handler. Implement the appropriate TOKEN path or REQUEST workflow directly and verify its behavior. References elsewhere to "declared freshness" mean this documented design choice only, not an additional implementation artifact.

Introduce metadata/marker interfaces later only through a separately approved decision justified by a concrete reusable enforcement mechanism or architecture-validation need.

## 6.2 JWT authentication at the API boundary

Each protected user-facing business API must validate incoming JWT access tokens locally through the approved ASP.NET Core authentication scheme.

JWT validation must verify:

- Signature and trusted signing key.
- Trusted issuer.
- Intended audience.
- Token lifetime, including the configured clock skew.
- Required authentication claims and applicable token-validation constraints.

SmartPark's approved access-token lifetime is **15 minutes**.

Use ASP.NET Core authentication middleware and endpoint authorization policies.

Conceptual configuration:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configure trusted issuer, audience,
        // signing keys, and validation parameters.
        // Use the approved 15-minute expiration policy.
    });

builder.Services.AddAuthorization();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

The JWT bearer authentication handler validates the token and constructs an authenticated `ClaimsPrincipal`.

The authenticated principal is exposed through `HttpContext.User`.

Controllers, MediatR handlers, and Domain entities must not manually decode or revalidate the raw JWT to establish actor identity.

The API must explicitly enforce authentication using `[Authorize]`, an equivalent endpoint policy, or an approved authenticated-by-default policy.

Public endpoints must be explicitly designated. Service/device/provider-initiated operations use separate trusted service-authentication policies.

## 6.3 `ICurrentActor` responsibility

`ICurrentActor` is an inward-owned Application abstraction for accessing authenticated actor information.

Conceptually:

```csharp
public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? AccountId { get; }
    string? RoleCode { get; }
    bool IsAuthenticated { get; }
}
```

The exact property types and claim names must match the existing SmartPark authentication contract.

The API layer provides the implementation using the authenticated `HttpContext.User`.

`ICurrentActor` must not:

- Validate JWT signatures independently.
- Fetch account/session records automatically.
- Perform current-access authorization.
- Call UserService.
- Trust client-supplied identity fields.

It is responsible for exposing the trusted token-time identity context and allowing Application code to fail closed when required actor information is absent or malformed.

The Application layer must not depend directly on `HttpContext`, `ClaimsPrincipal`, or ASP.NET Core framework types.

### Important distinction

`ICurrentActor` provides **authenticated token-time actor information**.

It does not guarantee that the User, Account, role, or session remains currently authorized after the JWT was issued.

## 6.4 `TOKEN` operation execution

For an operation classified as `TOKEN`:

```text
HTTP Request
    ↓
JWT validated by API authentication
    ↓
Endpoint authorization policy
    ↓
Controller
    ↓
MediatR command/query
    ↓
Handler obtains actor through ICurrentActor
    ↓
Local resource authorization
    ↓
Domain invariants
    ↓
Business operation
```

No UserService network call or asynchronous current-access Saga is required solely to refresh authorization.

The operation intentionally accepts token-time claim freshness for the remaining valid lifetime of the JWT.

## 6.5 `REQUEST` operation execution

For an operation classified as `REQUEST`:

```text
HTTP Request → JWT authentication → endpoint authorization
    ↓
Controller → MediatR handler → trusted ICurrentActor
    ↓
Persist pending workflow intent + outbox atomically
    ↓
HTTP accepted/pending response
    ↓
Saga dispatches independent work as permitted by use-case contract
    ├── Account-current authorization → ALLOW / DENY / timeout
    └── Explicitly approved speculative branch(es) → success / rejection / unknown
    ↓
Persist outcomes and evaluate combined current state
    ├── authorization ALLOW + prerequisites satisfied → finalization may proceed
    ├── DENY → reject or compensate/reconcile existing/in-flight effects
    └── missing/unknown → bounded recovery, no assumed success
```

The initial HTTP handler must not synchronously query UserService for `REQUEST` freshness. **The Saga need not wait for an authorization response before starting other independent, explicitly approved steps.** Starting work in parallel is conditional on its reversibility, isolation, bounded resource consumption, idempotency, reconciliation capability, and domain safety.

**Authorization gate:** `Allowed` is required before confirmed finalization and other explicitly gated irreversible effects. A reversible provisional hold may be an actual authoritative capacity effect before `Allowed` only when the owning use case expressly permits it and defines cancellation, expiry, compensation, late-result behavior, and unknown-outcome recovery. Do not infer approval merely because an operation is technically asynchronous.

An early denial must set a persistent cancellation intent; if business work is in flight, a late success must be reconciled/compensated before the workflow is represented as cleanly cancelled. Final success must not be returned while authorization or required prerequisites are unresolved.

## 6.6 Asynchronous authorization integration

The Saga uses a semantic Application-owned authorization integration contract or a framework-independent workflow port where useful.

Conceptual example:

```csharp
public interface ICurrentAccountAccessRequester
{
    Task RequestAsync(
        CurrentAccountAccessRequest request,
        CancellationToken cancellationToken);
}
```

The request object conceptually contains:

- `WorkflowId` / `CorrelationId`
- `AuthorizationAttemptId`
- `AttemptNumber`
- Trusted `UserId`
- Trusted `AccountId`
- Token-time `RoleCode` or role identifier
- Approved operation/access policy context, where applicable

The method only dispatches or durably records the authorization request. It does not return a final authorization grant.

The authoritative outcome is received later through a correlated Saga message.

Infrastructure owns MassTransit/RabbitMQ integration, transport configuration, scheduling, and adapters. Application and Domain must not depend on concrete RabbitMQ, HTTP, Redis, or gRPC types.

## 6.7 Authorization outcome and Saga enforcement

UserService evaluates authoritative current User/Account/role-access state rather than trusting the role snapshot from the initiating JWT.

Possible outcomes:

- `Allowed`
- `Denied`
- Transient dependency failure / timeout
- Invalid authorization request/contract

The Saga must:

1. Correlate the outcome to the correct workflow and current authorization attempt.
2. Reject late, duplicate, stale-attempt, or state-incompatible outcomes.
3. Enforce the use-case authorization gate for finalization/irreversible actions, while allowing only explicitly approved speculative steps before `Allowed`.
4. Track independent branch outcomes; evaluate continuation, cancellation, compensation or reconciliation against their **combined current state**, including in-flight and unknown effects.
5. Use bounded retries and explicit timeout/failure states.
6. Preserve authoritative historical, physical, and financial facts.
7. Escalate ambiguous outcomes to reconciliation/manual review.

The owning business service remains responsible for resource ownership, facility/lot scope, applicable domain permissions, business invariants, and local concurrency correctness.

UserService is not a universal resource-authorization engine.

## 6.8 UserService-owned workflows

Security and Account workflows entirely owned by UserService may use direct authoritative local authorization against UserService's own data.

They do not require an asynchronous distributed Saga merely to check information already owned by the same service.

They must still enforce the appropriate current security policies, concurrency guarantees, and audit requirements.

## 6.9 Non-user service operations

Internal broker consumers, trusted devices, payment-provider callbacks, and other machine-originated operations must use their respective service/device/provider trust contracts.

They must not require an end-user `ICurrentActor` when no authenticated user initiated the operation.

A user-facing operation that invokes another service does not transfer authority merely by copying unverified user identifiers into a message.

## 6.10 Mandatory implementation and testing rules

- Protected endpoints must enforce JWT authentication before controller/handler execution.
- Required authenticated claims must be validated and missing/malformed actor context must fail closed.
- `ICurrentActor` must not duplicate JWT validation or current-access verification.
- `TOKEN` operations must not invoke UserService solely to refresh authorization.
- `REQUEST` handlers must atomically persist pending workflow intent and outbox; only the Saga may dispatch approved speculative effects according to its use-case policy.
- Saga transitions must allow permitted independent branches to progress while authorization is pending, but enforce `Allowed` for finalization/irreversible effects.
- Denial while an effect is in flight must persist cancellation intent, then reconcile or compensate late successful effects before declaring cleanup complete.
- Role and Account identifiers provided in client request bodies must not replace authenticated actor identity.
- Authorization attempt correlation and late-response fencing are mandatory.
- Current-access denial must never be converted into an implicit success.
- Authorization dependency outages must use bounded retries/recovery.
- Operation classification must be verified against the approved classification matrix through implementation review and relevant architecture/integration tests.

The matrix is a design contract, not an automatically enforced runtime policy. Until a reusable enforcement mechanism exists, correctness must be demonstrated through explicit implementation and tests.

### Deferred improvements

SmartPark may introduce a runtime authorization metadata registry, CQRS marker interface, authorization pipeline behavior, or stronger `COMMIT`/`WORKFLOW` model when a demonstrated need justifies the additional complexity.

These mechanisms must not be introduced merely to duplicate existing ASP.NET Core authentication or annotate commands without enforcing actual behavior.

# 7. Asynchronous `REQUEST` authorization messaging semantics

Do not model an authorization request itself as a past-tense fact.

Conceptually:

```text
Command/request:
CheckCurrentAccess

Outcome facts:
CurrentAccessAllowed
CurrentAccessDenied
```

Names are illustrative until the API/event contract is finalized.

Every request/outcome must carry both the workflow/correlation identifier and the specific `AuthorizationAttemptId`. Only the current outstanding attempt may advance the Saga; a late ALLOW from an earlier attempt must not supersede a newer result.

The outcome must be idempotently consumable.

The authorization request must contain only the minimum trusted actor/policy context required for UserService to decide current Account access.

Raw bearer JWTs are prohibited from Saga state and broker messages.

---

# 8. Saga use rule

A Saga/process manager is appropriate when a workflow:

- crosses service boundaries;
- runs across multiple messages;
- must remember progress;
- changes behavior by state, including independently progressing branches and combined outcomes when permitted;
- must tolerate duplicates/out-of-order delivery;
- needs durable retry, compensation, timeout, or forward recovery.

Do not convert simple request → local DB → response operations into Sagas without a demonstrated workflow need.

---

# 9. Global Saga retry, redelivery, timeout, and unknown-outcome policy

**Approved decision (user confirmation, 2026-11-04):** set **numeric project defaults** for ordinary internal Saga steps and the Account-current authorization workflow. Use-case owners may specify a stricter or different policy **only with documented justification and tests**. These are SmartPark engineering defaults, not values prescribed by MassTransit or an industry standard.

## 9.1 Global default numeric budgets

| Concern | SmartPark initial default | Interpretation |
|---|---|---|
| Internal synchronous dependency attempt timeout (when used inside a consumer) | **2 seconds** | Applies to ordinary internal HTTP/gRPC calls; individual slower integrations must document an override. |
| Short consumer retry | **1 retry, after 250 ms** | Initial attempt + one additional execution for a classified transient fault. |
| Delayed message redelivery | **2 redeliveries: after 10 s and 30 s** | For a step that uses middleware-owned redelivery, not in addition to a Saga-owned retry scheduler for the same failure. |
| Planned message-consumer executions | **6 total** | `(1 initial + 1 immediate retry) × (1 initial delivery + 2 redeliveries) = 6`. |
| Normal internal step automatic recovery deadline | **90 seconds** | Measured from first attempt and **not reset** by redelivery/restart. Must check deadline before another side effect. |
| Async Account-current authorization response timeout | **5 seconds per request attempt** | The Saga records `PendingAuthorization` and schedules a timeout; no thread is held open waiting. |
| Async Account-current authorization logical checks | **3 maximum** | Attempt 1, then at most two *new* Account-current evaluations. |
| Async Account-current authorization waits after timeout | **10 s, then 30 s** | Waits before attempts 2 and 3; each gets its own 5 s reply timeout. |
| Async authorization overall deadline | **90 seconds from pending authorization admission** | If no usable answer, explicit `FailedDependency`/`AuthorizationUnavailable`, never ALLOW/DENY guessed from timeout. |
| Unknown effect: status/reconciliation reads | **3 checks at +0 s, +5 s, +20 s** | Each check has a 2 s timeout; no new non-idempotent effect is issued. |
| Unknown effect: automatic reconciliation deadline | **60 seconds after unknown is detected** | Unresolved → `ManualReviewRequired`/`AwaitingReconciliation`; **not** `Failed` or automatically compensated. |
| Effectful idempotency record retention | **7 days after terminal resolution, minimum** | Must be longer for a use case/provider with a longer replay/settlement horizon; permanent business uniqueness constraints remain preferred where appropriate. |

**Time semantics:** these are configurable policies and *attempt budgets*, not real-time guarantees of message processing during broker outages. Scheduler delays, backlogs, and at-least-once transport duplicates can generate late deliveries. State checks and idempotency must still prevent late/duplicate effects.

## 9.2 Exception classification

**Potentially retryable, after proving the step is safe to repeat:**

- connection reset/refused, transient network/DNS failure;
- 502/503/504 or other **explicitly classified** transient service failures;
- brief transient DB deadlock/serialization failure where the local transaction can be safely replayed;
- transient broker delivery/infrastructure fault;
- 429 only when `Retry-After` fits within the step's remaining deadline.

**Not automatically retryable:**

- invalid authentication, account-current DENIED, permission/resource-scope denial;
- deterministic validation/business-rule rejection;
- unsupported transition, malformed payload;
- a permanent 4xx or deterministic conflict;
- unspecified 500 with potentially committed side effects;
- a state-changing request that times out **unless** the same operation is protected by durable idempotency/status reconciliation.

A transport timeout does not prove that the recipient failed to commit. Classify it as **unknown** whenever a side effect could have happened.

## 9.3 Message middleware budgets versus Saga-level budgets

There are **two separate failure mechanisms**:

1. **Consumer throws** due to a transient failure. MassTransit message retry/redelivery can replay the message.
2. **Message was accepted, but the Saga never receives an outcome**. The consumer may have completed or the reply may have been lost; message retry middleware cannot infer the missing reply. The **Saga must schedule/handle the response timeout**.

**Never stack the same logical step's own timeout/reissue scheduler on top of middleware-delayed redelivery without calculating and enforcing the combined budget.** This causes retry multiplication/storms.

### Profile A — ordinary retryable consumer processing

```text
Delivery 1: execute → 250 ms → retry (2 executions)
Wait 10 s
Delivery 2: execute → 250 ms → retry (2 executions)
Wait 30 s
Delivery 3: execute → 250 ms → retry (2 executions)
------------------------------------------------
Maximum planned: 6 executions; 90-second deadline
```

MassTransit configuration concept (illustrative; the filter must match the concrete exception types and the installed version):

```csharp
endpoint.UseDelayedRedelivery(r =>
{
    r.Intervals(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
    // Filter to approved transient exceptions in the actual endpoint.
});
endpoint.UseMessageRetry(r =>
{
    r.Intervals(TimeSpan.FromMilliseconds(250));
    // Filter to approved transient exceptions in the actual endpoint.
});
```

**Do not copy this unfiltered to every endpoint.** Only transient exceptions should reach these policies. Preserve correct filter ordering and outbox configuration.

MassTransit delayed redelivery for RabbitMQ may require the delayed-exchange plug-in (or an alternative queue/scheduler strategy compatible with the installed version). Do not assume every v9 example works unchanged on the project's installed MassTransit version.

### Profile B — Saga-owned asynchronous Account-current authorization

```text
Check #1 → await outcome up to 5 s
  transient fault / timeout
Wait 10 s
Check #2 → await outcome up to 5 s
  transient fault / timeout
Wait 30 s
Check #3 → await outcome up to 5 s
  transient fault / timeout
No result → AuthorizationUnavailable / FailedDependency
```

- **3 logical current-access evaluations maximum**, with a **90-second overall deadline**.
- Give each *new evaluation* a fresh `AuthorizationAttemptId`, preserving the same WorkflowId and monotonically increasing AttemptNumber.
- Reject/ignore prior-attempt late outcomes, including a late `Allowed`; otherwise an older ALLOW could override a more recent DENY.
- A duplicate delivery of the *same* evaluation retains the same `AuthorizationAttemptId` and is idempotent.
- UserService's authorization consumer may use **one** 250 ms immediate retry for transient processing failures, but **must not also schedule its own delayed redelivery for the same authorization request** when the Saga owns the 10 s/30 s recheck schedule.
- At most **6 planned physical consumer executions** under these configured policies (3 logical evaluations × 2 consumer executions each), excluding unplanned transport duplicates.
- After deadline: fail closed; respond/reflect dependency failure, **not** access denial. The original JWT's expiry after ingress does not by itself deny Account-current authorization.

**Rationale for new attempt IDs:** an authorization check is a *read-only current evaluation*. On a later timeout/recheck, SmartPark wants a **fresh current decision**, not a cached ALLOW produced before a later Account lock. This is deliberately different from retrying a **business mutation**, which must reuse its original idempotency/operation ID.

## 9.4 Exact rule: UNKNOWN outcome is NOT FAILED outcome

Unknown occurs when the caller cannot establish whether an effect committed, for example:

```text
OperationService Saga           Capacity owner
       │                              │
       ├──── ReserveCapacity ────────►│
       │                              ├── COMMIT capacity claim
       │               X response lost│
       │                              │
       └── timeout / no outcome       │
```

From OperationService's perspective, timeout means:

```text
No proof of success ≠ proof of failure.
```

**Do not:**

- mark the step `Failed` and assume no capacity was reserved;
- send a new ReserveCapacity using a new OperationId;
- call ReleaseCapacity without confirming a claim exists **unless** the owner's protocol explicitly supports safe release-before-reserve/tombstone semantics;
- delete a local reservation intent merely because a timeout elapsed;
- equate broker delivery acknowledgement with business commit;
- infer success/failure from timestamps alone.

### Required operation identifiers

For every effectful downstream command, persist at least:

```text
WorkflowId            = owning Saga
OperationId           = stable business-effect idempotency key
CommandId             = stable command identity (may equal OperationId by contract)
OwningService         = authoritative recipient
OperationType         = ReserveCapacity / ReleaseCapacity / etc.
EffectState           = Pending / Succeeded / Rejected / Unknown / Compensating
FirstAttemptAtUtc
LastAttemptAtUtc
ReconcileDeadlineUtc
```

`OperationId` must be reused for all deliveries/retries of the **same intended effect**. A compensating action has its **own** idempotent `CompensationOperationId` and references the original OperationId.

### Required reconciliation algorithm

```text
1. Send effectful command with stable OperationId.
2. Positive authoritative outcome:
       Succeeded → continue; persist correlation/evidence.
3. Explicit authoritative rejection (guaranteed no effect):
       Rejected → apply business failure/compensation policy.
4. Timeout / disconnect / ambiguous fault:
       OutcomeUnknown; stop sending new effect IDs.
5. Query the authoritative owning service by original OperationId:
       t + 0s, t + 5s, t + 20s (up to 3 queries, 2s each).
6. Status response:
       Succeeded  → continue, or compensate if cancellation was requested.
       Rejected   → reject only if recipient guarantees no effect committed.
       Processing → wait/recheck within deadline.
       NotFound   → NOT automatically failed; old message may still arrive.
       Unknown    → continue reconciliation within deadline.
7. At 60s unresolved:
       AwaitingReconciliation / ManualReviewRequired.
       Do not falsely report Succeeded or Failed.
8. If a late success/rejection later arrives:
       correlate to original operation, reconcile safely by Saga state;
       never resurrect an invalid terminal workflow or lose an external effect.
```

**Special case — authoritative `NotFound`:** It may be treated as "definitely not executed" **only when the owning service explicitly guarantees** that the operation was never accepted, the original command can no longer be delivered/executed (including outbox/requeue/replay), and the status/index is complete and consistent for that OperationId. Otherwise `NotFound` is still inconclusive.

### Explicit status contract recommended for effect owners

```text
GetOperationStatus(OperationId)
    → Succeeded | RejectedNoEffect | Processing | NotFound | Unknown
```

The owning service must persist its operation outcome/idempotency record in the **same local transaction** as the authoritative side effect when correctness depends on it.

**If the owning service has no status/reconciliation API**, the implementing use case must choose one of:

- a demonstrably safe idempotent reissue using the **same** OperationId;
- an authoritative local DB/evidence inspection by the owner (not cross-service direct DB access);
- explicit `AwaitingReconciliation`/manual investigation.

Do not silently invent a status endpoint during implementation.

## 9.5 UNKNOWN differs between authorization check and business effect

| Case | Retry/handling rule |
|---|---|
| Async Account-current authorization response missing | No business effect occurred by the check itself. Saga may issue a **new** current-access evaluation (new AuthorizationAttemptId) up to 3 total; ignore older attempts. |
| Capacity claim response missing | Side effect **may have committed**. Query status using the original OperationId; if safe to retry, reuse the **same** OperationId. |
| Payment initiation/charge response missing | Potential financial effect. Do not initiate a new charge with a new key; reconcile using provider/Payment-owned transaction identity and its contract. |
| Release-capacity compensation response missing | The release **may have succeeded**. Reconcile or reissue the **same compensation** idempotently; never manufacture a second independent release. |
| Local DB commit result unknown | Reload from authoritative local DB by business/idempotency key before replaying the transaction; an exception after a commit attempt does not establish rollback. |

## 9.6 Concurrency, idempotency, and outbox

- Incoming deliveries may be duplicated or reordered; the **state machine and owning DB** determine if an effect may occur.
- Persist local business-state transition + outcome/idempotency record + outbox publication atomically as applicable.
- Implement inbox/dedupe where needed, but do not rely on a short transport duplicate-detection window as the sole business idempotency guarantee.
- Preserve idempotency records for **at least 7 days after terminal workflow resolution**, or longer when business/provider/audit requirements require; use database uniqueness/conditional updates for durable invariants.
- A later replay after the retention horizon requires authoritative reconciliation before execution.
- Late messages after a timeout must check the workflow/operation deadline and state before causing a new side effect. A terminal Saga must not be resurrected by an old ALLOW or old capacity command.
- Outbox delivery infrastructure can recover after a longer outage, but workflow business retries and effects are bounded by their deadlines. Do not assume the outbox automatically stops late commands: consumers must enforce state/idempotency/expiry policy.

## 9.7 Retry exhaustion and manual intervention

```text
Known transient error, budget remains  → retry/redeliver
Known deterministic rejection          → reject/compensate by state
Authorization unanswered after budget  → AuthorizationUnavailable / FailedDependency
Known compensable effect, undo needed  → bounded compensation workflow
Effect outcome unknown after 60s       → AwaitingReconciliation / ManualReviewRequired
Compensation outcome unknown           → reconcile; do not claim "reversed"
```

After bounded attempts are exhausted, surface an operational alert/error-queue record as appropriate. **Manual replay is never automatic** and must first establish the authoritative outcome/idempotency key and current workflow state.

## 9.8 Required use-case overrides and documentation

For each important Saga, the design/implementation MUST document:

1. operation type and whether it has external side effects;
2. who owns retries: middleware **or** Saga scheduler (avoid unbounded stacking);
3. timeout per attempt and overall deadline;
4. immediate and delayed retry count, exact intervals, and **calculated maximum executions**;
5. which failures are transient versus deterministic versus outcome-unknown;
6. idempotency key, owner and retention guarantee;
7. authoritative status/reconciliation protocol or reason it is not needed;
8. definition of `Succeeded`, `RejectedNoEffect`, `Processing`, `NotFound`, `Unknown`;
9. compensation and unknown-compensation handling;
10. late-message, terminal-state, and manual-review behavior;
11. metrics/tests for outage, lost replies, duplicates, restart, and retry exhaustion.

If these items are necessary for correctness but absent from the use-case document, an implementing AI agent must stop at that design boundary and surface the omission instead of inventing a policy.

## 9.9 Acknowledged limitations and technical debt

- These numbers are **starter operational defaults**, not measured latency SLOs. Tune with realistic load/chaos tests, especially before production-scale deployments.
- Broker/network partitions mean no finite configured retry budget can guarantee an exact count of physical deliveries. Stable idempotency and durable state checks are mandatory.
- Account-current authorization has an accepted race between UserService's ALLOW and subsequent effects. `COMMIT`/`WORKFLOW` are deferred; use-case-specific revocation handling remains necessary.
- `NotFound` without an authoritative idempotency ledger cannot prove that a command never committed.
- Without a reconciliation API, certain unknown-effect cases **cannot be safely auto-resolved**; this is explicit debt assigned to the affected service/use case.
- Durable outbox recovery and late delivery require expiry/state guards; application business budgets are not a replacement for durable transport recovery.
- The service-authentication/message-authentication trust contract must be enforced; merely carrying UserId/AccountId/RoleCode in RabbitMQ messages is not proof of authorization.

---

# 10. Global continuation and compensation principles

Every important Saga must document:

1. Saga state;
2. triggering event/command;
3. allowed transition;
4. side effects already committed;
5. retry/redelivery behavior;
6. cancellation behavior;
7. compensation required;
8. late/duplicate/out-of-order message behavior;
9. pivot point;
10. post-pivot forward-recovery behavior;
11. manual-review trigger.

Global direction:

```text
No external business effect committed
→ reject/cancel workflow

Speculative/compensable effect committed or still in flight
→ record cancellation intent; reconcile unknown/late results and compensate deterministically

Physical/financial/historical fact reached
→ preserve the fact; use forward recovery/correction

Outcome ambiguous or unsafe to automate
→ reconciliation/manual review
```

Do not delete historical/physical/financial facts to make a workflow "look rolled back."

---

# 11. Global race-policy rules versus use-case rules

## 11.1 Globally approved rules

The following are global:

- no single cleanup algorithm applies to all workflows;
- timestamps alone do not prove causality;
- a past authorization decision cannot be retroactively erased;
- pre-pivot deterministic side effects may be compensated;
- post-pivot physical/financial/historical facts use forward recovery;
- late/duplicate messages must not resurrect a terminal workflow;
- compensation commands must be idempotent;
- independently progressing branches require durable, separately tracked results and a guarded join before finalization;
- cancellation with in-flight work is not a clean terminal state until late/unknown effects are safely handled;
- ambiguous outcomes go to reconciliation/manual review rather than destructive guessing;
- each important Saga must explicitly document its accepted authorization/revocation race window.

## 11.2 Required per-use-case documentation

Each important workflow MUST define:

- which authorization moment matters;
- which race after that authorization is explicitly accepted;
- whether access-change/revocation events can cancel a pending workflow;
- which speculative steps may start before authorization, including their actual side effects and holding/expiry limits;
- the combined authorization + business branch outcomes required to proceed;
- how denial interacts with in-flight/unknown effects and late success;
- which states react to revocation;
- the pivot point;
- compensation before pivot;
- forward recovery after pivot;
- concurrency/atomicity mechanism for local writes;
- late/out-of-order behavior;
- manual-review criteria.

An AI agent or developer implementing a Saga must treat missing items above as **unfinished design**, not invent them silently.

---

# 12. Revocation races under the approved freshness model

## 12.1 `TOKEN`

A current access change may remain unseen until the 15-minute JWT expires.

That stale window is accepted for operations classified `TOKEN`.

## 12.2 `REQUEST`

A typical timeline may be:

```text
T1 JWT validated at ingress; pending intent persisted
T2 Saga dispatches current-access check and approved reversible preparation concurrently
T3 Speculative business effect may complete or remain in flight
T4 UserService outcome ALLOWED or DENIED arrives
T5 Saga joins relevant results and either finalizes, cancels/compensates, or reconciles
T6 Later access/revocation change may occur after ALLOW
```

A T4 `Allowed` does not prove the Account will remain eligible forever. A T4 `Denied` does not imply independent speculative work never happened. The owning use case must explicitly handle both realities, including the race between an in-flight command, its delayed result, and a denial/cancellation intent.

Subsequent revocation may trigger cancellation/compensation before a pivot, or forward recovery after a pivot, according to the use-case policy.

## 12.3 `COMMIT` and `WORKFLOW`

These stronger guarantees remain deferred. A use case requiring them must explicitly propose/approve the stronger security semantics.

---

# 13. AccessHandle and AccessVersion

## 13.1 AccessHandle — deferred

Not needed for initial Account-current `REQUEST`.

Reconsider when a workflow must later prove/revalidate the **exact initiating session** without persisting the bearer token.

## 13.2 AccessVersion — deferred

Not needed while the initial design does not maintain distributed authorization caches/projections or use security revision ordering.

Reconsider for:

- Redis/local authorization projections;
- ordered access-change events;
- stale-event rejection;
- distributed cache invalidation;
- stronger long-running authorization consistency.

### Acknowledged debt without AccessVersion

Access-change events can become difficult to order when roles/accounts are changed, restored, and changed again.

Saga correlation/state can prevent many invalid transitions, but it does not prove which security snapshot is newest.

If SmartPark later relies on local authorization projections or repeated enable/disable/role-change events, AccessVersion or another monotonic authority revision should be reconsidered.

---

# 14. Reservation as the reference asynchronous `REQUEST` workflow

Reservation is an example of the **parallel, speculative** orchestration principle; its detailed Saga states and compensation matrix are intentionally **deferred**.

```text
POST reservation → local JWT validation → trusted actor
    ↓
Persist pending reservation workflow intent + outbox → 202/pending
    ↓
Saga initiates independent branches (only where specifically approved)
    ├── Current Account authorization → ALLOW / DENY / unavailable
    └── Optional reversible preparation/temporary hold → success / reject / unknown
    ↓
Saga evaluates combined correlated outcomes
    ├── ALLOW + required business conditions → authorize final confirmation
    ├── DENY + no effect → reject
    ├── DENY + effect committed/in flight → cancellation intent + compensation/reconciliation
    └── missing/unknown → bounded retry or reconciliation; no false success
```

**Not yet decided for Reservation:** whether capacity may be provisionally held before `Allowed`; hold TTL/quota/isolation; authoritative owner and contract; exact join/pivot; outcomes after denial; late results; compensations and manual review. The owning use case must approve these before implementation. A physical gate opening, captured payment, or other irreversible action is **not** automatically eligible for speculative execution.

The companion Reservation example file is a **deferred draft**, not an approved state machine or implementation template.

---

# 15. Reservation/API response semantics

For an asynchronous `REQUEST` workflow, the initial request should not claim a final confirmed resource before authorization/business completion.

Default architecture direction:

```text
HTTP 202 Accepted
workflow/request identifier
status = PendingAuthorization
```

The exact response envelope, polling/status endpoint, SignalR update, and eventual confirmed resource representation belong to the Reservation API design.

A use case that requires immediate synchronous final confirmation must explicitly choose a different interaction model.

---

# 16. SignalR access-revocation contract — global baseline

SignalR is a **best-effort realtime UX channel**, not authorization authority.

## 16.1 Global contract

Recommended global hub:

```text
/hubs/access
```

Recommended event:

```text
AccessRevoked
```

Minimal conceptual payload:

```text
AccessRevoked
{
    reasonCode
    scope
    occurredAtUtc
}
```

Illustrative scopes:

```text
USER
ACCOUNT
SESSION
```

Illustrative reason codes:

```text
USER_LOCKED
USER_DISABLED
ACCOUNT_DISABLED
SESSION_REVOKED
ROLE_CHANGED_REAUTH_REQUIRED
```

Final serialization/casing/error catalog must follow SmartPark API conventions.

## 16.2 Targeting semantics

Global rule:

- User lock/disable → all active connections for that User;
- Account disable → active connections for that Account;
- exact session revoke → connection(s) for that session;
- role/access change requiring reauthentication → affected Account/session connections according to policy.

Frontend must not decide whether a revocation applies to another user/account; targeting is a backend responsibility.

## 16.3 Frontend forced-logout behavior

On applicable `AccessRevoked`:

1. clear local access token;
2. clear refresh token/session credentials;
3. clear authenticated in-memory/user state;
4. stop authenticated SignalR connection/reconnect loop;
5. clear sensitive cached UI state as appropriate;
6. redirect to Sign In;
7. display a safe reason message.

Do **not** call normal `/logout` after a server-forced revocation when the server session is already invalid.

## 16.4 SignalR failure fallback

SignalR delivery is not guaranteed.

If the browser misses the event:

- refresh must still fail once server-side refresh/session authority is revoked;
- future `REQUEST` workflows will be denied by current Account-current authorization when applicable;
- `TOKEN` operations may continue to accept the still-valid token until its 15-minute expiry.

That last point is an explicit consequence/technical debt of the selected performance model.

## 16.5 Security ordering

Backend security state is committed first.

SignalR notification happens afterward.

Never wait for browser acknowledgement before treating access as revoked.

## 16.6 Scale-out debt

A single-instance SignalR connection registry is not sufficient for horizontal scale.

If UserService/notification hosting is scaled to multiple instances, SmartPark must add an appropriate scale-out strategy/backplane/managed realtime service or equivalent.

This is deferred until scaling requires it.

---

# 17. Current-access outcome categories

At minimum, asynchronous current-access workflow semantics must distinguish:

```text
ALLOWED
DENIED
AUTHORITY_UNAVAILABLE / TRANSIENT_FAILURE
INVALID_REQUEST / CONTRACT_ERROR
```

`DENIED` is a business/security decision and is not retryable.

`AUTHORITY_UNAVAILABLE` is a technical condition and may enter bounded recovery.

Do not map:

```text
UserService unavailable → ALLOWED
```

or:

```text
UserService unavailable → DENIED
```

The Saga must preserve the distinction.

---

# 18. Service-to-service trust

The broker/internal integration boundary must prevent arbitrary callers from forging trusted authorization requests/outcomes.

The final implementation must define:

- RabbitMQ credentials/vhost/permissions or equivalent service trust;
- allowed publishers/consumers where practical;
- message contract ownership;
- correlation;
- idempotency;
- safe logging;
- no raw user bearer credential propagation.

Exact service-authentication/infrastructure mechanics remain an implementation/design item unless already established by the canonical architecture baseline.

**7 November 2026 follow-up:** the user approved retaining identity-provider-issued OAuth service tokens as a **future/refactoring direction**, with implementation deferred under [SERVICE-AUTH-01](../../05-project-management/decision-log/2026-11-07-service-authentication-deferral.md). Provider, detailed policy and migration remain unselected. This does not change the approved human TOKEN/REQUEST model or broker trust requirements. For SPARK-243, subsequent explicit approval selects the existing shared `Services:Key` as temporary binding-lookup enforcement. All holders share that capability; caller-specific isolation remains deferred. See the linked decision for the accepted limitation. No unauthenticated lookup is approved.

---

# 19. Operation classification rule

The approved initial operation-family classification is maintained in:

[approved operation classification matrix](../../01-requirements/srs/authorization-operation-classification.md)

Global rule:

- do not classify solely by GET/POST/PUT/DELETE;
- classify by security impact, stale-claim tolerance, physical/financial consequence, and workflow semantics;
- only `TOKEN` and `REQUEST` are initially available as normal application choices;
- stronger requirements must explicitly propose `COMMIT` or `WORKFLOW`.

---

# 20. Canonical integration and change control

Requirements are owned by the [SRS](../../01-requirements/srs/smartpark-srs-v1.1.md), [permission matrix](../../01-requirements/srs/permissions.md) and [authentication/recovery contract](../../01-requirements/srs/authentication-recovery.md). This ADR owns architecture semantics; the [classification matrix](../../01-requirements/srs/authorization-operation-classification.md) owns the initial operation-family choices. Neither introduces new product permissions or a role-management feature.

Implementation conventions live in the [shared coding guide](../../07-skills/SmartPark-Coding-Skill-v1.0.md). The [API template](../../03-design/api/api-design-template.md) requires operation-specific contracts; [acceptance scenarios](../../04-testing/requirements-traceability/authorization-freshness-scenarios.md) record unexecuted verification obligations. Agent routers link these owners without duplicating the full policy. Runtime migration gaps belong in the [Conflict Register](../../../ai/architecture-reference.md#8-conflict-register-docs-vs-code).

Changes to a classification require an explicit approved decision and coordinated requirements, API, workflow and test updates. Historical reports and frozen baselines remain evidence of their original dates, not current enforcement policy.

---

# 21. Coding-guide rules to promote

The shared coding guide promotes these approved obligations:

1. JWT validation at the API boundary.
2. No raw bearer JWT in Saga state, broker messages, domain records, or logs.
3. Behavior consistent with the approved operation matrix and use-case design; no code-level freshness declaration or metadata artifact is required.
4. `TOKEN` means no current-access round trip solely for freshness.
5. `REQUEST` means durable pending workflow + asynchronous Account-current authorization **alongside eligible, approved speculative work**; accepted `Allowed` is required at authorization-gated finalization.
6. Current-access integration behind inward-owned semantic abstractions.
7. Resource authorization remains with the resource-owning service.
8. UserService is not a universal domain authorization engine.
9. Every important Saga documents independent-branch eligibility, combined-outcome join, denial with in-flight effects, retry, redelivery, timeout, compensation, pivot, late-message, and manual-review behavior.
10. Automatic retries/redeliveries are finite.
11. Retried external effects are idempotent or reconciled.
12. Outbox/inbox patterns are used where delivery/duplication affects correctness.
13. No generic timestamp-based cleanup across services.
14. `AccessHandle`/`AccessVersion` are not default fields.
15. Missing per-use-case race/pivot/compensation decisions are design gaps that an AI agent must surface instead of inventing silently.

---

# 22. Technical debt and acknowledged trade-offs

These are accepted consequences of the current architecture, not hidden defects.

| Debt / trade-off | Current consequence | Potential future improvement |
|---|---|---|
| 15-minute `TOKEN` freshness | stale Account/role claims may be accepted until token expiry | shorter token lifetime, stronger classification, projection/cache with invalidation |
| SignalR is best effort | disconnected FE may not immediately clear token | scale-out/backplane; reconnect reconciliation; stronger current checks |
| Account-current REQUEST, not exact-session | exact session revoke after ingress does not automatically cancel pending Saga | exact-session reference or AccessHandle; `WORKFLOW` freshness |
| No AccessVersion | repeated access-change events/projections lack monotonic authority ordering | Account/access revision/version |
| Async REQUEST | client receives pending state instead of immediate final outcome | sync authorization for selected endpoints; better realtime/polling UX |
| Broker/UserService dependency | authorization can remain pending during outage | bounded redelivery, operational alerts, future local projection/Redis |
| Authorization-to-effect race | access may change after ALLOW before later Saga effect | revocation events; use-case compensation; future `COMMIT`/`WORKFLOW` |
| Speculative effect before current-access outcome | temporarily consumes real capacity/resources for a request that may be denied; possible contention and cleanup costs | use-case opt-in with quotas/TTL, measurement, reversible holds and compensation |
| Independent Saga branches and concurrent outcomes | authorization and business results can race, including denial during an in-flight effect and late success after cancellation intent | persisted per-branch state, correlation/idempotency, concurrency guards, reconciliation and guarded join |
| Retry configuration complexity | nested retry/redelivery can amplify traffic | centralized observability, budgets, circuit breaking, load testing |
| Service-to-service trust contract still detailed later | forged internal messages must be prevented by infrastructure | broker ACL/service identity/mTLS as architecture matures |
| Transport replacement is not perfectly transparent | HTTP/RabbitMQ/Redis may have different timing/orchestration shapes | stable semantic port + adapter/activity boundaries |
| No global cleanup algorithm | use cases require explicit race/compensation design | intentional; do not replace with destructive generic cleanup |
| Saga persistence/operations overhead | more state, correlation, monitoring, error handling | limit Saga use to workflows that justify it |
| Role snapshot persisted with workflow | snapshot can become stale | UserService outcome uses current authoritative role; future versioning if needed |
| Numeric retry defaults not benchmarked | 2s/5s/90s defaults may misfit some deployments | latency/chaos tests and explicit use-case overrides |
| Missing authoritative operation status | timeout cannot safely prove failure or trigger independent retry | operation ledger + status reconciliation contract |
| Late outbox/broker delivery after workflow deadline | a previously enqueued command may arrive after the Saga has stopped | recipient-side state/expiry checks and stable idempotency keys |

These items must remain visible in the architecture/technical-debt documentation so future teams understand why the system behaves this way and when revisiting the decision is justified.

---

# 23. Decisions delegated to each important use case

The global architecture intentionally does **not** decide these universally.

Every important workflow implementation/design must explicitly document:

```text
Authorization freshness classification
Authorization gate / forbidden irreversible effects
Explicitly permitted speculative effects and their limits
Combined branch-state join/continuation rule
Cancellation handling for in-flight/unknown effects
Actor context persisted
Current-access request/outcome contract
Saga states
Correlation key
Timeout
Immediate retry policy
Delayed/redelivery policy
Total effective retry budget
Idempotency
Outbox/inbox use
Revocation behavior by state
Accepted race window
Pivot
Compensation
Forward recovery
Late/duplicate message handling
Manual-review criteria
User-facing pending/failure behavior
Observability/alert requirements
```

If one of these matters for correctness and the use-case document omits it, implementation must stop at the design boundary and surface the missing decision.

---

# 24. Final approved architecture

```text
Client → Business API validates JWT → trusted CurrentActor
                          │
                        CQRS
                   ┌──────┴──────────┐
                   │                 │
                 TOKEN             REQUEST
                   │                 │
             local checks     persist pending intent + outbox
                   │                 │
                   │                Saga
                   │            ┌────┴─────────────────────────┐
                   │            │                              │
                   │     UserService async             Approved independent
                   │     Account-current check         speculative branch(es)
                   │            │                              │
                   │     ALLOW / DENY / fault         outcome / unknown / in flight
                   │            └──────────────┬───────────────┘
                   │                           │
                   │                 persist + join branch outcomes
                   │                           │
                   │          ┌────────────────┼────────────────┐
                   │          │                │                │
                   │       ALLOW +          DENY /           unresolved
                   │       prerequisites     cancellation      result
                   │          │                │                │
                   │    finalization       compensation/    bounded recovery/
                   │    gate passed        reconciliation   manual review
                   │          │
                   └──────────┴─► local resource/domain checks and guarded writes
                                  (at the applicable use-case stage)
```

**Global boundary:** independent speculative work needs explicit use-case authorization and safe reversal/reconciliation; final confirmation or irreversible protected actions remain gated on accepted `Allowed` and other required business prerequisites.

---

# 25. Final decision status

## APPROVED

- local JWT validation;
- 15-minute access token;
- per-use-case freshness classification;
- `TOKEN` initial support;
- asynchronous Account-current `REQUEST` initial support with explicitly approved parallel speculative branches and state-dependent continuation/compensation;
- Saga actor snapshot (`UserId`, `AccountId`, current token-time role identifier);
- no raw JWT persisted;
- no AccessHandle initially;
- no AccessVersion initially;
- CQRS contract in §6, including Application-owned actor/authorization ports and no generic authorization markers or executable freshness enum initially;
- numeric retry/reconciliation/retention defaults in §9, with documented and tested use-case overrides;
- bounded retry/redelivery globally required;
- state-specific continuation/compensation;
- use-case-specific race policy;
- SignalR as secondary realtime revocation UX;
- explicit technical-debt documentation.

## DEFERRED

- `COMMIT` as a general freshness level;
- `WORKFLOW` as a general freshness level;
- exact-session continuity;
- AccessHandle;
- AccessVersion;
- distributed authorization cache/projection;
- generic SignalR scale-out;
- per-use-case retry numbers/pivots/compensation specifics where not separately approved;
- detailed Reservation Saga parallel branch selection, state matrix, capacity-hold behavior and finalization contract.

---

# 26. Companion review files

The supplied decision package identified these companion files:

1. [approved operation classification matrix](../../01-requirements/srs/authorization-operation-classification.md)
2. `SmartPark_Reservation_Saga_State_Compensation_Example.md`

The first records the **approved initial operation-family classifications**, subject to later use-case-specific changes.

The second was not supplied for this integration and is not imported as a contract. It is a **deferred, non-authoritative example**; its detailed Reservation state/compensation model must be redesigned separately for speculative parallel branches before implementation.
