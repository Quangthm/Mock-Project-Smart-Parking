# UC-VEH-01 — Register and maintain vehicles

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Register and maintain vehicles.

## Preconditions

Driver manages own vehicle records; Operator manages assigned-lot visit intake.

## Main flow

1. Store raw/normalized plate, vehicle category and image for plated vehicles.
2. Reject duplicate active binding without disclosing another account; validate compatible vehicle categories.
3. For supported non-plated walk-ins, create visit/session ticket without fabricated permanent plate.
4. Permit reasoned Operator recognition/session correction while retaining original evidence.

## Alternatives and failures

Binding conflict opens UC-VEH-02; operational plate correction cannot transfer account binding. EV charging/dimension automation is not implied.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-VEH-01, FR-VEH-03, FR-VEH-04  
**US:** US-D01, US-D09, US-O01  
**Rules:** BR-VEH-01, BR-VEH-03, BR-CAP-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
