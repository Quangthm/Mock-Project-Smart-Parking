# Driver Features Specification & Implementation Inventory

> **Document Status:** Feature inventory; verify against current source before implementation  
> **Source Module:** `FE/src/roles/driver/`  
> **Prototype Architecture:** Feature-based / Domain-driven (`van/src/features/`)  
> **Visual Standard:** [`../design-system.md`](../design-system.md)

---

## 1. Tổng quan Kiến trúc Miền nghiệp vụ Driver

Phân hệ Driver (Tài xế) phục vụ người dùng cuối trong việc tìm kiếm bãi xe, đặt trước chỗ đỗ (2D/3D), quản lý phương tiện cá nhân, mua vé tháng/năm và tra cứu lịch sử chi phí.

Trong prototype `van`, phân hệ Driver được tổ chức theo feature để thử nghiệm thiết kế lại dựa trên nguồn `FE/`. Tài liệu này là inventory/đặc tả tính năng cần đối chiếu với mã nguồn và yêu cầu hiện hành; nó không khẳng định mọi chức năng đã được triển khai hoặc kết nối.

```text
van/src/features/
├── auth/                 # Đăng ký, đăng nhập OTP, 2FA/MFA, hồ sơ tài khoản
├── vehicle/              # Quản lý phương tiện Việt Nam (CAR, MOTORCYCLE)
├── parking-discovery/    # Tìm kiếm bãi đỗ, bản đồ MapTiler 2D, Geolocation GPS
├── booking/              # Luồng đặt chỗ 3 bước (3D/2D Slot Picker, Concurrency, Checkout)
├── subscription/         # Chợ vé tháng/năm, mã QR quét barrier, quản lý thẻ vé
└── support/              # Khiếu nại, ticket hỗ trợ Operator, lịch sử biên lai VAT 8%
```

---

## 2. Bảng Kiểm kê Chi tiết Tính năng (Feature Inventory)

### 2.1. Feature: Authentication & Account Security (`features/auth`)
*Quản lý danh tính, bảo mật và thông tin tài khoản tài xế.*

- **Màn hình & Component liên quan:** `SignUp.tsx`, `DriverOtpForm.tsx`, `SignIn.tsx`, `BootstrapPassword.tsx`, `AccountSecurityPanel.tsx`.
- **Trạng thái kết nối Backend:** **Đã kết nối API thực tế** qua `FE/src/lib/authApi.ts` (`/api/auth/*`).
- **Các tính năng đã tích hợp:**
  1. **Đăng ký tài khoản Driver:** Form nhập Họ tên, Email/SĐT, kiểm định mật khẩu 6 tiêu chí (độ dài 8-15 ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt).
  2. **Quy trình OTP Challenge:** Gửi mã OTP kích hoạt qua Email/SMS, đếm ngược gửi lại mã (`resendDriver`), khôi phục phiên đăng ký đang chờ (`recoverDriver`).
  3. **Đăng nhập linh hoạt:** Đăng nhập truyền thống bằng mật khẩu hoặc đăng nhập không mật khẩu qua OTP challenge (`/otp/request` + `/otp/login`).
  4. **Xác thực 2 bước (2FA / TOTP):** Tạo Secret Key và QR URI (`/mfa/setup`), sao chép key một chạm, kích hoạt bằng mã 6 số (`/mfa/enable`).
  5. **Hồ sơ cá nhân:** Cập nhật Họ tên, Số điện thoại cá nhân (Email được khóa chỉ đọc để bảo mật định danh).

---

### 2.2. Feature: Vehicle Management (`features/vehicle`)
*Quản lý danh sách phương tiện cá nhân phục vụ nhận diện biển số tại cổng và đặt chỗ.*

