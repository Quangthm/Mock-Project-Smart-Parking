# AI Chatbot — Đọc từ đây

Chatbot trả lời từ knowledge đã duyệt và gọi API trong phạm vi được cấp quyền. Luồng đặt chỗ toàn trình còn mở. Mục lục cập nhật **13/11/2026**.

## Tìm đúng tài liệu

| Bạn cần gì? | Đọc file nào? |
| --- | --- |
| Tiến độ và việc tiếp theo | [Kế hoạch phát triển](design-and-development-plan.md) |
| Gửi leader duyệt hệ thống, tiến độ và phần còn mở | [Tổng quan hệ thống và phạm vi review](system-overview-and-review.md) |
| Giao owner chốt dependency cụ thể | [Phụ thuộc và quyết định](service-dependencies-and-decisions.md) |
| Gửi tester setup, chạy test case và ghi kết quả | [Hướng dẫn setup và test](../../04-testing/assistant-handoff-testing.md) |
| Sửa nội dung FAQ | [Knowledge duy nhất](p1/knowledge/smartpark-knowledge.md), [hướng dẫn xuất bản](knowledge-and-faq.md) |
| Phát triển history/recovery | [Hội thoại và phục hồi](conversation-and-recovery.md) |
| Đọc xe/lịch đặt/tìm bãi | [Tích hợp tra cứu](business-data-integration.md) |
| Chuẩn bị draft sau khi đủ nguồn | [Điều kiện đặt chỗ](booking-prerequisites.md) |
| DTO/route/quyền/lỗi | [Contract S01](../api/assistant-s01-end-to-end-api-design.md), [ma trận API](assistant-s01-api-matrix.md) |
| Tiếp tục backend hiện tại | [Quá trình phát triển backend](../../../ai/tasks/active/chatbot-development-progress.md) |
| Tra quyết định/bằng chứng cũ | [Lịch sử thiết kế](luu-tru/design-history.md), [lịch sử triển khai](../../../ai/tasks/archive/chatbot-implementation-history.md), [bản lưu knowledge 0.3.1](luu-tru/knowledge/smartpark-knowledge-old.md) |

## Quy tắc cập nhật

- Tên file chatbot dùng tiếng Anh mô tả mục đích, không dùng tiền tố SESSION hoặc ngày; ngày và mốc công việc ghi trong nội dung. Quy ước này theo yêu cầu người dùng cho tài liệu chatbot, không đổi quy ước của các nhóm khác.
- Không tạo Markdown mới mỗi prompt. Cùng luồng backend thì cập nhật nhật ký đang làm; checkpoint ngắn ghi thay đổi, kiểm chứng và bước tiếp theo.
- Kế hoạch chứa ưu tiên/phạm vi. Kết quả hiện hành thuộc hướng dẫn bàn giao; nhật ký giữ bằng chứng chi tiết. Contract không chép lại vào roadmap.
- Chỉ tạo file khi có mục đích độc lập chưa được tài liệu hiện có phụ trách; đặt tên theo nội dung, không thêm master/final/v2/new hoặc thư mục session chỉ để ghi tiến độ.
- Lịch sử có ngày/nhãn rõ; trạng thái cũ và test cũ không tự là bằng chứng hiện tại.
- Giữ đường dẫn runtime `p1/knowledge/smartpark-knowledge.md`. Không xuất bản draft hoặc đổi manifest khi dọn docs.
- Frontend có owner riêng; backend bàn giao contract/finding, không tự sửa UI.

## Tài liệu chuyên biệt vẫn giữ

- [Availability authority](../api/assistant-s06-availability-authority.md), [read integration](../api/assistant-s06-read-integration.md), [search owner proposal](../api/assistant-s06-search-owner-proposal.md): boundary/evidence; proposal không là approval.
- [P0 auth contract](../api/iot-assistant-p0-api-design.md): kế thừa, đọc cùng amendment S01 và ADR hiện hành.
- [Demo/scaffold](chatbot-scaffold.md): công cụ phát triển riêng, không phải entrypoint chạy thật.
- LPR/AI khác vẫn theo SRS/service boundaries; Future/Deferred không tự trở thành MVP chatbot.
