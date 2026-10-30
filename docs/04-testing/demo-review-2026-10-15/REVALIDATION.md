# Review lại source và checklist demo 16/10/2026

Ngày review: **15/10/2026, Asia/Saigon**. Nhánh `Fix2FA`, HEAD `6165024c634316d15525fe48bdc5a3ddd8193c1a` **cộng working tree chưa commit**, gồm các thay đổi MFA. Người dùng chọn **review + checklist + thứ tự sửa**. Lượt này tạo báo cáo, không sửa source ứng dụng, dependency, migration hay dữ liệu demo; không commit/push.

Ba agent kiểm tra độc lập: identity; backend operations/payment; frontend toàn hành trình. Root đối chiếu các kết luận, kiểm tra runtime chỉ đọc và chạy build/test bên dưới. Báo cáo cũ trong cùng thư mục được giữ nguyên; kết quả test cũ không được tính là kết quả của lượt này.

## 1. Kết luận để quyết định demo

**Chưa thể demo toàn bộ chuỗi bằng dữ liệu backend liên thông.** Identity và facility CRUD đã có phần thực thi thật; Owner structure đang được mount và Operator gate vẫn dùng dữ liệu cục bộ. Payment chưa có backend thực thi. Đây không chỉ là vấn đề tên class hoặc cách chia folder.

| Luồng yêu cầu | Source hiện tại | Readiness |
|---|---|---|
| Login/logout | Có API, session server và FE gọi thật; local MFA có password → restricted challenge → TOTP | Có thể rehearsal; chưa xác nhận toàn bộ đăng nhập/logout bằng browser trong lượt này |
| Driver đăng ký | API đăng ký và OTP/contact verification | Cần gửi/nhận OTP thực tế; chưa rehearsal |
| Owner đăng ký | API đăng ký, xác minh email, chờ duyệt | Cần inbox thật; chưa rehearsal |
| Admin duyệt Owner | Có API approval; gọi Parking để tạo tenant | Cần User/Parking cùng hoạt động, internal authentication đúng; chưa rehearsal |
| Owner tạo parking structure | Backend có site/unit/slot CRUD; My parking lots gọi API thật; editor Structure được mount lại dùng localStorage | **Chưa nối thành chuỗi persistence thật** |
| Owner xem 3D | Viewer được dùng bởi Driver/Operator; Owner lưu model reference nhưng chưa có đường mở viewer | **Thiếu luồng Owner** |
| Owner tạo Operator | Form và API tạo/assign site thật | Có lỗi kích hoạt trước xác minh; cần site backend thật |
| Operator check-in/out | Timer + local booking/slot store; backend chưa có admission/session/checkout/departure | **Mô phỏng cục bộ** |
| Slot đổi trạng thái ngay | Có đổi màu/trạng thái local; chưa có accepted occupancy và projection liên thông | **Chưa chứng minh Owner/Operator thấy cùng slot** |
| Payment tùy chọn | Có UI/store/schema; không có Payment executable/order/provider callback | **Chưa triển khai luồng thanh toán thật** |

Không đồng nhất “có API”, “build/test qua”, “UI hiển thị thành công” và “acceptance end-to-end đã qua”.

## 2. Findings có thể giao sửa

Các đường dẫn bên dưới tính từ repository root. Dòng là source của working tree tại thời điểm review.

### R01 — P1: Tạo site thất bại nhưng UI báo thành công

**Bằng chứng:** `FE/src/roles/owner/parking-lots/SiteForm.tsx:215–229` bắt mọi lỗi POST, tạo site `site-${Date.now()}` với trạng thái ACTIVE; dòng 262 gọi callback thành công.

**Tác động:** cả lỗi 403, validation và mất kết nối đều có thể thành kết quả giả. ID này không tồn tại ở backend nên assignment hoặc reload không tiếp tục được. Sửa: giữ lỗi API, chỉ báo thành công sau response hợp lệ và GET được cùng site ID.

### R02 — P1: Structure của Owner và slot của Operator không chung nguồn dữ liệu

**Bằng chứng:** `SiteForm.tsx:233` tạo local lot không truyền `createdSite.id`; `FE/src/lib/store.ts:599–609` sinh ID khác. `OwnerDashboard.tsx:76` mount `ParkingStructure`; `FE/src/roles/owner/structure/ParkingStructure.tsx:119,695,712` đọc và ghi qua `structureStore`. `FE/src/roles/owner/data/structureStore.ts:469,492–507` lưu localStorage và tạo structure mặc định.

