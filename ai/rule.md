# rule.md — Quy tắc theo dõi tiến độ session

**Mục đích:** Đảm bảo nếu session bị crash, hết context, mất kết nối, đổi máy, hoặc bất cứ biến cố nào, **session sau vẫn resume được trong < 5 phút** mà không mất tiến độ.

**Áp dụng cho:** Mọi agent (OpenCode / Claude / Kiro / khác) làm việc trên repo SmartPark.

**Vị trí log:**

- Live log: `ai/activity-log/_CURRENT_SESSION.md`
- Handoff đã đóng: `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md` (NN = số thứ tự kế tiếp)

---

## 1. Nguyên tắc cốt lõi

1. **Luôn có một file "live" duy nhất** cho session đang chạy: `ai/activity-log/_CURRENT_SESSION.md`. Đây là nơi ghi tiến độ liên tục.
2. **Update là việc đầu tiên** sau mỗi hành động đáng kể, **không** dồn cuối session.
3. **Append-only trong session.** Không xoá history cũ; sai thì gạch ngang + ghi lại.
4. **End-of-session phải đóng file:** rename `_CURRENT_SESSION.md` thành `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md` (NN = số thứ tự kế tiếp trong `ai/handoffs/`).
5. **Mỗi update phải timestamp ISO 8601** (`2026-10-12T09:30Z`), tránh "vừa nãy" / "lúc trước".
6. **Không tin trí nhớ.** Trước khi viết "đã làm X", verify bằng tool (read file, run command). Nếu chưa verify → đánh dấu `[UNVERIFIED]`.

---

## 2. Khi nào phải update `_CURRENT_SESSION.md`

Update **ngay** sau mỗi sự kiện sau (không đợi tích luỹ):

| Trigger | Bắt buộc ghi |
|---|---|
| Đọc xong file context khởi đầu (project-context, current-state, handoff) | Section "Context loaded" |
| Sửa / tạo / xoá file source code hoặc doc | Path + 1 dòng mô tả thay đổi |
| Chạy migration, build, test, docker cmd | Command + exit status (pass/fail/output tóm tắt) |
| Thêm/đổi package, secret, config | Tên + version + lý do |
| Lock một quyết định với user | Decision + rationale + ai confirm |
| Gặp error / bug / blocker | Symptom + root cause (nếu biết) + fix attempt |
| Hoàn thành 1 task trong todo list | Đánh dấu DONE + ref tới evidence |
| User đổi hướng / đổi yêu cầu | Old plan → new plan, ngắn gọn |
| Trước khi chạy lệnh có rủi ro (drop, delete, force-push, prod) | Pre-flight log + xác nhận user |

**Không cần ghi:** mỗi tool call vụn vặt (read 1 file để xem), mỗi lần grep, mỗi message trao đổi nhỏ.

---

## 3. Cấu trúc bắt buộc của `_CURRENT_SESSION.md`

```markdown
# _CURRENT_SESSION — <topic ngắn>

**Started:** 2026-10-12T09:30Z
**Agent:** OpenCode (<model>)
**Goal:** <1-2 dòng mục tiêu session>
**Status:** IN_PROGRESS | PAUSED | CLOSING

---

## 0. Context loaded
- [x] ai/context/project-context.md (read 2026-10-12T09:31Z)
- [x] ai/context/current-state.md
- [ ] <file khác> (skip — không cần cho task này)

## 1. Verified state at start
<output ngắn gọn của verify script (git status / build / test), hoặc lý do skip>

## 2. Plan
1. <bước 1>
2. <bước 2>
...

## 3. Progress log (append-only, newest cuối)

### 2026-10-12T09:45Z — Step 1 done
- Edited `src/Services/UserService/.../File.cs:42` (added null check)
- `dotnet build SmartParking.slnx` → 0 err, 0 warn
- Evidence: <command output / file path / commit hash>

### 2026-10-12T10:05Z — Step 2 blocked
- Symptom: <lỗi>
- Hypothesis: <nguyên nhân nghi ngờ>
- Next try: <bước tiếp>

## 4. Files changed this session
| Path | Change |
|---|---|
| `...` | `...` |

## 5. Commands run (chỉ những lệnh có side-effect)
- `dotnet build SmartParking.slnx` → OK
- `git commit -m "..."` → `abc1234`

## 6. Decisions locked
- D-2026-10-12-01: <quyết định> — confirmed by <ai> at <timestamp>

## 7. Open questions / risks
- Q: <câu hỏi chưa trả lời>
- R: <rủi ro chưa mitigate>

## 8. Next step (nếu pause/crash now)
**Câu lệnh chính xác để resume:** `<command>` hoặc `<đọc file X rồi tiếp step Y>`

## 9. Quick Facts (snapshot)
```
Branch:        <branch> @ <short-sha>, <clean|N uncommitted>
Build:         <PASS|FAIL>
Tests:         <N/M passed>
Services:      <UserService|ParkingService|ReservationService> <state>
Docker:        <running|stopped>
```
```

