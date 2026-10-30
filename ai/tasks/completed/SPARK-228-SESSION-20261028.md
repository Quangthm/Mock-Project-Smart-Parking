# Session Review — 2026-10-28

## 1. Session Information
- **Session ID:** SESSION-20261028-001
- **Date:** 2026-10-28
- **Agent:** Main Agent (+ api-designer, security-reviewer, backend-tester, fixer lanes)
- **Branch:** `feat/SPARK-228-3D_Model_BE`
- **Related Task:** SPARK-228 — 3D Digital Twin slot-state read path
- **Status:** ? COMPLETED � Phase 3 ��ng (commit e62553f5), F3 ship (commit 48547dd0), PR #36 m? v?i test guide (commit 832afe74). Branch sync v?i remote. S?n s�ng merge v�o develop sau PM duy?t.

## 2. Objectives
- [x] Xác nhận trạng thái repo sau merge `develop` và sau khi push branch.
- [x] Chốt phạm vi SPARK-228 ở mức read-path only.
- [x] Tạo file planning **ngoài repo**: `D:\My file\FA26 - OJT FSoft\Planning\SPARK-228-planning.md`.
- [x] **Phase 0.1** — Rà `ReadOperationalAsync` + endpoint đối chiếu openapi → bảng 3 cột + danh sách lệch.
- [x] **Phase 0.2** — Xác nhận role matrix (Driver/Operator/Owner/Admin) + redaction.
- [x] **Phase 0.3** — Chứng minh fixture parse được bằng DTO (deserialize thật).
- [x] **Phase 1a-docs** — Sửa 12 node `nullable: true` trong openapi sang union 3.1.
- [x] **Phase 1a-BE** — Projection DTO + 2 trục + metrics + freshness + read path lock-free + Cache-Control + legacy gate.
- [x] **Phase 1b** — Dual-mode auth + redaction theo role.
- [x] **Phase 3.1** — `OperationalLayoutContractFixtureTests` (xUnit, clock cố định).
- [x] **Phase 3.2** — Unit test A2 (`OperationalStateMapping`) + A7 (`OperationalLayoutRedaction`).
- [x] **Security re-audit 1b** → đóng 4 finding F1–F4 + 8 regression test.
- [x] Phase 0 runtime: gọi 4 token thật trên stack đang chạy — **đã đóng 2026-10-30** (§3.11, §7): anonymous/driver/operator + 401/404/legacy đều đúng.
- [x] **Doc defect + Postman** — sửa D1 (telemetry theo `SLOT_OVERRIDE`) và B3 (số dòng `Program.cs`) trong cả hub + 228; tạo Postman collection 32 request + environment + manual test guide.
- [x] **Commit Phase 1** — `a2176840`, gộp 1 commit, 20 file.
- [x] **Phase 2.1** — Bỏ if/else map trạng thái, thay bằng `Record<Union, T>` + default fail-closed.
- [x] **Phase 2.2** — Adapter đọc 2 trục, không gộp `status` ở nguồn sự thật.
- [x] **Phase 2.3** — Fixture vào chế độ dev qua feature flag (`VITE_OPERATIONAL_LAYOUT_FIXTURE`).
- [x] **Phase 2.4** — Stale: giảm 30% + banner; reconnect (`online` + `visibilitychange`) snapshot lại.
- [x] **Phase 2.5** — `slotCode` → mesh: không cắt tiền tố, không fallback index, có log slot không khớp.
- [x] Verify mắt Phase 2 (F5) — **user đã tự test tay FE + BE (2026-10-30): mọi thứ chạy ổn**, trừ 2 lỗi màn Owner (BACKUP hiện amber ở 3D + viền vàng ở 2D) → đã sửa ở §3.12 và chờ user test lại.
- [x] Commit Phase 2 — nằm trong commit gộp `27f11f31` (46 file).
- [x] **Nhãn slot hiển thị** — thêm `formatSlotLabel()` (raw `slotCode` **giữ nguyên** cho bind A8/FR-MAP-03), áp 17 call site / 8 file. Kèm script SQL demo đặt 6 trạng thái — **không chạy**, user tự chạy (§9 item 8).
- [x] **Phase 3 — endpoint HTTP test** — `OperationalLayoutEndpointTests.cs`, 10 case (E1–E10), `Microsoft.AspNetCore.Mvc.Testing`; `Program` đổi thành `public partial class Program`.
- [x] **Seed khớp GLB** — 48 slot (36 `CarParkingSpace_*` + 12 `PARKING_EV_CAR_*`), **0 slot xe máy**, capacity 48/48/48.
- [x] **Postman chạy được** — account thật + guard lỗi + tạo operator-no-override qua API; 33 request / 9 folder.
- [x] **Vòng sửa sau test tay của user** — filter áp vào 3D; `eligibleSlots` backup dùng 2 trục; bỏ slot MOTO; nạp đủ 12 EV; sửa hướng dẫn Postman/FE.
- [x] **Merge `origin/develop` lần 1** (`048c1ad3`) + review tuân thủ coding skill bằng `@oracle` + sửa cancellation/enum/KPI responsive.
- [x] **Merge `origin/develop` lần 2** (`26c2f0e4`) — rename `ParkingService→FacilityService`, `ReservationService→OperationService`, giải 19 conflict, giữ toàn bộ code SPARK-228.
- [x] **Phase 3b — dựng lại stack**: dọn `bin/obj` của 3 thư mục service cũ; build 0/0; áp 4 script schema vào volume sẵn có; rebuild 5 image; `npm ci` cho frontend; 5/5 container healthy.
- [x] **Phase 3b — sửa 3 lỗi dữ liệu dev** (operator PENDING_VERIFICATION, race 57P03, stale password hash) → 4/4 account demo login đúng role.
- [x] **Phase 3b — smoke HTTP thật** cho `operational-layout` (anonymous/driver/operator/401/404/legacy/backup mark-release) — §7.
- [x] **Sửa lỗi Owner nhìn BACKUP thành `reserved` (user báo 2026-10-30)** — 3D cyan + 2D viền cyan `#0891b2` — §3.12.
- [x] **Script hoá việc sửa identity demo** — `scripts/repair-demo-identity.ps1` (dry-run + `-Apply`, hash bằng pgcrypto) — §3.12.
- [x] **Postman chạy lại sau merge** — 33 request / **123 assertion / 0 fail**, các case thiếu tiền đề nay `SKIP` (Blocked) thay vì fail — §7.
- [ ] **Commit đóng Phase 3** — user yêu cầu **đợi test lại lần cuối** rồi mới commit (m0472).
- [ ] **Push branch** (đang ahead 43) — chờ user quyết định.

## 3. Work Completed

### 3.0 Planning (trước Phase 0)
- [x] Merge integrity: 0 conflict marker; hub/228/244/openapi/memo còn nguyên; `§3.2`/`§4.3` của 228 **identical** với hub.
- [x] Hạ tầng: `rabbitmq` service có trong `compose.schema-integration.yml` (`:104-125`); chưa có MassTransit/publisher/consumer/SignalR.
- [x] Đóng blocker cấu hình: `SMARTPARK_RABBITMQ_*` trong `.env.example`; `setup.ps1` sinh password; `docker compose config --quiet` exit 0.

### 3.1 Phase 0.1 — Contract conformance (TRƯỚC khi sửa)
**Kết luận:** chỉ `success` khớp. **0/45 field path trong `data.*`** đúng path + shape. Response là **domain aggregate serialize trực tiếp** (`Program.cs:142` → `ParkingStructure`), không có DTO projection.

Các lệch chặn (đã xử lý ở Phase 1a):
1. **Không có projection DTO** — đổi aggregate sẽ đổi luôn `GET /structure` (`Program.cs:141`).
2. **2 trục không tồn tại** — `repo:205` `CASE physical_state WHEN 'AVAILABLE' THEN 'OPERATIONAL' WHEN 'OCCUPIED' THEN 'OPERATIONAL' WHEN 'UNAVAILABLE' THEN 'BLOCKED'` ⇒ gộp AVAILABLE+OCCUPIED, `UNAVAILABLE`→`BLOCKED` ngoài contract; `reservation_state` NULL tới client dưới dạng `null`.
3. **GET chiếm edit-hold độc quyền + advisory lock** (`LockSiteAsync`/`PrepareAsync`/`AcquireAsync`/`PublishAsync`) ⇒ 2 GET cùng site = **409 `STRUCTURE_BUSY`** (không khai báo trong openapi `:56-74`); 16 GET toàn hệ ⇒ cạn `ParkingGateConnectionPool`. Trái `openapi.yaml:26` ("Non-blocking read path using ReadSnapshotAsync; does not acquire exclusive edit holds").
4. Thiếu toàn bộ `metrics`/`floors`; `totalPhysicalCapacity` sai nghĩa (đếm mọi loại xe); `spatial_unit_capacities` không bao giờ được SELECT.
5. Freshness triple + `contractVersion` vắng; không có bảng heartbeat.
6. `vehicleType`/`floor` được khai báo nhưng **bị bỏ qua** ⇒ trả cả site thay vì 400.
7. `Cache-Control` chỉ `no-store` (thiếu `no-cache, must-revalidate`); không có catch-all `INTERNAL_ERROR`.
8. `CapacityViews` là computed getter → ném `CAPACITY_CONFLICT` (400) trong lúc serialize.
9. Tài liệu sai tham chiếu dòng: design `:256-257` đảo `Program.cs:142`/`:165` so với code.

