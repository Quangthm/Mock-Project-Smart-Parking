# Vehicle registration and private claim intake — API design

| Metadata | Value |
|---|---|
| Story / tasks | US-D09 / SPARK-243; SPARK-224 backend, SPARK-225 frontend, SPARK-226 design, SPARK-227 acceptance |
| Owner | UserService |
| Revision | 8 November 2026 |
| Status | Implemented in the working tree; architecture rewritten to MediatR/repositories/UoW; prior PostgreSQL results are historical; current verification recorded separately |
| Baseline | `917975c56b5008d472bd3372cf2555aee14be87d`; changes uncommitted |
| Traceability | FR-VEH-01–04, BR-VEH-01/02, UC-VEH-01/02; [vehicle requirements](../../01-requirements/srs/modules/01-identity-access.md), [use cases](../../01-requirements/use-cases/uc-veh-01.md), [media](user-images-api-design.md) |
| Freshness / origin | Driver routes: TOKEN with authoritative local UserService security and exact Account ownership. Internal lookup: SERVICE using the explicitly approved temporary shared key |

## Overview

Register CAR and MOTORCYCLE vehicles. Retain the exact submitted plate text and a separate server-derived canonical value. A duplicate belonging to another Account creates a private `PENDING_DISPUTE` submission, not an active vehicle. Preserve the incumbent vehicle ID, binding and existing rights. Registration is not legal-title verification. Only an active CAR is selectable for Specific Slot; Operations independently enforces that reservation rule.

The claim-photo decision is [approved](../../05-project-management/decision-log/2026-11-07-vehicle-claim-images.md). Adjudication, Operator findings, holder notifications, transfers and full removal remain later UC-VEH-02/D12 work. External M5 CAR-only registration wording conflicts with the adopted CAR/MOTORCYCLE registration decision and must be corrected by its document owner; the CAR-only slot rule is unchanged.

## API specification

| Method / path | Authority | Success / result |
|---|---|---|
| GET `/api/vehicles` | Bound Driver Account | 200 own live vehicles; both categories, creation-time order |
| POST `/api/vehicles` | Bound Driver Account | 201 active vehicle after commit; 409 `PLATE_EXISTS` for duplicate, with pending intake committed for another holder |
| PATCH `/api/vehicles/{id}` | Bound Driver Account and own live record | 200 for supported full-field metadata/image edit; stable ID. Identity-changing edits withheld as below |
| GET `/api/vehicles/registration-claims` | Bound Driver Account | 200 own open submissions, creation-time order; separate collection from active vehicles |
| POST `/internal/vehicle-bindings/resolve` | Valid `X-Service-Key` matching existing `Services:Key` | 200 ACTIVE or common INELIGIBLE result; optional revision recheck |
| DELETE `/api/vehicles/{id}` | Deferred design only | Not implemented; no removal/transfer capability is implied |

No list pagination or client-supplied Account filter is currently exposed. Private lists and the resolver use no-store responses. Driver routes require valid authentication and local current User/Account/session checks; missing token challenges, wrong role is forbidden, and foreign/missing resource IDs share the safe not-found response.

### Registration and full-field edit request

```json
{"plate":" 51a-123.45 ","vehicleType":"CAR","imageAssetId":"00000000-0000-4000-8000-000000000001","expectedImageAssetId":null}
```

| Field | Required / meaning |
|---|---|
| `plate` | Required string, maximum 30 characters; preserve case, whitespace and separators exactly |
| `vehicleType` | Required CAR or MOTORCYCLE |
| `imageAssetId` | Required nonempty UUID, backend-admitted owned vehicle-purpose asset |
| `expectedImageAssetId` | Nullable; omitted means null. Existing reference expected when replacing an image |

Unknown JSON fields are ignored. Forged `accountId`, `canonicalPlate`, `status`, IDs and timestamps cannot change server-owned fields. PATCH requires the same field set; it is not a partial JSON patch. Raw-format edits that normalize to the same identity may succeed. Changing canonical plate/category is withheld with `VEHICLE_DEPENDENCY_UNAVAILABLE` until race-safe reservation/visit/pass coordination exists. A collision with another active vehicle returns `PLATE_EXISTS` without mutation.

