# Consolidated v1.0 overview documents — 14 October 2026

## Basis and purpose

User requested project-wide SRS and solution architecture entry points that summarize the direction and link to existing module/detail files. Source: `docs` branch at [923e9c7](https://github.com/Quangthm/Mock-Project-Smart-Parking/commit/923e9c783c77f1feb75440cbc79d8422c0b03c16). Current RC3 remains the requirements review candidate; the v1.0 titles are document versions, not release sign-off.

## Changes

| File | Action and purpose |
|---|---|
| `docs/01-requirements/srs/smartpark-srs-v1.0.md` | Add consolidated product overview, role scope, journeys, key rules, quality profile, deferrals and detailed reading links. |
| `docs/02-architecture/solution-architecture/smartpark-solution-architecture-v1.0.md` | Add target architecture overview, six service boundaries, layering, communication/data principles, critical workflows and implementation reading links. |
| `docs/README.md` | Add the two main entry links; preserve existing RC3 guidance. |
| `docs/01-requirements/srs/README.md` | Add the consolidated SRS entry link above the module index. |
| `docs/02-architecture/README.md` | Replace stale Kafka overview with already-confirmed RabbitMQ/six-service direction and current navigation; identify v0.9 as historical. |
| This decision/change record | Record purpose, provenance, scope and validation. |

## No new product decisions

No detailed requirement, rule, policy value, permission, API/schema, code or acceptance result is modified. RC2 TOTP/recovery and RC3 payOS decisions remain in force. All original FR/BR/UC/US IDs and the 72 NOT RUN acceptance cases remain untouched. Older architecture v0.9 is not deleted or rewritten; current navigation identifies its status. No branch push, commit, PR or deployment is performed by this deliverable.

The architecture index correction applies existing SB-01/RabbitMQ decisions; it is not a new broker selection. Summaries describe the approved target, not an implementation audit. Checked the UserService Domain project target (`net10.0`); no assertion that every service is fully aligned.

## Validation

Reviewed current module summaries, permission and state/recovery contracts, policy catalogue, ownership/media/payOS contracts, deferred register and acceptance profile from the pinned branch. Checked all local Markdown file links in the six delivered files against the repository tree plus new paths. Provided exact diffs for the three replaced index files and before/after hashes in the delivery inventory. Unchanged detail files are not included in the overlay.

The source manifest/release status is not promoted by this publication layer. Update formal release/integrity records when the team actually publishes its chosen revision; do not reuse old ZIP-package checksums as repository-wide acceptance evidence.