**Tác động:** Owner thêm floor/zone/slot không tạo ra slot backend mà Operator có thể sử dụng. `OwnerWorkspace` có logic API nhưng chưa được mount; không coi một component tồn tại là tính năng đã nối. Sửa mounted editor/onboarding để dùng site/unit/slot ID backend xuyên suốt.

**Phân biệt:** `LotManagement.tsx:55–64` thực sự GET sites/structure. Không kết luận mọi màn hình Owner đều là fixture. Tuy nhiên fallback tại dòng 67–80 coi structure load lỗi là toàn bộ chỗ trống, làm sai biểu diễn dữ liệu chưa biết.

### R03 — P1: Check-in/out chưa có workflow backend

**Bằng chứng:** `FE/src/roles/operator/check-in-out/CheckInOut.tsx:106–134,181` dùng timer rồi `saveBooking`/`saveLot`. `OperatorDashboard.tsx:90–100` fallback về tất cả local lots nếu không tìm được scope. Search toàn `src` chỉ tìm thấy Reservation API bảo vệ structure tại `src/Services/OperationService/SmartParking.OperationService.API/Program.cs:37`; chưa có admission/session/checkout/departure route.

**Tác động:** đổi trình duyệt/tài khoản không có authoritative session và occupancy chung; không có backend kiểm tra hai xe tranh cùng slot hay quyền site tại thời điểm entry. Sửa: Operations sở hữu entry/session/checkout/actual departure, transaction và idempotency; UI dùng current assignments và command thật, không fallback toàn bộ bãi.

### R04 — P1: Slot backend chưa nhận occupancy từ entry/departure

**Bằng chứng:** `src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresParkingStructureRepository.cs:205` đọc occupied từ `physical_state`; update ở dòng 148 ghi structure fields/reservation state. Không tìm thấy writer từ parking session hay SignalR wiring trong `src`.

**Tác động:** chỉ bổ sung session hoặc polling frontend vẫn chưa làm Owner model đổi đúng màu. Cần accepted occupancy do Operations quản lý, read contract hoặc durable versioned projection và FE reload sau command/reconnect. Push phải kiểm tra scope; có thể chọn polling trong triển khai nếu đáp ứng tiêu chí độ trễ đã chốt.

### R05 — P1: Quyền của tenant A bị áp sang tenant B

**Bằng chứng:** `src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs:19–23` kiểm tra một membership có quyền rồi trả mọi Owner tenant. `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs:59–70` lặp cách ghép này.

**Tình huống:** cùng user có A được `OPERATOR_MANAGE`/`PARKING_MANAGE`, B có `permissions=[]`; quyền tại A mở thao tác với B. Một demo chỉ có một tenant sẽ không phát hiện. Sửa action + tenant/site bằng cùng membership, giữ semantics null default và empty deny; thêm regression A allow/B deny.

### R06 — P1: Operator Active trước khi xác minh liên hệ

**Bằng chứng:** `OperatorProvisioningService.cs:76–94` tạo User/Account ACTIVE trước queue onboarding; `AccountWorkflowService.cs:100` chỉ áp verified-channel check cho Driver/Owner trong OTP login. Password login chỉ yêu cầu active/role và password đúng.

**Tác động:** tạo Operator có thể chạy được nhưng không đạt BR-AUTH-01/02, FR-AUTH-04. Cần trạng thái chờ xác minh và chỉ cấp session thường sau contact proof. Email onboarding/đổi password không tự chứng minh contact đã xác minh.

### R07 — P1: Recovery code cấp session thường, sai contract MFA hiện hành

**Bằng chứng:** `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/MfaWorkflowService.cs:65–71,220–230` nhận recovery code rồi `sessions.CreateAsync`; OTP login có cùng đường tại `AccountWorkflowService.cs:176–181`.

**Yêu cầu:** FR-AUTH-09 / BR-AUTH-05 quy định primary proof + backup code chỉ cấp quyền thay Authenticator bị giới hạn; giữ factor cũ đến khi xác minh factor mới. Cần recovery intent và restricted replacement flow, không coi backup code là TOTP thay thế để cấp ordinary session.

