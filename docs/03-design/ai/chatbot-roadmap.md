# AI Chatbot — Tiến độ và việc còn mở

Cập nhật **17/11/2026**. Tài liệu này quản lý phạm vi bàn giao, thứ tự công việc và dependency. Thiết kế nằm ở [chatbot-design.md](chatbot-design.md), lệnh setup và expected result nằm ở [hướng dẫn tester](../../04-testing/assistant-handoff-testing.md), bằng chứng theo từng phiên nằm trong [nhật ký phát triển](../../../ai/tasks/active/chatbot-development-progress.md).

## Trạng thái bàn giao

Người dùng đã báo setup lại database, Gemini Test connection PASS và widget hoạt động trên máy local ngày 17/11. Máy tester vẫn cần nghiệm thu độc lập với commit, tài khoản, provider/model và corpus của môi trường mình.

| Phạm vi | Đã có | Việc còn cần xác minh hoặc phát triển |
| --- | --- | --- |
| Quản trị provider | Cấu hình theo key/model, priority, encrypted credential, rotate/revoke, revision và Test connection | Nghiệm thu từng provider thật, lỗi/quota, lưu cấu hình sau restart và giải mã bằng keyring gốc |
| FAQ công khai | Knowledge có manifest, retrieval lexical/typo giới hạn, câu trả lời có nguồn, fallback tài liệu | Sửa câu không nhận diện hoặc chọn sai bài, đo chất lượng trên tập giữ riêng và model thật |
| Hội thoại đăng nhập | Lịch sử theo Account/role, revision, replay, cancel/recovery và retention | Nghiệm thu A/B bằng UserService thật, mất ACK commit, deadline dưới lock và các interleaving khi rotate/revoke |
| Tra cứu Driver | Adapter đọc xe, lịch đặt và tìm bãi theo cấu hình/readiness | Kiểm HTTPS/trust, publication, site–tenant binding, dữ liệu thật và owner failure trên máy QA |
| Bản nháp đặt chỗ | Parser phía FE cho các mẫu câu được hỗ trợ | Chưa có luồng draft nhiều lượt → quote → form xác nhận → tạo booking/thanh toán hoàn chỉnh |
| Setup và vận hành | Docker/host runners, helper JWT/TLS/knowledge hỗ trợ Windows PowerShell 5.1, fixture DB và backup/restore | Setup máy nhận, SLO/tải/chi phí, rollout/rollback và diễn tập khôi phục đúng DB + keyring + PFX |

Operations hiện dùng `UnsupportedDiscoveryCapacity`. Khi thiếu nguồn, số chỗ giữ `null`/unknown dù site có slot. Discovery price là estimate; booking quote và giá được khách chấp nhận còn cần contract riêng.

## Thứ tự công việc

| Ưu tiên | Việc thực hiện | Owner phối hợp | Điều kiện hoàn tất |
| --- | --- | --- | --- |
| 1 | Setup máy tester và chạy Settings/FAQ/history | QA + IoT + Admin, User owner hỗ trợ JWT | Ghi PASS/FAIL/BLOCKED từng ca áp dụng, có commit/corpus/model và môi trường. Test connection và FAQ generation được ghi riêng |
| 2 | Sửa chất lượng FAQ đã phát hiện | IoT + QA + người duyệt nội dung | Đúng bài, đúng câu hỏi, đúng citation và từ chối an toàn trên tập đánh giá giữ riêng |
| 3 | Nghiệm thu owner reads | IoT + User/Operations/Facility/Payment + QA | Capabilities khớp cấu hình, kết quả khớp API thật, lỗi nguồn/unknown được hiển thị đúng |
| 4 | Đóng khoảng kiểm chứng concurrency và vận hành | IoT + QA kỹ thuật + DevOps | Có ca cưỡng bức lost ACK, lock/deadline, outage/backlog, rotate/revoke và khôi phục DB/credential |
| 5 | Chốt dependency rồi triển khai booking | Leader/Product + owner nghiệp vụ + IoT/FE | A01–A06 và G04–G07 có contract/producer/evidence; khách review và xác nhận trước mọi thao tác ghi |
| 6 | Release theo phạm vi được duyệt | Leader + DevOps + QA | Có workload/SLO/budget, privacy/provider terms, rollout/rollback và biên bản nghiệm thu môi trường đích |

FAQ hiện còn các câu `unknown` và chọn sai nguồn, ví dụ đăng ký tài khoản bị hiểu thành đăng ký chủ bãi, cập nhật bãi bị chọn thành tìm bãi. Danh sách câu và expected cụ thể thuộc **mục 4.6–4.7** của hướng dẫn tester. Safe refusal có thể đạt kiểm an toàn nhưng nhu cầu nghiệp vụ chưa hỗ trợ vẫn ghi GAP.

## Dependency và quyết định của owner

Các API xe, lịch đặt của bản thân, catalogue và discovery pricing đã có source. Bảng dưới ghi những phần còn OPEN trong phạm vi khảo sát. Owner xác nhận bằng contract, branch/commit và test evidence của mình.

