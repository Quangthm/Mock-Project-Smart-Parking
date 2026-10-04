# SmartParking Frontend Lead Guide

This guide applies to frontend work in this project. The root `AGENTS.md` points here so it is read before implementation. `.agent/lead.md` is for backend work; do not carry its backend assumptions into this app.

## Project facts

- SmartParking is a React 19 + TypeScript + Vite 8 app using Tailwind CSS 4, running in Figma Make.
- `src/main.tsx` is the entry point, `src/App.tsx` composes the app, and `src/index.css` imports Tailwind and defines global styles.
- The app already has a running development server. Do not start another one unless the user asks.
- Use the existing dependencies and patterns. Ask before adding a dependency or changing project configuration.
- Follow the UI's existing visual language and responsive behavior. Use Tailwind utilities and existing CSS variables/components where appropriate; global font setup and global styles belong in `src/index.css`.
- Check the actual source before relying on paths or APIs. The documented structure is a starting point, not proof that every listed file currently exists.

## Working rules

1. For a small, clear, reversible UI change, make the requested change directly. For an ambiguous, multi-file, or risky change, inspect the relevant code first, ask up to four focused questions if needed, then present a short plan and wait for approval before editing. A direct instruction such as “làm đi” or “ok” authorizes the described work.
2. This project may be local and not yet in Git. Git is optional: do not block work because `.git` or a clean `git status` is missing. Before delegated or broad edits, record the relevant files' current state and preserve any existing local changes. Never reset, clean, or overwrite unrelated work.
3. Keep changes within the requested scope. Do not reformat unrelated code, create unrequested files, or change dependencies, configuration, routes, or visual behavior outside the task.
4. Prefer the smallest implementation that fits the existing component structure. Keep UI text in the language and tone already used by the surrounding screen. Make interactive controls work; do not present decorative controls as functional.
5. Do not claim a build, lint, or test passed unless it was actually run. Do not run tests or verification commands unless the user asks for verification or the task explicitly includes it. When verification is requested, use only scripts that exist in `package.json` (currently `npm run build` and `npm run format`; formatting rewrites files, so do not run it unless requested).
6. Report material uncertainty and any out-of-scope issue without silently expanding the task.

## Executor (`agy`) workflow

- Use `agy` only when the task is substantial, repetitive, or spans multiple similar files. Handle small edits and nuanced UI decisions directly.
- Give the Executor a self-contained task with the exact allowed files, desired visible behavior, existing components/APIs verified from source, constraints, and a clear stop condition if required information is missing.
- For read-only inspection, say explicitly that it must not modify files. For implementation, compare the resulting changes with the recorded baseline; use `git diff` when available, otherwise compare the affected files before and after.
- Review each changed line and check for unintended files, invented imports/APIs, broken interaction states, responsive regressions, and unrelated restyling. Run build/lint only when verification is requested.
- If the Executor cannot run or returns no usable result, say so plainly and continue with direct inspection or implementation when authorized.

## Frontend task summary format

For a planned task, keep the plan concise:

```text
## Goal
One sentence describing the user-visible result.

## Verified current state
Relevant source files and observed behavior.

## Approach and scope
Files to change, files to leave alone, and why this approach fits.

## Acceptance
What the user should see or do to confirm the result; list machine checks only if requested.

## Risks
Specific UI, responsive, state, or navigation risks and how to spot them.
```

End a plan that needs approval with: “Bạn duyệt kế hoạch này chứ?” Then wait.
