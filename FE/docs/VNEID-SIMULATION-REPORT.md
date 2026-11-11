# VNeID Simulation

| Hạng mục | Trạng thái | Kết quả |
|---|---|---|
| Driver | DONE | Form được kiểm tra trước khi mở demo. Chỉ gọi `registerDriver` sau khi người dùng hoàn tất demo. API hiện tại tạo thử thách OTP trong request đăng ký. |
| Owner | DONE | Demo chạy trước `ownerApi.register`. Setup tiếp tục bằng credential do API cấp. Hủy demo giữ nguyên form. Hồ sơ đang xử lý tiếp tục qua cơ chế khôi phục hiện có. |
| Operator | PARTIAL | Sau khi `POST /bootstrap-password` thành công, FE xóa tham số invitation khỏi URL và mở demo. Người dùng có thể đóng và mở lại demo trong cùng phiên. Hoàn tất demo chuyển sang Sign In. |
| Ràng buộc kích hoạt Operator phía server | BLOCKED BY BE | `/bootstrap-password` xác minh email và chuyển tài khoản sang ACTIVE trong cùng request. Server cần một checkpoint riêng nếu phải buộc hoàn tất onboarding trước khi cấp quyền. |
| UI dùng chung | DONE | Modal có năm bước, dữ liệu mẫu cố định, xác nhận, hai lượt quét có thể chạy lại, thông báo trạng thái và hành động theo vai trò. Animation dùng Motion và hỗ trợ reduced motion. |
| Kiểm thử trình duyệt và API thật | NOT STARTED | Chưa xác minh viewport, bàn phím, gửi OTP thật hoặc invitation thật trong đợt này. |

Demo chỉ tồn tại trong bộ nhớ của màn hình. Refresh trong demo đăng ký quay về form hoặc hồ sơ chờ đã có. Refresh sau khi API kích hoạt Operator sử dụng điểm khôi phục Sign In vì invitation đã được tiêu thụ. Dữ liệu mẫu chỉ hiển thị trong modal. Không có input CCCD, tải ảnh, truy cập camera hay request gửi dữ liệu demo.

## Tệp triển khai

- `src/components/ui/VNeIDSimulation.tsx`
- `src/roles/authentications/Sign Up/SignUp.tsx`
- `src/roles/authentications/BootstrapPassword.tsx`
- `tests/turnstile-auth-lifecycle.test.cjs`

## Kiểm thử

- **PASS** production build và TypeScript cho mã ứng dụng. TypeScript toàn dự án báo thiếu Vitest, Testing Library và MSW trong `src/test`.
- **237/238 PASS** bộ test Node. Hai test Driver xác nhận request đăng ký đợi demo. Test Owner xác nhận hủy không gọi API, rồi có thể làm lại với dữ liệu form cũ. Bộ Owner onboarding chạy qua 19 kiểm tra khôi phục.
- **FAIL** test payOS tại `tests/frontend/payment-checkout.test.cjs:51`. URL thực tế dùng localhost:5065, trong khi assertion đợi proxy tương đối `/payment-api`.
- **NOT STARTED** kiểm tra browser bằng Playwright CLI. CLI 0.1.24 khả dụng, dev server localhost:5173 chưa chạy lúc kiểm tra.
- **NOT STARTED** lint. Package không định nghĩa script lint.
