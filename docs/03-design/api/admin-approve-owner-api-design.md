# Admin review of the initial Partner application API design

SPARK-196 / SPARK-195 / SPARK-197 / SPARK-202 / SPARK-178 · SPARK-296 update, 21 October 2026. **v1.1 documentation review candidate based on approved decisions**; new wire details and implementation pending.

Canonical behavior: [identity SRS](../../01-requirements/srs/modules/01-identity-access.md), [UC-AUTH-02](../../01-requirements/use-cases/uc-auth-02.md), [media contract](../../02-architecture/service-boundaries/media-contract.md), [service ownership](../../02-architecture/service-boundaries/ownership-and-events.md), [shared API contract](../../02-architecture/api/v1.1-contracts.md).

## Actor and endpoint contract

All review/read/replace operations require current Admin access and `ACCOUNT_ADMIN`; actor identity comes from validated authentication, never request fields. MVP has one protected seeded Admin. Review of a Partner application does not alter that safeguard.

| Operation | Method/path | Contract |
|---|---|---|
| List initial applications | `GET /api/owner-applications` | Existing newest-first application list; safe company/contact, submitted first-lot setup, verification/simulation progress, decision/provisioning and delivery metadata. No credential/OTP content. |
| Inspect initial application | `GET /api/owner-applications/{id}` | Authorized application plus protected agreement version references/history and current review version. |
| Replace uploaded policy/agreement | Path/method design-pending V11-DES-01 | Initial application must still be pending. Upload/finalize replacement, preserve original and every replacement plus actor/time/reason. No other application fields are editable. |
| Review | `PATCH /api/owner-applications/{id}/review` | Existing `status: approved|rejected`, `reviewNote` (existing max 2000); rejection requires a reason. Bind reviewed application/agreement versions using agreed concurrency token (V11-DES-01). |

The two final outcomes are **approved** and **rejected**. No third decision, request-corrections or in-app applicant re-acceptance flow exists. External discussion is not evidence of an in-app consent action. Document replacement does not apply to pending additional-lot applications. Approved/rejected applications and their reviewed versions are immutable.

Illustrative existing decision fields, **not the complete concurrency-safe v1.1 request**:

```json
{
  "status": "rejected",
  "reviewNote": "<safe recorded rejection reason>"
}
```

V11-DES-01 must agree a review-version precondition and wire field/header before integration. A stale review or concurrent replacement cannot silently approve an unseen version; reject the stale attempt without decision/provisioning mutation. A successful review response contains the committed review result and separate provisioning/delivery state. It cannot report successful provisioning merely because the review committed.

## Approval and document guards

Approval requires saved first-lot setup, verified email, successful mandatory consented VNeID simulation, finalized required agreement assets and eligible pending application. Phone is required input but not approval verification; phone OTP login needs separate verified-phone evidence. No government identity or business-licence verification claim is inferred from simulated data.

User & Access owns application/version/review metadata and agreement lifecycle. Upload and restricted reads use the [media owner model](../../02-architecture/service-boundaries/media-contract.md), opaque asset/version references, content validation, pending/finalized reconciliation, acknowledged holds and policy retention. Admin may replace only uploaded policy/agreement documents before final review, with reason/actor/time. V11-DES-01 owns upload formats/limits, protected read route and retention-class agreement; no arbitrary document URL field or new retention number is approved.

When approved after a replacement, send **“Your application was approved with some modifications”**. This notification accompanies ordinary approval and identifies the approved evidence version as designed; it does not reopen the application or require re-acceptance. For rejection, retain reason/history, send a safe email explanation and safe authorized login failure. The same rejected initial email cannot register again; re-enable cannot reverse rejection (D09).

## Review, provisioning and opening sequence

```mermaid
sequenceDiagram
    actor Admin
    participant User as User and Access
    participant Facility
    participant Domains as Operations / Payment / IoT
    participant Notify as Notification delivery
    Admin->>User: Inspect proposal and agreement versions
    Admin->>User: Review with expected version and decision
    User->>User: Guard prerequisites/version; commit final decision and durable work
    User-->>Admin: Review result; provisioning and delivery separately pending
    User->>Notify: Queue approval or rejection email
    opt Approved initial application
        User->>Facility: Provision same first lot using stable operation identity
        User->>Domains: Coordinate service-owned submitted configuration
        alt Successful provisioning
            Facility-->>User: Durable successful result / site identity
            User->>User: Allow normal Owner login if other security gates pass
        else Failed or outcome unknown
            Facility-->>User: Pending/failed result; retry/reconcile same identity
            User->>User: Keep normal login blocked
        end
        Note over User,Domains: User coordinates the workflow; exact orchestration/recovery contract is design-pending
        Note over Facility,Domains: Activation authorizes the lot; opening requires separate readiness checks
    end
```

User owns identity, organization, membership and review. Facility owns lot configuration; Operations owns accepted occupancy/claims and plan/coverage; Payment owns tariffs/financial terms; IoT owns integrations/raw observations. An application proposal is evidence, not operational authority. No Domain-layer HTTP orchestration, cross-service DB writes or legacy Parking tenant authority is approved.

Retries reuse the same application/operation/provisioned site identity and must detect changed payload/scope. An unknown remote result is reconciled before retrying creation. A provisioning failure is never rejection; it remains visible and blocks initial Owner login until resolved. Preserve existing independent locks/disablement. Operational opening follows the Facility/Operations readiness and permission contract in [facility design](parking-structure-api-design.md).

## Errors, concurrency and delivery

Retain safe unauthorized/forbidden, `APPLICATION_NOT_FOUND`, `APPLICATION_CLOSED`, `CONTACT_UNVERIFIED`, validation and concurrency outcomes. New setup/simulation/document-version/provisioning result schemas are V11-DES-03/V11-DES-01/V11-DES-04; do not conflate dependency failure with a failed or rolled-back review. Concurrent review/replacement is serialized or conditionally committed against the inspected version; only one final decision exists. Notification failures preserve committed business facts and retry delivery without repeating review/provisioning (D10).

## Retained decision references

| Existing reference | v1.1 disposition |
|---|---|
| DEC-APP-01 | Contact verification, simulation/setup, review and provisioning are separate gates. |
| DEC-APP-02 | Public email domains remain permitted. |
| DEC-APP-03 | Real national-ID collection remains excluded; simulated information-sharing and agreement upload are explicit scope. |
| DEC-APP-04 | Stable application/provisioning linkage is required; exact identifiers are V11-DES-04. User organization authority replaces legacy Parking tenant assumption. |
| DEC-APP-05 | Manual rejection with recorded reason; no same-email initial re-registration. Additional-lot Draft-as-new follows its separate contract. |

Pending work: V11-DES-01/V11-DES-04 and V11-DES-04 for separate additional-lot review. [Acceptance catalogue](../../04-testing/requirements-traceability/acceptance-scenarios.md): new v1.1 scenarios NOT RUN; no implementation or OpenAPI completion is claimed.
