# AIChatbot

Tài liệu cập nhật ngày **17/11/2026**.

| Cần làm gì? | Đọc ở đâu? |
| --- | --- |
| Hiểu kiến trúc, quyền, provider, knowledge và hội thoại | [Thiết kế](chatbot-design.md) |
| Xem đã làm, việc còn thiếu và dependency từng owner | [Lộ trình](chatbot-roadmap.md) |
| Setup máy, tạo dữ liệu và chạy câu hỏi theo role | [Hướng dẫn tester](../../04-testing/assistant-handoff-testing.md) |
| Sửa nội dung FAQ | [Knowledge nguồn](p1/knowledge/smartpark-knowledge.md) và [quy trình xuất bản](chatbot-design.md#knowledge-and-faq) |
| Tìm route, DTO, quyền và mã lỗi | [API contract](../api/assistant-s01-end-to-end-api-design.md) |
| Tra quyết định và thiết kế cũ | [Lịch sử thiết kế](archive/design-history.md) |
| Tra quá trình triển khai và kết quả từng lần kiểm thử | [Nhật ký phát triển](../../../ai/tasks/active/chatbot-development-progress.md) |

## Cập nhật tài liệu

- Thiết kế mô tả cơ chế và ranh giới hiện hành. Lộ trình ghi trạng thái, owner và tiêu chí hoàn tất.
- Tester ghi kết quả tại hướng dẫn bàn giao. Nhật ký giữ mốc thời gian, quyết định và bằng chứng từng phiên.
- Cập nhật nội dung tại đúng tài liệu phụ trách, dùng liên kết khi cần dẫn lại.
- Knowledge nguồn đang được biên tập. Bản runtime cần Markdown và manifest được duyệt, khớp hash và thời hạn.
- Thay đổi quyền, dữ liệu gửi provider, kiến trúc hoặc contract cần theo quy trình quyết định của dự án.
