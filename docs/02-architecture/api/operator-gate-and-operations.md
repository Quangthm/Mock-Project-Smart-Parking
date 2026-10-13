# Operator Gate and Operations APIs

All operator-facing requests require a signed-in operator bearer token and a
site assignment. The frontend uses these base URLs:

- Parking Service: `VITE_PARKING_API_BASE_URL` (default `http://localhost:5045`)
- Reservation Service: `VITE_RESERVATION_API_BASE_URL` (default `http://localhost:5055`)

## Reservation Service

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/reservations?siteId={siteId}` | List reservations scoped to the operator's assigned site |
| `GET` | `/api/reservations/lookup?siteId={siteId}&query={plateOrReservationId}` | Find a reservation by ID or normalized plate |
| `POST` | `/api/reservations/{reservationId}/check-in?siteId={siteId}` | Check in a confirmed reservation; optional body `{ "plateNumber": "..." }` |
| `POST` | `/api/reservations/{reservationId}/check-out?siteId={siteId}` | Check out an active parking session; optional body `{ "plateNumber": "..." }` |
| `GET` | `/api/parking-sessions/walk-in/lookup?siteId={siteId}&plate={plate}` | Find an active walk-in session by plate |
| `POST` | `/api/parking-sessions/walk-in/check-in?siteId={siteId}` | Create a walk-in session and claim an available slot; body `{ "plateNumber": "..." }` |
| `POST` | `/api/parking-sessions/{sessionId}/walk-in/check-out?siteId={siteId}` | Complete a walk-in session; body `{ "plateNumber": "..." }` |

The Reservation Service validates site assignment and plate match. Gate changes
are persisted transactionally. AI results with confidence of at least `0.90`
automatically check in a matching confirmed reservation or create a walk-in
session when no reservation exists. Lower confidence results require the
operator to activate the displayed check-in action. Check-out completes the
parking session; payment calculation and collection remain the Payment
Service's responsibility.

## Parking Service

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/parking-lots/{siteId}/operator-layout` | Read the assigned operator's live site layout |
| `POST` | `/api/parking-lots/{siteId}/slots/{slotId}/backup` | Designate an eligible slot as backup; body `{ "reason": "..." }` |
| `DELETE` | `/api/parking-lots/{siteId}/slots/{slotId}/backup` | Release a backup designation |

Backup actions require the operator's `SLOT_OVERRIDE` permission.
Layout reads use a read-only Reservation snapshot and do not acquire the durable structure-edit hold.
The Reservation Service calls internal Parking Service gate-slot claim/release
endpoints. The Reservation database requires
`scripts/database/microservices/03.1-reservation-gate-audit.sql` before enabling
gate check-in. Parking slot release uses the `current_slot_id` stored on the
Reservation Service's parking session; Parking DB does not duplicate a session ID.

## Parking Service — operator operations

Every path below is scoped by `siteId` and verifies the operator's assignment.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/operator/sites/{siteId}/tickets` | List site tickets |
| `PATCH` | `/api/operator/sites/{siteId}/tickets/{ticketId}` | Change ticket status; body `{ "status": "open|in-progress|resolved", "resolutionNote": "..." }` |
| `GET` | `/api/operator/sites/{siteId}/incidents` | List incidents |
| `POST` | `/api/operator/sites/{siteId}/incidents` | Report an incident; body `{ "type": "...", "details": "..." }` |
| `PATCH` | `/api/operator/sites/{siteId}/incidents/{incidentId}` | Resolve a reported incident |
| `GET` | `/api/operator/sites/{siteId}/audit` | Read operator audit events |
| `GET` | `/api/operator/sites/{siteId}/notifications` | List notifications for the signed-in operator |
| `PATCH` | `/api/operator/sites/{siteId}/notifications/{notificationId}/read` | Mark an operator notification read |

Operator operation data (tickets, incidents, audit events, and notifications)
is stored in the Parking Service database. The service creates the required
tables at startup; `scripts/database/microservices/02.2-parking-operator-operations.sql`
is also provided for database setup and migration workflows.
