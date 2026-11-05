# SmartPark — Repository Instructions

The canonical AI agent instruction router is **[ai/AGENTS.md](ai/AGENTS.md)**. Read it before any repository work.

It determines which additional context files, architecture references, task/session protocols, and scoped instructions apply.

For **all coding/implementation/review tasks**, the router loads **[`docs/07-skills/SmartPark-Coding-Skill-v1.0.md`](docs/07-skills/SmartPark-Coding-Skill-v1.0.md)** as the primary shared implementation convention, then loads architecture/service/feature context only as required by the task.

Do not bypass `ai/AGENTS.md` and read the coding skill in isolation; requirements, service ownership, scoped `AGENTS.md`, and task-specific contracts still apply.

For tools that do not auto-discover this file, configure the tool's native instruction mechanism to load `ai/AGENTS.md`.
