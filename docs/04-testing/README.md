# Testing

Current tester suite (15/10/2026): [demo user journey test cases](test-cases/demo-user-journey-2026-10-15.md), [button-by-button rehearsal](demo-review-2026-10-15/QUY-TRINH-BAM-TEST-DEMO.md), and [working-tree change handoff](../05-project-management/handoffs/2026-10-15-changes-vs-6165024.md). Operator create no longer accepts a password; use the [updated API contract](../02-architecture/api/operator-provisioning.md). The journey suite distinguishes backend acceptance from local UI behavior and records missing end-to-end flows as blockers.

Tester handoff for the author's assigned tasks: [API guide](tester-api-handoff-2026-10-08.md), [Postman collection](postman/assigned-workflows-2026-10-08.postman_collection.json), and [push/database guide](push-and-database-handoff-2026-10-08.md).

Current fix/review: [all supplied tasks, 08/10/2026](cross-task-fix-review-2026-10-08.md), with [current workflow API/schema mapping](../02-architecture/api/workflows-2026-10-08.md). Dated reports describe the code at their review date.

Test documentation must trace back to the current SRS baseline and preserve
unfinished requirements as unfinished rather than inventing expected behavior.

Do not turn a future/deferred feature into a mandatory MVP test without an
approved scope change.
