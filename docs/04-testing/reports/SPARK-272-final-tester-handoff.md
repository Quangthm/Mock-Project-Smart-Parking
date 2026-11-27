# SPARK-272 final tester handoff

## VERIFIED

The original implementation retains **109 distinct passing automated tests**. Checkpoint 1 contributed 49 Operations tests and a passing API build, including two-connection read-only snapshot consistency. Checkpoint 2 contributed eight Facility candidate and affected regression tests with a passing Facility API build. Checkpoint 3 contributed 41 parking-search HTTP tests, nine chatbot response-contract tests, one Facility/Operations checkout-boundary test, and one Operations physical-state agreement test. The modified projects built. See [the original task session](../../../ai/tasks/completed/TASK-SPARK-272-SESSION-20261127.md) for scope and exact results.

Enhancement A added **six distinct passing tests** for keyword/filter combinations, deterministic sort order and ranked cursor behavior. Its 41 directly affected parking-search regressions and nine chatbot contract checks passed again; these are existing tests, counted once in the original baseline. The Operations API built, and `git diff --check` passed.

Enhancement B added **six distinct passing tests**: two Facility tests against disposable PostgreSQL for published/withdrawn/unusable location and private JWT permission, three Operations tests against disposable PostgreSQL for confirmation ordering, Account isolation, fallback, owner response handling and read-only behavior, and one Operations HTTP authorization test for anonymous/wrong-role access. The directly affected existing Facility candidate-route authorization regression passed again and is not an additional distinct test. Facility and Operations API builds passed. The initial sandboxed VSTest runs **aborted before test execution** when the test host did not connect within 90 seconds; successful isolated PostgreSQL runs followed outside the sandbox with zero failures or skips. Aborts are not passes.

**Cumulative distinct automated passes: 121 = 109 original + 6 Enhancement A + 6 Enhancement B.** Enhancement C changes documentation only; no builds or automated tests were rerun. See [the enhancement session](../../../ai/tasks/active/TASK-SPARK-272-ENHANCEMENT-SESSION-20261127.md) for the cumulative record.

## NOT RUN

- Full Facility–Operations–Payment runtime integration with actual service identity, routing, publication, policy and price configuration.
- Manual execution of the [SPARK-272 Postman collection](../postman/SPARK-272-Parking-Search.postman_collection.json), including controlled owner failures and publication drift.
- Real slot selection and SPARK-273 reservation admission after a discovery result, including a search-to-booking race.
- CAR gate occupancy, located sessions, checkout and departure observations, including disagreement between Facility physical state and Operations sessions.
- Active zero quota, low quota, no active quota, buffered conflict, and maintenance beginning at the requested reservation end.
- Live chatbot current/interval, full/closed, unknown/stale, price-null, filtering and continuation conversations.
- Driver UI acceptance after SPARK-276 integrates the canonical endpoint.
- High-cardinality candidate batches near 100 sites and 2,048 registered slots, 512 KiB owner response bound and five-second deadline compliance.
- Full cross-service runtime for the historical-location read, including service JWT, publication, routing and unavailable/malformed Facility responses.
- [Enhancement Postman scenarios](../postman/SPARK-272-manual-test-guide.md): Vietnamese keyword equivalence, empty/oversized keyword, current and interval price ranking, availability ranking, zero/unknown/stale ranking, bound cursors, changed ranks and expiration.
- Browser location permission granted/denied/unsupported/timed out/unusable; historical fallback `data` versus `data:null` versus dependency failure; manual address/map selection overriding delayed automatic responses.
- Guest map integration, frontend keyword/sort/radius/cursor controls, and search-to-reservation acceptance through a real slot and fresh SPARK-273 admission.
- Large catalogue and candidate-batch performance, price/availability ranking latency, large reservation-history lookup performance and the existing five-second search deadline.

## UNSUPPORTED

Motorcycle pooled availability has no exact count. Generic discovery does not prove account, canonical-plate, ownership, or vehicle-specific booking eligibility. The guest-facing map and frontend historical fallback are not yet implemented.

## KNOWN LIMITATION

Facility and Operations use independent service-owned snapshots. Their observations can change between search and admission. A general slot row update timestamp does not prove physical-observation freshness. Facility accepts at most 100 sites and 2,048 registered slots per candidate batch; an oversized enumeration fails instead of returning a partial count. The catalogue is capped at 1,000 sites, search radius at 50 km, and public owner work has a five-second deadline. Large-batch and ranking latency are unmeasured. Unlocated walk-ins consume no hypothetical named-slot claim under the approved model. Dynamic price/availability ranking can restart a continuation when relevant ordering or membership changes. Historical fallback requires the latest qualifying site's current publication and usable coordinates; it does not traverse older bookings. Its query returns one row but large-history ordering performance is unmeasured. The Driver frontend still uses its legacy discovery route pending SPARK-276. Final reservation admission remains authoritative.

The automated evidence supports backend handoff for further testing. It does not establish frontend acceptance, live user-story acceptance or deployment readiness.
