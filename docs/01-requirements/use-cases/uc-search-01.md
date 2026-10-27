# UC-SEARCH-01 — Discover parking and directions

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver

[Use-case index](README.md) · [SRS module](../srs/modules/03-discovery-visualization.md)

## Goal

Discover parking and directions.

## Preconditions

Location selected or permission requested.

## Main flow

1. Choose consented current GPS/network location or manual origin on MapTiler; default 5 km radius and offer expansion if empty.
2. Filter vehicle category separately from outdoor/basement/multi-storey lot type; filter distance, price and availability, with Haversine straight-line ordering.
3. Without a selected interval use current backend time, category availability and current tariff labelled with billing unit. Invent no duration/total and do not compare unlike raw tariff units as one total-price filter.
4. With a selected interval show its category availability and estimated total for price comparison/filtering. Operations discovery composes authoritative Facility data and Payment tariffs; expose sources/freshness and label full lots unless deliberately filtered.
5. Select lot details and use an explicit button to open that lot's internal 2D/3D page with category/interval context; revalidate backend booking capacity/terms.
6. Optionally open external-map directions. Embedded routing/turn-by-turn is excluded; show travel estimate only with an actual source, otherwise omit/defer it. Haversine is not road distance or ETA.

## Alternatives and failures

Denied GPS, empty results, provider failure and stale/disconnected availability are distinct. Neither cached markers nor internal model selection reserves/guarantees capacity; backend reconfirms.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-CAP-01, FR-CAP-02, FR-MAP-01  
**US:** US-D03, US-D16, US-O03  
**Rules:** BR-CAP-01, BR-AI-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
