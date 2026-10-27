# Reservation / allocation state clarification — 25 October 2026

**Status:** APPROVED clarification by project lead — documentation update; implementation and acceptance remain pending  
**Scope:** Allocation Lead Time wording, Occupancy Validity Timespan interpretation, physical occupancy versus reservation/protection semantics, and 2D/3D visualization examples  
**No schema redesign implied:** `RESERVED` and `PROTECTED` may remain mutually exclusive values of the existing Reservation / Protection State projection/enum.

## RA-01 — Effective Allocation Lead Time

Allocation begins at:

```text
allocation_point = reservation_start - effective ALLOCATION_LEAD_TIME
```

The confirmed default `ALLOCATION_LEAD_TIME` is **1 hour**.

The effective value is resolved through the approved policy hierarchy. Admin defines the platform default and permitted bounds. An authorized Owner may set a lot-scoped override only within approved bounds. If no permitted override resolves, the 1-hour default applies.

A late-created or late-confirmed reservation whose allocation point has already passed is evaluated immediately using the same idempotent allocation command used by the scheduler.

Allocation Lead Time determines **when allocation processing begins**. It does not change the established ordinary priority order: earlier reservation start, then earlier confirmation time when starts are equal.

### Retained timing example

```text
Driver A reservation: 09:30–11:00
Driver B reservation: 10:00–12:00
effective ALLOCATION_LEAD_TIME: 30 minutes

Driver A allocation point: 09:00
Driver B allocation point: 09:30
```

The 30-minute value is retained only as an explanatory resolved-policy example from the earlier SRS. It is not a newly approved bound or default.

## RA-02 — `OCCUPIED` does not identify the reservation holder

Physical state and Reservation / Protection State remain independent dimensions.

`physicalState = OCCUPIED` means that a vehicle is physically present, or remains treated as occupied under the authoritative occupancy projection. **It does not by itself identify the occupant and does not prove that the occupant is the Driver tied to a reservation or protection state.**

The relationship between:

- the current physical occupant/session, and
- the reservation/allocation/protection claimant

must be established from authoritative Operations linkage/history, such as the accepted parking session, allocation and reservation association. It must never be inferred merely because the slot is `OCCUPIED + RESERVED` or `OCCUPIED + PROTECTED`.

The current occupant and reservation claimant **may be the same Driver or different Drivers**.

### Scenario A — different current occupant and future reservation claimant

```text
Current time: 10:00

Slot S-12:
physicalState = OCCUPIED

Current accepted occupant/session:
Driver A

Future reservation:
Driver B
Reservation start: 13:30
Requested/associated slot: S-12

Reservation Protection Window:
active for Driver B
```

The slot can therefore be displayed as:

```text
OCCUPIED + PROTECTED
```

while Driver A is the current occupant and Driver B is the future reservation claimant.

This does **not** fulfill Driver B's reservation. The protection state tells SmartPark to retain and monitor the future reservation claim while the physical state tells SmartPark that the slot is not currently free.

If Driver A leaves before Driver B needs the slot, the physical state may become:

```text
AVAILABLE + PROTECTED
```

while the protection remains active so a new incompatible walk-in or claim cannot consume the newly freed capacity.

If Driver A is still occupying the slot when allocation/fulfillment requires a usable resource, SmartPark must **not** mark Driver B fulfilled and must **not** displace Driver A. Operations applies the established compatible alternative, reallocation, conflict-queue and failure-handling rules.

### Scenario B — reservation-linked Driver becomes the actual occupant

```text
Slot S-12:
reservation claimant = Driver B

Driver B arrives and is admitted to S-12.
Operations links the accepted parking session to Driver B's reservation/allocation.

physicalState = OCCUPIED
reservationState = RESERVED
```

In this case, the current occupant and reservation claimant are the same Driver **because the authoritative session/reservation linkage proves it**, not because the slot happens to show `OCCUPIED + RESERVED`.

### Scenario C — `OCCUPIED + RESERVED` can also represent different Drivers

The same visible state combination does not prove identity equality. A slot may already be physically occupied by an unrelated session while a reservation association exists for a different Driver. Until Operations proves fulfillment through the accepted session/allocation/reservation linkage, the future reservation remains unfulfilled and conflict/reallocation rules still apply.

## RA-03 — Occupancy Validity Timespan