**Field dư từng bị lộ:** `site.{tenantId,address,latitude,longitude,isActive,status}`, `units[]` (kèm `path` chứa GUID ancestry), `paths[]` (kèm `mapData`), `backupPolicies[]`, `capacityViews[]` (kèm `pendingPayment`/`protected`/`effectiveBackup`), `slots[].{unitId,operationalStatus,isPhysicallyOccupied}`.

### 3.2 Phase 0.2 — Role matrix (TRƯỚC khi sửa)
Identity/authorization đẩy toàn bộ sang UserService qua header `Authorization` forward nguyên bản; ParkingService không validate JWT cục bộ. `ReadOperationalAsync` chỉ hỏi **operator authorizer**.

| Caller | Trước 1b | Hợp đồng | Sau 1b |
|---|---|---|---|
| Anonymous | 401 | 200 Sanitized | 200 Sanitized (0 call) |
| Driver | 403 | 200 Sanitized | 200 Sanitized |
| Operator + `SLOT_OVERRIDE` | 200 full aggregate | 200 Telemetry | 200 Telemetry |
| Operator assigned, thiếu `SLOT_OVERRIDE` | 403 | (hub nói Telemetry — xem §6 D1) | 200 Sanitized |
| Owner đúng tenant | 403 | 200 Full Structural Metrics | 200 Telemetry |
| Owner khác tenant | 403 | (không định nghĩa) | 200 Sanitized |
| Admin | 403 | 200 Full Telemetry | 200 Telemetry |

- **Không có tầng redaction nào** trước 1b: search `operatorContext|isBookable|sessionType|Redact|Mask` = 0 hit; `OwnerScope` chỉ có `UserId/TenantId`, **không mang role**.
- **Lỗ hổng ranh giới thật (đã đóng):** route legacy `/operator-layout` trả **cùng aggregate** cho operator chỉ cần **assignment** (UserService `site-scope`) ⇒ gate `SLOT_OVERRIDE` của route mới không phải ranh giới thật.
- **Mâu thuẫn hợp đồng đã xử lý:** §4.3 mang nhãn `[PROPOSED]`, matrix `:72` ghi operator-only là `[VERIFIED]`, nhưng openapi `security: [BearerAuth: [], {}]`, TC-API01-03/06 và A7 đều yêu cầu dual-mode ⇒ **dual-mode + redaction là yêu cầu của SPARK-228**.

### 3.3 Phase 0.3 — Fixture validation
Cả hai fixture deserialize thành công vào DTO đúng shape hợp đồng (`DTO_DESERIALIZATION_OK`, `slots=48`, `distinctSlotCodes=48`, `combos=20`, `badSlotCodes=0`).

| Kiểm | Kết quả |
|---|---|
| Field envelope bắt buộc (10) | đủ ở cả 2 file |
| Field bắt buộc mỗi slot (11) | 48/48 đủ |
| Field lạ ngoài schema | 0 |
| Enum ngoài tập | 0 (`physicalState` đủ 5, `reservationState` đủ 4, `slotType` đủ 4) |
| `slotId`/`slotCode` trùng | 0; 48/48 distinct |
| `slotCode` pattern/độ dài | 0 lỗi; dài nhất 21 (`CarParkingSpace_008 2`) |
| `isBookable` invariant | 0 vi phạm (16 `true` = đúng `AVAILABLE + UNRESERVED`) |
| Metrics khai báo vs suy ra | khớp: 48/13/9/16, Zone_A 24/4, Zone_B 24/9 |
| Motorcycle 20/8/12 | **không suy ra được** từ `slots[]` (pool scalar) — chỉ đọc literal |
| Diff NORMAL vs STALE | **đúng 2 field**: `generatedAt` (12:00→11:45), `isFresh` (true→false), 900 s > 30 s |

Phủ tổ hợp (48 slot): **20/20, không thiếu, không thừa**

| physical \ reservation | UNRESERVED | RESERVED | PROTECTED | BACKUP |
|---|---:|---:|---:|---:|
| AVAILABLE | 16 | 4 | 1 | 1 |
| OCCUPIED | 9 | 2 | 1 | 1 |
| UNKNOWN | 1 | 1 | 1 | 1 |
| MAINTENANCE | 2 | 1 | 1 | 1 |
| UNAVAILABLE | 1 | 1 | 1 | 1 |

Tổng theo trục: physical `{AVAILABLE:22, OCCUPIED:13, UNKNOWN:4, MAINTENANCE:5, UNAVAILABLE:4}`; reservation `{UNRESERVED:29, RESERVED:9, PROTECTED:5, BACKUP:5}`.

**Verdict: PARSEABLE-WITH-GAPS** — gap không nằm ở fixture: G1 schema 3.1 dùng `nullable: true` (**đã sửa ở 1a-docs**); G2 chưa có DTO production (**đã sửa ở 1a-BE**); G3 coverage hẹp (chỉ CAR, 1 floor, 2 zone, không có `isEvCharging=true`); G4 timestamp tĩnh ⇒ test phải dùng clock cố định.

### 3.4 Phase 1a-docs — openapi 3.1 nullable
12 node `nullable: true` → `type: [X, "null"]` (11 node) và `anyOf: [$ref SessionType, type: 'null']` (1 node: `sessionType`). Dòng gốc: 391, 395, 407, 419, 435, 446, 450, 456, 460, 464, 485, 611.
Kiểm: `Select-String "nullable"` → **0 match**; `git diff --stat` → 14 insertions / 24 deletions; `YAML_PARSE_OK`, `NULL_TYPE_PROPS=12`; không BOM, LF-only.

### 3.5 Phase 1a-BE — Projection + read path
**File mới:** `Application/OperationalLayoutProjection.cs`, `Persistence/PostgresOperationalLayoutReader.cs`.
**File sửa:** `Application/ParkingStructureService.cs`, `API/Program.cs`, `Persistence/PostgresParkingStructureRepository.cs` (chỉ comment).

- Union DTO: `success/data`, `siteId, siteCode, siteName, contractVersion:"1.2.0", generatedAt, isFresh, stalenessThresholdSeconds:30, metrics, floors[], slots[]`. `SlotVisualStateDto` có `[JsonPropertyName("coordinates3d")]` vì camelCase mặc định sẽ ra `coordinates3D`. Enum member VIẾT HOA để `JsonStringEnumConverter` phát đúng chuỗi hợp đồng.
- **`CASE` ở `PostgresParkingStructureRepository.cs:205` được GIỮ** vì query đó còn phục vụ `GET /structure`, phải byte-identical. Projection có SQL riêng, không CASE, đọc thẳng `physical_state`.
- Read path: bỏ `LockSiteAsync`/`RecoverSiteAsync`/`PrepareAsync`/`AcquireAsync`/`PublishAsync`; dùng `ReadSnapshotAsync` (sau F3 đã bỏ luôn — xem §3.9).
- `Cache-Control: no-store, no-cache, must-revalidate`; `vehicleType` sai → 400 `INVALID_VEHICLE_TYPE`; `floor` sai → 400 `INVALID_FLOOR`; catch-all → 500 `INTERNAL_ERROR`.
- Legacy `/operator-layout` giờ gọi `ISlotOperatorAuthorizer.ScopeAsync` trước khi trả.

**17 rule suy ra:** đọc thẳng `physical_state`; NULL/empty reservation → `UNRESERVED`; giá trị lạ → `BACKUP`; physical lạ → `UNKNOWN`; `isBookable = AVAILABLE && UNRESERVED`; `isEvCharging = EV && OCCUPIED`; `sessionType`/`operatorContext` luôn `null`; `coordinates3d`/`features` parse thành object (`JsonElement`), malformed → null; `updatedAt` từ `parking_slots.updated_at`; floor/zone/block ancestry qua `parent_id`; `totalPhysicalCapacity` = số slot CAR; `occupiedCount` = mọi loại xe; `reservedCount` = chỉ RESERVED; `availableCarSlots` = CAR && AVAILABLE && UNRESERVED; motorcycle = sum `spatial_unit_capacities` `vehicle_type='MOTORCYCLE'`; `zoneCapacities` theo unit ZONE; `zoneOnlyReservedCount` = `allocatedSlotCount` = subtree RESERVED (**OPEN D0-5**); `generatedAt = UtcNow`; `isFresh = true` (**OPEN R3**, chưa có bảng heartbeat).

**Giả định có ghi chú:** `spatial_unit_capacities.spatial_unit_id` NOT NULL ⇒ không thể có row cấp site (demo seed có row MOTORCYCLE cấp ZONE, `max_capacity=2`); `supported_vehicle_type` NULL/lạ → `OVERSIZED`; `slot_type` lạ → `STANDARD`; `unit_type` lạ → `FLOOR`; thiếu floor ancestry → `floorId = Guid.Empty`, `floorCode = ""`.

### 3.6 Phase 1b — Dual-mode auth + redaction
**File mới:** `Application/OperationalLayoutAccess.cs`, `Application/OperationalLayoutRedaction.cs`, `Infrastructure/LayoutCallerResolver.cs`.
**File sửa:** `OperationalLayoutProjection.cs` (thêm `LayoutReadScope` + chữ ký reader), `PostgresOperationalLayoutReader.cs`, `ParkingStructureService.cs`, `Program.cs`.

