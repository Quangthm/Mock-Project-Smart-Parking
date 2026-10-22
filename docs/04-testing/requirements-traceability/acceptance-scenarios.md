# Acceptance scenarios — v1.1 documentation review candidate

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
| AC-008 | E02; V11-06 | Owner invites Operator; Operator verifies email and completes mandatory simulation, with or without password setup | Activation only after verified contact and simulation; password optional; no Admin per-hire approval; assigned-lot access only; cross-owner/self-promotion rejected | NOT RUN |
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
| Payments | **payOS QR first:** controlled, low-value production integration tests plus deterministic failure/race fixtures; tested Operator cash collection | PAY-01 replaces the one-real-sandbox gate. VNPay/MoMo/ZaloPay retain separate planned identities and honest integration/fixture/deferred labels. Native VNPay may be simulated/deferred if resources are insufficient. Fixtures do not prove real collection/refund delivery; no claim that payOS implements all three providers. |
| Media | Evaluate Cloudinary adapter for backend upload, restricted reads, holds, deletion and recovery; fixtures for deterministic failures | Provider capability and account limits unverified until tested. No production/free-tier suitability claim. |
| Notifications | Local email capture and SMS fixtures; real delivery tests separately labelled if configured | No proof of external delivery from local capture. Applies to all-role OTP flows. |
| Hardware/gate | Deterministic events and Operator workflows | No real adapter or physical device acceptance. |
| Maps | Manual location fallback and deterministic coordinates for core tests; configured provider connectivity tested separately | No GPS-only dependency or claim of provider integration from coordinates alone. |
| Assistant/LPR | General contracts and manual/ordinary-UI fallback; detailed integration/evaluation deferred after v1.0 | No accuracy, model-completion or live-provider claim from fixtures. |
| Deployment | Docker local profile, one instance per required service, separate PostgreSQL databases/credentials, RabbitMQ and scoped SignalR | One PostgreSQL server permitted. No mandatory clustering or Redis scale-out. |

PAY-01 supersedes the earlier one-real-payment-sandbox requirement with controlled low-value payOS production tests plus deterministic failure/race fixtures. Three native real payment integrations, live SMS/email, real hardware and production HA are not required by this local acceptance profile. Local fixtures must be isolated from any real-money/live-device environment. UNKNOWN payment outcomes are reconciled rather than blindly recharged.

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

No incompatible double capacity commitment, duplicate collection/refund or unauthorized access is acceptable merely because aggregate latency/error targets pass. Scope provider upload/payment/AI latency separately. Record production, sandbox (if separately used) and fixture modes and actual timings; unexecuted tests stay NOT RUN. These are local mock-project objectives, not production SLOs or claims of achieved throughput.

Delivery team must choose and record workload mix/pacing, actual hardware, seeded distribution, provider capability checks and fault injection. Ordinary conflicts are counted separately, not hidden as success. Product invariants have zero tolerance for incompatible double capacity, duplicate money and unauthorized access even if aggregate performance passes. Any concrete required-gate exception returns to PM-04 with evidence; no preapproved fixture-only substitution for the required payOS production integration evidence.


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
| AC-060 | Sole seeded Admin requests assisted self-reset, or Owner resets Operator | Reject application-level self-approval/Owner reset; seeded Admin uses controlled maintainer recovery. Ordinary own security operations retain their proof requirements. | NOT RUN |
| AC-061 | No other Admin | Use controlled verified maintainer procedure; if unavailable or verification insufficient remain pending; no public bypass. | NOT RUN |
| AC-062 | Login races reset; old JWT and refresh after change | Current security/factor version enforced; pre-change session cannot authorize APIs or SignalR. | NOT RUN |
| AC-063 | Duplicate Admin command or support timeout | One mutation; query same operation; approved ticket is not completed reset; durable recovery retains outcome. | NOT RUN |
| AC-064 | Blocked account/Owner pending approval after recovery | Credential recovery does not unblock, approve or grant rights. | NOT RUN |
| AC-065 | Evidence/notification export and log inspection | Scoped access and redaction; no secrets/QR/codes/tokens in log/event/export; delivery failure separate from mutation. | NOT RUN |

