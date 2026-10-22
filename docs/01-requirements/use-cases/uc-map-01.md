# UC-MAP-01 — Use 2D/3D lot and recorded vehicle location

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Owner

[Use-case index](README.md) · [SRS module](../srs/modules/03-discovery-visualization.md)

## Goal

Use 2D/3D lot and recorded vehicle location.

## Preconditions

Current authorized facility/session view.

## Main flow

1. From selected MapTiler lot details, use the internal-model button with lot/category/interval context; render supported versioned outdoor/building/basement layout on its separate page.
2. Display backend physical/protection states separately and requested versus allocated resource.
3. Show vehicle location only to recorded precision.
4. On disconnect mark stale; reconnect reloads authorized current state.

## Alternatives and failures

Frontend never grants allocation or clears occupancy; no invented slot from zone-only evidence. Revalidate reservation through the backend; model selection does not reserve. Geographic directions remain external only.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MAP-02, FR-MAP-03, FR-MAP-04  
**US:** US-D16, US-O03  
**Rules:** BR-EMERG-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
