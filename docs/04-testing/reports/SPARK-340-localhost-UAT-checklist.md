# SPARK-340 — Checklist test FE + BE trên localhost

Mở http://localhost:5173. Dùng tài khoản seed Admin/Owner của môi trường local và mật khẩu trong `.env`; không chụp hoặc xuất mật khẩu/token vào evidence. Admin Dashboard → **System Config** là màn hình kiểm thử chính, không có URL riêng cho từng tab.

Ghi PASS / FAIL / NOT RUN, thời điểm, dữ liệu fixture và ảnh màn hình đã che thông tin nhạy cảm cho từng ca. Browser DevTools → Network có thể kiểm tra status/code và response; che Authorization khi lưu bằng chứng.

| Ca | Thao tác | Kết quả cần quan sát |
| --- | --- | --- |
| U01 Admin | Đăng nhập Admin, mở System Config → Operational Policies | Catalogue tải từ OperationService :5055, có 14 key, giá trị/version hiện hành và bounds thực tế; không 403 |
| U02 Draft | Create draft, thay một giá trị rồi lưu; đóng và mở lại draft | Các giá trị/bounds khác được giữ; chưa activate thì effective hiện tại không đổi |
| U03 Validation | Nhập minimum ≤0, minimum lớn hơn maximum, arrival tolerance ngoài 15–30, bound min > max | Có lỗi field hoặc lỗi có mã từ BE, không tạo revision hoạt động |
| U04 Conflict | Mở cùng draft ở hai cửa sổ; cửa sổ A lưu rồi B lưu với token cũ | B nhận 409 và hướng dẫn reload, không ghi đè A |
| U05 Immediate | Activate một draft hợp lệ ngay | Lịch sử và catalogue tải lại đúng version; revision trước còn đọc được |
| U06 Scheduled | Schedule một draft vài phút trong tương lai; đọc Effective Policy trước/sau mốc đó | Trước mốc dùng version cũ, tại/sau mốc dùng version mới; lịch sử SCHEDULED/CURRENT nhất quán |
| U07 Overlap | Hai draft cùng mốc kích hoạt | Draft thứ hai bị từ chối 409; không có hai revision hiệu lực chồng lấn |
| U08 Effective | Effective Policy → PLATFORM, rồi SITE với tenant/site đúng | PLATFORM_DEFAULT và SITE_OVERRIDE đúng dữ liệu; 0 hiển thị là giá trị thật; bật/tắt arrival tolerance độc lập giá trị phút |
| U09 Owner | Owner Dashboard → Policies, chọn bãi thuộc Owner; thử giá trị trong/ngoài Admin bounds | Giá trị hợp lệ publish được; ngoài bounds bị chặn; khi không cấp override thì không tự có khoảng 0..5 cho driver reallocation |
| U10 Legacy | Với bãi đã có DRIVER_REALLOCATION_REQUEST_LIMIT=2, republish giữ nguyên rồi thử đổi sang 5 khi chưa có bounds | Giữ nguyên dữ liệu cũ được phép; đổi mới không được tự cấp quyền |
| U11 Cross-policy | Site có override maximum=60 phút; Admin thử tăng minimum kế thừa thành90 phút | Admin activation bị từ chối vì effective minimum > maximum; cấu hình cũ còn hoạt động |
| U12 Payment | Integrations → PayOS; GET và lưu FIXTURE | Response chỉ có masked client id/configured flags; không có raw API/checksum key; FIXTURE không gọi provider |
| U13 Payment validation | Chọn URL không được phép hoặc gửi credential rỗng qua DevTools/API client | 400 có code cụ thể; lỗi hiện đúng trên form; không lưu cấu hình sai |
| U14 Mode isolation | Với credential SANDBOX/LIVE giả lập chỉ dùng fixture HTTP trong test tự động, đổi mode nhiều lần | Credential của mode này không sao chép sang mode khác; input chưa lưu được xóa khi đổi mode. Không gửi credential giả tới provider thật |
| U15 Restart | Ghi lại mode/status/masked id, restart rồi force-recreate Payment theo lệnh dưới | Cấu hình vẫn giải mã/khôi phục được; không tự mất credential; không cần nhập lại khóa |
| U16 Storage failure | Agent kiểm thử mô phỏng DB unavailable; user thử mở/lưu Integrations | 503 PAYMENT_INTEGRATION_PERSISTENCE_FAILED, không success giả; sau phục hồi giữ nguyên cấu hình đã commit |
| U17 Notification | Integrations → Notifications | EMAIL/SMS/PUSH/IN_APP hiển thị fixture provider tương ứng và trạng thái do BE cung cấp |
| U18 RBAC | Owner/Driver đăng nhập rồi mở màn Admin hoặc gọi các API Admin | UI chặn quyền; API trả 403. Anonymous API 401; Admin 200 |
| U19 Confirmed booking | Driver dùng vehicle/slot thuộc fixture hợp lệ, review terms rồi xác nhận booking; lưu receipt/booking id; Admin đổi policy | Booking cũ giữ giá trị/điều khoản đã chấp nhận; booking mới theo policy mới. Nếu thiếu fixture đánh NOT RUN, không đánh PASS bằng revision id đơn thuần |
| U20 Responsive | Kiểm tra các tab ở desktop, tablet và mobile; dùng bàn phím cho tab/modal | Bảng/card đọc được, nút chính còn truy cập được, focus và thông báo lỗi hợp lý |

## Restart test

Chỉ chạy khi không có người đang lưu cấu hình. Không xóa volume.

```powershell
docker compose -f compose.app.yml restart payment-service
docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service
```

Payment dùng named volume `payment-data-protection-keys` tại `/app/.keys/payment`. Lần chuyển môi trường hiện tại đã giữ bộ khóa cũ trước khi tạo lại container. Khi triển khai sang máy khác cần chuyển/backup key store an toàn cùng dữ liệu đã mã hóa.

## Các ca cần test tự động có kiểm soát

UI không chứng minh được hai giao dịch chạy đúng interleaving hoặc webhook HMAC. Agent độc lập phải chạy PostgreSQL concurrency; Save A thất bại trong khi Save B chờ; hydration đọc lỗi rồi Save; callback từ checkout đã bind sau mode switch/rotation/restart; binding thiếu hoặc thay thế bị từ chối; provider phản chiếu secret trên initiate/cancel được redact.

Checkout PayOS thật được tạo trước khi cơ chế binding bền vững tồn tại không có đủ dữ liệu để suy ra khóa phát hành. Cần đối soát và khôi phục bằng thông tin đáng tin cậy trước khi xử lý callback; không tạo binding bằng cách đoán timestamp. Ghi riêng trường hợp legacy này khi rollout có giao dịch thật đang chờ.