- **Role chỉ lấy từ `GET /api/auth/me`** (session + live DB role re-validation). ParkingService **không** parse JWT cục bộ (đã kiểm: 0 match `JwtSecurityTokenHandler|ClaimsPrincipal|FindFirst`).
- Anonymous = header trắng → `(Anonymous, null, [], false)` với **0 outbound call**. Driver/Admin dừng sau `me`. Owner → `owner-scope` (`PARKING_MANAGE`); Operator → `slot-scope` (`SLOT_OVERRIDE` + assignment). 403 ở scope ⇒ Sanitized, không phải 403 ra ngoài.
- **Tenant lấy từ row site khớp**, không từ caller: `WHERE id=$1 AND deleted_at IS NULL AND ($2::uuid[] IS NULL OR tenant_id = ANY($2)) AND ($3::boolean OR status='ACTIVE')` (+ tenant liveness, xem F4). DTO không mang `tenantId` ⇒ không có oracle membership.
- **Redaction là một choke point duy nhất** — đúng một `return OperationalLayoutRedaction.Apply(...)` trong `ReadOperationalAsync`. Sanitized: `BACKUP→RESERVED`, `sessionType=null`, `operatorContext=null`, `features` allowlist, `isBookable` giữ giá trị trước mask, zone `name=null`/`allocatedSlotCount=0`, thứ tự/độ dài `slots[]` giữ nguyên.
- Telemetry trả nguyên instance (no-op).

### 3.7 Phase 3.1 — Fixture contract test
`tests/.../OperationalLayoutContractFixtureTests.cs` (280 dòng, 3 case: 2 `[Theory]` + 1 `[Fact]`) + link fixture qua `csproj` (`<None Include="..\..\..\FE\src\fixtures\..." Link="fixtures\..." CopyToOutputDirectory="PreserveNewest" />`), không copy đôi.

Assert: envelope + DTO deserialize; 48 slot / 48 `SlotCode` / 48 `SlotId` distinct; **ma trận 5×4 hard-code**; đủ 5 `PhysicalState` + 4 `ReservationState`; `slotCode` ≤32 khớp pattern và giữ nguyên `CarParkingSpace_008 2` / `CarParkingSpace_012 3`; metrics 48/13/9/16 + Zone_A 24/4 + Zone_B 24/9 + motorcycle 20/8/12 (literal); `isBookable == (AVAILABLE && UNRESERVED)`; NORMAL `isFresh=true` / STALE `false`; diff round-trip **đúng 2 path**; **clock cố định** `2026-10-26T12:00:00Z` (không dùng `UtcNow`).

### 3.8 Phase 3.2 — A2 + A7 unit test
- `OperationalStateMappingTests.cs` — 27 case phủ **mọi nhánh** của 2 hàm map, gồm cả fallback fail-closed.
- `OperationalLayoutRedactionTests.cs` — Telemetry no-op (same instance), mask BACKUP, null `sessionType`/`operatorContext`, allowlist `features`, metric/zone, `isBookable` frozen, không mutate input, **A7 trên fixture thật**.
- `OperationalLayoutReadPathTests.cs` — chứng minh `ReadOperationalAsync` **không** gọi `IStructureCommitments` (0 call) và masked counts chảy qua choke point.
- **A7 số thật (fixture 48 slot):** genuine `RESERVED = 9`, `BACKUP = 5`; sau mask slot-level `RESERVED = 14` (= 9 + 5); Telemetry `reservedCount = 9`, Sanitized `reservedCount = 14`; non-null `sessionType` 24→0, `operatorContext` 14→0.

### 3.9 Security re-audit 1b → F1–F4
| ID | Mức | Vấn đề | Xử lý |
|---|---|---|---|
| F1 | Medium | `slots[]` mask BACKUP→RESERVED nhưng `metrics.reservedCount` + `zoneOnlyReservedCount` vẫn là số thật ⇒ hiệu số trong **cùng một response** = số slot BACKUP | Sanitized dùng **masked counts** tính từ **toàn bộ row set trước filter**; Telemetry giữ số thật. Thêm `OperationalLayoutReadResult(Layout, MaskedReservedCount, MaskedZoneReservedCounts)` — **không** thêm field wire |
| F2 | Medium | `features` allowlist chỉ đúng với object: array/string/number đi qua nguyên vẹn; key hợp lệ copy nguyên value lồng nhau ⇒ `{"covered":{"internalNotes":...}}` lọt | Non-object → `null`; key hợp lệ chỉ nhận **primitive**; object/array bị drop |
| F3 | Low-Med | `commitments.ReadSnapshotAsync(...)` là **dead call** (bỏ return value) ⇒ Owner/Operator entitled bị 503 khi ReservationService chết mà không có lợi ích | **Xoá hẳn**; route không còn phụ thuộc ReservationService |
| F4 | Low | Site probe không kiểm tenant còn sống | Thêm `JOIN tenants t ... AND t.status='ACTIVE' AND t.deleted_at IS NULL` (mirror `EnsureTenantAsync`/`PrepareAsync`); site của tenant đã xoá → 404 `SITE_NOT_FOUND` |

Kèm 8 regression test pin F1/F2 + read path (xem §3.8, `OperationalLayoutReadPathsTests.cs`).

### 3.10 Phase 2 — Frontend binding
Chia **2 lane song song, file không giao nhau**: fixer = data layer, designer = visual/interaction. Module hợp đồng `FE/src/lib/operationalLayout.ts` do orchestrator viết trước để hai lane không tranh file; chỉ lane data được bổ sung.

- **2.1 `Record<Union, T>` thay if/else:** `operatorApi.ts` `siteFromStructure()` bỏ chuỗi if/else 5 nhánh, thay bằng 2 Record + default fail-closed. Bảng dẫn xuất thật:
  - physical: `AVAILABLE` → xét overlay reservation; `OCCUPIED` → `occupied`; `UNKNOWN` → `disabled`; `MAINTENANCE` → `maintenance`; `UNAVAILABLE` → `disabled`.
  - overlay (chỉ khi physical = `AVAILABLE`): `UNRESERVED` → `available`; `RESERVED`/`PROTECTED`/`BACKUP` → `reserved`.
  - **`SlotStatus` vẫn được populate** nhưng chỉ còn là giá trị trình bày dẫn xuất (~20 consumer cũ không đổi). Nguồn sự thật là `physicalState`/`reservationState`/`isBookable` mới thêm trên `OperatorSlot`.
- **2.2 Adapter 2 trục:** `PHYSICAL_STATE_VISUAL` và `RESERVATION_STATE_VISUAL` là 2 Record độc lập; `mapSlotVisualState` trả 6 trạng thái hình ảnh (`unknown` là fail-closed); `isSlotBookable` chỉ đúng với `AVAILABLE + UNRESERVED`. Operator sites giờ load qua `/operational-layout` (projection) thay vì `/operator-layout` (aggregate cũ).
- **2.3 Feature flag:** `VITE_OPERATIONAL_LAYOUT_FIXTURE` (mặc định `false` trong `.env.example`), chỉ có hiệu lực khi `=== 'true'` **và** `import.meta.env.DEV`; cờ tắt ⇒ HTTP thật y như trước; JSON fixture chỉ dynamic-import trong chế độ dev nên không vào bundle production.
- **2.4 Stale + reconnect:** ngưỡng **luôn** đọc `stalenessThresholdSeconds` từ response (không hard-code 30); thiếu/0/không parse được ⇒ coi là stale. `STALE_DIM_OPACITY = 0.7` = giảm 30%, áp trên canvas 3D và trên KPI/grid/list; banner `role="status"` + `aria-live="polite"`. Reconnect: listen `window online` + `document visibilitychange` → refetch, có guard chống gọi trùng; lỗi ⇒ **giữ snapshot cuối**, tuyệt đối không thay bằng fixture. Không SignalR (thuộc SPARK-244).
- **2.5 `slotCode` → mesh:** bỏ strip tiền tố, bỏ `slots[index]` fallback, bỏ slot tự chế `slot-auto-N`; bind **exact + case-sensitive + raw** qua `matchSlotCode`/`matchSlotCodes`; `SlotMatchResult` trả thêm `unmatchedSlotCodes`/`unmatchedMeshNames`; viewer log **1 lần mỗi lượt map** (tối đa 10 ví dụ + tổng số), có chữ ký chống log lặp mỗi frame.
- **A4 sửa muộn:** `SlotInspectionModal.tsx` trước đó render `UNKNOWN` = "VACANT". Giờ hiển thị `STATE UNKNOWN` + màu `unknown`, reservation hiển thị riêng (`None`/`Reserved`/`Protected hold`/`Backup hold`) và thêm dòng `Bookable: Yes/No`.
- **Màu mới:** `unknown` = `#7c6f8f` (violet-grey, không thể nhầm với xanh available), `unavailable` = `#334155` (slate đậm). available/occupied/reserved/maintenance/selected giữ nguyên. `slotMaterialManager` bỏ fallback `?? available` (trước là **fail-OPEN**) → giờ fail-closed về `unknown`.
- **Bug production đã sửa:** `OperatorBackupPanel.tsx` vẫn gọi `/operational-layout` nhưng type là `Structure` và `mark(slot.id)` ⇒ từ Phase 1b panel **hỏng thật** (nút disable vĩnh viễn, `POST /undefined/backup`). Giờ dùng `slotId`, enable theo `isBookable`/physical + `UNRESERVED`.
- **Nhãn slot hiển thị (lane designer, sau khi đóng 2.1–2.5):** `formatSlotLabel()` thêm vào `operationalLayout.ts` chỉ đổi **chuỗi hiển thị**; raw `slotCode` vẫn là khoá bind FR-MAP-03/A8. Map: `CarParkingSpace_008`→`CAR-008`, `CarParkingSpace_008 2`→`CAR-008-2`, `PARKING_EV_CAR_001`→`EV-001`, `CAR-01`/`MOTO-02` giữ nguyên, rỗng→`''`. Áp **17 call site / 8 file**: `LotStatus.tsx` 3 (L382, L379 aria-label, L425), `ParkingStructure.tsx` 5 (L241 confirm, L329 aria, L330 card, L344/L345 dialog), `SlotManagement.tsx` 3 (L286, L366, L429), `SlotInspectionModal.tsx` 1, `OperatorBackupPanel.tsx` 1, `Parking3DViewer.tsx` 1 (tooltip L1000), `OperatorOverview.tsx` 1 (L337), `BackupCapacityPanel.tsx` 1 (option label, `value` vẫn `slot.id`). **Giữ raw có chủ đích:** `number: slot.code` nạp vào viewer/`mapModelSlots`, predicate search, `layoutSlots` keys, `setReleasingSlot({id, code})`, `mark(slotId)`, payload `toBooking`, log `describeBindingDiagnostics`. Không thêm `title`; aria-label luôn khớp nhãn nhìn thấy.

