# SPARK-177 — PR/Jira draft (KHÔNG PHẢI PR ĐÃ TẠO)

**Branch hiện có:** `test/SPARK-177-login-logout-qa` (dựa trên Fix2FA; kiểm tra branch base/target theo team).

**Commit title (chờ quy định team):** `SPARK-177 docs: add login logout QA audit and evidence`

**PR title:** `[SPARK-177] qa: add login logout QA audit and evidence`

## PR description

Jira: https://mangamanagement.atlassian.net/browse/SPARK-177

QA deliverables for reviewer feedback. This PR documents historical demo/Fix2FA evidence separately from current execution, a traceability workbook, unit-test execution summary (73 passed, 26 skipped), unresolved MFA HTTP 500 observation, PostgreSQL schema fixture blocker and UAT/Newman gaps.

**Status:** QA progress / review requested; **NOT READY FOR DONE**.

**To attach / validate before final PR:** current Git HEAD & target branch; sanitized TRX; Postman/Newman output; current FE UAT reviewer; defect links and retests. Do not commit secrets, .env files or unreviewed FE/.env.example deletion.

## Jira comment (draft)

Đã tổng hợp báo cáo QA cũ và kết quả chạy trên Fix2FA cho SPARK-177, kèm ma trận traceability theo hướng dẫn SPARK-201 đang chờ nhóm phê duyệt áp dụng. UserService.Tests: 73 PASS / 0 FAIL / 26 SKIPPED (TRX kèm theo khi PR). API smoke negative: 5 PASS; login có MFA tạo challenge thành công; xác minh MFA quan sát HTTP 500 và đang chờ triage. Các phần refresh/session security, Newman current run, FE UAT/reviewer và integration fixture vẫn chưa đủ bằng chứng. Đề nghị review tài liệu, xác nhận baseline schema/auth, hỗ trợ điều tra MFA và thống nhất điều kiện nghiệm thu. Đây là báo cáo tiến độ; chưa đề xuất chuyển Done.

Jira ↔ GitHub PR link chỉ hoạt động nếu GitHub for Jira đã tích hợp repo. PR title không tự động đổi Assignee hoặc Done.
