# SPARK-340 · Independent Technical Review & Quality Gate

**Phán quyết: REJECTED — trả lại Phase 1.** Ngày review 12/11/2026. Đối tượng là bản API Design v1.0, trạng thái IN REVIEW trên nhánh `feat/SPARK-340-admin-platform-policies`. Đây là đánh giá hợp đồng tĩnh. Tám endpoint đều là Proposed. Chưa có kết quả chạy xUnit, Newman hay UAT trong lần review này. [Bản thiết kế](admin-platform-policies-and-integrations-spark-340-api-design.md)

## 1. Executive summary

Hai điều kiện chặn phê duyệt là phạm vi ghi Site của Admin vượt ma trận quyền và khả năng chuyển credential PayOS tới đích chưa được phê duyệt. Ngay cả khi đóng hai vấn đề này, ví dụ catalogue, quy tắc kích hoạt và DTO Site hiện vẫn cho BE, FE và QA các kết quả khác nhau trên cùng một dữ liệu.

| Chiều đánh giá | Kết quả tại cổng thiết kế |
| --- | --- |
| 1. Cấu trúc | Đủ metadata, overview, traceability, security, 8 endpoint, error catalogue, concurrency/snapshot, 3 sequence diagrams, open questions và review checks. Các khối metadata endpoint đầy đủ. Bảng trường, validation và ví dụ lỗi chưa đồng đều. |
| 2. Kiến trúc | Đúng ba service sở hữu và nguyên tắc cấm đọc chéo DB. Controller, MediatR/validation và Domain đã được nhận diện. Sequence 1–2 nối Application trực tiếp với PostgreSQL và §6.2 đặt LINQ persistence ở handler. Cần thể hiện repository port và EF ở Persistence. |
| 3. Bảo mật | JWT Bearer, ADMIN, TOKEN với độ trễ tối đa phần đời còn lại của token 15 phút phù hợp ADR. FR-LOT-06 được giữ đối với thiết bị và phiên đỗ. GET/PUT chỉ trả cờ cho hai key và mask Client ID đúng dạng `****1234` (chuỗi ngắn chỉ `****`). Thiết kế cấm log raw credential nhưng thiếu bảo vệ đích gửi và đường lỗi. |
| 4. Nghiệp vụ và đồng thời | Bốn key chống lạm dụng và múi giờ thuộc Platform, SQL cũng chặn Site ghi. Zero của buffer và flag disabled được minh họa đúng. Null theo scope, bounds v1, thay thế revision và mapping snapshot còn sai hoặc chưa quyết định. |
| 5. Sẵn sàng triển khai | Chưa thể dùng v1.0 làm SSOT để ba nhóm viết DTO, form và assertion độc lập. Xem điều kiện bàn giao ở §4. |

## 2. Detailed findings

Số dòng dưới đây thuộc bản thiết kế được rà soát. “Critical” chỉ rủi ro nếu triển khai đúng theo hợp đồng đề xuất. Không có bằng chứng trong lần review này về credential thực đã bị lộ hoặc booking thực đã bị hồi tố.

### Critical — chặn phê duyệt

**C1 · Admin có thể ghi lựa chọn riêng của bãi.** Dòng 619, 657, 664 và 699 cho phép Admin tạo rồi kích hoạt Site revision, kể cả lựa chọn bật/tắt late tolerance của Owner. Ma trận `policy.operational.publish` chỉ cấp cho Admin quyền ghi defaults/bounds của Platform. Admin xem effective theo Site là luồng đọc hợp lệ. Giới hạn cả tạo lẫn activate theo scope của bản ghi, gồm trường hợp Admin gửi ID của một Site draft có sẵn. Chốt kết quả từ chối và kiểm tra revision vẫn nguyên trạng. [Ma trận quyền](../../01-requirements/srs/permissions.md) · [Catalogue, ARRIVAL_LATE_TOLERANCE](../../01-requirements/business-rules/policy-catalogue.md)

**C2 · PUT PayOS cho thay đích kết nối trong khi giữ key cũ.** Dòng 843–854 cho ghi `baseUrl` và giữ credential khi bỏ qua/null. Hợp đồng không giới hạn đích theo provider/mode. DI đặt `HttpClient.BaseAddress` theo cấu hình và adapter gắn `x-api-key`, `x-client-id` lên request. Khi PUT có hiệu lực, masking trên GET không bảo vệ credential ở lần gọi outbound. Chốt tập đích HTTPS do deployment tin cậy theo mode, hoặc để server chọn đích. Từ chối cấu hình ngoài tập trước khi lưu, không chuyển credential qua redirect khác origin, và xác nhận fixture không gửi request mang key. Kiểm tra cả response lỗi và log bằng credential giả. [HTTP client của Payment](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/InfrastructureDependencyInjection.cs) · [PayOsAdapter](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs)

