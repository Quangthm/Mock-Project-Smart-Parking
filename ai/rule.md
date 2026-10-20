# rule.md — Quy tắc quản lý session và checkpoint

**Mục đích:** Đảm bảo nếu session bị crash, hết context, mất kết nối, đổi máy, hoặc bất cứ biến cố nào, **session sau vẫn resume được nhanh chóng** mà không mất tiến độ đáng kể.

**Áp dụng cho:** Mọi AI coding agent làm việc trên bất kỳ repository nào sử dụng quy tắc này.

**Canonical paths:**

- Live snapshot: `ai/activity-log/_CURRENT_SESSION.md`
- Handoff đã đóng: `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md` (NN = số thứ tự kế tiếp)
- Activity log tháng: `ai/activity-log/`
- Decision records: `ai/decisions/`
- Task records: `ai/tasks/active/` → `ai/tasks/completed/`
- Templates: `ai/templates/`

---

## 1. Nguyên tắc cốt lõi

1. **Một file live duy nhất** cho session đang chạy: `ai/activity-log/_CURRENT_SESSION.md`.
2. **Checkpoint-driven, không micro-log.** Ghi tại 4 checkpoint bắt buộc (§2), không ghi sau từng file edit hay tool call vụn vặt.
3. **Append-only trong session.** Không xoá history cũ; sai thì gạch ngang + ghi lại.
4. **Đóng session = rename** `_CURRENT_SESSION.md` → `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md`.
5. **Timestamp ISO 8601** (`2026-10-12T09:30Z`) cho mọi entry. Tránh "vừa nãy" / "lúc trước".
6. **Không tin trí nhớ.** Verify bằng tool trước khi ghi "đã làm X". Chưa verify → đánh dấu `[UNVERIFIED]`.

---

## 2. Bốn Checkpoint bắt buộc

### Checkpoint 1 — Session Init & Plan Lock

Trước khi bắt đầu implementation đáng kể:

1. Đọc `_CURRENT_SESSION.md` (nếu tồn tại).
2. Chạy `git status --short --branch` + các lệnh verify cần thiết.
3. Xác định mục tiêu, constraints, và context đã load.
4. Định nghĩa plan ngắn, có thứ tự (nếu task đủ nhỏ → plan tương xứng, không tạo ceremony thừa).
5. Ghi initial snapshot và exact next action vào `_CURRENT_SESSION.md`.

### Checkpoint 2 — Milestone Completed

Sau khi hoàn thành một milestone có ý nghĩa VÀ đã chạy build/test liên quan:

1. Ghi outcome của milestone.
2. Ghi files changed.
3. Ghi verification commands + actual results.
4. Ghi quyết định hoặc deviations quan trọng.
5. Update exact resume pointer.

**NEVER** đánh dấu milestone "verified" nếu chưa chạy check thành công. Nếu check không áp dụng → giải thích lý do.

### Checkpoint 3 — Blocker, Direction Change, hoặc Risk

Flush state khi:

- Fix giống nhau fail hơn 2 lần liên tiếp.
- User thay đổi scope / requirements / priorities.
- Một assumption quan trọng bị invalidate.
- Sắp thực hiện destructive hoặc high-risk operation.
- External dependency / environment / design decision chặn tiến độ.

Trước lệnh rủi ro, ghi **pre-flight checkpoint** khi khả thi:

- Intended action.
- Expected impact.
- Backup / recovery strategy (nếu applicable).
- Unresolved risks.

> Pre-flight checkpoint **KHÔNG** phải là authorization. KHÔNG thực hiện lệnh bị cấm chỉ vì đã ghi checkpoint.

### Checkpoint 4 — Turn End / Session Close

Cuối mỗi response mà đã thay đổi code/config/tests/docs → update `_CURRENT_SESSION.md` đủ để agent khác resume.

Khi đóng session hoàn toàn:

1. Ghi final verified state.
2. Ghi incomplete work + outstanding risks.
3. Ghi exact next step (nếu còn việc).
4. Tạo handoff record khi work spans sessions.
5. Update activity log tháng nếu project workflow yêu cầu.

**Không** ghi entry trùng nếu checkpoint gần nhất đã capture đúng state.

---

## 3. Cấu trúc bắt buộc của `_CURRENT_SESSION.md`

```markdown
# _CURRENT_SESSION — <topic ngắn>

## 1. Quick Facts (Live Snapshot)
```
Task:          <mục tiêu 1-2 dòng>
Status:        IN_PROGRESS | PAUSED | CLOSING
Started:       <ISO 8601>
Agent:         <tên tool> (<model>)
Branch:        <branch> @ <short-sha>, <clean|N uncommitted>
Last verified: <milestone cuối đã verify, hoặc "none">
Build:         <PASS|FAIL|NOT RUN>
Tests:         <N/M passed|NOT RUN>
```

## 2. Context Loaded
- [x] <file đã đọc> (read <timestamp>)
- [ ] <file skip — lý do>

## 3. Plan
1. [x] <bước hoàn thành>
2. [ ] **<bước đang làm>** ← ACTIVE
3. [ ] <bước chờ>
   - BLOCKED: <lý do> (nếu bị chặn)

## 4. Milestone Progress Log (append-only, newest cuối)

### <timestamp> — <milestone title>
- **Outcome:** <kết quả>
- **Files changed:** `<path>` — <mô tả ngắn>
- **Verification:** `<command>` → <actual result>
- **Decisions/deviations:** <nếu có>
- **Commit:** <hash> (nếu có)

## 5. Decisions & Open Questions / Risks
- **D-<id>:** <quyết định confirmed> — by <ai/user> at <timestamp>
- **Q:** <câu hỏi chưa trả lời>
- **R:** <rủi ro chưa mitigate>

## 6. Exact Resume Pointer (Next Step)
- **File/module:** `<exact path or module>`
- **Action:** <hành động cụ thể tiếp theo>
- **Prerequisite:** <điều kiện cần, nếu có>
- **Next verify:** `<command>`
```

