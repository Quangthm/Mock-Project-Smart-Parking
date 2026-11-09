# User media foundation — cleanup deferral and implementation decisions

**Decision date:** 6 November 2026 (Asia/Saigon)  
**Source:** Explicit user decisions during the SPARK-373 Cloudinary foundation task.  
**Status:** Decisions approved; foundation implemented and verified on 7 November 2026. Automatic deletion remains deferred. See [verification evidence](../../04-testing/reports/SPARK-373/media-foundation-verification.md).  
**Scope:** UserService avatar and vehicle images. Partner PDF implementation remains a separate task.

## Confirmed decisions

| Topic | Approved direction | Implementation status |
|---|---|---|
| Image decoder | Use SkiaSharp, independent of the Cloudinary storage adapter; maximum 8,192 pixels per side, 25,000,000 pixels total, no animated/multiframe images. | Implemented; actual decode plus PNG/WebP animation-marker rejection. |
| Concurrent attachment | An identical attachment retry succeeds without another mutation; a stale conflicting replacement returns HTTP 409. | Implemented; real PostgreSQL competing-attachment test passed. |
| Legacy registration | Remove HTTPS-string image registration support rather than retaining a compatibility writer. Account for existing frontend callers and the SPARK-243 contract. | Implemented in API and both forms; old columns/data retained. |
| Database verification | Dedicated disposable PostgreSQL test containers may be started. | Executed on disposable fixtures; no existing application database changed. |
| Unattached drafts | Draft lifetime is 24 hours from admission. | Implemented and tested. |
| Expiry enforcement | Persist timestamps and block expired reads in this foundation; a rolling UTC cutoff needs no per-image timer. Attachment/retry must not reset age. | Implemented and tested; attached image deadline remains admission + 30 days. |
| Automatic deletion | Defer the automatic deletion worker to later backlog implementation. | **DEFERRED — NOT IMPLEMENTED.** |

## Backlog: USER-MEDIA-CLEANUP-01

This is a repository backlog identifier, **not a created Jira issue**.

- **Status:** DEFERRED / implementation pending; no completion claim.
- **Owning service:** User & Access / UserService; individual assignee and delivery date not assigned.
- **Dependency:** SPARK-373 asset metadata, approved expiry semantics and provider adapter.
- **Scope:** Automatic removal of expired avatar/vehicle image bytes and abandoned draft bytes. Preserve vehicle identity, accepted account bindings, bookings and required audit metadata.
- **Policy:** The existing 30-day image maximum and acknowledged-hold rules are not extended by deferring this worker. Unattached drafts have the approved 24-hour lifetime. Partner PDFs, invoices and other services' assets retain their separate policies and owners.
- **Known limitation:** Without a deletion worker or an equivalent verified operational deletion process, provider bytes can remain beyond their due date. Read denial alone does not remove those bytes. Do not report physical-retention enforcement as implemented or tested.

### Required implementation and acceptance evidence

- [ ] Select due assets from authoritative timestamps/deadlines; do not reset age on attachment, reads or retries. Final timestamp anchor must be recorded in the approved contract.
- [ ] Apply approved draft expiry and image retention rules; preserve all acknowledged active holds.
- [ ] Serialize hold acknowledgement against the irreversible deletion decision; reject late preservation acknowledgement and preserve independent holds on release.
- [ ] Safely claim work under concurrency; keep provider calls outside database transactions; bound retries and recover after restart or unknown provider outcomes.
- [ ] Delete the exact provider asset/version and verify original, derived, cached and backup copies to the extent provider capabilities allow; record unsupported or unresolved verification honestly.
- [ ] Record safe deletion evidence, retry state and overdue failures. The 24-hour provider-confirmation target does not extend a hard retention deadline.
- [ ] Verify expiry boundaries, competing workers, hold/deletion races, provider outages, unknown outcomes and restart recovery using isolated PostgreSQL and fake-provider tests; label live-provider results separately.
- [ ] Document scheduling, operational recovery, monitoring and rollout; update this backlog item only after actual evidence is available.

## Clarifications resolved

The user explicitly confirmed retaining timestamps and blocking expired reads now while deferring deletion, and subsequently confirmed all three image admission limits. Read expiry is due at equality: unattached drafts at admission + 24 hours; attached images at admission + 30 days. Successful attachment within the draft window does not restart the image age. The cleanup backlog remains open regardless of read-expiry implementation.

## References

- [Media requirements](../../01-requirements/srs/modules/09-file-media.md)
- [Media business rules](../../01-requirements/business-rules/09-file-media.md)
- [Media ownership contract](../../02-architecture/service-boundaries/media-contract.md)
- [Implementation backlog](deferred-and-implementation-register.md)
