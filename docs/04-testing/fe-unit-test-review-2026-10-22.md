# Báo Cáo Đánh Giá: Sửa Lỗi Unit Test & Review Frontend Flow

- **Tài liệu tham chiếu:** Báo cáo sửa lỗi Unit Test & Review Frontend Flow của `@duykh`
- **Người review thẩm định:** AI Assistant (hỗ trợ `@Tăng Hồ Minh Quang`)
- **Branch kiểm tra:** `test/fe-unit-tests` (Commit `5c6b448e`) ➔ **Target branch:** `develop`
- **Ngày thực hiện:** 22/10/2026
- **Trạng thái thẩm định:** **ĐÃ XÁC MINH ĐỘC LẬP (VERIFIED)**

---

## 1. Đánh giá Frontend: Điểm làm tốt và Điểm chưa tốt

### 1.1. Những điểm Frontend đã làm tốt
1. **Phạm vi nghiệp vụ rộng và bám sát đặc tả 4 Role:**
   - Triển khai đầy đủ các luồng màn hình theo tài liệu `FE/docs/SMARTPARKING_FE_SCREEN_FLOW.md` cho 4 nhóm người dùng chính: **Driver**, **Parking Lot Owner**, **Operator**, và **System Admin**.
   - Phân định rõ ràng mã màn hình (`PUB`, `AUTH`, `DRV`, `OWN`, `OPR`, `FIN`, `POS`, `ADM`).
2. **Luồng xác thực & Onboarding chi tiết:**
   - **Driver:** Có quy trình xác thực OTP, đối soát giấy tờ (CCCD/GPLX), và xử lý trạng thái chờ duyệt tài khoản (`PENDING_APPROVAL`).
   - **Owner:** Cung cấp Onboarding Wizard 4 bước cấu hình bãi đỗ (Lot Type ➔ Structure ➔ Devices ➔ Pricing & Policy).
   - **Sign In:** Hỗ trợ linh hoạt cả Password và OTP Challenge, có cơ chế countdown và gửi lại mã xác thực.
3. **Phân quyền và bảo vệ truy cập (Role-based Navigation):**
   - Sử dụng `RoleRoute` và quản lý phiên trong `AppContext`.
   - Tự động điều hướng an toàn: người dùng chưa đăng nhập, phiên hết hạn hoặc không đủ quyền sẽ được đưa về Landing Page hoặc trang Sign In tương ứng.
4. **Trực quan hóa cấu trúc bãi đỗ (Visual Parking Structure & 3D Viewer):**
   - Hỗ trợ mô hình cây phân cấp Unit (Floor/Zone) ➔ Slot (chỗ đỗ).
   - Tích hợp component 3D Viewer (`Parking3DViewer.tsx`) hỗ trợ hiển thị mặt bằng bãi đỗ trực quan.

---

### 1.2. Những điểm Frontend CHƯA TỐT (Hạn chế & Rủi ro kỹ thuật)
1. **Kiến trúc SPA bị dồn vào một URL `/` duy nhất (State-driven SPA thay vì URL Routing):**
   - Toàn bộ màn hình của Driver, Owner, Admin đều render qua `AppContext.view` và tab state cục bộ tại URL `/` (chỉ Operator có một số route riêng như `/dashboard/operation`, `/dashboard/finance`, `/pos`).
   - **Hậu quả:** Khi người dùng F5/Reload trình duyệt hoặc muốn bookmark/chia sẻ liên kết một trang cụ thể (ví dụ đang ở tab "Parking Structure" hay "Revenue"), toàn bộ state bị mất và người dùng bị quay lại tab mặc định.
   - Giao tiếp giữa Navbar/Sidebar và Dashboard dùng Custom Event `sp:dashboard-nav` phát tán qua `window.dispatchEvent` thay vì dùng React Router navigate chuẩn — đây là anti-pattern trong React, gây khó khăn cho việc debug và tiềm ẩn rủi ro rò rỉ bộ nhớ.
2. **Kích thước Bundle quá lớn, thiếu Code Splitting (Lazy Loading):**
   - Kết quả build cho thấy: `dist/assets/index-DEJF9EgW.js` nặng tới **3.15 MB** (gzip: 825 KB). Trình đóng gói Vite đã đưa ra cảnh báo: `(!) Some chunks are larger than 500 kB after minification`.
   - Toàn bộ mã nguồn của cả 4 role và thư viện đồ họa 3D (Three.js) đều được đóng gói chung và tải về ngay lần đầu truy cập. Cần áp dụng `React.lazy()` và `Suspense` để tách bundle theo từng Role/Dashboard.
