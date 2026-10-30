# Báo cáo đánh giá mức độ sẵn sàng demo và chất lượng source SmartParking

**Ngày kiểm tra:** 15/10/2026  
**Ngày dự kiến demo:** 16/10/2026  
**Nhánh:** `Fix2FA`  
**Commit nền:** `6165024c634316d15525fe48bdc5a3ddd8193c1a`, có xét các thay đổi local chưa commit, bao gồm phần MFA.  
**Phạm vi:** Kiểm tra môi trường, chạy kiểm thử và review source. Không sửa source ứng dụng.  
**Cách đọc:** Báo cáo này trình bày độc lập, bao gồm kết luận, bằng chứng, ảnh hưởng và tiêu chí sửa. Các đường dẫn source là đường dẫn tương đối từ root repository; số dòng ứng với checkout tại thời điểm kiểm tra.

## 1. Kết luận

**Môi trường Docker và database đã chạy được. Tuy nhiên, toàn bộ luồng demo chưa kết nối hoàn chỉnh với backend.**

Luồng mục tiêu là: **đăng ký Driver/Owner → xác minh → Admin duyệt Owner → Owner tạo structure và xem 3D → Owner tạo Operator → Operator check-in/check-out → slot cập nhật**, kèm login/logout và payment nếu đủ điều kiện.

Kết quả hiện tại chia thành ba nhóm:

1. **Đã kiểm chứng trên hệ thống đang chạy:** Docker, các bảng migration được kiểm tra và login/logout Admin.
2. **Đã có implementation nhưng chưa kiểm chứng đủ luồng thực tế:** đăng ký Driver/Owner, xác minh email, duyệt Owner và tạo Operator. Các bước này còn phụ thuộc delivery, cấu hình và phân quyền.
3. **Chưa đủ implementation để nghiệm thu luồng yêu cầu:** Owner structure dùng backend xuyên suốt, Owner 3D, Operator check-in/check-out có session thật, slot đồng bộ và payment thật.

**Chưa nên xác nhận toàn bộ kịch bản demo đã hoàn thành.** Việc cần ưu tiên là nối dữ liệu frontend–backend, bổ sung workflow còn thiếu và sửa phân quyền. Không có cơ sở coi những phần này chỉ còn lỗi setup Docker.

> Database chạy thành công và test đạt không đồng nghĩa tất cả màn hình đã sử dụng đúng dữ liệu backend.

## 2. Tình trạng từng bước demo

| Bước demo | Tình trạng | Điều cần biết |
| --- | --- | --- |
| Login / logout | **Đã kiểm tra với Admin** | Đăng nhập, đọc thông tin tài khoản, đăng xuất thành công. Token cũ bị từ chối sau logout. Các role khác chưa rehearsal đầy đủ. |
| Driver đăng ký | **Có API, cần chạy thử thực tế** | Có đăng ký và xác minh OTP. Cần xác nhận nhận được email và kích hoạt tài khoản. |
| Owner đăng ký | **Có API, cần chạy thử thực tế** | Có đăng ký, xác minh email và trạng thái chờ duyệt. |
| Admin duyệt Owner | **Có API, chưa duyệt application thật trong lần kiểm tra này** | Đã đọc được danh sách application bằng Admin. Khi duyệt cần FacilityService để tạo tenant. |
| Owner tạo parking structure | **Chưa hoàn chỉnh** | Backend có CRUD site/unit/slot, nhưng màn hình structure đang dùng dữ liệu local. |
| Owner xem model 3D | **Chưa có trong luồng Owner** | Viewer đang được dùng ở Driver/Operator; Owner chưa có đường mở viewer của structure đã lưu. |
| Owner tạo Operator | **Có API, còn lỗi nghiệp vụ** | Account được kích hoạt trước xác minh contact. Email onboarding còn cần cấu hình HTTPS origin hợp lệ. |
| Operator check-in/check-out | **Đang mô phỏng ở frontend** | Chỉ cập nhật booking và slot trong localStorage; chưa có backend session/entry/departure. |
| Slot đổi trạng thái ngay | **Chỉ có thay đổi cục bộ** | Chưa chứng minh Owner và Operator ở hai trình duyệt thấy cùng trạng thái đã lưu backend. |
| Payment | **Chưa triển khai luồng thật** | Có database và giao diện, chưa có Payment service/gateway/callback xử lý giao dịch. |

