# Testing

## Run and verify

- [Local setup and application runners](../../LOCAL-SETUP.md).
- [Automated PostgreSQL and RabbitMQ tests](../../tests/README.md).
- [Database integration and deployment baseline](../02-architecture/database/schema-integration.md).
- [Current API design](../02-architecture/api/SmartPark-All-API-Design.md) and [Operator activation contract](../02-architecture/api/operator-provisioning.md).

## Manual QA and evidence

- [SPARK-327 JWT/JWKS Postman guide](postman/SPARK-327-JWT-JWKS-guide.md): importable collection and blank local environment, with explicit restart, MFA and expiration checkpoints.
- [SPARK-327 completed testing and token-handoff resolution](reports/SPARK-327-postman-token-handoff-2026-11-06.md): user acceptance, v1/v2 comparison, diagnostic evidence and prevention guidance; exact original Desktop mechanism remains unproven.
- [Consumer startup outage check](../../tests/startup/consumer-startup.test.cjs): after building the solution, run `node --test tests/startup/consumer-startup.test.cjs` to check Facility/Operation/Payment liveness with UserService unavailable. This does not verify consumer JWT authentication, which remains unimplemented.
- [Demo user journey test cases](test-cases/demo-user-journey-2026-10-15.md) and [button-by-button rehearsal](QUY-TRINH-BAM-TEST-DEMO.md).
- [Assigned-workflow API handoff](tester-api-handoff-2026-10-08.md) and [Postman collection](postman/assigned-workflows-2026-10-08.postman_collection.json). The dated handoff is a collection companion; use the current API design when contracts differ.
- [MFA QA recheck, 20/10/2026](MFA_QA_RECHECK_2026-10-20.md): focused regression evidence and pending tester follow-up.
- [Acceptance scenarios and traceability](requirements-traceability/acceptance-scenarios.md).
- [Payment integration test plan](payment-integration/payos-test-plan.md).

`evidence/` and `reports/` retain QA artifacts; `test-cases/` and `test-plans/` contain reusable test documentation. Dated evidence applies only to the reviewed code and environment. Obsolete standalone implementation reviews and push notes can be retrieved from Git history.

Test documentation must trace back to the current SRS baseline and preserve
unfinished requirements as unfinished rather than inventing expected behavior.

Do not turn a future/deferred feature into a mandatory MVP test without an
approved scope change.