Supported examples: CAR `" 51a-123.45 "` becomes `51A12345`; MOTORCYCLE `"59-x1 123.45"` becomes `59X112345`. Existing domain formats are retained; no expanded legal/special-series or non-plated self-registration is implied.

### Active vehicle response

POST returns 201; GET/PATCH return 200 using `success/data`:

```json
{"success":true,"data":{"id":"00000000-0000-4000-8000-000000000002","rawPlate":" 51a-123.45 ","canonicalPlate":"51A12345","vehicleType":"CAR","imageAssetId":"00000000-0000-4000-8000-000000000001","imageReference":"/api/media/images/00000000-0000-4000-8000-000000000001/content","imageStatus":"managed","createdAt":"2026-11-08T00:00:00+00:00","updatedAt":"2026-11-08T00:00:00+00:00"}}
```

GET wraps an array of that projection. No Account or provider locator is exposed. Legacy URL-only rows retain IDs/data but return null managed references and `reupload-required`; arbitrary URLs are never fetched or promoted into trusted media. Image expiry does not remove an active binding.

### Private claim response

```json
{"success":true,"data":[{"id":"00000000-0000-4000-8000-000000000003","rawPlate":"51A12345","canonicalPlate":"51A12345","vehicleType":"CAR","imageAssetId":"00000000-0000-4000-8000-000000000004","status":"PENDING_DISPUTE","createdAt":"2026-11-08T00:01:00+00:00"}]}
```

The claim ID is not a vehicle ID. Image access is restricted to the claimant Account; reviewer access is deferred to explicitly authorized adjudication. The photo follows admission + 30 days without resetting age. Claims survive image expiry; pending status does not establish a preservation hold. VEH-CLAIM-HOLD-01 and USER-MEDIA-CLEANUP-01 remain deferred.

### Binding lookup request and response

```json
{"accountId":"00000000-0000-4000-8000-000000000005","vehicleId":"00000000-0000-4000-8000-000000000002","expectedRevision":1}
```

Account and vehicle UUIDs must be nonempty. `expectedRevision` is optional/null or a positive integer. Operations supplies the Account bound to its trusted workflow; this request does not authenticate an end-user. A service credential must not come from the browser.

```json
{"success":true,"data":{"state":"ACTIVE","vehicleId":"00000000-0000-4000-8000-000000000002","accountId":"00000000-0000-4000-8000-000000000005","vehicleType":"CAR","revision":1}}
```

Foreign, missing, removed, unbound and claim IDs return the common result:

```json
{"success":true,"data":{"state":"INELIGIBLE","vehicleId":null,"accountId":null,"vehicleType":null,"revision":null}}
```

MOTORCYCLE can resolve ACTIVE with its real category; Operations must reject it for Specific Slot. No plate, image, claimant or other-holder detail is returned. Revision advances through the owning IUnitOfWork when tracked Account, canonical plate, category or removal state changes, not for image/raw-format edits. Raw SQL/bulk updates bypass that mechanism and are not supported binding writers. A successful lookup is a current observation, not a reservation lock. Identity-changing Driver edits remain disabled pending the Operations-side coordination contract.

**Temporary trust exception:** reuse existing `Services:Key`, at least 32 characters, via exactly one `X-Service-Key` header. Missing/wrong keys return 401 `INVALID_SERVICE_KEY`; missing configuration denies access (the host already rejects missing service-key setup). Every shared-key holder has this capability; Operations-specific identity is not established. [Explicit approval and SERVICE-AUTH-01](../../05-project-management/decision-log/2026-11-07-service-authentication-deferral.md) defer the replacement. Use protected transport outside isolated development; never log the key.

### Validation and errors

The existing `success:false`, `code`, `message`, optional `errors` and `traceId` envelope applies; HTTP status is not duplicated in JSON.