---

## 3b. Liên kết với các record khác trong `ai/`

- **Activity log tháng:** khi đóng session, thêm 1 entry ngắn vào `ai/activity-log/smartpark-ai-activity-YYYY-MM.md` (theo `ai/templates/activity-log-template.md`).
- **Handoff:** file đã đóng ở `ai/handoffs/` dùng cấu trúc `ai/templates/handoff-template.md`.
- **Decision record:** quyết định quan trọng → tạo record riêng trong `ai/decisions/` (theo `ai/templates/decision-record-template.md`).
- **Task record:** task AI-assisted → `ai/tasks/active/` → `ai/tasks/completed/` sau khi human review.

---

## 4. Tần suất flush ra disk

- **Sau mỗi mục Section 3 (progress log entry)** → write file ngay lập tức.
- Không buffer nhiều entry rồi mới ghi. Nếu agent crash giữa entry → coi entry đó như chưa làm.
- Mỗi 30 phút wallclock, dù không có entry mới, vẫn touch file + ghi heartbeat `### <timestamp> — heartbeat (no progress)` để session sau biết khoảng thời gian dead.

---

## 5. Quy tắc đóng session

Khi user nói "đóng session" / "kết thúc" / "tạm dừng dài hạn":

1. Set `Status: CLOSING`.
2. Re-verify state hiện tại (git status / build / test) → paste output vào Section 1.
3. Đảm bảo Section 8 "Next step" là câu lệnh paste-and-run được.
4. Rename `ai/activity-log/_CURRENT_SESSION.md` → `ai/handoffs/NN_Session_YYYY-MM-DD_<topic>_Handoff.md` (NN = số kế tiếp trong `ai/handoffs/`).
5. Thêm 1 entry ngắn vào activity log tháng.
6. Nếu có quyết định quan trọng → tạo/cập nhật record trong `ai/decisions/`.
7. Commit khi user yêu cầu. **Không** auto-push trừ khi user yêu cầu.

---

## 6. Quy tắc mở session mới

Đầu mỗi session, **trước khi gõ lệnh code đầu tiên**:

1. Liệt kê `ai/handoffs/` → tìm file số lớn nhất; kiểm tra `ai/activity-log/_CURRENT_SESSION.md` có tồn tại không.
2. Đọc theo thứ tự: `ai/context/project-context.md` → `ai/context/current-state.md` → handoff mới nhất → plan tương ứng.
3. Tạo `_CURRENT_SESSION.md` mới (template Section 3) trong < 60 giây.
4. Chạy verify (git status / build / test) → paste output vào Section 1.
5. Nếu verify FAIL: **dừng**, báo user trước khi sửa state. Không tự ý migrate lùi / drop data.

---

## 7. Anti-patterns (cấm)

- ❌ Viết tiến độ vào chat reply rồi không ghi file (mất khi context reset).
- ❌ Sửa file handoff cũ đã đóng (history must be immutable). Sai thì viết file đính chính mới.
- ❌ "Done" mà không có evidence (command output / file diff / test result).
- ❌ Dồn 10 thay đổi rồi mới update 1 lần.
- ❌ Dùng từ mơ hồ: "gần xong", "ổn rồi", "hình như pass". Phải có số / status cụ thể.
- ❌ Tạo file handoff không theo thứ tự `NN_` hoặc đặt ngoài `ai/handoffs/`.
- ❌ Xoá `_CURRENT_SESSION.md` mà chưa rename thành handoff.

---

## 8. Crash recovery checklist (cho session N+1)

Nếu mở `ai/activity-log/_CURRENT_SESSION.md` và thấy `Status: IN_PROGRESS` (tức session trước crash):

1. Đọc Section 8 "Next step" — đó là điểm tiếp.
2. Re-verify state hiện tại có khớp Section 9 "Quick Facts" cuối cùng không.
3. Nếu lệch: ghi delta vào progress log với marker `### <ts> — POST-CRASH RECONCILE`.
4. Tiếp tục từ next step. **Không** rename file thành handoff cho tới khi session kết thúc bình thường.
5. Khi đóng, Section 3 progress log sẽ có cả phần pre-crash + post-crash → giữ nguyên, đó là đúng lịch sử.

---

## 9. Ví dụ tối thiểu của 1 entry tốt

```markdown
### 2026-10-12T11:20Z — UserService build PASS
- Action: thêm null check trong `VehicleRegistrationService.cs:18`
- Result: `dotnet build SmartParking.slnx` → 0 err, 0 warn
- Test: `dotnet test SmartParking.slnx` → 42/42 passed
- Next: verify endpoint bằng smoke script
```

Ngắn, có evidence, có next pointer. Đủ để session sau đọc 1 entry là biết mình đang ở đâu.

---

**END.** Rule này tự nó là live document — nếu pattern thay đổi, sửa file này + ghi entry vào `_CURRENT_SESSION.md` giải thích lý do.