- **Màn hình & Component liên quan:** `VehicleManager.tsx`, tích hợp trong `BookingFlow.tsx` và `Subscriptions.tsx`.
- **Trạng thái kết nối Backend:** **Đã kết nối API thực tế** qua `FE/src/lib/vehiclesApi.ts` (`/api/vehicles`).
- **Các tính năng đã tích hợp:**
  1. **Danh sách xe đã đăng ký:** Hiển thị thẻ xe gồm loại xe, biển số thô, biển số chuẩn hóa.
  2. **Đăng ký xe mới & Chỉnh sửa xe (`POST` / `PATCH`):**
     - Phân loại phương tiện: Ô tô (**CAR**) hoặc Xe máy (**MOTORCYCLE**).
     - **Chuẩn hóa & Validate biển số Việt Nam (5 số):**
       - Xe ô tô: Định dạng chuẩn `51A-123.45` (Regex: `/^[1-9][0-9][A-Z][0-9]{5}$/`).
       - Xe máy: Định dạng chuẩn `59-B1 123.45` (Regex: `/^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$/`).
     - Tự động định dạng khi blur (`formatVietnamesePlateValue`).
     - Yêu cầu liên kết hình ảnh xe lưu trữ qua giao thức an toàn `https://`.

---

### 2.3. Feature: Parking Discovery & Interactive Map (`features/parking-discovery`)
*Tìm kiếm, lọc và định vị bãi đỗ xe trực quan trên bản đồ số.*

- **Màn hình & Component liên quan:** `FindParking.tsx`, `LotPolicyModal.tsx`.
- **Trạng thái kết nối Backend:** Kết hợp **MapTiler SDK (`@maptiler/sdk`)** + Client Geolocation API + Store dữ liệu bãi xe.
- **Các tính năng đã tích hợp:**
  1. **Bản đồ 2D tương tác:** Hiển thị các bãi đỗ xe đang hoạt động (`active`) trên nền MapTiler streets.
  2. **Marker trạng thái trực quan:**
     - Marker xanh lá: Bãi còn chỗ trống (`available > 0`).
     - Marker đỏ: Bãi đã kín chỗ.
     - Popup chi tiết khi click marker: Tên bãi, địa chỉ, số chỗ trống, đơn giá giờ (VND/giờ).
  3. **Định vị GPS ("Use my location"):**
     - Lấy tọa độ GPS người dùng từ trình duyệt (`navigator.geolocation`).
     - Tính khoảng cách thực tế theo công thức Haversine (`distanceKm`).
     - Camera tự động lia mượt mà (`flyTo`) đến vị trí người dùng (Zoom level 14).
     - Tự động sắp xếp danh sách bãi xe theo thứ tự gần nhất.
  4. **Tìm kiếm bãi xe:** Lọc theo thời gian thực theo Tên bãi hoặc Địa chỉ.
  5. **Xem chính sách bãi xe:** Modal xem quy định đỗ xe, quy chế an ninh và chính sách hủy bãi qua PDF (`LotPolicyModal`).
  6. **Nút "Book this lot":** Chuyển bãi xe được chọn sang luồng đặt chỗ.

---

### 2.4. Feature: Parking Slot Booking Flow (`features/booking`)
*Luồng đặt trước chỗ đỗ xe 3 bước kết hợp công nghệ 3D Digital Twin.*

