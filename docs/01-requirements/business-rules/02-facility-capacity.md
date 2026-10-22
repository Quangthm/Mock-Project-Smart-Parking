# Facility Capacity — business rules v1.1

[Rule index](README.md) · [SRS module](../srs/modules/02-facility-capacity.md)

## BR-CAP-01

**Status:** CONFIRMED — implementation pending / not assessed

Capacity is reported and validated separately by supported vehicle type, including motorcycle. Unused motorcycle capacity does not demonstrate automobile capacity.

Initial first-lot setup is saved within Partner Registration before email verification and combined Admin review (FR-AUTH-01/05). An approved Owner submits each additional lot as a separate application for Admin approval/rejection. Rejection leaves the Owner and other approved lots unaffected. Rejected applications remain immutable; Draft as new lot application creates a distinct proposal and review identity, including identical content. Submission/provisioning retries are idempotent; absence of content-deduplication does not permit duplicate applications/sites. Admin document replacement is confined to the initial pending Partner application.

Approval authorizes the lot; successful provisioning/activation and operational opening remain separate. Facility owns configuration/readiness; Operations validates accepted occupancy/claims and admission readiness, with required Payment terms before sales. Pending/failed/unknown provisioning is visible and recoverable without duplicate sites. Normal initial Owner login waits for successful provisioning. Open only after required configuration and operational readiness checks under existing permissions; approval never automatically opens for parking. Owner updates/deactivates owned facility profile, address, coordinates, supported categories and operating/open-state configuration while preserving accepted rights and actual occupation.

Spatial deactivation never silently cascades to descendants. Explicitly select/handle affected floors/zones/rows/spaces and resolve operational dependencies before acceptance. FE supports multiselect. A batch is confined to one lot and is all-or-nothing: validate authorization, versions, descendants and dependencies for every item; return per-item errors and mutate none when any fails. Recheck dependencies at commit; planned restriction/recovery coordination and emergency physical-closure safeguards remain applicable.

**Trace:** FR-CAP-02, FR-VEH-04, FR-LOT-01/04; UC-VEH-01, UC-LOT-01, UC-SEARCH-01

## BR-CAP-02

**Status:** CONFIRMED — implementation pending / not assessed

Report compatible category capacity, distinct unavailable units/claims, pending-payment holds, confirmed reservations and actual occupancy separately. Pending payment blocks incompatible claims but is not counted as confirmed reserved capacity. Union overlapping occupied/protected/backup coverage rather than subtracting it twice. Example: 10 slots, 3 occupied, 2 backup with 1 occupied overlap leaves 6 before other claims. Fixed backup and dynamic protection are distinct designations; no monthly guarantee claim.

**Trace:** FR-CAP-03/06; UC-RES-01

## BR-CAP-03

**Status:** CONFIRMED — implementation pending / not assessed

No incompatible claims may consume the same last resource. Idempotent retries return the same logical result and do not deduct again. Implementation lock/database/event strategy is not prescribed.

**Trace:** FR-CAP-04, FR-RES-12; UC-RES-01

## BR-CAP-04

**Status:** CONFIRMED — implementation pending / not assessed

Backup capacity follows the domain contract. Owner may configure backup capacity. Operator-marked backup slots remain backup; Protected capacity is dynamically calculated from applicable future reservations and the Reservation Protection Window. This protection is separate from configured backup capacity and does not force an already-occupied vehicle to move. Use 4-hour reservation protection, separate fixed backup and dynamic protection, compatible temporal claims and a conflict queue for affected claimants. Occupancy projection horizon is 4 hours, never a timer to free a vehicle. Protect requested preferences without promising impossible exact-slot admission.

**Trace:** FR-CAP-07, FR-BAS-04; UC-POL-01, UC-RES-02

## BR-CAP-05

**Status:** CONFIRMED — implementation pending / not assessed

A claim becoming occupied changes accounting once; physical/protection states may coexist. Expiry/cancellation of a booking cannot release a vehicle's still-occupied space; release follows verified departure.

**Trace:** FR-CAP-05, FR-GATE-05/09; UC-GATE-01