## PAY-01 additional acceptance — NOT RUN

[Execution plan](../payment-integration/payos-test-plan.md). Use controlled low-value payOS production evidence where indicated; isolate deterministic simulations. These cases supplement, rather than replace, existing money/capacity/refund scenarios.

| Case | Scenario | Required outcome | Result |
|---|---|---|---|
| AC-066 | Checkout route identity and simulation/deferred options | payOS recorded as payOS; no silent native-provider substitution; only working routes in real-money checkout; demo simulations clearly labelled and isolated. | NOT RUN |
| AC-067 | Controlled low-value production payOS collection and callback/query | Authentic amount/order/currency/payee/transaction verified, credit once, appropriate owning-domain fulfillment; real evidence retained with secrets redacted. | NOT RUN |
| AC-068 | VNPay native adapter unfinished; MoMo/ZaloPay fixtures | Separate planned identities and actual modes visible; no separate sandbox gate; payOS does not claim three native integrations. | NOT RUN |
| AC-069 | Create timeout, callback replay and callback/query race | Reconcile stable operation; no blind fresh order or duplicate credit; existing retry bound preserved. | NOT RUN |
| AC-070 | Cancel/expiry races verified money; ordinary versus monthly late payment | Cancellation is not refund/no-money proof; ordinary compatible reacquisition or refund; monthly strict A<H preserved. | NOT RUN |
| AC-071 | Advertised payer application compatibility | Require tested app/flow evidence; retain actual payOS provider identity; unsupported/unverified apps not advertised as supported. | NOT RUN |
| AC-072 | Surplus refund via selected provider arrangement, unsupported or UNKNOWN result | One eligible obligation; payout proof required; unknown reserves balance; unsupported remains visible/pending; no silent cash/manual online-refund fallback. Record actual capability separately from fixtures. | NOT RUN |


## v1.1 acceptance — NOT RUN

Authority: [approved register](../../05-project-management/decision-log/2026-10-20-approved-v1.1-changes.md).
The original Decision column above uses the **v1.0 review register's** D01–D06 namespace (for example D01 is prepayment); below D01–D12 refer to **20 October 2026 revision 0.2**. They are distinct dated decision sources, not duplicate requirement IDs. AC-008 and AC-060 are unexecuted specifications updated for the approved v1.1 behavior; historical execution reports remain unchanged.

Technical design-pending values are prerequisites to execution, not invented fixtures or accepted defaults. Record build, implementation commit and evidence when cases actually run.