> Snapshot hỗ trợ resume nhanh mà không cần đọc toàn bộ history. Section 1 phải đủ để biết task gì, ở đâu, status ra sao.

---

## 4. Liên kết với các record khác trong `ai/`

- **Activity log tháng:** khi đóng session, thêm 1 entry ngắn vào `ai/activity-log/` (theo `ai/templates/activity-log-template.md`).
- **Handoff:** file đã đóng ở `ai/handoffs/` dùng cấu trúc `ai/templates/handoff-template.md`.
- **Decision record:** quyết định quan trọng → tạo record riêng trong `ai/decisions/` (theo `ai/templates/decision-record-template.md`).
- **Task record:** task AI-assisted → `ai/tasks/active/` → `ai/tasks/completed/` sau khi human review.

Không duplicate full content của historical records bên trong `_CURRENT_SESSION.md`. Snapshot là live pointer, không phải thay thế cho activity history.

---

## 5. Quy tắc mở session mới

Trước khi gõ lệnh code đầu tiên:

1. Kiểm tra `ai/activity-log/_CURRENT_SESSION.md` có tồn tại không.
2. Liệt kê `ai/handoffs/` → tìm file số lớn nhất.
3. Đọc handoff/snapshot mới nhất + context files phù hợp task (theo routing table trong `ai/AGENTS.md`).
4. Tạo `_CURRENT_SESSION.md` mới (template §3).
5. Chạy verify (git status / build / test nếu phù hợp) → ghi vào Quick Facts.
6. Nếu verify FAIL: **dừng**, báo user trước khi sửa state. KHÔNG tự ý migrate lùi / drop data.

---

## 6. Quy tắc đóng session

Khi user nói "đóng session" / "kết thúc" / "tạm dừng dài hạn":

1. Set `Status: CLOSING`.
2. Re-verify state hiện tại → cập nhật Quick Facts.
3. Đảm bảo Section 6 "Exact Resume Pointer" cụ thể, paste-and-run được.
4. Rename `ai/activity-log/_CURRENT_SESSION.md` → `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md`.
5. Thêm 1 entry ngắn vào activity log tháng.
6. Nếu có quyết định quan trọng → tạo/cập nhật record trong `ai/decisions/`.
7. Commit khi user yêu cầu. **KHÔNG** auto-push trừ khi user yêu cầu.

---

## 7. Crash recovery (POST-CRASH RECONCILE)

> Không có crash recovery 100%: unflushed changes, machine failure, storage failure có thể gây mất dữ liệu. Quy tắc dưới đây tối đa hóa khả năng phục hồi.

Nếu mở `_CURRENT_SESSION.md` và thấy `Status: IN_PROGRESS` (session trước crash):

1. Đọc `ai/rule.md` (file này).
2. Đọc `ai/activity-log/_CURRENT_SESSION.md`.
3. Chạy `git status --short --branch`.
4. Kiểm tra staged, unstaged, untracked changes.
5. So sánh working tree với checkpoint cuối (Quick Facts + Milestone Progress Log cuối).
6. Xác định work đã hoàn thành nhưng chưa ghi.
7. Chạy lại **chỉ** verification checks cần thiết.
8. Cập nhật snapshot với reconciled facts, đánh marker `### <ts> — POST-CRASH RECONCILE`.
9. Resume từ exact next safe action.

**NEVER** assume snapshot khớp hoàn toàn với working tree.
**NEVER** discard uncommitted work chỉ vì nó không có trong checkpoint cuối.
**KHÔNG** rename file thành handoff cho tới khi session kết thúc bình thường.

Khi đóng session, progress log sẽ có cả phần pre-crash + post-crash → giữ nguyên, đó là đúng lịch sử.

---

## 8. Anti-patterns (cấm)

- ❌ Viết tiến độ vào chat reply mà không ghi file (mất khi context reset).
- ❌ Sửa file handoff cũ đã đóng (history must be immutable). Sai thì viết file đính chính mới.
- ❌ "Done" mà không có evidence (command output / file diff / test result).
- ❌ Dùng từ mơ hồ: "gần xong", "ổn rồi", "hình như pass". Phải có số / status cụ thể.
- ❌ Tạo file handoff không theo thứ tự `NN_` hoặc đặt ngoài `ai/handoffs/`.
- ❌ Xoá `_CURRENT_SESSION.md` mà chưa rename thành handoff.
- ❌ Micro-log từng file edit hoặc từng tool call vụn vặt thay vì checkpoint theo milestone.

---

## 9. Ví dụ tối thiểu của 1 milestone entry tốt

```markdown
### 2026-10-12T11:20Z — <SERVICE_OR_MODULE> build PASS
- **Outcome:** Thêm null check trong `<SOURCE_FILE>:18`
- **Files changed:** `<SOURCE_FILE>` — added null guard
- **Verification:** `<BUILD_COMMAND>` → 0 err, 0 warn; `<TEST_COMMAND>` → 42/42 passed
- **Decisions/deviations:** none
- **Next:** verify endpoint bằng smoke script
```

Ngắn, có evidence, có next pointer. Đủ để session sau đọc 1 entry là biết mình đang ở đâu.

---

**END.** Rule này là live document — nếu pattern thay đổi, sửa file này + ghi entry vào `_CURRENT_SESSION.md` giải thích lý do.
