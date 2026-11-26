# Admin Audit Search & Inspection API Design (`SPARK-338`)

**Status:** APPROVED TARGET CONTRACT (v1.1 — `US-A03`, `FR-RPT-03`, `FR-RPT-04`, `UC-RPT-02`)  
**Owning Service:** `NotificationAuditService` (`SmartParking.NotificationAuditService.API`)  
**Freshness Classification:** `TOKEN` (locally validated 15-minute JWT + Admin role/policy enforcement; per `docs/01-requirements/srs/authorization-operation-classification.md` §3).

---

## 1. Overview & Architectural Boundaries

- **Purpose:** Expose read-only, paginated search and inspection of protected immutable audit records stored in `central_audit_projections`.
- **Immutability:** `central_audit_projections` is strictly append-only. No `POST`, `PUT`, `PATCH`, or `DELETE` endpoints are exposed on `/api/v1/audit-projections`.
- **Asynchronous Read-Model Honesty:** Audit projections are ingested asynchronously via RabbitMQ/MassTransit. Responses explicitly distinguish **event occurrence time (`occurredAt`)** from **projection receipt time (`projectedAt`)**, compute `ingestionLagSeconds`, and include `readModel` metadata so clients never misrepresent delayed projections as exhaustive real-time evidence.
- **Redaction:** Sensitive keys inside `details` JSON (e.g., `password`, `token`, `accessToken`, `refreshToken`, `otp`, `secret`, `privateKey`, `apiKey`, `signature`, `authorization`, `recoveryCode`, `backupCode`) are recursively masked as `"[REDACTED]"` before serialization.

---

## 2. Endpoints

### 2.1 Search Audit Projections

- **Method & Routes:**
  - `GET /api/v1/audit-projections`
  - `GET /api/audit-projections` (alias)
- **Authorization:** `[Authorize(Policy = "AdminOnly")]` (requires authenticated JWT with `role = Admin` / `ADMIN`).

#### Query Parameters

| Parameter | Type | Default | Constraints / Description |
|---|---|---|---|
| `actorId` | `Guid?` | `null` | Exact match on `actor_id` |
| `actorRole` | `string?` | `null` | Case-insensitive match on `actor_role` (max 50 chars) |
| `action` | `string?` | `null` | Case-insensitive substring match on `action` (max 100 chars) |
| `originatingService` | `string?` | `null` | Case-insensitive match on `originating_service` (max 100 chars) |
| `resourceType` | `string?` | `null` | Case-insensitive match on `resource_type` (max 100 chars) |
| `resourceId` | `Guid?` | `null` | Exact match on `resource_id` |
| `lotId` | `Guid?` | `null` | Matches `resource_id == lotId` OR `details` JSON containing the `lotId`/`siteId` GUID |
| `search` | `string?` | `null` | Case-insensitive substring match across `action`, `originatingService`, `actorRole`, `resourceType`, and `details` (max 200 chars) |
| `from` | `DateTimeOffset?` | `null` | Inclusive lower bound on `occurred_at` |
| `to` | `DateTimeOffset?` | `null` | Inclusive upper bound on `occurred_at` (must be `>= from`) |
| `page` | `int` | `1` | `>= 1` |
| `pageSize` | `int` | `20` | `1..100` |

#### Success Response (`200 OK`)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "b1f2c3d4-0000-4000-8000-000000000001",
        "originatingService": "UserService",
        "eventId": "e1f2c3d4-0000-4000-8000-000000000002",
        "actorId": "a1f2c3d4-0000-4000-8000-000000000003",
        "actorRole": "Admin",
        "action": "USER_LOCKED",
        "resourceType": "UserAccount",
        "resourceId": "c1f2c3d4-0000-4000-8000-000000000004",
        "details": "{\"reason\":\"Policy violation\",\"accessToken\":\"[REDACTED]\"}",
        "occurredAt": "2026-11-26T05:00:00Z",
        "projectedAt": "2026-11-26T05:00:02Z",
        "ingestionLagSeconds": 2.0
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1,
    "readModel": {
      "isAsynchronousProjection": true,
      "retentionPolicy": "7 years (immutable append-only projection)",
      "notice": "Audit projections are populated asynchronously from domain events; missing or delayed events may reflect ingestion lag rather than absence of activity.",
      "queriedAt": "2026-11-26T05:10:00Z"
    }
  }
}
```

---

### 2.2 Inspect Single Audit Projection

- **Method & Routes:**
  - `GET /api/v1/audit-projections/{id:guid}`
  - `GET /api/audit-projections/{id:guid}` (alias)
- **Authorization:** `[Authorize(Policy = "AdminOnly")]`
- **Success Response (`200 OK`):** `{ "success": true, "data": { ...AuditProjectionDto... } }`

---

## 3. Error Responses

| Status | `errorCode` | Condition |
|---|---|---|
| `400 Bad Request` | `VALIDATION_ERROR` | Invalid `page`, `pageSize` out of `1..100`, `from > to`, or oversized filter strings |
| `401 Unauthorized` | `UNAUTHORIZED` | Missing, expired, or invalid JWT |
| `403 Forbidden` | `FORBIDDEN` | Authenticated user does not hold `Admin` role |
| `404 Not Found` | `NOT_FOUND` | Audit projection `id` does not exist |
| `405 Method Not Allowed` | — | Any `POST` / `PUT` / `PATCH` / `DELETE` attempt on audit projection resources |
