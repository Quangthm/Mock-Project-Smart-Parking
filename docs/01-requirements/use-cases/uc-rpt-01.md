# UC-RPT-01 — Monitor lot operations and Owner reports

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Owner

[Use-case index](README.md) · [SRS module](../srs/modules/08-notifications-reporting.md)

## Goal

Monitor lot operations and Owner reports.

## Preconditions

Assigned/owned-lot scope and current grants.

## Main flow

1. Read Operations occupancy/visits/reservations and relevant near-term work.
2. Compose Payment revenue/refund and Facility/IoT owned reports through authorized APIs.
3. Export only same scoped data; retain simulated-mode/freshness labels.

## Alternatives and failures

No unrelated lots/private conversations; future payment-method/geographic/retention/pattern analytics do not become active by dashboard design.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-07, FR-GATE-04, FR-RPT-02  
**US:** US-O03, US-OW04  
**Rules:** BR-AUTH-01, BR-PRIV-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