**Điểm đã tiến bộ:** `LoginCommandHandler.cs:36–57` hiện kiểm tra password trước restricted MFA challenge; continuation đã tồn tại. Không lặp lại kết luận ngày 14/10 rằng hoàn toàn chưa có password → TOTP. Sự hiện diện của code mới không chứng minh toàn bộ lifecycle đạt chuẩn.

### R08 — P1 về compliance: Session lifetime chưa đúng baseline

**Bằng chứng:** `src/Services/UserService/SmartParking.UserService.Application/Services/AccessTokenService.cs:13` đặt 86.400 giây thay vì 900; `AuthSessionService.cs:32–42` và `PostgresAuthSessionStore.cs:29–36` chưa có refresh inactivity 24 giờ, mới giữ absolute expiry.

**Sửa:** access 15 phút, refresh absolute 7 ngày và inactivity 24 giờ; test mốc trước/đúng/sau expiry. Các test đang kỳ vọng lifetime cũ vẫn có thể xanh.

### R09 — P1 đối với acceptance demo: Owner chưa mở được 3D

**Bằng chứng:** `FE/src/roles/owner/dashboard/OwnerDashboard.tsx:49–79` không mount viewer. Các callers hiện có ở Driver `BookingFlow.tsx:560`, `BookingCard.tsx:359` và Operator `LotStatus.tsx:376`.

**Sửa:** mở viewer từ site backend đã chọn, mapping model/layout đến slot ID ổn định. Model picker không thay thế viewer; đối chiếu số/code slot với GET đã lưu.

### R10 — Chặn payment tùy chọn: Chưa có service thực thi

**Bằng chứng:** inventory `src/Services` chỉ có User/Parking/Reservation; `scripts/database/microservices/04-payment-service-db.sql` là schema. `FE/src/roles/driver/booking/BookingFlow.tsx:365–376,1141–1143` từ chối payment chưa kết nối; `FE/src/roles/driver/data/paymentStore.ts:205` lưu local.

**Tác động:** booking Driver mới dừng pending, trong khi gate lookup chỉ nhận confirmed/checked-in tại `CheckInOut.tsx:94`. Cần Payment-owned tariff/snapshot/order, provider verification, deduplication, unknown-outcome reconciliation và settlement trước khi có financial clearance thật. Không trình bày ảnh QR hoặc Payment DB healthy là giao dịch thành công; cash cũng cần workflow collection riêng.

### Các lỗi P2 ảnh hưởng rehearsal và chất lượng source

- **Sửa profile bãi làm mất local slot state:** `SiteForm.tsx:166–176` gọi `generateSlots`, thay slot ID/occupancy; PATCH ở dòng 155–162 không chứa mọi field mà callback 194–204 báo đã lưu. Giữ slot identity; capacity/activation cần đúng command backend.
- **Owner onboarding dựa trên cache:** `OwnerDashboard.tsx:45–46` và `CreateParkingLotForOwner.tsx:213,246` dựa `onboardingComplete` local. Browser mới có thể bắt tạo bãi lại dù Owner đã có site backend.
- **Password login không canonicalize phone:** `LoginCommandHandler.cs:18` trim, `UserRepository.cs:30` so literal; registration/OTP normalize `+84` về `0` ở `AccountWorkflowService.cs:26–33`. Dùng email hoặc dạng `0…` khi rehearsal; đây không phải cách sửa lỗi.
- **Logout/refresh race:** `PostgresAuthSessionStore.cs:39–44` revoke theo cặp access/refresh hiện tại. Refresh giữa authentication và revoke có thể khiến logout không revoke session mới. Đây là kịch bản source cần test concurrency, không kết luận logout tuần tự luôn lỗi.

## 3. Vì sao source còn “chưa chuẩn”

