# Database Change Log Report

## A. Metadata
* **Change ID:** [Unique ID, e.g., DB-YYYYMMDD-NN]
* **Recorded At:** [Local timestamp with timezone, e.g., 2026-10-09T11:00:00+07:00]
* **Task/Purpose:** [Description of the task or purpose]
* **Author/Generator:** [Author or generating agent]
* **Report Status:** Draft

## B. Git Comparison References
* **Baseline Branch:** [e.g., origin/develop]
* **Baseline SHA:** [Exact SHA]
* **Current Branch:** [e.g., feature/add-reservations]
* **Current HEAD SHA:** [Exact SHA]
* **Working-Tree State:** [Clean | Uncommitted changes present]
* **Included DB Paths:** [List of paths inspected]

## C. Executive Summary
* **Overview:** [Short description of the overall database changes]
* **Summary Counts:** [Added: X tables, Y columns, Z triggers | Modified: ... | Removed: ...]
* **Main Implications:** [Design or compatibility implications]
* **Breaking Changes:** [List any breaking changes, or "None"]

## D. Detailed Changes

> **Rules:** Each Change subsection describes exactly ONE database object.
> Do NOT group multiple tables into one Change. See Workflow §5.1.

### Change 1
* **Category:** [Table Added | Column Modified | Column Removed | Index Added | Constraint Modified | Trigger Added | Table Removed | etc.]
* **SQL File Path:** [Exact path, e.g., `scripts/database/microservices/01-user-service-db.sql`]
* **Database Object:** [Single object name, e.g., `users`]
* **Change Source:** [Committed | Uncommitted (Staged) | Uncommitted (Working Tree) | Untracked]
* **Before State:**
  ```sql
  -- Exact baseline definition from git show <BASE_SHA>:<file>
  -- e.g.: status VARCHAR(50) CHECK (status IN ('ACTIVE','INACTIVE','LOCKED','PENDING_APPROVAL'))
  -- Write "Not present in baseline" for new objects.
  -- NEVER write "Baseline definitions" or other vague placeholders.
  ```
* **After State:**
  ```sql
  -- Exact final definition from working tree
  -- e.g.: status VARCHAR(50) CHECK (status IN ('ACTIVE','INACTIVE','LOCKED','PENDING_APPROVAL','PENDING_VERIFICATION','REJECTED'))
  ```
* **Reason for Change:** [Concrete reason with evidence, or "Unknown"]
* **Expected Impact:** [Impact on data, queries, ORM, application logic]
* **Compatibility Assessment:** [Breaking | Backward-compatible] — [Brief explanation]
* **Evidence/Reference:** [File path with line numbers, e.g., `01-user-service-db.sql#L12-L15`]

### Change 2
*(Repeat the same structure for each significant database object change)*

## E. Cross-Object Impact
* **Dependencies:** [Dependencies between changed database objects. State "None identified" if none.]
* **Related Constraints/Indexes:** [Impact on constraints or indexes across tables]
* **Application/Doc Areas:** [Relevant application layers, ORM models, or documentation areas affected]
* **Inconsistencies/Risks:** [Potential inconsistencies or risks. State "None identified" if none.]

## F. Open Questions and Risks
* **Unresolved Questions:** [List any unresolved questions, or "None"]
* **Assumptions:** [List any assumptions made during analysis, or "None"]
* **Potential Regressions:** [List any potential regressions, or "None"]
* **Requires Human Review:** [List items requiring human review, or "None"]

## G. Verification Status
* **Git Diff Reviewed:** [Yes | No | Not applicable]
* **Untracked DB Files Reviewed:** [Yes | No | Not applicable]
* **SQL Execution Verified:** [Yes | No | Not verified — explain]
* **Database Tests Executed:** [Yes | No | Not verified — explain]
* **Verification Evidence:** [Specific explanation or reference to evidence]

## H. Version Traceability
* **Baseline Comparison:** `[Baseline Branch]` (`[SHA]`) vs `[Current Branch]` (Working State on top of `[HEAD SHA]`)
* **Related Reference:** [Commit SHA or PR URL, if available. Otherwise "Not yet committed"]
* **Reconstruction Notes:** [Exact git command to reproduce comparison, e.g., `git diff 4a83adc..HEAD -- scripts/database/`]

## I. Final Status
**Status:** Draft

> **Disclaimer:** This report records the observed database changes at generation time. It does not prove or guarantee SQL execution, testing, deployment, or merging unless explicitly supported by verification evidence. The developer must review and promote this report's status before committing.