### Major — phải sửa trước khi khóa hợp đồng

| ID | Sai lệch và tác động | Điều kiện đóng |
| --- | --- | --- |
| M1 · Bounds v1 | Dòng 169–236, 406–420 và 593–597 mô tả bounds như min duration 15–60 và hold 5–15 trên v1, kể cả override 30 phút trên chính ID seed. Seed chỉ có `ARRIVAL_LATE_TOLERANCE` 15–30. Resolver từ chối giá trị thay đổi khi thiếu bound. FE có thể bật một ô nhập mà BE luôn từ chối. | Trả bounds từ revision tại thời điểm đọc. Phân biệt key được phép cấp bounds với key hiện đã có bounds. Cho ví dụ v1 đúng seed và ví dụ override trên một revision mới thực sự cấp bounds. [Seed v1](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql) · [Resolver](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs) |
| M2 · DTO và kế thừa | Dòng 658–663 bắt buộc số cho cả Site trong khi null ở Site cho phép kế thừa. Bốn trường duration ở 634–637 có trong JSON nhưng thiếu validation table. Dòng 528 cho phép cả null lẫn true ở Platform dù SQL yêu cầu cờ Platform null. Resolver yêu cầu Site revision và lựa chọn bật/tắt tường minh. | Khóa bảng theo scope, null/omitted, đơn vị minutes trên effective/bounds và hours/days trên draft. Chốt dữ liệu Platform, Site disabled, Site chưa có revision, site không tồn tại và tenant/site lệch nhau. Phân biệt thiếu quyết định Site với lỗi 404. Làm rõ nguồn của override có giá trị bằng default. [SQL](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) · [OperationalPolicyRow](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/OperationalPolicyRow.cs) |
| M3 · Kích hoạt và race | Dòng 718–720 cho đóng interval cũ đang mở. Predicate 975–977 lại bắt chính interval đó trong kiểm tra overlap và không thể hiện lọc scope. Ví dụ 735–738 và 744–755 có cùng mốc nhưng thiếu tiền điều kiện phân biệt 200 với 409. Dòng 714 chỉ kiểm tra revision đang mở, trong khi catalogue lỗi cấm activate cả revision đã hết hạn. Không có quy tắc từ chối mốc quá khứ. | Chốt theo giờ server, chỉ chuyển từ draft, xác định predecessor được đóng và các interval còn lại trong cùng scope. Thay đổi hai hàng nguyên tử. Kiểm tra hai khoảng kề nhau tại T, đã có lịch tương lai, hai admin requests đồng thời, gửi lại sau timeout và backdate. GiST chỉ áp dụng hàng `is_active`, trả 409 cho constraint `operational_policy_effective_scope`. |
| M4 · Sửa draft và lịch sử | AC-3 yêu cầu tạo **và sửa** draft. POST luôn tạo ID/version mới, GET history chỉ là ví dụ rút gọn, chưa có cách tải đủ dữ liệu để sửa hoặc nhận biết draft/scheduled/current/expired khi mở lại màn hình. `isActive=true` xuất hiện cả khi chưa tới ngày hiệu lực. | Chốt cách sửa/thay thế draft và quan hệ revision cũ–mới trong phạm vi đã duyệt, điều kiện chống ghi đè, outcome khi retry, trạng thái ở mốc biên, thứ tự phân trang ổn định. Gắn actor/scope vào dấu vết thay đổi. |
| M5 · Snapshot | Cam kết không hồi tố ở 949–961 đúng. Dòng 950 chỉ sai tên cột ở `reservation_terms`. Bảng này dùng `snapshot_json` ở bước review. `reservations` có `accepted_operational_policy_snapshot` và `accepted_pricing_snapshot`. Gán cùng tên cho giao dịch Payment cũng chưa có mapping. | Nêu rõ review → acceptance và nguồn version/giá của Payment. So sánh snapshot và revision của booking/payment A trước và sau activation, rồi tạo B sau T để xác minh chính sách mới. Bao gồm review đã thay đổi cần xác nhận lại theo FR-POL-06. [Schema Operations](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) · [SRS](../../01-requirements/srs/modules/04-reservations-allocation.md) |
| M6 · Cấu hình PayOS thực sự có hiệu lực | Dòng 795–808, 854–871, 1058–1068 hứa mode, health, lastTestedAt và cập nhật options. Options hiện chỉ có ClientId, ApiKey, ChecksumKey, BaseUrl. Adapter giữ `IOptions.Value` và chọn fallback từ credential. Ví dụ PUT tự trả HEALTHY với timestamp mới dù không mô tả lần kiểm tra. Audit không thể hiện version/scope theo UC-POL-02. Adapter còn ghi nguyên response body khi provider trả lỗi HTTP. | Chốt nguồn lưu bền thuộc Payment, thời điểm adapter thấy bản cập nhật, cách giữ/rotate key, điều kiện LIVE/SANDBOX/FIXTURE, nguồn health/webhook status và hành vi khi chưa test. Định nghĩa audit redacted có scope/version. Bao phủ outbound errors, middleware và diagnostics cho cả đường thành công và lỗi. [Options](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Options/PayOsOptions.cs) · [UC-POL-02](../../01-requirements/use-cases/uc-pol-02.md) |
| M7 · Hợp đồng lỗi | Dòng 98 gọi envelope là chuẩn chung. Với Operations, FluentValidation hiện trả `INVALID_REQUEST`, exclusion/unique trả `RESOURCE_CONFLICT`; v1.0 hứa `VALIDATION_FAILED` và `OVERLAPPING_POLICY_INTERVAL`. Dòng 978 mô tả handler như đã thực hiện mapping. Chưa xác định lỗi chính xác khi Site override ngoài bound hay khi cấu hình đọc lên đã bất nhất. | Ghi mapping mới là Proposed, phân biệt lỗi write 400 với read 503, xác định field errors tùy chọn, bind/auth/domain/provider và mã theo endpoint. Chỉ map đúng constraint policy sang overlap. Ghi rõ adaptation tại từng service trước khi QA dùng chung assertion. [Handler hiện tại](../../../src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs) · [Envelope Operations](../../../src/Services/OperationService/SmartParking.OperationService.API/Middleware/ApiErrors.cs) |