### 3.11 Phase 3b — Merge `origin/develop` (Fix2FA + đổi tên service) và dựng lại stack

**Merge 1 — `048c1ad3`** (16 file, +589/−112): mang về `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`; đã review tuân thủ skill bằng `@oracle` (verdict `VIOLATIONS-FOUND`: 1 blocker = thiếu verify responsive, 4 should-fix) → đã sửa 2 lỗi code (`catch (OperationCanceledException) when (RequestAborted.IsCancellationRequested)`, `vehicleType` phải khớp canonical name để chặn `?vehicleType=1`), designer sửa KPI strip 7 ô + toggle nhóm 3D/2D/List cho mobile, và **sửa fixture test Postgres** để 11 test env-gated thực sự chạy được (nguyên nhân: fixture trỏ tới file schema đã bị xoá ở `cfb31ed4`).

**Merge 2 — `26c2f0e4`** (`origin/develop`, 33 commit, 339 file): đổi tên `ParkingService → FacilityService`, `ReservationService → OperationService`; test project `SmartParking.ParkingService.Tests → SmartParking.FacilityService.Tests`; chatbot sang `IoTService`. Giải 19 conflict, nguyên tắc: giữ **toàn bộ** code SPARK-228 của ta, lấy rename + schema list + 4 file FE test của develop.

**Dọn rác build (không xoá source):** `src/Services/ParkingService/`, `src/Services/ReservationService/`, `tests/Services/SmartParking.ParkingService.Tests/` chỉ còn `bin/obj` (gitignored) sau rename → đã xoá. `src/Services` còn đúng 5 service; **không** có service "cũ" nào.

**Dựng lại stack (Docker):**
- Áp 4 script schema mới vào **volume sẵn có** (idempotent; initdb của compose chỉ chạy khi volume rỗng): `05.13-Mfa-Lifecycle.sql`, `05.14-Identity-Access-Hardening.sql`, `05.15-Owner-Onboarding.sql` (user DB), `05.16-Parking-Owner-Onboarding.sql` (facility DB).
- `docker compose -f compose.app.yml up -d --build` → 5/5 image rebuild; `docker compose -f compose.app.yml exec -T frontend npm ci` (volume `fe-node-modules` thiếu `qrcode.react`, `@microsoft/signalr`) rồi restart frontend.
- `scripts/seed-demo-site.ps1` chạy lại OK: 48 slot (36 `CarParkingSpace_*` + 12 `PARKING_EV_CAR_*`), DEMO-HQ.

**3 lỗi dữ liệu dev (không phải lỗi code SPARK-228):**
1. `user-service` exit do `Development bootstrap email already belongs to a user with a different role` — account operator ở `PENDING_VERIFICATION` (luồng OperatorActivation của Fix2FA). Sửa: chạy lại `seed-demo-site.ps1` → account `ACTIVE`.
2. `user-service` exit do `57P03 database system is not yet accepting connections` — race lúc Docker Desktop khởi động; `up -d user-service` lại sau khi DB healthy.
3. **Cả 4 account demo login 401 `AUTH_FAILED`** — `DataSeeder.SeedRoleUserAsync` chỉ set `PasswordHash` khi **tạo mới** user, không bao giờ rehash user đã tồn tại; `.env` đã đổi mật khẩu ⇒ hash trong DB là mật khẩu cũ. Bằng chứng: `failed_login_attempts = 1` (đã qua lookup, fail ở `passwords.Verify`), hash `$2a$12$` dài 60 (đúng format). Sửa: rehash owner/operator/admin theo `.env` (BCrypt workFactor 12, khớp `BcryptPasswordService`), set `operator.email_verified_at` (bắt buộc để `AccountRolePolicy` trả role `operator`), tạo `driver@gmail.com`/`Password@123` (seed chỉ tạo driver khi DB rỗng). **Đây là dữ liệu dev — đổi `.env` lần nữa thì phải rehash lại.**

### 3.12 Vòng sửa cuối trước khi đóng Phase 3 (2026-10-30)

**Lỗi user báo (sau khi tự test tay FE + BE):** operator mark BACKUP xong, chuyển sang account **owner** thì (a) model 3D hiển thị chính slot đó như `reserved` (amber) thay vì BACKUP, (b) 2D slot view vẫn đúng *status* nhưng **viền vàng** thay vì màu của BACKUP.

**Nguyên nhân (đọc code, không suy đoán):** màn owner `FE/src/roles/owner/structure/ParkingStructure.tsx` gộp 2 trục về lại 1 `status` legacy:
- `RESERVATION_APPEARANCE_MAP` chỉ có `PROTECTED` và `PENDING_PAYMENT`; `BACKUP` rơi vào default `{ status: "reserved" }`.
- `modelSlots` dựng `{ id, number, floor: 1, status }` và **không** truyền `physicalState`/`reservationState`, nên `Parking3DViewer` → `resolveSlotVisualState` chỉ còn legacy status ⇒ amber.
- Card 2D render `data-status={appearance.status}`; `theme.css` chỉ có rule `[data-status="reserved"]` (amber) ⇒ viền vàng.

**Sửa (lane `des-1`, 2 file — không đổi backend, không đổi `operationalLayout.ts`):**
1. Thêm helper `slotAxes(slot)` dẫn xuất **một lần** cho cả 2 bề mặt: physical (`isPhysicallyOccupied`→OCCUPIED, `MAINTENANCE`/`UNKNOWN`/`BLOCKED`→tương ứng, `ACTIVE|AVAILABLE|OPERATIONAL`→AVAILABLE, còn lại→**UNKNOWN** fail-closed); reservation (rỗng/`NONE`→UNRESERVED, 3 giá trị hợp lệ giữ nguyên, còn lại→**BACKUP** fail-closed — phản chiếu `OperationalStateMapping`).
2. `slotAppearance` gọi `mapSlotVisualState({physicalState, reservationState})`; giữ `RESERVATION_APPEARANCE_MAP` trước (nhãn PROTECTED/PENDING_PAYMENT không đổi), rồi `visualState === "backup"` → `{ label: "Backup", status: "backup" }`.
3. `modelSlots` spread `...axes` ⇒ 3D đi nhánh 2 trục ⇒ cyan.
4. `theme.css` thêm `.parking-space[data-status="backup"] { border-color:#0891b2; background:#0891b21f; color:#0891b2; }` (đúng hex `SLOT_VISUAL_STATE_HEX.backup`, không tạo token mới).
5. Icon record thêm `backup: "clock"` (dùng lại bộ icon sẵn có).

**Verify:** `npm run build` (FE) exit 0 (1239 module, chỉ 2 warning có sẵn); `npx tsc --noEmit` → **3 lỗi có sẵn** của SPARK-228 + 1 lỗi có sẵn của develop `GroupedNumberInput.tsx(168,7) TS17001` (file không thuộc lane, byte-identical với HEAD); `node --test` 2 file layout → **26/26**; `git diff` = 2 file, 46 insertions / 7 deletions.

**Residue đã biết (không sửa):** giá trị legacy `PENDING_PAYMENT` map sang BACKUP (fail-closed) nên mesh sẽ cyan trong khi nhãn card vẫn "Pending payment"; thực tế `reservation_state` trong DB chỉ có `PROTECTED|RESERVED|BACKUP|NULL` nên đây là trường hợp lý thuyết.

**Script hoá việc sửa identity demo — `scripts/repair-demo-identity.ps1` (NEW):**
- Vì `DataSeeder.SeedRoleUserAsync` chỉ set `PasswordHash` khi **tạo mới** user, đổi `SMARTPARK_*_PASSWORD` trong `.env` không bao giờ có hiệu lực với row đã tồn tại ⇒ phải rehash tay (đã làm ở §3.11 mục 3). Script này làm lại việc đó một cách tái lập được.
- Dry-run mặc định: in danh sách account + **probe login thật** (`POST /api/auth/login`) cho cả 4 account.
- `-Apply`: sinh hash **bên trong PostgreSQL** bằng `pgcrypto` (`crypt(pw, gen_salt('bf', 12))` → `$2a$12$`, dài 60) nên **plaintext không lọt vào command line / process table**; đồng thời clear `failed_login_attempts`/`locked_until`, set operator `status='ACTIVE'` + `email_verified_at` (bắt buộc để `AccountRolePolicy` map `SITE_OPERATOR → operator`), và tạo `driver@gmail.com` + `accounts` + `account_roles('DRIVER')` nếu thiếu.
- Đã chứng minh tương thích: tạo 1 user tạm bằng hash pgcrypto → login **OK role=driver** → xoá user tạm. Sau đó chạy `-Apply` thật: 4/4 account login đúng role (`driver`/`owner`/`operator`/`admin`).
- Script chỉ sửa **dữ liệu demo dev**; không đổi schema, không đổi business state.

