# Discovery Visualization — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

Search using consented GPS, network positioning or manual location; default radius 5 km with expansion when empty. Haversine distance provides deterministic distance ordering; expose all options, vehicle/time filters, backend availability, name/address, distance, ordinary day/night prices and travel estimates with source/freshness. Full lots are labelled. Map provider connectivity is tested separately from deterministic coordinates.

Owner configures lot/floor/zone/row/space identifiers, access paths, compatible vehicle categories, category-specific pools and map associations. Support outdoor, multi-floor building and multi-floor basement templates; outdoor has no artificial floor. Car and motorcycle capacities are not interchangeable. Fixed 3D templates are deployment assets. No automatic mixed-category area conversion or EV charging in MVP.

2D/3D displays backend state, requested versus allocated location and only recorded precision. Show stale/disconnected status and reload authorized state after reconnect. A displayed slot creates no guarantee. Driver interface supports discovery/reservation/own visit/payment/help; Operator shows approaching/waiting vehicles, open sessions, allocation and emergencies; Owner shows configuration, staff and reports; Admin shows approvals, integration/security and audit. Role visibility grants no extra command permission.

## Functional requirements

### FR-CAP-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D03

Search using consented GPS, network positioning or manual location; default radius 5 km with expansion when empty. Haversine distance provides deterministic distance ordering; expose all options, vehicle/time filters, backend availability, name/address, distance, ordinary day/night prices and travel estimates with source/freshness. Full lots are labelled. Map provider connectivity is tested separately from deterministic coordinates.

### FR-MAP-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Map Service  
**Stories:** US-D16

Search using consented GPS, network positioning or manual location; default radius 5 km with expansion when empty. Haversine distance provides deterministic distance ordering; expose all options, vehicle/time filters, backend availability, name/address, distance, ordinary day/night prices and travel estimates with source/freshness. Full lots are labelled. Map provider connectivity is tested separately from deterministic coordinates.

### FR-MAP-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D16, US-O03

2D/3D state visualization and backend authority follow the domain contract; show requested and actual allocated positions distinctly.

### FR-MAP-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D16

Show vehicle location only to the precision actually recorded for its session; do not invent a slot where only a zone is known.

### FR-MAP-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D16, US-O03

Refresh views from backend-confirmed events; mark stale/disconnected state and reload current state after reconnect; latency targets remain the domain contract.
