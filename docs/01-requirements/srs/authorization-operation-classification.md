# SmartPark Authorization Operation Classification Matrix — Approved Initial Baseline

**Status:** APPROVED INITIAL CLASSIFICATION (project-lead direction); not a permanent implementation freeze  
**Date:** 2026-11-04  
**Source:** Refines `SmartPark_Authorization_Operation_Classification_Matrix_Draft.md` using the project lead's approved operation selections.  
**Scope:** Authorization freshness for authenticated **end-user business API operations**. This is **not** a substitute for role/permission matrices, resource ownership checks, domain invariants, machine/provider authentication, or UserService's authoritative security rules.  
**Documentation status:** Canonical initial classification baseline, integrated on 2026-11-04. Architecture semantics are owned by the [authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md). Runtime adoption and acceptance remain separate work.

**Provenance:** User-supplied `SmartPark_Authorization_Operation_Classification_Matrix_Initial_Baseline_2026-11-04.md`. This table classifies invocation types; it does not grant permissions, approve hypothetical endpoints, introduce MVP role management, or replace the [action-by-role permission matrix](permissions.md).

---

## 1. Approved policy

### 1.1 Default

**`TOKEN` is the initial default for authenticated end-user business operations not explicitly listed as `REQUEST`.** A business API validates the JWT locally, obtains the trusted actor claims, and enforces any local resource/scope and domain rules. Token-time claims may remain stale for up to the remaining **15-minute** JWT lifetime.

### 1.2 Exactly six initially approved `REQUEST` operation families

1. **Create reservation**.
2. **Cancel own future reservation**.
3. **Modify/reschedule reservation**.
4. **Check-in / admit vehicle** (user-initiated variant with an authenticated actor).
5. **Checkout / settle active parking session** (user-initiated variant with an authenticated actor; obligation/safety exceptions still apply).
6. **Register/subscribe for monthly or yearly parking plans** (new subscription/registration workflow).

For these six families, use **asynchronous Account-current `REQUEST` with optional parallel speculative work**: the API validates JWT locally, persists a durable pending workflow and trusted actor context, and returns accepted/pending. The Saga requests UserService current access **while it may initiate independent, explicitly approved reversible/compensable business steps**. Its correlated authorization and business outcomes drive state-dependent continuation, compensation or reconciliation. `Allowed` is required for final confirmation and other explicitly gated irreversible effects, **not necessarily for every provisional effect**. Whether speculative steps exist is decided per use case; do not assume they are permitted automatically.

### 1.3 Explicit exceptions to the default

- **Public APIs**: `PUBLIC`; no end-user JWT requirement.
- **Internal services, gate devices, IoT, payment callbacks, message consumers**: `SERVICE` / provider-device-specific authentication; do **not** fabricate an end-user actor or user JWT.
- **UserService's own authentication, sessions, and security/role/permission writes**: UserService performs **its own authoritative local checks**, inside its own application/domain/persistence workflow. **Do not introduce a Saga or remote current-access call merely to implement an intra-UserService authorization check.** `TOKEN` in the table describes an authenticated request's ingress classification and does not waive these local authoritative checks.
- **Chatbot-initiated action**: inherits the security and freshness of the underlying business action; it cannot downgrade `REQUEST` to `TOKEN`.

**Not every financial, physical, or administrative operation automatically requires `REQUEST`.** These are the six initial choices. Later use-case reviews may strengthen or change a classification, with an explicit documented reason.

---

## 2. Freshness and origin legend

| Class | Meaning in this baseline |
|---|---|
| `PUBLIC` | No end-user authentication required for this operation. Abuse/rate-limit requirements may still apply. |
| `TOKEN` | A locally validated 15-minute JWT is sufficient **for freshness**, plus mandatory local resource/scope/domain enforcement. No cross-service current-access round trip just for freshness. |
| `REQUEST` | Locally authenticate JWT and durably admit pending Saga; UserService authorization and explicitly approved reversible preparation may proceed in parallel. Join outcomes and require `Allowed` before gated finalization/irreversible effects. |
| `SERVICE` | Trusted service/device/provider identity and message/API trust; **not** an end-user JWT classification. Specific service/provider contract required. |
| `AUTH_FLOW` | Login, refresh, OTP, MFA, recovery: UserService-owned authentication/session lifecycle, governed by its authoritative local rules rather than a business-service Saga freshness level. |
| `COMMIT` / `WORKFLOW` | Recognized but deferred stronger models; not selectable as default initial implementation. |

