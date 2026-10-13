# Acceptance scenarios — RC1

**ALL NOT RUN.** This file specifies expected outcomes; documentation checks are not product acceptance. Execute with actual build/commit, machine/resources, dataset distribution, provider/fixture modes, timestamps and retained evidence.

| Case | Decision | Scenario | Expected outcome | Result |
|---|---|---|---|---|
| AC-001 | D01 | Booking100000, deposit30000, completed short visit | Minimum100000 survives; credit30000 once; collect70000, excluding separately disclosed extras | NOT RUN |
| AC-002 | D01 | Same booking no-show with30000 collected | No additional70000 debt; no-show refund0 under default | NOT RUN |
| AC-003 | D01 | Cancel exactly30 / exactly15 minutes before start | 100% /50% of eligible collected money, not full unpaid booking price | NOT RUN |
| AC-004 | D01 | Positive booking with tiny percent deposit rounding0 | Validation; cannot silently confirm as no-payment | NOT RUN |
| AC-005 | E01 | Final125000, collected300000; duplicate checkout/refund commands | One175000 surplus obligation; commercial checkout may complete before payout | NOT RUN |
| AC-006 | E01 | Online refund unsupported or timeout | Visible pending/reconciliation; no silent cash fallback or duplicate refund | NOT RUN |
| AC-007 | E01 | Original cash overcollection | Audited cash return, payout proof independent of charge-covered settlement | NOT RUN |
| AC-008 | E02 | Owner creates Operator, verifies and assigns own lot | No Admin per-hire approval; current scoped access; cross-owner/self-promotion rejected | NOT RUN |
| AC-009 | E02 | Revoked Operator replays previous private request/command | No new protected access/effect; historical attribution retained | NOT RUN |
| AC-010 | E03 | Full monthly-covered booking under fixed-deposit mode | Explicit zero-payment confirmation; one compatible claim; no provider success fabrication | NOT RUN |
| AC-011 | E03 | Partial monthly booking coverage | Covered ordinary fees excluded before minimum; positive balance uses chosen prepayment | NOT RUN |
| AC-012 | A-01 | Cancel pass with future zero-priced dependent booking | List blockers; Driver must explicitly cancel booking before pass cancellation completes | NOT RUN |
| AC-013 | A-01 | Concurrent dependent booking and voluntary pass cancellation | One serialized winner; cancellation blocked or stale coverage quote rejected; no unpriced entitlement | NOT RUN |
| AC-014 | A-01 | Driver rebooks after dependency/pass cancellation | Fresh ordinary price/confirmation and capacity check; no guaranteed old resource | NOT RUN |
| AC-015 | A-01 | Temporary/permanent closure with dependent bookings | Separate approved failure handling; no forced voluntary prerequisite or invented unpaid penalty | NOT RUN |
| AC-016 | B03 | Ordinary session15:00 /15:01 | Inclusive exemption at15:00; full applicable interval charged above threshold | NOT RUN |
| AC-017 | B03 | Two8-minute sessions;20-minute midnight-spanning session | Each8-minute ordinary session free; midnight20-minute session evaluated once | NOT RUN |
| AC-018 | D04 | 60 covered+10 uncovered minutes | Whole session exceeds15; uncovered10 charged; covered60 never charged | NOT RUN |
| AC-019 | B03 | Unrounded2499.50 /2500 VND | 2000 /3000; no intermediate rounding | NOT RUN |
| AC-020 | B04 | Checkout10:00, payment10:03, departure10:05 | Billing cutoff10:00; occupancy released10:05 only | NOT RUN |
| AC-021 | B04 | Unpaid recovery during UNKNOWN payment | Block blind invalidation until reconcile; no new grace after valid recovery | NOT RUN |
| AC-022 | B04 | Paid visit requested reopen for longer stay | Reject paid reopen; actual exit/reentry needed for new session | NOT RUN |
| AC-023 | B04 | Paid20000, corrected15000 then duplicate correction | One5000 refund; recompute at original cutoff; original transaction retained | NOT RUN |
| AC-024 | B01 | Concurrent last-slot requests and duplicate create | One compatible winner; loser conflict; replay no second claim | NOT RUN |
| AC-025 | B01 | 10slots/3occupied/2backup/1overlap | 6 available before other claims; occupied backup counted once | NOT RUN |
| AC-026 | B01 | Physical unknown/projection expiry or maintenance while occupied | No automatic release; preserve occupied fact; block unsafe allocation | NOT RUN |
| AC-027 | D02 | Cap disabled versus enabled at limit; concurrent create | Disabled differs from zero; lot/account pending+confirmed+allocated count atomically | NOT RUN |
| AC-028 | D05 | Alternative acceptance one tick before /exact expiry | Before succeeds if guarded; equality expired; underlying right separate | NOT RUN |
| AC-029 | D06 | Arrival at cutoff; delayed authoritative proof after no-show | Timely; audited correction, compatible recovery/no displacement or failure handling | NOT RUN |
| AC-030 | DEF12 | Ordinary payment succeeds after hold expiry, capacity free/taken | Atomic reacquisition / reconciliation-refund, never conflicting resurrection | NOT RUN |
| AC-031 | M01 | Jan31→Feb28/29→Mar31 | Original anchor/timezone retained; not30-day period | NOT RUN |
| AC-032 | M02 | Monthly paidAt beforeH but entitlement commit atH | No activation; reconcile/refund; no backdating | NOT RUN |
| AC-033 | M04 | Available-only pass at full lot/outside/expiry while parked | No guaranteed entry; outside deduction0; expiry preserves occupied fact | NOT RUN |
| AC-034 | M05 | Multiple paid-period cancellation refunds and rounding cap | Per-period actual unused fraction/rate; sum then round once; remaining collected-money cap wins | NOT RUN |
| AC-035 | M06 | Temporary closure overlaps later permanent closure/cancellation | Union verified intervals; compensate once; temporary retains end, permanent ends remaining coverage | NOT RUN |
| AC-036 | B08 | Each of4roles email-only/phone-only verified OTP | Both independently work in declared modes; missing/unverified channel rejected; no both-required gate | NOT RUN |
| AC-037 | B08 | Refresh rotation near7-day absolute or24-hour inactivity | No lifetime extension; expired/inactive session cannot refresh | NOT RUN |
| AC-038 | B09 | Admin reads conversation near90-day last-message expiry | No retention reset; authorized hold separately handled | NOT RUN |
| AC-039 | B09 | Hold placement races deletion and other hold release | Owner acknowledges only preservable asset; other holds survive; missing evidence honest | NOT RUN |
| AC-040 | B09 | Provider upload success/DB failure; delete timeout | Reconcile stable asset; no false attachment/deletion completion; due access restricted | NOT RUN |
| AC-041 | PRIV | Cross-account/lot report, evidence, export, SignalR | Denied/redacted consistently; Admin no blanket evidence privilege | NOT RUN |
| AC-042 | ABUSE | Cancels10:00,10:12,10:20; command replay | Rapid expiry13:12 then13:20; replay not recounted | NOT RUN |
| AC-043 | ABUSE | Fifth23:00 also rapid3h; existing cancellation next | Restricted until02:00; existing eligible cancellation still allowed; no login lock | NOT RUN |
| AC-044 | RETRY | Provider timeout, duplicate callback, late failed callback after success | UNKNOWN reconciliation; one effect; success not erased by failure callback | NOT RUN |
| AC-045 | RETRY | Two concurrent refunds/corrections | Reserve balance including unknown in-flight; no double payout allocation | NOT RUN |
| AC-046 | RECOVERY | Crash after local commit before publish, then redelivery | Durable outbox/inbox recovers once-only business effects; no lost acknowledged facts | NOT RUN |
| AC-047 | AI | Assistant stale quote/missing parameter/auth failure | Clarify or route ordinary UI; no invented success or autonomous financial/security write | NOT RUN |