**Postman sau merge (API UserService đã đổi):** collection cũ fail 19 assertion vì `CreateOperatorDto` **bỏ hẳn `password`** (và có `[JsonUnmappedMemberHandling(Disallow)]`) ⇒ `0.7` trả 400 `"The JSON property 'password' could not be mapped to any .NET member"`; operator nay tạo theo **luồng invitation** (`POST /api/users` → `DeliveryId`/`DeliveryStatus:pending`, sau đó `POST /api/auth/bootstrap-password` với token 64-hex **chỉ tồn tại trong email** — `GET /api/auth/deliveries/{id}` chỉ trả `{Id,Kind,Status,Attempts,SentAt}` nên Postman **không thể** tự hoàn tất). Đã sửa artifact: bỏ `password` khỏi `0.7`; `0.8`/`4.3`/`8.3` SKIP khi thiếu `operatorNoOverrideToken`; `2.2` SKIP khi thiếu `expiredToken`; thêm biến `operatorNoOverrideId` (27 biến). Không nới assertion nào trên đường chạy được.

## 4. Technical Decisions
- **Decision:** SPARK-228 chỉ đọc; mọi mutation bị loại khỏi sprint. **Impact:** không cần MassTransit/SignalR.
- **Decision:** Giữ nguyên `FR-MAP-03` và `slotMatcher.ts`; mesh selection thuộc SPARK-238.
- **Decision:** Phase 1 **tách 1a/1b** (user chốt). 1a = projection + 2 trục + metrics + freshness + Cache-Control, operator-only; 1b = dual-mode + redaction.
- **Decision:** Read path **lock-free ngay trong Phase 1** (user chốt), sau đó **bỏ luôn snapshot** theo F3. **Reason:** `openapi.yaml:26` yêu cầu non-blocking; `OperatorLayoutService.cs:13-19` là pattern sẵn có. **Deviation:** không còn gọi `ReadSnapshotAsync` như quyết định ban đầu — snapshot không được dùng ở đâu.
- **Decision:** Sửa `openapi.yaml` sang union 3.1 (user chốt) để A1 validate strict được.
- **Decision:** Siết legacy `/operator-layout` cùng predicate `SLOT_OVERRIDE` (user chốt).
- **Decision:** `physicalState` đọc **trực tiếp** `parking_slots.physical_state`; `reservationState` map `NULL → UNRESERVED`.
- **Decision:** `Cache-Control` = `no-store, no-cache, must-revalidate` + `Vary: Authorization`.
- **Decision:** Giữ `CASE` ở aggregate query, tạo SQL riêng cho projection — để `GET /structure` byte-identical.
- **Decision:** Test fixture ở backend xUnit (tiêu chí "parse bằng DTO").
- **Decision (F1):** `metrics` **mask theo visibility** trong Sanitized. **Trade-off chấp nhận:** counter FE thấy cao hơn thực tế cho driver; operator vẫn suy ra được số BACKUP bằng cách diff 2 class (cần token operator — rào cản cao hơn nhiều).
- **Decision (F4):** tenant liveness áp cho **cả hai** mode; `$3` chỉ bypass site status, không bypass tenant liveness.
- **Decision (Phase 2.2):** giữ `SlotStatus` như **giá trị trình bày dẫn xuất**, không xoá. **Reason:** ~20 consumer cũ; R2 cấm gộp trục ở *nguồn sự thật*, không cấm tồn tại một giá trị tiện dụng. Hai trục luôn có mặt trên object.
- **Decision (Phase 2.4):** stale do **server** quyết (`stalenessThresholdSeconds`); FE không hard-code 30. Thiếu ngưỡng ⇒ fail-closed = stale.
- **Decision (Phase 2.5):** mesh bind **exact raw `slotCode`**. **Trade-off chấp nhận:** site thật dùng code kiểu `CAR-01` trong khi node GLB là `CarParkingSpace_###` ⇒ toàn bộ mesh hiện `unknown` kèm log cảnh báo. Đây là hệ quả đúng của A4/A8 (fail-closed), không phải lỗi — nhưng cần quyết định dữ liệu ở §6 F2.
- **Decision (nhãn slot, user chốt 2026-10-29):** giữ bind exact raw `slotCode`, **chỉ format nhãn hiển thị** qua `formatSlotLabel()`. **Reason:** đổi seed sang `car_01` không cứu được 3D (node GLB vẫn `CarParkingSpace_###`), `ParkingStructure.AddSlot` upper-case (`Domain/ParkingStructure.cs:167`) nên `car_01` không tạo được qua API, và đổi fixture sẽ phá vector A8. Đổi tên node GLB hoặc chỉnh ingest để SPARK-238.

## 5. Files Changed
| File | Change |
|---|---|
| `docs/03-design/api/3d-digital-twin-openapi.yaml` | 12 node nullable → union 3.1 |
| `src/.../API/Program.cs` | Đăng ký reader + resolver; endpoint mới (cache header, filter, catch-all); legacy gate |
| `src/.../Application/ParkingStructureService.cs` | `ReadOperationalAsync` orchestration; single redacted return; bỏ snapshot |
| `src/.../Persistence/PostgresParkingStructureRepository.cs` | Comment-only (+3 dòng) |
| `tests/.../SmartParking.ParkingService.Tests.csproj` | Link 2 fixture |
| `src/.../Application/OperationalLayoutProjection.cs` | **NEW** — DTO + enum + `LayoutReadScope` + `OperationalLayoutReadResult` |
| `src/.../Application/OperationalLayoutAccess.cs` | **NEW** — caller kind/visibility/caller/resolver |
| `src/.../Application/OperationalLayoutRedaction.cs` | **NEW** — redaction + features allowlist |
| `src/.../Application/OperationalStateMapping.cs` | **NEW** — map 2 trục (seam testable) |
| `src/.../Infrastructure/LayoutCallerResolver.cs` | **NEW** — resolve role qua `/api/auth/me` |
| `src/.../Persistence/PostgresOperationalLayoutReader.cs` | **NEW** — projection SQL + metrics + masked counts |
| `tests/.../OperationalLayoutContractFixtureTests.cs` | **NEW** — Phase 3.1 |
| `tests/.../OperationalStateMappingTests.cs` | **NEW** — A2, 27 case |
| `tests/.../OperationalLayoutRedactionTests.cs` | **NEW** — A7, 14 fact |
| `tests/.../OperationalLayoutReadPathTests.cs` | **NEW** — F3 regression |
| `FE/src/lib/operationalLayout.ts` | **NEW** — hợp đồng 2 trục + `Record` map + `mapSlotVisualState`/`isSlotBookable`/`isLayoutStale`/`matchSlotCode(s)`/`formatSlotLabel` |
| `FE/src/roles/owner/structure/ParkingStructure.tsx` | Nhãn `formatSlotLabel` ở 5 chỗ hiển thị (card, confirm, dialog, aria-label); **§3.12:** thêm `slotAxes()` fail-closed 2 trục + `mapSlotVisualState` trong `slotAppearance` + spread axes vào `modelSlots` + icon `backup` |
| `FE/src/roles/owner/dashboard/BackupCapacityPanel.tsx` | Nhãn option `formatSlotLabel` (value vẫn `slot.id`) |
| `FE/src/lib/parkingApi.ts` | Thêm `operationalLayout(id, opts?)` + feature flag fixture (dev-only) |
| `FE/src/lib/operatorApi.ts` | Bỏ if/else → `Record`; `OperatorSlot` thêm 2 trục + `isBookable`; load qua projection |
| `FE/src/lib/3d/slotMatcher.ts` | Bind exact raw `slotCode`, bỏ index fallback + slot tự chế, trả unmatched |
| `FE/src/lib/3d/parking3DConfig.ts` | `SLOT_VISUAL_STATE_COLORS/HEX/LABELS` (6 trạng thái) |
| `FE/src/lib/3d/slotMaterialManager.ts` | Bỏ fallback fail-open → fail-closed `unknown` |
| `FE/src/components/3d/Parking3DViewer.tsx` | 2 trục + legend 6 trạng thái + dim 30% + banner + log unmatched |
| `FE/src/components/ui/StaleDataBanner.tsx` | **NEW** — banner `role=status` + helper thuần test được |
| `FE/src/roles/operator/parking-lot/useLotStatus.ts` | Filter/đếm 2 trục + reconnect + stale |
| `FE/src/roles/operator/parking-lot/LotStatus.tsx` | KPI/legend/filter 2 trục + dim + banner |
| `FE/src/roles/operator/parking-lot/SlotInspectionModal.tsx` | A4: `UNKNOWN` không còn hiện "VACANT" |
| `FE/src/roles/operator/dashboard/OperatorBackupPanel.tsx` | Retype projection, `mark(slotId)` |
| `FE/.env.example` | `VITE_OPERATIONAL_LAYOUT_FIXTURE=false` |
| `tests/frontend/operational-layout-adapter.test.cjs` | **NEW** — 15 test (ma trận 20/20, fail-closed, stale, slotCode) |
| `tests/frontend/operational-layout-visual.test.cjs` | **NEW** — 9 test (màu, dim, banner, predicate 2 trục) |
| `FE/src/theme.css` | **§3.12** — rule `.parking-space[data-status="backup"]` (cyan `#0891b2`, nền `#0891b21f`) |
| `scripts/repair-demo-identity.ps1` | **NEW (§3.12)** — dry-run + `-Apply`; hash bằng `pgcrypto` trong DB, probe login 4 account, tạo `driver@gmail.com` nếu thiếu, set operator `ACTIVE` + `email_verified_at` |
| `docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_collection.json` | **§3.12** — bỏ `password` khỏi request `0.7` (`CreateOperatorDto` không còn field này, `Disallow` unmapped); `0.8`/`2.2`/`4.3`/`8.3` chuyển sang SKIP khi thiếu token thay vì fail; thêm biến `operatorNoOverrideId` |
| `docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_environment.json` | **§3.12** — 27 biến (thêm `operatorNoOverrideId`); password owner/operator/admin vẫn để rỗng, nạp qua `--env-var` từ `.env` |
| `docs/04-testing/postman/SPARK-228-manual-test-guide.md` | **§3.12** — cập nhật run-book Newman: nạp password qua `scripts/load-env.ps1` + `--env-var`, ghi rõ 2 đường lấy `operatorNoOverrideToken` / `expiredToken` |
| `ai/tasks/active/SESSION-20261028-spark-228-planning.md` | File này |

