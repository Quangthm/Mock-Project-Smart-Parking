# rule.md — Quy tắc vận hành Agent & Hệ thống 5 Skills cốt lõi

**Mục đích:** Quy chuẩn hóa toàn bộ kỹ năng thực thi (Execution Skills), điểm kiểm soát checkpoint và quy trình quản lý tiến độ cho AI Coding Agent trên repository SmartPark. Đảm bảo tính nhất quán về commit, pull request, ghi nhật ký session, cấu hình môi trường và kiến trúc code.

---

## Danh mục 5 Core Skills bắt buộc tuân thủ

| Quy tắc | Skill | Tài liệu đặc tả / Hướng dẫn chi tiết |
|---|---|---|
| **Rule 1** | **Commit Skill** | [`docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md) |
| **Rule 2** | **Pull Request Skill** | [`docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md) |
| **Rule 3** | **Session Skill** | [`docs/07-skills/SmartParking_Session_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Session_SKILL_v1.0.md) |
| **Rule 4** | **.env Config Skill** | [`docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md) |
| **Rule 5** | **Coding Skill** | [`ai/architecture-reference.md`](architecture-reference.md) |

---

## Chi tiết từng Rule

### Rule 1. Commit Skill (Quy chuẩn Git Commit)
- **Tài liệu tham chiếu:** [`docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Git_Conventional_Commits_Jira_SKILL_v1.0.md).
- **Yêu cầu cốt lõi:**
  1. Áp dụng chuẩn **Conventional Commits 1.0.0** kết hợp Jira Issue Key:
     `<type>(<scope>): <description> [SPARK-<number>]`
     *Ví dụ:* `feat(reservation): implement slot conflict checks [SPARK-81]`
  2. Mỗi commit phải đại diện cho **1 thay đổi logic duy nhất** (One logical change per commit). Tách biệt giữa commit tính năng (`feat`), sửa lỗi (`fix`), tài liệu (`docs`) và kiểm thử (`test`).
  3. Tuyệt đối không commit các file nhạy cảm, secret, thông tin credentials hoặc generated code tạm thời.

---

### Rule 2. Pull Request Skill (Quy chuẩn tạo Pull Request)
- **Tài liệu tham chiếu:** [`docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Pull_Request_SKILL_v1.0.md).
- **Yêu cầu cốt lõi:**
  1. **Tiêu đề PR:** Tuân thủ format `<type>(<scope>): <description> [SPARK-<number>]`.
  2. **Target Branch:** Mặc định trỏ về `develop` trong chu kỳ phát triển.
  3. **PR Description:** Bắt buộc áp dụng template tiêu chuẩn sau:

```markdown
## Summary
<!-- Briefly explain the purpose of this PR. -->

## Related Issue
- Closes #<issue-number>

## Changes
- [ ] Added ...
- [ ] Updated ...
- [ ] Removed ...

## Implementation Details
<!-- Explain important technical decisions or architectural changes. -->

## Testing
- [ ] Build succeeds
- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] Manual testing completed (if applicable)

**Test results:**
<!-- Describe the commands executed and their results. -->

## Screenshots / Evidence
<!-- Attach screenshots, logs, or API responses if relevant. -->

## Breaking Changes
- [ ] No breaking changes
- [ ] Breaking changes described below

<!-- Describe any breaking changes, configuration changes, or migration requirements. -->

## Reviewer Notes
<!-- Highlight files, logic, or risks that need special attention. -->
```

---

