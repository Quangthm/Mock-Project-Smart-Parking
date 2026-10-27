# UC-RPT-02 — Audit actions and apply data retention

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin, scoped case handlers, data-owning services

[Use-case index](README.md) · [SRS module](../srs/modules/08-notifications-reporting.md)

## Goal

Audit actions and apply data retention.

## Preconditions

Explicit audit/investigation or originating case scope.

## Main flow

1. Read immutable audit projection with actor/time/lot/action filters.
2. Apply type-specific retention and request/acknowledge scoped holds with owner.
3. Review holds every 30 days and case closure; release only originating hold.
4. Delete due unheld assets/copies with provider evidence and durable retries; record unresolved outcome.

## Alternatives and failures

Admin has no blanket business-evidence access. Administrative chat reads do not reset 90-day last-message clock. Twenty-four-hour deletion target cannot extend hard deadline.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-RPT-03, FR-RPT-04, FR-RPT-05  
**US:** US-A03, US-A05  
**Rules:** BR-PRIV-01, BR-PRIV-02, BR-PRIV-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
