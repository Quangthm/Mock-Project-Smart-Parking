# UC-NOT-01 — Deliver reminders and status notifications

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Notification service, authorized recipient

[Use-case index](README.md) · [SRS module](../srs/modules/08-notifications-reporting.md)

## Goal

Deliver reminders and status notifications.

## Preconditions

Authoritative business event and supported channel/preference.

## Main flow

1. Record deduplicated recipient/event/template/version delivery work.
2. Deliver in declared in-app/email-capture/SMS-fixture mode.
3. Distinguish queued/pending/failed/delivered and retry logical delivery without changing the committed result or recreating account/application/site. Email Partner approval/rejection with safe reason; modified-document approval uses the required notification and distinguishes review approval from provisioning readiness.
4. Send monthly reminders at 7 and 1 day; refresh authorized bell after reconnect.

## Alternatives and failures

Delivery failure creates no debt, renewal, cancellation or reversal. Other unapproved reminder offsets remain unseeded.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-06, FR-PAY-08, FR-RPT-01  
**US:** US-D11, US-OW02  
**Rules:** BR-PAY-02, BR-FAIL-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
