# UC-VEH-02 — Review disputed plate binding

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Admin

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Review disputed plate binding.

## Preconditions

Conflicting normalized plate binding; authorized support case.

## Registration intake before adjudication

The [7 November SPARK-243 decision](../../05-project-management/decision-log/2026-11-07-vehicle-claim-images.md) approves associating the claimant's uploaded vehicle photo with their pending registration submission now. This intake does not itself open an authorized Support case, perform adjudication or grant an active vehicle binding. The claimant can access their own submission; explicitly authorized reviewers receive the evidence access needed for their scoped review when adjudication is implemented. Other Drivers and unrelated reviewers receive no access.

Claim photos follow the existing admission + 30-day attached-image deadline; retries and review do not reset it. Missing/expired evidence does not resolve the claim or authorize transfer. Claim-specific preservation beyond the deadline is deferred under VEH-CLAIM-HOLD-01; pending status alone never creates an indefinite hold. Existing acknowledged holds remain protected under the media policy. Implementation and acceptance evidence remain pending.

## Main flow

1. Driver supplies verified contact and relevant registration/transfer evidence.
2. Operator supplies scoped local findings; notify existing holder.
3. Admin assesses evidence sufficiency and records a reasoned decision.
4. User & Access commits approved binding change with version guard and preserved history. Soft removal under UC-VEH-01 never substitutes for reviewed transfer or invalidates active dependencies.

## Alternatives and failures

Missing evidence or unresolved ownership stays pending/escalated. Nonresponse alone never transfers binding; no routine CCCD/GPLX collection.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-VEH-02  
**US:** US-A04, US-D09  
**Rules:** BR-VEH-02

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
