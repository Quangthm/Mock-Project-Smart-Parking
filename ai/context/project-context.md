# SmartPark AI Agent Project Context

**Status:** WORKING
**Last Updated:** 2026-10-01
**Purpose:** Minimum shared context an AI development agent should read before making project changes.

## Project

SmartPark is a configurable smart parking management and reservation platform for a simulated parking environment. The project is being developed by approximately 8 people over approximately 3 months, so scope control is important.

## Current architecture direction

```text
Client
  ↓
API Gateway
  ↓
Microservices
  ↓
Service-owned persistence
```

Current direction includes synchronous API communication where an immediate response is required and Kafka only where asynchronous/event-driven communication provides a real benefit.

## Current technology direction

- Backend: ASP.NET Core / .NET 10
- Frontend: ReactJS
- UI/styling: TailwindCSS + Material UI
- Database: PostgreSQL
- Containerization: Docker
- Messaging/event layer: Kafka when justified
- 3D parking visualization: Three.js
- AI: chatbot, recommendation, and image-recognition capabilities are in MVP scope, but model/provider selection and some implementation details remain deferred.

## Current repository direction

GitLab is the primary working/progress repository during the architecture-foundation phase. GitHub is planned to become the canonical engineering repository later, with GitHub Actions for CI/CD.

## Canonical requirements principle

The SRS remains the authoritative requirements document. Context, review, handoff, and decision records explain how to interpret and evolve it; they do not silently replace it.

## Core domain concepts that must remain distinct

```text
Reservation
Allocation
Physical Occupancy
Parking Session
Payment
Protection / Backup Capacity
Operator Actions
```

Do not collapse these concepts into a single status unless a canonical project decision explicitly does so.

## Reservation modes

The current baseline supports:

1. Specific Slot Reservation
2. Zone Reservation
3. Capacity Reservation

A Specific Slot Reservation expresses a preferred physical slot, not an absolute exact-slot guarantee.

## Reservation/payment semantics currently confirmed

- Reservation lifecycle conceptually includes `PENDING_PAYMENT`, `CONFIRMED`, `ALLOCATED`, `PARKING`, and `COMPLETED`, with exceptional paths such as expiry/cancellation/unfulfillable outcomes.
- `PENDING_PAYMENT` is a temporary payment hold and is not the same as `RESERVED`.
- `RESERVED` means associated with an already-paid reservation for a defined period.
- Payment hold and reservation protection window are separate mechanisms.
- Current approved payment-hold default: 5 minutes, configurable at system level by Admin.
- Reservation Protection Window is owner/parking-lot configurable.

## Allocation semantics currently confirmed

```text
Allocation Time = Reservation Start Time - Allocation Lead Time
```

Normal allocation priority:

1. Earlier reservation start time.
2. Earlier reservation confirmation time when start times are equal.

If allocation cannot be made by Allocation Time, the driver is warned that fulfillment at arrival may not be guaranteed; the reservation does not automatically become a walk-in booking.

Accepted reallocation does not create a second parking charge solely because the physical slot changed.

## Important exceptions

AI agents working on reservation-related code/documentation should be aware of, at minimum:

- existing occupancy / overstay conflicts;
- preferred slot becoming occupied, unavailable, or under maintenance;
- unexpected loss of an already allocated slot;
- emergency-driven reallocation;
- physical arrival order when previously allocated capacity is unexpectedly lost;
- no allocation by Allocation Time;
- backup-capacity exhaustion;
- payment callback races and expiry cases;
- concurrent claims for the same capacity;
- sensor/camera/manual observations disagreeing.

## Open/deferred areas

Do not invent values for unresolved areas, including exact policy values and several technical details. Current deferred work includes allocation scheduling, state-transition refinement, database concurrency/locking, idempotency, audit persistence for reallocation, race conditions among allocation/arrival/emergency/payment callbacks, exact physical-state reconciliation, and refund callback behavior.

See the canonical project documents before proposing changes.

## AI-specific note

The current SRS contains some legacy AI material that still needs reconciliation with the newer Chatbox architecture. Do not assume every older AI use case, implementation phase, or autonomous-action statement is current.
