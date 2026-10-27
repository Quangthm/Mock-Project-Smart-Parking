# Facility Capacity — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

**Capacity/layout:** support individually represented spaces and category-specific pools. A car unit and motorcycle unit are not interchangeable generic units. Specific-slot/zone/capacity reservation products remain unchanged; a capacity reservation may be allocated from individually represented spaces. Automatic mixed-category area conversion has not been selected for MVP.

Three 3D model/template types: outdoor lot, multi-floor building lot and multi-floor basement. Owner configures floors, zones and rows; outdoor lots have no artificial floor. Fixed model assets remain versioned deployment assets; facility configuration controls instances/layout within supported templates.

An occupied backup slot is deducted once (occupied takes precedence), while its backup designation/history remains. Example: 10 slots, 3 occupied, 2 backup of which 1 occupied → 4 distinct unavailable slots before other claims, leaving 6. Do not infer a physically occupied backup can satisfy another reservation.

Late tolerance is optional. When enabled, Owner selects 15–30 minutes, default 15. Disabled retains baseline reservation validity until end; no zero-minute value substitutes for disabled. Arrival at or before the cutoff is timely; no-show only after the cutoff without authoritative timely arrival. Alternative must meet mandatory vehicle/accessibility/explicitly promised facility constraints; exact preferred slot is not guaranteed. Offer expiry releases its temporary alternative claim, not automatically the underlying entitlement. Offer duration defaults to 5 minutes; acceptance/expiry concurrency remains design work.

Owner receives read-only diagnostics at owned lots; assigned Operator retains lot-device management. This amends deferred Owner reporting scope without enabling real IoT. Manual location selection is supported when GPS is denied/unavailable.

**Plate correction:** assigned Operator may correct recognition/session identification with reason, actor, original/corrected values and history. IoT & Recognition retains original recognition/evidence; Operations owns accepted visit identification. This does not automatically mutate global account/vehicle binding.

Duplicate global binding is rejected without exposing another account's personal information. Driver may submit a dispute; Operator verifies local incident and supplies findings. Admin adjudicates already-bound/ownership-change disputes with evidence through a minimal reviewed support case. N owns support-case coordination; U owns binding adjudication facts/guarded mutation; operational evidence stays with its owner. Historical visits/invoices/evidence keep original associations. Accepted evidence direction now includes verified contact and relevant vehicle-registration/transfer evidence in existing permitted image formats, with scoped Operator findings (the media contract). Notify the existing holder; never transfer automatically for nonresponse. Admin records evidence sufficiency and reasons; unresolved cases remain pending/escalated (the vehicle binding requirement). No routine national-ID collection is approved.

Facility owns structural/service changes; Operations owns accepted occupancy and availability. Planned reductions remain pending until recoverable Operations restriction/recovery commits; emergency physical closure is recorded immediately and admission stopped safely even if communication fails. Preserve occupied facts, affected rights and history; recover compatible capacity or apply approved failure handling, never displace occupants.

Physical facts AVAILABLE/OCCUPIED/UNKNOWN/MAINTENANCE/UNAVAILABLE are separate from reservation/protection NOT_RESERVED/RESERVED/PROTECTED/BACKUP. Maintenance or closure cannot erase accepted occupation. Unknown is excluded from automatic allocation until authoritative reconciliation. Fixed backup and demand-driven protection are distinguished in the claim ledger.

**Occupancy and reservation/protection identity clarification:** `OCCUPIED` describes physical occupation/projection, not who owns a reservation. The current occupant/session and the reservation/protection claimant may be the same Driver or different Drivers. Never infer identity equality from `OCCUPIED + RESERVED` or `OCCUPIED + PROTECTED`; authoritative Operations session/allocation/reservation linkage establishes whether a reservation has actually been fulfilled.

`RESERVED` and `PROTECTED` remain mutually exclusive values of the current Reservation / Protection State projection. This clarification does not require a split enum or database migration. An underlying reservation entitlement may remain active in Operations while the slot projection is `PROTECTED` during the protection behavior.

