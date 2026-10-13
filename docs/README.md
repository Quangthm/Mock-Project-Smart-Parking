# SmartPark Documentation

This folder contains the shared SmartPark requirements, architecture, design, testing, project-management, and reference documentation.

> **Current baseline:** SmartPark v1.0 RC2 review candidate.  
> The SEC-01/SEC-02 security amendment is included. Product acceptance remains **NOT RUN**.  
> This is not yet a signed v1.0 release.

## Current requirements baseline

The active requirements baseline is the modular SmartPark v1.0 documentation under `01-requirements/`.

Current RC2 totals:

- **135** Functional Requirements (FRs)
- **57** Business Rules (BRs) + **9** monthly rules
- **41** Use Cases (UCs)
- **46** User Stories
- **65** acceptance scenarios, all currently **NOT RUN**

Start with:

- [SRS reading index](01-requirements/srs/README.md)
- [Business rules and policy catalogue](01-requirements/business-rules/README.md)
- [Use cases and user stories](01-requirements/use-cases/README.md)
- [Requirements traceability](01-requirements/requirements-traceability/README.md)
- [Permissions](01-requirements/srs/permissions.md)
- [State transitions, retries, and unknown outcomes](01-requirements/srs/state-transitions.md)
- [Authentication and account recovery](01-requirements/srs/authentication-recovery.md)

Supporting baseline records:

- [Service ownership and event contracts](02-architecture/service-boundaries/ownership-and-events.md)
- [Acceptance specifications — NOT RUN](04-testing/requirements-traceability/acceptance-scenarios.md)
- [Deferred, future, and implementation work](05-project-management/decision-log/deferred-and-implementation-register.md)
- [Baseline review](05-project-management/decision-log/baseline-review-2026-10-13-rc1.md)
- [RC2 change summary](05-project-management/decision-log/rc2-change-summary.md)
- [v1.0 baseline manifest](01-requirements/srs/baselines/v1.0/manifest.md)

## Source-of-truth rules

The active SRS and requirement modules under `01-requirements/` are the current requirements baseline.

Supporting documents must not silently override approved requirements. A review, recommendation, example, fixture value, `OPEN`, `FUTURE`, `DEFERRED`, or otherwise unresolved item must not be promoted into a confirmed requirement unless an approved decision explicitly authorizes that change and the active baseline is updated accordingly.

In particular:

- Do not invent Business Rules from unrelated sections.
- Preserve unresolved or deferred decisions until they are explicitly approved.
- Do not treat future/deferred AI features as current implementation scope.
- Do not treat examples, demo values, or fixture data as production policy.
- Do not let architecture, design, test, or implementation notes silently redefine business behavior.
- When an approved decision changes the baseline, update the affected requirement, traceability, and decision records together.

## Documentation layout

| Area | Purpose |
|---|---|
| `01-requirements/` | SRS modules, business rules, policies, permissions, use cases, user stories, and traceability |
| `02-architecture/` | Service boundaries, ownership, events, database alignment, integration architecture, and technical decisions |
| `03-design/` | UI/UX, product design, AI interaction design, and 3D/digital-twin design notes |
| `04-testing/` | Acceptance scenarios, test plans, test cases, and requirement coverage |
| `05-project-management/` | Change records, validation records, decision logs, handoffs, and release/baseline management |
| `06-research-and-reference/` | Research and external/reference material retained by the project |
| `templates/` | Reusable documentation templates |
| `archive/` | Historical baselines and non-authoritative evidence retained for traceability |

## Active documentation vs archive

Files under the active documentation areas above describe the current project baseline or supporting current work.

Historical inputs and comparison evidence, when retained in GitHub, belong under `archive/` and must be clearly treated as **non-authoritative historical material**. Historical files should not be used to override the current baseline.

Generated patch overlays, temporary review workspaces, duplicate package copies, local notes, ZIP files, and other publication-only artifacts should remain outside the repository unless the team explicitly decides to archive them.

## Change and validation records

Publication-oriented change and validation records belong under `05-project-management/`.

Use these records to understand why the baseline changed, but do not treat a validation report as evidence that the software itself has passed implementation, integration, security, or product acceptance testing.

The current acceptance scenarios remain **NOT RUN** until execution evidence is recorded.

## Naming

Use descriptive lowercase kebab-case names for living Markdown records.

Use a version in the filename when document versioning is meaningful. Do not use ambiguous names such as `final.md`, `latest.md`, or `new.md`.

Historical filenames may be preserved when renaming would weaken traceability. If a historical file is moved or renamed, update all references and integrity records that depend on its path.

## Publication status

This documentation set currently represents the SmartPark **v1.0 RC2 review candidate**.

The RC2 security amendment adds the approved current-MVP TOTP and account-recovery requirements while preserving the previously approved parking, payment, monthly, capacity, and service-ownership baseline.

Final v1.0 publication still requires the applicable PM review/sign-off and any repository-publication cleanup recorded in the project decision/change records.
