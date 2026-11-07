# SPARK-238 — Branch Verification & Manual Test Guide

Branch: `feat/SPARK-238-facility-structure-port` (base `origin/develop` = `ea0d212c`)
Scope: FR-LOT-02 / UC-LOT-01 / US-OW01 — Owner facility structure configuration.

> Status legend: **VERIFIED** = đã chạy thật và có kết quả ghi lại. **TO RUN** = cần bạn chạy. **BLOCKED** = không thể pass ở branch này vì phụ thuộc service khác.

> **Checklist đã được sắp xếp lại cho dễ theo dõi:** C5 = luồng chọn mesh 3D để đăng ký; C6 = luồng end-to-end (đăng ký → đặt chỗ → quan sát, rồi vòng đời maintenance window). Ma trận 7 panel trước đây nằm ở **C9**. C10 là các mục **BLOCKED / out-of-scope** cần xác nhận là "đúng như đã ghi nhận", không phải regression.

---

## 0. Điều gì đã thay đổi (để biết cần test gì)

| # | Commit | Nội dung |
|---|---|---|
| a | `76630c39` | Contract v1.3 APPROVED + OpenAPI |
| b | `ac849ea7` | Bỏ bảng tombstone `structure_removed_resources` → OperationService |
| c | `cd99422a` | P2 backend → FacilityService / OperationService (compat table, restore, capacity reconcile, maintenance windows, batch, reject Owner coords) |
| d | `ce76fd05` | P3 UI → Owner structure screen (tree, mesh registration, compat, paths, availability, batch) |
| e | chưa commit | 4 file FE (capacity read-only + empty-state), 3 file FE (My Parking Lots + edit-site), fix `tenant_id` khi INSERT `structure_backup_policies`, 5 file test mới, session log |
| f | chưa commit | Fix vòng lặp re-render vô hạn ở tab Parking Structure; ô Planned Car Spaces read-only; đếm bay multi-floor (indoor 64 / outdoor 48 / underground 48); sửa site không ghi đè địa chỉ khi chỉ đổi Name; upper bound capacity theo số bay model |

---

## 1. Chuẩn bị môi trường

### 1.1. Stack đang chạy

| Thành phần | URL | Ghi chú |
|---|---|---|
| Frontend (Vite dev server) | `http://localhost:5173` | Container bind-mount **chính repo này** (`SmartParking-238\FE`). Sửa FE xong chỉ cần **F5** trên browser; Vite hot-reload. |
| Facility API | `http://localhost:5045` | Mọi route `/api/parking-lots/**` và route nội bộ `/internal/**`. |
| UserService | `http://localhost:5035` | Login: `POST /api/auth/login`. |
| Operation/Reservation service | `http://localhost:5055` | Reservation + impact projection. |
| Facility DB | `facility-db-1` | DB deploy `smartpark_facility`. Gated test dùng server `127.0.0.1:5442` (host port của `SMARTPARK_DB_PARKING_PORT`). |

### 1.2. Tài khoản và dữ liệu seed

- Owner email: `owner@smartpark.local`.
- **Mật khẩu Owner:** giá trị `SMARTPARK_OWNER_PASSWORD` trong `.env` của repo là **SAI** (trả `AUTH_FAILED`). Tester phải dùng **mật khẩu Owner thật** đang đăng nhập được. Không dán mật khẩu vào tài liệu/commit.
- Demo site: `b0000000-0000-4000-8000-000000000001` = **DEMO-HQ**, template outdoor **48 bay**.
- Demo tenant (tham chiếu): `a0000000-0000-4000-8000-000000000001`. Luôn ưu tiên đọc `tenantId` thật từ `GET /api/parking-lots` (`site.tenantId`).
- `BACKUP_MARK`: demo Owner **KHÔNG** có quyền này (xem C2).
- Service-to-service key cho route `/internal/**`: biến `SMARTPARK_SERVICE_KEY` trong `.env` (compose map thành `Services__Key`). Route `/internal/**` trả **401** nếu thiếu/sai header `X-Service-Key`.

### 1.3. Kiểm tra nhanh trước khi test

```powershell
docker ps --filter name=smartpark-app- --format "{{.Names}}`t{{.Status}}"
curl.exe -s -o NUL -w "facility %{http_code}`n" http://localhost:5045/health
curl.exe -s -o NUL -w "frontend %{http_code}`n" http://localhost:5173/
```

Cả hai phải **200**.

Xác nhận frontend đang phục vụ code của repo này (phải in `containsMarker=YES`):

```powershell
$m = curl.exe -s "http://localhost:5173/src/roles/owner/structure/SlotRegistrationPanel.tsx"
if ($m -match 'Select meshes in 3D') { 'containsMarker=YES' } else { 'containsMarker=NO' }
```

Nếu `NO` ⇒ container vẫn mount repo khác. Chỉ khi đó mới cần recreate frontend (thao tác Docker do người vận hành stack thực hiện; tài liệu này không yêu cầu tự start/stop/rebuild):

```powershell
docker compose -f compose.app.yml up -d --force-recreate frontend
```

### 1.4. Đăng nhập Owner (lấy bearer token)

```powershell
$body = @{ email = 'owner@smartpark.local'; password = '<REAL_OWNER_PASSWORD>' } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri 'http://localhost:5035/api/auth/login' `
  -ContentType 'application/json' -Body $body
$token = $login.data.accessToken
$headers = @{ Authorization = "Bearer $token" }
"role=$($login.data.user.role) tokenLen=$($token.Length)"
```

**PASS:** HTTP `200`, `success = true`, `data.user.role = "owner"`, `data.accessToken` là chuỗi khác rỗng.
**FAIL:** `AUTH_FAILED` (sai mật khẩu — dùng mật khẩu thật), hoặc `Connection refused` (UserService chưa lên).

---

## 2. Bản đồ điều hướng UI (Owner)

Sidebar trái:

```
Dashboard
Parking management
  ├─ Sites
  ├─ My parking lots        <-- C3, C4
  └─ Parking Structure      <-- C5, C6, C7, C8, C9
Business
  ├─ Operators
  ├─ Financial
  └─ Policies
```

Trong tab **Parking Structure**, thứ tự panel từ trên xuống:

1. Header + dropdown **Site** (góc phải trên).
2. **Service availability** (`ServiceAvailabilityPanel`).
3. **Capacity** (3 ô số — `CapacitySummary`).
4. Cảnh báo "No live parking slots..." nếu chưa đăng ký slot.
5. Cột trái: **Spatial tree** (`StructureTree`).
6. Cột phải: **Site 3D model** (`Parking3DViewer`) + **Slot registration** (`SlotRegistrationPanel`).
7. **Registered slots** (`RegisteredSlotsPanel`).
8. **Access paths** (`AccessPathsPanel`).
9. `<details>` **Capacity and backup settings** → `BackupCapacityPanel`.

Modal phụ: `UnitDialog` (thêm/rename/capacity unit), `SlotCompatibilityDialog` (sửa compatibility), `ReleasedIdentifierDialog` (mã đã release).

---

## 3. Checklist tổng

| # | Nhóm | Mục cần kiểm | Kết quả mong đợi | Trạng thái |
|---|---|---|---|---|
| C1 | Postman | Chạy lại toàn bộ collection (83 request) | 0 fail (trừ 5.16/5.17 = 403 là ĐÚNG) | **TO RUN** |
| C2 | Postman | `BACKUP_MARK` (5.16/5.17) + `backup` (5.15) | 5.16/5.17 = **403**; 5.15 = **200** | **VERIFIED** (theo collection + DEC-LOT-04) |
| C3 | FE | Ô "Planned Car Spaces" chỉ-đọc, đúng số bay model | Outdoor 48 / Multi-storey 64 / Basement 48; không gõ được | **TO RUN** |
| C4 | FE | Sửa site (Name/Address/Active) | Chỉ đổi Name ⇒ địa chỉ giữ nguyên; đổi địa chỉ ⇒ lưu thật; Active lưu qua action `active` | **TO RUN** |
| C5 | FE | Chọn nhiều mesh 3D để đăng ký slot | Chọn nhiều mesh, đăng ký nhiều slot một lần, kết quả từng ô | **TO RUN** |
| C6 | E2E | Đăng ký → đặt chỗ → quan sát; vòng đời maintenance window | Slot chuyển OCCUPIED rồi về AVAILABLE; serviceState SCHEDULED → MAINTENANCE_UNCERTAIN → ACTIVE | **TO RUN** (phần `commitments[]`: **BLOCKED**) |
| C7 | FE | A12 — mesh chưa đăng ký phải "trơ" | Không hover/click/tính capacity | **TO RUN** |
| C8 | FE | Overlay SPARK-228 raw-exact | Màu trạng thái đúng, tách khỏi logic đăng ký | **TO RUN** |
| C9 | FE | 7 panel của trang Parking Structure | Mỗi panel hoạt động đúng | **TO RUN** |
| C10 | Docs | Xác nhận các mục BLOCKED/out-of-scope | Không trình bày là fail của SPARK-238 | **TO RUN** |

