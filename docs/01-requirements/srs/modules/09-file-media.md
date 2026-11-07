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
| Initial Partner agreement/policy upload | Readable PDF, including scanned PDF | 10 MiB (10,485,760 bytes) per file inclusive; exactly one current agreement and one current policy at submission |
| Generated invoice PDF | Server-generated | Separate financial-document lifecycle; not arbitrary user PDF upload |

Validate content, signature/decoding and size, not client filename/MIME alone. Initial Partner agreement/policy is approved on 6 November 2026: reject password-protected/encrypted, unreadable, malformed or non-PDF files. Partner wire details and implementation remain deferred under Q01/V11-DES-01; no general PDF evidence upload is enabled. No upload of the three fixed 3D deployment assets through this media flow. User avatar/vehicle images additionally require dimensions at most 8,192 pixels per side and 25,000,000 pixels total; animated/multiframe images are rejected.

Scoped Operator may request a hold for handled incident/appeal; Owner for escalated/monthly case; Admin for authorized platform investigation. Asset owner acknowledges before another service claims preservation. Authorized handler releases only the originating hold when its need ends; other active holds remain. Audit place/release and coordinate deletion races.

Missing required evidence keeps evidence-dependent decisions pending; a case can exist with EVIDENCE_PENDING. Approved emergency operations may proceed with an audited missing-evidence incident. Never display failed upload as successfully attached.

Accepted recommended operational target: provider deletion confirmation within 24 hours after deletion becomes due, with retry/alert if overdue. This does **not** extend a hard maximum-retention deadline: schedule provider work accordingly, restrict ordinary delivery when due/no hold, and record unresolved failures honestly. Confirm originals/derived delivery copies according to evaluated provider capabilities. Financial documents: 7 years from issuance, subject to holds; conversations: 90 days from last message, unaffected by administrative reads; hold review every 30 days and at case closure (§16.4). C-20 categories otherwise retained.

| Data / action | Approved policy | Remaining precise specification |
|---|---|---|
| Images | Existing maximum 30 days, subject to authorized holds | Preserve existing class/scope; do not silently expand retention because an image is attached to a financial record. |
| Submitted initial Partner documents — approved partnership | All submitted versions during active partnership and 12 months after recorded closure, subject to acknowledged holds | Approval time is not the retention anchor; missing authoritative closure blocks automated deletion. |
| Submitted initial Partner documents — rejected/withdrawn | All submitted versions for 90 days from recorded rejection/withdrawal, subject to acknowledged holds | Missing lifecycle timestamp blocks automated deletion and is raised with Partner owner; inactivity is not withdrawal. |
| Audit logs | Existing 7 years | Existing audit scope and protected access retained. |
| Operational logs | Existing 90 days hot + 1 year cold | Preserve class distinction and existing lifecycle; production duration cannot be “tested” by a short demo. |
| Financial documents | 7 years from issuance, subject to holds | Project-policy approval, not a conclusion about applicable law. Preserve issuance/version/adjustment links. |
| Assistant conversations | 90 days from the last message | Administrative reads do not reset the clock; necessary case evidence follows its own owner/class/hold, not indiscriminate full-chat copying. |
| Hold review | Every 30 days and at case closure | Review is not auto-release. Retain other active holds when one originating hold is released; overdue reviews alert authorized handler. |
| Deletion proof | Provider outcome plus asset/version/time, retries and capability-appropriate verification, including derived copies | Evaluate actual provider capabilities; record unresolved failure honestly. |
| Deletion operational target | Existing 24-hour confirmation target | Does not extend any hard retention deadline; start work accordingly and restrict ordinary access when due without hold. |
| Plate-binding evidence | Verified contact, relevant registration/transfer evidence where applicable, scoped Operator findings, Admin decision | OTP/plate photo alone insufficient. Existing accepted image types/limits; no routine national-ID collection or general PDF upload. Admin reviews sufficiency, notifies existing holder and records decision; no transfer on silence; unresolved cases pending/escalated. |

Preserve §14.4 upload limits, backend content validation, restricted proxy delivery, service-owned stable references and hold acknowledgement. Missing evidence keeps evidence-dependent action pending; approved emergency action can proceed with an audited missing-evidence incident. No blanket storage-outage free-exit or automatic binding-transfer policy.

**User image rollout status — 6 November 2026:** automatic avatar/vehicle image deletion is explicitly deferred to [USER-MEDIA-CLEANUP-01](../../../05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md). Unattached User image drafts have a 24-hour lifetime. Persist timestamps and block expired ordinary reads in the foundation; attachment/retry does not reset the admission timestamp. This deferral does not extend the existing 30-day maximum or establish physical-retention enforcement. No runtime evidence is claimed by these requirements.

## Initial Partner agreement lifecycle

User owns application/document metadata and the original plus each replacement version, with actor, reason, time and stable owning-service asset references. Scoped applicants upload/read only their application evidence; authorized Admin review/replacement uses protected authenticated delivery. A public URL or application ID alone grants nothing. Admin replacement is allowed only for uploaded agreement/policy documents in the initial pending application, never other fields or additional-lot applications. Approved/rejected applications are immutable.

Incomplete drafts are allowed. Exactly one current agreement and one current policy are required before submission. Save setup and required evidence before email OTP, then verify email before Admin review. An incomplete draft cannot advance as completed first-lot setup. Only current document versions count toward the two required files.

Approval checks expected application/document versions so a concurrent replacement cannot silently alter the approved agreement. Persist the version approved and audit; approval after replacement sends the required modification notification without an in-app re-acceptance step. Upload failure or missing required evidence blocks evidence-dependent review, not truthful persistence of the pending application. Reconcile orphan uploads by stable identity.

Retain every submitted original/replacement version using the Partner retention rows above; do not overwrite assets or reuse invoice/image durations. Acknowledged holds protect affected versions beyond the deadline; review holds every 30 days and at case closure. Releasing one hold preserves other active holds; final release re-evaluates the original deadline. Immutable final decisions and version references survive according to their own policy while due, unheld PDF bytes follow the Partner retention policy. Audit/version metadata and content retention are separate. [Approved policy and rollout](../../../05-project-management/decision-log/2026-11-06-cloudinary-foundation-and-partner-policy.md): Q01 policy approved, Partner wire details/runtime deferred, provider verification pending. The fake VNeID API returns a GUID substitute associated with the user and persisted in User & Access; it is not government identity data or an authentication token. Additional simulation fields, consent/completion evidence encoding and retention remain Q02 design work; no database field name or wire route is inferred.

## Functional requirements


Design/implementation owners and exit criteria: [V11-DES-01 (Q01) and V11-DES-02 (Q02)](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
