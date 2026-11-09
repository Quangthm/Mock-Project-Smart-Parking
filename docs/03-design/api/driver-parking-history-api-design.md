# Driver Parking History — API Design

| Metadata | Value |
| --- | --- |
| Status | DRAFT / PROPOSED |
| Contract version / date | 1.0 / 2026-11-09 |
| Owning service / author | OperationService / Antigravity Agent |
| Jira design / implementation / verification | SPARK-217 / SPARK-218 / SPARK-220 |
| Requirements baseline | SRS v1.1 (FR-GATE-08, BR-GATE-02, BR-GATE-03, BR-GATE-04, BR-PAY-05, BR-PAY-06, M-04, UC-GATE-02) |
| Implementation | In Progress |

## Overview

Provides authenticated Drivers access to their authoritative parking sessions (both active and completed) sourced directly from `OperationService`.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | FR-GATE-08, BR-GATE-02, BR-GATE-03, BR-GATE-04, BR-PAY-05, BR-PAY-06, M-04, UC-GATE-02, US-D04 |
| Permission / service ownership | OperationService / Driver Role (Subject `sub` / `account_id`) |
| Approved decisions | Freshness: Token validated; Driver authorized strictly to own account |

## API Specification

### Endpoint: GET /api/parking-sessions/my-history

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/parking-sessions/my-history` |
| Availability | Implemented in Sprint 3 |
| Authentication | Bearer JWT (Driver account) |
| Freshness / invocation origin | TOKEN / REQUEST |
| Permission / account state | Authenticated Driver (`AccountId` matching token claims) |
| Resource scope | Own account sessions |
| Content type | `application/json` |
| Headers | `Authorization: Bearer <token>` |

#### Query Parameters

| Field | Location | Type / format | Required / nullable | Constraints and meaning | Example |
| --- | --- | --- | --- | --- | --- |
| `status` | query | string | Optional / null allowed | Filter by session status: `ALL`, `ACTIVE`, `COMPLETED` (default: `ALL`) | `ACTIVE` |
| `limit` | query | integer | Optional | Max items to return (1-100, default 50) | `20` |

#### Request Sample

```http
GET /api/parking-sessions/my-history?status=ALL HTTP/1.1
Host: localhost:5055
Authorization: Bearer eyJhbGciOi...
Accept: application/json
```

#### Response Sample (200 OK)

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "reservationId": "7b895f64-5717-4562-b3fc-2c963f66afa7",
      "siteId": "9c895f64-5717-4562-b3fc-2c963f66afa8",
      "siteName": "Central Lot",
      "slotId": "1a895f64-5717-4562-b3fc-2c963f66afa9",
      "slotNumber": "A-01",
      "originalPlate": "51A-123.45",
      "vehicleType": "CAR",
      "ticketReference": "TKT-20261109-001",
      "entryTime": "2026-11-09T08:30:00Z",
      "entryImageUrl": "https://storage.smartpark.vn/evidence/entry-001.jpg",
      "exitTime": null,
      "exitImageUrl": null,
      "status": "ACTIVE",
      "paymentStatus": "UNPAID",
      "createdAt": "2026-11-09T08:30:00Z"
    }
  ]
}
```

#### Validation and Error Responses

* `401 Unauthorized`: Missing or invalid Bearer token.
* `403 Forbidden`: Account is not permitted or revoked.
* `500 Internal Server Error`: Transient database/service failure.