## 6. Issues and Blockers

**Đã đóng:**
- D0-1 read path → chốt + làm (và bỏ snapshot theo F3).
- D0-2 tách phase → 1a/1b.
- D0-3 openapi nullable → sửa YAML.
- D0-4 legacy route → siết `SLOT_OVERRIDE`.
- F1/F2/F3/F4 (BE) → đã sửa + có regression test.
- B3 + D1 → đã sửa trong cả 2 design doc (lane doc-defect): telemetry nay ghi rõ cần assignment **AND** `SLOT_OVERRIDE`; refs `Program.cs` đã đúng (`/operational-layout` :145–180, `/operator-layout` :203–209); thêm `INVALID_FLOOR`/`IDENTITY_UNAVAILABLE` vào catalog §7.1; §3.5 bỏ mô tả `ReadSnapshotAsync`.
- **F2 (FE) → đã đóng bằng dữ liệu**: seed demo nay dùng đúng tên node GLB (36 `CarParkingSpace_*` + 12 `PARKING_EV_CAR_*`), nên bind exact không còn xám toàn bộ. Rule bind (không cắt tiền tố, không fallback index) **giữ nguyên**.
- **Postman 401 toàn bộ (user báo)** → nguyên nhân: environment ship account placeholder (`*@example.test`, `replace-with-*`) và 2 account không tồn tại. Đã sửa: account thật (`driver@gmail.com`, `owner@smartpark.local`, `operator@smartpark.local`, `admin@smartpark.local`), mật khẩu owner/operator/admin để trống + mô tả key `.env`, operator-no-override được **tạo qua API** bằng owner token, owner-other-tenant và `expiredToken` thành bước SKIP không fail, login script không còn `TypeError` (in body khi fail).
- **Filter không tác dụng ở 3D** → `LotStatus.tsx` truyền `site.slots` cho `Parking3DViewer`; đã đổi sang `filteredSlots` + tự clear selection khi slot bị ẩn.
- **"Backup Slots & Buffer" không chọn được slot** → `eligibleSlots` còn so `reservationState === 'NONE'` trong khi Phase 2 đã chuẩn hoá thành `UNRESERVED` (luôn khác null) ⇒ danh sách rỗng. Đã sửa sang `physicalState === 'AVAILABLE' && reservationState === 'UNRESERVED' && isBookable === true` (fail-closed), và bỏ đọc legacy `operationalStatus`/`isPhysicallyOccupied`.
- **Slot xe máy sai bản chất** → đã bỏ `MOTO-01`/`MOTO-02` khỏi seed; xe máy chỉ còn là capacity (`spatial_unit_capacities` MOTORCYCLE = 2) trên vùng vàng của model, không quản lý theo slot.

**Còn mở:**
- **D0-5 — Field chưa có nguồn:** `zoneCapacities[].allocatedSlotCount` (= `zoneOnlyReservedCount`, và = 0 khi Sanitized), `floors[].level` (thứ tự theo tên), `slots[].rowCode` (tên BLOCK gần nhất), `sessionType`/`operatorContext` (luôn `null` — không có nguồn trong ParkingService). Đã emit best-effort + comment `// OPEN`. **Chấp nhận cho Phase 2** (FE render `null`/giá trị suy ra, không vỡ).
- **B1 — ĐÃ ĐÓNG (2026-10-30):** stack Docker dựng được, gọi thật 4 token, chạy endpoint end-to-end; Postman 33 request / 123 assertion / 0 fail (§7).
- **Owner thấy BACKUP thành `reserved` (user báo 2026-10-30) → đã sửa (§3.12):** `ParkingStructure.tsx` gộp 2 trục về 1 `status` legacy, `BACKUP` rơi vào default `reserved` (amber) và không truyền axes cho 3D; 2D thiếu rule CSS `[data-status="backup"]`. Nay 3D + 2D cùng cyan `#0891b2`.
- **B4 — `isFresh` là prototype `true`:** không có bảng heartbeat. Phải ghi rõ là prototype (R3). Đây là lý do banner stale chưa từng bật trên dữ liệu thật.
- **B6 — Delta nghiệp vụ:** `OCCUPIED + PROTECTED` giữ `PROTECTED` tới khi xe rời; test A3 không được assert "PROTECTED chưa từng thành RESERVED".
- **D2 — `metrics.reservedCount` phụ thuộc visibility:** trong Sanitized nó là masked count (14), không phải số thật (9). Đây là deviation có chủ đích so với câu chữ hợp đồng ("count exactly RESERVED"); FE phải coi `metrics` là số hiển thị, không phải số liệu DB.
- **D3 — Mã lỗi ngoài catalog:** `IDENTITY_UNAVAILABLE` (503) và `INVALID_FLOOR` (400) không có trong catalog openapi. Giữ nguyên thay vì "nói dối" bằng `COMMITMENTS_UNAVAILABLE`.
- **D4 — `zoneCode` trùng giữa 2 floor** sẽ gộp masked count khi nhóm theo `zoneCode`. Schema cho phép; chấp nhận.

**Mở ra ở Phase 2 (FE):**
- **F1 (FE) — harness frontend: 2 file xanh, 2 file đỏ do drift UI cũ.** Đã sửa plumbing: `auth-api` **21/21** và `auth-context` **9/9** nay xanh (trước đó đỏ vì `require is not defined` trong sandbox `vm` và thiếu stub `DriverOtpForm`/`matchMedia`). `sign-in` **17/21** và `operator-provisioning` **1/22** vẫn đỏ vì test viết cho UI **trước** refactor 2026-10-27: `SignIn.tsx` bỏ `<select>` mode (giờ nhận prop `flow`) và đổi message validation sang email-only; `CreateOperatorForm` đổi id `operator-*`→`op-*` và cần prop `isOpen`; `SiteForm` bỏ input `site-code`; `OperatorManagement` đổi thứ tự effect; copy `No parking sites are available for this account.` Không có bug sản phẩm nào bị phát hiện. **Đã chốt (2026-10-29): giữ đỏ, chỉ ghi nhận drift** — không viết lại assertion trong scope SPARK-228 (sửa sẽ biến hành vi hiện tại thành "chuẩn" mà chưa chốt ý định). Hệ quả: `node --test tests/frontend/*.test.cjs` = **72/97**, **không xanh**; đọc mục này trước khi kết luận có regression.
- **F2 (FE) — code site thật ≠ tên node GLB:** demo seed dùng `CAR-01...`, GLB dùng `CarParkingSpace_###`. Bind exact ⇒ màn 3D sẽ **xám toàn bộ** (đúng fail-closed) + log cảnh báo. **Đã chốt (2026-10-29): giữ fail-closed + log, không nới rule; việc chỉnh dữ liệu seed/ingest để sprint khác (SPARK-238).** A8 chỉ chứng minh được trên fixture 48 slot.
- **F3 (FE) — owner vẫn đi đường aggregate:** `loadOperatorSites` cho role `owner` dùng `parkingApi.list()` + `parkingApi.structure(id)`; aggregate không mang đủ 2 trục nên mapping owner phải suy từ `operationalStatus`/`isPhysicallyOccupied` (đường suy luận cũ). Operator đã chuyển sang projection. **Đã chốt (2026-10-29): chấp nhận là giới hạn đã biết** — tiêu chí 2.2 chỉ được tuyên bố đúng cho operator; chuyển owner sang `/operational-layout` để follow-up (cần xác nhận owner nhận 200 telemetry trên route này — Postman folder `05 - Owner / Admin`).
- **F4 (FE) — cảnh báo build:** `INEFFECTIVE_DYNAMIC_IMPORT` cho `operationalLayout.ts` (dynamic import bởi `parkingApi.ts`, static import ở 6 nơi khác). Không ảnh hưởng chức năng (JSON fixture vẫn lazy); chỉ là nhiễu log build.
- **F5 (FE) — ĐÃ ĐÓNG (2026-10-30):** user tự test tay FE+BE, xác nhận chạy ổn. Hai lỗi còn lại (BACKUP hiển thị amber ở 3D, viền vàng ở 2D màn Owner) đã sửa ở §3.12; chỉ chờ user xác nhận lại lần cuối.
- **F6 (FE) — type drift có sẵn (không sửa):** `OperatorDashboard.tsx` dùng `SlotStatusFilter` mà không import; `SiteManagement.tsx` (`setMessage`); `useSiteManagement.ts` (`reassignSite`). `vite build` không typecheck nên build vẫn xanh.
- **F7 (FE) — `node_modules` bị sửa ngoài git:** lane designer phải chạy `npm install --no-save --package-lock=false motion@14.6.0` vì `motion` thiếu trong `node_modules`; `FE/package.json` và `package-lock.json` **không đổi**. Ai review cần biết môi trường đã bị chạm.
- **F8 (FE) — search trong LotStatus vẫn so khớp raw code:** `useLotStatus.ts` filter theo `slot.number` (raw), trong khi UI đã hiển thị nhãn rút gọn ⇒ gõ `CAR-008` không tìm thấy (`008` hoặc `car` vẫn được). Chấp nhận tạm; sửa cần chạm hook/data layer nên để follow-up.
- **F9 (FE) — `operator-provisioning.test.cjs` đổi lý do fail:** 4 case `ParkingStructure` giờ abort lúc load với `Unexpected import: ../../../lib/operationalLayout` (harness `vm` dùng import map cố định). Số case vẫn 1 pass / 21 fail. Sửa 1 dòng mỗi loader (`'../../../lib/operationalLayout': load('lib/operationalLayout.ts', {})`) sẽ trả về đúng lý do cũ — **không làm** vì user đã chốt giữ đỏ/drift.

