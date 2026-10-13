# Facility Capacity — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/02-facility-capacity.md)

## BR-CAP-01

**Status:** CONFIRMED — implementation pending / not assessed

Capacity is reported and validated separately by supported vehicle type, including motorcycle. Unused motorcycle capacity does not demonstrate automobile capacity.

**Trace:** FR-CAP-02, FR-VEH-04; UC-VEH-01, UC-LOT-01, UC-SEARCH-01

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