---

## 4. Chi tiết từng case

### C1 — Chạy lại toàn bộ Postman collection

**Kiểm gì:** toàn bộ surface HTTP của SPARK-238 (lot, units, slots, paths, PATCH actions, reads, maintenance, batch, operations, negative/auth) chạy đúng trên stack thật.

**Điều kiện trước:**
- Stack 200/200 (mục 1.3).
- Đã import 2 file:
  - `docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json`
  - `docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_environment.json`
- Đã chọn environment `SmartParking - SPARK-238 Facility Structure (local)`.

**Các bước:**
1. Mở environment, điền các biến `secret`:
   - `ownerPassword` ← **mật khẩu Owner thật** (giá trị `.env` hiện tại sai).
   - `operatorPassword` ← `SMARTPARK_OPERATOR_PASSWORD` (nếu chạy folder 00 đủ 4 login).
   - `adminPassword` ← `SMARTPARK_ADMIN_PASSWORD` (tùy chọn).
   - `driverPassword` đã có sẵn giá trị seed.
   - Kiểm tra `baseUrl=http://localhost:5045`, `userServiceUrl=http://localhost:5035`, `reservationServiceUrl=http://localhost:5055`.
2. Chạy folder **`00 - Setup (run first)`** trước. Folder này login Owner (0.1), Operator (0.2), Driver (0.3), Admin (0.4) và sinh `runSuffix`.
3. Chạy lần lượt các folder **01 → 10** theo thứ tự. Lần đầu **tắt** "Stop run on error" để thấy hết lỗi.
4. Không đổi thứ tự: folder 03 phụ thuộc unit của folder 02; folder 07 giả định baseline window list.

**PASS:**
- Tổng cộng **83 request**; không có `500`, `405`, `503`.
- `00 - Setup`: 4 token + `runSuffix` đều xanh.
- Riêng **5.16 và 5.17 = 403 FORBIDDEN** (đúng, xem C2).
- 7.4 = 409 `STRUCTURE_IN_USE`; 7.6 = 409; 7.8 = 409; 7.11 = 409; 8.4/8.5 = 409 kèm `items[]`.

**FAIL:**
- Bất kỳ `500` (xem log bên dưới), `405`, hoặc `503`.
- 5.16/5.17 trả 200 (sai kỳ vọng bảo mật) — không "sửa" assert thành 200.
- Login 0.1 fail với `AUTH_FAILED` (sai `ownerPassword`).

**Nếu còn 500:**

```powershell
docker logs --since 10m smartpark-app-parking-service-1 2>&1 |
  Select-String -Pattern "fail:|Exception|42P01|23502" | Select-Object -Last 20
```

`42P01` = thiếu bảng (ví dụ `structure_maintenance_windows`); `23502` = thiếu cột NOT NULL (ví dụ `tenant_id`).

**BLOCKED:** không.

---

### C2 — `BACKUP_MARK` (5.16/5.17 phải 403) và `backup` (5.15 phải 200)

**Kiểm gì:** phân quyền backup đúng DEC-LOT-04 và đúng hai action khác nhau:
- `backup` (cấu hình capacity) cần `BACKUP_CONFIGURE` → Owner **có** → **200**.
- `markBackup` (đánh dấu 1 slot là backup) cần `BACKUP_MARK` → Owner **không có** → **403**.

**Điều kiện trước:** C1 folder 00 đã lấy `ownerToken`; đã có `createdSiteId` và `slotId` từ folder 01–03.

**Các bước:**
1. Trong Postman mở request **5.15 PATCH backup (site-level CAR policy)** → Send.
2. Mở **5.16 PATCH markBackup (mark)** → Send.
3. Mở **5.17 PATCH markBackup (release)** → Send.

**PASS:**
- 5.15 → **200** `{ success: true }`.
- 5.16 → **403** `{ success: false, code: "FORBIDDEN" }`.
- 5.17 → **403** `{ success: false, code: "FORBIDDEN" }`.
- Sau 5.16/5.17, slot không đổi `reservationState` (vẫn `null`/`NONE`).

**FAIL:** 5.16/5.17 trả 200 (nghĩa là quyền bị cấp nhầm), hoặc 5.15 trả 403 (Owner thiếu `BACKUP_CONFIGURE` — kiểm tra seed).

**Giải thích backend:** query owner-scope là
`(a.permissions IS NULL AND @p <> 'BACKUP_MARK') OR @p = ANY(a.permissions)`;
`accounts.permissions` của demo Owner là `NULL` ⇒ riêng `BACKUP_MARK` bị từ chối. Đúng DEC-LOT-04.

**Nếu bạn thật sự muốn test nhánh có quyền (thay đổi dữ liệu — chỉ làm khi chủ động):**

```sql
UPDATE accounts SET permissions = ARRAY['BACKUP_MARK']
WHERE user_id = '7e0490e0-c1f6-44aa-9672-eb8faa239095';  -- xác nhận lại id từ seed của bạn
```

**BLOCKED:** không.

---

### C3 — Ô "Planned Car Spaces" phải chỉ-đọc và đúng số bay của model

**Kiểm gì:** ô capacity trong form site **không nhập được**, và giá trị là **số bay thực của template 3D** (không phải số 30 hard-code), đếm đúng cả model nhiều tầng.

**Điều kiện trước:**
- Đã đăng nhập Owner trong browser (`http://localhost:5173`).
- Có ít nhất 1 site (DEMO-HQ hoặc site tạo mới).

**Các bước:**
1. Sidebar → **Parking management** → **My parking lots**.
2. Chọn một lot → mở form sửa (**Edit** / **Sửa**).
3. Nhìn section **Capacity & Levels** → ô **Planned Car Spaces**.
4. Thử click vào ô và gõ số.
5. Đổi **Parking Structure Type** lần lượt (Outdoor Lot / Multi-Storey / Basement Parking) và quan sát lại ô này.
6. (Tùy chọn) Upload một file `.glb` ở **3D model for car capacity** và quan sát ô cập nhật theo model upload.

**PASS:**
- Ô **read-only**: không gõ được, không có viền focus nhập liệu, có `aria-readonly="true"`, **không** có dấu `*`.
- Giá trị = số bay model:
  - **Outdoor Lot** → **48**
  - **Multi-Storey** → **64**
  - **Basement Parking** → **48**
- Ngay dưới ô có dòng: *"Capacity is counted from the slots you register. Set per-area limits in Parking Structure."*
- Khi đổi type/upload model, số cập nhật theo model mới; **không** phải hằng số 30.
- Nếu model chưa đọc xong hoặc đọc lỗi: ô tạm hiện giá trị `totalPhysicalCapacity` đã lưu của site (thường `0` với lot chưa đăng ký slot) hoặc placeholder `—` nếu chưa có gì; dưới ô hiện thông báo `role="status"`. Việc này **không** chặn Save.
- Lưu site thành công dù ô này read-only (nó không có `max` chặn submit).

**FAIL:**
- Thấy số `30`.
- Gõ được số / ô có thể sửa.
- Có `max=...` khiến submit bị chặn vì capacity.
- Multi-storey vẫn ra 48 (đếm sai model nhiều tầng).

**BLOCKED:** không.

---

### C4 — Sửa site phải lưu thật (đặc biệt: chỉ đổi Name thì không được ghi đè địa chỉ)

**Kiểm gì:** 3 fix FE:
1. Chỉ đổi **Name** ⇒ Province/City/District/Address **giữ nguyên** (không bị compose lại từ mặc định Quận 1).
2. Đổi **Address** thật ⇒ lưu và reload vẫn đúng.
3. Đổi **Active/Inactive** ⇒ lưu qua action `active`, reload vẫn đúng.