**Tồn tại từ trước:**
- Chưa có MassTransit/publisher/consumer/SignalR — thuộc SPARK-244.
- `FR-MAP-03` đang dùng làm trace cho hành vi Operator (SPARK-244 AC-7). Không sửa.
- Mojibake `â€` ở một số file `docs/01-requirements`; không ảnh hưởng code.

## 7. Validation Results
Chạy thật trong session này:
- **Baseline trước khi sửa:** `dotnet build SmartParking.slnx --nologo -v q` → **0 Warning / 0 Error**, 58.65 s.
- **Phase 1a:** build → **0/0** (11.71 s); `dotnet test tests/Services/SmartParking.ParkingService.Tests --nologo --no-build` → **48 passed / 9 skipped / 57**.
- **Phase 3.1:** filtered **3/3**; full → **51 passed / 9 skipped / 60**; build **0/0**.
- **Sau mapping extraction:** build 0/0; **51 / 9**.
- **Phase 1b:** build **0/0**; **51 passed / 9 skipped**.
- **Phase 3.2:** filtered **34/34**; full → **85 passed / 9 skipped / 94**; build **0/0**.
- **Sau F1–F4:** build **0/0**; **85 passed / 9 skipped / 94**.
- **Phase 3.2b (+8 case):** filtered **15/15**; full → **93 passed / 9 skipped / 102**; build **0/0**.
- **Phase 2 (FE):** `npm run build` trong `FE/` → **✓ built** (1239 module, 7.14 s; warning `INEFFECTIVE_DYNAMIC_IMPORT` + chunk > 500 kB có sẵn); `npx tsc --noEmit -p tsconfig.json` → **đúng 3 lỗi có sẵn**, không phát sinh lỗi mới (§6 F6 — `vite build` không typecheck nên phải chạy riêng); `node --test` 2 file layout → **24 passed / 0 failed** (15 + 9).
- **Phase 2 — toàn bộ glob test frontend:** `node --test tests/frontend/*.test.cjs` → **97 test, 72 pass, 25 fail**. Xanh: `auth-api` 21/21, `auth-context` 9/9, `operational-layout-adapter` 15/15, `operational-layout-visual` 9/9. Đỏ: `sign-in` 17/21, `operator-provisioning` 1/22 — drift UI cũ, xem §6 F1. **User chốt: giữ đỏ, chỉ ghi nhận.**
- **Nhãn slot hiển thị (orchestrator re-verify lane designer):** `npm run build` trong `FE/` → **exit 0**, `✓ built in 3.63 s` (1239 module, vẫn 2 warning đã chấp nhận); `npx tsc --noEmit -p tsconfig.json` → **đúng 3 lỗi có sẵn**, `TSC_EXIT=2` không đổi; `node --test` 2 file layout → **24 passed / 0 failed**. Review `git diff` 4 file hiển thị nhỏ: chỉ thay biểu thức render bằng `formatSlotLabel(...)`, `value`/`slot.id`/`slot.code` dùng cho dữ liệu **không** bị viết lại.
- **Final:** `dotnet build SmartParking.slnx --nologo` → **Build succeeded, 0 Warning(s), 0 Error(s)**; `dotnet test tests/Services/SmartParking.ParkingService.Tests --nologo` → **93 passed / 9 skipped / 102 total**. 9 skip là test Postgres/gate có sẵn (thiếu fixture).
- **Harness ngoài repo** (không nằm trong source): reader chạy trên `facility-db` local → `ANON_ACTIVE=OK`, `ADMIN_INACTIVE_ALLOWED=OK`, `OWNER_TENANT=OK`, `WRONG_TENANT=ERROR SITE_NOT_FOUND`; resolver anonymous `calls=0`; redaction `SANITIZED_RESERVED=14`, `TELEMETRY_RESERVED=9`, `NON_OBJECT_FEATURES=null`; site probe SQL chạy được qua `psql`.

**Chưa chạy:** ~~endpoint end-to-end với token thật~~ → **ĐÃ CHẠY (2026-10-30)**, B1 đóng.

- **Newman (re-verify sau vòng sửa §3.12, chạy thật trong turn này):** `newman run` với password nạp qua `--env-var` từ `.env` (`scripts/load-env.ps1`) → **33 requests / 123 assertions / 0 failed / 1 iteration**. Các case thiếu tiền đề (`operatorNoOverrideToken`, `expiredToken`, `ownerOtherTenantEmail`) SKIP đúng theo thiết kế, không fail. Login 4 account (driver/owner/operator/admin) đều thành công.

- **Phase 3 — endpoint test (lane bac-1 + orchestrator re-verify):** `dotnet build SmartParking.slnx --nologo -v q` → **0 Warning / 0 Error, 6.79 s**; `dotnet test ... --filter "FullyQualifiedName~OperationalLayoutEndpointTests"` → **10/10**; full → **103 passed / 9 skipped / 112**.
- **Vòng sửa sau test tay của user (orchestrator re-verify, cuối cùng):** `dotnet build SmartParking.slnx --nologo -v q` → **0 Warning / 0 Error, 20.51 s**; `dotnet test ... --no-build` → **103 passed / 9 skipped / 112**; `npm run build` (FE) → **✓ built in 2.79 s** (1239 module, 2 warning có sẵn); `npx tsc --noEmit` → **đúng 3 lỗi có sẵn**; `node --test` 2 file layout → **24 passed / 0 failed**.
- **Seed mới (đếm tĩnh trên file):** `CarParkingSpace_*` 72 match = 36 hàng × 2 statement; `PARKING_EV_CAR_*` 24 match = 12 hàng × 2; **0** hàng `MOTO-*`; `seed-demo-site.ps1:208` → `Expected @('1','2','48','2','48')`. **Chưa chạy SQL** (AI không tự thực thi SQL).
- **Postman (re-parse):** 33 request / 9 folder / 26 biến; `driverEmail=driver@gmail.com`, `driverPassword=Password@123`, `ownerEmail=owner@smartpark.local`, `operatorEmail=operator@smartpark.local`, `adminEmail=admin@smartpark.local`, `ownerPassword` rỗng (lấy từ `.env`).

- **Phase 3b (sau merge develop, chạy thật trong turn này):**
  - `dotnet build SmartParking.slnx --nologo` → **0 Warning / 0 Error** (40.23 s).
  - `dotnet test tests/Services/SmartParking.FacilityService.Tests` **không** env → 112 passed / 13 skipped / 125; **có** `SMARTPARK_STRUCTURE_TEST_CONNECTION` → **124 passed / 1 failed / 125, 0 skipped**. Fail duy nhất `ReactivatingSitePublishesStateBeforeCapacityWithBackup` (`23514 Resource is removed or inactive`) — **đã chứng minh pre-existing trên develop** bằng `git worktree` sạch ở `origin/develop` (cũng fail y hệt).
  - Docker: 5/5 container healthy, `/health` = 200 trên **5035 / 5045 / 5055 / 5065**; log 10 phút gần nhất của 4 service: **0** dòng `42P01` / `42703` / `Unhandled exception`.
  - Login thật: `driver` → role `driver`; `owner` → `owner`; `operator` → `operator`; `admin` → `admin` (token ~658 ký tự).
  - **Smoke `GET /api/parking-lots/{id}/operational-layout`** (site DEMO-HQ `b0000000-0000-4000-8000-000000000001`):
    - Anonymous → **200**, `slots=48`, `contractVersion=1.2.0`, `isFresh=true`, header `Cache-Control: no-store, no-cache, must-revalidate`, `Vary: Authorization`.
    - Driver → **200 Sanitized**: `BACKUP=0`, `sessionType`/`operatorContext` toàn `null`.
    - Operator (`SLOT_OVERRIDE`) → **200 Telemetry**: thấy `BACKUP` thật.
    - A7: sau khi mark 1 backup, `sanitized.reservedCount (2) == telemetry.reservedCount (0) + số BACKUP (2)` → **đúng**.
    - Token sai → **401**; site không tồn tại → **404**; legacy `/operator-layout` + driver → **403**, + operator → **200**.
    - `POST .../slots/{slotId}/backup` → 200; `DELETE .../backup` → 200 (trạng thái trả lại như cũ).
  - FE: `http://localhost:5173` → 200; Vite transform `src/main.tsx` (1980 B) và `src/lib/operationalLayout.ts` (21662 B) → 200; sau `npm ci` đã có `qrcode.react`, `@microsoft/signalr`, `motion`.

