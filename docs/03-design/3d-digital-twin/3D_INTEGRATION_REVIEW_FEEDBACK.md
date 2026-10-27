# 3D Digital Twin — Technical Review Feedback & Architectural Reconciliation

> Historical review record (12 October 2026), retained without rewriting its original observations/results. It is not current implementation evidence or the v1.1 authority. Use the [current backend binding](3D_BACKEND_SLOT_STATE_BINDING.md) and [service ownership contract](../../02-architecture/service-boundaries/ownership-and-events.md). In particular, any v0.9 claim that legacy ParkingService owns accepted occupancy is superseded by Operations authority under V11-09. Current discovery-to-model requirements are in the [hub](README.md#maptiler-discovery-to-internal-2d3d-booking-v11-12).

## Document Control

| Thuộc tính | Giá trị |
| :--- | :--- |
| **Mã tài liệu** | `SPARK-3D-REVIEW-FEEDBACK` |
| **Phiên bản** | `v1.0.0` |
| **Trạng thái** | Hoàn thành — Trình duyệt PM & Technical Lead |
| **Dự án** | SmartPark (Hệ thống Quản lý Bãi đỗ xe Thông minh) |
| **Tác giả** | Nhóm Kỹ thuật 3D Digital Twin & Frontend |
| **Người nhận** | Project Manager, Technical Lead, Solution Architect |
| **Ngày tạo** | 2026-10-12 |
| **Tasks liên quan** | SPARK-186, SPARK-228, SPARK-188 |
| **Tài liệu được review** | `# SmartPark 3D Integration Review Handof.md` |

---

## 1. Tóm tắt điều hành (Executive Summary)

Nhóm phát triển 3D Digital Twin trân trọng cảm ơn Project Manager (PM) và Technical Lead đã cung cấp bản đánh giá chi tiết (`# SmartPark 3D Integration Review Handof.md`) liên quan đến tài liệu triển khai và mã nguồn tích hợp 3D.

Sau khi rà soát và đối chiếu chéo với **SmartPark SRS v0.9**, **Solution Architecture v0.9**, và mã nguồn thực tế tại kho lưu trữ (`src/Services/` và `FE/`), nhóm xin gửi bản phản hồi kỹ thuật gồm 2 nội dung trọng tâm:
1. **Làm rõ các điểm nhìn nhận sai sót / hiểu nhầm của AI PM**: Đính chính về mô hình 6 microservices thực tế của dự án và phân định rõ ranh giới giữa *mã nguồn prototype FE cũ* với *tài liệu đặc tả mới*.
2. **Tiếp thu và hoàn tất cập nhật các điểm đúng đắn**: Nhóm đã chỉnh sửa trực tiếp vào cả 3 tài liệu chính thức (`3D_ENTERPRISE_MODEL_INGESTION_RULES.md`, `3D_MODEL_IMPLEMENTATION_GUIDE.md`, `3D_BACKEND_SLOT_STATE_BINDING.md`).

---

## 2. Phản hồi các điểm AI PM nhìn nhận sai sót / ảo giác kiến trúc

### 2.1. ❌ Ảo giác về việc phân chia dịch vụ thành "Facility & Visualization" và "Parking Operations"

> [!NOTE]
> **Ghi chú chuyển tiếp kiến trúc (v1.1 / Fix2FA):** Nhận định kỹ thuật tại mục 2.1 này là biên bản lịch sử ghi nhận ngày 12/10/2026 dựa trên hiện trạng codebase và tài liệu v0.9 cũ. Kể từ quyết định **V11-09** (SRS v1.1) và branch refactor `Fix2FA`, kiến trúc 6 dịch vụ đã chính thức phê duyệt phân tách **Facility Service** (quản trị hạ tầng/spatial layout/3D model) và **Parking Operations** (quản trị phiên/occupancy). Hiện tại mã nguồn `src/Services/ParkingService` đang trong quá trình chuyển đổi hoàn tất sang `src/Services/FacilityService`.


* **Nhận xét của AI PM (Mục 1, 2, 9, 10, 28):**
  > *"The target six-service baseline supersedes the old service boundary: Facility & Visualization → resource/configuration/layout, Parking Operations → accepted operation/claims/availability... Rewrite backend slot-binding document around O-authoritative state → F visualization projection → FE... The current ParkingService endpoint should be treated as transitional."*
* **Thực tế kiến trúc và mã nguồn dự án:**
  1. **Quy hoạch 6 microservices chính thức của SmartPark** (theo `SmartPark_Solution_Architecture_v0.9.md (lưu trữ tại docs/archive/ và được kế thừa tại smartpark-solution-architecture-v1.1.md)` §3.4) bao gồm:
     - **`UserService`** *(Đã có code)*: Quản trị tài khoản, quyền hạn, phương tiện đã đăng ký.
     - **`ParkingService`** *(Đã có code)*: Quản trị toàn bộ hạ tầng vật lý bãi đỗ (`parking_sites`, `spatial_units`), vị trí đỗ (`parking_slots`), và **trạng thái vận hành vật lý của slot** (`physical_state`).
     - **`ReservationService`** *(Đã có code)*: Quản trị đặt chỗ, vòng đời giữ chỗ, ưu tiên phân bổ và rào cản bảo vệ.
     - **`IoTService`** *(Sắp triển khai theo roadmap)*: Thu thập telemetry cảm biến vật lý và camera LPR, gửi sự kiện đồng bộ về `ParkingService`.
     - **`PaymentService`** *(Sắp triển khai theo roadmap)*: Xử lý giao dịch thanh toán và đối soát cổng thanh toán.
     - **`NotificationService`** *(Sắp triển khai theo roadmap)*: Quản lý thông báo thời gian thực và tin nhắn người dùng.
  2. **Thực trạng backend hiện tại**: Nhóm backend đã hoàn tất task **SPARK-188** trong `src/Services/ParkingService/` với endpoint chính thức:
     ```text
     GET /api/parking-lots/{id}/operational-layout
     ```
  3. **Đánh giá**: Tên gọi *"Facility & Visualization"* và *"Parking Operations"* **hoàn toàn không tồn tại** trong bất kỳ tài liệu kiến trúc hay mã nguồn nào của dự án. Đây là sự suy diễn/ảo giác từ mô hình AI của PM. Nếu tách dịch vụ theo đề xuất này, tài liệu thiết kế 3D sẽ bị ngắt kết nối hoàn toàn khỏi hệ thống backend .NET 10 thực tế.
* **Kết luận & Hành động**: Nhóm giữ nguyên thẩm quyền dữ liệu của **`ParkingService`** theo đúng kiến trúc chính thức v0.9, đồng thời cập nhật bảng mô tả kiến trúc 6-service rõ ràng trong `3D_BACKEND_SLOT_STATE_BINDING.md §2.2`.

---

### 2.2. ❌ Đánh đồng thiếu sót của Code Prototype FE cũ với Tài liệu Thiết kế mới

* **Nhận xét của AI PM (Mục 1, 4, 5, 6, 15):**
  > *"The slot-binding implementation currently associates model meshes with backend slots mainly by array/spatial order; fabricates missing slots as slot-auto-*; defaults missing state to available... This conflicts with SPARK-228."*
* **Thực tế:**
  - Logic map theo mảng `slots[index]` và sinh `slot-auto-*` thực sự tồn tại trong file mã nguồn prototype cũ [`FE/src/lib/3d/slotMatcher.ts`](file:///D:/My%20file/FA26%20-%20OJT%20FSoft/Mock%20Project%20OJT/Mock-Project-Smart-Parking/FE/src/lib/3d/slotMatcher.ts) từ Sprint 1.
  - Tuy nhiên, trong tài liệu đặc tả thiết kế mới ([`3D_BACKEND_SLOT_STATE_BINDING.md`](file:///D:/My%20file/FA26%20-%20OJT%20FSoft/Mock%20Project%20OJT/Mock-Project-Smart-Parking/docs/03-design/3d-digital-twin/3D_BACKEND_SLOT_STATE_BINDING.md) §4.2, §4.3), nhóm **đã chủ động phát hiện và cấm triệt để** điều này:
    > *"Array-index pairing (`slots[i] -> meshes[i]`) is strictly prohibited. Node mapping must resolve via SlotMappingRegistry using normalized slot_code. Missing backend slots must render UNMAPPED / UNKNOWN, never synthesize available slots."*
  - AI của PM đã đọc mã nguồn FE cũ và ngộ nhận rằng tài liệu thiết kế mới cũng mắc lỗi tương tự.
* **Kết luận & Hành động**: Đây là hạng mục nợ kỹ thuật (technical debt) của mã nguồn FE cần được tái cấu trúc (refactor) trong task **SPARK-228** để bắt kịp với tài liệu thiết kế mới, không phải là lỗi tư duy trong tài liệu thiết kế.

---

## 3. Các điểm đúng đắn đã tiếp thu và cập nhật vào tài liệu

Nhóm đã ghi nhận đầy đủ các đóng góp mang tính xây dựng và chuẩn hóa từ PM, cụ thể đã cập nhật vào 3 file tài liệu như sau:

### 3.1. Phân định Enterprise Ingestion thuộc Phase 2 (Đã sửa trong `3D_ENTERPRISE_MODEL_INGESTION_RULES.md`)
* **Hành động đã thực hiện**:
  - Chuyển `Status` thành: **`PHASE 2 — PROPOSED SPECIFICATION (Deferred from v1.0 MVP)`**.
  - Bổ sung Callout Banner thông báo phạm vi: SmartPark v1.0 MVP sử dụng **3 mô hình 3D archetype cố định** được đóng gói trực tiếp trong deployment assets. Mọi quy trình Owner tự upload file GLB hay cloud validation pipeline đều thuộc Phase 2 và không ràng buộc nghiệm thu v1.0 MVP.

### 3.2. Chuẩn hóa Tech Stack thực tế của Frontend (Đã sửa trong `3D_MODEL_IMPLEMENTATION_GUIDE.md`)
* **Hành động đã thực hiện**:
  - Cập nhật Bảng Công nghệ (Mục 4): Khẳng định stack chính thức là **React 19 + Vite 8 + TypeScript + Three.js** (xóa bỏ Next.js).
  - Dán nhãn rõ ràng: Mã nguồn hiện tại (`IMPLEMENTED`), Tùy chọn nâng cao (`OPTIONAL`), Tối ưu hóa khuyến nghị (`RECOMMENDED OPTIMIZATION`).
  - Làm rõ Draco WebAssembly (Mục 10): Hiện tại các mô hình MVP load trực tiếp bằng `GLTFLoader` chuẩn rất nhẹ và nhanh; cấu hình Draco là tùy chọn cho môi trường mạng yếu.

### 3.3. Thống nhất chuẩn Realtime SignalR & Cơ chế Phục hồi Reconnect Snapshot
* **Hành động đã thực hiện** (Đã sửa trong `3D_MODEL_IMPLEMENTATION_GUIDE.md §20` & `3D_BACKEND_SLOT_STATE_BINDING.md §6.2`):
  - Thay thế toàn bộ STOMP/WebSocket bằng **ASP.NET Core SignalR** (`/hubs/parking-lot`, event `SlotStateChanged`).
  - Thiết lập nguyên tắc bất biến: **Authoritative Reconnect Snapshot Rule**: Khi mất mạng và kết nối lại, Client tuyệt đối không chắp vá message bị thiếu, mà **phải lập tức gọi REST endpoint `GET /api/parking-lots/{id}/operational-layout`** để khôi phục snapshot thẩm quyền đầy đủ.

### 3.4. Minh bạch hóa số liệu Benchmark (Đã sửa trong `3D_MODEL_IMPLEMENTATION_GUIDE.md §25 & §28`)
* **Hành động đã thực hiện**:
  - Đổi toàn bộ nhãn bảng số liệu thành: **`[DESIGN TARGETS / LAB BENCHMARK ESTIMATES]`**.
  - Quy định rõ tiêu chuẩn nghiệm thu SPARK-186: Bắt buộc đính kèm trace Chrome DevTools với commit SHA, phần cứng GPU/CPU và cấu hình trình duyệt cụ thể.
  - Xóa lệnh test script giả định, thay thế bằng quy trình audit memory / draw call thủ công qua `renderer.info` và Chrome DevTools Memory tab.

### 3.5. Dọn dẹp tài nguyên GPU triệt để (Disposal Routine)
* **Hành động đã thực hiện** (Đã sửa trong `3D_MODEL_IMPLEMENTATION_GUIDE.md §25.1`):
  - Cập nhật hàm `disposeThreeScene`: Duyệt qua toàn bộ scene graph để dispose không chỉ Geometry, Material mà cả các **Textures đính kèm (`map`, `normalMap`, `roughnessMap`, `emissiveMap`,...)**, đảm bảo không rò rỉ VRAM khi chuyển trang.

---

## 4. Ma trận đối chiếu tổng hợp (Review Reconciliation Matrix)

| # | Vấn đề từ Review PM | Đánh giá của Nhóm Kỹ thuật | Hành động xử lý | Trạng thái |
| :---: | :--- | :--- | :--- | :---: |
| **1** | Đòi hỏi tách thành *Facility & Visualization* và *Parking Operations*. | ❌ **Sai hoàn toàn (Ảo giác)**. Không có trong kiến trúc v0.9 hay codebase. | Giữ nguyên vai trò của `ParkingService`, làm rõ roadmap 6 services trong doc binding. | **ĐÃ XỬ LÝ** |
| **2** | Phê bình mapping theo mảng `slots[index]` và tạo `slot-auto-*`. | ⚠️ **Đúng cho Code FE cũ, Sai cho Doc mới**. Doc mới đã cấm triệt để điều này. | Giữ nguyên chuẩn hóa `slot_code` trong doc, lập kế hoạch refactor code FE (SPARK-228). | **ĐÃ XỬ LÝ** |
| **3** | Chuyển Enterprise Ingestion thành Phase 2 Proposed Spec. | ✅ **Hoàn toàn đúng và hợp lý**. Phù hợp với scope v1.0 MVP. | Đổi Status tài liệu Ingestion Rules và thêm Scope Disclaimer. | **ĐÃ XỬ LÝ** |
| **4** | Chuẩn hóa Tech Stack về React 19 + Vite 8, bỏ Next.js. | ✅ **Hoàn toàn đúng**. Phù hợp với `FE/package.json`. | Cập nhật Mục 4 & Mục 10 trong Implementation Guide. | **ĐÃ XỬ LÝ** |
| **5** | Đổi Realtime sang SignalR và cơ chế Snapshot Reconnect. | ✅ **Hoàn toàn đúng**. Phù hợp với nền tảng ASP.NET Core .NET 10. | Cập nhật Mục 20 trong Guide và Mục 6.2 trong Binding doc. | **ĐÃ XỬ LÝ** |
| **6** | Chuyển số liệu đo đạc thành Design Targets nếu chưa có trace. | ✅ **Hoàn toàn đúng**. Chuẩn hóa quy trình nghiệm thu QA. | Dán nhãn Design Targets và bổ sung hướng dẫn DevTools trace. | **ĐÃ XỬ LÝ** |
| **7** | Dispose triệt để GPU texture/material khi unmount. | ✅ **Hoàn toàn đúng**. Tránh crash WebGL context trên trình duyệt. | Cập nhật hàm `disposeThreeScene` giải phóng toàn bộ map textures. | **ĐÃ XỬ LÝ** |
| **8** | Tách 2 trục trạng thái: 5 Physical và 4 Reservation. | ✅ **Đúng định hướng**. Đã có trong doc binding nhưng cần refactor code FE. | Khẳng định lại trong tài liệu; chuẩn bị triển khai trong SPARK-228. | **ĐÃ XỬ LÝ** |

---

## 5. Kế hoạch hành động tiếp theo (Next Steps)

1. **Về mặt Tài liệu (Documentation)**:
   - Toàn bộ 3 tài liệu chính thức (`3D_MODEL_IMPLEMENTATION_GUIDE.md`, `3D_ENTERPRISE_MODEL_INGESTION_RULES.md`, `3D_BACKEND_SLOT_STATE_BINDING.md`) đã được cập nhật hoàn tất, sẵn sàng để PM nghiệm thu task **SPARK-186**.
2. **Về mặt Mã nguồn Frontend (Implementation - Task SPARK-228)**:
   - Tiến hành refactor các file trong `FE/src/lib/3d/`:
     - Cập nhật [`FE/src/lib/types.ts`](file:///D:/My%20file/FA26%20-%20OJT%20FSoft/Mock%20Project%20OJT/Mock-Project-Smart-Parking/FE/src/lib/types.ts) sang mô hình 2 trục độc lập (`PhysicalState` & `ReservationState`).
     - Viết lại [`FE/src/lib/3d/slotMatcher.ts`](file:///D:/My%20file/FA26%20-%20OJT%20FSoft/Mock%20Project%20OJT/Mock-Project-Smart-Parking/FE/src/lib/3d/slotMatcher.ts): xóa bỏ hoàn toàn `slots[index]` và `slot-auto-*`, chuyển sang lookup $O(1)$ theo `slot_code`.
     - Nâng cấp `SlotMaterialManager.ts` sang mô hình 2 kênh (Channel A: xe proxy, Channel B: viền Emissive Halo).
     - Bổ sung cơ chế xử lý dữ liệu mất kết nối / không xác định (`UNKNOWN` / `STALE`), xóa bỏ hoàn toàn fallback về `available`.