## Local load/recovery recipe

| Integration | Approved local profile | Evidence / limit |
|---|---|---|
| Payments | One real sandbox adapter, **VNPay first candidate**; deterministic MoMo/ZaloPay fixtures; tested Operator cash collection | Keep all named channel directions, but label mode in acceptance report. Actual sandbox availability/credentials must be verified; do not silently substitute fixtures for the one sandbox gate or claim three real integrations. |
| Media | Evaluate Cloudinary adapter for backend upload, restricted reads, holds, deletion and recovery; fixtures for deterministic failures | Provider capability and account limits unverified until tested. No production/free-tier suitability claim. |
| Notifications | Local email capture and SMS fixtures; real delivery tests separately labelled if configured | No proof of external delivery from local capture. Applies to all-role OTP flows. |
| Hardware/gate | Deterministic events and Operator workflows | No real adapter or physical device acceptance. |
| Maps | Manual location fallback and deterministic coordinates for core tests; configured provider connectivity tested separately | No GPS-only dependency or claim of provider integration from coordinates alone. |
| Assistant/LPR | General contracts and manual/ordinary-UI fallback; detailed integration/evaluation deferred after v1.0 | No accuracy, model-completion or live-provider claim from fixtures. |
| Deployment | Docker local profile, one instance per required service, separate PostgreSQL databases/credentials, RabbitMQ and scoped SignalR | One PostgreSQL server permitted. No mandatory clustering or Redis scale-out. |

Three real payment sandboxes, live SMS/email, real hardware and production HA were alternatives, not selected local acceptance requirements. Local fixtures must be isolated from any real-money/live-device environment. UNKNOWN payment outcomes are reconciled rather than blindly recharged.

