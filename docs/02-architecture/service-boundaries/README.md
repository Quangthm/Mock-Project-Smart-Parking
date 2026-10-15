# Service Boundaries

The SRS currently identifies logical services including:

- User Service
- Parking Service
- Reservation Service
- Operations Service
- Payment Service
- Notification Service
- IoT Service
- Search / Recommendation Service

Additional boundaries mentioned by the SRS or later work must not be treated as
confirmed service boundaries unless explicitly established.

Use this folder to document ownership decisions as they are confirmed.

Confirmed implementation ownership:

- Reservation Service owns reservations, site-scoped gate lookup, check-in, and
  check-out. Gate transitions update the reservation, parking session, slot
  allocation, physical occupancy, and audit record in one database transaction.
- Parking Service owns operator site layouts and backup-slot designation and
  release.
- Parking Service owns operator site layouts, backup-slot designation and
  release, support tickets, incident reports, operator audit events, and
  operator notifications.
- Plate recognition is an external AI HTTP integration. The frontend submits
  images to the configured recognition service; recognition results do not
  authorize or execute gate actions.
