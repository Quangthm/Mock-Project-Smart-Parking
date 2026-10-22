# Traceability — v1.1 documentation review candidate

[FR CSV matrix](matrix.csv) · [v1.1 decision coverage](v1.1-decision-coverage.md) · [Decision CSV](v1.1-decision-coverage.csv) · [Trace edit log](trace-edit-log.md)

- [01-identity-access](01-identity-access.md)
- [02-facility-capacity](02-facility-capacity.md)
- [03-discovery-visualization](03-discovery-visualization.md)
- [04-reservations-allocation](04-reservations-allocation.md)
- [05-sessions-operations](05-sessions-operations.md)
- [06-pricing-payments](06-pricing-payments.md)
- [07-monthly-passes](07-monthly-passes.md)
- [08-notifications-reporting](08-notifications-reporting.md)
- [10-ai-assistance](10-ai-assistance.md)

Each original FR/BR/UC/US ID is retained; current status is separate from source approval labels. Matrix edges are requirement associations, not executed coverage results. [Acceptance scenarios](../../04-testing/requirements-traceability/acceptance-scenarios.md) are all NOT RUN.


## RC2 additions

The RC2 amendment added security IDs. Current definition inventories are maintained in the SRS modules, business-rule files, use-case index and story catalogue; the [v1.1 manifest](../srs/baselines/v1.1/manifest.md) records verified counts. Product tests remain NOT RUN.


## Source namespaces and implementation evidence

The v1.1 decision CSV/Markdown pair maps all V11-01–12 and D01–D12 from the dated 20 October 2026 register. Earlier RC1 D IDs are a different dated namespace (for example original D01 concerns deposits); they remain historical authority for unchanged behavior. Requirement, BR, UC, story and AC identifiers remain unique within their canonical catalogues. Repeated references in views are not duplicate definitions.

Each v1.1 row links canonical requirements → API/architecture → acceptance and named owner workstream/action. Implementation commit/build and execution evidence are **PENDING/NOT RUN** for every new decision; a documentation diff is not an implementation commit.
