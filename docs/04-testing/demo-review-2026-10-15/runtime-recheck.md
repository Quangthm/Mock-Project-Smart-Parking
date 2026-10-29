# Kiểm tra lại sau khi Docker đã chạy — 15/10/2026

Phạm vi theo yêu cầu: kiểm tra lại setup và review readiness cho demo 16/10, giữ nguyên lựa chọn review/checklist từ lượt trước. Ba agent đã kiểm tra lại identity, frontend và operations. Không sửa source ứng dụng hay migration; không gửi OTP/email và không chạy payment thật.

## Kết quả mới đã xác nhận

| Kiểm tra | Kết quả |
| --- | --- |
| Docker Compose | Cả 6 container User/Parking/Reservation/Payment/Notification/IoT healthy; DB bind loopback 5441–5446 |
| User API 5035 | Đang trả lời; OpenAPI 200; endpoint bảo vệ không token trả 401 |
| Parking API 5045 | Đang trả lời; root `/` trả 404, không phải health endpoint |
| Reservation API 5055 | Đang trả lời; request không service key trả 401 |
| Frontend 5173 | HTTP 200; module Vite đang phục vụ đúng các component đã review |
| Login/logout Admin thật | Login thành công, `/me` thành công, đọc Owner applications thành công; logout thành công; gọi `/me` bằng token cũ trả 401 |
| Migration User | `auth_challenges`, `workflow_deliveries`, `mfa_recovery_codes` tồn tại |
| Migration Parking | `structure_synchronizations` tồn tại |
| Migration Reservation | `structure_release_receipts` tồn tại |
| UserService tests với PostgreSQL | **83 passed, 0 failed, 0 skipped** |
| ParkingService tests với PostgreSQL | **33 passed, 0 failed, 0 skipped** |

Các test DB tạo/xóa database tên ngẫu nhiên riêng, không áp schema vào database demo. Parking test tạo role tạm để kiểm tra password authentication. Fixture dùng provider giả cho một số dependency và HTTP TestServer: 116 test pass không thay thế rehearsal các service/email/browser thật.

Lần gọi UserService test có build bị MSB3021/MSB3027 do DLL Parking API bị tiến trình đang chạy giữ. Không dừng các service của người dùng. Chạy lại `--no-build --no-restore` bằng test binary đã build ở lượt trước thành công 83/83; không có source thay đổi mới trong lượt này. Parking suite build/test bình thường thành công 33/33. Logs được lưu cùng thư mục báo cáo. Đây là giới hạn build trên Windows khi service đang chạy, không phải một failed test assertion.

## Vẫn còn chặn chuỗi demo

1. **SiteForm che lỗi API:** mọi lỗi tạo site có thể biến thành site giả và thông báo thành công (`FE/src/roles/owner/parking-lots/SiteForm.tsx:207`). Database healthy không sửa logic này.
2. **Owner onboarding/structure dùng local:** mounted OwnerDashboard/ParkingStructure không nối chuỗi units/slots backend. Local lot và real site còn có thể khác ID. F5 vẫn thấy dữ liệu local không chứng minh PostgreSQL đã lưu.
3. **Owner chưa có đường xem 3D:** viewer hiện được gọi từ Driver/Operator; model picker của Owner không phải viewer của saved structure.
4. **Operator check-in/out dùng local:** component thực sự đang được Vite phục vụ gọi `store.saveBooking`/`store.saveLot`, không tạo session/entry/departure backend. Backend cũng chưa có các workflow này. Chưa có accepted occupancy write/projection để các tài khoản nhìn cùng slot.
5. **Payment chưa có implementation:** container Payment DB healthy chỉ chứng minh storage sẵn sàng, không có Payment service/order/provider/callback để thanh toán.
6. **Security vẫn cần sửa:** Operator Active trước xác minh; permission tenant A có thể mở scope B; access token 24 giờ và thiếu refresh inactivity; Admin user management thiếu ACCOUNT_ADMIN; phone normalization và logout/refresh race còn trong source. Test xanh không chứng minh các expectation này đúng baseline.

Chi tiết và file/dòng trong [identity-review.md](identity-review.md), [operations-review.md](operations-review.md), [frontend-review.md](frontend-review.md). Thứ tự sửa và checklist trong [README.md](README.md).

## Email và phần chưa kiểm chứng

Root `.env` có SMTP host/port nhưng username/password/from để trống. Development user-secrets có các trường credential này, nên không kết luận SMTP chưa cấu hình. Chưa gửi mail hoặc kiểm tra inbox thực tế. `Onboarding:PublicOrigin` chưa thấy trong `.env`, process hiện tại, Development user-secrets hay tracked appsettings đã kiểm tra; tiến trình API có thể có cấu hình riêng chưa quan sát. Adapter yêu cầu HTTPS origin để tạo email link Operator, cần kiểm chứng trước demo.

Query chỉ đọc delivery cho kết quả PENDING=0, FAILED=0 tại thời điểm kiểm tra; điều này không chứng minh email đã được gửi/nhận.

Chưa đăng ký account thật mới, chưa duyệt application thật hay tạo facility/operator trong database demo. Các bước này có thể gửi mail và tạo dữ liệu; kết quả integration fixture không được mô tả là đã hoàn tất các bước đó trong hệ thống đang chạy. Admin smoke check chỉ tạo rồi thu hồi session của chính lần kiểm tra, không in token/password.

## Đánh giá cho demo

Môi trường database và basic Admin authentication hiện hoạt động. Có thể tiếp tục rehearsal Driver/Owner registration và approval với email nhận được, nhưng chưa có bằng chứng acceptance toàn chuỗi. Để demo structure → 3D → Operator → check-in/out → slot đồng bộ bằng dữ liệu thật vẫn phải bổ sung/nối các phần nêu trên. Payment nên để sau luồng chính. Checkout chỉ giải phóng occupancy khi xác nhận actual departure theo contract; payment/gate-open không thay thế xe đã rời bãi.