| Case | v1.1 decision | Requirements/rules | Scenario | Expected outcome | Result |
|---|---|---|---|---|---|
| AC-073 | V11-01, V11-04 | FR-AUTH-01, FR-AUTH-05 | Partner registration is submitted and retried | One pending User/Account/application, existing Owner role; Partner is UI/business terminology, not a new role. No normal Owner permissions. | NOT RUN |
| AC-074 | V11-02, V11-03, D07 | FR-AUTH-01, FR-LOT-01 | Attempt email OTP issuance/review before first-lot setup or review before simulation evidence, then complete prerequisites and retry | Server rejects premature steps; saved setup precedes OTP; required simulation and verified email precede review. Retry preserves initial application and first-lot identity. | NOT RUN |
| AC-075 | V11-06 | FR-AUTH-01 | Driver and Partner registration complete/decline/fail simulated VNeID | Explicit consent and simulated information required; decline/failure does not complete registration. No real government verification claim or email/review/MFA bypass. | NOT RUN |
| AC-076 | V11-06 | FR-AUTH-04 | Invited Operator follows email activation with and without a password | Verified email and successful mandatory simulation gate activation; password setup optional. Eligible verified-channel OTP login remains available through shared login/MFA gates. | NOT RUN |
| AC-077 | V11-06, D03 | FR-AUTH-05 | Seed exactly one Admin; attempt Admin registration and exempt other roles | Only seeded Admin exempt from simulation; no Admin registration route; Operators/Drivers/Owners cannot inherit exemption. | NOT RUN |
| AC-078 | V11-02 | FR-AUTH-05, BR-PRIV-01 | Admin replaces initial pending agreement, then attempts non-document edit, additional-lot edit and finalized edit | Only initial pending uploaded documents replaceable. Preserve original and each replacement with actor/time/reason. Other edits rejected. | NOT RUN |
| AC-079 | V11-02 | FR-AUTH-05 | Concurrent agreement replacement and approval use an old reviewed revision | Approval binds reviewed document/application version; stale review conflicts rather than approving unseen replacement. Final state/version immutable. | NOT RUN |
| AC-080 | V11-02, D10 | FR-AUTH-05, FR-RPT-01 | Approve after replacement; applicant does not perform in-app re-acceptance | Outcome is Approved and notification says “Your application was approved with some modifications”. No third outcome/corrections/re-acceptance workflow. | NOT RUN |
| AC-081 | V11-04, V11-07, V11-09 | FR-AUTH-05, FR-LOT-01 | Approval commits; provisioning times out/fails, is retried and receives duplicate outcome | Keep approved review plus visible pending/failed/unknown provisioning; one tenant/first lot; retry uses same identity. Failed provisioning is not rejection and Owner need not recreate first lot. User owns identity/organization/membership/review, Facility configuration, Operations accepted occupancy/claims/discovery/plan coverage, Payment financial terms and IoT raw observations/integrations; application snapshots do not transfer authority. | NOT RUN |
| AC-082 | V11-04, V11-07, V11-11, D07 | FR-AUTH-02, FR-BAS-08 | Owner login before review, during failed provisioning, after successful provisioning while lot is still unopened, and with enabled TOTP | Safe validated reasons block normal session until approval and provisioning success. Required MFA challenge remains; completed login returns necessary tokens, authorized profile retrieval is separate. | NOT RUN |
| AC-083 | V11-05 | FR-LOT-01 | Approved Owner submits additional lot; Admin rejects it | Separate review required; reject leaves Owner and other approved lots usable; no automatic lot approval and no initial-application document-edit permission. | NOT RUN |
| AC-084 | V11-05, D09 | FR-LOT-01 | Attempt to edit rejected additional-lot proposal, then Draft as new | Old record immutable; new proposal has distinct identity and independent review, linked provenance allowed. | NOT RUN |
| AC-085 | V11-05 | FR-LOT-01 | Draft identical rejected-lot content; retry one submission concurrently | Identical content allowed in a new proposal; retries of one request create only one application/site. | NOT RUN |
| AC-086 | V11-04, V11-05, V11-07 | FR-LOT-01, FR-LOT-04 | Approved/provisioned lot lacks required configuration or explicit opening | Activation/authorization alone does not open parking. Existing Facility/Operations readiness and scoped opening guards reject premature operations that require an open/ready lot. | NOT RUN |
| AC-087 | D01, D09 | FR-AUTH-05, FR-AUTH-06 | Admin re-enables a disabled account that also has pending/rejected review, revoked grants, expired sessions or booking restriction | Require authorization and reason; remove only administrative disable restriction. Do not verify contacts, approve/reverse rejection, revive sessions/grants or clear independent restrictions. | NOT RUN |
| AC-088 | D02, D03 | FR-AUTH-03, FR-AUTH-05 | Seeded Admin updates own profile/security, then self-disables/locks/deletes or changes roles | Ordinary own permitted profile/security operations work with proof; administrative authority-removing operations rejected. Authentication lockout still applies. | NOT RUN |
| AC-089 | D03, D04 | FR-AUTH-05, FR-AUTH-11 | Concurrent administrative operations target the sole/last Admin; recovery after auth lockout | No operation can remove last eligible administrative access; sole-Admin prohibition enforced atomically. Maintainer recovery only when required, with no public lockout bypass. | NOT RUN |
| AC-090 | D05 | FR-AUTH-05, FR-AUTH-06 | Attempt elevation/demotion or role-management calls | No MVP role-management feature/endpoint. Fixed assignment in approved signup/activation remains allowed and cannot be used for elevation. | NOT RUN |
| AC-091 | D06 | FR-AUTH-01, FR-AUTH-02 | Driver completes contact verification | No automatic normal session; Driver uses shared login and account/MFA gates. | NOT RUN |
| AC-092 | D07 | FR-AUTH-01, FR-AUTH-02 | Replay application ID, another applicant credential, expired setup credential and recovered credential | Application ID alone grants nothing; scope/owner/purpose/expiry enforced; secure recovery grants only approved setup/resend/verification, never normal session. Technical expiry follows reviewed contract. | NOT RUN |
| AC-093 | V11-04, D08 | FR-AUTH-01, FR-BAS-08 | Review Partner with supplied unverified phone and verified email; try phone OTP login | Email verification mandatory for approval; supplied phone need not be verified for review. Phone must be separately verified before phone login; either verified channel independently suffices after eligibility. | NOT RUN |
| AC-094 | D09 | FR-AUTH-01, FR-AUTH-05 | Reject initial Partner with reason; repeat registration using normalized same email and re-enable | Immutable rejection, safe email/login explanation after sufficient proof; repeat initial registration rejected. Re-enable does not reverse review; additional-lot Draft as new remains. | NOT RUN |
| AC-095 | D10 | FR-RPT-01, FR-BAS-06 | Approval email/notification delivery fails after business commit, then redelivery is retried | Business review/provisioning result persists; queued/pending/failed/delivered distinct; no second approval/account/site or notification-driven rollback. | NOT RUN |
| AC-096 | D11 | FR-LOT-01, FR-LOT-04 | FE multi-select requests same-lot batch deactivation with one invalid member | Return per-item validation errors; no selected item changes. Mixed-lot or unauthorized requests fail scope validation. | NOT RUN |
| AC-097 | D11 | FR-LOT-02, FR-LOT-04 | Deactivate parent with unselected descendants or unresolved bookings/occupancy; then explicitly resolve and retry | No silent cascade; require explicit descendant/dependency handling and Operations guards. Valid same-lot batch commits all members together and replay creates no extra effect. | NOT RUN |
| AC-098 | D12 | FR-VEH-01, FR-VEH-02 | Driver removes vehicle with active booking/session/pass dependency, then after dependencies handled | Block invalidation while dependencies remain; eligible removal is soft, retains association/visit evidence. Removal is not transfer approval; physical cleanup follows retention and holds. | NOT RUN |
| AC-099 | V11-10 | FR-MON-01, FR-MON-02, FR-MON-03, FR-MON-04 | Purchase yearly product across month-end/leap-year boundary | Twelve consecutive calendar months under separately reviewed yearly calendar-boundary terms, not fixed 365 days or twelve independently prepaid monthly periods; boundary examples verified against configured terms. | NOT RUN |
| AC-100 | V11-10 | FR-MON-03, FR-MON-09 | Purchase one yearly period ahead then a second; compare monthly maximum-three limit | One yearly period in advance allowed; second rejected. Monthly maximum three prepaid periods unchanged and not applied to prohibit yearly product. | NOT RUN |
| AC-101 | V11-10 | FR-MON-11, FR-MON-12, FR-MON-14 | Cancel yearly product with separately configured rate and dependent future bookings | Applicable coverage/dependency/rounding/balance invariants preserved; use accepted yearly policy, never assume monthly refund percentages or invent renewal defaults. Missing required policy blocks sale. | NOT RUN |
| AC-102 | V11-09, V11-10 | FR-MON-01, FR-PAY-01 | Owner enables monthly only, yearly only, both or neither, with absent/zero price fields | Explicit enablement flags govern product availability; missing/zero price is not a flag. Operations owns plans/coverage; Payment owns authoritative prices/financial terms. | NOT RUN |
| AC-103 | V11-12 | FR-CAP-01, FR-MAP-01 | Search from consented current/manual origin with car/motorcycle and outdoor/basement/multi-storey filters | MapTiler discovery; default 5 km and expansion when empty; category independent of lot type; Haversine distance ordering; backend facts and labelled full lots unless filtered out. | NOT RUN |
| AC-104 | V11-12 | FR-CAP-01, FR-MAP-01 | Search without selected date/time; compare hourly/day/night tariffs | Current-time category availability and current tariff with unit; no invented duration/total and no comparison of unlike units as normalized total-price filtering. | NOT RUN |
| AC-105 | V11-12 | FR-CAP-01, FR-MAP-01 | Select interval and apply price/availability filters | Full selected-interval vehicle availability and Payment-sourced estimated total used for price comparison; source/freshness visible and backend rechecked at booking. | NOT RUN |
| AC-106 | V11-12 | FR-CAP-01, FR-MAP-04 | GPS denied, provider unavailable, empty result and stale/disconnected facts | Manual origin available; states represented distinctly and honestly. Provider failure is not zero capacity or guaranteed availability; reconnect reloads authorized facts. | NOT RUN |
| AC-107 | V11-12 | FR-MAP-01 | Open directions with no travel-estimate source | External map directions only; no embedded route calculation/turn-by-turn. Omit/defer unsourced estimate; Haversine distance never labelled road distance or ETA. | NOT RUN |
| AC-108 | V11-12 | FR-MAP-02, FR-MAP-03, FR-MAP-04 | Select discovery lot then internal 2D/3D booking; capacity changes during handoff | Preserve lot identity; distinguish physical/protection and requested/allocated positions, show only recorded precision; marker/slot selection grants no capacity; backend booking revalidates. | NOT RUN |
| AC-109 | V11-08 | FR-AUTH-01, FR-LOT-01 | Review proposed development reset/seed plan before execution | Require approved documentation, named teammate, exact environments/databases, reviewed script versions and scheduled date, plus schema/seed alignment. This scenario is NOT RUN and this docs task performs no reset. | NOT RUN |
| AC-110 | V11-11, D03, D07 | FR-AUTH-02, FR-BAS-08 | Exercise access expiry, refresh absolute/inactivity boundary and third failed OTP across roles | 15-minute access, absolute 7-day refresh and 24-hour inactivity remain; rotation cannot extend absolute expiry. Three OTP failures trigger 15-minute authentication lock, including seeded Admin; enabled TOTP cannot be bypassed. | NOT RUN |
| AC-111 | V11-02 | BR-PRIV-01, BR-PRIV-02, BR-PRIV-03 | Request agreement originals/replacements as unrelated applicant, scoped reviewer and expired retention/hold workflow | Private domain-authorized media references; no arbitrary public URL or blanket Admin access; version audit preserved, retention classification reviewed, holds/deletion proof honored. | NOT RUN |
| AC-112 | V11-03, D10 | FR-AUTH-01, FR-RPT-01 | Registration/setup commits but email queue or provider fails, then applicant resends | Persist pending account/application and saved setup; delivery status truthful; authorized retry issues/delivers challenge under approved OTP controls without duplicating business records or skipping setup. | NOT RUN |
| AC-113 | V11-06 | FR-AUTH-01, FR-AUTH-04 | Fake VNeID API returns a GUID during successful consented Driver/Partner registration or Operator activation; reload from persisted identity data | Store the returned simulated GUID against the correct User in User-owned database persistence; it survives retrieval beyond browser state. Never treat it as a government ID or auth token, expose another user's binding, or mark declined/failed simulation complete. Concrete schema/retry/conflict semantics follow the reviewed contract; sole seeded Admin exemption remains. | NOT RUN |
