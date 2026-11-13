# File Media — business rules v1.1

[Rule index](README.md) · [SRS module](../srs/modules/09-file-media.md)

## BR-PRIV-01

**Status:** CONFIRMED — implementation pending / not assessed

Apply least exposure and owning-service authorization. Initial Partner agreement/policy uploads are restricted readable PDFs (including scans), at most 10 MiB inclusive each; reject password-protected/encrypted, unreadable, malformed or non-PDF content. Allow incomplete drafts, but require exactly one current agreement and one current policy before submission or completed first-lot setup. Save setup/evidence before email OTP, then verify email before Admin review. Retain every original/replacement with actor/time/reason/revision; final review binds exact application and document versions and fails after a competing replacement. Only authorized Admin may replace documents on an initial pending application; no other fields, additional-lot editing or changes to finalized approved/rejected records. Preserve approval-with-modifications notification. Stable owned assets and authenticated reads replace arbitrary URLs. Q01 policy is approved; Partner wire details/runtime remain deferred and provider verification pending. Mandatory simulated VNeID returns a user-associated GUID persisted in User & Access; additional fields, consent/completion evidence and retention remain Q02. The GUID is neither government identity data nor an authentication token.

User avatar/vehicle images remain JPEG/PNG/WebP up to 5 MiB inclusive, with at most 8,192 pixels per side and 25,000,000 pixels total, no animated/multiframe images. Record admission timestamps; unattached drafts expire at 24 hours, attached images at 30 days from admission. Block expired ordinary reads now. [Automatic deletion remains deferred](../../05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md); no physical-retention compliance is claimed.

**Trace:** FR-AUTH-01/05/06, FR-RPT-05; UC-RPT-01, UC-RPT-02, UC-AI-03

## BR-PRIV-02

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; Assistant keeps one current conversation per Account and role until reset. Reset hides the old transcript and makes it due for deletion after 7 days, subject to acknowledged holds. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-05; UC-RPT-02, UC-LPR-01

## BR-PRIV-03

**Partner retention amendment — approved 6 November 2026:** retain all submitted initial Partner document versions throughout an active partnership and 12 months after recorded closure; approval time is not the anchor. Rejected/withdrawn applications retain all submitted versions 90 days from recorded rejection/withdrawal. Require authoritative lifecycle timestamps; missing events must be raised with the Partner owner and block automated deletion. Inactivity does not establish closure/withdrawal. Acknowledged holds protect affected versions; review every 30 days and at case closure, release only the originating hold, and re-evaluate the original deadline when the final hold ends. Immutable final decisions/version references remain distinct from due, unheld PDF bytes. Image and invoice retention remain separate. [Decision and implementation boundary](../../05-project-management/decision-log/2026-11-06-cloudinary-foundation-and-partner-policy.md).

**Status:** CONFIRMED — implementation pending / not assessed

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; Assistant keeps one current conversation per Account and role until reset. Reset hides the old transcript and makes it due for deletion after 7 days, subject to acknowledged holds. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

**Trace:** FR-RPT-03/04; UC-AUTH-02, UC-RPT-02