3. **Vấn đề đồng bộ State và Xử lý Bất đồng bộ (Async/Await):**
   - Trước khi sửa, các hàm mutate dữ liệu như `addUnit`, `addSlot` trong `ParkingStructure.tsx` không có `async/await`, dẫn đến việc modal đóng trước khi server hoàn tất ghi nhận dữ liệu.
   - Dự án phụ thuộc nhiều vào việc tăng biến đếm `setReload(r => r + 1)` để gián tiếp kích hoạt `useEffect` fetch lại dữ liệu, dễ gây race condition và render thừa.
4. **Tồn tại tính năng chưa hoàn thiện và Mock API:**
   - Trong tài liệu `SMARTPARKING_FE_SCREEN_FLOW.md`, nhiều luồng vẫn đang ở trạng thái **Blocked** (như luồng giữ chỗ `Hold attempt` trong `BookingFlow`).
   - Một số API chưa có trên Backend phải dùng hàm giả lập (stub) như `operatorsApi.reassignSite()`.
5. **Kỷ luật kiểm tra kiểu (Type-checking) chưa nghiêm:**
   - Để lọt các lỗi TypeScript cơ bản (`'inputRef.current' is possibly 'null'`, gọi biến `setBookingId` không tồn tại), chứng tỏ dev chưa chạy `tsc --noEmit` trước khi commit mã nguồn.
6. **Bộ Unit Test đặc thù và dễ vỡ (Fragile Test Harness):**
   - Bộ test ở `tests/frontend/` dùng Node VM (`node:vm`) tự biên dịch file `.tsx` bằng runtime TypeScript và mock thủ công DOM/React.
   - Khi giao diện thêm icon mới (`UntitledIcon`), bọc thêm thẻ hay đổi cấu trúc HTML, bộ test sẽ crash vì thiếu mock, làm chậm tiến độ tích hợp liên tục (CI/CD).

---

## 2. Chi tiết các phần Tester (@duykh) đã sửa đổi

Trong commit `5c6b448e` thuộc nhánh `test/fe-unit-tests`, tester đã thay đổi tổng cộng **10 files**:

### 2.1. Thay đổi trong mã nguồn ứng dụng (`FE/src/` - 6 files)
1. **`FE/src/roles/authentications/Sign In/SignIn.tsx`:**
   - Tái tích hợp hàm kiểm tra định dạng email/số điện thoại: `isValidSignInContact(email)`.
   - Bổ sung thuộc tính `maxLength={255}` vào ô nhập tài khoản.
   - Bổ sung thuộc tính `role="alert"` cho khối hiển thị thông báo lỗi để hỗ trợ accessibility và khớp với bộ test.
2. **`FE/src/roles/owner/operators/CreateOperatorForm.tsx`:**
   - Bọc toàn bộ các input của form trong `<fieldset disabled={busy}>` giúp tự động vô hiệu hóa toàn bộ control khi form đang submit.
   - Bổ sung tùy chọn `<option value="all">All active sites</option>` và cập nhật logic `siteIds` khi người dùng chọn tất cả hoặc để trống.
   - `export` hằng số `DEFAULT_OPERATOR_PERMISSIONS`.
   - Chuẩn hóa thông báo lỗi validate email thành `"Enter a valid email address."`.
   - Bổ sung xử lý thông báo lỗi trả về từ server (như email đã tồn tại).
3. **`FE/src/roles/owner/structure/ParkingStructure.tsx`:**
   - Chuyển `addUnit` và `addSlot` sang hàm `async`, bổ sung `await mutate(...)`.
   - Ngay sau khi mutate thành công, chủ động gọi `parkingApi.structure(siteId)` để cập nhật state cấu trúc mới nhất cho UI thay vì chỉ phụ thuộc vào `setReload`.
4. **`FE/src/components/forms/GroupedNumberInput.tsx`:**
   - Sửa lỗi TypeScript `TS18047`: Bổ sung kiểm tra `&& inputRef.current` tại `useLayoutEffect`.
5. **`FE/src/roles/driver/support/DriverSupport.tsx`:**
   - Sửa lỗi TypeScript `TS2304`: Xóa dòng `setBookingId("")` thừa còn sót lại sau refactor.
6. **`FE/src/lib/parkingApi.ts`:**
   - Khai báo bổ sung phương thức stub `reassignSite(operatorId: string, siteIds: string[])` vào `operatorsApi`, ném ra lỗi rõ ràng: `"Operator reassignment is not yet supported by the backend."` để sửa lỗi thiếu method khi `SiteManagement` gọi.

### 2.2. Thay đổi trong bộ Unit Test (`tests/frontend/` - 4 files)
1. **`tests/frontend/auth-api.test.cjs`:**
   - Cập nhật regex parse import hỗ trợ cả dấu ngoặc kép `"` lẫn ngoặc đơn `'`.
2. **`tests/frontend/auth-context.test.cjs`:**
   - Bổ sung mock cho `DriverOtpForm`.