**Điều kiện trước:** đã đăng nhập Owner, có 1 site. Nên chọn site **không phải DEMO-HQ** hoặc ghi lại giá trị trước khi sửa.

**Các bước A — chỉ đổi Name:**
1. Tab **My parking lots** → chọn lot → **Edit**.
2. Ghi lại **Province / City**, **District**, **Street Address / Complex** và dòng **Full address** hiện tại.
3. Chỉ đổi **Site Name** thành `SPARK238 Name <giờ>`.
4. Bấm **Save Changes**.
5. **F5**, mở lại đúng lot.

**PASS A:**
- Toast/notice thành công chỉ xuất hiện **sau** khi server đã lưu.
- Name mới được lưu.
- City/District/Street/Full address **y nguyên** như trước khi sửa.
- Active không bị đổi ngoài ý muốn.

**Các bước B — đổi Address thật:**
1. Mở lại form sửa.
2. Đổi **District** (ví dụ sang một quận khác trong cùng thành phố) và/hoặc sửa **Street Address**.
3. Bấm **Save Changes** → **F5** → mở lại.

**PASS B:** District/Street/Full address mới được lưu; Full address đúng thứ tự `street, district, city name`.

**Các bước C — đổi Active:**
1. Mở form sửa, bật/tắt checkbox **Active** ở section **Base Pricing & Site Status**.
2. **Save Changes** → **F5** → mở lại form.

**PASS C:** trạng thái Active/Inactive giữ đúng sau F5; nhãn đổi giữa `Active` và `Inactive`.

**FAIL:**
- Chỉ đổi Name mà City/District/Address bị đổi (ví dụ nhảy về `Quận 1` / `TP. Hồ Chí Minh`).
- Toast thành công nhưng F5 lại thấy giá trị cũ.
- Active bật/tắt nhưng F5 quay lại trạng thái cũ.

**Kiểm tra trực tiếp DB (tùy chọn):**

```powershell
docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select site_code,name,address,status,updated_at from parking_sites where deleted_at is null order by updated_at desc limit 3;"
```

- `status` phản ánh Active/Inactive.
- LƯU Ý: `total_physical_capacity` **KHÔNG** phải số bạn nhập — nó là **số slot đã đăng ký** (A7/F07). Lot chưa đăng ký slot ⇒ `0`. Đây là thiết kế, không phải bug.

**BLOCKED:** không.

---

### C5 — Multi-select mesh trên 3D để đăng ký slot (chi tiết)

**Kiểm gì:** luồng đăng ký slot bằng cách chọn **nhiều mesh template** trong khung 3D, với kết quả **từng mesh** và retry chỉ các mesh lỗi. Đây là luồng từng bị chặn bởi vòng lặp re-render vô hạn; sau fix phải mượt, đổi site được, panel không lặp.

#### C5.0. Điều kiện trước

1. Đã đăng nhập Owner; trình duyệt mở `http://localhost:5173`.
2. Có site và đã có **ít nhất 1 spatial unit** (FLOOR/ZONE/BLOCK). Nếu **Spatial tree** đang trống, làm C9.1 trước để tạo tối thiểu 1 FLOOR hoặc 1 Outdoor zone.
3. Site chọn là một site còn **mesh chưa đăng ký**. Với outdoor template mới tinh: **48 unregistered / 0 registered**.
4. Hiểu rõ fallback template (quan trọng, tránh báo sai):
   - Với **site do server tạo** (Owner id server không khớp local demo store), `lotType` rơi về **outdoor** ⇒ mọi site server đều hiển thị cùng `outdoor_parking_lot.glb`.
   - **Per-site upload** được lưu **theo site trong IndexedDB của browser** (không lên server).
   - Vì vậy: **đừng kỳ vọng mỗi site server có template khác nhau**. Muốn template khác, hãy **upload model** cho site đó (hoặc dùng site demo local có sẵn model).
   - Khi `!ownerLot`, màn hình hiện dòng: *"No lot-specific model is bound to this site, so the default outdoor template is shown..."* — đây là disclosure, không phải lỗi.

#### C5.1. Mở đúng màn hình và chọn site

1. Sidebar → **Parking management** → **Parking Structure**.
2. Ở dropdown **Site** (góc phải trên, cạnh tiêu đề), chọn **DEMO-HQ** hoặc site cần test.
3. Quan sát:
   - Panel **Site 3D model** hiện overlay **"Loading the 3D parking model..."** rồi biến mất (outdoor ~5.8 MB, có thể vài giây).
   - Caption dưới tiêu đề: *"Registered bays are interactive. Unregistered template bays stay inert."*
   - Badge góc phải panel: **"Live slots only"**.
   - Dưới tiêu đề **Slot registration** có dòng đếm: `N unregistered meshes · X registered slots · Y registered slots without a matching mesh`.

**PASS bước này:** panel **không tự lặp vô hạn**, dropdown giữ đúng site bạn chọn, đổi sang site khác rồi quay lại vẫn được (đây là regression test cho bug re-render).

**FAIL bước này:** các panel nhấp nháy/lặp, dropdown tự nhảy về site đầu, hoặc không đổi site được.

#### C5.2. Bật chế độ chọn mesh

1. Trong panel **Slot registration**, bấm nút **"Select meshes in 3D"**.
2. Quan sát:
   - Nút đổi thành **"Finish selecting"** (kiểu primary), `aria-pressed="true"`.
   - Badge panel 3D đổi thành **"Selecting meshes"**.
   - Overlay góc trái 3D: **"Selecting unregistered bays"** + dòng "Registered bays stay live and cannot be selected here..."
3. Nếu nút **disabled** và có tooltip *"No unregistered bays are available to select in this site's 3D model."*:
   - Nghĩa là `census.unregisteredMeshes === 0`.
   - Nguyên nhân: mọi mesh đã map vào slot đã đăng ký, **hoặc** model 3D chưa load (fallback chỉ vẽ slot đã đăng ký).
   - Xử lý: chờ model load xong; hoặc chọn site khác; hoặc dùng danh sách mesh keyboard-friendly ở bước C5.3.

**PASS:** nút bật được và 3D vào đúng chế độ chọn.

#### C5.3. Chọn nhiều mesh

Cách A — chọn trực tiếp trên 3D:
1. Rê chuột lên một ô đỗ **chưa đăng ký**.
2. Kỳ vọng tooltip: `<tên mesh>` + **"Not registered — no capacity, no booking"** + **"Click to select for registration"**. Con trỏ thành `pointer`.
3. **Click** ô đó. Tooltip đổi thành **"Click to deselect"**.
4. Click thêm **2–3 ô khác** (tổng ≥ 2 mesh).
5. Mỗi mesh được chọn xuất hiện thành một dòng trong danh sách **Slot registration** bên dưới, kèm **Slot code** gợi ý.

Cách B — chọn bằng danh sách (ổn định hơn nếu khó click 3D):
1. Mở `<details>` **"Choose meshes from a list (keyboard-friendly) · N"**.
2. Tick checkbox của ít nhất 2 mesh.
3. Danh sách selection bên dưới cập nhật tương ứng.

**PASS:** chọn được ≥ 2 mesh; click lại một mesh đã chọn sẽ **bỏ chọn**; số dòng selection khớp số mesh đã chọn.

**FAIL:** hover/click không phản hồi dù đang ở chế độ chọn; hoặc click một ô lại chọn nhầm ô khác; hoặc số dòng selection không khớp.

#### C5.4. Kiểm tra / sửa slot code

1. Với mỗi dòng selection, đọc **tên mesh** và ô **Slot code**.
2. Slot code gợi ý = `suggestSlotCode(mesh.name)` (đã chuẩn hoá, ví dụ mesh `CARPARKINGSPACE_001` → code `001`).
3. Nếu cần, sửa code:
   - Tối đa **50 ký tự**.
   - Không trùng nhau (không phân biệt hoa/thường) trong cùng lần đăng ký; lỗi hiện ngay dưới ô.
   - **Quan trọng cho C6/C8:** nếu muốn overlay vận hành tô màu đúng theo bay, **slot code phải trùng CHÍNH XÁC tên mesh gốc** (raw, phân biệt hoa/thường). Code đã chuẩn hoá vẫn đăng ký/interactive được (nhờ matcher tolerant của 238) nhưng overlay 228 sẽ **fail-closed thành `unknown`** (xám tím), không phải available/occupied. Đây là hành vi đúng, không phải bug.