## 3. Những gì đã kiểm tra thành công

### Môi trường

- Cả **6 database Docker** đều `healthy`: User, Parking, Reservation, Payment, Notification và IoT.
- User API cổng `5035`, Parking API `5045`, Reservation API `5055` đều đang trả lời HTTP.
- Frontend tại `http://localhost:5173` trả HTTP `200` và phục vụ đúng source đang review.
- Các bảng migration quan trọng đã tồn tại: `auth_challenges`, `workflow_deliveries`, `mfa_recovery_codes`, `structure_synchronizations`, `structure_release_receipts`.

### Login / logout thực tế

1. Đăng nhập Admin thành công.
2. Gọi `/api/auth/me` thành công.
3. Đọc danh sách đơn đăng ký Owner thành công.
4. Logout thành công.
5. Dùng token cũ gọi lại `/me` nhận `401`.

### Kiểm thử

| Bộ kiểm tra | Kết quả | Giới hạn |
| --- | --- | --- |
| UserService với PostgreSQL thật | **83 đạt, 0 lỗi, 0 bỏ qua** | Dùng database test riêng; không thay thế rehearsal email/browser thật. |
| FacilityService với PostgreSQL thật | **33 đạt, 0 lỗi, 0 bỏ qua** | Kiểm tra các chức năng đã có; không chứng minh các API check-in/out chưa được triển khai. |
| Frontend build ở lượt review trước | **Thành công** | Build không chứng minh luồng nghiệp vụ hoạt động. Không chạy lại vì source không thay đổi trong lượt kiểm tra sau. |
| Frontend tests ở lượt review trước | **66 đạt, 22 lỗi** | 22 lỗi xảy ra do test harness thiếu mock import, chưa chạy đến assertions; không đồng nghĩa có 22 lỗi giao diện thực tế. |

**Lưu ý khi chạy test trên Windows:** Lần build UserService test bị khóa DLL vì FacilityService đang chạy. Chạy lại bằng test binary đã build với `--no-build --no-restore` đạt 83/83. Không dừng service đang dùng để demo.

## 4. Các vấn đề cần ưu tiên sửa

**Cách phân loại:** “Chặn demo” là bước không thể chứng minh hoạt động end-to-end theo yêu cầu. “Lỗi bảo mật/nghiệp vụ” có thể không xuất hiện trong demo thuận lợi nhưng vẫn cần sửa trước khi nghiệm thu. “Cần kiểm chứng” là phần chưa có đủ bằng chứng thực tế; không tự coi là lỗi đã tái hiện.

### 4.1. Form tạo site có thể báo thành công giả

**Hiện tại:** Khi API tạo site lỗi, frontend bắt lỗi rồi tự tạo một site local và báo đã lưu.

**Ảnh hưởng:** Người demo nhìn thấy thông báo thành công, nhưng database không có site đó. Các bước tạo Operator hoặc tải lại danh sách có thể không sử dụng được site.

**Cần sửa:** Hiển thị lỗi API; chỉ báo thành công sau khi backend xác nhận. Dùng cùng ID do backend trả về.

**Source:** `FE/src/roles/owner/parking-lots/SiteForm.tsx`, khoảng dòng 207–262.

### 4.2. Structure của Owner và dữ liệu Operator chưa cùng nguồn

**Hiện tại:** Owner structure lưu trong localStorage; Operator đọc lot/booking từ local store khác. Local lot còn có thể có ID khác site backend.