### Rule 3. Session Skill (Quản lý tiến độ task & Khôi phục sự cố)
- **Tài liệu tham chiếu:** [`docs/07-skills/SmartParking_Session_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Session_SKILL_v1.0.md).
- **Tác dụng của file Session:**
  - Lưu trữ toàn diện thông tin làm việc của 1 task chính (thường gắn với một task Jira `SPARK-<number>`).
  - Đảm bảo tính liên tục (Context Continuity): khi agent crash, reset context window hoặc đổi máy, phiên tiếp theo resume được ngay lập tức trong < 5 phút.
  - Ghi nhận quyết định kỹ thuật, file đã chỉnh sửa, lệnh kiểm chứng và rủi ro còn tồn đọng.
- **Vị trí lưu trữ & Phân loại:**
  - File session được tạo và quản lý tại thư mục: `ai/tasks/`.
  - **`ai/tasks/active/`**: Chứa file session của task đang trong quá trình thực hiện (`In Progress`).
  - **`ai/tasks/completed/`**: Khi task hoàn thành và kiểm thử pass, di chuyển file session sang thư mục này.
  - Đặt tên file: `TASK-<JiraKey>-SESSION-YYYYMMDD.md` (hoặc `SESSION-YYYYMMDD-<topic>.md`).

#### Checkpoint Triggers & Crash Recovery Protocol

Trong quá trình thực hiện task, Agent bắt buộc phải cập nhật session snapshot tại **4 Checkpoints cốt lõi**:

1. **Checkpoint 1 — Session Initialization and Reconciliation (Khởi tạo & Đối soát):**
   - Kiểm tra `git status --short --branch` và xác định task mới hay tiếp tục task cũ.
   - Nếu có session đang active, đọc mục tiêu, tiến độ, blockers và handoff notes.
   - So sánh trạng thái file thực tế với session log để đối soát thận trọng.
   - **Cấm tự ý xóa/revert:** Không được tự ý revert hay nhận vơ thay đổi chưa giải thích trong working tree (có thể là thay đổi của user).
   - Nếu bắt đầu task phức tạp mới: tạo file session trong `ai/tasks/active/`.

2. **Checkpoint 2 — Milestone Completion and Progress Recording (Ghi nhận mốc hoàn thành):**
   - Sau khi hoàn thành một khối logic rõ ràng, cập nhật công việc đã làm, danh sách file thay đổi, quyết định kỹ thuật.
   - Ghi nhận lệnh kiểm thử đã chạy kèm kết quả thực tế.
   - **Không đợi test pass mới ghi:** Nếu build/test lỗi, vẫn ghi rõ lỗi, lệnh đã chạy và rủi ro còn lại.
   - Phân định rõ ràng: *Implemented & Validated*, *Implemented but not validated*, *Partially implemented*, *Blocked*, *Proposed*. Đánh dấu `[UNVERIFIED]` cho phần chưa xác thực.

3. **Checkpoint 3 — Blocker, Direction Change, or Risk (Điểm nghẽn, đổi hướng hoặc rủi ro):**
   - Cập nhật session ngay khi:
     - Thử sửa cùng một lỗi quá 2 lần liên tiếp mà vẫn thất bại.
     - User thay đổi yêu cầu hoặc phạm vi task.
     - Xuất hiện quyết định kiến trúc quan trọng.
     - Phát hiện rủi ro bảo mật, toàn vẹn dữ liệu, hoặc sắp chạy thao tác có rủi ro cao.
     - Phát hiện lệch trạng thái nghiêm trọng giữa git working tree và session log.
   - Ghi rõ bằng chứng, các phương án đã thử, câu hỏi chưa có lời giải và hành động an toàn tiếp theo.

4. **Checkpoint 4 — Turn End, Task Completion, and Recovery Handoff (Kết thúc lượt, đóng task & bàn giao):**
   - Ở cuối mỗi phản hồi có thay đổi code/doc/config: rà soát `git status`, đảm bảo diff không chứa file thừa, cập nhật session với trạng thái thực tế và bước tiếp theo (Next actionable step).
   - Khi task hoàn tất toàn bộ yêu cầu và validation pass: cập nhật trạng thái session sang `Completed`, di chuyển file từ `ai/tasks/active/` sang `ai/tasks/completed/`.

- **Template Session chuẩn 10 mục:**

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

---

### Rule 4. .env File Skill (Quy chuẩn cấu hình môi trường & Secret)
- **Tài liệu tham chiếu:** [`docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md`](../docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md).
- **Yêu cầu cốt lõi:**
  1. `.env` là file local cá nhân (được `.gitignore`). Mọi cấu hình chung mới phải khai báo mẫu tại `.env.example` và `FE/.env.example`.
  2. Biết chính xác script cần chạy lại sau khi sửa `.env` (ví dụ: `scripts/run-app-service.ps1 -Service <X> -NoBuild` cho container, hoặc `setup.ps1 -SkipDatabase -SkipRestore` để đồng bộ user-secrets).
  3. Tuyệt đối không commit mật khẩu, khóa bí mật JWT, connection string lên git hoặc in ra log.

---

### Rule 5. Coding Skill (Quy chuẩn kiến trúc & Triển khai mã nguồn)
- **Tài liệu tham chiếu:** [`ai/architecture-reference.md`](architecture-reference.md) và [`ai/context/architecture-guardrails.md`](context/architecture-guardrails.md).
- **Yêu cầu cốt lõi khi lập trình:**
  1. **Clean Architecture (5 layers):** Tuân thủ hướng phụ thuộc `Domain ← Application ← Infrastructure/Persistence ← API`.
     - *Domain:* Chứa Entity, Value Object, Invariants; không phụ thuộc bất kỳ layer nào.
     - *Application:* Chứa Commands, Queries, MediatR Handlers, DTOs, FluentValidation rules.
     - *API:* Chỉ tiếp nhận HTTP request, map DTO và dispatch qua `ISender` / MediatR. Controllers tuyệt đối **không** inject trực tiếp `DbContext`, Repository hoặc PostgreSQL connection.
     *(Lưu ý: Không suy diễn mọi service đều triển khai 5 layer giống hệt nhau; cần đối chiếu `ai/architecture-reference.md` với từng service cụ thể).*
  2. **CQRS & MediatR Pattern:** Tách biệt rõ ràng luồng Command (ghi/thay đổi trạng thái) và Query (đọc dữ liệu).
  3. **Ranh giới dữ liệu dịch vụ (Database per Service):** Mỗi microservice sở hữu schema độc lập. Không truy vấn chéo database giữa các dịch vụ.
  4. **Frontend Architecture:** React 19 + TypeScript + Tailwind CSS v4 + Three.js (xem thêm [`FE/AGENTS.md`](../FE/AGENTS.md)). Không sử dụng Material UI (MUI).
  5. **Definition of Done (DoD):** Sau khi code xong phải chạy lệnh xác thực `dotnet build SmartParking.slnx` (0 error) và unit tests trước khi hoàn tất task. Báo cáo kết quả trung thực, đánh dấu `[UNVERIFIED]` cho những mục chưa chạy kiểm thử.
  6. **Thiết kế API Specification:** Khi tạo tài liệu đặc tả thiết kế API mới hoặc cập nhật API contract, bắt buộc tuân theo template chuẩn tại [`docs/03-design/api/api-design-template.md`](../docs/03-design/api/api-design-template.md). Bao gồm đầy đủ metadata, traceability (FR/BR/UC), method/route, request/response sample, validation & error codes, retry & state outcomes, và review checklist.

---

**END.** Mọi AI Agent khi làm việc trên repository SmartPark bắt buộc phải đối chiếu và tuân thủ 5 rules trên.
