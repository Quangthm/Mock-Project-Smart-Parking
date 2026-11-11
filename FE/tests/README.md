# Frontend Automated Testing Suite (SmartPark FE)

Tài liệu hướng dẫn cài đặt, thực thi và bảo trì toàn bộ các bài kiểm tra tự động cho ứng dụng SmartPark Frontend (React 19 + TypeScript + Vite 8).

---

## 1. Cấu trúc và phân loại kiểm thử

Bộ kiểm thử được chia thành 4 cấp độ rõ ràng theo chuẩn kiến trúc:

| Loại kiểm thử | Công cụ / Thư viện | Thư mục mã nguồn | Mục tiêu kiểm tra |
|---|---|---|---|
| **Unit Tests** | Vitest (v8) | `FE/src/test/unit/` | Các hàm thuần túy: State mapping (Parking, Payment), Formatters, Phone/Email normalization, Fail-closed logic. |
| **Component Tests** | React Testing Library + user-event | `FE/src/test/component/` | Kiểm tra giao diện người dùng, trạng thái tải/vô hiệu hóa, chống duplicate submit, accessibility roles, StaleDataBanner. |
| **Integration Tests** | Vitest + MSW (Mock Service Worker) | `FE/src/test/integration/` | Tích hợp giữa API Clients (`authApi`, `paymentApi`, `parkingApi`) và quản lý phiên (Session, Token expiry, Auth guards, Error handling 400/401/403/404/500). |
| **E2E / Browser Tests** | Playwright | `FE/e2e/` | Hành trình người dùng trên trình duyệt (Desktop Chrome, Mobile Chrome Pixel 5), kiểm tra layout responsive và bảo vệ tuyến đường (Protected routes). |
| **Node/SSR Regression** | Node.js Test Runner | `FE/tests/owner-onboarding.test.mjs` | Bộ 19 kiểm thử hồi quy độc lập cho Owner onboarding và storage migration (được bảo tồn nguyên vẹn). |

---

## 2. Các lệnh thực thi kiểm thử (`package.json`)

Tất cả các lệnh được chạy từ thư mục `FE/`:

```bash
# 1. Chạy toàn bộ kiểm thử Unit, Component, Integration + Node Regression (Dùng cho CI)
npm test

# 2. Chạy riêng bài kiểm thử Đơn vị (Unit)
npm run test:unit

# 3. Chạy riêng bài kiểm thử Thành phần (Component)
npm run test:component

# 4. Chạy riêng bài kiểm thử Tích hợp API (Integration - MSW)
npm run test:integration

# 5. Chạy báo cáo độ bao phủ mã (Coverage)
npm run test:coverage

# 6. Chạy kiểm thử hồi quy Node có sẵn
npm run test:regression

# 7. Chạy kiểm thử Trình duyệt Đầu cuối (E2E Playwright)
# Yêu cầu cài browser một lần đầu: npx playwright install
npm run test:e2e
```

---

## 3. Ma trận độ bao phủ luồng quan trọng (Critical Flow Matrix)

| Luồng quan trọng | Loại test | File kiểm thử | Các ca kiểm thử trọng yếu được bảo vệ |
|---|---|---|---|
| **Xác thực & Phiên làm việc (Auth)** | Unit / Component / Integration | `state-and-utils.test.ts`<br>`api-integration.test.ts`<br>`MfaCodeInput.tsx` | - Đăng nhập hợp lệ lưu Token & User.<br>- Lỗi 401/403 không lưu Token/Identity.<br>- Hết hạn phiên tự động kích hoạt `authExpiredEvent`.<br>- Nhập mã MFA số, chống ký tự không hợp lệ. |
| **Bảo vệ Bãi đỗ xe & Fail-Closed** | Unit / Component / Integration | `state-and-utils.test.ts`<br>`ui-components.test.tsx`<br>`api-integration.test.ts` | - **CRITICAL**: Không bao giờ hiển thị trạng thái `UNKNOWN` hoặc rỗng thành `available`.<br>- Chỉ có `AVAILABLE + UNRESERVED` mới được đặt chỗ.<br>- `BACKUP` có visual state riêng biệt với `RESERVED`.<br>- Hiển thị `StaleDataBanner` khi dữ liệu vận hành bị cũ / mất kết nối trực tiếp. |
| **Thanh toán & Đơn hàng (Payment)** | Unit / Component / Integration | `state-and-utils.test.ts`<br>`ui-components.test.tsx`<br>`api-integration.test.ts` | - Định dạng UUID đơn hàng thanh toán.<br>- Kiểm tra URL cổng PayOS đáng tin cậy (`https://pay.payos.vn`).<br>- Phân biệt rõ ràng `SUCCESS` (chỉ khi `isAuthoritative = true`) với `PENDING` và `FAILED`.<br>- Không bao giờ giả mạo trạng thái thành công khi gateway timeout hoặc lỗi mạng.<br>- Nút bấm `Button` có `loading=true` tự động disable + `aria-busy=true` để **chống gửi trùng (duplicate submit)**. |
| **Điều hướng & Tương thích Di động** | E2E | `navigation.spec.ts` | - Kiểm tra điều hướng trang chủ và tiêu đề.<br>- Kiểm tra responsive layout trên Mobile Viewport (chiều rộng hẹp <= 450px) không vỡ khung.<br>- Kiểm tra bảo vệ route khi không có token. |

---

## 4. Chính sách Mocking và Tính xác định (Determinism)

1. **Tuyệt đối không dùng dữ liệu nhạy cảm (No PII):**
   - Không commit thông tin thẻ thật, mật khẩu tài khoản thật, hoặc SMS/email cá nhân.
   - Sử dụng các hòm thư test và mã token đại diện (`valid-token`, `driver@example.com`).
2. **Dọn dẹp môi trường (Cleanup & Reset):**
   - File `FE/src/test/setup.ts` tự động `cleanup()` DOM và xóa sạch `localStorage`, `sessionStorage` sau mỗi test.
   - Máy chủ MSW tự động `resetHandlers()` sau mỗi test case, đảm bảo các test độc lập 100%.
3. **Phân biệt Mock Test và Live Server Test:**
   - Các bài kiểm tra Vitest + MSW chạy mô phỏng mạng để kiểm thử logic xử lý và chuyển đổi trạng thái của FE.
   - Bài test Playwright kết nối đến preview server cục bộ để kiểm tra DOM render thực tế.

---

## 5. Tích hợp CI (GitHub Actions)

Workflow tự động được cấu hình tại [`.github/workflows/frontend-ci.yml`](../../.github/workflows/frontend-ci.yml):
- Tự động kích hoạt khi có Pull Request hoặc Push ảnh hưởng đến thư mục `FE/`.
- Thực hiện kiểm tra bản dựng (`npm run build`), chạy toàn bộ test suite (`npm test`), và xuất báo cáo coverage (`npm run test:coverage`).
- Bất kỳ bài kiểm thử nào thất bại sẽ chặn PR merge để bảo vệ độ ổn định của nhánh chính.