**Ảnh hưởng:** Owner tạo slot nhưng Operator không có cùng slot thực tế. Một trình duyệt đổi trạng thái không chứng minh tài khoản khác nhìn thấy thay đổi.

**Cần sửa:** Nối màn hình đang được sử dụng vào API structure; thống nhất `siteId` và `slotId`; tải lại dữ liệu từ backend.

**Bằng chứng cụ thể:** `ParkingStructure.tsx` import `structureStore` và gọi `getStructure`/`saveStructure`; đây là lưu trữ trình duyệt. Backend có route tạo unit/slot, và `OwnerWorkspace.tsx` có phần gọi API, nhưng component này chưa được mount trong luồng Owner hiện tại. Vì vậy có code API ở nơi khác không chứng minh tab Structure đang sử dụng API đó.

**Source chính:**

- `FE/src/roles/owner/dashboard/OwnerDashboard.tsx`
- `FE/src/roles/owner/structure/ParkingStructure.tsx`
- `FE/src/roles/operator/dashboard/OperatorDashboard.tsx`

> LocalStorage vẫn tồn tại sau F5. Vì vậy, F5 còn dữ liệu chưa đủ để kết luận PostgreSQL đã lưu.

### 4.3. Chưa có nghiệp vụ backend cho check-in/check-out

**Hiện tại:** Frontend thay đổi booking và slot sau một timer; backend chưa có API admission/session/checkout/departure tương ứng.

**Ảnh hưởng:** Chưa có kiểm soát authoritative về quyền Operator, slot còn trống, hai xe tranh cùng slot hoặc thao tác gửi lặp.

**Cần sửa:** Triển khai Operations entry/session và actual departure; lưu trạng thái trong transaction, kiểm tra scope và chống xử lý lặp; sau đó nối UI.

**Source frontend:** `FE/src/roles/operator/check-in-out/CheckInOut.tsx`, khoảng dòng 106–181.

**Bằng chứng backend:** `src/Services/OperationService/SmartParking.OperationService.API/Program.cs`, dòng 37–51, chỉ đăng ký ba route internal cho prepare/impact/release khi chỉnh structure. `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs`, dòng 92–121, có route structure và backup; chưa có API admission/session/checkout/departure.

**Bằng chứng trạng thái slot:** `src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresParkingStructureRepository.cs`, khoảng dòng 205, đọc occupancy từ `physical_state='OCCUPIED'`. Chưa có entry/departure workflow ghi accepted occupancy tương ứng. Không tìm thấy SignalR hub/subscription trong source backend đã kiểm tra. Chỉ thêm polling hoặc đổi màu UI sẽ không giải quyết nguồn dữ liệu chưa được cập nhật.

### 4.4. Owner chưa có đường xem 3D

**Hiện tại:** Viewer có ở Driver/Operator, nhưng chưa được mở từ luồng Owner.

**Cần sửa:** Cho Owner mở viewer của site đã chọn, dùng layout và trạng thái slot đã lưu. Màu slot phải tương ứng đúng ID backend.

**Bằng chứng:** `FE/src/roles/owner/dashboard/OwnerDashboard.tsx`, khoảng dòng 48–80, không mount `Parking3DViewer`. Caller của viewer được tìm thấy ở Driver BookingFlow/BookingCard và Operator LotStatus. Kết luận ở đây là thiếu đường xem 3D trong luồng Owner, không phải toàn dự án chưa có component 3D.

### 4.5. Còn lỗi phân quyền và kích hoạt tài khoản

| Vấn đề | Ảnh hưởng | Hướng sửa |
| --- | --- | --- |
| Quyền ở tenant A có thể mở scope tenant B | Có thể thao tác ngoài membership được cấp quyền | Kiểm tra action và tenant trên cùng membership hiện tại. |
| Operator Active trước xác minh contact | Có thể đăng nhập trước khi recipient xác minh | Giữ pending và chỉ kích hoạt sau proof hợp lệ. |
| Access token 24 giờ; thiếu refresh inactivity | Chưa đúng baseline 15 phút access, 24 giờ refresh inactivity | Sửa lifetime và test các mốc hết hạn. |
| Admin user management bỏ qua `ACCOUNT_ADMIN` | Admin có permission rỗng vẫn có thể quản lý user | Dùng kiểm tra quyền nhất quán. |

