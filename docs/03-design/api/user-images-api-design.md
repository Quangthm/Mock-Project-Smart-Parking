# User images — API Design

| Metadata | Value |
|---|---|
| Status | Implementation contract; feature decisions approved through [6 November decision](../../05-project-management/decision-log/2026-11-06-cloudinary-foundation-and-partner-policy.md) and [explicit follow-ups](../../05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md) |
| Contract version / date | 1 / 2026-11-07 |
| Owning service | UserService |
| Jira implementation / integration | SPARK-373 / SPARK-243; no external Jira update performed |
| Requirements baseline | SRS v1.1 with approved 6 November media amendment |
| Implementation | Working tree on `feature/SPARK-373-cloudinary-media-foundation`; uncommitted; evidence in task handoff |

## Overview

Upload an owned image, inspect its pending/ready state, attach it with a concurrency precondition, and retrieve bytes through authenticated application routes. No provider URL, arbitrary external URL import, general file upload or Partner PDF route is exposed.

| Traceability | Reference |
|---|---|
| FR / BR / UC | FR-AUTH-01/05/06, FR-RPT-05; [BR-PRIV-01/02/03](../../01-requirements/business-rules/09-file-media.md); [media SRS](../../01-requirements/srs/modules/09-file-media.md) |
| Permission / ownership | [Permissions](../../01-requirements/srs/permissions.md); [media owner contract](../../02-architecture/service-boundaries/media-contract.md) |
| Freshness | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md): ordinary own-profile/vehicle TOKEN, with existing authoritative local UserService account/session checks |

## API Specification

All routes require a valid Bearer token and current eligible User/Account/session under existing UserService local checks. Avatar scope is the User; vehicle scope is the **exact bound Driver Account**, including when one User has two Driver Accounts. A shared Cloudinary key grants no SmartPark permission. Unknown JSON properties are ignored and cannot select owner, purpose, provider or lifecycle state.

| Method / URL | Binding / scope | Success |
|---|---|---|
| `POST /api/vehicles/images` | Multipart `file` and exactly one `vehicleId` (existing own vehicle) or `draftIntentId` (nonempty UUID); required `Idempotency-Key` header; Driver only | 200 ready, or 202 pending |
| `POST /api/auth/me/avatar/images` | Multipart `file`; no vehicle/draft fields; required `Idempotency-Key`; own User, any eligible role | 200 ready, or 202 pending |
| `GET /api/media/images/{assetId}` | UUID path; authorized owner and current target/draft scope; no body | 200 metadata |
| `GET /api/media/images/{assetId}/content` | Same scope; ready and unexpired; no body | 200 image bytes |
| `PUT /api/vehicles/{vehicleId}/image` | JSON `assetId`, nullable `expectedAssetId`; own existing vehicle | 200 metadata |
| `PUT /api/auth/me/avatar` | JSON `assetId`, nullable `expectedAssetId`; own User | 200 metadata |

Availability: these routes are implemented by this foundation; media is disabled by default. No caller can delete provider bytes through these routes. Partner wire routes remain DEFERRED.

### Request samples and fields

```http
POST /api/vehicles/images
Authorization: Bearer <session-token>
Idempotency-Key: vehicle-image-001
Content-Type: multipart/form-data; boundary=<browser-generated>

file=<JPEG/PNG/WebP bytes>
draftIntentId=00000000-0000-4000-8000-000000000001
```

| Field | Location / type | Required / nullable | Meaning |
|---|---|---|---|
| `file` | multipart binary | Required | Nonempty, at most 5,242,880 bytes inclusive; actual JPEG/PNG/WebP; maximum 8,192 pixels per side and 25,000,000 total; no animated/multiframe content |
| `Idempotency-Key` | header string | Required | 1–128 ASCII letters/digits or `._:-`; scoped to creator Account and purpose; fingerprint includes bytes and target/draft intent |
| `vehicleId` / `draftIntentId` | multipart UUID | Exactly one for vehicles, neither for avatar | Target is checked against bound Account; draft intent grants no access to another asset |
| `assetId` | attach JSON UUID | Required, nonempty | Server-issued local asset identity; must match owner/purpose/state/target |
| `expectedAssetId` | attach JSON UUID | Nullable; omitted means null | Expected existing reference; null means no verified reference yet |

```json
{"assetId":"00000000-0000-4000-8000-000000000002","expectedAssetId":null}
```

Filename and multipart MIME are untrusted; decoded content determines MIME. ASP.NET form/temp buffering is bounded and request-owned; bytes passed inward contain no ASP.NET upload object. The whole multipart request has 64 KiB overhead allowance beyond the image size limit.

### Response sample

```json
{"success":true,"data":{"assetId":"00000000-0000-4000-8000-000000000002","state":"ready","expiresAt":"2026-11-08T00:00:00+00:00","contentPath":"/api/media/images/00000000-0000-4000-8000-000000000002/content","statusPath":"/api/media/images/00000000-0000-4000-8000-000000000002","recoveryCode":null}}
```

`assetId` is a local UUID. `state` is pending/ready; ready does not imply attachment. `expiresAt` is the applicable draft or attached-image deadline. `contentPath` is null while pending. `statusPath` is always the local metadata route. `recoveryCode` is a safe recovery classification, never a provider error/URL. Exposed metadata contains no hash, filename, provider credential or provider locator.

