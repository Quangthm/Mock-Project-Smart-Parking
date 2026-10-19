# UC-GATE-01 — Check in a reserved, walk-in or monthly vehicle

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Driver

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Check in a reserved, walk-in or monthly vehicle.

## Preconditions

Gate arrival/entry requested; current operational scope.

## Main flow

1. Verify arrival/identity and ticket/QR/manual evidence separately from actual entry.
2. Validate reservation/monthly rights or account-free walk-in and actual compatible space.
3. Authorize entry only when physically feasible; early entry uses disclosed extra terms.
4. Record actual entry/session and occupancy once; link claim without double deduction.

## Alternatives and failures

Scan/open alone creates no occupied session. Duplicate session, mismatch or unknown physical resource blocks automation; uncertain LPR uses manual verification. Monthly pass never guarantees full-lot admission.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-CAP-05, FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09, FR-RES-07, FR-RES-11  
**US:** US-D01, US-O01, US-O02, US-O08  
**Rules:** BR-GATE-01, BR-GATE-02, BR-RES-04, BR-CAP-05, M-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