**Bằng chứng và điều kiện của từng lỗi:**

- **Ghép sai quyền với tenant:** `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs`, dòng 35–46, chỉ xác nhận có ít nhất một membership có quyền. `src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs`, dòng 19–23, sau đó trả tất cả tenant Owner đang active. `OperatorProvisioningService.cs`, dòng 59–72 trong cùng thư mục Repositories, dùng cách ghép tương tự. Trường hợp cụ thể: user có quyền tại A, permission rỗng tại B; A làm kiểm tra ban đầu thành công nhưng B vẫn lọt vào scope. Đây là kết luận từ source với điều kiện nhiều membership, chưa trình bày là exploit đã tái hiện trên hệ thống đang chạy.
- **Operator kích hoạt sớm:** `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs`, dòng 76–85, tạo User và Account ở trạng thái Active trước xác minh contact. Cần test không cấp normal session trước proof hợp lệ.
- **Lifetime không đúng baseline:** `src/Services/UserService/SmartParking.UserService.Application/Services/AccessTokenService.cs`, dòng 13, giữ access lifetime 86.400 giây. `AuthSessionService.cs` trong cùng thư mục chưa kiểm tra refresh inactivity 24 giờ. Một số test đang assert lifetime cũ nên test pass chưa chứng minh đáp ứng yêu cầu. Cần giữ giới hạn tuyệt đối 7 ngày của refresh khi bổ sung idle expiry.
- **Thiếu permission Admin:** `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserManagementService.cs`, dòng 31–41 và 66–82, kiểm tra role/status nhưng thiếu `ACCOUNT_ADMIN`. Lỗi này nằm ở quản lý user; không được suy rộng rằng route duyệt Owner cũng thiếu cùng kiểm tra, vì route duyệt có kiểm tra permission.
- **Login bằng phone không chuẩn hóa nhất quán:** `LoginCommandHandler.cs`, dòng 18, trong thư mục `Application/Usecase/Login`, chỉ trim contact; `UserRepository.cs`, dòng 26–32, so phone trực tiếp. Registration chuẩn hóa `+84` về `0`, nên cùng số điện thoại có thể bị từ chối ở password login. Cần dùng chung quy tắc chuẩn hóa.
- **Race logout/refresh:** `PostgresAuthSessionStore.cs`, dòng 31–44, trong thư mục Persistence/Repositories, logout khớp access ID và refresh hash hiện tại. Nếu refresh thay cặp token trước khi logout thu hồi, thao tác logout bằng cặp cũ có thể không thu hồi session mới. Cần test concurrency có phối hợp thời điểm; login/logout tuần tự đã pass không loại trừ race này.

### 4.6. Payment chưa có luồng thực thi

**Hiện tại:** Payment database đã chạy, nhưng chưa có service tạo order, xác minh callback và ghi nhận settlement.

**Cần sửa:** Triển khai qua Payment owner và provider contract; kiểm tra amount/order, callback lặp và kết quả chưa xác định. Nên làm sau luồng chính nếu thời gian demo hạn chế.

**Không trình bày UI payment hoặc database Payment healthy như bằng chứng giao dịch thật.**

**Bằng chứng:** Source `src/Services` hiện có User, Parking và Reservation; chưa tìm thấy Payment service thực thi/provider adapter. `FE/src/roles/driver/booking/BookingFlow.tsx`, khoảng dòng 365–376 và 1141–1143, thông báo gateway chưa được cấu hình. Database schema chỉ cung cấp cấu trúc lưu trữ.