Use the existing policy term **Occupancy Validity Timespan**.

The confirmed default is **4 hours**, but the value is configurable under the approved policy hierarchy. Runtime behavior uses the **effective `OCCUPANCY_VALIDITY_TIMESPAN`**, not a hard-coded four-hour constant.

For availability/projection purposes, a slot that is currently accepted as `OCCUPIED` is treated as occupied for the effective Occupancy Validity Timespan from the evaluation/current time unless a newer authoritative occupancy observation changes the state earlier.

Example:

```text
Evaluation time: 10:00
OCCUPANCY_VALIDITY_TIMESPAN: 4 hours
Slot S-12 current physical state: OCCUPIED

Projected occupied horizon: through 14:00
unless newer authoritative evidence changes it earlier.
```

This projection helps SmartPark evaluate future reservations whose intervals overlap the projected occupation. It does not guarantee that the vehicle remains until 14:00 and does not create an automatic physical release at 14:00.

Projection expiry must never substitute for verified physical departure.

## RA-04 — 2D/3D visualization semantics

The 2D/3D view continues to use the existing independent channels:

- Physical channel: `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE`.
- Reservation / Protection channel: `RESERVED`, `PROTECTED`, `BACKUP`, or no displayed reservation/protection state.

`RESERVED` and `PROTECTED` remain mutually exclusive projection values in the current Reservation / Protection State model. This clarification does **not** require a new database column, a split enum, or a schema migration.

The reservation entitlement/claim can continue to exist in Operations while the slot projection changes from `RESERVED` to `PROTECTED` when protection behavior is active.

A 2D/3D user may therefore see combinations such as:

```text
OCCUPIED + RESERVED
OCCUPIED + PROTECTED
AVAILABLE + RESERVED
AVAILABLE + PROTECTED
```

The visual combination must not by itself claim whether the occupant is the reservation holder.

Where an authorized Operations projection explicitly establishes the relationship, the UI may communicate a safe relationship such as:

- `Occupied by your vehicle / your active reservation`, or
- `Currently occupied; protected/reserved for your future booking`.

It must not expose another Driver's identity merely to explain that the current occupant differs from the reservation claimant.

## RA-05 — Capacity accounting remains distinct-resource accounting

The restored examples do not reintroduce double subtraction.

If occupied/protected/backup sets overlap on the same physical resource, SmartPark unions the distinct unavailable resources/claims before subtracting capacity.

The existing example remains authoritative:

```text
10 total
3 occupied
2 backup
1 slot belongs to both occupied and backup

Distinct unavailable = 4
Available before other claims = 6
```

The earlier arithmetic example may still be used when the sets are explicitly mutually distinct:

```text
Total = 100
Occupied = 60
Protected = 20
Pending Payment = 5
Backup = 10

Available = 5
```

If any of those sets overlap, the overlapping physical resource is counted once.


## RA-06 — Numeric values identified as defaults are configurable policies

This clarification supersedes D02's "locked absent approved range" treatment **for the policies listed below**. Their recorded numbers are defaults, not hard-coded business constants.

Runtime behavior uses the resolved **effective** value. The policy hierarchy is:

```text
Admin platform default + Admin-defined permitted bounds
    -> authorized Owner/lot or Owner/product override within those bounds
    -> supported more-specific scope where already allowed
    -> immutable accepted/effective context
```

Unset inherits. A valid numeric zero remains distinct from disabled where the policy permits zero. Policy changes apply prospectively and do not rewrite already accepted booking/order terms.

**No missing numeric bounds are invented by this clarification.** Where a non-default value requires bounds that are not yet recorded, Admin must define/approve those bounds before the Owner override can be activated.

The following numeric defaults are explicitly configurable:

