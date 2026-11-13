# AI Chatbot — Kế hoạch phát triển backend

Cập nhật **08/11/2026**. Backend là phạm vi hiện tại; frontend có owner riêng. Đây là kế hoạch hiện hành duy nhất. [Mục lục](README.md) · [nhật ký đang làm](../../../ai/tasks/active/chatbot-development-progress.md).

## 1. Mục tiêu và trạng thái

Đích sản phẩm: FAQ từ knowledge; Driver xem xe/lịch đặt; chuẩn bị và sửa draft qua hội thoại, đưa form để khách xác nhận; backend nghiệp vụ kiểm tra và thực hiện. AI không tự giữ chỗ, tạo thanh toán hoặc quyết định quyền/giá.

Người dùng xác nhận đã chạy thật. Nhật ký 08/11 có Gemini FAQ thật trên bản knowledge runtime được duyệt. Source Markdown còn draft; không suy mọi bản sửa corpus hoặc mọi provider/luồng Driver đã nghiệm thu. HEAD local `3d3def76` vẫn là scaffold; implementation mới ở working tree chưa commit.

| Mốc | Đã có | Việc tiếp theo |
| --- | --- | --- |
| S01 | Ma trận API/ownership/contracts | Chốt owner wire còn mở |
| S02–S03 | Settings, model theo key, discovery, priority, test connection, routing | Kiểm chứng vận hành; không xây lại Settings |
| S04 | Markdown nguồn, truy xuất từ khóa và tập đánh giá Việt/Anh, sửa lỗi gõ giới hạn | Duyệt publication và đối chiếu chất lượng câu trả lời provider trong môi trường đích |
| S05 | Chat theo Account, history, ledger, cancel/recovery, xử lý FAQ thống nhất. Test PostgreSQL cách ly đã qua kill giữa request và retention | Kiểm với UserService thật, deadline dưới lock và mất ACK |
| S06 | Owner read adapters và search | Thiếu positive availability authority |
| S07 | Contract/checklist | Chưa có draft; chờ G04/G05 và validation |
| S08 | Read UI/transport, fixture browser evidence | FE owner phụ trách UI |
| S09 | HTTP pool, tái sử dụng kết quả parse knowledge, histogram theo bước và số đo fixture | Đo tải và độ trễ trên môi trường đích |
| S10 | Script PostgreSQL dùng một lần, smoke test Settings/FAQ/Driver, chạy lại migration, backup/restore DB và đọc lại keyring | Chạy checklist bàn giao trên môi trường đích với tài khoản và provider test |

[Ma trận nghiệm thu](../../04-testing/assistant-handoff-testing.md) sở hữu kết quả hiện hành; số test lịch sử không tự trở thành PASS mới.

## 2. S02–S03: không xây lại phần đã có

Review source độc lập chưa tìm thấy P1/P2 đủ chắc chắn trong quản trị. Đã có per-key model, grouping/priority, encrypted secrets, rotate/revoke, revision, mutation receipt, discovery cache/cursor và durable connection-test receipt.

Bằng chứng Settings/rotate/revoke/keyring và HTTP Test connection đã được bổ sung trên PostgreSQL cách ly. Việc còn mở:

1. Safe diagnostics: `ProviderDiagnostics` gộp upstream401/403/404/5xx thành `PROVIDER_REJECTED`. Phân biệt key/quyền/model/outage giúp Admin xử lý; mã lỗi mới phải thống nhất với FE, không trả raw upstream body.
2. Nghiệm thu riêng từng provider thật và đối chiếu nội dung/citation. Harness cách ly kiểm riêng Test connection và FAQ bằng hai adapter giả lập.
3. Kiểm chứng replay khi mất ACK commit, rotate/revoke khi request đang chạy và backup/restore DB cùng keyring của môi trường đích.

## 3. S04: cải thiện hiểu câu hỏi trước khi cân nhắc train

`GuestIntents` chặn một số lớp câu trước retrieval. `PublicFaqRetrieval` ưu tiên khớp từ/cụm rồi thử phục hồi một lỗi gõ trong alias ngắn nếu duy nhất và đủ điều kiện. Khi không tìm được bài, handler hỏi lại tại chỗ. Driver gửi tiêu đề của backend hoặc câu hỏi cố định về đỗ quá giờ. Chi tiết và tập kiểm thử ở [knowledge và FAQ](knowledge-and-faq.md).

