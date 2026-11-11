# SESSION-20261111-vneid-simulation-integration.md

- **Date:** 2026-11-11
- **Branch:** `feat/vneid-simulation-integration` (tracking `origin/develop`)
- **Status:** In Progress
- **Jira Reference / Traceability:** V11-06, V11-DES-02, DB26, FR-AUTH-01, FR-AUTH-04, AC-113

---

## 1. Goal & Objectives
Thiết kế và triển khai luồng tích hợp VNeID mô phỏng (Simulated VNeID Integration) cho SmartPark v1.1:
- Tạo VNeID giả lập trả về một GUID thay thế (substitute GUID) được lưu bền vững trong `UserDb` của `UserService`.
- Hỗ trợ Driver registration, Owner/Partner onboarding và Operator activation.
- Bảo đảm consent rõ ràng, kiểm tra điều kiện tiên quyết (prerequisite check).
- Bảo đảm quy tắc miễn trừ duy nhất cho Seeded MVP Admin.
- Tuyệt đối không xem GUID là CCCD/GPLX thật hoặc credential đăng nhập.

---

## 2. Checkpoints & Progress
- [x] **Checkpoint 1 — Initialization & Branch Setup**: Tạo branch `feat/vneid-simulation-integration` từ `origin/develop`.
- [x] **Checkpoint 2 — Architecture & Domain Contracts**:
  - Entity `UserSimulatedIdentity` trong `UserService.Domain`.
  - Abstraction `IVNeIdSimulationService` trong `UserService.Application`.
  - Entity mapping & DB Migration trong `UserService.Persistence`.
  - SQL Schema cập nhật trong `01-user-access-service-db.sql`.
- [x] **Checkpoint 3 — Application & API Endpoints**:
  - MediatR commands/handlers & `VNeIdSimulationService` trong `UserService.Application`.
  - Controllers/Endpoints cho Driver, Owner, Operator trong `UserService.API`.
  - Prerequisite guards: Driver verify, Owner first-lot setup, Operator activation.
  - Optional password setup cho Operator.
  - Cập nhật FE authApi client methods.
- [x] **Checkpoint 4 — Verification & Tests**:
  - Unit tests cho commands/handlers & domain rules (`VNeIdSimulationTests.cs` passed 3/3).
  - FE tests passed (`npm test` 29/29).
  - Documentation updated (`driver-register-api-design.md`, `owner-register-api-design.md`, `owner-create-operator-api-design.md`).