- **Màn hình & Component liên quan:** `BookingFlow.tsx`, `Parking3DViewer.tsx`.
- **Trạng thái kết nối Backend:** Giao diện hoàn chỉnh; hiện có thông báo chặn chờ kết nối API Backend Booking (`POST /api/bookings`).
- **Các tính năng đã tích hợp:**
  1. **Tiến trình 3 bước:** `Select Slot` ➔ `Time & Vehicle` ➔ `Payment`.
  2. **Chọn ô đỗ linh hoạt với 2 chế độ xem (View Modes):**
     - **Chế độ 3D Interactive (`Parking3DViewer`):** Render mô hình không gian bãi xe thời gian thực bằng Three.js. Hỗ trợ xoay, zoom, pan và click chuột trực tiếp vào mô hình ô đỗ 3D để chọn slot.
     - **Chế độ Lưới 2D (Grid View):** Phân chia theo từng tầng (Floor 1, 2, ...). Mã màu từng ô: Xanh lá (Available), Đỏ (Occupied), Vàng (Reserved). Hỗ trợ kiểm tra slot concurrency (ngăn chọn slot đã có xe hoặc đang bị người khác giữ).
  3. **Chọn khung thời gian & Phương tiện:**
     - Datetime-local picker cho giờ vào (Start time) và giờ ra (End time).
     - Tự động tính số giờ đỗ và tổng chi phí dự kiến (`Math.ceil(hours) * hourlyRate`).
     - Dropdown chọn xe từ danh sách phương tiện đã đăng ký (API `/api/vehicles`).
     - Form thêm nhanh xe mới ngay trong luồng đặt chỗ.
  4. **Phương thức thanh toán:**
     - Hỗ trợ 6 cổng thanh toán: MoMo, VNPAY, VietQR, Thẻ quốc tế Visa/Mastercard, Apple Pay, ZaloPay.
     - Màn hình hướng dẫn thanh toán QR Code (VietQR).
     - Tra cứu Parking Policies ngay tại bước thanh toán.

---

### 2.5. Feature: Active Booking & 3D Slot Wayfinding (`features/booking`)
*Quản lý lượt đặt xe đang hiệu lực và dẫn đường vị trí đỗ.*

- **Màn hình & Component liên quan:** `ActiveBookings.tsx`, `BookingCard.tsx`.
- **Trạng thái kết nối Backend:** Component UI đã xây dựng đầy đủ; màn hình tab hiện để placeholder chờ API backend.
- **Các tính năng đã tích hợp:**
  1. **Thẻ đặt chỗ (`BookingCard`):** Hiển thị Booking ID, tên bãi, số ô đỗ, biển số xe, thời gian bắt đầu/kết thúc, số tiền VND, badge trạng thái đặt chỗ (`confirmed`, `checked-in`, `completed`, `cancelled`) và trạng thái thanh toán (`paid`, `pending`, `failed`).
  2. **Dẫn đường ô đỗ 3D ("View in 3D"):**
     - Mở Modal 3D qua React Portal độc lập với layout trang.
     - **Tự động lấy nét camera (`focusSlotId`):** Camera 3D tự động bay tới và zoom cận cảnh vào đúng ô đỗ đã đặt của tài xế.
  3. **Thanh toán bù ("Pay Now"):** Kích hoạt lại luồng thanh toán nếu đơn bị pending hoặc failed.

---

### 2.6. Feature: Commuter Passes & Subscriptions (`features/subscription`)
*Thị trường mua vé tháng/năm và quản lý thẻ xe định kỳ.*

- **Màn hình & Component liên quan:** `Subscriptions.tsx`.
- **Trạng thái kết nối Backend:** Local Store UI Workflow (sẵn sàng nối API Package / Pass).
- **Các tính năng đã tích hợp:**
  1. **Pass Marketplace (Chợ vé):**
     - Banner khuyến mãi phong cách giao thông đô thị hiện đại.
     - Tìm kiếm theo Tên bãi, Địa chỉ hoặc Tên doanh nghiệp vận hành.
     - Bộ lọc danh mục nhanh: Tất cả, "🔥 Hot", "📍 Gần bạn", "✨ Đề xuất".
     - Thẻ thông tin gói vé: hiển thị giá vé tháng (Monthly Pass - 30 ngày) và vé năm (Annual Pass - 365 ngày kèm tỷ lệ tiết kiệm).
     - Modal thanh toán mua vé: chọn thời hạn gói, gán biển số xe đã đăng ký, chọn phương thức thanh toán và kích hoạt vé tức thì.
  2. **My Passes (Vé của tôi):**
     - Danh sách vé đang hiệu lực hoặc hết hạn.
     - **Tạo mã QR Barrier:** Dùng để quét camera/máy quét tại cổng vào/ra tự động.
     - Bộ đếm ngược số ngày còn hạn sử dụng, thông tin xe được gán, nút gia hạn vé ("Renew Pass").
  3. **Order History (Lịch sử đơn mua gói):**
     - Bảng kê hóa đơn mua vé định kỳ kèm mã đơn, thời gian mua, số tiền và trạng thái thanh toán.