Đề xuất theo thứ tự, **chưa duyệt thay kiến trúc/egress**:

- Lập tập đánh giá tách khỏi câu dùng tinh chỉnh: có/không dấu, lỗi chính tả, đồng nghĩa, câu dài/nhiều ý, ngoài nguồn, private/live data. Đo đúng bài, từ chối nhầm, sai nguồn, citation và latency.
- Cải thiện chuẩn hóa/truy xuất public, giữ đúng ý hỏi; không chỉ thêm vô hạn câu vào if/else. Đo lexical baseline trước khi quyết định semantic retrieval.
- Nếu lexical không đủ: thử retrieval ngữ nghĩa hoặc cho model chọn/đáp từ tập đoạn public giới hạn. Corpus nhỏ có thể thử một lượt generation từ nhiều đoạn thay vì classifier network rồi generation. Chốt đoạn/token, nguồn, privacy và cách từ chối trước.
- AI được hiểu cách diễn đạt, không tự tạo chính sách/giá/chỗ trống. Hỏi lại khi thực sự mơ hồ; thiếu một phần thì nói rõ phần chưa có nguồn.
- Không mở raw text/history Driver sang provider từ approval Guest/Gemini cũ. Không tự thêm vector DB, embedding service, model/package.

Fine-tuning không sửa việc backend từ chối trước khi gọi model. [Hướng dẫn chính thức về tối ưu độ chính xác](https://developers.openai.com/api/docs/guides/optimizing-llm-accuracy) phân biệt retrieval/context với cải thiện hành vi model; cần đo đúng nguyên nhân trước.

## 4. S05: kết quả sửa backend

- Guest và Driver dùng chung nhánh hỏi lại về thời gian, đến trễ và đỗ quá giờ. Trường hợp đỗ quá giờ trích mục hướng dẫn thanh toán.
- Fallback Driver hướng về FAQ khi câu hỏi ngoài phạm vi. Trạng thái tìm bãi nằm ở capabilities và nhánh tìm bãi.
- Test PostgreSQL cách ly kiểm replay, cạnh tranh cancel/finish, phục hồi sau kill tiến trình và retention. Deadline dưới lock và mất ACK còn mở.
- Context hiện chỉ continuation hẹp tối đa6 lượt/12k ký tự; không phải booking draft hay trí nhớ tổng quát.

## 5. S08: ranh giới frontend

Không xác nhận lỗi UI mới và không sửa FE trong lượt này. Hai finding S05 thuộc backend. Backend bàn giao đúng capabilities/cards/actions/errors/recovery; đổi route/DTO/error enum cần ghi chú cho FE owner.

## 6. S09: tối ưu có căn cứ

1. Guest và managed Demo dùng chung named HTTP client với handler được tái sử dụng. Credential gắn từng request. Cookie, redirect và logging mặc định của client đều tắt.
2. `PublicKnowledgeFile` tái sử dụng kết quả parse theo SHA-256. Mỗi lần đọc vẫn kiểm byte, manifest và thời hạn. Poll giữ nhịp 100 ms.
3. Histogram đo request, đọc knowledge, lần gọi provider, truy xuất FAQ, readiness, admission, failover và finish. Xem [bằng chứng S09](../../04-testing/assistant-handoff-testing.md).

## 7. Thứ tự và gates

- Ưu tiên: sửa S05 → tập eval FAQ → quyết định S04 → HTTP pool/metrics → nghiệm thu thật. Mỗi fix có regression và independent review/rereview.
- Contract/architecture/privacy mới đi qua approval gate. Việc không phụ thuộc quyết định mới tiếp tục độc lập.
- Không thay service khác, DB/schema/personal config/start-stop runtime/call provider có phí ngoài quyền đã có. SQL cần review/backup/restore/quyền riêng; không reset dữ liệu.
- User tự test đạt trước candidate sạch; commit/push cần duyệt riêng và Jira key thật. Dọn docs không sửa index.
- Cập nhật một nhật ký hiện tại; [kế hoạch/prompt gốc](luu-tru/design-history.md#tai-lieu-4) giữ làm lịch sử, không phải kế hoạch hiện hành thứ hai.