| Policy | Confirmed default | Effective behavior |
|---|---:|---|
| `RESERVATION_MIN_DURATION` | 15 minutes | Validate against effective minimum. |
| `RESERVATION_MAX_DURATION` | 24 hours | Validate against effective maximum. |
| `MAX_ADVANCE_BOOKING` | 7 days | Validate requested start against effective advance horizon. |
| `INTER_BOOKING_BUFFER` | 0 | Use effective temporal buffer; zero remains a valid value. |
| `PAYMENT_HOLD_DURATION` | 5 minutes | `payment_hold_expiry = hold_created_at + effective PAYMENT_HOLD_DURATION`. |
| `RESERVATION_PROTECTION_WINDOW` | 4 hours | `protection_start = reservation_start - effective RESERVATION_PROTECTION_WINDOW`. |
| `ALLOCATION_LEAD_TIME` | 1 hour | `allocation_point = reservation_start - effective ALLOCATION_LEAD_TIME`. |
| `ALTERNATIVE_OFFER_DURATION` | 5 minutes | `offer_expiry = offer_created_at + effective ALTERNATIVE_OFFER_DURATION`. |
| `OCCUPANCY_VALIDITY_TIMESPAN` | 4 hours | `projected_occupied_until = evaluation_time + effective OCCUPANCY_VALIDITY_TIMESPAN`, unless newer authoritative evidence changes it earlier. |
| `DRIVER_REALLOCATION_REQUEST_LIMIT` | 2 accepted requests | Enforce the effective accepted-request limit; retries/invalid/system recovery remain excluded. |
| `MONTHLY_PAYMENT_HOLD_DURATION` | 15 minutes | Monthly order deadline uses the effective hold duration while preserving strict `A < H`. |
| `MONTHLY_RENEWAL_WINDOW` | 7 days | Renewal availability opens at the effective configured window before final committed `E`. |
| `MONTHLY_EXPIRY_NOTICE` | 7 days and 1 day | Use the effective configured reminder schedule; delivery failure never renews coverage. |
| `MONTHLY_MAX_PREPAID_TERMS` | 3 | Enforce the effective monthly prepaid-period limit; yearly remains a separate product rule. |

This decision does not silently change unrelated security, retention, refund, cancellation-abuse, pricing, yearly-design-pending or other numeric policies that are not listed above.

### Protection-window example

```text
Reservation: 09:00–12:00
effective RESERVATION_PROTECTION_WINDOW: 4 hours (default)

Protection starts: 05:00
```

If an approved lot override resolves a different effective value, protection starts from that value instead. The 4-hour number is the default, not a universal constant.

## Relationship to archived baseline

The archived v0.9 SRS already established:

- Allocation Time = Reservation Start − Allocation Lead Time.
- Allocation Lead Time as a configurable policy concept.
- Physical state and reservation/protection state as separate dimensions.
- `OCCUPIED + PROTECTED`.
- `AVAILABLE + RESERVED → OCCUPIED + RESERVED`.
- Reservation Protection Window examples.
- Occupancy Validity Timespan as a projection concept.

The later v1.0/v1.1 review established the 1-hour allocation default, the 4-hour protection/projection defaults, stable allocation, immediate late-confirmed evaluation and distinct-resource capacity accounting.

This clarification reconciles the wording without changing the database enum model.

## Affected active documents

- `docs/01-requirements/srs/smartpark-srs-v1.1.md`
- `docs/01-requirements/srs/modules/02-facility-capacity.md`
- `docs/01-requirements/srs/modules/03-discovery-visualization.md`
- `docs/01-requirements/srs/modules/04-reservations-allocation.md`
- `docs/01-requirements/business-rules/02-facility-capacity.md`
- `docs/01-requirements/business-rules/04-reservations-allocation.md`
- `docs/01-requirements/business-rules/policy-catalogue.md`
- `docs/01-requirements/srs/modules/07-monthly-passes.md`
- `docs/01-requirements/business-rules/07-monthly-passes.md`
- `docs/01-requirements/use-cases/uc-res-02.md`
- `docs/01-requirements/use-cases/uc-mon-02.md`
- `docs/01-requirements/use-cases/uc-mon-03.md`
- `docs/01-requirements/srs/state-transitions.md`
- `docs/03-design/3d-digital-twin/3D_BACKEND_SLOT_STATE_BINDING.md`
- `docs/04-testing/requirements-traceability/acceptance-scenarios.md`
- `docs/05-project-management/decision-log/README.md`

## Acceptance status

The added acceptance scenarios are specifications only and remain **NOT RUN**. This documentation clarification does not claim that the current scheduler, policy persistence, visualization API, frontend, database or tests already implement the clarified behavior.

## Baseline-package integrity note

`docs/01-requirements/srs/baselines/v1.1/manifest.md` and its SHA inventory are intentionally not rewritten. They describe the earlier SPARK-296 review package. A future reviewed baseline should generate a new manifest/hash inventory including this clarification.
