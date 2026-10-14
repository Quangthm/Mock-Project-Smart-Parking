# SmartPark Documentation

This folder contains the shared SmartPark requirements, architecture, design, testing, project-management, and reference documentation.

> **Current baseline:** SmartPark v1.0 RC3 review candidate.  
> RC3 includes the previously approved RC2 SEC-01/SEC-02 security amendment and the RC3 PAY-01 payment-profile amendment.  
> Product acceptance remains **NOT RUN**. This is not yet a signed v1.0 release.

## Current requirements baseline

The active requirements baseline is the modular SmartPark v1.0 documentation under `01-requirements/`.

Current RC3 totals:

- **135** Functional Requirements (FRs)
- **57** Business Rules (BRs) + **9** monthly rules
- **41** Use Cases (UCs)
- **46** User Stories
- **72** acceptance scenarios, all currently **NOT RUN**

RC3 adds no new FR, BR, UC, or user-story IDs. It updates the approved MVP payment-integration profile and adds acceptance coverage for PAY-01.

### Start here

Read the two project-wide overviews first, then follow their links for detailed contracts:

- [Consolidated SRS v1.0 — product scope and business direction](01-requirements/srs/smartpark-srs-v1.0.md)
- [Solution Architecture v1.0 — ownership and technical direction](02-architecture/solution-architecture/smartpark-solution-architecture-v1.0.md)

- [SRS reading index](01-requirements/srs/README.md)
- [Business rules and policy catalogue](01-requirements/business-rules/README.md)
- [Use cases and user stories](01-requirements/use-cases/README.md)
- [Requirements traceability](01-requirements/requirements-traceability/README.md)
- [Permissions](01-requirements/srs/permissions.md)
- [State transitions, retries, and unknown outcomes](01-requirements/srs/state-transitions.md)
- [Authentication and account recovery](01-requirements/srs/authentication-recovery.md)

### Architecture and integration

- [Service ownership and event contracts](02-architecture/service-boundaries/ownership-and-events.md)
- [payOS adapter clarification](02-architecture/payment-integration/payos.md)

### Testing and release records

- [payOS test plan](04-testing/payment-integration/payos-test-plan.md)
- [Acceptance specifications — 72 scenarios, NOT RUN](04-testing/requirements-traceability/acceptance-scenarios.md)
- [Deferred, future, and implementation work](05-project-management/decision-log/deferred-and-implementation-register.md)
- [Baseline review](05-project-management/decision-log/baseline-review-2026-10-13-rc1.md)
- [RC2 security change summary](05-project-management/decision-log/rc2-change-summary.md)
- [RC3 PAY-01 change summary](05-project-management/decision-log/rc3-change-summary.md)
- [Change history](05-project-management/CHANGES.md)
- [Documentation validation](05-project-management/VALIDATION.md)
- [v1.0 baseline manifest](01-requirements/srs/baselines/v1.0/manifest.md)

## RC3 payment-profile amendment

RC3 prioritizes **payOS QR** as the working MVP online-payment implementation.

The RC3 payment amendment:

- replaces the earlier one-real-sandbox/VNPay-first acceptance gate with controlled low-value payOS production tests plus deterministic failure/race fixtures;
- keeps native VNPay, MoMo, and ZaloPay as separate planned options rather than treating payOS transactions as native provider transactions;
- exposes real-money checkout only through functioning routes;
- preserves assigned-Operator cash collection;
- does not change tariff rules, rounding, reservation minimums, deposits, cancellation/no-show policy, monthly rules, refund eligibility/percentages, permissions, capacity/occupancy rules, retention, TOTP, or account recovery.

Refund obligations remain mandatory. Unsupported online refund execution must remain visibly pending/reconciled rather than being silently replaced by an unapproved cash or manual-online fallback.

## Source-of-truth rules

The active SRS and requirement modules under `01-requirements/` are the current requirements baseline.

Supporting documents must not silently override approved requirements. A review, recommendation, example, fixture value, `OPEN`, `FUTURE`, `DEFERRED`, or otherwise unresolved item must not be promoted into a confirmed requirement unless an approved decision explicitly authorizes that change and the active baseline is updated accordingly.

In particular:

- Do not invent Business Rules from unrelated sections.
- Preserve unresolved or deferred decisions until they are explicitly approved.
- Do not treat future/deferred AI features as current implementation scope.
- Do not treat examples, demo values, or fixture data as production policy.
- Do not let architecture, design, test, or implementation notes silently redefine business behavior.
- When an approved decision changes the baseline, update the affected requirement, traceability, decision, and acceptance records together.
- Do not treat a configured or partially implemented payment route as accepted real-payment support without the required evidence.

## Documentation layout

| Area | Purpose |
|---|---|
| `01-requirements/` | SRS modules, business rules, policies, permissions, use cases, user stories, and traceability |
| `02-architecture/` | Service boundaries, ownership, events, database alignment, payment integration, and technical decisions |
| `03-design/` | UI/UX, product design, AI interaction design, and 3D/digital-twin design notes |
| `04-testing/` | Acceptance scenarios, integration test plans, test cases, and requirement coverage |
| `05-project-management/` | Change records, validation records, decision logs, handoffs, and release/baseline management |
| `06-research-and-reference/` | Research and external/reference material retained by the project |
| `templates/` | Reusable documentation templates |
| `archive/` | Historical baselines and non-authoritative evidence retained for traceability |

## Active documentation vs archive

Files under the active documentation areas above describe the current project baseline or supporting current work.

Historical inputs and comparison evidence, when retained in GitHub, belong under `archive/` and must be clearly treated as **non-authoritative historical material**. Historical files must not override the current baseline.

Generated patch overlays, temporary review workspaces, duplicate package copies, local notes, ZIP files, and other publication-only artifacts should remain outside the repository unless the team explicitly decides to archive them.

## Change and validation records

Publication-oriented change and validation records belong under `05-project-management/`.

Use these records to understand why the baseline changed, but do not treat a validation report as evidence that the software itself has passed implementation, integration, security, provider, or product acceptance testing.

The current **72 acceptance scenarios remain NOT RUN** until execution evidence is recorded.

## Naming

Use descriptive lowercase kebab-case names for living Markdown records.

Use a version in the filename when document versioning is meaningful. Do not use ambiguous names such as `final.md`, `latest.md`, or `new.md`.

Historical filenames may be preserved when renaming would weaken traceability. If a historical file is moved or renamed, update all references and integrity records that depend on its path.

## Publication status

This documentation set currently represents the SmartPark **v1.0 RC3 review candidate**.

The baseline consists of:

- the RC1 domain baseline;
- the RC2 SEC-01/SEC-02 security amendment;
- the RC3 PAY-01 payment-profile amendment.

Final v1.0 publication still requires the applicable PM review/sign-off, implementation evidence where required, and execution of acceptance scenarios before any pass result can be claimed.
