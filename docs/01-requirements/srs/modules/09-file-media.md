# File Media — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

Public only when facility photos/diagrams are explicitly published for public listing. LPR, vehicle/profile images, incident/appeal evidence, financial documents and Partner application agreement/policy documents are restricted. Use backend uploads, service-owned stable asset references and authenticated proxy for evidence.

| Purpose | Accepted MVP types | Accepted limit |
|---|---|---|
| Facility photos | JPEG, PNG, WebP | 5 MiB each; 10 per lot |
| Avatar / vehicle image | JPEG, PNG, WebP | 5 MiB each |
| LPR / incident / appeal image | JPEG, PNG | 10 MiB each; 5 per submission |
| User video / SVG / HTML / archive | Not accepted in initial upload scope | No general upload endpoint for these types |
| Initial Partner agreement/policy upload | Required upload scope; formats/limits design-pending (Q01) | User-owned versioned restricted assets; no arbitrary document URL |
| Generated invoice PDF | Server-generated | Separate financial-document lifecycle; not arbitrary user PDF upload |

Validate content, signature/decoding and size, not client filename/MIME alone. Initial Partner agreement/policy upload is now required scope under V11-02; exact formats, sizes/counts and retention classification remain Q01 technical design work owned by User/API/media leads. This does not enable general PDF evidence upload. No upload of the three fixed 3D deployment assets through this media flow.

Scoped Operator may request a hold for handled incident/appeal; Owner for escalated/monthly case; Admin for authorized platform investigation. Asset owner acknowledges before another service claims preservation. Authorized handler releases only the originating hold when its need ends; other active holds remain. Audit place/release and coordinate deletion races.

Missing required evidence keeps evidence-dependent decisions pending; a case can exist with EVIDENCE_PENDING. Approved emergency operations may proceed with an audited missing-evidence incident. Never display failed upload as successfully attached.

Accepted recommended operational target: provider deletion confirmation within 24 hours after deletion becomes due, with retry/alert if overdue. This does **not** extend a hard maximum-retention deadline: schedule provider work accordingly, restrict ordinary delivery when due/no hold, and record unresolved failures honestly. Confirm originals/derived delivery copies according to evaluated provider capabilities. Financial documents: 7 years from issuance, subject to holds; conversations: 90 days from last message, unaffected by administrative reads; hold review every 30 days and at case closure (§16.4). C-20 categories otherwise retained.

| Data / action | Approved policy | Remaining precise specification |
|---|---|---|
| Images | Existing maximum 30 days, subject to authorized holds | Preserve existing class/scope; do not silently expand retention because an image is attached to a financial record. |
| Audit logs | Existing 7 years | Existing audit scope and protected access retained. |
| Operational logs | Existing 90 days hot + 1 year cold | Preserve class distinction and existing lifecycle; production duration cannot be “tested” by a short demo. |
| Financial documents | 7 years from issuance, subject to holds | Project-policy approval, not a conclusion about applicable law. Preserve issuance/version/adjustment links. |
| Assistant conversations | 90 days from the last message | Administrative reads do not reset the clock; necessary case evidence follows its own owner/class/hold, not indiscriminate full-chat copying. |
| Hold review | Every 30 days and at case closure | Review is not auto-release. Retain other active holds when one originating hold is released; overdue reviews alert authorized handler. |
| Deletion proof | Provider outcome plus asset/version/time, retries and capability-appropriate verification, including derived copies | Evaluate actual provider capabilities; record unresolved failure honestly. |
| Deletion operational target | Existing 24-hour confirmation target | Does not extend any hard retention deadline; start work accordingly and restrict ordinary access when due without hold. |
| Plate-binding evidence | Verified contact, relevant registration/transfer evidence where applicable, scoped Operator findings, Admin decision | OTP/plate photo alone insufficient. Existing accepted image types/limits; no routine national-ID collection or general PDF upload. Admin reviews sufficiency, notifies existing holder and records decision; no transfer on silence; unresolved cases pending/escalated. |

Preserve §14.4 upload limits, backend content validation, restricted proxy delivery, service-owned stable references and hold acknowledgement. Missing evidence keeps evidence-dependent action pending; approved emergency action can proceed with an audited missing-evidence incident. No blanket storage-outage free-exit or automatic binding-transfer policy.

## Initial Partner agreement lifecycle

User owns application/document metadata and the original plus each replacement version, with actor, reason, time and stable owning-service asset references. Scoped applicants upload/read only their application evidence; authorized Admin review/replacement uses protected authenticated delivery. A public URL or application ID alone grants nothing. Admin replacement is allowed only for uploaded agreement/policy documents in the initial pending application, never other fields or additional-lot applications. Approved/rejected applications are immutable.

Approval checks expected application/document versions so a concurrent replacement cannot silently alter the approved agreement. Persist the version approved and audit; approval after replacement sends the required modification notification without an in-app re-acceptance step. Upload failure or missing required evidence blocks evidence-dependent review, not truthful persistence of the pending application. Reconcile orphan uploads by stable identity.

Retain original/replacement evidence under the existing media lifecycle and acknowledged holds; do not overwrite or destructively replace assets. Q01 must classify agreement retention and protected upload/download contracts before implementation acceptance; do not silently reuse invoice retention or extend the image maximum. Audit/version metadata and asset-content retention are separate. The fake VNeID API returns a GUID substitute associated with the user and persisted in the User & Access database; it is not government identity data or an authentication token. Additional simulation fields, consent/completion evidence encoding and retention remain Q02 design work; no database field name or wire route is inferred.

## Functional requirements


Design/implementation owners and exit criteria: [V11-DES-01 (Q01) and V11-DES-02 (Q02)](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
