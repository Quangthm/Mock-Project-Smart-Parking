# SmartParking Session Tracking & Review Skill

**Version:** 1.0  
**Project:** SmartParking  
**Scope:** AI Agent Session Logging, State Preservation & Task Progress Tracking  
**Status:** Team convention  

---

## 1. Mục đích và Tác dụng của File Session

File Session ghi lại toàn bộ nhật ký và bối cảnh kỹ thuật trong quá trình thực hiện một task chính (thường gắn với một Jira Task `SPARK-<number>`).

### Tác dụng cốt lõi:
1. **Bảo toàn bối cảnh (Context Continuity):** Khi AI bị crash, hết context window, ngắt kết nối mạng hoặc đổi ca làm việc/đổi máy, agent tiếp theo có thể nắm bắt toàn bộ trạng thái trong < 5 phút mà không phải đọc lại toàn bộ mã nguồn.
2. **Theo dõi quyết định kỹ thuật (Technical Provenance):** Ghi nhận tại sao một giải pháp được chọn, các phương án thay thế bị từ chối và phạm vi ảnh hưởng.
3. **Phục vụ bàn giao & Review (Handoff & Audit):** Người review (mentor/lead) và lập trình viên khác có thể kiểm chứng chính xác các lệnh verify đã chạy, file đã chỉnh sửa và kết quả thực tế.

---

## 2. Vị trí lưu trữ & Quy tắc Phân loại

Mọi file session phục vụ cho task được lưu trữ tại:

```text
ai/tasks/
├── active/        # Chứa session của các task ĐANG THỰC HIỆN (In Progress)
└── completed/     # Chứa session của các task ĐÃ HOÀN THÀNH và được nghiệm thu
```

- **Khi bắt đầu làm task:** Tạo file session mới trong thư mục `ai/tasks/active/`.
  - Quy ước đặt tên file: `TASK-<JiraKey>-SESSION-YYYYMMDD.md` hoặc `SESSION-YYYYMMDD-<topic>.md`.
  - Ví dụ: `ai/tasks/active/SPARK-262-SESSION-20261021.md`.
- **Trong quá trình làm:** Cập nhật tiến độ, file thay đổi, blocker và lệnh test vào file này.
- **Khi task hoàn thành (DoD được thỏa mãn):** Di chuyển file từ `ai/tasks/active/` sang `ai/tasks/completed/`.

---

## 3. Template Session Chuẩn

Bắt buộc sử dụng cấu trúc markdown sau cho mỗi file session:

```markdown
# Session Review — YYYY-MM-DD

## 1. Session Information
- **Session ID:** SESSION-YYYYMMDD-001
- **Date:** YYYY-MM-DD
- **Agent:** Main Agent / Sub-Agent
- **Branch:** feature/your-branch
- **Related Task:** [Task ID or description]
- **Status:** In Progress / Completed / Blocked

## 2. Objectives
- [ ] Objective 1
- [ ] Objective 2

## 3. Work Completed
- [x] Task or change completed
- [x] Files created or modified
- [x] Tests or validations performed

## 4. Technical Decisions
- **Decision:** What was decided?
- **Reason:** Why was this approach selected?
- **Alternatives:** Other approaches considered
- **Impact:** Affected components or services

## 5. Files Changed
| File | Change | Reason |
|---|---|---|
| path/to/file | Modified | Explain why |
| path/to/new-file | Created | Explain why |

## 6. Issues and Blockers
- **Issue:** Description
- **Root Cause:** Known cause or Unknown
- **Resolution:** Fix applied or Pending
- **Remaining Risk:** Any unresolved concern

## 7. Validation Results
- **Build:** Pass / Fail / Not Run
- **Tests:** Pass / Fail / Not Run
- **Lint/Analysis:** Pass / Fail / Not Run
- **Manual Verification:** Results

## 8. Git Status
- **Branch:** feature/your-branch
- **Commit:** Commit hash or Not Committed
- **Working Tree:** Clean / Modified / Untracked
- **Uncommitted Changes:** List relevant changes

## 9. Handoff / Next Steps
- [ ] Next task
- [ ] Remaining issue
- [ ] Required information or dependency

## 10. Context for Next Session
- Current implementation state
- Important constraints and conventions
- Files or documentation to read first
- Commands required to reproduce or verify
- Known risks and assumptions
```
