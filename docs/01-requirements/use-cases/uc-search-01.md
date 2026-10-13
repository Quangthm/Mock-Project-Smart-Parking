# UC-SEARCH-01 — Discover parking and directions

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver

[Use-case index](README.md) · [SRS module](../srs/modules/03-discovery-visualization.md)

## Goal

Discover parking and directions.

## Preconditions

Location selected or permission requested.

## Main flow

1. Use GPS/network where available or accept manual input.
2. Search compatible lots/time within 5 km by Haversine distance; allow expansion.
3. Show backend availability, prices, address, distance/travel estimate and freshness; full lots remain labelled.
4. Driver selects result and reviews fresh reservation terms.

## Alternatives and failures

Empty results, stale availability and denied GPS are distinct; no model ranking or guaranteed capacity from a search result.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-CAP-01, FR-CAP-02, FR-MAP-01  
**US:** US-D03, US-D16, US-O03  
**Rules:** BR-CAP-01, BR-AI-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
