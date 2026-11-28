# 28 November 2026 MVP decision amendment

**Status:** Product Owner approved. Requirements and architecture direction. Implementation and acceptance evidence are recorded separately in the [service-owner handoff](../handoffs/2026-11-28-mvp-service-owner-handoff.md). This amendment supersedes conflicting clauses in earlier decisions. Preserve earlier records and unrelated approved behavior. In particular, supersede DEC-OPR-03 granular grants, DB13's permission catalogue, the older Admin action-scope wording where it is narrower, and SPARK-335's post-payment-only eligibility. The 4 November freshness policy and SEC-02 recovery security rules retain their authority.

## A. Fixed roles and resource scope

Each Account has exactly one fixed role established through the approved registration, provisioning or bootstrap flow. Authorize the authenticated Account for that role's documented responsibilities, subject to User and Account eligibility, session rules, tenant isolation and the resource owner's business-state and scope checks. Drivers act on their authorized own records, Owners on their organizations and facilities, and Operators on assigned facilities. Ownership confers no Operator-only authority. Admin authority is limited to designated platform responsibilities. Account overrides, action flags, configurable permission editors and Admin role management are outside MVP. Preserve actual ownership and site assignment relationships.

Retain the [authorization freshness ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) and [operation matrix](../../01-requirements/srs/authorization-operation-classification.md). TOKEN has the existing 15-minute stale-claim window, the six REQUEST families use Account-current authorization, and UserService remains locally authoritative for its security operations.

## B. Admin access restrictions

| Scope | Supported actions | Effect |
|---|---|---|
| User | Lock, unlock, disable, re-enable | Apply or remove the named restriction for the User. A new lock or disable revokes sessions across the User's applicable Accounts. |
| Account | Disable, re-enable | Apply or remove the named restriction on the selected Account. Disable revokes only that Account's sessions. |

Require the current Admin Account and session, exact target scope, administrative reason and a valid state transition. Protect the sole seeded Admin from administrative lock, disable, deletion and authority loss. Ordinary own security operations and authentication lockout still apply. Unlock and re-enable remove only the selected restriction. Contact verification, Owner review and provisioning, MFA and other independent restrictions remain in force. Revoked sessions stay revoked, removed assignments stay removed, and existing business records remain intact.

Commit the security change, applicable session revocation and durable audit evidence in the security transaction. After commit, best-effort `/hubs/access` `AccessRevoked` targets all connections for a User restriction or only the selected Account's connections for an Account restriction. The affected frontend clears authentication state and protected caches. Notification & Audit projects durable audit evidence independently. Browser acknowledgement and audit projection are unnecessary for enforcement of the committed restriction. TOKEN freshness continues to apply.

## C. Appeals and checkout

Financial appeals require an authoritative recorded fine, charge or transaction. Eligible targets are paid parking charges, fines paid in completed checkout and officially issued fines on an ongoing parking session, including unpaid fines. General unpaid parking fees, active sessions without an existing disputed fine, hypothetical or unconfirmed violations or fines, and requests for an existing facility-failure explanation are excluded. Existing Driver ownership and staff scope apply. Other / General support remains a distinct asynchronous workflow. A financial reference is optional there and a generic ticket supplies no refund authority.

| Decision | Approved rule |
|---|---|
| C01 | A pending appeal leaves an issued fine payable until an authorized decision changes the obligation. |
| C02 | A pending appeal does not block normal checkout, payment or departure. Existing settlement, physical departure and emergency recovery rules apply. |
| C03 | Operations durably freezes the applicable parking obligation and fine amounts for the checkout attempt before requesting preparation from Payment. Order ID and QR availability are independent of this cutoff. The appeal decision commit and cutoff have a definitive order. |
| C04 | Preserve the prepared attempt, Payment order amount and original financial history. Appeal acceptance alone triggers no cancellation, replacement or regeneration of an order. |
| C05 | Acceptance committed before the cutoff may correct or waive the disputed unpaid fine. The subsequent preparation uses the corrected obligation. Uncollected money requires no refund. |
| C06 | Acceptance after the cutoff can commit independently with an outstanding adjustment, including while preparation or payment is pending. Verified collection allows an authorized eligible refund request. Definitive unpaid termination applies the correction to the next valid preparation. Pending or unknown settlement retains the adjustment. |
| C07 | Timeout, expired QR, lost reply and missing local order reference are insufficient to establish unpaid termination. Payment owns verified settlement and reconciliation of late, unexpected and duplicate collections. Unknown settlement creates neither a speculative refund nor a conflicting order. |
| C08 | Operations owns the officially issued fine obligation with a durable identity linked to the parking session and authoritative violation decision. A suspected violation or unreviewed recognition result is insufficient. Assess reuse of existing violation records during future design. Payment owns financial records and execution. |
| C09 | Preserve violation, fine, payment, invoice and appeal decision history. Replays return the existing outcome without duplicate adjustments, refunds or reconciliation effects. ACCEPTED and REFUNDED are separate. Payment validates eligible collected balance, accounting for deposits, credits, adjustments, previous refunds and reserved balances under BR-REF-01. |
| C10 | Monthly or yearly parking fee coverage leaves an independently issued fine obligation in force. |

| State at acceptance | Required outcome |
|---|---|
| Before the Operations cutoff | Apply authorized correction or waiver. |
| Preparation in flight | Preserve frozen amounts and record the outstanding adjustment. |
| Existing unpaid Payment order | Preserve its amount. Apply the pending or definitive outcome rule using Payment evidence. |
| PENDING | Await Payment's authoritative outcome while retaining the accepted decision and outstanding adjustment. |
| UNKNOWN | Retain the adjustment without speculative refund or conflicting order. |
| Verified paid | Initiate the authorized eligible refund request. |
| Definitively terminated unpaid | Apply correction at the next valid preparation. |
| Adjustment already executed | Return the existing outcome. |
| Late or unexpected collection | Use existing Payment reconciliation. |

The implementation design for the cutoff, fine identity, attribution and adjustment persistence remains open. My Parking Session, registered walk-in Account association, ownership transfer and visibility, retrospective session claiming, and new ownership predicates for appeals or financial history are deferred. Use the existing ownership boundary.

## D. Facility-failure visibility

Expose an affected reservation's accepted outcome and recorded reason through existing authorized details, history or supported notification projections. Facility owns structural and service-availability facts, Operations owns the accepted reservation outcome and reason, and Payment owns financial outcomes. The Driver can inspect those facts without submitting an appeal or support ticket. Existing facility-failure cancellation and compensation rights remain in force.

## E. Admin-assisted account recovery

UC-AUTH-06 remains **CONFIRMED MVP**. Implementation is **INCOMPLETE**, presentation delivery is **DEFERRED until after the final presentation**, and acceptance is **NOT RUN**. SEC-02 evidence and security requirements remain in force. Self-service authentication, MFA and backup-code recovery retain their separate scope.

## Traceability

See the [requirement and acceptance mapping](../../01-requirements/requirements-traceability/v1.1-decision-coverage.md#28-november-2026-mvp-amendment) and the [implementation and Jira handoff](../handoffs/2026-11-28-mvp-service-owner-handoff.md).
