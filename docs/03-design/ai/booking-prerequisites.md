# AI Chatbot — Điều kiện triển khai draft đặt chỗ

Đối chiếu nhánh `AI-ChatBot`, HEAD `3d3def763870f66f8da71386357a66f776fa61e5` và working tree có thay đổi staged, unstaged và untracked của S01–S06. Phạm vi theo [S07](luu-tru/design-history.md#tai-lieu-4-s07--draft-đặt-chỗ-nhiều-lượt), [handoff S06](business-data-integration.md), [phiếu leader](luu-tru/design-history.md#tai-lieu-6), [availability authority](../api/assistant-s06-availability-authority.md) và [S01 API matrix](assistant-s01-api-matrix.md). Ưu tiên bản cập nhật 08/11 và source hiện tại khi các dòng lịch sử khác trạng thái.

## 1. Availability · A01–A06

| ID | Source và producer đã có | Câu hỏi cần leader/owner trả lời · OPEN |
| --- | --- | --- |
| A01 | Facility `DiscoveryCatalogueStore` xuất site/category và configuration version. `ParkingStructureService` / `StructureOutcomeBuilder` có slot và category totals cho kết quả sửa cấu trúc. Operations `SiteCapacityPoolRow` giữ pool cấp site hoặc unit; `StructureSafetyStore.BuildSnapshotAsync` đọc pool và hợp nhất slot đã biết cho structure safety. `ReservationGateStore.CreateWalkInAsync` ghi session theo category, có thể thiếu slot. | Operations + Product chọn phạm vi site/category nào có inventory đầy đủ? Chỉ rõ producer và version của tập tài nguyên không trùng, mapping slot/pool sang published configuration, quota/backup và cách xử lý xe chưa gán slot. |
| A02 | `ReservationGateStore.TransitionAsync` có liên kết reservation–allocation–session khi check-in có allocation; checkout ghi `ExitTime`, trạng thái hoàn tất và release allocation phù hợp. | Operations cung cấp bảng hiệu lực theo trạng thái pending/confirmed/allocated/parked/cancelled/expired, khóa tương quan chống đếm trùng, xử lý claim độc lập và writer commit release/expiry. Revision policy buffer/protection đến từ đâu? |
| A03 | IoT Python `recognition_store.py` ghi LPR `UNPROCESSED` với direction từ input. Operations có đường check-in/out và dữ liệu session; tìm trong call chain search chưa thấy adapter xác thực passage vật lý. | Operations + owner thiết bị chỉ rõ producer xác nhận vào/ra, ingress auth, binding tenant/site/resource/session, provenance, ordering/dedup/replay và quy tắc mất kết nối hoặc thiếu coverage. Nếu thiếu producer, trình contract riêng. |
| A04 | Facility `DiscoveryCatalogueEndpoints` → `DiscoveryCatalogue.Publish` → `DiscoveryCatalogueStore.Publish` phát hành category/version và một service window tùy chọn. `ParkingSearchHandler.Project` trả 0 nếu closure còn tươi, chứa thời điểm hiện tại hoặc giao với interval yêu cầu rảnh suốt khung giờ. | Facility + Operations xác nhận nguồn lịch đầy đủ theo ngày, ngoại lệ, resource restrictions và compatibility. Republish đổi version, SQL P01 có invalidation khi đổi profile/status/binding; thay đổi cấu trúc và restriction tham gia revision nào, và Operations kiểm revision thế nào? |
| A05 | `DiscoveryHosting` đọc một cặp ngưỡng MaxAge/Skew cho search; `DiscoveryCapacity` kiểm tuổi/skew và coverage, yêu cầu PolicyVersion có giá trị. Hiệu lực revision cần adapter chứng minh. Search có giới hạn interval/lookahead riêng. | Operations + Product chỉ rõ producer policy hiệu lực theo site/category, occupancy horizon, protection/buffer và max age/skew từng source có số đo. Chốt so sánh tại ngưỡng và vượt ngưỡng; tách policy booking khỏi giới hạn discovery. |
| A06 | `ReservationGateStore` ghi session/claim; `StructureSafetyStore.ReleaseAsync` có cập nhật pool. Facility `OperatorGateSlotStore.ClaimAvailableAsync/ReleaseAsync` ghi trạng thái slot, thiếu watermark chung và bằng chứng passage. `CapacityEvidence.Complete` là điều kiện nội bộ của adapter. | Operations liệt kê toàn bộ competing writers và cách read/fence chung với revision ngoài service. Xác minh trusted site–tenant attribution, watermark gốc, late events và consistency bằng concurrent PostgreSQL; giữ paging live. |

`DiscoveryHosting` đã nối `IDiscoveryCapacitySource` vào search. Production DI dùng `UnsupportedDiscoveryCapacity`; `DiscoveryCapacity` chỉ có thể tính số tài nguyên cố định rảnh suốt interval khi adapter cung cấp evidence đầy đủ, có scope/configuration/policy/coverage hợp lệ và đủ tươi. Nhánh closure được nối sẵn. Giữ count null cho evidence thiếu hoặc không đủ, giữ stale khi quá tuổi theo contract, giữ lỗi dependency khi owner lỗi hoặc dữ liệu mâu thuẫn. `availableOnly` chỉ nhận kết quả dương đã chứng minh. Chưa có lát cắt positive-count vừa đủ nguồn vừa có contract được duyệt để nối thêm.

`PostgresOperationalLayoutReader` của Facility cũng có slot state/count tại thời điểm đọc. Projection này phục vụ layout và thiếu coverage claim/interval giữa các owner.

## 2. Quote và accepted pricing snapshot · G04/G05

### G04 · Ranh giới đã chốt và đích contract

- **Đã chốt ở mức ownership/authorization.** Operations điều phối validation theo Account và dữ kiện domain. Payment là authority về giá. Theo ADR và S01, quote đọc dưới Driver Bearer/TOKEN với kiểm tra ownership/scope/domain. Account lấy từ actor đã xác thực. G06 create là REQUEST và vẫn có gate riêng. Quote/draft chỉ phục vụ review, không giữ chỗ.
- **Đã chốt ở mức SRS.** Review hiển thị xe, vị trí, interval, mode được bật (Specific Slot/Zone/Capacity), scope, giá, prepayment, booked minimum, extra-use, deadline và refund. Khi accept, giữ immutable phiên bản Operations và Payment đã được xác nhận. Policy booking có default 15 phút / 24 giờ / 7 ngày / buffer 0 theo thứ tự minimum / maximum / horizon / buffer; backend resolve effective policy qua hierarchy và scope, chat không hardcode. [SRS Reservations & Allocation](../../01-requirements/srs/modules/04-reservations-allocation.md)
- **Đích trong S01 §1, §7 và §15, wire còn D05 OPEN.** Operations dự kiến nhận site, vehicle, start/end và timezone, optional requested slot. Owner chuẩn hóa input và gắn opaque quote ID/version với Account, xe/category, site/tenant, khoảng thời gian chuẩn hóa, lựa chọn có ảnh hưởng và terms/version. Projection đề xuất có amount VND do Payment tính, expiry và availabilityAsOf. Server giữ quyền quyết định binding, scope và payload dùng khi xác nhận.
- **Expiry và sửa draft theo đích S01.** Tại `now >= expiresAt` quote hết hạn, với clock và TTL do owner chốt. Đổi xe/bãi/giờ hoặc input ảnh hưởng quote thì tăng revision, vô hiệu quote/confirmation cũ, rồi lấy lại bằng chứng về xe thuộc Account, site/category, interval/policy, capacity, giá và điều khoản. Chỉ áp kết quả quote khi khớp draft revision đang chờ. Thay đổi input đã review, hoặc thay đổi giá/terms/availability, cần hiển thị lại để người dùng xác nhận bản mới. Quote hết hạn chặn xác nhận với quote cũ và cần refresh; quy tắc xác nhận lại khi refresh cùng nội dung còn OPEN. Handoff form chỉ điền dữ liệu. Owner kiểm tra lại ở boundary cuối; câu đồng ý trong chat chỉ mở review.
- **Source.** Operations có `OwnReservationHosting` cho list/detail, search và `CheckoutController` cho checkout Operator/gate. Chưa thấy `POST /api/reservation-quotes` hay producer quote. Payment `PricingController` → `CalculateParkingFeeQueryHandler` → `IParkingTariffPort`/calculator tính phí từ context, hoặc dùng override được validator giới hạn theo role, hoặc accepted policy khi có ReservationId. Route nhận tenant/site từ DTO, thiếu binding Account/vehicle và lifecycle quote. `DiscoveryPriceHosting` có service trust riêng cho discovery; tariff/estimate SITE trả về chỉ dùng tham khảo.
- **Đường checkout hiện tại.** `CheckoutController.Pricing` → `CalculateCheckoutPricingQueryHandler` lấy tenant từ operator site scope → `CheckoutPaymentClient.CalculateAsync` gọi Payment calculate. Ref loại RESERVATION truyền ReservationId và có thể dẫn đến call ngược về Operations để đọc snapshot.

**OPEN · Operations/Payment + Product.** Duyệt route/DTO và delegation/trust, owner lookup xe/site và attribution tenant, policy hiệu lực/booking horizon/terms, phạm vi Payment tariff/estimate, cách lưu/resolve quote và TTL/clock. Chốt mapping unknown/stale/capacity conflict, availability revalidation tại quote và handoff, cách so sánh thay đổi có ý nghĩa, và xác nhận lại sau refresh cùng nội dung. D05 wire và G04 chưa đủ để bật draft.

### G05 · Bằng chứng và quyết định cần phê duyệt

- **Baseline đã chốt.** Snapshot bất biến lưu chính sách Payment đã accept, gồm time blocks, đơn giá, grace và rounding để tính thời gian đỗ thực tế bằng chính sách đã chấp nhận. Invoice JSON lưu kết quả tính phí riêng. SQL `03b-reservation-operations-service-db.sql` khai báo `reservations.accepted_pricing_snapshot`; chưa thấy mapping, writer hoặc provider tương ứng trong Operations C#. [Payment pricing baseline](../../02-architecture/api/payment-service/01-pricing-calculation.contract.md)

- Payment `ReservationSnapshotHttpAdapter` gọi GET `internal/reservations/{id}/pricing-snapshot?tenantId=...&siteId=...`. DI đặt base URL và timeout 3 giây, chưa thấy cấu hình service auth cho hop này hoặc counterpart trong Operations. Các query ID cần được owner kiểm binding/scope. Không dùng lại discovery key mặc định.
- Với ReservationId và không có override, handler yêu cầu accepted policy snapshot, không rơi về active tariff. Hiện adapter coi 404 là null rồi handler báo `CORRUPTED_POLICY_SNAPSHOT` (422); empty JSON body/null và JSON sai có đường lỗi corrupted, non-404 HTTP lỗi ánh xạ unavailable (503), timeout nội bộ ánh xạ 504, caller cancel truyền tiếp. Đây là hành vi source hiện tại. DTO còn mặc định hóa một số trường như policy version, grace, timezone và rounding, nên chưa thể dùng làm chuẩn snapshot nghiêm ngặt. Snapshot policy này cũng khác JSON kết quả tính phí của `PricingSnapshotExtensions`.
- Baseline Payment yêu cầu snapshot thiếu/hỏng hoặc sai site/vehicle chặn luồng tự động với 422. DTO hiện thiếu site/tenant để đối soát response; calculator báo `VEHICLE_TYPE_MISMATCH` và middleware hiện dùng 400 mặc định cho mã này. Upstream 401/403 hiện gộp vào 503. Owner cần chốt wire và đối soát trước khi nối.
- **OPEN · Operations + Payment.** Chốt giữ dependency với provider Operations được xác thực SERVICE, hay loại bỏ bằng thiết kế owner phê duyệt. Nếu giữ, chốt thời điểm persist snapshot được accept, immutable payload/version và binding reservation/site/tenant với quote và terms đã xác nhận, caller trust/scope, điều khoản retention/compatibility, strict validation, missing/forbidden/corrupt/unavailable/timeout và retry/reconciliation. Snapshot cần phản ánh chính sách đã chấp nhận sau xác nhận ở owner boundary; chat chỉ mang tham chiếu handoff. Giữ fail-closed với accepted snapshot thiếu/hỏng; các quyết định G05 và G06 về ghi/đọc/lifecycle vẫn OPEN.

## 3. Checklist triển khai và nghiệm thu S07

### Có thể làm ngay

- [ ] Chốt phạm vi site/category/mode pilot, owner phụ trách từng câu A01–A06/G04/G05 và link phê duyệt. Dùng các read contract hiện có để lập inventory/fixture nghiệm thu: xe của Account, search public, own reservation list/detail. Giữ riêng search price và availability.
- [ ] Chuẩn bị acceptance vectors cho tiếng Việt, thời gian có timezone và tham chiếu “bãi thứ hai” gắn kết quả đã hiển thị trong đúng Account/conversation. Khi nhiều lựa chọn, hỏi lại. Chuẩn bị checklist kiểm review revision, replay request và privacy theo S05. Phần này là chuẩn bị contract/test, draft vẫn chờ gate.

### Bị chặn

- [ ] Operations cần positive availability từ A01–A06 đủ authority/coverage cho phạm vi duyệt, hoặc owner duyệt hướng validation capacity khác có nguồn thật và traceability. S06 vẫn PARTIAL.
- [ ] Operations/Payment cần chốt và cung cấp G04, giải quyết G05, duyệt D05 owner wire và các phần delegation/trust áp dụng. Sau gate mới triển khai draft theo Account/conversation, ID/revision/expiry, validation khi edit và form handoff S08.
- [ ] Nghiệm thu runtime riêng cho schema, HTTPS/trust, published sites, site–tenant binding, policy/revision thật, PostgreSQL concurrency và owner failures. G06 create/admission và G07 checkout là phạm vi tiếp theo.

### Test sau khi có contract và producer

| Ca | Kết quả cần chứng minh |
| --- | --- |
| Luồng nhiều lượt | Thiếu hoặc đa nghĩa về loại xe, ngày giờ, điểm xuất phát hay bãi thì hỏi lại. Khi đủ dữ kiện, lấy validation/quote từ owner và tạo đúng revision. Handoff chỉ prefill form; assert không có call hold/create/pay. |
| Đổi xe | Xe khác hoặc xe máy thay ô tô buộc xác minh lại ownership/category, compatibility và lựa chọn phụ thuộc. Xe ngoài Account bị từ chối; nhiều xe phù hợp thì hỏi lại. Quote/confirmation cũ mất hiệu lực và reply cũ về muộn không ghi đè revision mới. |
| Đổi bãi | “Bãi thứ hai” resolve từ danh sách đã thấy cùng Account/conversation và phiên bản liên quan; nếu mơ hồ/hết hạn thì hỏi lại. Xác minh site/tenant, mode/slot, thời gian, capacity, giá/terms mới. |
| Đổi giờ | Chuẩn hóa timezone/offset, từ chối quá khứ và ngoài effective policy; đánh giá lại toàn interval. Kiểm biên half-open, thay đổi hạn chế/coverage và revision cũ. |
| Quote hết hạn | Test ngay trước, đúng và sau expiresAt với clock kiểm soát. Chặn quote cũ ở handoff/owner; quote mới chỉ gắn revision đúng. Review lại khi input, giá, terms hoặc availability đổi; refresh cùng nội dung theo quyết định OPEN ở G04. |
| Availability unknown/stale | Count null, UI báo chưa xác định; không vào ready khi thiếu authority. Fresh closure chồng interval cho kết quả đóng/0. Available-only rỗng hiển thị không có kết quả đã xác minh. |
| Owner lỗi | Test timeout/cancel, 401/403 service, 5xx và payload sai. Handoff giữ trạng thái an toàn, phân biệt dependency failure với unknown hợp lệ và không khôi phục quote đã invalid. Scope/timeout/error mapping theo contract owner. |
| Scope, replay và review | Account/conversation khác không đọc hoặc cập nhật draft. Request replay/cancel và reply về muộn không tăng revision lần nữa hoặc phục hồi revision cũ. Một câu “đồng ý” trong chat chỉ mở review form. |

Test contract riêng của Operations/Payment ở G05 và khi tích hợp G06/G07: tariff đổi sau accept vẫn dùng policy đã lưu, snapshot thiếu/hỏng/sai scope fail closed. S07 dùng fixture cho ranh giới contract đã được duyệt và kiểm handoff/review.

### Phân loại bằng chứng

- **Source hiện có.** A01–A06 có evaluator và closure, positive producer thiếu. G04 có các primitive đọc/tính, quote owner thiếu. G05 có consumer Payment và DDL, provider Operations thiếu.
- **Offline · chạy lại trên working tree 08/11.** Hai project build thành công. UserService.Tests: 54 pass, 0 fail/skip với `DiscoveryCapacityTests|ParkingSearchHttpTests|OwnReservationReadHttpTests`. PaymentService.Tests: 31 pass, 0 fail/skip với `DiscoveryPriceHttpTests|ParkingPricingCalculatorTests`. VSTest trong sandbox timeout kết nối testhost; chạy lại ngoài sandbox với các binary vừa build thì pass. Các test Domain và HTTP/DI dùng evidence synthetic hoặc owner stub; tiêu chí S07 ở trên chưa có implementation để chạy. [Session S06](../../../ai/tasks/archive/chatbot-implementation-history.md#nhat-ky-2)
- **Runtime.** Handoff 08/11 và delivery session có evidence local riêng cho IoT/User và Admin/FAQ. Facility publication rollout, search hops với trust thật, authoritative availability, G04/G05 và test concurrent end-to-end vẫn UNVERIFIED trong lần bàn giao này. [Session delivery](../../../ai/tasks/active/chatbot-development-progress.md)

## 4. Bàn giao FE · Xem lại thông tin nhập từ chat

Widget Driver nhận mẫu tiếng Việt như “đặt cho tôi chỗ ô tô từ 14h đến 16h mai”, hỏi xe/bãi còn thiếu và cập nhật khi Driver gửi “xe 51A-123.45”, “bãi FPT”, “đổi bãi sang bãi Trung Tâm” hoặc “đổi giờ sang 15h đến 17h”. Với “chiều mai”, “2h đến 4h” hoặc chỉ một mốc giờ, widget hỏi lại giờ bắt đầu và kết thúc. Với tham chiếu “bãi thứ hai”, widget hỏi tên bãi cụ thể. “Đồng ý” hiển thị thông tin để xem lại.

Kiểu [`BookingPreview`](../../../FE/src/lib/assistantBookingPreview.ts) là đầu ra của phần nhập liệu phía chat. Ví dụ sau khi Driver cung cấp đủ các mục:

```json
{
  "vehicleType": "CAR",
  "vehicleText": "51A-123.45",
  "siteText": "Trung Tâm",
  "startAt": "2026-11-11T15:00:00+07:00",
  "endAt": "2026-11-11T17:00:00+07:00",
  "timeZone": "Asia/Ho_Chi_Minh",
  "missingFields": []
}
```

| Trường | Cách dùng khi nối form |
| --- | --- |
| `vehicleType` | Gợi ý loại xe `CAR` hoặc `MOTORCYCLE`. Giá trị `null` yêu cầu nhập loại xe. |
| `vehicleText`, `siteText` | Nhãn Driver nhập hoặc `null`. Dùng làm gợi ý để tìm/chọn xe thuộc Account và bãi từ nguồn chính thức. |
| `startAt`, `endAt`, `timeZone` | Cặp ISO có offset, cùng `null` khi chưa đủ ngày giờ. Câu tương đối tính theo ngày Việt Nam lúc nhập. Widget hiện nhận khoảng trong cùng ngày. Form kiểm lại interval theo policy hiệu lực từ owner. |
| `missingFields` | Các mục nhập còn thiếu trong `vehicleType`, `vehicle`, `site`, `date`, `timeRange`. Mảng rỗng cho biết đủ thông tin nhập để xem lại. |

Phần xem lại dùng bộ nhớ React của widget theo phiên xác thực. Tải lại trang, đổi Account, tạo chat mới hoặc khi trợ lý báo không khả dụng sẽ xóa dữ liệu xem lại. Lượt nhập bản nháp được xử lý trong trình duyệt. Các câu hỏi thông thường vẫn theo luồng chat hiện có.

**OPEN · Product/Assistant + FE.** Chốt lưu trữ, vòng đời, TTL, revision, cách gắn bản nháp với hội thoại và wire truyền dữ liệu sang form. `BookingPreview` là kiểu nội bộ để FE tích hợp sau khi chốt wire. Form cần giải quyết nhãn thành ID qua dữ liệu owner và kiểm tra lại trên các chỉnh sửa.

**OPEN · Operations/Payment.** G04/G05 và validation theo owner ở các mục trên tiếp tục là điều kiện để nối báo giá và hiển thị thông tin nghiệp vụ. Dữ liệu xem lại chưa có bằng chứng về xe, bãi, chỗ trống hoặc giá.

## 5. Đối chiếu bàn giao SPARK-273 · 10/11/2026

Đối chiếu source của `AI-ChatBot` tại `04c6def2` và working tree S06/S07 với ref Git cục bộ `origin/feature/SPARK-273-driver-book-parking-ahead-of-arrival` tại `5daa4cc6`. Hợp đồng và bàn giao của owner nằm trên ref SPARK-273 trong `docs/03-design/api/reservation-first-slice-api-design.md` và `docs/05-project-management/handoffs/2026-11-09-reservation-{frontend,producer}-handoff.md`. Đây là đối chiếu source. [UNVERIFIED] Triển khai runtime và kết quả từ các service thật.

| Bước | Wire trên ref owner | Hiện trạng ở `AI-ChatBot` và việc đang chờ |
| --- | --- | --- |
| Kiểm tra chỗ | SPARK-273 cần xe CAR và `requestedSlotId` cho `SPECIFIC_SLOT`. Facility cung cấp eligibility và resource facts. Operations kiểm tra capacity theo interval khi xử lý yêu cầu. | `GET /api/parking-search` là tìm kiếm công khai. Production đăng ký `UnsupportedDiscoveryCapacity`; kết quả `null` là chưa xác định. **Operations + Facility** cần bàn giao nguồn slot/capacity có thẩm quyền cho bước chọn và kiểm tra lại khi tạo đơn. |
| Xem trước giá | `POST /api/reservation-price-previews`, Driver Bearer/TOKEN. Body cần `siteId`, `requestedSlotId`, `start`, `end`. `vehicleId` và `paymentMethod` có thể null. Response gồm `bookedAmount`, `depositAmount`, `currency`, `pricingVersion`, `requiresCompleteSelection`. | Route ở SPARK-273, chưa có ở `AI-ChatBot`. **Payment + Operations** cần nối producer thực. `POST /api/v1/pricing/calculate` và giá Discovery phục vụ ngữ cảnh riêng. |
| Báo giá để khách xem lại | `POST /api/reservation-terms`, Driver Bearer/TOKEN. Thêm `vehicleId` và `paymentMethod` bắt buộc. Response có `termsId`, `termsVersion`, `validUntil`, `commercial`, `operationalPolicy`. Owner lấy Account từ JWT. Giá và điều khoản gắn với lựa chọn. | Route ở SPARK-273. Adapter Facility và commercial mặc định trả `503 OWNER_UNAVAILABLE`. **Operations + Payment + Facility** cần bàn giao producer và policy hiệu lực. `validUntil` do owner cấp, tách khỏi `holdExpiresAt` phát sinh sau admission. |
| Form và xác nhận | Form chọn ID từ dữ liệu owner, đổi `BookingPreview.startAt/endAt` thành `start/end` có offset, bổ sung slot và phương thức. Sau khi khách chấp nhận terms, `POST /api/reservation-requests` với cùng lựa chọn, `mode: SPECIFIC_SLOT`, `termsId`, `termsVersion` và `Idempotency-Key` UUID. Driver Bearer, REQUEST, Account-current bất đồng bộ. | `BookingFlow.tsx` có đọc xe và chuyển bước nội bộ. **FE booking + Product/Assistant** cần bàn giao entry point, wire prefill, vòng đời theo Account, bước review/submit và lưu dữ liệu phục hồi receipt. **Operations + UserService** cần route đã tích hợp và cấu hình JWT/authorization cho môi trường test. |
| Tra trạng thái | `202` có `requestId` và `Location`. `GET /api/reservation-requests/{requestId}` dùng Driver Bearer/TOKEN và scope Account. Khi có `reservationId`, GET chi tiết own booking để đọc state, `holdExpiresAt`, `orderId`. | **Operations + Assistant** cần thống nhất own-read trước merge. Cùng `/api/me/reservations`, S06 dùng `from/to/limit/cursor` và `data.items/nextCursor/asOf`; SPARK-273 dùng `pageSize/before/beforeId` và `data` là mảng. GET chi tiết S06 dùng `data.reservation`, SPARK-273 trả trực tiếp booking view. Một merge thô có thể xung đột route và làm hỏng tra cứu S06. |
| Trạng thái thanh toán | Form chỉ chuyển Payment với `orderId` do backend trả. Receipt `PENDING_AUTHORIZATION`, `PREPARING` hay `BOOKING` vẫn đang xử lý. Điều kiện hiển thị đã đặt là backend xác nhận booking. | **Payment + Operations + FE booking** cần order gắn terms và H, checkout đúng Account và dữ liệu thanh toán có thẩm quyền. Kết quả redirect và trạng thái thanh toán được hiển thị riêng với trạng thái đặt chỗ. |

`BookingPreview` vẫn là dữ liệu nhập trong widget. Luồng owner đi từ bản nháp sang form để chọn site/slot trước khi gọi preview, rồi bổ sung xe và phương thức để review terms. Handoff FE của owner mô tả luồng form và API, còn giao diện nhận bản nháp vẫn **OPEN**. Giữ tích hợp nghiệp vụ ở trạng thái chờ theo cổng phê duyệt của SmartPark Coding Skill cho tới khi các bên thống nhất contract và route chạy được trên nhánh tích hợp.

Operations cần hợp nhất `OperationService.API/Program.cs` khi merge, giữ được search S06 và giải quyết hợp đồng own reads. Ref SPARK-273 riêng có gate `ReservationAdmission:Enabled`; khi tắt, các route reservation bị chặn với `503 RESERVATION_ADMISSION_DISABLED`. Môi trường nghiệm thu cần admission đã bật, JWKS tin cậy, CORS cho origin FE, schema/policy và broker cho Account-current. SPARK-273 có ghi `AcceptedPricingSnapshot` vào booking ở phía Operations. Đường đọc snapshot của Payment và transport thương mại vẫn cần owner bàn giao.

### Ca nghiệm thu khi owner cung cấp môi trường

| Ca | Kỳ vọng cần đối chiếu với backend |
| --- | --- |
| Terms hết hạn | Thử trước, đúng và sau `validUntil`. Từ thời điểm hết hạn, yêu cầu lấy terms mới. Backend kiểm tra hạn của terms đã review. |
| Giá/terms đổi hoặc đổi lựa chọn | Bỏ kết quả về muộn của lựa chọn trước. Hiển thị terms mới và yêu cầu khách chấp nhận lại. Source `BookingProgressHandler` trả `REJECTED / TERMS_CHANGED_OR_EXPIRED`, còn tài liệu owner ghi `TERMS_STALE`. Operations cần thống nhất mã để FE đưa khách về bước review. |
| Mất chỗ | Dùng hai Account tranh cùng tài nguyên hoặc đóng slot giữa review và submit. Kiểm `SLOT_INELIGIBLE` khi owner trả. `CAPACITY_CONFLICT` là kỳ vọng trong spec. Adapter mặc định trả `CAPACITY_COORDINATION_UNAVAILABLE` và workflow hoãn xử lý. |
| Timeout preview/terms | Giữ khả năng sửa form. Chỉ hiện terms còn hiệu lực gắn với lựa chọn hiện tại. Kết quả đến muộn cho lựa chọn cũ bị bỏ qua. |
| Timeout trước hoặc sau khi ghi admission | Dùng `data.requestId` để GET receipt khi đã biết ID. CORS trên ref chưa expose `Location` cho FE khác origin. Nếu mất toàn bộ response, GET chỉ nhận `requestId`, nên gửi lại đúng body và cùng `Idempotency-Key` dưới cùng Account để lấy receipt rồi GET theo ID đó. Đọc `data.state` kể cả replay trả `202`. Timeout truyền tải giữ trạng thái chưa rõ. `FAILED_DEPENDENCY / AUTHORIZATION_UNAVAILABLE` là kết quả của workflow khi backend trả. |
| Gửi lại, đổi payload, đổi Account | Double-click hoặc gửi đồng thời cùng key và payload chuẩn hóa phải trả cùng `requestId` và chỉ tạo một workflow. Payload khác với cùng key trả `409 IDEMPOTENCY_KEY_REUSED`. Test cả refresh và mất kết nối sau commit. Chặn tự động replay ý định cũ khi đổi Account. Khi admission bật và JWT trust hoạt động, Account khác tra receipt nhận `404`, JWT thiếu/sai nhận `401`, vai trò khác nhận `403`. |
| Timeout thanh toán và kết quả muộn | Kiểm lại trên receipt, booking và luồng Payment cùng order/attempt theo hợp đồng owner. Kiểm kết quả pending, late payment và `CONFIRMED` đã commit. `404` đơn lẻ chưa xác định hiệu ứng trước đó. |

Các ca nghiệp vụ trong bảng đều [UNVERIFIED] trên nhánh tích hợp. Có thể kiểm riêng luồng nhập bản nháp, định tuyến S06, hiển thị `null` và `0` bằng fixture của chatbot.
