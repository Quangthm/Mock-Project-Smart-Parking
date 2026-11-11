# Reservation confirmation cutoff — final evaluation checkpoint

**Approved by the user on 10 November 2026.** This replaces the commit-before-T rule in D4c and the 9 November PostgreSQL-recorded commit-timestamp design. Other reservation rules and ownership boundaries remain unchanged.

## Current rule

For a booking starting at T, eligibility is decided using **one database timestamp captured at the final guarded backend checkpoint**, strictly before T. Verified Payment evidence (or genuine zero-price eligibility) must already be available; acquire the workflow/plate/site guards and load the booking plus locked Facility dependency facts before evaluating. All time-sensitive confirmation checks use the same `evaluatedAt`. The capacity implementation must use that supplied timestamp rather than reading another clock. No browser/provider timestamp can substitute for it.

On success, persist `confirmation_evaluated_at`, `confirmed_at` (the same decision time), stable confirmation effect ID, confirmed lifecycle, completed receipt, Facility completion outcome and outbox fact in the owning UoW transaction. `confirmed_at` is an eligibility decision timestamp, not a claimed physical COMMIT timestamp. Confirmation becomes externally visible only after successful durable commit. The local transaction requests synchronous commit; there is no deadline recheck at commit and no post-commit evidence activation stage.

At evaluation 10:59:59 for T=11:00, the transaction may finish at 11:00:02 and still qualify. At exactly 11:00 or later the evaluation fails. Waiting for locks past T cannot retain an earlier request-start timestamp. A prior live hold may expire before evaluation; reacquisition still requires compatible capacity at the checkpoint. A committed confirmation is not undone merely because H/T passes afterward.

Rollback leaves no confirmation/event/ready outcome. A new attempt captures a new database timestamp; it cannot reuse the timestamp from rolled-back work. Lost acknowledgement is recovered by the same workflow/booking/effect identity and committed receipt, without a second charge or confirmation event. An unknown commit result must be resolved before a new attempt; absence from a stale/read-replica query is not proof of rollback.

## Removed design

No `CONFIRMATION_PENDING` candidate lifecycle, transaction/cluster-ID proof fields, `pg_xact_status`/`pg_xact_commit_timestamp` lookup, commit-timestamp retention requirement or `track_commit_timestamp` Compose setting remains in the new path. This uses existing PostgreSQL clock/transaction facilities, not new SQL functions/triggers. The original Operations schema declares the final fields directly; no overlay or ALTER upgrade is supplied.

If an environment has already installed the abandoned candidate schema/state, inventory and reconcile its rows before deployment. CREATE IF NOT EXISTS cannot upgrade it, and the new worker cannot silently classify old candidates. No database reset, candidate conversion or removal of a server setting owned by another feature is authorized by this source change.

## Limits and verification

This simplifies the cutoff; it does not complete capacity/restriction checks, producer delivery or late-payment reconciliation. All required checks must succeed for the same guarded decision. Current unavailable adapters continue to prevent live sales. Raw SQL, bulk writes, direct DbContext saves and uncoordinated writers can bypass C# guard/immutability enforcement; validate supported writers in deployment.

Tests are updated but execution remains deferred by the user. Verify a transaction paused after successful evaluation and committed after T; a lock wait reaching T before evaluation; rollback and retry after T; lost commit acknowledgement; exactly-T rejection; paid/zero-price branches; and atomic receipt/event/dependency recovery. See the [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md) and [remaining workflow checklist](../handoffs/2026-11-10-reservation-completion-checklist.md).