`REQUEST` verifies **current User/Account/role authority**, not ongoing validity of the exact original browser session. The Saga does not persist the raw bearer JWT. `AccessHandle` and `AccessVersion` remain deferred.

---

## 3. Approved initial operation matrix

The **Freshness / origin** column represents the **approved initial direction** for that kind of invocation. Rows that have more than one invocation origin are separated or explained explicitly.

| Area | Operation / example | End-user actor? | Freshness / origin | Saga? | Owning authority | Initial implementation / boundary note |
|---|---|---|---|---|---|---|
| Public | Browse public parking/site information | No | `PUBLIC` | No | Facility / Operations | Public read; no user authorization assumed. |
| Auth | Login / OTP / MFA / recovery authentication steps | Not yet authenticated in all steps | `AUTH_FLOW` | No auth Saga required merely for self-contained logic | User & Access | Local authentication and abuse/security checks. |
| Auth | Refresh access token | Refresh/session context | `AUTH_FLOW` | No | User & Access | Must check current refresh/session/User/Account eligibility; never refresh using stale JWT alone. |
| Profile | Get own profile | Yes | `TOKEN` | No | User & Access | Self identity/resource access remains enforced. |
| Profile | Update ordinary own profile fields | Yes | `TOKEN` | No | User & Access | Local authorization/mass-assignment protection remain required. |
| Security | Change password / MFA / recovery settings | Yes (when authenticated) | `TOKEN` + **UserService local authority** | No Saga merely for local validation | User & Access | Current session/credential/re-authentication/security checks remain locally authoritative as required. `TOKEN` does not imply JWT alone is enough. |
| Vehicle | List/get own vehicles | Yes | `TOKEN` | No | User & Access / confirmed owning service | Local Account/vehicle ownership filtering. |
| Vehicle | Add/update ordinary vehicle metadata | Yes | `TOKEN` | No by default | Confirmed owning service | Local validation and ownership; revisit if a cross-service approval is added. |
| Reservation | **Create reservation** | Yes | **`REQUEST`** | **Yes (planned)** | Parking Operations | Authorization and any approved reversible preparation/hold may progress concurrently; final confirmation gated on `Allowed`. Capacity-hold policy deferred. |
| Reservation | **Cancel own future reservation** | Yes | **`REQUEST`** | **Yes (planned)** | Parking Operations | Only approved reversible preparatory steps may overlap authorization; cancellation/release, in-flight result handling and final transition remain use-case-specific. |
| Reservation | Read own reservation/status/history | Yes | `TOKEN` | No | Parking Operations | Local Account ownership and query filtering. |
| Reservation | **Modify/reschedule reservation** | Yes | **`REQUEST`** | **Yes (planned)** | Parking Operations | May overlap current access with approved reversible allocation preparation; final reallocation/release, join and compensation per use case. |
| Parking session | **Check-in / admit vehicle — user-initiated** | Yes | **`REQUEST`** | **Yes (planned)** | Parking Operations | Approved non-physical preparation may run in parallel; **opening a barrier remains authorization-gated** unless a separately approved safety/emergency rule applies. |
| Parking session | Check-in / barrier signal — trusted device/operator service path | Not necessarily | `SERVICE` (or explicit authenticated operator path) | Use-case-specific | Parking Operations / IoT evidence / Facility gate | Must authenticate device/service and resolve trusted reservation/session; cannot assume a customer JWT. |
| Parking session | Read own active session | Yes | `TOKEN` | No | Parking Operations | Account/resource scope enforced locally. |
| Parking session | **Checkout / settle active session — user-initiated** | Yes | **`REQUEST`** | **Yes (planned)** | Parking Operations + Payment | May overlap safe calculations/verification; irreversible payment/gate effects are gated by their own safety/authority contracts. Account lock must not strand physical exit. |
| Parking session | Gate exit / physical settlement — trusted operational path | Not necessarily | `SERVICE` or separately authorized operator | Use-case-specific | Parking Operations + Payment | Physical safety/payment outcomes cannot depend on invented customer actor. |
| Plans | Browse publicly advertised monthly/yearly plans | No if truly public | `PUBLIC` | No | Operations (plan definition) / Payment (financial terms) | Public catalogue unless owner restricts it. |
| Plans | **Register/subscribe for monthly or yearly parking plan** | Yes | **`REQUEST`** | **Yes (planned)** | Operations (plan lifecycle) + Payment (financial obligations) | May run approved reversible preparation concurrently; enrollment activation/irreversible financial effect stays gated. Payment/compensation per use case. |
| Plans | View own passes/subscriptions | Yes | `TOKEN` | No | Operations | Local ownership; account/scope checks. |
| Plans | Renew/change/cancel existing monthly/yearly plan | Yes if user-facing | `TOKEN` **initial default** | TBD per use case | Operations + Payment | Not automatically covered by **new registration** `REQUEST` choice; future workflow review may reclassify. |
| Payment | Initiate customer payment — directly authenticated user API | Yes if such API exists | `TOKEN` **initial default** | Not solely for freshness | Payment | Payment may still need strong provider/business validation; do not infer current-access Saga just because funds move. |
| Payment | Payment processing initiated by Operations/other service | **No user actor required by default** | `SERVICE` | Depends on payment orchestration | Payment | Authenticate calling service and use trusted business reference, invoice/session, idempotency, gateway confirmation; do not fabricate CustomerActor. |
| Payment | Payment provider webhook/callback | No | `SERVICE` / provider verification | Depends | Payment | Validate provider signature/authenticity, replay/ordering/idempotency; not JWT user authorization. |
| Payment | Read own payment history (user-facing) | Yes | `TOKEN` | No | Payment | Ownership/scope locally enforced if such API exists. |
| Facility | Read restricted facility/site configuration | Yes | `TOKEN` | No | Facility | Stale role exposure up to token expiry accepted initially; local scope still enforced. |
| Facility | Modify facility/site configuration | Yes | `TOKEN` | No by default | Facility | Owner/operator scope + domain/safety validation; potential future stronger freshness acknowledged. |
| Admin | List/search users | Yes | `TOKEN` + **UserService local checks** | No | User & Access | Sensitive read; UserService may check current permissions locally. |
| Admin | Lock/disable User or Account | Yes | `TOKEN` + **UserService local authority** | No Saga for local security write | User & Access | Authoritative current admin/target checks must not be weakened. Revocation/SignalR notifications after local commit. |
| Admin | Change role/permissions/scopes | Yes | `TOKEN` + **UserService local authority** | No Saga for local security write | User & Access | Current admin authorization and atomic policy updates owned by UserService. |
| Audit | Read security/audit records | Yes | `TOKEN` | No by default | Notification & Audit / User & Access | Local scope and least-privilege mandatory; review if 15-min stale role becomes unacceptable. |
| Notification | Read own notifications | Yes | `TOKEN` | No | Notification & Audit | Local Account ownership. |
| Assistant | FAQ/informational chatbot query | Yes if personalized | `TOKEN` | Not solely for authorization | Assistant / IoT as owned | Public FAQ may be `PUBLIC` when no actor required. |
| Assistant | Read or reset own conversation/history | Yes | `TOKEN` | No | Assistant | Local Account and role ownership. Reset affects the targeted conversation only. |
| Assistant | Chatbot-triggered business write | Depends on underlying action | **Inherit underlying operation** | Depends | Owning business service | Six REQUEST families remain REQUEST when invoked through chatbot. Manual confirmation/business rules remain. |
| Realtime | Connect to authenticated access/notification hub | Yes | `TOKEN` on connection | No auth Saga | Hub owner | Server-side revocation notification is best-effort UX; SignalR disconnect is not enforcement. |
| Internal integration | Consume trusted domain/integration event | No end-user JWT required | `SERVICE` | Depends | Event consumer | Broker identity, trusted origin/contract, idempotency, resource facts. |

