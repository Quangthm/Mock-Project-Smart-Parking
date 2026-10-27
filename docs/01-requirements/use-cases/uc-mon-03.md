# UC-MON-03 — Renew, expire or repurchase a monthly/yearly pass

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operations, Payment

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Renew, expire or repurchase a monthly/yearly pass.

## Preconditions

Existing paid periods or expired prior pass.

## Main flow

1. For monthly renewal, open according to effective `MONTHLY_RENEWAL_WINDOW` (default 7 days) before final committed E and append from that E with original anchor. Yearly renewal policy and calendar-boundary details are separately configurable/design-pending; no monthly default is silently inherited.
2. Verify full settlement and strict deadline; no automatic charge or next-period debt.
3. Send monthly expiry reminders using effective `MONTHLY_EXPIRY_NOTICE` (default schedule 7 days and 1 day before E), and yearly reminders only under separately agreed settings; at E stop coverage without clearing occupancy.
4. After expiry repurchase with new non-retroactive start/anchor; while parked retain same physical session.

## Alternatives and failures

Jan31→Feb28/29→Mar31; no fixed 30-day month. Late money/UNKNOWN follows reconciliation, not backdated coverage.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MON-09, FR-MON-10, FR-MON-14  
**US:** US-D14, US-O07  
**Rules:** M-01, M-02, M-03, M-04, M-08

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