| ID | Owner theo dịch vụ | Cần cung cấp hoặc chốt | Điều kiện gỡ chặn |
| --- | --- | --- | --- |
| A01 | Operations + Facility | Inventory đầy đủ, mapping slot/pool/category/version/quota và xe chưa gán slot | Producer và coverage/mapping được duyệt, fixture chống thiếu/đếm trùng |
| A02 | Operations | Hiệu lực reservation/allocation/session, correlation, release/expiry và buffer | State/effect table, khóa correlation và evidence cập nhật của writers |
| A03 | Operations + thiết bị/IoT | Xác nhận vào/ra vật lý, provenance, scope, dedup, ordering và coverage | Producer xác thực, kiểm replay/outage/site binding |
| A04 | Facility + Operations | Lịch, ngoại lệ, restriction, compatibility và revision | API/version/hiệu lực cùng các ca hợp lệ suốt interval |
| A05 | Operations + Product | Effective policy, horizon/buffer, age/skew theo từng nguồn | Policy/version/clock/threshold được duyệt và có fixture |
| A06 | Operations + các writer | Competing writers, watermark, site–tenant trust và concurrency | Protocol writer và kiểm tranh chấp chứng minh snapshot nhất quán |
| G04 | Operations + Payment | Booking quote gắn Account/vehicle/site/interval/terms/expiry | Route/DTO, quyền, TTL, binding, validation/refresh được duyệt và có provider trả dữ liệu |
| G05 | Operations + Payment | Accepted pricing snapshot bất biến, producer/writer và SERVICE trust | Lifecycle persist/resolve, lỗi missing/corrupt, binding và test đạt |
| G06 | Operations + User | Driver create, REQUEST admission, authorization/fencing, idempotency/result lookup | API ghi, current-access flow và retry/unknown outcome được nghiệm thu |
| G07 | Operations + Payment + FE | Booking/order/payment linkage, route/DTO, amount/ownership và reconciliation | Producer/consumer khớp, success/timeout/replay không tạo order trùng hoặc mất liên kết |
| G08 | User + FE | `/hubs/access`/`AccessRevoked` và target connection/role | Đối chiếu counterpart server thật trước khi nhận revocation realtime UX đã hoàn tất |

G08 bổ sung UX thu hồi quyền. FAQ và HTTP recovery vẫn tuân policy JWT hiện hành. Việc chọn một hướng thiết kế cần đi cùng producer/API và evidence tương ứng.

Checkout staff/gate và Driver booking có mục đích nghiệp vụ riêng. Cần đối chiếu route/DTO/quyền/amount/snapshot theo contract booking trước khi nối chatbot, kể cả khi một route Payment có tên gần giống đã tồn tại. Tham chiếu [read integration](../api/assistant-s06-read-integration.md), [availability authority](../api/assistant-s06-availability-authority.md) và [booking/search proposal](../api/assistant-s06-search-owner-proposal.md).

## Quyết định trước khi mở rộng

| Chủ đề | Nội dung cần chốt |
| --- | --- |
| Retrieval/NLU | Đo baseline lexical, câu có/không dấu, typo, đồng nghĩa, nhiều ý, câu riêng tư/live và ngoài nguồn. Chỉ cân nhắc semantic hoặc multiple-source generation sau khi đo sai bài, từ chối nhầm, citation và latency |
| Dữ liệu gửi provider | Chốt provider, dữ liệu, privacy, egress, token và chi phí. Approval Guest/Gemini cũ không tự mở raw text/history Driver hoặc provider khác |
| Draft nhiều lượt | Schema/TTL/version, tham chiếu xe/giờ/bãi, invalidation, validation và form khách xác nhận. Chủ thể nghiệp vụ thực hiện thao tác ghi |
| Diagnostics | Phân biệt lỗi key/quyền/model/outage nếu mở rộng `PROVIDER_REJECTED`; thống nhất public error contract với FE và giữ raw upstream body khỏi response |
| Hiệu năng | Workload/SLO/budget và độ trễ tắt/revoke khi đổi polling/cache. Đo DB/TLS/provider thật trước khi nhận đạt vận hành |
| Triển khai | Corpus approval, quyền áp SQL, backup/restore DB + keyring + PFX, retention/deletion SLA, rollout/rollback và provider terms |

## Phiếu bàn giao cho leader và QA

Điền khi quyết định tích hợp hoặc release. Lịch sử proposal không thay cho phê duyệt mới của môi trường đích.

| Thông tin | Giá trị cần ghi |
| --- | --- |
| Candidate | PR, commit và phạm vi được duyệt |
| Môi trường | Máy/stack, người setup, owner JWT/TLS và dữ liệu test |
| Corpus | Version, digest và bản ghi Admin duyệt |
| Provider | Configuration/model, quota/budget và kết quả từng lần kiểm |
| QA | Người chạy, case ID, PASS/FAIL/BLOCKED và bằng chứng đã che secret |
| Dependency | Người phụ trách từng A/G, contract/evidence và mốc tích hợp do leader xác nhận |
| Release | Rủi ro chấp nhận, ticket/owner, quyết định merge/release và kế hoạch rollback |

Ngày hoàn thành và người phụ trách cụ thể do nhóm xác nhận. Nhật ký giữ số liệu test và bằng chứng lịch sử, hướng dẫn tester giữ ca nghiệm thu, còn roadmap này cập nhật trạng thái và việc tiếp theo.
