# SPARK-340 — F-A/F-B/F-C follow-up

Date17/11/2026. Baseline8ca61a609f07fb1acec8a9654d9944fbd50309db, branch feat/SPARK-340-admin-platform-policies. The independent F6–F8 report remains unchanged. This follow-up implements its three P3 findings under the already-approved binding design. No additional schema, shared DB import/DDL, Docker restart or SPARK-273 orchestration change.

## Repairs

**F-A — closed current-key verification bypass.** PayOsAdapter always reads the per-order signing binding for a webhook. Options.ChecksumKey is never a verification fallback, including direct construction without settings. Real checkout creation now requires settings authority and binding persistence regardless of optional constructor arguments. Options-only local emulation remains available for the existing zero-outbound fixture path. Cancel/status configuration still uses its configured credentials; this change does not claim that every options read was removed.

Regression covers a current-key-signed webhook without a binding (rejected with/without binding service), an exact older bound key (accepted while current options key is rejected), missing settings/bindings (zero outbound), and actual Infrastructure DI resolution. Earlier positive webhook tests now explicitly seed an authoritative binding rather than relying on the removed shortcut.

**F-B — validate association from persisted order.** PayOsOrderCodes.Resolve is the single issuance mapping used by checkout and repository. It preserves positive numeric codes and the existing deterministic little-endian Guid fallback, with an explicit safe magnitude for Int64.MinValue. Nonpositive numeric codes are rejected. The repository reads the authoritative order, rejects a mismatching provider code even with a valid caller-supplied PaymentOrderId, and conditionally inserts against the same observed order ID/local code. Read-back checks parent identity/code. A concurrent local-code change therefore cannot silently pass the validation-to-insert boundary.

Supported checkout/import/migration writers share this repository. This is not a new cross-table database trigger or protection against arbitrary privileged out-of-band DML. No new DDL/constraint is introduced by this follow-up; the original approved table/FK/index remain in place. PG regression proves foreign provider code rejection before the first row exists, valid numeric association and the existing canonical Guid fallback case.

**F-C — inventory continues after unsupported rows.** Inventory now returns PaymentOrderId, OriginalOrderCode, nullable OrderCode, ProviderLinkId and State. Nonnumeric/nonpositive/noncanonical codes receive UNSUPPORTED_ORDER_CODE and OrderCode=null; valid subsequent rows are still inspected. Positive numeric fields preserve their previous meaning. Exit0 means the report completed, not that every row is ready. Unsupported/missing bindings remain explicit rollout actions. This does not silently repair the pre-existing nonnumeric local-order webhook lookup limitation.

## Actual verification

| Gate | Result |
|---|---|
| Focused adapter/authority/inventory |22passed/0failed after test-harness correction. |
| Full solution build --no-restore |0errors/29warnings, including previously observed dependency/advisory-feed warnings. |
| Full BE --no-build |**1190passed/0failed/114skipped/1304total**; Payment335/12skip, Operation71/3skip. |
| Real PG Payment |**9passed/0failed/0skipped**, generated temporary DBs only. |

[Sanitized evidence](evidence/SPARK-340-FA-FC-verification-20261117.json) records each command and actual counters. PG includes real CLI mixed-inventory JSON assertions, preservation of original identity, continuation to valid rows, mismatch-before-insert, original repair/migration/restart/order/retention/concurrency cases and distinct backend PID at outbound.

One initial DI regression failed because the service's named-client configuration overrode the test's defaults-level fake handler. That failed attempt did not reliably isolate outbound HTTP and may have attempted the configured origin with synthetic credentials. It used no real credentials or user DB state. The harness was corrected to post-configure every client's primary handler; the positive DI case then proves an intercepted call and the negative case proves zero calls. All final reruns use the isolated fake handler. No failing result was relabeled PASS.

No FE implementation or collection change in this follow-up, so their prior33-test result remains historical rather than a new run. Newman58/live DDL/import/restart/user UAT remain NOT RUN here. Remaining P4-level findings from the independent report, deferred UAT and U19/SPARK-273 retain their status. Independent re-review and actual rollout remain pending.

## Handoff

Use [updated reviewer prompt](SPARK-340-F6-F8-reviewer-prompt.md) and [rollout guide](SPARK-340-F6-F8-rollout-guide.md). Existing target DB still requires the previously approved additive script before the new Payment binary. Inspect every inventory row state before rollout, including unsupported rows. Source changes can be reviewed/tested without mutating the user's stack. No release-ready or full-story acceptance claim is made by these automated results.
