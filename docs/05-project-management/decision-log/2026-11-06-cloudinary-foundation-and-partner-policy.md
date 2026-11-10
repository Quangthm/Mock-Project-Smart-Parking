# Cloudinary foundation and initial Partner document policy

**Approved:** 6 November 2026 (Asia/Saigon). Source: user-supplied Cloudinary implementation plan and explicit follow-up decisions. Policy approval does not establish implemented APIs or provider capabilities.

## Delivery boundary and order

1. Publish the approved Partner policy and proposed User image contracts.
2. Supply shared development credentials to the four .NET hosts and both Python IoT processes. Configuration alone activates no media capability.
3. Implement UserService avatar/vehicle images, protected reads, owned attachment, recovery and additive UserDb upgrade. Other services receive configuration only.
4. Hand off the owned-asset contract to SPARK-243. New-vehicle registration end-to-end integration belongs to that task. Per later user direction, remove HTTPS-string image registration rather than maintaining that legacy writer; identify affected clients.

Partner uploads/replacements, document-version tables, lifecycle-event integration, PDF provider tests and Admin review/UI are later implementation. Partner media routes remain absent or disabled. Existing published terms and their acceptance/version history are separate from uploaded Partner evidence.

## Initial Partner policy — approved, runtime deferred

The canonical rules live in [media requirements](../../01-requirements/srs/modules/09-file-media.md) and [media business rules](../../01-requirements/business-rules/09-file-media.md):

- Readable PDFs, including scans, at most 10 MiB inclusive per file. Reject encrypted/password-protected, unreadable, malformed and non-PDF input by actual content validation.
- Incomplete drafts are allowed. Submission requires exactly one current agreement and one current policy. Save required setup/evidence before email OTP, then verify email before Admin review. An incomplete draft cannot complete first-lot setup.
- UserService owns restricted bytes, references and version history. Applicant access is limited to their application. Authorized Admin replacement is limited to initial pending applications; retain originals and replacements with actor, reason, timestamp and revision. Final review binds exact application/document versions and rejects a stale review after a competing replacement. Approved/rejected records are immutable. Preserve approval-with-modifications notification and additional-lot boundaries.
- Preserve all submitted versions during the active partnership and 12 months after authoritative recorded closure; approval does not start this clock.
- Rejected/withdrawn applications retain all submitted versions for 90 days after authoritative rejection/withdrawal. Missing lifecycle events block automated deletion and must be raised with the Partner owner; inactivity does not prove withdrawal/closure.
- Acknowledged holds protect affected versions. Review every 30 days and at case closure; releasing one hold preserves others. On final release re-evaluate the original retention deadline. Retain immutable review decisions/version references while due, unheld PDF bytes follow their retention policy.

## Shared development provider permissions

The same development cloud name, API key and API secret reach UserService, FacilityService, OperationService, PaymentService, `ai-rest` and `ai-live`. Each process has whatever provider permissions that credential grants. Folder prefixes are organizational, not authorization boundaries. SmartPark resource authorization and asset metadata/lifecycle remain with the owning service.

Future service-specific credentials **and narrower permissions**, or separate provider environments, remain options; no key-management platform is introduced. Keep credentials in runtime configuration, never in business identifiers or relationships. Do not claim provider-side isolation between shared-key services.

## User image decisions and explicit backlog

See [User media decisions and USER-MEDIA-CLEANUP-01](2026-11-06-user-media-cleanup-deferral.md): SkiaSharp with approved bounds; 24-hour draft lifetime; timestamps and expired-read blocking now; automatic deletion deferred; identical attachment retry succeeds and stale replacement conflicts. Existing image maximum remains 30 days subject to acknowledged preservation holds. Read blocking does not prove provider byte deletion.

## Evidence and remaining work

Q01/V11-DES-01 is **policy approved**, **Partner wire details/runtime deferred**, and **provider verification pending**. DB25 and the cross-service media action remain open. Live Cloudinary image/PDF access-control, cache and backup behavior is NOT RUN until recorded by an implementation test. No data migration or live provider operation is established by this decision record.