### 4.7. Chỉnh profile site có thể làm mất trạng thái slot local

**Hiện tại:** `FE/src/roles/owner/parking-lots/SiteForm.tsx`, dòng 166–176, tạo lại danh sách slot bằng `generateSlots` khi sửa profile.

**Ảnh hưởng:** ID và trạng thái occupied/reserved trong dữ liệu local có thể bị thay thế. UI còn báo các giá trị capacity/active đã lưu trong khi request profile chỉ gửi một số trường thông tin site.

**Cần sửa:** Giữ nguyên ID và trạng thái slot khi chỉnh profile; thay đổi capacity/active qua đúng API nghiệp vụ. Chỉ hiển thị giá trị đã được backend xác nhận.

### 4.8. Onboarding Owner và lựa chọn site của Operator phụ thuộc dữ liệu local

**Owner:** `OwnerDashboard.tsx`, dòng 44–46, chặn workspace theo `onboardingComplete`; cờ này đến từ cache local trong `authApi.ts`. Cùng Owner có thể bị yêu cầu onboarding lại trên browser sạch dù đã có site backend. Cần xác định trạng thái onboarding từ dữ liệu server.

**Operator:** `OperatorDashboard.tsx`, khoảng dòng 44–100, lấy lot local và có fallback trả toàn bộ lot nếu filter không có kết quả. Màn hình assignments có gọi API thật nhưng chưa điều khiển selector/action ở dashboard. Đây là sai lệch UI/scope; chưa phải bằng chứng Operator đã đọc được dữ liệu tenant khác từ backend. Cần lấy danh sách site được cấp quyền từ server và backend kiểm tra lại mỗi command.

## 5. Email: đã cấu hình một phần, chưa xác nhận giao nhận

- Root `.env` để trống SMTP username/password/from, nhưng Development user-secrets có các trường này. Vì vậy không kết luận SMTP chưa cấu hình.
- Chưa gửi OTP/email trong lần kiểm tra này; cần thử inbox thật trước demo.
- Chưa thấy `Onboarding:PublicOrigin` trong các nguồn cấu hình đã kiểm tra. Adapter email Operator yêu cầu origin HTTPS; cần kiểm tra cấu hình hiệu lực của API và thử link onboarding.
- Không có delivery PENDING/FAILED tại thời điểm query không có nghĩa email đã được nhận.

## 6. Đánh giá theo hướng dẫn kiến trúc và coding

Việc review đối chiếu hướng dẫn `AGENTS.md` được cung cấp và các requirement/contract liên quan trong repository. Các điểm dưới giải thích cụ thể vì sao chức năng có thể chạy nhưng source chưa đạt hướng kiến trúc mong muốn.

| Hướng dẫn cần đáp ứng | Hiện trạng quan sát | Hướng điều chỉnh |
| --- | --- | --- |
| Controller mỏng, use case qua CQRS/MediatR | Một số action Auth/OwnerApplications/Operators gọi workflow service trực tiếp; Parking route gọi service trực tiếp | Tách command/query và handler cho từng workflow được sửa; controller chỉ xử lý transport. |
| Application điều phối; Persistence tập trung truy cập dữ liệu | Nhiều quyết định nghiệp vụ, validation và transaction nằm trong các workflow service thuộc Persistence | Đưa orchestration vào Application qua repository/UOW port; giữ SQL và lock trong Persistence. |
| Controller không truy cập DbContext trực tiếp | `IdentityScopeController` dùng `AppDbContext` để truy vấn membership | Tách query có kiểm tra permission và tenant nhất quán. |
| Domain không phụ thuộc serialization | `src/Services/FacilityService/SmartParking.FacilityService.Domain/ParkingStructure.cs`, dòng 181–186, parse JSON bằng `JsonDocument` | Parse/map JSON ở boundary ngoài Domain; Domain nhận dữ liệu có kiểu. |
| Transaction local không bảo đảm atomic cho HTTP service khác | `OwnerRegistrationService.cs`, dòng 75–106 trong UserService Persistence/Repositories, gọi Parking khi transaction local còn mở | Dùng operation ID ổn định và workflow phục hồi; lưu durable intent/outcome để xử lý remote success nhưng local commit thất bại. |
| Phân quyền gắn với action và resource hiện tại | Quyền và tenant có thể lấy từ hai membership khác nhau | Kiểm tra cùng actor/action/membership/tenant; bổ sung test deny scope. |