**PASS:** không có lỗi code trùng/rỗng; hiểu rõ trade-off code chuẩn hoá vs raw.

#### C5.5. Chọn unit / loại slot / category

1. **Owning unit** (bắt buộc): chọn unit sẽ sở hữu các slot này. Nếu danh sách trống, quay lại C9.1 tạo unit.
2. **Slot type**: `STANDARD` / `VIP` / `EV` / `DISABLED`.
3. **Categories (1–3)**: tick ít nhất 1 (`CAR`, `MOTORCYCLE`, `OVERSIZED`).
4. Nếu chọn **≥ 2 category**, **Primary category** bắt buộc phải chọn tường minh (`Choose explicitly`). Nếu chỉ 1 category, primary tự bằng category đó.
5. Nếu bỏ hết category, hoặc ≥2 mà chưa chọn primary ⇒ nút Register bị chặn và hiện lỗi.

**PASS:** form hợp lệ khi có unit + 1–3 category + primary hợp lệ.

#### C5.6. Đăng ký

1. Bấm nút **"Register N slots"** (N = số mesh đang chờ, không gồm mesh đã xong).
2. Trong lúc chạy: badge từng dòng hiện **"Registering..."**, nút hiện **"Registering..."**.
3. Kết quả **từng dòng**: **Registered** (xanh) / **Failed** (đỏ) / **Released code** (vàng) / **Restored** (xanh dương).
4. Dòng summary: `X registered · Y failed · Z released decisions` (+ `N restored` nếu có).
5. Nếu có failed: nút **"Retry failed (N)"** chỉ gửi lại các mesh failed. Nút **"Clear finished"** xoá các dòng đã succeeded/released khỏi selection.

**PASS:**
- Nhiều slot được đăng ký trong **một lần bấm** (mỗi mesh = 1 request `POST /api/parking-lots/{siteId}/slots`).
- Kết quả hiển thị **riêng từng ô**, không gộp thành một thông báo chung.
- Sau khi refresh cấu trúc: panel **Registered slots** tăng đúng số slot; dòng census giảm đúng số mesh đã đăng ký; các slot vừa đăng ký trở nên interactive trong 3D.
- Không có vòng lặp re-render sau khi register.

**FAIL:**
- Một mesh lỗi làm mất kết quả của các mesh khác.
- Không có nút Retry failed khi có failed.
- Đăng ký xong nhưng Registered slots/census không đổi.
- Panel lặp lại / dropdown site nhảy.

**Kiểm tra DB sau khi đăng ký:**

```powershell
docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select s.slot_code, u.name as unit, s.supported_vehicle_type, s.deleted_at
 from parking_slots s
 join spatial_units u on u.id = s.spatial_unit_id
 where s.site_id = '<SITE_ID>'
 order by s.slot_code;"

docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select sc.slot_id, sc.vehicle_type
 from slot_compatibilities sc
 where sc.site_id = '<SITE_ID>'
 order by sc.slot_id, sc.vehicle_type;"
```

**Dự phòng bằng API (nếu UI không bấm được):**

```powershell
$slotBody = @{
  unitId             = '<UNIT_ID>'
  code               = 'CARPARKINGSPACE_001'   # càng khớp raw mesh name càng tốt
  vehicleType        = 'CAR'
  primaryVehicleType = 'CAR'
  vehicleTypes       = @('CAR')
  type               = 'STANDARD'
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://localhost:5045/api/parking-lots/$siteId/slots" `
  -Headers $headers -ContentType 'application/json' -Body $slotBody
```

Kỳ vọng: **200** `{ success:true, data:"<slot-guid>" }`.

**BLOCKED:** không. (Fallback template per-site là giới hạn đã biết, không phải BLOCKED của C5.)

---

### C6 — End-to-end: đăng ký → đặt chỗ → quan sát, và vòng đời maintenance window (chi tiết)

Case này gồm **C6-A** (booking) và **C6-B** (maintenance). Chạy C6-A trước, rồi C6-B.

#### C6.0. Điều kiện trước

- Hoàn thành C5: có **≥ 2 slot đã đăng ký** trong **cùng một unit**, biết `slotId` và `code`.
- Site đang **ACTIVE** (nếu đã deactivate ở C9, bấm **Activate lot** trong Service availability trước).
- Có `$headers` (bearer Owner) từ mục 1.4 và `$siteId`.
- (Cho quan sát màu 3D) slot code trùng chính xác tên mesh gốc. Nếu không, vẫn quan sát được qua panel **Registered slots** và `GET /structure`; chỉ màu 3D là `unknown`.

#### C6-A. Đặt chỗ và quan sát

**A1. Ghi lại trạng thái trước khi đặt.**

1. Trong tab **Parking Structure**, panel **Registered slots**, tìm slot sẽ đặt.
2. Trạng thái mong đợi trước khi đặt: badge màu xanh `OPERATIONAL` (hoặc `AVAILABLE`), không có `Occupied`/`RESERVED`.
3. Ghi lại `slotId`: mở DevTools Network (hoặc gọi API bên dưới) và lấy `data.slots[].id` của slot đó.

```powershell
$structure = Invoke-RestMethod -Uri "http://localhost:5045/api/parking-lots/$siteId/structure" -Headers $headers
$tenantId  = $structure.data.site.tenantId
$slot      = $structure.data.slots | Where-Object { $_.code -eq '<SLOT_CODE>' } | Select-Object -First 1
"slotId=$($slot.id) occupied=$($slot.isPhysicallyOccupied) op=$($slot.operationalStatus)"
```

**A2. Đặt chỗ (claim).**

Đường deterministic dùng route nội bộ của FacilityService (**cổng 5045**, không phải 5055):

```powershell
$key = ((Select-String -Path .env -Pattern '^SMARTPARK_SERVICE_KEY=').Line -split '=',2)[1].Trim()

$claim = @{
  tenantId         = $tenantId
  vehicleType      = 'CAR'
  preferredSlotId  = $slot.id
  targetUnitId     = $null
  allowReservedSlot = $false
} | ConvertTo-Json

curl.exe -s -w "`nHTTP %{http_code}`n" -X POST `
  "http://localhost:5045/internal/gate-sites/$siteId/slots/claim" `
  -H "Content-Type: application/json" -H "X-Service-Key: $key" -d $claim
```

Kỳ vọng:
- **200** body là slot được claim: `{ "id":"...", "code":"...", "vehicleType":"CAR" }`.
- Nếu slot ưu tiên không còn trống: **409** `{ code:"NO_AVAILABLE_SLOT" }`.
- Nếu thiếu/sai `X-Service-Key`: **401** (body rỗng).
- Nếu `tenantId` rỗng / site rỗng / `vehicleType` không thuộc `CAR|MOTORCYCLE|ANY`: **400**.

Nếu có màn hình Driver đặt chỗ được cho cùng site/slot, bạn có thể dùng UI đó thay cho bước claim; các bước quan sát bên dưới giữ nguyên.

**A3. Quan sát sau khi đặt.**

1. Quay lại tab **Parking Structure**, **F5** (hoặc bấm Retry nếu có lỗi cũ).
2. Kỳ vọng:
   - Panel **Registered slots**: slot vừa claim có badge **Occupied** (đỏ).
   - Panel **Capacity**: dòng "Live state" tăng **1 occupied**.
   - 3D: nếu slot code trùng raw mesh name ⇒ ô đổi **đỏ (Occupied)**. Nếu code đã chuẩn hoá ⇒ ô có thể là **`unknown` (xám tím)** do overlay 228 fail-closed; đây là hành vi đúng, dùng panel/API để kết luận.
3. Kiểm tra API:

```powershell
$after = Invoke-RestMethod -Uri "http://localhost:5045/api/parking-lots/$siteId/structure" -Headers $headers
($after.data.slots | Where-Object { $_.id -eq $slot.id }) |
  Select-Object id, code, isPhysicallyOccupied, operationalStatus, reservationState
```

Kỳ vọng: `isPhysicallyOccupied = True`.

**A4. Trả slot (release) và quan sát hồi phục.**

```powershell
$release = @{ tenantId = $tenantId } | ConvertTo-Json
curl.exe -s -o NUL -w "HTTP %{http_code}`n" -X POST `
  "http://localhost:5045/internal/gate-sites/$siteId/slots/$($slot.id)/release" `
  -H "Content-Type: application/json" -H "X-Service-Key: $key" -d $release