**Canonical different-driver example:** at 10:00, Slot S-12 is physically `OCCUPIED` by Driver A while Driver B has a future reservation associated with S-12 and its protection window is active. The slot may therefore be `OCCUPIED + PROTECTED`. Driver A's occupation does not fulfill Driver B's reservation. If Driver A departs before fulfillment, the slot can become `AVAILABLE + PROTECTED`; if Driver A remains when a usable resource is required, Operations uses compatible alternative/reallocation/conflict handling and never displaces Driver A.

**Canonical same-driver example:** when Driver B is admitted into the reservation's accepted allocation and Operations links the parking session to that reservation, `OCCUPIED + RESERVED` may represent the same Driver. The equality is proven by the linkage, not the state combination.

**Occupancy Validity Timespan:** this is a configurable policy; the current default is 4 hours. Runtime projection uses the resolved effective value. At an evaluation time of 10:00 with the 4-hour default effective, a currently accepted `OCCUPIED` slot is treated as occupied through the 14:00 projection horizon unless newer authoritative evidence changes it earlier. The projection supports future-capacity evaluation and may be displayed together with a future reservation/protection overlay. It is not a guarantee of actual departure time and its expiry never releases physical occupancy.


Combined onboarding retains service authority: User owns identity, organization/membership and review evidence; Facility owns structural configuration; Operations owns accepted occupancy/claims, discovery and plan definition/coverage; Payment owns tariffs/financial terms; IoT owns raw observations/integrations. A submitted proposal is evidence, not another service's operational source of truth.

## Functional requirements

### FR-LOT-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Prospective Owner, Owner, Admin (review)
**Stories:** US-OW01

Initial first-lot setup is saved within Partner Registration before email verification and combined Admin review (FR-AUTH-01/05). An approved Owner submits each additional lot as a separate application for Admin approval/rejection. Rejection leaves the Owner and other approved lots unaffected. Rejected applications remain immutable; Draft as new lot application creates a distinct proposal and review identity, including identical content. Submission/provisioning retries are idempotent; absence of content-deduplication does not permit duplicate applications/sites. Admin document replacement is confined to the initial pending Partner application.

Approval authorizes the lot; successful provisioning/activation and operational opening remain separate. Facility owns configuration/readiness; Operations validates accepted occupancy/claims and admission readiness, with required Payment terms before sales. Pending/failed/unknown provisioning is visible and recoverable without duplicate sites. Normal initial Owner login waits for successful provisioning. Open only after required configuration and operational readiness checks under existing permissions; approval never automatically opens for parking. Owner updates/deactivates owned facility profile, address, coordinates, supported categories and operating/open-state configuration while preserving accepted rights and actual occupation.

### FR-LOT-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW01

Owner configures lot/floor/zone/row/space identifiers, access paths, compatible vehicle categories, category-specific pools and map associations. Support outdoor, multi-floor building and multi-floor basement templates; outdoor has no artificial floor. Car and motorcycle capacities are not interchangeable. Fixed 3D templates are deployment assets. No automatic mixed-category area conversion or EV charging in MVP.

### FR-LOT-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW01

Owner configures lot/floor/zone/row/space identifiers, access paths, compatible vehicle categories, category-specific pools and map associations. Support outdoor, multi-floor building and multi-floor basement templates; outdoor has no artificial floor. Car and motorcycle capacities are not interchangeable. Fixed 3D templates are deployment assets. No automatic mixed-category area conversion or EV charging in MVP.

### FR-LOT-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator  
**Stories:** US-O05, US-OW01

Facility owns structural/service changes; Operations owns accepted occupancy and availability. Planned reductions remain pending until recoverable Operations restriction/recovery commits; emergency physical closure is recorded immediately and admission stopped safely even if communication fails. Preserve occupied facts, affected rights and history; recover compatible capacity or apply approved failure handling, never displace occupants.

Spatial deactivation never silently cascades to descendants. Explicitly select/handle affected floors/zones/rows/spaces and resolve operational dependencies before acceptance. FE supports multiselect. A batch is confined to one lot and is all-or-nothing: validate authorization, versions, descendants and dependencies for every item; return per-item errors and mutate none when any fails. Recheck dependencies at commit; planned restriction/recovery coordination and emergency physical-closure safeguards remain applicable.

