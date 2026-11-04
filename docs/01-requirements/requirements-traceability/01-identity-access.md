# Identity/access traceability — v1.1

Original IDs retained; SEC-01/02 adds5 FRs,4 rules,3 UCs and3 cross-role stories. Implementation/test coverage is not asserted.

| FR | Status | Module | Rules | Stories | Use cases |
|---|---|---|---|---|---|
| FR-AUTH-01 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-02 | US-D08, US-OW01 | UC-AUTH-01 |
| FR-AUTH-02 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-01, BR-AUTH-04, BR-AUTH-07 | US-A01, US-D08 | UC-AUTH-01 |
| FR-AUTH-03 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-03 | US-D08 | UC-AUTH-01 |
| FR-AUTH-04 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-01, BR-AUTH-02 | US-OW03 | UC-AUTH-03 |
| FR-AUTH-05 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-01, BR-AUTH-02, BR-AUTH-06, BR-PRIV-01, BR-PRIV-02 | US-A01, US-OW01 | UC-AUTH-02, UC-AUTH-06 |
| FR-AUTH-06 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-01, BR-AUTH-03, BR-PRIV-01 | US-A01 | UC-AUTH-02 |
| FR-VEH-01 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-VEH-01 | US-D09 | UC-VEH-01 |
| FR-VEH-02 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-VEH-02 | US-A04, US-D09 | UC-VEH-02 |
| FR-VEH-03 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-VEH-01 | US-D09, US-O01 | UC-VEH-01 |
| FR-VEH-04 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-CAP-01, BR-VEH-03 | US-D01, US-O01 | UC-VEH-01 |
| FR-BAS-08 | CONFIRMED — implementation pending / not assessed | 01-identity-access | BR-AUTH-02, BR-AUTH-04, BR-AUTH-07 | US-A01, US-D08 | UC-AUTH-01 |
| FR-AUTH-07 | CONFIRMED MVP — implementation and acceptance pending | 01-identity-access | BR-AUTH-04, BR-AUTH-07 | US-SEC01 | UC-AUTH-04 |
| FR-AUTH-08 | CONFIRMED MVP — implementation and acceptance pending | 01-identity-access | BR-AUTH-04, BR-AUTH-07 | US-SEC01 | UC-AUTH-01, UC-AUTH-04 |
| FR-AUTH-09 | CONFIRMED MVP — implementation and acceptance pending | 01-identity-access | BR-AUTH-05, BR-AUTH-07 | US-SEC02 | UC-AUTH-05 |
| FR-AUTH-10 | CONFIRMED MVP — implementation and acceptance pending | 01-identity-access | BR-AUTH-06, BR-AUTH-07 | US-SEC02, US-SEC03 | UC-AUTH-06 |
| FR-AUTH-11 | CONFIRMED MVP — implementation and acceptance pending | 01-identity-access | BR-AUTH-06, BR-AUTH-07 | US-SEC03 | UC-AUTH-02, UC-AUTH-06 |


## v1.1 decision and acceptance links

V11-01–04, V11-06–07, V11-11; D01–10, D12: [exact changed clauses, API/architecture contracts and acceptance scenarios](v1.1-decision-coverage.md). Existing IDs are retained; these associations describe requirements, not implementation/test completion.

## Authorization freshness amendment — 2026-11-04

FR-AUTH-06 and BR-AUTH-01/07 retain their IDs with revised freshness semantics; BR-AUTH-03 resource ownership remains mandatory; FR-AUTH-02/04 and recovery rules retain UserService local authority. See the [operation matrix](../srs/authorization-operation-classification.md), [ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) and [AF-001–AF-014 acceptance/coverage map](../../04-testing/requirements-traceability/authorization-freshness-scenarios.md). The map covers affected reservation, gate and plan use cases without inventing missing modify/reschedule APIs. Existing product permissions remain unchanged; no implementation/test completion asserted.