| Aspect | Approved target / recipe | Required test-plan detail |
|---|---|---|
| Candidate reference machine | 4 CPU cores and 16 GB RAM, subject to actual team equipment | Declare actual CPU/RAM/OS/container allocations, build and commit; different hardware is reported explicitly. |
| Seed scale | 10 lots; 1,000 represented spaces/category-capacity units; 10,000 users; 100,000 historical reservation/session records | Define distribution, active claims, vehicle categories and representative image sizes; do not double-count a represented slot and its pool projection. |
| Warm-up | 2 minutes | Exclude warm-up from steady-state percentile reporting. |
| Steady workload | 20 concurrent users for 10 minutes | Declare search/availability, detail reads and operational/reservation command proportions and pacing. |
| Burst | 50 concurrent users for 1 minute | Report separately from steady state; do not average away burst failures. |
| Core API latency | p95 ≤500 ms | Core read/write endpoint classes reported separately; external-provider work separately measured. |
| Unexpected errors | <1% | Expected business conflicts separated; report their frequency rather than hiding them. |
| SignalR freshness | p95 ≤2 seconds under healthy local profile | Define origin-commit-to-authorized-client-update measurement and report reconnect recovery separately. |
| Restart recovery | ≤5 minutes | Preserve acknowledged local transactions and durable recovery intents; demonstrate reconciliation without duplicate financial/capacity effects. |
| Backup schedule / RPO | Daily backup; backup RPO ≤24 hours | Backup-loss tolerance is separate from ordinary-restart durability. Failed/overdue backup must be visible. |
| Restore | ≤30 minutes | Restore into declared clean local target and verify core data/relationships and recoverable workflows. Define recovery dataset/configuration scope. |

No incompatible double capacity commitment, duplicate collection/refund or unauthorized access is acceptable merely because aggregate latency/error targets pass. Scope provider upload/payment/AI latency separately. Record sandbox versus fixture mode and actual timings; unexecuted tests stay NOT RUN. These are local mock-project objectives, not production SLOs or claims of achieved throughput.

Delivery team must choose and record workload mix/pacing, actual hardware, seeded distribution, provider capability checks and fault injection. Ordinary conflicts are counted separately, not hidden as success. Product invariants have zero tolerance for incompatible double capacity, duplicate money and unauthorized access even if aggregate performance passes. Any concrete required-gate exception returns to PM-04 with evidence; no preapproved fixture substitution for the one-real-sandbox gate.


## SEC-01/SEC-02 additional acceptance — NOT RUN

Use a real local TOTP implementation with documented security settings and clock tests; email capture/SMS fixtures remain separately labelled.

| Case | Scenario | Required outcome | Result |
|---|---|---|---|
| AC-048 | Every role enrolls optionally | No role is forced to enroll; phone-only verified account can enroll with backup codes; invalid pending proof never enables. | NOT RUN |
| AC-049 | Password/email OTP/phone OTP login with enabled TOTP | All primary paths stop at restricted challenge; no normal token/API/SignalR until TOTP proof. | NOT RUN |
| AC-050 | Expired/replayed TOTP or simultaneous same time step | Reject expired/stale/reused proof; one winner, no extra session from command replay. | NOT RUN |
| AC-051 | Voluntary disable | Recent primary plus current TOTP required; invalidate codes/contexts/sessions; notification and audit. | NOT RUN |
| AC-052 | Backup-code concurrent redemption and timeout | One consumption/recovery intent; same-operation lookup, no duplicate recovery session. | NOT RUN |
| AC-053 | Failed/abandoned backup replacement | Old factor remains; consumed code cannot be reused; no silent disable. | NOT RUN |
| AC-054 | Successful backup replacement | Old secret/codes/contexts/sessions invalid; fresh backup set; no plaintext logs. | NOT RUN |
| AC-055 | Regenerate codes; interrupted display | Old set invalid; no plaintext retrieval or multiple active sets; failure visible and authenticated regeneration available. | NOT RUN |
| AC-056 | Pre-login recovery for existing/nonexistent account | Generic response; rate controls; no account existence/contact disclosure or normal session. | NOT RUN |
| AC-057 | Evidence missing, only OTP, or no existing channel | Keep pending/escalated; no automatic reset/contact rebinding; no new national-ID requirement. | NOT RUN |
| AC-058 | Admin authorizes password only | User chooses password; Admin cannot read/set known password; TOTP remains; old sessions revoked on mutation. | NOT RUN |
| AC-059 | Admin TOTP/BOTH reset; grant delivery failure | Old factor/codes/sessions revoked; REPLACEMENT_REQUIRED blocks all ordinary login; no success before required new proof/password completion. | NOT RUN |
| AC-060 | Admin resets own account or Owner resets Operator | Reject prohibited privilege; different Admin required for Admin target. | NOT RUN |
| AC-061 | No other Admin | Use controlled verified maintainer procedure; if unavailable or verification insufficient remain pending; no public bypass. | NOT RUN |
| AC-062 | Login races reset; old JWT and refresh after change | Current security/factor version enforced; pre-change session cannot authorize APIs or SignalR. | NOT RUN |
| AC-063 | Duplicate Admin command or support timeout | One mutation; query same operation; approved ticket is not completed reset; durable recovery retains outcome. | NOT RUN |
| AC-064 | Blocked account/Owner pending approval after recovery | Credential recovery does not unblock, approve or grant rights. | NOT RUN |
| AC-065 | Evidence/notification export and log inspection | Scoped access and redaction; no secrets/QR/codes/tokens in log/event/export; delivery failure separate from mutation. | NOT RUN |
