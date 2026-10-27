# UC-GATE-02 — Check out and resolve identity/ticket exceptions

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Driver

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Check out and resolve identity/ticket exceptions.

## Preconditions

Open visit and current identity/evidence.

## Main flow

1. Resolve ticket/session/vehicle identity; use scoped entry-evidence lookup for lost ticket.
2. Accept verified checkout and freeze cutoff; compute booked minimum/coverage/extras/prepayment credit.
3. Collect remaining charge or recognize covered balance; automatic surplus refund proceeds separately.
4. On commercial settlement authorize ordinary gate opening; record actual departure separately and release occupancy once.

## Alternatives and failures

Mismatch stops automated checkout without theft/fine inference. Unpaid recovery requires audit and reconciled money, retains same session/no new grace. Paid session cannot reopen; system mispricing corrects at original cutoff. Lost ticket adds no unapproved fee.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-GATE-05, FR-GATE-06, FR-GATE-07, FR-GATE-08, FR-PAY-02, FR-PAY-09, FR-PAY-11  
**US:** US-D02, US-D04, US-O02, US-O05, US-OW02  
**Rules:** BR-GATE-02, BR-GATE-03, BR-GATE-04, BR-PAY-05, BR-PAY-06, M-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
