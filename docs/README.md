# SmartPark Documentation

This folder contains the project's shared documentation.

## Primary requirements baseline

The current primary baseline for requirements documentation is:

`01-requirements/srs/baselines/SmartPark_Consolidated_Project_Baseline_v0.8.md`

Baseline metadata:

- Project: SmartPark
- Baseline Version: 0.8
- SRS Version: 1.0 (Draft)
- Date: 2026-09-30
- Status: Draft / Working Compilation

The SRS is the primary source for requirement statements. The documentation
folders below provide places for related working documents, diagrams, test
artifacts, design records, and project records.

## Important baseline rule

Do not silently convert a blank, `N`, `OPEN`, `FUTURE`, `DEFERRED`, or otherwise
unfinished item in the SRS into a confirmed requirement.

In particular:

- Keep intentionally blank SRS sections blank until the team defines them.
- Do not invent Business Rules from other SRS sections.
- Preserve unresolved `N` values.
- Do not treat future/deferred AI features as current implementation scope.
- When the SRS is updated, update the baseline copy and record the change.

## Where to put documents

| Area | Purpose |
|---|---|
| `01-requirements/` | SRS, use cases, business rules, traceability |
| `02-architecture/` | Architecture, service boundaries, API, database, diagrams |
| `03-design/` | UI/UX, product AI, 3D digital twin |
| `04-testing/` | Test plans, test cases, requirement coverage |
| `05-project-management/` | Decisions, meeting notes, handoffs |
| `06-research-and-reference/` | Research and external references |
| `templates/` | Reusable document templates |

## Naming

Use descriptive lowercase kebab-case names for living Markdown records.

Use a version in a filename when document versioning is meaningful.
Do not use ambiguous names such as `final.md`, `latest.md`, or `new.md`.

## Source-of-truth note

The SRS remains the requirements baseline. A supporting document must not
quietly override it. Conflicts or proposed changes should be recorded and
then reflected in a future SRS revision when approved.
