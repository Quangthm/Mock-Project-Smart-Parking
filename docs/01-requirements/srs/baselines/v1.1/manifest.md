# SmartPark v1.1 documentation review candidate manifest

**v1.1 documentation review candidate based on approved decisions.** SPARK-296; prepared 20–22 October 2026 (Asia/Saigon). Documentation sign-off, API agreement, implementation and product acceptance remain separate gates. No signed v1.0 release is assumed.

- [Approved revision 0.2 decisions](../../../../05-project-management/decision-log/2026-10-20-approved-v1.1-changes.md)
- [Decision coverage and pending owner/actions](../../../requirements-traceability/v1.1-decision-coverage.md)
- [Documentation index](../../../../README.md)
- [Validation record](../../../../05-project-management/VALIDATION.md)
- [Historical v1.0 RC3 manifest](../v1.0/manifest.md)

## Source and review base

Work remains on `docs/SPARK-296-v1.1-baseline-update`; eventual PR target is `develop`. Initial clean HEAD and local develop were `c9a1fc6d7114e3d0b23ba72dc1788e2043004d3a`, matching the handoff. During resumed work, the checkout was externally advanced to `327420dbda48a59c0c1ed4d9c3f1e5f93d485084` and the existing edits were staged. Incoming database-name corrections are preserved. No branch switch, commit, push or reset was performed by this task.

The review package uses the latter SHA as its effective pre-edit base, captured separately from the unchanged original snapshot. The package manifest records both snapshots and the overlap resolution. The patch contains SPARK-296 documentation differences against this effective base; incoming application/setup changes are excluded.

## Canonical inventory

| Catalogue | Definitions |
|---|---:|
| Functional requirements | 135 |
| Business rules | 57 |
| Monthly/yearly product M rules | 9 |
| Use cases | 41 |
| User stories | 46 |
| Acceptance scenarios | 113, all NOT RUN |
| v1.1 decision coverage rows | 24 |

Existing FR/BR/M/UC/story identifiers are retained. AC-073–113 are new unexecuted specifications; AC-008 and AC-060 are revised unexecuted specifications. Historical test results, screenshots and prior decision evidence retain their original meaning. AC-113 covers the [21 October V11-06 GUID clarification](../../../../05-project-management/decision-log/2026-10-21-vneid-simulation-guid.md).

## Integrity and delivery

[SHA-256 inventory](sha256.csv) records exact working-file bytes for added/modified documentation in this candidate, excluding this manifest and the hash inventory itself to avoid circular hashes. The external package manifest records every overlay file, including these two files, with before/after hashes and status. Hashes describe document integrity, not software verification.

Artifacts live in the ignored `.cache/SPARK-296/delivery/` directory: docs overlay ZIP, patch, apply instructions, change manifest, validation report and evidence log. Use either overlay or patch, never both. No SQL, application code, executable test collection, database operation, deployment or Jira update is part of this candidate.

The `smartpark-srs-v1.0.md` and `smartpark-solution-architecture-v1.0.md` filenames remain stable compatibility paths to canonical v1.1 documents. Superseded API implementation material is retained as explicitly non-authoritative historical evidence, with active navigation to the reviewed target contracts. The package manifest enumerates any additions/removals/renames; copying an overlay cannot delete a file.
