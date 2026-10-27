# payOS integration acceptance plan — RC3

**All tests NOT RUN.** [PAY-01](../../05-project-management/decision-log/2026-10-13-payos-priority-pay-01.md) replaces the sandbox gate with controlled low-value payOS production tests plus deterministic failure/race fixtures. This is a plan, not transaction authorization for this document-writing session or a claim of implementation.

## Execution preparation

Record actual build/commit, tester, date/time, account/payee mapping, selected API/SDK version and bank arrangement, callback endpoint, payment/refund correlation and evidence location. The responsible team sets explicit low-value amounts and a bounded aggregate budget with the test account holder; no numeric business limit is invented here. Keep secrets and private bank data out of committed reports. Separate real-money credentials/data from simulation and load/fault testing. Use fixtures for destructive/time-controlled races; do not conduct a load test against production payment endpoints.

## Evidence split

| Track | Required evidence | Limit |
|---|---|---|
| Controlled real payOS integration | Actual low-value order/link, verified collection callback, query reconciliation, matching transaction/amount/payee, one credited ledger outcome and appropriate domain fulfillment | Provider integration cannot be established by screenshots, fixture callbacks or link creation alone. |
| Real refund capability | Capability assessment plus actual payout evidence for the supported approved refund route, linked to collected money; demonstrate partial/surplus handling where supported | If unsupported/unexecuted, record BLOCKED/NOT RUN and pending obligations; no cash substitution or fixture-only refund-delivery claim. |
| Deterministic adapter/workflow fixtures | Signature/identity mismatch, callback replay, UNKNOWN creation/collection/refund, cancellation race, late money, concurrent last capacity/refunds, recovery after crash | Demonstrates SmartPark rules, not provider behavior or actual money movement. |
| Native provider options | Per-provider mode: real/sandbox/fixture/deferred, with evidence for any claimed implementation | payOS scanning cannot count as native VNPay/MoMo/ZaloPay completion. |
| Operator cash | Authorized collection and original-cash refund auditing; UNKNOWN online-payment guard | Existing cash acceptance remains required. |

Reuse AC-001–007, AC-010–011, AC-020–023, AC-030, AC-032, AC-044–046 with appropriate fixtures. Use AC-066–072 for PAY-01 specifics in the [canonical scenarios](../requirements-traceability/acceptance-scenarios.md). Low-value real test data must respect existing pricing/rounding rules; the 300,000/125,000 example may be exercised in deterministic tests without requiring those real amounts.

## Result recording and gate

Report each case with mode, expected/actual outcome, identifiers, timestamps and redacted durable evidence. Distinguish specification PASS from application acceptance; current results remain NOT RUN. Required payOS collection evidence cannot be replaced by all-fixture results. A separate VNPay sandbox is no longer a required gate. Record real refund capability failures explicitly under PM-04 for scope/route resolution; do not close the gap by approving a new refund method within a test report. Preserve approved local performance targets; provider latency is measured separately.