---

### 2.7. Feature: Support, Tickets & Financial Records (`features/support`)
*Trung tâm xử lý khiếu nại, hỗ trợ tài xế và tra cứu hóa đơn.*

- **Màn hình & Component liên quan:** `DriverSupport.tsx`, `BookingHistory.tsx`, `PaymentHistory.tsx`, `PaymentDetailModal.tsx`.
- **Trạng thái kết nối Backend:** Ticket & Notification Store UI; Payment Receipt logic.
- **Các tính năng đã tích hợp:**
  1. **Ticket Hỗ trợ & Khiếu nại (`DriverSupport`):**
     - Form gửi khiếu nại/yêu cầu trợ giúp (Subject, Message) đến bộ phận Operator.
     - Tự động kích hoạt thông báo hệ thống gửi về chuông thông báo của tài xế.
  2. **Lịch sử Ticket (`BookingHistory` - Tab Tickets):**
     - Danh sách ticket đã gửi kèm ngày giờ, mã Booking liên quan.
     - Badge trạng thái xử lý: Đang xử lý ("Processing" - cam) hoặc Đã xử lý ("Resolved" - xanh lá).
  3. **Chi tiết Hóa đơn & Biên lai Thanh toán (`PaymentDetailModal`):**
     - Bóc tách chi phí minh bạch theo chuẩn VAT 8%: Tiền trước thuế, thuế VAT, phí tiện ích bãi xe (82%), phí nền tảng.
     - Chức năng in biên lai (Print) tự động ẩn thanh công cụ điều khiển.
     - Chức năng sao chép mã giao dịch (Copy Transaction ID).
     - Chức năng thử lại thanh toán (*Retry Payment*) với phương thức thay thế.

---

### 2.8. Cross-Cutting UI Capabilities (Tiện ích dùng chung trên Dashboard)
- **Trợ lý ảo AI nổi (`FloatingChatbot.tsx`):** Nằm cố định ở góc phải màn hình, hỗ trợ giải đáp các câu hỏi thường gặp về cách đặt chỗ, quy định bãi và hướng dẫn thanh toán.
- **Chuông thông báo thời gian thực (`NotificationBell.tsx`):** Nhận sự kiện thông báo (`storage`, `sp-data-change`), hiển thị popup toast thông báo mới và badge số lượng chưa đọc.
- **Tùy biến giao diện (Appearance):** Chuyển đổi Dark/Light mode nếu được hỗ trợ. Không cung cấp các palette accent tùy chọn trừ khi có quyết định sản phẩm mới phù hợp với [`../design-system.md`](../design-system.md).
- **Ví điện tử SmartParking Wallet (`WalletView.tsx` & `TopUpModal.tsx`):** Quản lý số dư ví, nạp tiền theo mệnh giá định sẵn, lưu nhật ký giao dịch và audit log (thành phần code đã có sẵn).

---

## 3. Nguyên tắc áp dụng trong prototype (`van/`)

Khi dựng lại các tính năng trên tại `van/`, tuân thủ các nguyên tắc sau:

1. Xác nhận tính năng, quyền và hành vi dựa trên `FE/`, API/DTO/validator và policy hiện hành trước khi thiết kế hoặc mô tả trạng thái.
2. Tuân thủ thứ bậc màn hình, responsive behavior, tokens, typography và spacing tại [`../design-system.md`](../design-system.md).
3. Dùng component và kiến trúc phù hợp với dự án; CVA/Radix/`cn()` áp dụng khi tạo giá trị thực, không phải yêu cầu hình thức cho mọi component.
4. Fixture trong prototype phải được ghi nhãn, không trình bày như bãi, giá, số chỗ hoặc tích hợp live đã xác nhận.
