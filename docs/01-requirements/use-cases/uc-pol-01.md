# UC-POL-01 — Configure platform, lot and pricing policies

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Admin

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Configure platform, lot and pricing policies.

## Preconditions

Authorized policy owner/scope; domain catalogue entry exists.

## Main flow

1. Create draft typed values under approved default/bounds and explicit choices.
2. Reject missing required tariff values, invalid units/bands or unapproved scalar overrides.
3. Review/schedule activation and retain immutable revision.
4. Resolve backend terms for new transactions; accepted transactions keep their snapshots.

## Alternatives and failures

No demo value becomes default; fixed deposit may exceed charge, but provider representation/limits require validation; zero-price covered branch overrides deposit mode.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-05, FR-CAP-07, FR-PAY-01, FR-PAY-10, FR-POL-01, FR-POL-02, FR-POL-03, FR-POL-04, FR-POL-05, FR-POL-07, FR-POL-08  
**US:** US-A02, US-D02, US-O05, US-OW02  
**Rules:** BR-POL-01, BR-POL-02, BR-POL-03, BR-CAP-04, BR-PAY-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
