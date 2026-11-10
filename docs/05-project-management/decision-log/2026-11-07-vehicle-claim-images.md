# Vehicle registration claim images — SPARK-243

**Decision date:** 7 November 2026 (Asia/Saigon)
**Source:** Explicit user approval in the SPARK-243 implementation conversation.
**Status:** APPROVED behavior; claim attachment implemented in the SPARK-243 working tree; isolated evidence is linked from the vehicle API. Live acceptance remains pending.
**Scope:** Claim-photo association now; explicitly authorized reviewer access with later UC-VEH-02 adjudication. Temporary shared-key lookup protection is approved separately below.

## Approved behavior

- Associate the submitted, backend-validated, owned vehicle photo with the claimant's pending registration submission. A claim is separate from an active vehicle; its photo grants no binding, booking, entry or transfer right and does not alter the incumbent's registration.
- Use the existing UserService-owned asset and restricted delivery model. Preserve the original submission's photo reference on registration retries; do not create a vehicle merely to attach a claim image.
- The claimant accesses their own submission under the exact authenticated Account. When adjudication is implemented, explicitly authorized reviewers may access the evidence needed for their review under UC-VEH-02. This is not permanent claimant-only visibility, nor blanket access for every Operator or Admin. Detailed reviewer permissions and routes belong to that later workflow.
- Apply the existing attached-image deadline of upload admission + 30 days. Association, retries and review do not reset image age. Unattached drafts retain their existing 24-hour deadline. Preserve independently acknowledged holds under the existing policy; a pending claim does not itself create a hold or unlimited retention.
- Keep claim/binding metadata independent of image availability under its applicable lifecycle. Image expiry does not resolve a claim, transfer a binding or invalidate an incumbent's rights. Missing required evidence must remain visible to the later review workflow rather than imply approval.

The existing [automatic deletion deferral](2026-11-06-user-media-cleanup-deferral.md) remains in force. Expired ordinary-read denial is not evidence that provider bytes were deleted. This decision neither implements deletion nor broadens ordinary access to preserved evidence.

## Backlog: VEH-CLAIM-HOLD-01

This is a repository backlog identifier, **not a created Jira issue**.

- **Status:** DEFERRED — claim-specific preservation design and implementation pending.
- **Owner:** UserService / UC-VEH-02 review workflow; named assignee and delivery date unassigned.
- **Trigger:** Review requires claim-image evidence beyond its normal deadline.
- **Scope:** Define explicit preservation requests, authorized actors and case scope, asset-owner acknowledgement, review/release rules and restricted access to preserved evidence. Reuse existing hold policy; do not invent an automatic or indefinite hold for every pending claim.
- **Dependencies:** Claim-photo association, adjudication authorization, existing media-hold model, and deletion coordination under USER-MEDIA-CLEANUP-01.
- **Acceptance required:** Prove unauthorized requests cannot preserve or reveal evidence; preservation is not claimed before owner acknowledgement; hold acknowledgement serializes with deletion; retries are idempotent; releasing one hold preserves others; final release re-evaluates the original deadline; claim status and incumbent rights are independent of image expiry/deletion. Apply existing 30-day hold review and case-closure review policy; overdue review is not automatic release.
- **Failure boundary:** Already unavailable/deleted evidence cannot be represented as recovered or preserved. Record the actual evidence state and leave evidence-dependent decisions pending.

## Not approved by this decision

No OAuth provider, service credential strategy, service-token lifetime, certificate infrastructure or team-wide authentication migration has been selected. The user requested an explanation of those alternatives, not implementation. No reviewer endpoint, hold endpoint, automatic Support case, holder notification or adjudication implementation is introduced here.

**Subsequent clarification:** option 2 is now retained as a future/refactoring direction under [SERVICE-AUTH-01](2026-11-07-service-authentication-deferral.md). Its implementation is deferred; provider details remain deferred. The user subsequently selected the existing shared service key for temporary binding lookup protection (8 November). This does not change the claim-photo decision.

## Canonical references

- [Vehicle requirements](../../01-requirements/srs/modules/01-identity-access.md#fr-veh-02)
- [Media requirements](../../01-requirements/srs/modules/09-file-media.md)
- [Dispute review use case](../../01-requirements/use-cases/uc-veh-02.md)
- [Vehicle API design](../../03-design/api/vehicle-registration-api-design.md)
- [User image API](../../03-design/api/user-images-api-design.md)
- [Deferred and implementation register](deferred-and-implementation-register.md)
