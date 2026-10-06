# SPARK-173 — Review Login/Logout

Ngày review: 06/10/2026. Phạm vi: luồng email/password Login, khôi phục phiên qua `/me`, Logout và cơ chế phiên/refresh hiện có. Đã review lại thay đổi sau sửa; kết quả nằm ở workspace, chưa commit/push hoặc cập nhật trạng thái Jira.

## Nguồn và contract

- SRS v0.9 §3.1.4, FR-AUTH-02/06 và UC-AUTH-01 yêu cầu logout kết thúc phiên/khả năng gia hạn, xóa trạng thái client và kiểm tra trạng thái/quyền hiện tại.
- `API Design.md` được tham chiếu trong chat trước có bảng endpoint `/api/auth/login`, `/api/auth/logout` và request email/password. File chỉ có 40 dòng, chưa có response/error/logout body đầy đủ. Không coi contract triển khai là design đã được duyệt đầy đủ.
- [Auth design alignment](../02-architecture/api/auth-design-alignment-2026-10-06.md) ghi các quyết định triển khai trước và nguồn báo cáo so sánh. Các kết quả cũ chỉ là lịch sử; lần review này chạy lại kiểm chứng.

## Phát hiện và sửa

| Mức | Phát hiện trước sửa | Sửa và kiểm chứng |
| --- | --- | --- |
| P2 | `AppProvider` xóa token khi `/me` lỗi mạng/server lúc F5; phiên backend vẫn còn nhưng client mất khả năng thử lại. | Bỏ xóa token trong catch khôi phục. `authApi` vẫn xóa token khi `/me` trả 401. Test lỗi tạm thời giữ token; 401 vẫn làm hết phiên. |
| P2 | `authApi.logout()` đã bảo vệ token của phiên mới, nhưng `AppProvider.signOut()` vẫn xóa user/view khi phản hồi logout của phiên cũ tới muộn. | Dùng revision của trạng thái auth để bỏ qua kết quả/lỗi logout cũ. Test success/failure đến sau login mới đều giữ user/view/currentUserId mới. |
| P2 | Catch logout có thể ghi đè thông báo hết phiên sau sự kiện `sp-auth-expired`; callback khôi phục cũ chưa kiểm tra thay đổi auth ở provider. | Tăng revision khi đổi user/hết phiên; kiểm tra revision trong callback restore/logout. Test giữ màn đăng nhập và thông báo hết phiên, chặn restore cũ. |

Các ca hồi quy tương ứng đã fail trên code cũ và pass sau sửa. Chỉ sửa `FE/src/context/AppContext.tsx`; không cần đổi backend hay contract endpoint trong lần review này.

## Review lại code

- Login xác minh bcrypt, chỉ cấp phiên cho user/account còn hoạt động và có role, bỏ user soft delete. Counter/khóa được lưu trong transaction và serialize bằng PostgreSQL row lock.
- JWT RS256 kiểm tra issuer/audience/lifetime, phiên tồn tại/chưa revoke và trạng thái/role hiện tại trong middleware Bearer. `/me` trả danh tính từ backend.
- Refresh lưu hash, có hạn tuyệt đối 7 ngày và rotation bằng conditional update. Hai request dùng cùng snapshot chỉ một request thắng; cặp token cũ không được sử dụng lại.
- Logout bắt buộc đúng cặp bearer/refresh của cùng user/phiên, revoke đúng phiên, giữ phiên thiết bị khác. Sai cặp token hoặc lỗi persistence không được báo là logout thành công.
- Frontend giữ token khi logout lỗi hoặc probe bearer lỗi mạng/server; xóa khi logout thành công hoặc bearer thực sự không hợp lệ. Callback cũ không được thay user/view mới. Logout trùng được chặn và có thể thử lại sau lỗi.
- Đã đọc lại toàn bộ diff và test mới; không phát hiện thêm lỗi cần sửa trong các luồng vừa kiểm chứng. Test provider dùng hooks giả lập để kiểm tra chuyển trạng thái bất đồng bộ, không thay thế kiểm thử DOM/React lifecycle trên trình duyệt.

## Kết quả kiểm chứng mới

| Kiểm chứng | Kết quả |
| --- | --- |
| `dotnet test SmartParking.slnx -c Release --no-restore` với `SMARTPARK_AUTH_TEST_CONNECTION` | 54/54 pass, 0 fail, 0 skip; gồm 2 test PostgreSQL thật và 1 scaffold có sẵn. |
| `node --test tests/frontend/auth-api.test.cjs tests/frontend/auth-context.test.cjs` | 28/28 pass: 19 test API frontend và 9 test trạng thái provider. |
| `npm.cmd run build` trong FE | Pass; còn cảnh báo bundle lớn có sẵn. |
| `git diff --check` | Pass. |

Lần đầu chạy backend không đặt connection string: 52 pass, 2 skip. Sau đó chạy lại với PostgreSQL test đã có ở `127.0.0.1:65433`: đủ 54 pass. Hai test tự tạo/xóa database tên ngẫu nhiên `smartpark_auth_test_<guid>`, không sử dụng dữ liệu user của database dự án ở cổng 5433. Không tạo hoặc dừng container có sẵn.

## Giới hạn và việc tiếp theo

- Chưa chạy lại thao tác UI trên trình duyệt hoặc smoke test API bằng tiến trình HTTP thật trong lần review này. HTTP tests dùng ASP.NET TestHost; test PostgreSQL kiểm chứng persistence/concurrency thật.
- SRS yêu cầu OTP authentication, MFA tùy chọn và session 24 giờ; triển khai hiện tại dùng email/password và access token 1 giờ theo quyết định alignment trước. Đây là khác biệt còn tồn tại, không được coi là hoàn thành toàn bộ FR-AUTH-02. Chính sách khóa password 3 lần/15 phút là quyết định triển khai trước, không phải chức năng OTP đã hoàn thành.
- FE chưa tự động refresh; khi access hết hạn phải đăng nhập lại. Registration/OTP, Owner approval, Operator provisioning và phân quyền tài nguyên nghiệp vụ nằm ngoài SPARK-173 này.
- RSA key tạm trong Development không giữ hiệu lực JWT qua restart. Muốn kiểm chứng triển khai nhiều instance/restart cần key cấu hình ổn định ngoài source.

Kết luận: hoàn thành review và sửa lỗi phát hiện cho luồng Login/Logout hiện có; còn các khoảng cách với SRS nêu trên cần được thống nhất/triển khai trong task tương ứng trước khi tuyên bố toàn bộ authentication đạt SRS.
