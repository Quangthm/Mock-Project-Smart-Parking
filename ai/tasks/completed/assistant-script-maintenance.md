# Assistant Script Maintenance

## 1. Change Summary

- **Date:** 2026-11-19
- **Starting point:** `develop` at `31bdfa71`
- **Task:** Consolidate Assistant setup, readiness and regression scripts.
- **Status:** Completed

## 2. Objectives

- Keep three Assistant entry points under `scripts/`.
- Complete independent review and update tester handoff before removing superseded files.

## 3. Work Completed

- Consolidated operation and regression modes, then obtained independent review.
- Addressed review findings on Docker endpoint validation and signing fixture isolation.
- Updated tester handoff and current references to the superseded commands.
- Removed the 18 verified paths after a second review and preserved both handoff anchors used by the archive.

## 4. Technical Decisions

- Preserve the names and options consumed by shared setup and runner scripts.
- Keep the PowerShell signing bridge and its C# helpers in IoTService tooling.
- Keep Assistant's Docker fixtures local and isolate signing aliases from the developer environment.
- Resolve the setup child executable from the current PowerShell installation.

## 5. Files Changed

- Assistant setup, check and test entry points under `scripts/`.
- Signing tooling under `src/Services/IoTService/Tools/Assistant/`.
- Assistant handoff, linked setup guidance and PostgreSQL test instructions.

## 6. Issues and Blockers

- Workspace-local temporary directories were needed for .NET file-based tools in the Windows sandbox.
- A PowerShell 7 rerun exposed empty process variables left by the runner fixture. The fixture now removes its new aliases using the environment provider.

## 7. Validation Results

- See [Assistant handoff, section 7](../../../docs/04-testing/assistant-handoff-testing.md#7-bộ-kiểm-tra-tự-động-và-bằng-chứng-đã-có) for test commands and results.

## 8. Git Status

- Initial working tree was clean. No commits were created.
- Pre-deletion checkpoint confirmed 18 exact paths in `scripts/` are clean relative to HEAD, tracked and exclusively authored by the local Git identity. SQL migrations, shared scripts and the ignored `.trx` are outside the deletion list.
- Post-deletion inspection found exactly three Assistant scripts, no live references to removed entry points and no whitespace errors.

## 9. Handoff / Next Steps

- Use the Assistant handoff for QA runs and new test evidence.

## 10. Context for Next Session

- The tester handoff documents the current entry points and keeps earlier results as dated evidence.