```

Kỳ vọng: **204**. Sau đó F5 lại: slot trở về `OPERATIONAL`/available, "Live state" giảm về 0 occupied, 3D trở lại xanh (nếu overlay bind được).

**PASS C6-A:**
- Claim **200** và `isPhysicallyOccupied` chuyển `False → True`.
- UI phản ánh **Occupied** ở panel Registered slots + Capacity.
- Release **204** và trạng thái trở lại available.

**FAIL C6-A:**
- Claim 200 nhưng structure/UI không đổi (projection không cập nhật).
- Claim 409 dù slot đang trống và đúng `vehicleType`.
- Claim 401 dù đã gửi `X-Service-Key` (kiểm tra key lấy từ `SMARTPARK_SERVICE_KEY`).
- Slot không trở lại available sau release.

#### C6-B. Vòng đời maintenance window

**Kiến thức bắt buộc trước khi kết luận (tránh báo sai):**
- Window là **half-open `[start, end)`**; `start >= now` (không được bắt đầu trong quá khứ).
- `POST` trả **201** với `enforcementState = "pending"`.
- Vì enforcement mặc định **pending**, khi window đang phủ `now` thì `serviceState.state` là **`MAINTENANCE_UNCERTAIN`**, **KHÔNG phải** `MAINTENANCE`. `MAINTENANCE` chỉ xảy ra khi mọi window đang phủ đều đã `acknowledged` — nhánh acknowledgement **không** được test ở đây.
- Vòng đời quan sát đúng: **`SCHEDULED` → `MAINTENANCE_UNCERTAIN` → `ACTIVE`** (khi hết hạn, không cần job).
- Window là **site-wide**. API không nhận `unitId`/`slotId` (resource-scoped bị chặn — DE-13).
- `PATCH` chỉ **nới dài**: rút ngắn hoặc đổi `start` ⇒ **409 STRUCTURE_IN_USE**.
- `DELETE` bị chặn **409 STRUCTURE_IN_USE** nếu còn window khác đang phủ `now`.
- `DELETE` thành công trả **200** `{ success:true }` (không phải 204).

**B1. Baseline.**

1. Trong **Service availability**, đọc badge state (kỳ vọng `ACTIVE`) và danh sách **Maintenance windows**.
2. Hoặc gọi API:

```powershell
Invoke-RestMethod -Uri "http://localhost:5045/api/parking-lots/$siteId/maintenance-windows" -Headers $headers |
  ConvertTo-Json -Depth 6
```

Kỳ vọng: **200** `{ success:true, data:[...] }`. Ghi lại danh sách hiện có (có thể rỗng).

**B2. Tạo window tương lai (SCHEDULED).**

UI: form **Schedule a closure** → **Start** = now + 2 ngày, **End** = now + 2 ngày 2 giờ, **Reason** bất kỳ → **Schedule closure**.

API (kỳ vọng **201**):

```powershell
$start = (Get-Date).ToUniversalTime().AddDays(2).ToString('o')
$end   = (Get-Date).ToUniversalTime().AddDays(2).AddHours(2).ToString('o')
$winBody = @{ start = $start; end = $end; reason = 'SPARK-238 C6 smoke' } | ConvertTo-Json

$win = Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5045/api/parking-lots/$siteId/maintenance-windows" `
  -Headers $headers -ContentType 'application/json' -Body $winBody
$win.data | Select-Object id, start, end, enforcementState, reason
```

**PASS B2:**
- HTTP **201**; `data.enforcementState = "pending"`; `start`/`end` đúng.
- Panel hiện window với badge **"Enforcement pending"**; state badge **`SCHEDULED`**; banner *"A closure is scheduled... lifecycle status is unchanged."*
- `GET /structure` → `data.serviceState.state = "SCHEDULED"`, `closedUntil = null`; **`data.site.status` vẫn `ACTIVE`** (lifecycle không đổi).

**B3. Window chồng lấn phải bị chặn (409).**

Tạo window thứ 2 chồng lấn (ví dụ start = start+1h, end = end+1h):

```powershell
# Chồng lấn window B2: now+2d+1h -> now+2d+3h (giao với now+2d -> now+2d+2h)
$start2 = (Get-Date).ToUniversalTime().AddDays(2).AddHours(1).ToString('o')
$end2   = (Get-Date).ToUniversalTime().AddDays(2).AddHours(3).ToString('o')
$overlap = @{ start = $start2; end = $end2; reason = 'overlap probe' } | ConvertTo-Json
curl.exe -s -w "`nHTTP %{http_code}`n" -X POST `
  "http://localhost:5045/api/parking-lots/$siteId/maintenance-windows" `
  -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d $overlap
```

Kỳ vọng: **409** `{ success:false, code:"STRUCTURE_IN_USE", message:"Another maintenance window already covers this period." }`.

**LƯU Ý BLOCKED:** body 409 **không** chứa `commitments[]` (DE-4). Đừng assert nó.

**B4. Extend-only (PATCH).**

1. Nới dài: `PATCH /api/parking-lots/{siteId}/maintenance-windows/{windowId}` body `{ "end": "<later>" }` → **200**; `enforcementState` reset về **pending**. UI bấm **Extend**.
2. Rút ngắn: body `{ "end": "<earlier>" }` → **409** `STRUCTURE_IN_USE` (`"A maintenance window can only be extended."`). UI thường đã chặn end sớm hơn bằng `min` trên ô **New end**, nên để kiểm tra 409 rút ngắn hãy dùng **API** (hoặc gõ đè giá trị nếu trình duyệt cho phép).
3. Đổi start: body `{ "start": "<khác start hiện tại>", "end": "<later>" }` → **409** `STRUCTURE_IN_USE` (`"Moving the start is not an extension..."`). UI không có ô sửa start, nên case này **chỉ test qua API**.

**PASS B4:** nới dài 200; rút ngắn 409; đổi start 409.

**B5. MAINTENANCE_UNCERTAIN khi window bắt đầu.**

1. Tạo window bắt đầu **sớm** (không được ở quá khứ): `start = now + 1 phút`, `end = now + 10 phút`, reason bất kỳ. Kỳ vọng **201**.
2. Đợi tới sau `start` (sleep ~70–90 giây).
3. Gọi `GET /api/parking-lots/{siteId}/structure` (hoặc F5).

**PASS B5:**
- `data.serviceState.state = "MAINTENANCE_UNCERTAIN"`.
- `data.serviceState.closedUntil` = end của window.
- Panel badge **MAINTENANCE UNCERTAIN**, banner *"A closure is in progress but enforcement is not confirmed..."*.
- **KHÔNG** kỳ vọng `MAINTENANCE` (pending enforcement). Nếu thấy `MAINTENANCE` khi chưa acknowledge, đó mới là bất thường.

**B6. Cancel window và guard 409.**

1. Hủy window **chưa bắt đầu** (ví dụ window B2 hoặc một window mới tạo không phủ now): `DELETE /api/parking-lots/{siteId}/maintenance-windows/{windowId}` → **200** `{ success:true }`. UI bấm **Cancel window**.
2. Trong khi còn window khác đang phủ `now`, thử hủy window hiện tại → **409** `STRUCTURE_IN_USE` (`"Another maintenance window still covers this period."`).

**PASS B6:** cancel 200; cancel khi bị window khác phủ now ⇒ 409.

**B7. Tự hết hạn về ACTIVE.**

1. Đợi qua `end` của window đang phủ (hoặc tạo window ngắn `start = now + 1 phút`, `end = now + 3 phút` và đợi).
2. `GET /structure` / F5.

**PASS B7:** `serviceState.state` trở lại **`ACTIVE`**, `closedUntil = null`, không cần job.

**FAIL C6-B:**
- `POST` trả 500 (thiếu bảng `structure_maintenance_windows`).
- `POST` với start trong quá khứ lại thành công (phải bị 400).
- Window chồng lấn tạo được (phải 409).
- `PATCH` rút ngắn thành công (phải 409).
- `DELETE` bỏ qua guard và làm mất closure đang phủ now.
- `serviceState` không tự về ACTIVE sau `end`.
- Kỳ vọng sai `MAINTENANCE` thay vì `MAINTENANCE_UNCERTAIN` (đây là hiểu nhầm, không phải bug).

**BLOCKED (C6-B):** danh sách `commitments[]` trong body 409 khi lot còn reservation/session sống **chưa hoạt động**:
- Lý do: projection impact của OperationService **không trả `end`/`type` của từng commitment** (DE-4). Backend hiện chỉ kiểm tra **chồng lấn giữa các window**.
- Hệ quả: 409 chỉ có `code` + `message`, **không** có `commitments[]`. Không coi là fail của SPARK-238.

---

### C7 — A12: mesh chưa đăng ký phải "trơ"

**Kiểm gì:** mesh template chưa đăng ký **không** được hover/click/tính capacity/đặt chỗ; chỉ mesh đã đăng ký mới interactive.

**Điều kiện trước:** đang ở **Parking Structure**, site có cả mesh đã đăng ký và chưa đăng ký; **KHÔNG** bấm "Select meshes in 3D" (chế độ bình thường).

**Các bước:**
1. Rê chuột lên một ô đỗ **chưa** đăng ký.
2. Click vào ô đó.
3. Rê chuột/click lên một ô **đã** đăng ký.
4. So sánh dòng census `N unregistered meshes` trước/sau khi click mesh chưa đăng ký.
5. (Tùy chọn) thử claim một slot chưa đăng ký bằng API (không có `slotId` trong structure) → phải không chọn được.

**PASS:**
- Mesh chưa đăng ký: **không** hover highlight, **không** tooltip slot, **không** click chọn được, **không** làm xuất hiện card slot, **không** tăng registered/capacity.
- Mesh đã đăng ký: hover có tooltip slot, click chọn được, hiện card với code/unit/category/status.
- Con trỏ trên mesh chưa đăng ký là `grab` (không phải `pointer`).

**FAIL:**
- Hover/click mesh chưa đăng ký lại hiện slot card hoặc tooltip slot.
- Mesh chưa đăng ký được tính vào capacity / claim được.
- Mesh đã đăng ký lại "trơ" (ngược lại).

**BLOCKED:** không.

---

### C8 — Overlay SPARK-228 vẫn đúng (raw-exact)

**Kiểm gì:** màu vận hành trong 3D vẫn do mapping **raw-exact** của SPARK-228 quyết định, tách hoàn toàn khỏi logic đăng ký của SPARK-238; mesh không bind được thì fail-closed `unknown`, không tô "available" giả.

**Điều kiện trước:** site có slot đã đăng ký **mà slot code trùng chính xác tên mesh gốc** (nếu không, xem giải thích bên dưới). Tốt nhất dùng site demo có seed sẵn.

**Các bước:**
1. Vào **Parking Structure**, chọn site demo (`b0000000-0000-4000-8000-000000000001`).
2. Quan sát legend dưới khung 3D (Available / Occupied / Reserved / Backup / Maintenance / Unknown / Unavailable / Selected).
3. Đối chiếu màu từng ô với trạng thái trong panel **Registered slots**:
   - `available` = xanh lá `#22c55e`
   - `occupied` = đỏ `#ef4444`
   - `reserved` = hổ phách `#f59e0b`
   - `backup` = cyan `#0891b2`
   - `maintenance` = xám `#64748b`
   - `unavailable` = xám đậm `#334155`
   - `unknown` = xám tím `#7c6f8f`
