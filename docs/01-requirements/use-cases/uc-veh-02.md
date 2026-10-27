# UC-VEH-02 — Review disputed plate binding

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Admin

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Review disputed plate binding.

## Preconditions

Conflicting normalized plate binding; authorized support case.

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