**Note on `Saga?`:** “Yes (planned)” denotes the architectural direction for the six `REQUEST` workflow families. It does not assert an already implemented Saga or settle event schemas, state machines, API responses, or multi-service ownership.

---

## 4. Security boundaries preserved regardless of freshness

1. **JWT acceptance is never just decoding.** Verify signature, issuer, audience, expiry, algorithms, and other approved token checks at ingress.
2. **Freshness is not permission.** `TOKEN` does not authorize access to arbitrary resources, privileged mutations, or other Accounts' data.
3. **UserService remains authoritative for its own security state.** Self-contained UserService workflows use local current stores/authorization as necessary; the absence of a Saga is not the absence of a check.
4. **Service/device/provider operations do not require a fabricated user actor.** They require their own trusted origin and domain authorization model.
5. **`REQUEST` is parallel-capable async Account-current.** UserService returns an authoritative outcome; the Saga correlates it alongside independently progressing approved speculative branches and decides by their combined state.
6. **Authorization outcome failures do not become ALLOW.** Pending/dependency failure states follow bounded retry and fail-closed policy.
7. **Authorization gate applies to final/irreversible effects.** Only explicitly approved reversible provisional effects may run before `Allowed`; physical gate opening, irreversible financial effects, and final confirmation remain gated unless an approved safety/obligation policy explicitly specifies otherwise.
8. **Use-case-specific changes are allowed.** Changes to initial classifications need an explicit decision record/approval and coordinated SRS, API, CQRS/Saga, security/test updates.

