# PM actions — RC3

### 20.2 Decisions/actions still belonging to the project manager

| PM item / where | What still needs PM action | Recommended direction / when |
|---|---|---|
| **PM-01 — Final scope and requirements release**; §9, §10 closure criteria, §15.3 | Approve the synchronized v1.0 document set and disposition of any genuinely new clauses that remain after traceability review. This review alone is not the release. | BA/architect should first produce one concrete release candidate and a delta-only approval list. Keep all confirmed rules; map technical consequences to their approved parent decision; explicitly retain future exclusions. Do not reopen each historical “Approve” row as a new question if later decisions already settle its behavior. PM signs off the resulting manifest once the reviewable set exists. |
| **PM-02 — Delivery ownership, priority and dates**; §15.2, §§18–19 | Assign accountable people and target sprint/date for document propagation, schema alignment, implementation and acceptance evidence. Names/dates are not inferable from this review. | Sequence: synchronize requirements/permission/policy/state contracts → reconcile schema and develop → implement critical money/capacity/auth paths → execute local acceptance. Assign BA/architect to contracts, backend to domain workflows, QA to boundary/race cases; PM selects actual team members and dates. |
| **PM-03 — Published support availability**; §14.5, §15.1 | Supply actual project/demo support availability and responsible handler/rotation if this is exposed to users. | Retain asynchronous support; publish only availability the team can provide, without an invented production response SLA or 24/7 promise. This is an operational declaration, not a blocker to drafting domain contracts. |
| **PM-04 — Acceptance exception, only if a concrete blocker occurs**; §16.6–7 | Decide whether to change scope, defer a release gate or accept an explicitly documented deviation if the selected payOS/media integration or reference environment cannot meet the agreed acceptance profile. | Team first attempts and records evidence. Keep PAY-01 controlled low-value payOS production testing plus deterministic failure/race fixtures and honest mode labels. Do not preapprove fixture substitution, lower performance targets or claim real refund delivery from fixtures. No fresh PM decision is needed while the approved profile remains feasible. |

No new universal fee, numeric business limit, role override or financial penalty is needed merely to finish the current catalogue/matrix. If a technical author proposes a change to who can act, what money is owed, when rights expire, or which integrations count as accepted, escalate that concrete change to PM rather than burying it in an implementation detail.

A-01 is confirmed and applied. No new product question was found during this synchronization. The reviewable candidate is now available for PM-01; publication signature/date/commit remain unset. Actual implementation ownership/dates and published support availability are not inferred from previous team assignments.


## RC2 status

SEC-01/SEC-02 business choices are closed. Numeric security configuration and controlled last-Admin recovery implementation require team security/design review and test evidence. Assign an operational maintainer before that exceptional route is offered. Final requirements release sign-off remains pending; no new business-policy question is opened by this patch.

## RC3 status

PAY-01 is approved: payOS implementation priority, truthful provider identity and replacement of the sandbox gate by controlled low-value production tests plus deterministic failure/race fixtures. No further PM confirmation is needed to document those choices. Test account/custody, numeric test budget, refund capability and implementation evidence remain team execution/design work; a concrete scope or financial-workflow deviation returns to PM-04. Final release approval and actual delivery assignments remain pending.