4. Hover một ô đã đăng ký nhưng **code không trùng raw mesh name** (nếu có).

**PASS:**
- Màu phản ánh đúng trạng thái vận hành của slot.
- Slot đã đăng ký nhưng **không bind được mesh** hiển thị **`unknown`** (xám tím) + tooltip *"No reliable state reported for this bay"*, **không** xanh available.
- Không có slot nào bị tô theo logic đăng ký của 238 (hai nguồn tách biệt).
- Slot backup hiển thị cyan, không phải hổ phách.

**FAIL:**
- Mesh không bind được lại xanh "available".
- Màu slot đổi theo việc "đã đăng ký" thay vì trạng thái vận hành.
- Backup hiển thị hổ phách (lẫn với reserved).

**Giải thích quan trọng (từ code):** `mapModelSlots` bind theo **raw, case-sensitive** `mesh.name === slot.code`. Vì vậy:
- Nếu bạn đăng ký slot bằng code đã chuẩn hoá (`001` thay vì `CARPARKINGSPACE_001`), overlay 228 **không bind** ⇒ ô đó `unknown`.
- Đây là **fail-closed đúng thiết kế**, không phải bug. Muốn thấy màu vận hành cho slot tự đăng ký, dùng slot code = raw mesh name.

**BLOCKED:** không.

---

### C9 — 7 panel của trang Parking Structure

**Kiểm gì:** từng panel hoạt động đúng, kể cả validation phía client và các action gọi backend.

**Điều kiện trước:** tab **Parking Structure**, đã chọn site; với panel cần slot, đã có slot đăng ký (C5).

#### C9.1. Spatial tree (`StructureTree`)

**Các bước:**
1. Nếu cây trống: bấm **+ Floor** hoặc **+ Outdoor zone** → modal **Add floor, zone or block**.
2. Nhập **Name** (ví dụ `F1`), **Capacity** (0 = không giới hạn), bấm **Add unit**.
3. Trên một FLOOR, bấm **+ ZONE**; trên ZONE bấm **+ BLOCK**.
4. Thử tạo **BLOCK** không có parent (đổi Type = BLOCK, để Parent trống) → phải bị chặn.
5. Thử tạo 2 unit cùng tên (khác hoa/thường) dưới cùng parent → phải báo lỗi trùng.
6. Bấm **Rename** một unit → đổi tên → **Save name**.
7. Bấm **Capacity** một unit → nhập số > số bay model (ví dụ 99 với outdoor 48) → phải báo lỗi upper bound.
8. Bấm **Delete** một unit còn con/slot/path.

**PASS:**
- Thứ tự hợp lệ: FLOOR → ZONE → BLOCK, tối đa 3 tầng. Child type sai bị chặn.
- Tên trùng (không phân biệt hoa/thường) trong cùng sibling bị báo lỗi.
- Capacity:
  - `0` = "No layout limit" và luôn hợp lệ.
  - Số âm hoặc không nguyên bị chặn.
  - Số **> số bay model** bị chặn với thông báo `"This model has N physical bays, so the layout limit cannot be higher."` (outdoor 48 / indoor 64 / underground 48).
  - Số **< số slot live** trong unit + descendants bị backend từ chối (layout limit không được thấp hơn slot thật).
- Xoá unit còn con/slot/path bị chặn **kèm lý do** (`Cannot be deleted yet: ...`); server cũng không cascade.
- Xoá unit hợp lệ → released, có thể restore tường minh qua dialog mã đã release (nếu server trả `IDENTIFIER_RELEASED`).

**FAIL:** tạo được BLOCK không parent; trùng tên không báo; capacity > bay model được lưu; xoá unit còn con mà không báo.

#### C9.2. Slot registration (`SlotRegistrationPanel`)

Xem **C5**. Ở đây chỉ cần xác nhận panel hiển thị đúng census và empty-state.

**PASS:** dòng census đúng; empty-state **"No unregistered bays to select"** xuất hiện đúng khi `unregisteredMeshes = 0`; nút bị disable kèm tooltip.

#### C9.3. Slot compatibility (`SlotCompatibilityDialog`)

**Các bước:**
1. Chọn một slot (click trong 3D hoặc trong **Registered slots**) → bấm **Edit compatibility**.
2. Modal **Compatibility for `<code>`** mở ra.
3. Tick 2–3 category; kiểm tra **Primary category** bắt buộc chọn tường minh khi ≥2.
4. Bấm **Save compatibility**.
5. Mở lại slot → xem lại danh sách category.
6. Thử trên slot đang **Occupied/Reserved**.

**PASS:**
- 1–3 category; primary bắt buộc khi ≥2; 1 category tự là primary.
- Lưu xong đọc lại thấy đủ danh sách + primary đánh dấu `· primary`.
- **Không có** ô nhập toạ độ/geometry/features (geometry là read-only, đến từ template).
- Slot `unclassified` hiện cảnh báo vàng.
- Slot đang occupied/reserved: nút **Save compatibility** bị disable + dòng giải thích; không cho sửa.
- Nếu slot không có `compatibilitySource`/`primaryVehicleType` từ server (DE-16), dialog vẫn hoạt động dựa trên `vehicleTypes`/`vehicleType` — không crash.

**FAIL:** cho lưu khi ≥2 category mà chưa chọn primary; có ô nhập coordinates; sửa được slot đang occupied/reserved.

#### C9.4. Registered slots (`RegisteredSlotsPanel`)

