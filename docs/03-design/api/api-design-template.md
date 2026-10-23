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
| Permission / account state | <current permission and eligible state> |
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
- [ ] Current account state, permissions and resource scope are explicit.
- [ ] Examples match schemas; material boundary/negative examples included.
- [ ] Retry, concurrency and time boundaries specified where applicable.
- [ ] Required state/schema mappings linked rather than silently omitted.
- [ ] Actual review evidence and exact design/implementation commits linked.

---
Adapted from the mentor-provided API Design Template.md and API Design Template.pdf received 2026-10-18. Retains Overview, API Specification, Request sample, Response sample, Validation, Activity Diagram and Sequence Diagram. SmartPark additions cover traceability, scope, contract status and conditional reliability details. Diagrams are optional for minimal contracts. This template does not approve new API behavior or a response-envelope migration.