| HTTP / code | Outcome / recovery |
|---|---|
| 400 VALIDATION_FAILED / INVALID_PLATE | Invalid fields/category/format; correct input; no registration/claim insert |
| 401 / 403 | Invalid session or wrong role under existing host policy |
| 404 VEHICLE_NOT_FOUND | Own-resource lookup failed; foreign and missing IDs indistinguishable |
| 409 PLATE_EXISTS | Active create rejected; refresh both private collections to confirm durable outcome |
| 409 VEHICLE_RECONCILIATION_REQUIRED | Historical-only binding or unsettled winner; activation withheld for reconciliation |
| 503 VEHICLE_DEPENDENCY_UNAVAILABLE | Canonical plate/category edit withheld; original vehicle unchanged |
| 409 VEHICLE_BINDING_STALE | Recheck returned a different binding revision |
| 401 INVALID_SERVICE_KEY | Internal caller rejected; no human-token fallback |
| Media errors | Existing 404 MEDIA_NOT_FOUND for missing/foreign/wrong-purpose references, 409 MEDIA_CONFLICT, 410 MEDIA_EXPIRED, 503 MEDIA_DISABLED/UNAVAILABLE; see image contract |
| 500 INTERNAL_ERROR / transport failure | Unknown/dependency failure, never INELIGIBLE or confirmed saved pending status; reconcile through reads |

### Retry and state outcomes

- Same Account's active plate: 409, no claim, no edit of the existing record.
- Different Account's active plate: one claimant-owned pending row committed before 409. No incumbent mutation, notification, booking cancellation or gate action.
- Same claimant/canonical plate: reuse the existing open claim; preserve its photo, original text/category and timestamp. Retry never promotes it after incumbent removal or image expiry.
- New registration of a historical-only plate: withhold activation; do not pick a historical winner by time. Null-owner live rows remain protected by active uniqueness.
- Invalid new input/media and failures before commit insert nothing. An unknown commit acknowledgement is not reported as saved; private reads reconcile the outcome.
- Active uniqueness is global across Accounts/categories; pending uniqueness is per Account/canonical plate. Targeted conflict handling does not translate unrelated database failures into plate conflicts.

## Rollout and remaining work

Use the [UserDb guide](../../02-architecture/database/microservices/01-user-access-service-db-docs.md) for the consolidated idempotent creation script and existing-schema limitations. No shared database is automatically migrated. Preserve legacy data and accepted IDs.

V11-DES-10 remains open for partial PATCH semantics, full removal, adjudication, legacy reconciliation and any future format expansion. AC-098 soft removal remains separate. SERVICE-AUTH-01, VEH-CLAIM-HOLD-01 and automatic media cleanup are deferred. Production credentials, real-provider uploads, manual responsive UI checks and an existing cross-service booking remain distinct acceptance evidence, not inferred from a build or fake provider.

## Review checks

- [x] Exact Account ownership, raw input, physical categories and separate private intake specified.
- [x] Shared-key exception and future security direction distinguished.
- [x] Commit/retry/error outcomes and identity-edit boundary explicit.
- [x] Implemented routes separated from deferred removal/review work.
- [ ] Full live-provider, UI and Operations integration acceptance; see the [SPARK-243 evidence record](../../04-testing/reports/SPARK-243-vehicle-registration-2026-11-08.md) and [deployment checklist](../../04-testing/reports/SPARK-243-deployment-checklist.md).

## Implementation path after architecture correction

`VehiclesController` maps narrow requests using AutoMapper and dispatches `IMediator`. Separate save/list/pending/binding handlers own use-case orchestration and return Application response DTOs. FluentValidation runs before dispatch; Domain factories/mutations enforce plate and metadata invariants. `IVehicleRepository`, `IRegistrationClaimRepository` and `IMediaAssetRepository` own persistence access; `IUnitOfWork` owns transaction commit and binding revision maintenance. The forwarding registration service/interface and binding store are removed. Media handlers use aggregate repositories rather than a general media store. Controllers do not project or expose tracked Domain entities.

The original UserDb script is the sole schema artifact. Circular FK ALTERs, the existing session immutability trigger and the role seed are the explicitly approved exceptions. No vehicle functions/triggers or overlay remain. Current build/unit evidence and user-run PostgreSQL/deployment checks are separated in the linked report; the former 70-test result does not validate this rewrite.
