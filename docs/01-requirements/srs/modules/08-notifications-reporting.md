# Notifications Reporting — SRS v1.1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** v1.1 documentation review candidate based on approved decisions, 20 October 2026; implementation/evidence not assessed. Existing requirement IDs and unrelated approved amendments remain in force.

Confirmed live views: Operator occupancy/gate and parking slots; 3D; Admin user management/approval; notification bell. Clients fetch current authorized state after reconnect. Missed messages never change authoritative capacity or business rights. Implement scoped subscriptions/revocation and minimal payloads; no cross-lot plate/evidence broadcasting. SignalR handles client updates, RabbitMQ durable inter-service outcomes. Redis scale-out remains optional, not prerequisite for single-instance local MVP. Approved healthy-local-profile freshness target: p95 ≤2 seconds (§16.7); measurement and recovery contracts remain to implement.

Domain owners provide scoped reports: revenue day/week/month/quarter, lot and registered/walk-in segmentation; hourly occupancy, duration, vehicle distribution and peaks; authorized new registrations. Payment-method breakdown, retention/geographic/user-pattern analytics remain FUTURE. No guest-only pricing implication. Export and SignalR enforce the same resource permissions.

Notifications include bookings/cancellation/expiry, payment and invoice/refund, appeal, overstay, monthly expiry 7 / 1 days, account provisioning, policy updates and opted-in availability alerts. In-app bell, local email capture and SMS fixtures have explicit modes. Delivery failure never rolls back the business fact. Preserve created pending accounts/applications and committed approval/rejection even when email fails; queued is not delivered. Retry the same logical delivery without repeating review/provisioning or creating records. Store originating event, recipient, template/version, channel, attempt/result; redelivery dedupes logical notification. Unspecified reminder timing has no seeded demo default. Domain-owned reports compose through authorized contracts.

## Functional requirements

### FR-RPT-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Notification Service  
**Stories:** US-D11

Notification delivery follows authoritative business events/states and supported channels, including expiry, refund and appeal status, overstay, activation and Partner review. Send approval/rejection email with a safe reason; approval after initial document replacement says “Your application was approved with some modifications”. This is approval, not a third outcome; distinguish approval from provisioning/sign-in readiness. Delivery failure is separate from the underlying business outcome and never rolls back a successful business state; record event ID, recipient, channel, template/version, attempt, delivery status and failure reason.

### FR-BAS-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner, Admin  
**Stories:** US-D11

Support in-app, email and SMS notification categories, opted-in availability alerts/preferences, policy updates and account provisioning. Monthly reminders are7 / 1 days; other unapproved timing remains unseeded configuration. Local email capture and SMS fixtures are the acceptance modes, not proof of real delivery.

### FR-RPT-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW04

Domain owners provide scoped reports: revenue day/week/month/quarter, lot and registered/walk-in segmentation; hourly occupancy, duration, vehicle distribution and peaks; authorized new registrations. Payment-method breakdown, retention/geographic/user-pattern analytics remain FUTURE. No guest-only pricing implication. Export and SignalR enforce the same resource permissions.

### FR-RPT-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A03

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

### FR-RPT-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A03

Expose authorized Admin audit search by actor, time, lot and action; support the domain contract and the domain contract without enabling log edits.

### FR-RPT-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A05

Persist protected originating audit facts and project them through Notification & Audit. Each owner enforces scoped access, retention and acknowledged holds. Images max 30 days; audit 7 years; operational logs 90 days hot + 1 year cold; financial documents 7 years from issuance; conversations 90 days from last message, unaffected by administrative reads. Review holds every 30 days and at case closure; release only originating hold, preserving others. Provider deletion target 24 hours does not extend a hard retention deadline.

### FR-BAS-07

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner  
**Stories:** US-OW04

Domain owners provide scoped reports: revenue day/week/month/quarter, lot and registered/walk-in segmentation; hourly occupancy, duration, vehicle distribution and peaks; authorized new registrations. Payment-method breakdown, retention/geographic/user-pattern analytics remain FUTURE. No guest-only pricing implication. Export and SignalR enforce the same resource permissions.


## Retained notification categories by source channel

Push/in-app direction: reservation confirmation, payment reminders, opted-in availability alerts and promotional offers. SMS direction: OTP, invoice/payment notice, configured overdue reminders and reservation QR references. Email direction: invoice/VAT-invoice subject, monthly usage reports, policy changes, password reset, slot reallocation and registration/approval/Owner-created Operator notices. These source categories remain in scope; they do not introduce a promotions/loyalty engine, arbitrary reminder schedule, raw private QR broadcast, tax-rate assumption or proof of live delivery. In the approved local profile use authorized in-app bell, email capture and SMS fixtures; evaluate live provider delivery separately.


## Account recovery notifications and support type

ACCOUNT_RECOVERY is current MVP, handled by Admin through General Support. Pre-login intake is restricted and generic; evidence/case access is scoped. Audit recovery submission/review/authorization and successful/failed credential changes separately. Notify existing verified channels of TOTP enrollment/disable/replacement, backup-code regeneration and Admin reset/password mutation, without including secrets or usable reset tokens in audit/event payloads. Notification failure is visible and retried; it never silently undoes a completed security change or supplies account access. Handler cannot mark a pending credential mutation as successful recovery.


Design/implementation owners and exit criteria: [V11-DES-07](../../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
