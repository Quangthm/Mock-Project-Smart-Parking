# AI Chatbot — Phụ thuộc dịch vụ và quyết định cần chốt

Cập nhật08/11/2026. **OPEN/PROPOSED không phải phê duyệt.** Không quy trách nhiệm cá nhân từ checkout. [Kế hoạch](design-and-development-plan.md) · [chi tiết đặt chỗ](booking-prerequisites.md).

## 1. API đã có source

Vehicles `/api/vehicles` (User), own reservations/list/detail và `/api/parking-search` (Operations), public sites (Facility), discovery prices (Payment) đã có code. Còn nghiệm thu trust/HTTPS/publication/site–tenant binding/dữ liệu thật; không báo tất cả là “owner chưa làm”.

## 2. Phiếu leader/owner

| ID | Owner | Cần cung cấp/chốt | Chặn phần nào |
| --- | --- | --- | --- |
| A01 | Operations + Facility | Inventory đầy đủ, slot/pool/category mapping/version/quota và xe chưa gán slot | Positive availability |
| A02 | Operations | Hiệu lực reservation/allocation/session, correlation, release/expiry, buffer | Capacity validation |
| A03 | Operations + thiết bị/IoT | Producer xác nhận vào/ra vật lý, provenance/scope/dedup/ordering/coverage | Occupancy tin cậy |
| A04 | Facility + Operations | Lịch/ngoại lệ/restriction/compatibility và revision | Rảnh suốt khung giờ |
| A05 | Operations + Product | Effective policy, horizon/buffer, age/skew từng nguồn | Đúng policy |
| A06 | Operations + writers | Competing writers, watermark, site–tenant trust và concurrency evidence | Tính nhất quán |
| G04 | Operations + Payment | Quote Account/vehicle/site/interval/terms/expiry, quyền và revalidation | S07 draft |
| G05 | Operations + Payment | Immutable accepted snapshot, producer/writer, SERVICE trust và lỗi | Giá đã xác nhận |
| G06 | Operations + User | Driver create, REQUEST admission, authorization/fencing, idempotency/result lookup | S08 đơn thật |
| G07 | Operations + Payment + FE | Route/DTO order, authoritative amount/ownership, replay/reconciliation/linkage | Booking → payment |
| G08 | User + FE | `/hubs/access`/`AccessRevoked` đối chiếu server thực | Revocation realtime; không chặn FAQ |

Tất cả dòng trên còn OPEN trong phạm vi nêu. Owner điền người phụ trách, phạm vi pilot, contract/approval link, evidence và mốc tích hợp. Chọn hướng không làm xuất hiện producer/API.

Production search dùng `UnsupportedDiscoveryCapacity`; thiếu nguồn giữ count null. Calculator/discovery estimate không tự là booking quote. Consumer snapshot không chứng minh provider tồn tại. Operations gọi orders/prepare, Payment có orders với DTO khác; cần thống nhất contract, không chỉ URL.

## 3. Quyết định chatbot nếu mở rộng

- S04: lexical hay semantic/multiple-source generation? Approval Guest/Gemini không tự mở Driver/OpenAI; chốt dữ liệu/provider/chi phí/eval/token trước đổi boundary.
- S07: draft schema/TTL/version, tham chiếu nhiều lượt, invalidation khi đổi xe/giờ/bãi, NLU/privacy. Chat chỉ prefill, khách xác nhận form, owner thực hiện.
- S09: workload/SLO/budget; độ trễ tắt/revoke nếu đổi polling/cache. HTTP pooling không đổi business authority.
- S10: corpus approval, deployment, SQL/backup/keyring, live acceptance, release/Git gates. Không ghi secrets.

## 4. Chi tiết và lịch sử

- [Điều kiện đặt chỗ](booking-prerequisites.md): A01–A06/G04/G05, edge cases/checklist.
- [Availability authority](../api/assistant-s06-availability-authority.md), [API matrix](assistant-s01-api-matrix.md), [contract S01](../api/assistant-s01-end-to-end-api-design.md).
- [Master cũ đầy đủ](luu-tru/design-history.md#tai-lieu-5), [phiếu S06 cũ](luu-tru/design-history.md#tai-lieu-6) giữ tradeoffs/questions, trạng thái06/11 là lịch sử.
