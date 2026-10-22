# Testing

## Run and verify

- [Local setup and application runners](../../LOCAL-SETUP.md).
- [Automated PostgreSQL and RabbitMQ tests](../../tests/README.md).
- [Database integration and deployment baseline](../02-architecture/database/schema-integration.md).
- [Current API design](../02-architecture/api/SmartPark-All-API-Design.md) and [Operator activation contract](../02-architecture/api/operator-provisioning.md).

## Manual QA and evidence

- [Demo user journey test cases](test-cases/demo-user-journey-2026-10-15.md) and [button-by-button rehearsal](demo-review-2026-10-15/QUY-TRINH-BAM-TEST-DEMO.md).
- [Assigned-workflow API handoff](tester-api-handoff-2026-10-08.md) and [Postman collection](postman/assigned-workflows-2026-10-08.postman_collection.json). The dated handoff is a collection companion; use the current API design when contracts differ.
- [MFA QA recheck, 20/10/2026](MFA_QA_RECHECK_2026-10-20.md): focused regression evidence and pending tester follow-up.
- [Acceptance scenarios and traceability](requirements-traceability/acceptance-scenarios.md).
- [Payment integration test plan](payment-integration/payos-test-plan.md).

`evidence/` and `reports/` retain QA artifacts; `test-cases/` and `test-plans/` contain reusable test documentation. Dated evidence applies only to the reviewed code and environment. Obsolete standalone implementation reviews and push notes can be retrieved from Git history.

Test documentation must trace back to the current SRS baseline and preserve
unfinished requirements as unfinished rather than inventing expected behavior.

Do not turn a future/deferred feature into a mandatory MVP test without an
approved scope change.