---

## 5. Implementation documentation checklist

For **each REQUEST family**, the owning implementation/specification MUST record:

- exact user/API vs device/service invocation paths;
- trusted actor snapshot and authorization request fields;
- authoritative UserService access policy and decision outcome;
- pending response (`202`/status or other approved contract) and client observation method;
- authorization gate (which final/irreversible effects are forbidden until `Allowed`);
- optional independent speculative work permitted before `Allowed`, including reversibility, TTL/quota/isolation, and ability to reconcile/compensate;
- combined branch-state join/continuation rules and denial with in-flight/unknown business effects;
- Saga correlation, states, transitions, timeouts and **bounded** retry/redelivery;
- idempotency and unknown-outcome reconciliation;
- resource ownership and business invariants;
- post-ALLOW revocation race accepted/rejected per state;
- pivot, compensation, forward recovery, late/out-of-order messages;
- manual review trigger and support/observability handling.

For **TOKEN operations**, document the required role/scope/ownership checks and explicitly acknowledge the stale-claim window where it could matter.

For **UserService local security operations**, document their **authoritative local access checks**, atomic concurrency/security mutations, and resulting revocation/session/SignalR behavior. Do not introduce a Saga merely because security is sensitive.

For **SERVICE/payment/device** flows, document service/provider authentication, allowed origin, correlation, idempotency, authority and any human confirmation needed; do not require an end-user actor unless the real use case supplies one.

---

## 6. Architectural trade-offs and technical debt

| Area | Accepted initial limitation / risk | When to revisit |
|---|---|---|
| `TOKEN` freshness | Current User/Account/role changes may not be observed for up to the remaining 15-minute JWT lifetime. | Security-sensitive use case, role revocation incident, stronger freshness requirement. |
| UserService local checks | A `TOKEN` label can be misread as “JWT alone is enough” for UserService security writes. | Coding-guide/agent reviews must enforce local authoritative checks. |
| Payment without actor | Payment service often acts from trusted business/service/payment-provider context rather than a human JWT. | New direct user-facing payment APIs, payment-authorization boundaries. |
| Physical gate variants | Physical gate/device flows cannot assume the customer is actively presenting a JWT. | Concrete check-in/out API and IoT/service trust design. |
| Async `REQUEST` | Initial API returns pending; workflow latency/availability depends on broker/UserService. | UX SLO, outage behavior and measured latency. |
| Parallel speculative effects | An access-denied request can consume temporary capacity, trigger compensation, or race with a late effect outcome. | Use-case-approved reversible holds, limits, durable branch state, correlation, compensation and reconciliation. |
| Exact-session behavior | Account-current Saga outcome does not prove exact initiating session is still active. | Workflow requiring session continuity or stronger `WORKFLOW` freshness. |
| No AccessVersion | Access-change event ordering and local projections lack a monotonic security revision. | Distributed authorization cache/projection or security event reordering problem. |
| Resource/scope correctness | JWT role freshness and local resource-scope truth are different concerns; JWT may not hold complete current scope. | Fine-grained permissions/scopes, tenant isolation test gaps. |
| Default `TOKEN` for some high-impact operations | Admin/facility/payment operations not in six REQUEST families may require stronger gates later. | Security threat model, incident, acceptance review, business/regulatory requirements. |
| Plan registration | New enrollment uses `REQUEST`; renewals, upgrades, cancellations not yet individually reviewed. | Plan lifecycle/use-case design. |

---

## 7. Change control / status

**APPROVED NOW:** Initial freshness classification of the six explicitly named REQUEST operation families, TOKEN as default for other authenticated end-user business operations, local-only authoritative UserService checks without auth Sagas, and a **parallel-capable speculative Saga pattern** subject to explicit per-use-case approval of eligible effects.

**NOT FINALIZED BY THIS MATRIX:** Specific controller/command types, actual Saga implementation, exact HTTP status/result contract, event names, payment-service actor requirements per endpoint, detailed checkout safety handling, gate device flows, plan renewal semantics, exact cross-service role/scopes or current-access DTO fields.

Before implementation, the responsible use-case owner must resolve those details using the canonical requirements and cross-module owner approvals. Do not silently assume a `REQUEST` label proves the Saga is implemented or that a `TOKEN` label waives other checks.
