# File Media — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

Public only when facility photos/diagrams are explicitly published for public listing. LPR, vehicle/profile images, incident/appeal evidence and financial documents are restricted. Use backend uploads, service-owned stable asset references and authenticated proxy for evidence.

| Purpose | Accepted MVP types | Accepted limit |
|---|---|---|
| Facility photos | JPEG, PNG, WebP | 5 MiB each; 10 per lot |
| Avatar / vehicle image | JPEG, PNG, WebP | 5 MiB each |
| LPR / incident / appeal image | JPEG, PNG | 10 MiB each; 5 per submission |
| User video / SVG / HTML / archive | Not accepted in initial upload scope | No general upload endpoint for these types |
| Generated invoice PDF | Server-generated | Separate financial-document lifecycle; not arbitrary user PDF upload |

Validate content, signature/decoding and size, not client filename/MIME alone. Additional scanned-document/PDF evidence requires explicit accepted scope. No upload of the three fixed 3D deployment assets through this media flow.

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

## Functional requirements
