# AI-Assisted Tasks & Session Records

Thư mục lưu trữ nhật ký phiên làm việc (Session Reviews) theo **Rule 3 — Session Skill** (`ai/rule.md`).

```text
ai/tasks/
├── active/        # Chứa session của các task ĐANG THỰC HIỆN (In Progress)
└── completed/     # Chứa session của các task ĐÃ HOÀN THÀNH và được nghiệm thu
```

## Quy trình làm việc:
1. Khi bắt đầu task mới: Tạo file session theo mẫu `docs/07-skills/SmartParking_Session_SKILL_v1.0.md` vào `ai/tasks/active/`.
   - Quy ước tên file: `TASK-<JiraKey>-SESSION-YYYYMMDD.md` (ví dụ: `SPARK-262-SESSION-20261021.md`).
2. Trong quá trình làm: Cập nhật liên tục tiến độ, thay đổi file, blocker và lệnh test.
3. Khi hoàn tất task (Definition of Done thỏa mãn): Di chuyển file từ `active/` sang `completed/`.