Đây là các sai lệch cụ thể, không phải kết luận toàn bộ solution sai Clean Architecture. Cần giữ các cơ chế đang hữu ích: refresh rotation có CAS, lock/counter đăng nhập, kiểm tra scope ở nhiều điểm và durable synchronization cho chỉnh structure. Không di chuyển class hàng loạt hoặc thay framework trước demo để tạo cảm giác đã sửa kiến trúc.

## 7. Thứ tự sửa đề xuất trước demo

| Thứ tự | Việc cần làm | Tiêu chí hoàn thành |
| --- | --- | --- |
| 1 | Bỏ thành công giả khi tạo site; thống nhất ID | API lỗi hiện lỗi; site tạo thành công có thể GET lại bằng cùng ID. |
| 2 | Nối Owner structure vào backend | Tạo floor/zone/slot rồi browser khác tải được cùng layout. |
| 3 | Sửa scope và vòng đời Operator | Chưa xác minh không có session; chỉ được thao tác site đã cấp quyền. |
| 4 | Triển khai entry/session/departure và occupancy | Một entry chiếm một slot; lặp command không nhân đôi; tranh slot chỉ một thao tác thắng. |
| 5 | Nối Operator UI và đồng bộ trạng thái | Owner/Operator nhìn cùng slot sau khi backend xác nhận. |
| 6 | Mở 3D cho Owner | Model đúng site/layout/slot và trạng thái mới nhất. |
| 7 | Hoàn thiện security và regression liên quan | Test đúng baseline; không giữ expectation cũ chỉ để test xanh. |
| 8 | Payment tùy thời gian còn lại | Có order và provider verification thật, xử lý callback lặp an toàn. |

Không ưu tiên đổi tên toàn solution hoặc di chuyển folder hàng loạt trước demo. Các lệch CQRS/Clean Architecture cần sửa theo từng luồng có kiểm chứng.

## 8. Tiêu chí nghiệm thu kịch bản demo

Các ô dưới đây chưa được đánh dấu vì chưa chạy toàn bộ chuỗi thực tế.

- [ ] Driver đăng ký bằng contact mới → nhận OTP → xác minh → login → logout.
- [ ] Owner đăng ký → xác minh email → chờ duyệt; trước duyệt không thao tác Owner.
- [ ] Admin duyệt đúng đơn → Owner đăng nhập lại và có tenant hợp lệ.
- [ ] Owner tạo site/structure → GET lại và kiểm tra cùng ID, code, số slot.
- [ ] Owner mở 3D của chính site đó; model khớp dữ liệu backend.
- [ ] Owner tạo Operator → recipient xác minh → đăng nhập và chỉ thấy site được giao.
- [ ] Check-in tạo session thật → đúng slot occupied → Owner/Operator thấy cùng trạng thái.
- [ ] Gửi lại check-in không tạo thêm session; Operator sai site bị từ chối.
- [ ] Checkout xử lý đúng trạng thái tài chính; actual departure giải phóng đúng slot một lần.
- [ ] Nếu có payment: xác minh giao dịch thật; callback lặp không ghi nhận hai lần.
- [ ] Chạy lại toàn bộ bằng hai browser/profile riêng, không dựa vào dữ liệu local đã seed.

> **Quy tắc slot:** Chốt phí, thanh toán, mở cổng và xe thực sự rời bãi là các sự kiện khác nhau. Theo contract, actual departure mới giải phóng occupancy; không tự chuyển slot sang trống chỉ vì payment thành công.