Binary reads return the validated image MIME, `Content-Disposition: inline; filename="image"`, `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`. Use an authorized fetch with the Bearer header; an unauthenticated HTML image request cannot read it. Dispose object URLs and clear any client image cache on logout/account switch. Current frontend profile code does not consume `avatarUrl`; new consumers must implement that lifecycle. Once a binary response starts, failures terminate the response rather than append JSON.

### Validation and error responses

Errors use existing `success:false`, `code`, `message`, optional `errors` and `traceId`; HTTP status is not repeated in JSON. Bearer challenges retain `WWW-Authenticate`.

| HTTP | Code | Trigger / recovery |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Missing/malformed fields, intent or idempotency key; correct input |
| 400 | `MEDIA_INVALID_IMAGE` | Invalid bytes, size, dimensions or animation; choose another image |
| 401 / 403 | `INVALID_TOKEN` / `FORBIDDEN` | Authentication/local account or Driver role check; reauthenticate or use appropriate account |
| 404 | `MEDIA_NOT_FOUND` | Missing or foreign asset/target; does not disclose another owner |
| 409 | `MEDIA_IDEMPOTENCY_CONFLICT` | Same key with different bytes/intent; use a new key for a new operation |
| 409 | `MEDIA_CONFLICT` | Wrong purpose/state/target or stale replacement; reload current resource |
| 409 | `MEDIA_PENDING` | Binary read before ready; check status/retry same upload |
| 410 | `MEDIA_EXPIRED` | Draft/image deadline reached; upload a new image |
| 413 / 415 | `PAYLOAD_TOO_LARGE` / `UNSUPPORTED_MEDIA_TYPE` | Request bound/content type rejected |
| 503 | `MEDIA_DISABLED` / `MEDIA_UNAVAILABLE` | Feature disabled or provider unavailable; retry after configuration/recovery |
| 500 | `INTERNAL_ERROR` | Sanitized unexpected failure; retry same key/check outcome |

### Retry and state outcomes

- Commit pending metadata and opaque deterministic provider locator before remote IO. Cancellation/restart leaves a recoverable row, not a successful attachment. No database transaction spans provider IO.
- Same actor/key/bytes/intent replays the same asset. Changed fingerprint conflicts. Provider upload uses explicit image/authenticated delivery with overwrite disabled; identity, dimensions, size and downloaded byte hash are verified before ready.
- A two-minute claim lease fences stale workers; a recovery pass runs every 30 seconds, at most ten candidates. Each asset has at most five claimed attempts with exponential minute backoff. Inconclusive outcomes remain pending. A confirmed absent object requires authorized same-key byte resubmission; background recovery has no durable raw bytes to upload. Exhausted attempts are flagged for manual reconciliation; creating a new request does not prove the old provider outcome absent.
- Identical attachment succeeds without another mutation. Concurrent replacements with the same expected reference have one winner and a 409 loser. An asset binds at most one vehicle; replacement does not overwrite provider objects. A replaced image is no longer readable through its former business-resource scope.
- At equality, draft age 24 hours or image age 30 days is expired. Attachment/retry/read does not reset admission. Ordinary expired reads and attachments are blocked. Bytes are not automatically removed in this slice; preservation holds do not grant broader ordinary read access.

## Vehicle/profile cutover and SPARK-243

`POST/PATCH /api/vehicles` now requires UUID `imageAssetId`; `expectedImageAssetId` protects replacements. New registration uses a ready owned draft asset. HTTPS `imageReference` input is removed; an arbitrary string cannot satisfy the required field. Existing DTO fields still carry plate/type; existing IDs/bindings persist. Responses include `imageAssetId`, application `imageReference` when managed, and `imageStatus` (`managed` or `reupload-required`). Legacy vehicle URLs remain stored for reconciliation/rollback but are not fetched or displayed as verified images. Legacy avatars preserve the profile response shape; managed `avatarUrl` points at the application content route. Ordinary profile PATCH remains name-only.

Both existing vehicle forms now upload files and submit asset IDs. SPARK-243 still owns full CAR/MOTORCYCLE registration acceptance, dispute workflow and new-registration end-to-end evidence. Foundation tests use existing synthetic drivers/vehicles and clearly distinguish real PostgreSQL from fake storage. No transfer/dispute capability is implied by an image attachment.

## Open questions and deferred items

| Item | Status / owner |
|---|---|
| USER-MEDIA-CLEANUP-01 | Automatic deletion, provider deletion proof, deletion scheduling/hold-race operational integration DEFERRED; UserService |
| Q01 / DB25 | Partner policy approved; Partner routes/version transport/tables/Admin integration DEFERRED; User/API/media |
| Live provider capabilities | NOT RUN until explicitly evidenced; no cross-service isolation claim under a shared key |
| Existing real data | Owner-led inventory/re-upload needed; no arbitrary URL download/backfill |

## Review checks

- [x] Scope, ownership, TOKEN/local authorization and unknown-field behavior explicit.
- [x] Upload, read, attachment, expiry and retry outcomes specified.
- [x] No provider URLs or secrets in the public contract.
- [x] Deletion and Partner rollout separated from current implementation.
- [x] [Evidence and handoff](../../04-testing/reports/SPARK-373/media-foundation-verification.md) records the base SHA and uncommitted implementation; no commit made by agent.
