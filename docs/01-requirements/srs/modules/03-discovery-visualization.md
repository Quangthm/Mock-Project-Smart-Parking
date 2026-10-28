# Discovery Visualization — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

Use MapTiler for geographic discovery, separate from the internal 2D/3D lot page. Search from consented current GPS/network location or a manual origin, with 5 km default radius and offered expansion when empty. Filter vehicle category independently from outdoor/basement/multi-storey lot type; support distance, price and availability filters. Haversine orders straight-line distance, never road distance or ETA. Operations supplies authoritative discovery/availability using Facility identity/location/configuration and Payment tariffs; frontend/local cached markers are not authority.

Without selected date/time, use current backend time and current availability for the chosen vehicle category; show the current applicable tariff and billing unit, without invented duration or total. Do not compare unlike hourly/day/night raw units as one normalized total-price filter. With a selected interval, use that vehicle/interval's availability and estimated total charge for price filtering/comparison. Expose source/freshness; full lots remain labelled unless intentionally excluded by availability filtering.

Selecting a lot shows important details and an explicit button to open its internal 2D/3D model page for booking, preserving lot/category/interval context. Selection, marker count or displayed slot never reserves capacity; the backend revalidates booking. Allow external-map directions only: embedded routing and turn-by-turn are excluded. Display travel estimates only with an actual identified source; otherwise omit and defer them. Distinguish denied GPS, empty results, provider failure and stale/disconnected backend data.

Owner configures lot/floor/zone/row/space identifiers, access paths, compatible vehicle categories, category-specific pools and map associations. Support outdoor, multi-floor building and multi-floor basement templates; outdoor has no artificial floor. Car and motorcycle capacities are not interchangeable. Fixed 3D templates are deployment assets. No automatic mixed-category area conversion or EV charging in MVP.

2D/3D displays backend state, requested versus allocated location and only recorded precision. Physical state and Reservation / Protection State remain independent display channels. A slot may therefore appear as `OCCUPIED + RESERVED` or `OCCUPIED + PROTECTED`; this combination does not prove whether the current occupant is the reservation holder. The relation is shown only when an authorized Operations projection establishes it, and another Driver's identity is not exposed merely to explain that the occupants differ. For selected future intervals, the view may show current/projected occupation under Occupancy Validity Timespan together with a reservation/protection overlay. Show stale/disconnected status and reload authorized state after reconnect. A displayed slot creates no guarantee. Driver interface supports discovery/reservation/own visit/payment/help; Operator shows approaching/waiting vehicles, open sessions, allocation and emergencies; Owner shows configuration, staff and reports; Admin shows approvals, integration/security and audit. Role visibility grants no extra command permission.

## Functional requirements

### FR-CAP-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D03

Use MapTiler for geographic discovery, separate from the internal 2D/3D lot page. Search from consented current GPS/network location or a manual origin, with 5 km default radius and offered expansion when empty. Filter vehicle category independently from outdoor/basement/multi-storey lot type; support distance, price and availability filters. Haversine orders straight-line distance, never road distance or ETA. Operations supplies authoritative discovery/availability using Facility identity/location/configuration and Payment tariffs; frontend/local cached markers are not authority.

Without selected date/time, use current backend time and current availability for the chosen vehicle category; show the current applicable tariff and billing unit, without invented duration or total. Do not compare unlike hourly/day/night raw units as one normalized total-price filter. With a selected interval, use that vehicle/interval's availability and estimated total charge for price filtering/comparison. Expose source/freshness; full lots remain labelled unless intentionally excluded by availability filtering.

Selecting a lot shows important details and an explicit button to open its internal 2D/3D model page for booking, preserving lot/category/interval context. Selection, marker count or displayed slot never reserves capacity; the backend revalidates booking. Allow external-map directions only: embedded routing and turn-by-turn are excluded. Display travel estimates only with an actual identified source; otherwise omit and defer them. Distinguish denied GPS, empty results, provider failure and stale/disconnected backend data.

### FR-MAP-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Map Service  
**Stories:** US-D16

Use MapTiler for geographic discovery, separate from the internal 2D/3D lot page. Search from consented current GPS/network location or a manual origin, with 5 km default radius and offered expansion when empty. Filter vehicle category independently from outdoor/basement/multi-storey lot type; support distance, price and availability filters. Haversine orders straight-line distance, never road distance or ETA. Operations supplies authoritative discovery/availability using Facility identity/location/configuration and Payment tariffs; frontend/local cached markers are not authority.

Without selected date/time, use current backend time and current availability for the chosen vehicle category; show the current applicable tariff and billing unit, without invented duration or total. Do not compare unlike hourly/day/night raw units as one normalized total-price filter. With a selected interval, use that vehicle/interval's availability and estimated total charge for price filtering/comparison. Expose source/freshness; full lots remain labelled unless intentionally excluded by availability filtering.

Selecting a lot shows important details and an explicit button to open its internal 2D/3D model page for booking, preserving lot/category/interval context. Selection, marker count or displayed slot never reserves capacity; the backend revalidates booking. Allow external-map directions only: embedded routing and turn-by-turn are excluded. Display travel estimates only with an actual identified source; otherwise omit and defer them. Distinguish denied GPS, empty results, provider failure and stale/disconnected backend data.

### FR-MAP-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D16, US-O03

Open the internal 2D/3D lot page from the selected MapTiler lot detail, preserving identity/category/interval. 2D/3D state visualization and backend authority follow the domain contract; show requested and actual allocated positions distinctly. Render physical state independently from the mutually exclusive Reservation / Protection State projection. `OCCUPIED` never identifies the reservation claimant by itself: `OCCUPIED + RESERVED/PROTECTED` may involve the same Driver or different Drivers. Only authorized Operations session/allocation/reservation linkage may establish that relationship; do not expose another Driver's identity to ordinary viewers. Booking revalidates backend rights/capacity; model selection alone commits nothing.

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


Design/implementation owners and exit criteria: [V11-DES-08](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