## 9. Cách thực hiện và giới hạn của kết quả

Ba agent review các mảng identity, operations và frontend; phần điều phối kiểm tra Docker, schema, API và chạy test với PostgreSQL. Không cộng các lần chạy lại/scoped test thành số test độc lập. Kết quả cuối cùng của hai suite backend là **116 test đạt, không skip**.

Các lệnh kiểm tra chính đã chạy:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml ps

# Có cấu hình connection test riêng trong process; không ghi secret trong báo cáo.
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-build --no-restore --verbosity minimal
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-restore --verbosity minimal

# Frontend: đã chạy trong cùng đợt review, trước lần kiểm tra Docker sau cùng.
# npm run build thực hiện tại thư mục FE.
npm run build
# Lệnh dưới thực hiện tại root repository.
node --test tests/frontend/*.test.cjs
```

Chỉ copy lệnh `dotnet test` mà không cấu hình `SMARTPARK_AUTH_TEST_CONNECTION` / `SMARTPARK_STRUCTURE_TEST_CONNECTION` sẽ làm test PostgreSQL bị skip. Các connection trong lần chạy này trỏ local server, fixture tự tạo database tên ngẫu nhiên, áp schema rồi xóa đúng database test. Không dùng database demo làm nơi áp schema test. Không ghi token, password hoặc connection string thật vào báo cáo.

**Giới hạn cần giữ khi đánh giá:**

- HTTP 200 frontend hoặc API trả 401/404 chỉ chứng minh tiến trình đang trả lời; không tự chứng minh nghiệp vụ thành công. Login/logout Admin có kiểm tra riêng như mục 3.
- Chưa đăng ký account thật mới, duyệt application thật, tạo facility/Operator hay gửi email trong database demo trong lượt kiểm tra này. Các ca test dùng dữ liệu riêng và có dependency giả ở một số boundary.
- Chưa thực hiện browser acceptance toàn chuỗi hoặc giao dịch provider thật. Chưa xác nhận hardware integration.
- Các lỗi source có điều kiện concurrency/nhiều membership được ghi rõ; không trình bày như exploit đã tái hiện nếu chưa chạy ca tương ứng.
- Build frontend thành công không thay thế TypeScript typecheck hay end-to-end test. Các lỗi VM harness cần sửa trước khi dùng bộ test đó làm cổng nghiệm thu.
- Tài liệu review lịch sử được đối chiếu lại với source hiện tại. Working tree đã bổ sung password → MFA continuation và các phần enrollment/recovery; không kết luận các phần này hoàn toàn chưa có. Chưa vì vậy mà xác nhận toàn bộ MFA/Admin recovery đạt contract.

**Phạm vi bàn giao:** Báo cáo và đề xuất sửa. Không thay source ứng dụng, không chạy migration sửa dữ liệu demo, không commit/push. Mọi mục trong kế hoạch sửa là công việc còn phải thực hiện và nghiệm thu, không phải thay đổi đã hoàn thành.

## 10. Kết quả smoke test trực tiếp trên hệ thống đang chạy

**Ngày chạy:** 15/10/2026. **Kết quả cuối: 26/26 kiểm tra HTTP đạt, 0 lỗi, 0 bị chặn.** Ngoài 26 kiểm tra HTTP, đã kiểm tra lại 6 container healthy và sự tồn tại của 5 bảng migration quan trọng. Không cộng những kiểm tra này vào số 116 test backend ở mục 3.

Smoke test kiểm tra nhanh các đường vào hệ thống. Đợt này không thực hiện browser automation hoặc toàn bộ quy trình demo.

| STT | Kiểm tra | HTTP mong đợi | HTTP thực tế | Kết quả |
| --- | --- | --- | --- | --- |
| 1 | Frontend index | 200 | 200 | Đạt |
| 2 | User OpenAPI | 200 | 200 | Đạt |
| 3 | `/me` không đăng nhập | 401 | 401 | Đạt |
| 4 | Danh sách đơn Owner không đăng nhập | 401 | 401 | Đạt |
| 5 | Danh sách user không đăng nhập | 401 | 401 | Đạt |
| 6 | Danh sách Operator không đăng nhập | 401 | 401 | Đạt |
| 7 | Operator assignments không đăng nhập | 401 | 401 | Đạt |
| 8 | Parking list không đăng nhập | 401 | 401 | Đạt |
| 9 | Parking internal thiếu service key | 401 | 401 | Đạt |
| 10 | Reservation internal thiếu service key | 401 | 401 | Đạt |
| 11 | Login với body rỗng | 400 | 400 | Đạt |
| 12 | Driver registration với DTO không hợp lệ | 400 | 400 | Đạt |
| 13 | Owner registration với DTO không hợp lệ | 400 | 400 | Đạt |
| 14 | Refresh không có token | 400 | 400 | Đạt |
| 15 | Parking internal validate với service key đúng và danh sách rỗng | 200 | 200 | Đạt |
| 16 | Admin login | 200 | 200 | Đạt |
| 17 | Admin đọc `/me` | 200 | 200 | Đạt |
| 18 | Admin đọc danh sách đơn Owner | 200 | 200 | Đạt |
| 19 | Admin đọc danh sách user | 200 | 200 | Đạt |
| 20 | Admin đọc application ID không tồn tại | 404 | 404 | Đạt |
| 21 | Admin gọi danh sách Operator dành cho Owner | 403 | 403 | Đạt |
| 22 | Admin gọi assignments dành cho Operator | 403 | 403 | Đạt |
| 23 | Admin gọi Parking list dành cho Owner | 403 | 403 | Đạt |
| 24 | Admin logout | 200 | 200 | Đạt |
| 25 | Access token cũ gọi `/me` sau logout | 401 | 401 | Đạt |
| 26 | Refresh token cũ sử dụng sau logout | 401 | 401 | Đạt |

**Ghi chú hợp đồng lỗi:** Lượt chạy đầu có một expectation của script sai: refresh thiếu token được kỳ vọng 401. Kiểm tra handler xác nhận contract hiện tại trả `MISSING_REFRESH_TOKEN` với HTTP 400. Đã sửa expectation của script và chạy lại đạt 26/26; không sửa ứng dụng hoặc đổi contract để làm test xanh. Refresh token đã bị thu hồi vẫn phải trả 401 và đã được kiểm tra.

**Những phần chưa được chứng minh bằng smoke test này:**

- Đăng ký hợp lệ, gửi/nhận OTP, xác minh và đăng nhập Driver/Owner/Operator.
- Admin duyệt đơn thật; Owner tạo structure/Operator thật; Operator thao tác đúng hoặc sai site với tài khoản của chính role đó.
- Refresh rotation thành công, lost-response hoặc race refresh/logout.
- Owner xem 3D bằng trình duyệt, check-in/out có session backend, slot đồng bộ giữa tài khoản và payment. Các khoảng trống implementation ở mục 4 vẫn còn.

**Thay đổi trong lần chạy:** Chỉ tạo session Admin để kiểm tra rồi thu hồi bằng logout; gửi DTO đăng ký không hợp lệ để kiểm tra validation. Không tạo account/site mới, không duyệt đơn, không gửi OTP/email, không tạo giao dịch. Script lưu tên ca kiểm tra và status; không lưu token/password/response chứa dữ liệu cá nhân.

Có thể chạy lại từ root repository bằng PowerShell:

```powershell
.\scripts\test-demo-smoke.ps1
```

Script dùng credential Admin/service key trong root `.env` và yêu cầu các service local đã chạy. Đây là kiểm tra nhanh các route đang có, không phải kết luận toàn bộ kịch bản demo đã đạt nghiệm thu.