## 8. Git Status
- Branch: `feat/SPARK-228-3D_Model_BE` (upstream `origin/feat/SPARK-228-3D_Model_BE`), **ahead 43 commit, chưa push**.
- Lịch sử commit của sprint:
  - `a2176840 feat(parking): project operational layout read path with redaction [SPARK-228]` — Phase 1 (20 file, +3119/−42).
  - `048c1ad3 Merge remote-tracking branch 'origin/develop'` — lần 1, mang coding skill (16 file, +589/−112).
  - `27f11f31 feat(3d-twin): bind operational layout read path and harden structure holds [SPARK-228]` — Phase 2 + Phase 3 gộp (46 file, +4037/−561).
  - `26c2f0e4 Merge remote-tracking branch 'origin/develop'` — lần 2 (rename FacilityService/OperationService).
- Working tree hiện tại (**2026-10-30, chưa commit** — chờ user test lần cuối):
  - `M FE/src/roles/owner/structure/ParkingStructure.tsx` — §3.12 (+46/−7)
  - `M FE/src/theme.css` — §3.12 (+4)
  - `M docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_collection.json` — bỏ `password` ở `0.7`, SKIP hoá `0.8`/`2.2`/`4.3`/`8.3`
  - `M docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_environment.json` — +`operatorNoOverrideId`
  - `M docs/04-testing/postman/SPARK-228-manual-test-guide.md` — run-book Newman
  - `?? scripts/repair-demo-identity.ps1` — script dev mới
  - `?? ai/tasks/active/SESSION-20261028-spark-228-planning.md` — file này
- **Cố ý không commit** session file: `ai/tasks/README.md` quy định `active/` chỉ giữ `.gitkeep`; chỉ track khi đạt DoD và chuyển sang `ai/tasks/completed/`.

## 9. Handoff / Next Steps
1. **Phase 2/3 đã commit** trong `27f11f31` (gộp) và merge `26c2f0e4`. Không còn việc commit tồn đọng. Nếu cần push: branch đang **ahead 43**, chưa push — chờ user quyết định.
  2. **B1 — đã đóng (2026-10-30):** chạy thật với stack Docker + 4 token. Kết quả anonymous/driver/operator/401/404/legacy đều đúng (§7). **Postman đã chạy trọn bộ: 33 request / 123 assertion / 0 fail** (§7) — A7 hai token nay có evidence.
  2b. **Vòng sửa cuối §3.12 — user cần test lại 2 điểm:** (a) màn **Owner → 3D**: slot BACKUP phải **cyan**, không còn amber `reserved`; (b) màn **Owner → 2D slot view**: viền + chữ cyan `#0891b2`, không còn viền vàng. Không đổi gì ở màn operator. Nếu không có slot BACKUP để xem, dùng script SQL demo ở item 8 (đặt `physical_state='AVAILABLE', reservation_state='BACKUP'` cho `CarParkingSpace_015`).
3. **F2 — đã chốt (2026-10-29):** giữ fail-closed + log, không nới rule bind; chỉnh dữ liệu seed/ingest để sprint khác (SPARK-238).
  4. **Verify bằng mắt Phase 2 — đã test tay bởi user (2026-10-30):** user xác nhận FE + BE chạy ổn, chỉ còn 2 lỗi màn Owner (đã sửa §3.12, chờ test lại). Màu `unknown`/`unavailable`, dim 30%, banner stale, legend/filter 2 trục, modal `STATE UNKNOWN` đã được user xem trực tiếp.
5. **F3** — cân nhắc chuyển owner path (`loadOperatorSites` role owner) sang `/operational-layout` thay vì aggregate, nếu owner thực sự nhận 200 telemetry. Hiện owner vẫn suy 2 trục từ aggregate (đường cũ) ⇒ tiêu chí 2.2 mới đúng cho operator.
6. **F1 — đã được develop giải quyết:** develop viết lại 4 file `tests/frontend/*.test.cjs`, merge lấy bản của develop. Hiện `node --test tests/frontend/*.test.cjs` → **120 pass / 10 fail**, cả 10 fail nằm ở `operator-provisioning.test.cjs` (import map của develop thiếu `../dashboard/BackupCapacityPanel`). Không còn là drift do SPARK-228.
7. Chuyển file này sang `ai/tasks/completed/` khi SPARK-228 xong (và lúc đó mới được track).
8. **Script SQL demo 6 trạng thái — AI KHÔNG chạy** (authority boundary: không tự thực thi SQL). User chạy trên `facility-db`:
   - Ids: `site_id='b0000000-0000-4000-8000-000000000001'`, `tenant_id='a0000000-0000-4000-8000-000000000001'` (`scripts/seed-demo-site.ps1:26-30`).
   - **Cập nhật 2026-10-29:** mã `CAR-01..MOTO-02` **đã bị xoá** khỏi seed. Dùng mã mới: `CarParkingSpace_001`, `_002`, `PARKING_EV_CAR_001`, `PARKING_EV_CAR_002`, … Lưu ý `slot_code` là **mixed-case** ⇒ so sánh phải exact (`s.slot_code = d.slot_code`), **không** dùng `upper()`.
   - Nội dung (ví dụ 6 slot, đổi mã tuỳ ý): `WITH demo(slot_code, physical_state, reservation_state) AS (VALUES ('CarParkingSpace_001','MAINTENANCE',NULL),('CarParkingSpace_002','UNKNOWN',NULL),('CarParkingSpace_013','UNAVAILABLE',NULL),('CarParkingSpace_014','OCCUPIED',NULL),('CarParkingSpace_015','AVAILABLE','BACKUP'),('PARKING_EV_CAR_001','AVAILABLE','RESERVED')) UPDATE parking_slots s SET physical_state=d.physical_state, reservation_state=d.reservation_state, updated_at=now() FROM demo d WHERE s.site_id='b0000000-0000-4000-8000-000000000001' AND s.deleted_at IS NULL AND s.slot_code=d.slot_code RETURNING s.slot_code, s.physical_state, s.reservation_state;`
   - Chạy: `Get-Content .\slot-states.sql -Raw | docker compose -f compose.schema-integration.yml exec -T facility-db psql -U smartpark_facility -d smartpark_facility -v ON_ERROR_STOP=1`
   - Revert: `UPDATE parking_slots SET physical_state='AVAILABLE', reservation_state=NULL, updated_at=now() WHERE site_id='b0000000-0000-4000-8000-000000000001' AND deleted_at IS NULL;`
   - Lưu ý: bypass domain + ReservationService (**demo only**); `OCCUPIED` ghi `reservation_state=NULL` cho khớp đường gate; 3D nay **bind được** vì mã trùng tên node GLB; nếu owner `/structure` ném `CAPACITY_CONFLICT` (aggregate `View()` tính `available<0`) thì revert và đổi ít slot hơn.

## 10. Context for Next Session
- Hợp đồng là nguồn chuẩn: hub `docs/03-design/api/3d-digital-twin-api-design.md` và bản 228; nếu lệch, hub thắng — trừ khi đã ghi deviation có chủ đích trong §6.
- Số hiệu mục không đánh lại: mục nào vắng trong 228 thì thuộc hub.
- Trạng thái §3.2 đã chốt: giai đoạn 2 giữ `OCCUPIED + PROTECTED`; hold dùng `slot_allocations.allocation_status='RESERVED'`; cửa sổ `T−15m → T−5m`, cadence 5 phút + event-driven; hết `BACKUP` thì huỷ + hoàn trả + notify; enum chỉ 3 giá trị `RESERVED/OCCUPIED/RELEASED`.
- Không sửa trace `FR-MAP-03`. Không thêm endpoint ghi vào 228.
- Encoding khi viết tài liệu: hub/228/244/openapi/memo/README = UTF-8 **không BOM**; `docs/01-requirements/**` = UTF-8 **có BOM**.
- **Hai route đọc cùng chủ đề:** `/operational-layout` (đăng ký `Program.cs:145`, handler 145–180 — projection mới, dual-mode) và `/operator-layout` (đăng ký `Program.cs:203`, handler 203–209 — raw aggregate, gate `SLOT_OVERRIDE`). Số dòng `Program.cs` dễ trôi sau mỗi lần sửa — đọc lại trước khi trích dẫn.
- **Fixture là vector test chuẩn:** 48 slot, 20/20 tổ hợp, `slotCode` = mesh thô (giữ `CarParkingSpace_008 2`), genuine `RESERVED=9` + `BACKUP=5` ⇒ post-mask `RESERVED=14`.
- **Mount point test:** `tests/Services/SmartParking.ParkingService.Tests/` — flat `*Tests.cs`; fixture được link vào output qua `csproj`, không copy đôi.