3. **`tests/frontend/sign-in.test.cjs`:**
   - Bổ sung mock cho component icon mới: `UntitledIcon`.
4. **`tests/frontend/operator-provisioning.test.cjs`:**
   - Tiêm mock `sessionStorage` và `document: { body: {} }` vào context của Node VM.
   - Bổ sung mock cho `PasswordVisibilityIcon` và `UntitledIcon`.
   - Cập nhật fixture để tìm đúng selector ID mới: `op-full-name`, `op-email`, `op-password`, `op-confirm-password`, `op-site`.
   - Cập nhật fixture cho flow mở modal (`createModal === 'unit' | 'slot'`) của `ParkingStructure`.
   - Cập nhật assertion kiểm tra thông báo lỗi linh hoạt hơn.

---

## 3. Mã sửa đổi có liên quan tới Backend (BE) không?

- **Khẳng định:** **KHÔNG CÓ BẤT KỲ DÒNG CODE BACKEND (C# / .NET) NÀO BỊ THAY ĐỔI** trong toàn bộ branch này. 100% thay đổi nằm ở `FE/src/` và `tests/frontend/`.
- **Mức độ liên quan về mặt API Contract:**
  - **Tương thích thông báo lỗi:** FE được điều chỉnh để bắt đúng định dạng chuỗi lỗi từ Backend (như lỗi trùng email từ AuthService).
  - **Xử lý khoảng trống API:** Do Backend chưa có endpoint gán lại bãi đỗ cho Operator, tester đã thêm stub method `reassignSite` ở FE với thông báo rõ ràng thay vì để ứng dụng crash.
  - **Xác minh không phá vỡ Backend:** Mục kiểm tra Backend (`dotnet test`) trong báo cáo của tester chỉ là bước chạy nghiệm thu regression test, không tác động vào mã nguồn dịch vụ.

---

## 4. Kết quả Kiểm tra và Chạy thử Thực tế (Verification)

Quá trình chạy kiểm thử độc lập trực tiếp trên commit `5c6b448e` cho kết quả như sau:

| Hạng mục kiểm tra | Lệnh thực thi | Kết quả thực tế | Đánh giá |
| :--- | :--- | :--- | :--- |
| **Frontend Unit Tests** | `node --test tests/frontend/*.test.cjs` | **73/73 PASSED (100%)** (~1.86s)<br>• `auth-api`: 21/21 passed<br>• `auth-context`: 9/9 passed<br>• `operator-provisioning`: 22/22 passed<br>• `sign-in`: 21/21 passed | ✅ Khớp 100% báo cáo |
| **TypeScript Typecheck** | `npx tsc --noEmit` (tại `FE/`) | **Exit Code 0 (Clean, 0 errors)** | ✅ Khớp 100% báo cáo |
| **Frontend Production Build** | `npm run build` (tại `FE/`) | **Build thành công (2.81s)** tạo đầy đủ thư mục `dist/` | ✅ Khớp 100% báo cáo |

### ⚠️ Lưu ý cảnh báo khi Merge nhánh:
- Commit `5c6b448e` của tester đã khắc phục **chính xác và hoàn chỉnh** các lỗi trên nền nhánh `develop`.
- Tuy nhiên, nếu merge vào các nhánh UI mới hơn (như `fe-v2.2` hiện tại đang có thay đổi bổ sung `PageHeader` vào `SignIn.tsx`), bộ test Node VM sẽ bị fail lại do thiếu mock `PageHeader`. Reviewer cần lưu ý điểm này để cập nhật mock khi tích hợp vào nhánh UI mới.

---

## 5. Khuyến nghị dành cho Reviewer (@Tăng Hồ Minh Quang)

1. **Về PR `test/fe-unit-tests`:**
   - **Khuyến nghị: DUYỆT (APPROVE)** merge vào `develop`. Các bản vá của tester giải quyết đúng các lỗi Typecheck, async state và khôi phục tỷ lệ pass 100% cho bộ unit test hiện hữu.
2. **Kế hoạch cải tiến kỹ thuật cho Frontend (Technical Debt):**
   - **Chuyển đổi Test Runner:** Nên lập kế hoạch chuyển bộ test FE từ custom Node VM sang **Vitest + React Testing Library (JSDOM)** để test bền vững hơn, không bị gãy mỗi khi đổi component giao diện.
   - **Tối ưu Router:** Chuyển đổi dần các tab sang URL Route cụ thể (`/driver/bookings`, `/owner/structure`) để người dùng có thể F5 và bookmark trang.
   - **Code Splitting:** Áp dụng dynamic import `React.lazy()` cho từng Dashboard vai trò để giảm dung lượng file bundle từ 3.15 MB xuống mức an toàn (< 500 KB).
