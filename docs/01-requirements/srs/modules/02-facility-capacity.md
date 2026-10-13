# Facility Capacity — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

**Capacity/layout:** support individually represented spaces and category-specific pools. A car unit and motorcycle unit are not interchangeable generic units. Specific-slot/zone/capacity reservation products remain unchanged; a capacity reservation may be allocated from individually represented spaces. Automatic mixed-category area conversion has not been selected for MVP.

Three 3D model/template types: outdoor lot, multi-floor building lot and multi-floor basement. Owner configures floors, zones and rows; outdoor lots have no artificial floor. Fixed model assets remain versioned deployment assets; facility configuration controls instances/layout within supported templates.

An occupied backup slot is deducted once (occupied takes precedence), while its backup designation/history remains. Example: 10 slots, 3 occupied, 2 backup of which 1 occupied → 4 distinct unavailable slots before other claims, leaving 6. Do not infer a physically occupied backup can satisfy another reservation.

Late tolerance is optional. When enabled, Owner selects 15–30 minutes, default 15. Disabled retains baseline reservation validity until end; no zero-minute value substitutes for disabled. Arrival at or before the cutoff is timely; no-show only after the cutoff without authoritative timely arrival. Alternative must meet mandatory vehicle/accessibility/explicitly promised facility constraints; exact preferred slot is not guaranteed. Offer expiry releases its temporary alternative claim, not automatically the underlying entitlement. Offer duration defaults to 5 minutes; acceptance/expiry concurrency remains design work.

Owner receives read-only diagnostics at owned lots; assigned Operator retains lot-device management. This amends deferred Owner reporting scope without enabling real IoT. Manual location selection is supported when GPS is denied/unavailable.

**Plate correction:** assigned Operator may correct recognition/session identification with reason, actor, original/corrected values and history. IoT & Recognition retains original recognition/evidence; Operations owns accepted visit identification. This does not automatically mutate global account/vehicle binding.

Duplicate global binding is rejected without exposing another account's personal information. Driver may submit a dispute; Operator verifies local incident and supplies findings. Admin adjudicates already-bound/ownership-change disputes with evidence through a minimal reviewed support case. N owns support-case coordination; U owns binding adjudication facts/guarded mutation; operational evidence stays with its owner. Historical visits/invoices/evidence keep original associations. Accepted evidence direction now includes verified contact and relevant vehicle-registration/transfer evidence in existing permitted image formats, with scoped Operator findings (the media contract). Notify the existing holder; never transfer automatically for nonresponse. Admin records evidence sufficiency and reasons; unresolved cases remain pending/escalated (the vehicle binding requirement). No routine national-ID collection is approved.

Facility owns structural/service changes; Operations owns accepted occupancy and availability. Planned reductions remain pending until recoverable Operations restriction/recovery commits; emergency physical closure is recorded immediately and admission stopped safely even if communication fails. Preserve occupied facts, affected rights and history; recover compatible capacity or apply approved failure handling, never displace occupants.

Physical facts AVAILABLE/OCCUPIED/UNKNOWN/MAINTENANCE/UNAVAILABLE are separate from reservation/protection NOT_RESERVED/RESERVED/PROTECTED/BACKUP. Maintenance or closure cannot erase accepted occupation. Unknown is excluded from automatic allocation until authoritative reconciliation. Fixed backup and demand-driven protection are distinguished in the claim ledger.

## Functional requirements

### FR-LOT-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW01

Owner creates/updates/deactivates owned facility profile, address, coordinates, supported categories, operating information and open/closed status. Layout/service changes follow the planned/emergency recovery contract and preserve existing accepted rights and actual occupation.

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

Convert a fulfilled reservation claim to actual occupancy once. Arrival, gate opening, payment, entitlement cancellation and physical departure remain separate facts; only verified departure releases occupied capacity. Preserve backup designation/history while counting occupied backup once.

### FR-CAP-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-O01, US-O02, US-O03

Reject ordinary walk-in admission if actual compatible space is unavailable. Reserved and monthly arrivals still require safe accommodation; no paid right permits impossible entry or automatic displacement. An available-only monthly pass holds no space outside the lot.

### FR-CAP-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator  
**Stories:** US-O05, US-OW02

Backup capacity follows the domain contract. Owner may configure backup capacity. Operator-marked backup slots remain backup; Protected capacity is dynamically calculated from applicable future reservations and the Reservation Protection Window. This protection is separate from configured backup capacity and does not force an already-occupied vehicle to move.


A resource with no reservation/protection record is not reserved; a dedicated NOT_RESERVED database enum is not required. RESERVED includes the explicitly approved zero-price-confirmed E03 branch as well as paid confirmed reservations. AVAILABLE+RESERVED can become OCCUPIED+RESERVED on actual entry; maintenance/unavailable configuration preserves the entitlement for recovery rather than erasing it.