### Minor / suggestion

- Metadata còn thiếu người soạn và bằng chứng/link Jira. Error sample dòng 103–109 vẫn có placeholder mẫu như `<FIELD_NAME>`. Dùng ví dụ validation thật với dữ liệu tổng hợp. Bearer token minh họa và `{id}` trong route là ký pháp giao thức hợp lệ. Bảng traceability trỏ §7/§7.2 cho snapshot và GiST, vị trí đúng là §6.1/§6.2.
- §2 bỏ FR-POL-06. Ghi nhận là dependency của luồng Driver/accepted terms và liên kết hợp đồng sở hữu. Ghi rõ 14 key là tập của `ReservationPolicyResolver.RequiredKeys`; policy catalogue v1.1 còn nhiều domain và key khác. [UC-POL-01](../../01-requirements/use-cases/uc-pol-01.md) · [Template](api-design-template.md)
- Sơ đồ đang dùng chuỗi `APP-->>API-->>FE` cho hai lần trả về. Tách từng message thành một dòng, render lại cả ba sơ đồ. Sequence 2 nên tách caller nội bộ Booking Engine khỏi route HTTP yêu cầu ADMIN.

### Kiểm tra tám endpoint

Cả tám có method, URL, availability, authentication, freshness, permission, scope, headers, content type và ví dụ thành công. Các phần còn thiếu để FE và QA dùng trực tiếp như sau.

| Endpoint | Thiếu hoặc cần chốt |
| --- | --- |
| GET catalogue | Bảng response thiếu description; điều kiện bound null với key chưa được cấp range và ví dụ v1 cần thống nhất (M1). |
| GET effective | Có bảng query; thiếu bảng response cho cả Platform và Site, nullability của siteRevision/flag và kết quả khi scope không hợp lệ (M2). |
| GET revisions | Có bảng filter; thiếu schema item đầy đủ, thứ tự, trạng thái và quy tắc kết hợp filter/pageSize ngoài miền (M4). |
| POST draft | Có bảng một phần; thiếu trường, đơn vị bounds, unknown/server-controlled fields, required theo scope, quyền và lỗi từng trường (C1, M2, M7). |
| POST activate | Có ví dụ 200/409; thiếu bảng path/body/response, precondition, snapshot của lịch trước lệnh và retry đủ để tái lập kết quả (C1, M3). |
| GET payment | Có bảng response; thiếu capabilities và nguồn chứng minh health/webhook status. Quy tắc masking cho ClientId và hai configured flags có thể kiểm thử độc lập. |
| PUT payment | Ví dụ và quy tắc giữ secret khi null/omitted đã có; thiếu bảng input/output, empty string, mode thiếu credential, đích được phép, trạng thái sau khi lưu và lỗi (C2, M6). |
| GET notifications | Có đủ bốn fixture trong ví dụ; thiếu bảng DTO channel, tập status, quan hệ isReady/status, ví dụ unavailable và cách biểu diễn suy giảm từng kênh. |

