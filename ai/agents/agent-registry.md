# SmartPark AI Agent Registry

Use this file to track which AI agents are used on the project and what role each agent has.

| Agent | Provider / Tool | Primary Use | Owner | Status | Profile & Scope |
|---|---|---|---|---|---|
| [`coding-planner`](coding-planner.md) | Gemini / Antigravity | Planning / Requirements Analysis & Todo-List | Developer | `ACTIVE` | Lập Micro-Incremental Todo-List, đối chiếu SRS/Arch, tuân thủ approval gate |
| [`coding-agent`](coding-agent.md) | Gemini / Antigravity | Coding / Step-by-Step Implementation | Developer | `ACTIVE` | Nhận plan từ planner, code từng bước nhỏ, verify và tick `[x]` |
| [`document-audit-agent`](document-audit-agent.md) | Gemini / Antigravity | Review / API Contract Compliance | Developer | `ACTIVE` | Đối chiếu code với `docs/02-architecture/api/`, phát hiện sai lệch Route/DTO/Status Code |
| [`architect-audit-agent`](architect-audit-agent.md) | Gemini / Antigravity | Review / Architecture & Boundary Compliance | Developer | `ACTIVE` | Kiểm toán theo `docs/02-architecture/source-code-architecture-rules.md`, giữ vững Clean Architecture |

## Core Pipeline

```text
User Task
   ↓
[coding-planner] (Phân tích & Lập Micro-Incremental Plan)
   ↓
[User Approval Gate: BẮT BUỘC User duyệt OK / Tiến hành]
   ↓
[coding-agent] (Thực thi code tuần tự từng bước & Tick [x])
   ↓
[document-audit-agent] + [architect-audit-agent] (Kiểm toán hợp đồng API & Kiến trúc phân tầng)
   ↓
Báo cáo nghiệm thu & Hoàn tất
```

> 💡 **Tự động hóa qua Skill:** Quy trình này được đóng gói thành skill [smartpark-task-pipeline](../../.agents/skills/smartpark-task-pipeline/SKILL.md). Trong Antigravity, bạn có thể kích hoạt skill này để điều phối toàn bộ pipeline trong 1 chat duy nhất.

## Rules

- Keep responsibilities explicit.
- Do not treat an AI agent as an approval authority.
- Human owners remain responsible for acceptance of changes.
