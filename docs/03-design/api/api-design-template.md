# <Feature> — API Design

| Metadata | Value |
| --- | --- |
| Status | DRAFT / IN REVIEW / APPROVED (link approval evidence) |
| Contract version / date | <version> / <YYYY-MM-DD> |
| Owning service / author | <service> / <author> |
| Jira design / implementation / verification | <links> |
| Requirements baseline | <version and pinned reference> |
| Implementation | <commit/PR, or NOT IMPLEMENTED> |

## Overview

<One or two sentences describing the capability and scope.>

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | <canonical links with section anchors> |
| Permission / service ownership | <links> |
| Approved decisions | <links, or none> |

Keep requirement narratives in their source artifacts. Include only observable API constraints here. Do not copy business objectives, functional requirements, use-case steps, SQL or repository algorithms. Never infer approval from current code.

## API Specification

Repeat this endpoint block for each operation; remove guidance and unused sections before review.

| Item | Contract |
| --- | --- |
| Method / URL | <METHOD> <path> |
| Availability | Implemented at <SHA> / Proposed / Deferred |
| Authentication | <anonymous, normal session, restricted purpose-bound credential, service identity> |
| Freshness / invocation origin | <TOKEN / REQUEST / PUBLIC / AUTH_FLOW / SERVICE; link operation matrix and any approved override> |
| Permission / account state | <required role/policy; token-time or authoritative local/current check per classification> |
| Resource scope | <own account, owned tenant/lot, assigned lot, platform> |
| Content type | <request / response> |
| Headers | <required headers; version or idempotency precondition only if applicable> |

### Request sample

<Valid request using synthetic data, including relevant path/query/header values. State “No body” when applicable. Never include live secrets.>

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| <name> | path/query/header/body | <type> | <required? null allowed?> | <bounds, enum, normalization> | <value> |

Define omitted versus null behavior, PATCH semantics, unknown-field handling and server-controlled fields. For lists, specify supported filters, stable ordering and pagination where applicable.

### Response sample

<HTTP status, relevant headers and valid JSON matching the approved response contract. A 204 response has no body.>

| Field | Type / format | Nullable / optional | Meaning | Example |
| --- | --- | --- | --- | --- |
| <name> | <type> | <rule> | <meaning, time zone, currency/units where relevant> | <value> |

Link the shared response contract if approved. Otherwise identify the current service envelope and label proposed changes. The mentor's `result/isSuccess/statusCode/message` sample is an example, not an adopted SmartPark-wide contract. Do not silently mix it with `success/data` or ProblemDetails.

### Validation and error responses

| HTTP status | Machine code | Trigger | Response example / schema | Client recovery |
| --- | --- | --- | --- | --- |
| <status> | <code> | <precise condition> | <valid JSON or linked schema> | <correct, retry, re-authenticate, query outcome> |

Cover applicable authentication, authorization/current scope, malformed binding, validation, business conflict, dependency failure and unexpected-error paths. A global catch does not establish a uniform response for all paths. Define field errors and correlation IDs where supported. Avoid secrets and another account's information.

### Authorization workflow — required when applicable

Use the [operation matrix](../../01-requirements/srs/authorization-operation-classification.md) and [authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md). The Freshness / invocation origin row records a documentation choice only; it does not require an interface, marker, attribute, property or registry entry in code. Classification does not grant product permissions. TOKEN has local JWT validation plus resource/domain checks and accepts remaining token-lifetime staleness. UserService security operations retain local authoritative checks; service/device/provider paths specify real origin authentication without a fabricated customer actor.

For REQUEST link the owning Saga design and specify trusted actor/policy context, durable pending admission, request/workflow ID, pending HTTP response and client status-observation method. `202`/PendingAuthorization is the architecture default, not a finalized endpoint/envelope. Specify authorization-gated effects, any explicitly approved reversible speculation, combined branch join, denial during in-flight/unknown effects, attempt correlation/fencing, idempotency/status lookup, retry owner/budgets/deadlines, accepted post-ALLOW race, pivot, compensation/forward recovery and manual-review behavior. Preserve safety/obligation exceptions. Missing necessary details are OPEN design work, not implicit approval.

Use the approved ADR §9 numeric defaults with justified tested overrides; payment business attempts and provider policies remain distinct. Do not invent DTO names, endpoints, speculative holds or an automatic runtime classification registry.

### Retry and state outcomes — if applicable

| Case | Observable contract |
| --- | --- |
| Repeated request / timeout / unknown outcome | <same result, conflict or safe lookup; do not invent idempotency support> |
| Concurrent mutation / stale version | <precondition and exact outcome> |
| Asynchronous dependency / notification failure | <committed, pending, failed; lookup/retry operation> |
| Time boundary | <expiry equality and authoritative clock semantics> |

Link state requirements. Retain only the outcomes needed by callers. Do not prescribe transactions, locks, outboxes or new abstractions without a separately justified implementation decision.

## Activity Diagram — optional

Use for a material caller-visible branch, or link the approved use-case/state artifact. Rejected paths must terminate without continuing to success. Omit diagrams that add nothing to a simple request/response.

## Sequence Diagram — optional

Use for a cross-service, asynchronous or security boundary. For .NET, show API → Application handler/use case → abstractions. Persistence owns database access; Infrastructure owns external integration. Distinguish implemented components from proposed ones. Link detailed diagrams when Jira requires them.

## Open questions and deferred items

| Item | Decision or implementation gap | Affected operation | Owner / Jira | Approval reference |
| --- | --- | --- | --- | --- |
| <ID> | <explicit issue; never silently choose policy> | <operation> | <link> | <link or pending> |

## Review checks

- [ ] Jira acceptance criteria covered or linked/deferred explicitly.
- [ ] Requirements and ownership linked; no copied FR/BR/UC narratives.
- [ ] Routes, fields, nullability, examples and errors are consistent.
- [ ] Implemented and proposed contracts clearly distinguished.
- [ ] Freshness/origin matches the approved matrix; permissions, local authority and resource scope are explicit.
- [ ] REQUEST pending response, authorized finalization, branch/race/recovery design and client observation are specified or explicitly blocked by OPEN decisions.
- [ ] TOKEN stale window, UserService local checks and service/provider trust are not conflated.
- [ ] Examples match schemas; material boundary/negative examples included.
- [ ] Retry, concurrency and time boundaries specified where applicable.
- [ ] Required state/schema mappings linked rather than silently omitted.
- [ ] Actual review evidence and exact design/implementation commits linked.

---
Adapted from the mentor-provided API Design Template.md and API Design Template.pdf received 2026-10-18. Retains Overview, API Specification, Request sample, Response sample, Validation, Activity Diagram and Sequence Diagram. SmartPark additions cover traceability, scope, contract status and conditional reliability details. Diagrams are optional for minimal contracts. This template does not approve new API behavior or a response-envelope migration.
