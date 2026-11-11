# Parking Session Slot Scan — API Design

| Metadata | Value |
| --- | --- |
| Status | Implemented in working tree; contract review pending |
| Contract version / date | v1 / 2026-11-11 |
| Owning service / author | Parking Operations Service / SmartPark |
| Jira design / implementation / verification | User-requested check-in/Space persistence fix / implementation in working tree / tests not run |
| Requirements baseline | Operator check-in/out flow; no separate SRS requirement identified |
| Implementation | `POST /api/parking-sessions/{referenceId}/scan` |

## Overview

Records the parking slot confirmed by an operator scan. OperationService validates the active session and its site scope, claims the slot through FacilityService, then persists the session location and occupied allocation. Checkout completion releases the Facility slot and marks the Operation allocation released.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | User request on 2026-11-11: check-in photo assignment becomes authoritative only after Space scan; checkout releases the slot |
| Permission / service ownership | OperationService owns parking session/allocation; FacilityService owns physical slot state |
| Approved decisions | User approved adding the scan/session API on 2026-11-11 |

## API Specification

| Item | Contract |
| --- | --- |
| Method / URL | `POST /api/parking-sessions/{referenceId}/scan?siteId={siteId}` |
| Availability | Implemented in working tree |
| Authentication | Existing operator authentication and site identity resolution through `IOperatorIdentityClient` |
| Freshness / invocation origin | Operator action; scoped by the authenticated operator's accessible site |
| Permission / account state | Operator must have access to the requested site |
| Resource scope | Active session in the operator's tenant and requested site; `referenceId` may be a session ID or reservation ID |
| Content type | `application/json` request and response |
| Headers | Existing operator authorization headers |

### Request sample

```http
POST /api/parking-sessions/2ec41fb8-ccdd-42df-86bd-45799d0dcb51/scan?siteId=693ee998-fb1c-413b-9130-27c983bf9327
Content-Type: application/json

{"slotId":"9b786980-8b81-4537-9603-38cc5fd260c8"}
```

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `referenceId` | path | UUID | Required | Active session ID or its reservation ID | `2ec41fb8-…` |
| `siteId` | query | UUID | Required | Site in the operator's authorized scope | `693ee998-…` |
| `slotId` | body | UUID | Required | Preferred randomly assigned slot for a walk-in; reservation scans use the reservation's persisted slot | `9b786980-…` |

### Response sample

```json
{
  "success": true,
  "data": {
    "id": "f531d005-1904-43a0-b165-9344442a8b0d",
    "reservationId": null,
    "siteId": "693ee998-fb1c-413b-9130-27c983bf9327",
    "currentSlotId": "9b786980-8b81-4537-9603-38cc5fd260c8",
    "locationScannedAt": "2026-11-11T04:00:00Z",
    "status": "ACTIVE"
  }
}
```

The actual response includes the parking-session history fields returned by `ParkingSessionHistoryEntry`; only fields relevant to slot assignment are shown above.

### Validation and error responses

| HTTP status | Machine code | Trigger | Client recovery |
| --- | --- | --- | --- |
| 400 | `INVALID_SCAN_TARGET` | Missing/empty session or slot identifier | Correct the scan request |
| 404 | `PARKING_SESSION_NOT_FOUND` | No active session in the operator's tenant/site | Refresh session list and scan again if still active |
| 409 | `NO_AVAILABLE_SLOT` | Preferred slot is no longer available | Refresh Space and select an available slot |
| 503 | `PARKING_SLOT_UNAVAILABLE` | FacilityService cannot safely claim the slot | Retry after the FacilityService recovers |

Repeated scans return the already persisted session location. If OperationService cannot persist after FacilityService claims a slot, the claim lease releases the slot. Payment completion is idempotent and retries the Facility release if the first release attempt fails.

## State outcomes

| Case | Observable contract |
| --- | --- |
| Before scan | Check-in session and image exist; no scanned location is recorded; photo remains a pending UI assignment |
| Successful scan | `currentSlotId`, `locationScannedAt`, and occupied slot allocation are persisted; Facility physical state becomes `OCCUPIED` |
| Repeated scan | Returns existing active session assignment |
| Checkout complete | Session becomes `COMPLETED` and Operation allocation becomes `RELEASED`; a slot previously confirmed by scan also becomes `AVAILABLE` in FacilityService |

