# Media ownership and portability — v1.1 review candidate

**Technical design; provider capabilities not yet validated.** Cloudinary is first evaluation adapter; R2 is future migration, not current dual-write scope.

### 3.1 Ownership and reference model

Store bytes in Cloudinary and authoritative asset metadata in the business service that owns the asset's lifecycle. Other services use an opaque reference, not the owner's database or an expiring URL.

| Asset | Proposed owner | Cross-service use |
|---|---|---|
| Avatar, registered-vehicle image | User & Access Service | Consumers receive an authorized representation/reference. Personal profiles need not have a parking tenant. |
| Initial Partner policy/agreement uploads and original/replacement versions | User & Access Service | Restricted applicant/Admin review access through owning-service authorization; immutable evidence after final review. No arbitrary external document URL. |
| Lot photo, uploaded diagram | Facility & Visualization Service | Search/UI consumes approved public or restricted representation. Fixed 3D models stay deployment assets under D4. |
| LPR source image and recognition result | Recognition/Evidence module within IoT & Recognition Service | Session/incident stores `{ownerService, assetId}` and a reference to recognition evidence. Store once; recognize again without changing original evidence. |
| Operator-uploaded incident or session evidence | Parking Operations Service | Appeal references the evidence; retention owner acknowledges holds. Proxy access, acknowledged holds and limits are confirmed under B06/§14.4; implementation mapping remains. |
| Generated financial document | Payment & Pricing Service | Customer receives an authorized document endpoint. Retention class is financial-document policy, not automatically the 30-day image policy. |

Suggested metadata: local `assetId`; business owner/resource; tenant/site where applicable; purpose and sensitivity; provider; provider environment/account locator; provider asset/object identifiers; resource/delivery type and version; validated MIME type, byte size and hash; creation time; retention deadline; hold records; lifecycle state; deletion/retry metadata. Provider secrets never belong in these records.

For Cloudinary, retain sufficient identifiers to retrieve/delete the precise resource (including public ID, resource type and delivery type, plus asset ID/version where applicable). An R2 adapter would use bucket/object/version details. Keep these details behind the adapter rather than forcing every business table to understand both providers.

Example business reference:

```json
{"ownerService":"iot-recognition","assetId":"opaque-uuid","purpose":"entry-evidence"}
```

Example opaque provider path: `smartpark/{environment}/{service}/{purpose}/{assetId}`. Do not put plates, email addresses, names or access tokens in paths. Folders are organization, not authorization. Do not assume one shared provider account provides isolation between service credentials; document the actual account capabilities during evaluation.

### 3.2 Request flow

1. Client uploads to the owning business API through the gateway. The owner checks current permission, tenant/site/resource, allowed purpose, type, size and content, and creates an idempotent pending asset record.
2. The owner's storage adapter uploads using server-held credentials and an opaque provider identifier. On success, the owner finalizes metadata and attaches the asset to the business record.
3. Restricted reads use a stable application route, such as `GET /api/sessions/{sessionId}/evidence/{assetId}/content`. Parking Operations authorizes the viewer. When the Recognition/Evidence module owns the image, it retrieves via a narrowly authorized internal request to IoT & Recognition; the asset owner verifies service identity, scope and asset binding. Do not accept arbitrary remote URLs supplied by clients.
4. Other services carry `{ownerService, assetId}`; they do not copy expiring links into durable events. Read-time delivery may be a proxy or an expiring access grant according to approved sensitivity.
5. Owner performs scheduled deletion after retention expiry unless a hold is acknowledged. An appeal must obtain the hold acknowledgement before claiming evidence is preserved. Hold release and deletion must coordinate against races.

Cloudinary distinguishes public, private and authenticated delivery. A signed transformation URL is not automatically a short-lived access grant, and some token-based controls depend on account features. For strict evidence access, evaluate authenticated originals and proxying; verify transformed copies and caches are also restricted. See [Cloudinary access-control documentation](https://cloudinary.com/documentation/control_access_to_media).

### 3.3 Failures and portability

An upload and a business-database commit are not one transaction. Use pending/finalized/deletion-pending states, idempotent provider keys and a reconciliation job. Handle upload-success/database-failure, abandoned uploads, duplicate requests, provider outage, deletion failure and expired access. Do not report evidence as attached until finalization succeeds. Never delete held evidence merely because its original device or business row is removed.

Portable interface responsibilities: store validated content, read/stream content, issue temporary access only when supported, delete, and obtain metadata. Authorization and retention policy remain in business services. A shared infrastructure package may contain adapters; it should not contain a shared media database or business rules. Avoid making Cloudinary transformation syntax part of the domain contract.

Migration to R2 still requires copying bytes, verifying hashes, switching locators and retaining a recovery window. The interface reduces code coupling; it does not eliminate migration, access-control or egress work. One provider remains active for the initial evaluation; do not add dual writes now.

### 3.4 Partner agreement document versions (V11-02)

Upload actual content through the owning User & Access API and the existing pending/finalized lifecycle. Application ID alone is not authorization: applicant setup credentials must be bound to that application; Admin read/replacement requires current review authority. Other services receive opaque `{ownerService, assetId}` references only when authorized, not reusable provider URLs. Supported formats, maximum sizes, download route, access-grant lifetime and exact wire schema are design-pending (User/API and media leads, DB25).

Only Admin may replace uploaded policy/agreement documents for an **initial pending** Partner application. Do not edit other application fields or documents on pending additional-lot applications. Preserve original bytes and each replacement, content hash/version, application/revision binding, actor, time and reason. Final approval atomically checks the reviewed application/document version; a concurrent replacement must fail the stale review or be excluded by concurrency control, never be silently approved.

Approved/rejected applications and their review evidence are immutable. Replacement is not destructive overwriting; approval after any replacement queues the exact approved-with-modifications notification. External agreement is not an in-app re-acceptance record. Keep restricted evidence/hold/deletion lifecycle under the owning service; do not apply the 30-day operational-image retention automatically to legal agreement evidence. Exact retention-class mapping remains design-pending with User/media lead under existing retention governance; no new duration is approved here.