## 3. Traceability & AC verification matrix

Tiêu chí lấy từ §AC của kế hoạch được duyệt (dòng 394–402). Trạng thái trong bảng đánh giá độ phủ của thiết kế. Bằng chứng chạy phần mềm thuộc Phase 2–4.

| AC | Kết quả | Bằng chứng hoặc phép kiểm quyết định |
| --- | --- | --- |
| AC-1 · Catalogue và effective view | Một phần | GET 1–2 có 14 key, kiểu, đơn vị, nguồn và zero buffer. So v1 với seed, kiểm tra nhãn inherited, explicit zero và disabled riêng biệt (M1–M2). |
| AC-2 · Resolution và bounds | Chưa đạt | Với bounds được cấp, kiểm tra hai biên inclusive, vượt mỗi biên và thiếu bound. Dùng luồng Owner được cấp quyền hoặc domain fixture; role Owner gọi route Admin phải 403. Phân biệt lỗi lúc ghi với dữ liệu hiệu lực không hợp lệ. |
| AC-3 · Draft, edit, schedule, history | Chưa đạt | Thiếu sửa draft; lịch thành công và xung đột có ví dụ chưa phân biệt. Lặp và chạy đồng thời lệnh activate; xác minh history không có gap/overlap ngoài dự kiến (M3–M4). |
| AC-4 · Snapshot bất biến | Một phần | Đã nêu invariant future-only. Cần mapping Operations/Payment và bằng chứng A trước T, B sau T với policy/price versions (M5). |
| AC-5 · Integration theo domain | Một phần | Đúng owners, GET/PUT PayOS và GET bốn fixtures. WBS mô tả delivery là đọc trạng thái, câu AC dùng “cấu hình”. Xác nhận giới hạn read-only với chủ story; ghi rõ phạm vi đã duyệt. Cần chứng minh cập nhật mode có hiệu lực (M6). |
| AC-6 · Bí mật và không cấp quyền vận hành | Chưa đạt | Mask/flags và FR-LOT-06 đúng. Test GET, PUT, validation failure, lỗi provider, diagnostics console/file và outbound destination bằng sentinel giả. Đóng C1/C2/M6. |
| AC-7 · TOKEN + ADMIN | Đạt ở mức thiết kế freshness | Tất cả 8 route khai báo đúng. Kiểm 401 thiếu/hết hạn token, 403 Owner/Driver/Operator, Admin hợp lệ và giới hạn resource scope C1. [Ma trận freshness](../../01-requirements/srs/authorization-operation-classification.md) · [ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) |
| AC-8 · Tự động hóa và bằng chứng | Chưa kiểm chứng | Cần xUnit cho invariant/race/redaction, node:test cho trạng thái form và Newman cho happy/negative paths với ID trích từ response. Chỉ ghi PASS khi có command, fixture và kết quả chạy. |
| AC-9 · Tái sử dụng, không mở rộng quyền | Một phần | Có reuse row, resolver và adapter. Resolver thực tế nằm trong `ReservationPolicies.cs`. Scope ghi Site chưa đúng; giữ pricing revisions trong Payment và dùng repository port ở Operations. [Quy chuẩn SmartPark](../../07-skills/SmartPark-Coding-Skill-v1.0.md) |

## 4. Sign-off & next steps

| Bàn giao | Quyết định | Điều kiện để ký |
| --- | --- | --- |
| Phase 2 · SPARK-368 Backend | Chưa ký | Sửa C1/C2, thống nhất DTO theo scope, lịch hiệu lực, mapping lỗi và lifecycle cấu hình PayOS với service owners. |
| Phase 3 · SPARK-369 Frontend | Chưa ký | Khóa response schema và mẫu Platform/Site/disabled, lịch sử draft, lỗi form, fixture chưa sẵn sàng và trạng thái PayOS chưa kiểm tra. |
| Phase 4 · SPARK-370 QA | Chưa ký | Chốt kết quả quan sát được cho từng ca trong ma trận, gồm từ chối ghi sai scope, hai activation đồng thời, secret trên đường lỗi và snapshot trước/sau T. QA có thể chuẩn bị fixture ngay. |

Chủ hợp đồng cập nhật bản thiết kế và đánh dấu rõ quyết định còn mở. Trong vòng review kế tiếp, BE, FE và QA cần đối chiếu cùng một bộ ví dụ với seed, schema và kết quả HTTP trước khi khóa phiên bản SSOT. Sign-off ở vòng đó xác nhận hợp đồng đủ để triển khai; nghiệm thu runtime của AC-8 cần bằng chứng chạy riêng.
