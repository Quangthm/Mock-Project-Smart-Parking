# SPARK-272 final tester handoff

## VERIFIED

The recorded automated baseline is 109 distinct passing tests with zero outstanding failures or skips. Checkpoint 1 contributed 49 Operations tests and a passing API build, including two-connection read-only snapshot consistency. Checkpoint 2 contributed eight Facility candidate and affected regression tests with a passing Facility API build. Checkpoint 3 contributed 41 parking-search HTTP tests, nine chatbot response-contract tests, one Facility/Operations checkout-boundary test, and one Operations physical-state agreement test. The modified projects built. See [the task session](../../../ai/tasks/completed/TASK-SPARK-272-SESSION-20261127.md) for scope and exact results.

## NOT RUN

- Full Facility–Operations–Payment runtime integration with actual service identity, routing, publication, policy and price configuration.
- Manual execution of the [SPARK-272 Postman collection](../postman/SPARK-272-Parking-Search.postman_collection.json), including controlled owner failures and publication drift.
- Real slot selection and SPARK-273 reservation admission after a discovery result, including a search-to-booking race.
- CAR gate occupancy, located sessions, checkout and departure observations, including disagreement between Facility physical state and Operations sessions.
- Active zero quota, low quota, no active quota, buffered conflict, and maintenance beginning at the requested reservation end.
- Live chatbot current/interval, full/closed, unknown/stale, price-null, filtering and continuation conversations.
- Driver UI acceptance after SPARK-276 integrates the canonical endpoint.
- High-cardinality candidate batches near 100 sites and 2,048 registered slots, 512 KiB owner response bound and five-second deadline compliance.

## UNSUPPORTED

Motorcycle pooled availability has no exact count. Generic discovery does not prove account, canonical-plate, ownership, or vehicle-specific booking eligibility.

## KNOWN LIMITATION

Facility and Operations use independent service-owned snapshots. Their observations can change between search and admission. A general slot row update timestamp does not prove physical-observation freshness. Facility accepts at most 100 sites and 2,048 registered slots per candidate batch; an oversized enumeration fails instead of returning a partial count. Large-batch latency is unmeasured. Unlocated walk-ins consume no hypothetical named-slot claim under the approved model. The Driver frontend still uses its legacy discovery route pending SPARK-276. Final reservation admission remains authoritative.

These results establish backend readiness for manual acceptance. They do not establish live acceptance or deployment readiness.