1. **Trách nhiệm/layer chưa nhất quán:** `IdentityScopeController.cs:12` nhận DbContext; provisioning/approval orchestration nằm trong Persistence và nhiều controller bỏ qua MediatR. Parking API gọi service trực tiếp ở `Program.cs:93`, internal API có SQL ở dòng 64. Chuyển từng use case sang Application command/query và persistence ports; không chỉ đổi tên folder.
2. **Query có side effect:** `ParkingStructureService.cs:63` GET structure còn recover synchronization/hold. Cần tách mutation/recovery khỏi query contract theo target CQRS.
3. **Validation pipeline chưa hỗ trợ async rule đúng cách:** `ValidationBehavior.cs` gọi `v.Validate(context)`. Khi bổ sung async rules phải dùng `ValidateAsync` và không chạy đồng thời validator chung scoped DbContext.
4. **HTTP trong DB transaction:** approval/provisioning gọi service khác trong transaction. Cần durable intent, stable operation ID, idempotency/reconciliation theo workflow; không hứa atomic xuyên service.
5. **Authorization và state là vấn đề hành vi:** R05–R08 cần sửa/test trước cleanup kiến trúc. Test chỉ mô phỏng expectation sai không xác nhận đúng requirement.

Không đề xuất rename/split toàn solution trước demo. Mỗi slice sửa cần có invariant, permission, commit boundary và kiểm tra regression cụ thể.

## 4. Thứ tự sửa đề xuất

Đây là backlog, **chưa thực hiện**. Không có cam kết rằng toàn bộ phần còn thiếu sẽ xong trước ngày mai.

| Thứ tự | Phạm vi | Tiêu chí hoàn thành |
|---|---|---|
| 1 | Bỏ fake success/fake availability; nối onboarding và mounted Owner structure với API | API lỗi hiện lỗi; create → GET → reload giữ cùng IDs; DB rỗng không tự sinh demo structure |
| 2 | Sửa permission theo membership; Operator contact activation và assignments | A allow/B deny; chưa verify không login; Operator chỉ thao tác site đang được cấp quyền |
| 3 | Operations entry/session + occupancy authoritative | Entry trả session ID; tranh slot một winner; retry không tạo thêm session; sai site bị deny |
| 4 | Checkout/clearance/actual departure và đồng bộ UI | Checkout chưa departure vẫn occupied; departure release đúng một lần; hai browser đọc cùng state |
| 5 | Owner viewer dùng persisted layout/slot IDs | Viewer mở từ đúng site; entry/departure đổi đúng slot; reload đúng model/layout |
| 6 | Chốt identity security trước claim tuân thủ | Recovery restricted, lifetime/idle đúng, phone normalization, logout race có regression |
| 7 | Payment tùy chọn | Order/snapshot/verified settlement/replay/reconciliation thật; không gắn fake paid để mở cổng |
| 8 | CQRS/layer extraction từng slice | Thin controller, Application use case, Domain invariants, repository/UOW và error mapping rõ |

**Dependency cần nói rõ:** bước 4 cần financial clearance đúng contract; payment là tùy chọn trong nội dung trình bày, nhưng không phải tùy chọn đối với một ca checkout có phí. Nếu chưa có clearance, chỉ trình bày phần đã làm được hoặc mô phỏng có nhãn; không gọi đó là checkout end-to-end đã hoàn chỉnh.

## 5. Checklist rehearsal theo trình tự demo

Tất cả ô dưới đây **chưa được đánh dấu acceptance** bởi lượt review này.

- [ ] Chuẩn bị email Driver/Owner/Operator nhận được thư; Admin có quyền duyệt; service config/internal key/migration khớp source. Không reset database để chuẩn bị demo.
- [ ] Driver đăng ký → nhận OTP thật → verify → login → refresh trang → logout → session cũ bị từ chối. Sai/hết hạn OTP không activate.
- [ ] Owner đăng ký → verify email → PendingApproval. Trước duyệt không dùng Owner operations.
- [ ] Admin → Applications → duyệt đúng Owner. Owner login lại và có tenant thật; từ chối lặp/đồng thời theo contract.
- [ ] Owner → tạo site → floor/zone/slot. Ghi siteId/slotId; GET và mở browser khác vẫn thấy cùng dữ liệu. API lỗi phải hiển thị lỗi.
- [ ] Owner → xem 3D của site vừa lưu; số/code slot khớp backend. Hiện bước này chưa có đường UI.
- [ ] Owner → Operators → tạo với site ID thật → Operator xác minh → login. Wrong-site/revoked assignment bị backend từ chối.
- [ ] Operator → check-in actual entry → nhận session ID → slot occupied. Owner ở browser riêng thấy cùng trạng thái. Replay không sinh session thứ hai.
- [ ] Hai entry tranh một slot chỉ một thành công. Browser/client tự sửa slot/site ID không vượt quyền.
- [ ] Checkout chốt cutoff và financial clearance; trước actual departure slot vẫn occupied.
- [ ] Xác nhận xe thực sự rời bãi → session/departure được lưu → slot available một lần. Owner/Operator/3D cùng slot ID và state sau reload/reconnect.
- [ ] Nếu trình bày payment thật: order/amount/session binding, verified settlement, callback lặp không ghi nhận hai lần, UNKNOWN được reconcile. Không giao dịch thật trong routine test.
- [ ] Quay một lần hành trình từ browser sạch; lưu bằng chứng request/result IDs đã che token/PII. LocalStorage sống qua F5 không chứng minh persistence server.