**Các bước:**
1. Gõ vào ô **Search slot code...** → danh sách lọc theo code.
2. Tick checkbox 2–3 slot → bấm **Remove selected (N)**.
3. Modal confirm hiện tên các slot; đồng ý.
4. Kiểm tra lại danh sách + 3D.
5. Thử **Remove** một slot đơn lẻ.
6. Click vào code slot để focus slot đó trong 3D.

**PASS:**
- Search lọc đúng; không khớp hiện `No registered slot matches "..."`.
- Xoá nhiều = **một batch atomic** (`POST /{siteId}/structure/batch` + header `Idempotency-Key`): nếu 1 slot fail ⇒ **không slot nào bị xoá**, hiện lỗi.
- Batch tối đa **50** item; vượt bị báo lỗi client-side.
- Xoá xong slot biến mất khỏi danh sách và 3D (mesh trở lại "trơ").
- Click code focus camera đúng slot.

**FAIL:** xoá một phần (không atomic); bỏ qua confirm; batch >50 vẫn gửi.

#### C9.5. Access paths (`AccessPathsPanel`)

**Các bước:**
1. Nhập **Path code** (ví dụ `P1`), chọn **From unit**, **To unit** khác nhau, bấm **Add path**.
2. Thử: để trống code; chọn From = To; không chọn endpoint.
3. Bấm **Delete** một path.

**PASS:**
- Tạo được path; hiện trong danh sách với `FROM → TO`.
- Thiếu code / thiếu endpoint / From = To bị chặn client-side với thông báo.
- Code > giới hạn ký tự bị chặn.
- Xoá path được (released), có thể restore qua dialog nếu server trả released.
- Nút **Add path** disabled khi site có < 2 unit.

**FAIL:** tạo được path From = To; tạo path thiếu endpoint; xoá không confirm.

#### C9.6. Service availability (`ServiceAvailabilityPanel`)

Xem chi tiết vòng đời ở **C6-B**. Ở đây kiểm tra thêm:

**Các bước:**
1. Đọc badge state + banner.
2. Bấm **Deactivate lot now** → confirm.
3. Quan sát state/banner; bấm **Activate lot** để khôi phục.
4. Dùng form **Schedule a closure**.

**PASS:**
- Badge map đúng: ACTIVE (xanh), SCHEDULED (vàng), MAINTENANCE (đỏ), MAINTENANCE_UNCERTAIN (vàng), INACTIVE (xám).
- Deactivate chỉ đổi lifecycle status, **không** tạo/hủy window; banner nói rõ.
- Nếu `serviceState` không có trong response: banner cảnh báo "not reported... unknown", **không** giả định ACTIVE; danh sách window đọc qua route riêng.
- Form chặn start trong quá khứ, end ≤ start, reason rỗng/>2000.

**FAIL:** deactivate lại tạo window; state bịa ACTIVE khi thiếu dữ liệu; form cho start quá khứ.

#### C9.7. Capacity (`CapacitySummary`)

**Các bước:**
1. Đọc 3 ô: **Registered physical slots**, **Configured limit**, **Operational availability**.
2. So sánh với số slot trong **Registered slots**.

**PASS:**
- 3 số **tách biệt**, có label rõ; **không** gộp, **không** bịa.
- `Registered physical slots` = `capacity.registeredPhysicalCount` nếu có, ngược lại = `structure.slots.length`.
- `Configured limit` = `Not reported` khi response **không có** block `capacity` (DE-16); `Not configured` nếu có block nhưng `configuredLimit = null`.
- `Operational availability` = `Unknown` khi không có số đáng tin; caption nêu lý do.
- Dòng "Live state" đếm occupied/protected/pending payment/effective backup đúng (chỉ hiện phần có dữ liệu).
- Không có badge `projection ...` nếu server không trả `projectionState`.

**FAIL:** gộp 3 số; hiện `0` cho availability khi thực ra `null`; tự suy ra configured limit từ registered count.

#### C9.8. Capacity and backup settings (`BackupCapacityPanel`)

**Các bước:**
1. Mở `<details>` **Capacity and backup settings**.
2. Dùng filter **Vehicle filter** / **Location filter**.
3. Trong form **Configure backup**: chọn Scope, Vehicle category, **Configured backup** (số), để **Car slot to designate** = `Capacity target only`, bấm **Save configuration**.
4. (Tùy chọn, để thấy hành vi 403) chọn một car slot ở **Car slot to designate** rồi Save.

**PASS:**
- Bảng capacity overview lọc đúng theo vehicle/location; hiện Total/Occupied/Protected/Pending payment/Backup/Unavailable.
- Save với **không chọn car slot** → action `backup` → **200**, notice "Backup configuration saved." (Owner có `BACKUP_CONFIGURE`).
- Save **có chọn car slot** → capacity lưu 200, nhưng `markBackup` trả **403** ⇒ panel hiện lỗi *"Capacity was saved, but the selected car slot was not designated: ..."*. Đây là **đúng** DEC-LOT-04, không phải bug. Vì vậy C9 nên để trống car slot.
- Không đủ điều kiện (slot occupied/reserved) thì slot không xuất hiện trong dropdown car slot.

**FAIL:** Save capacity 403 (thiếu `BACKUP_CONFIGURE`); panel nuốt lỗi 403 của markBackup và báo thành công trọn vẹn.

**BLOCKED (C9.7):** block `capacity` có `configuredLimit`/`operationalAvailable`/`projectionState` đọc từ server **chưa có** (DE-16, phụ thuộc SPARK-228) ⇒ UI hiện "Not reported"/"Unknown". Đúng như thiết kế.

---

### C10 — Xác nhận các mục BLOCKED / out-of-scope (không phải regression)

**Kiểm gì:** các khoảng trống đã ghi nhận vẫn đúng là "chưa có", và tester không báo nhầm chúng thành fail của SPARK-238.

**Các bước & kỳ vọng:**

1. **DE-16 — typed read DTO chưa có (BLOCKED on SPARK-228):**
   - `GET /api/parking-lots/{siteId}/structure` **không** trả block `capacity`.
   - Mỗi slot **không** trả `compatibilitySource` / `primaryVehicleType` (vẫn có `vehicleType`, `vehicleTypes`).
   - Response **không** có `expectedUpdatedAt`.
   - UI: `CapacitySummary` hiện **"Not reported"** / **"Unknown"**; dialog compatibility vẫn chạy.
   - **PASS:** đúng như trên. **FAIL:** coi việc thiếu các field này là bug SPARK-238.

2. **DE-4 — `commitments[]` trong 409 chưa có (BLOCKED):**
   - Gây 409 `STRUCTURE_IN_USE` (ví dụ tạo window chồng lấn) rồi đọc body.
   - **PASS:** body có `success:false`, `code:"STRUCTURE_IN_USE"`, `message`; **không** có `commitments[]`.
   - **FAIL:** assert `commitments[]` rồi báo fail; hoặc coi việc thiếu là regression.

3. **DE-13 — resource-scoped commitments (BLOCKED on OperationService):**
   - `POST /maintenance-windows` chỉ nhận `{ start, end, reason }`; `unitId`/`slotId` không được hỗ trợ qua route công khai (domain yêu cầu cả hai `null`).
   - **PASS:** window tạo ra là **site-wide**. **FAIL:** kỳ vọng tạo được window theo unit/slot rồi báo fail.

**Ghi nhận nợ:** DE-16 (typed read DTO ← SPARK-228), DE-13 (commitment theo resource ← OperationService), DE-4 (`commitments[]` trong 409).

---

## 5. Những "fail" KHÔNG phải bug của SPARK-238

| Hiện tượng | Lý do (đã xác minh) |
|---|---|
| `npx tsc --noEmit` ra **4 lỗi** | Pre-existing, file không liên quan: `GroupedNumberInput.tsx(168,7)`, `OperatorDashboard.tsx(307,49)`, `SiteManagement.tsx(61,28)`, `useSiteManagement.ts(93,42)` |
| `node --test tests/frontend/operator-provisioning.test.cjs` → **7 pass / 10 fail** (17 test) | Pre-existing harness/staleness: stub import thiếu `../dashboard/BackupCapacityPanel` ⇒ `Unexpected import` |
| `totalPhysicalCapacity` luôn 0 với lot mới | Thiết kế A7/F07 — là **số slot đã đăng ký**, không phải số nhập tay |
| 5.16/5.17 trả 403 | Đúng DEC-LOT-04 |
| `capacity` block không có trong `GET /structure` | Typed read DTO **chưa có** (DE-16, phụ thuộc SPARK-228) — UI hiện "Not reported" |
| `expectedUpdatedAt` không có trong response | Chưa expose (DE-16) |
| Slots tự đăng ký bằng code chuẩn hoá hiện `unknown` trong 3D | Overlay 228 bind raw-exact; code khác raw mesh name ⇒ fail-closed `unknown`. Đúng thiết kế |
| Window đang phủ hiện tại là `MAINTENANCE_UNCERTAIN`, không phải `MAINTENANCE` | Enforcement mặc định `pending`; `MAINTENANCE` chỉ khi đã `acknowledged` |