### FR-LOT-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-O03, US-O05

Manage, link, enable and disable lot devices within the Operator's assigned parking-lot scope. Owner grants the Operator access through account provisioning.

### FR-LOT-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A02

Admin manages authorized platform integration configuration; lot device mutation remains assigned-Operator scope. IoT & Recognition owns device/raw-observation adapters, Payment owns payment adapters, each service owns its other adapters. Platform configuration grants no implicit lot operation, unrestricted evidence or secret-reading permission. Real hardware remains FUTURE.

### FR-LOT-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator, Device Gateway  
**Stories:** US-O03, US-OW05

Show connectivity, last observation and errors to assigned Operator and read-only owned-lot diagnostics to Owner. Clearly identify simulated observations. Read permission grants no device mutation; real hardware integration remains FUTURE.

### FR-CAP-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner  
**Stories:** US-D03, US-O03

Report compatible category capacity, distinct unavailable units/claims, pending-payment holds, confirmed reservations and actual occupancy separately. Pending payment blocks incompatible claims but is not counted as confirmed reserved capacity. Union overlapping occupied/protected/backup coverage rather than subtracting it twice.

### FR-CAP-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01, US-O01

Operations atomically validates temporal/category-compatible claims and commits at most one feasible result against contested capacity. Specific Slot, Zone and Capacity remain enabled Owner choices. Idempotent retries return the same authorized result; no double deduction or competing incompatible commitment. Evaluate full intervals and buffer, not just a present-time scalar.

### FR-CAP-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01, US-O01

Operations atomically validates temporal/category-compatible claims and commits at most one feasible result against contested capacity. Specific Slot, Zone and Capacity remain enabled Owner choices. Idempotent retries return the same authorized result; no double deduction or competing incompatible commitment. Evaluate full intervals and buffer, not just a present-time scalar.

### FR-CAP-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-O01, US-O02

Convert a fulfilled reservation claim to actual occupancy once. A pre-existing `OCCUPIED` state does not prove fulfillment: fulfillment requires authoritative linkage between the accepted parking session, allocation and reservation. The current occupant may be the reservation claimant or a different Driver. Arrival, gate opening, payment, entitlement cancellation and physical departure remain separate facts; only verified departure releases occupied capacity. Preserve backup designation/history while counting occupied backup once.

### FR-CAP-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-O01, US-O02, US-O03

Reject ordinary walk-in admission if actual compatible space is unavailable. Reserved and monthly arrivals still require safe accommodation; no paid right permits impossible entry or automatic displacement. An available-only monthly pass holds no space outside the lot.

### FR-CAP-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator  
**Stories:** US-O05, US-OW02

Backup capacity follows the domain contract. Owner may configure backup capacity. Operator-marked backup slots remain backup; Protected capacity is dynamically calculated from applicable future reservations and the Reservation Protection Window. This protection is separate from configured backup capacity and does not force an already-occupied vehicle to move. A slot may therefore be `OCCUPIED + PROTECTED` while an unrelated current session remains in the slot. If that occupant departs, protection may remain active as `AVAILABLE + PROTECTED`; if the occupant remains when fulfillment needs the resource, apply recovery/reallocation rather than treating the reservation as fulfilled.


A resource with no reservation/protection record is not reserved; a dedicated NOT_RESERVED database enum is not required. RESERVED includes the explicitly approved zero-price-confirmed E03 branch as well as paid confirmed reservations. `AVAILABLE + RESERVED` can become `OCCUPIED + RESERVED` when the reservation-linked Driver actually enters and the accepted session is linked to that reservation. `OCCUPIED + RESERVED` alone does not prove that linkage and can also coexist with an unrelated current occupant. Maintenance/unavailable configuration preserves the entitlement for recovery rather than erasing it.


Design/implementation owners and exit criteria: [V11-DES-04 and 09](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