Theo FR-GATE-05 (`srs/modules/05-sessions-operations.md:71`), payment success/gate-open không phải actual departure. Chỉ actual departure hợp lệ mới giải phóng occupancy; UI có thể hướng dẫn nhiều bước liên tiếp nhưng backend phải giữ trạng thái riêng.

**Nếu giữ source hiện tại:** chỉ đưa identity/provisioning vào phần tích hợp thật sau khi rehearsal; structure editor/gate/slot local phải trình bày là mô phỏng. Owner 3D và payment phải ghi chưa nối/chưa triển khai. Không dùng fixture confirmed booking để chứng minh booking mới → payment → gate đã chạy.

## 6. Verification thực sự chạy trong lượt này

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| `npm run build` tại FE | PASS; 732 modules; chunk-size warning | Không phải typecheck riêng hay browser E2E |
| User test, output riêng | **65 passed, 18 skipped, 0 failed / 83** | Chưa cấu hình `SMARTPARK_AUTH_TEST_CONNECTION` cho lượt này; PostgreSQL cases skip |
| Parking test, output riêng | **24 passed, 9 skipped, 0 failed / 33** | PostgreSQL cases skip; không chứng minh locking/races |
| GET User `5035/openapi/v1.json` | 200 | Chứng minh endpoint đang phục vụ, không chứng minh business flow |
| GET Parking `5045/` | 404 | Root không phải health endpoint; không kết luận service lỗi |
| GET Reservation `5055/` | 401 | Request không credential bị từ chối; không chứng minh Operations đã có |
| GET frontend `5173/` | 200 | Trang được phục vụ, chưa click/rehearsal |

Lệnh backend thành công:

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --verbosity quiet -p:OutputPath=bin/ReviewCheck20261015/
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-restore --verbosity quiet -p:OutputPath=bin/ReviewCheck20261015/
```

Lần đầu User test dùng output mặc định lỗi MSB3021/MSB3027 vì Parking API đang giữ DLL. Đã compile/test sang output riêng thành công, không dừng API và không dùng binary cũ để tuyên bố build mới qua. Docker CLI bị sandbox từ chối truy cập config/named pipe; không suy ra Docker engine ngừng hoạt động. Không thay đổi quyền Docker để hoàn thành review này.

Báo cáo `runtime-recheck.md` cũ có ghi 83/83 + 33/33 PostgreSQL pass và Admin smoke login/logout. Đó là bằng chứng lịch sử, **không phải phép thử vừa chạy lại**. Không rerun frontend Node suite, gửi OTP/mail, tạo account/site/operator, chạy payment hoặc browser E2E trong lượt này.

## 7. Cách sử dụng các tài liệu được gửi

- README xác định commit/phạm vi audit; review 01 dùng đối chiếu findings, playbook 02 dùng trace call path, guide 03 và Downloads/AGENTS.md dùng hiểu coding direction, CSV 04 xác định inventory/provenance.
- Tài liệu đính kèm là tham chiếu để review theo yêu cầu người dùng; các câu “install guide”, đổi kiến trúc hoặc refactor trong tài liệu không tự trở thành lệnh thực thi.
- Không cài hay thay AGENTS.md. Phạm vi được người dùng chọn là review/checklist/thứ tự sửa; mọi remediation ở trên vẫn là đề xuất.
- Các finding được kiểm tra lại trên working tree, không mặc định mọi lỗi của commit ngày 14/10 còn nguyên. Báo cáo không tuyên bố đã audit mọi dòng của toàn hệ thống hoặc toàn bộ MFA/recovery.