---

## 6. Test tự động — lệnh chính xác

### 6.1. Backend

```powershell
# 1) Build toàn solution
dotnet build SmartParking.slnx --nologo -m:1

# 2) HTTP contract tests (không cần DB)
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj `
  --nologo -m:1 --no-build --filter "FullyQualifiedName~FacilityStructureEndpointTests"

# 3) Toàn bộ project FacilityService
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj `
  --nologo -m:1 --no-build

# 4) Gated Postgres tests (cần facility-db-1; host port 5442 = SMARTPARK_DB_PARKING_PORT)
# Lưu ý: connection string chỉ là anchor để CREATE DATABASE. Fixture tự tạo database
# tạm (structure_parking_test_* / structure_reservation_test_*) rồi drop sau mỗi test;
# KHÔNG ghi gì vào chính smartpark_facility.
$pw = ((Select-String -Path .env -Pattern '^SMARTPARK_DEV_PASSWORD=').Line -split '=',2)[1].Trim()
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = "Host=127.0.0.1;Port=5442;Database=smartpark_facility;Username=smartpark_facility;Password=$pw"
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj `
  --nologo -m:1 --no-build --filter "FullyQualifiedName~Postgres"
```

Nếu thiếu `SMARTPARK_STRUCTURE_TEST_CONNECTION`, các test Postgres bị **Skipped** (không phải fail).

Kỳ vọng đã ghi nhận trước đó: build `0W/0E`; `FacilityStructureEndpointTests` 18/18; project 148 passed / 22 skipped; gated Postgres 22/22.

### 6.2. Frontend

```powershell
Set-Location FE
npm run build
npx tsc --noEmit          # chỉ được có đúng 4 lỗi pre-existing ở mục 5
Set-Location ..

# Bốn suite Node (đã chạy lại trên branch này):
node --test tests/frontend/parking-structure-rules.test.cjs      # 24/24
node --test tests/frontend/mesh-slot-mapping.test.cjs            # 19/19
node --test tests/frontend/operational-layout-adapter.test.cjs   # 16/16 (guard SPARK-228)
node --test tests/frontend/address-composition.test.cjs          # 9/9  (guard C4 address fix)
```

Kỳ vọng: FE build `EXIT=0`; 4 suite trên đều **pass toàn bộ** (24/24, 19/19, 16/16, 9/9).

Không chạy `node --test tests/frontend/*.test.cjs` rồi kết luận fail: `operator-provisioning.test.cjs` có 10 fail **pre-existing** do harness (xem mục 5).

---

## 7. Checklist đóng gói P5 (D1–D8)

| # | Việc | Trạng thái |
|---|---|---|
| D1 | Commit 4 file FE (capacity read-only + empty-state) | chưa |
| D2 | Commit 3 file FE (My Parking Lots + edit-site) | chưa |
| D3 | Commit fix `tenant_id` + 5 file test mới | chưa |
| D4 | Thêm `overlay/parking-structure-maintenance.sql` vào `apply-workflow-migrations.ps1` (lúc merge — file dùng chung với lane 228) | chưa |
| D5 | Tạo mục A1–A12 trong `docs/04-testing/requirements-traceability/` | chưa |
| D6 | Session log `ai/tasks/active/TASK-SPARK-238-SESSION-20261028.md` → `ai/tasks/completed/` | chưa |
| D7 | Push + PR về `develop` | chưa |
| D8 | Ghi nợ out-of-scope: DE-16 (typed read DTO ← SPARK-228), DE-13 (commitment theo resource ← OperationService), DE-4 (`commitments[]` trong 409) | chưa |

Thứ tự merge: **238 → develop**, sau đó SPARK-244 mới tiếp tục phần consumer side.

---

## Phụ lục A — Bảng tra cứu API nhanh

Tất cả route `/api/**` cần `Authorization: Bearer <ownerToken>`. Route `/internal/**` cần `X-Service-Key: <SMARTPARK_SERVICE_KEY>`.

| Method + path | Body chính | Kỳ vọng |
|---|---|---|
| `POST /api/auth/login` (5035) | `{ email, password }` | 200, `data.accessToken` |
| `GET /api/parking-lots` | — | 200, `data[].tenantId` |
| `GET /api/parking-lots/{id}/structure` | — | 200; `data.{site,units,slots,paths,capacityViews,serviceState}`; **không** có `capacity` |
| `POST /api/parking-lots` | `{ code, name, address }` | **201**, `data.id` |
| `POST /api/parking-lots/{id}/units` | `{ type, name, parentId, capacity }` | 200, `data` = guid |
| `POST /api/parking-lots/{id}/slots` | `{ unitId, code, vehicleType, vehicleTypes, primaryVehicleType, type }` | **200**, `data` = guid |
| `POST /api/parking-lots/{id}/paths` | `{ code, from, to }` | 200, `data` = guid |
| `PATCH /api/parking-lots/{id}/structure` | `{ action, ... }` | 200 `{success:true}` |
| `POST /api/parking-lots/{id}/operations` | `{ idempotencyKey, edit }` | 200 completed; 202 pending; 409 failed |
| `POST /api/parking-lots/{id}/structure/batch` | header `Idempotency-Key`; `{ items:[{action,resourceId}] }` | 200 / 409 kèm `items[]` |
| `GET /api/parking-lots/{id}/maintenance-windows` | — | 200, `data[]` |
| `POST /api/parking-lots/{id}/maintenance-windows` | `{ start, end, reason }` | **201**, `enforcementState:"pending"` |
| `PATCH /api/parking-lots/{id}/maintenance-windows/{wid}` | `{ end }` | 200; rút ngắn/đổi start → **409 STRUCTURE_IN_USE** |
| `DELETE /api/parking-lots/{id}/maintenance-windows/{wid}` | — | **200**; nếu còn window phủ now → **409 STRUCTURE_IN_USE** |
| `POST /internal/gate-sites/{siteId}/slots/claim` (5045) | `{ tenantId, vehicleType, preferredSlotId?, targetUnitId?, allowReservedSlot? }` | 200 slot; 409 `NO_AVAILABLE_SLOT`; 401 nếu sai key |
| `POST /internal/gate-sites/{siteId}/slots/{slotId}/release` (5045) | `{ tenantId }` | **204** |

PATCH `action` dùng trong UI:
- `profile` `{ code, name, address, latitude, longitude }` → PATCH `/structure`.
- `active` `{ active }` → POST `/operations` (FE) hoặc PATCH `/structure` (đều dùng `Active`, bỏ qua `resourceId`).
- `renameUnit` `{ resourceId, name }`, `capacity` `{ resourceId, capacity }`, `removeUnit`/`removeSlot`/`removePath` `{ resourceId }`, `restoreUnit`/`restoreSlot`/`restorePath` `{ resourceId }`, `configureSlot` `{ resourceId, slotType, vehicleTypes, primaryVehicleType }`, `backup` `{ unitId, vehicleType, capacity }`, `markBackup` `{ resourceId, active }`.

---

## Phụ lục B — Mẫu kiểm tra HTTP bằng curl.exe (PowerShell)

Vì PowerShell 5.1 không có `-SkipHttpErrorCheck`, dùng `curl.exe` cho các case cần thấy mã lỗi:

```powershell
# 200/201
curl.exe -s -w "`nHTTP %{http_code}`n" -X GET `
  "http://localhost:5045/api/parking-lots/$siteId/structure" `
  -H "Authorization: Bearer $token"

# 409 mong đợi
curl.exe -s -w "`nHTTP %{http_code}`n" -X POST `
  "http://localhost:5045/api/parking-lots/$siteId/maintenance-windows" `
  -H "Authorization: Bearer $token" -H "Content-Type: application/json" `
  -d $overlap
```