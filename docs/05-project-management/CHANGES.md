# SPARK-296 — v1.1 documentation review candidate

20 October 2026 (Asia/Saigon). Applies the [approved revision 0.2 register](decision-log/2026-10-20-approved-v1.1-changes.md) to the local documentation baseline. This is a **v1.1 documentation review candidate based on approved decisions**; product acceptance remains NOT RUN.

Partner Registration retains the Owner role and creates a pending identity immediately. The initial proposal includes first-lot setup before email OTP, mandatory simulated VNeID, combined review and durable provisioning before normal login. Initial pending agreement replacement is versioned and limited to uploaded documents; final records are immutable. Additional lots have independent review and Draft as new.

The sole seeded Admin is protected, with no registration or role-management feature. Re-enable removes only an administrative disable restriction. Operator activation requires verified email and simulation, with optional password setup. Normal login returns necessary tokens after account and MFA gates; scoped onboarding credentials remain separate.

Driver monthly/yearly products, MapTiler discovery, explicit spatial batch deactivation and vehicle soft removal are integrated into the existing contracts. Operations retains discovery/coverage/occupancy authority; Payment retains financial terms. Earlier TOTP/recovery, payOS, settlement, reservation and retention invariants remain applicable.

[Decision coverage and acceptance mapping](../01-requirements/requirements-traceability/v1.1-decision-coverage.md) identifies all 24 V11/D decisions and design work. [The implementation register](decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions) tracks unresolved wire/security/media/policy details and the future development reset. No code, SQL, database, deployment or remote project changes form part of this documentation update.

The [21 October V11-06 clarification](decision-log/2026-10-21-vneid-simulation-guid.md) requires the fake VNeID API to return a GUID substitute persisted against the user. Canonical requirements, API/schema backlog and AC-113 reflect this direction; field names and implementation remain pending.

## Historical change records

The following RC1–RC3 records describe earlier review candidates and their original preparation context. Historical package-only comparison/patch paths are not current repository artifacts. Their counts, completion statements and earlier proposals do not override v1.1 or establish signed product acceptance.

# SmartPark v1.0 RC1 — change summary

Prepared 13 October 2026 from the five supplied files and the PM's subsequent Option A confirmation. This is a synchronized requirements release candidate, not a signed release, repository commit, implemented system or passed acceptance result.

## What changed

| Area | Superseded / clarified source | RC1 result and authority |
|---|---|---|
| Structure | One large SRS and consolidated companion files | 12 domain SRS modules; separate policy, permissions and state contracts; domain BRs; one file per UC; story catalogue; architecture, testing and decision records. Baseline §10 taxonomy used. |
| Identity | 24-hour token / optional current TOTP / unclear OTP role coverage | Password plus either verified email or phone OTP independently for every role; access15min, absolute refresh7days, inactivity24h; TOTP future. B08. |
| Operator accounts | Assignment of existing eligible accounts could imply a separate approver | Owner creates and assigns within own organization/lot; contact activation; no Admin per-hire approval. D03/E02. |
| Plate disputes | Unresolved binding-review proposal | Admin evidence-backed decision, verified contact/registration or transfer evidence/Operator findings; notify prior holder, never transfer on silence; preserve history. Q07. |
| Capacity | Additive counters could double-subtract occupied backup | Compatible temporal claim union; last-capacity atomic guard; accepted occupancy in Operations; no timer release or occupied displacement. SB-01/B01. |
| Facility changes | Unspecified ownership/precedence | Planned change coordinated with Operations; physical emergency recorded immediately with safe admission stop and recoverable outcomes. |
| Reservation limits | N/demo bounds | Min15min, max24h, horizon7days, buffer0; cap disabled by default, if enabled positive per-account/per-lot counting pending/confirmed/allocated. D02. |
| Allocation | Example30min and unresolved edge cases | Lead1h, protection4h, payment hold5min, offer5min; accepted Driver request allowance2; stable ranking and Operator true ties; delayed-arrival correction without displacement. D05/D06. |
| Payment precondition | Universal exclusion of no-payment confirmation | Positive price requires accepted prepayment; monthly-covered genuine zero price explicitly confirms without provider payment and with normal capacity guards. E03. |
| Prepayment | Unapproved30% default and fixed amount cap at full price | FULL default; percentage>0–100%; fixed>0 with no policy cap, even above booked price; positive-price rounded-zero deposit validates. D01. |
| Reservation minimum | Short stay versus reservation obligation unclear | Completed reserved visit owes disclosed covered-adjusted minimum plus eligible extras; prepayment credited once. Short visit does not waive booked obligation. D01/E03. |
| Cancellation/no-show | Full price could be confused with refund base/debt | Apply accepted percentage to eligible collected money only; no additional unpaid-remainder penalty. Ordinary bands100/50/0, no-show0. D01. |
| Excess deposit | Return owner/process unresolved | Payment automatically initiates surplus refund; original route where supported, audited cash for original cash; covered checkout may finish before payout. E01. |
| Billing | Duration-block ceiling, physical-exit final charge and old whole-VND/floor refund examples | Actual overlap to accepted checkout cutoff; sum then one nearest1000 half-up;2499.50→2000,2500→3000; cap refunds after rounding. B03/B04. |
| Free threshold | Earlier daily-per-vehicle aggregation proposal | Restored15min per whole session inclusive; full applicable duration above threshold; no daily reset/deduction. Reserved minimum survives. D01/D04. |
| Monthly overlap | Uncovered short tail could seem free | 60covered+10uncovered charges only uncovered10 because whole session exceeds15. D04. |
| Checkout recovery | Reopen paid visit or full-refund/recharge interpretation | Unpaid recovery with audit and reconciled money only, same session/no new grace; paid mispricing difference-only at original cutoff; continued stay requires actual exit/reentry. B04. |
| Monthly product | Guaranteed-capacity/slot clauses and sales cap examples | Available-only; no capacity hold outside, no guaranteed admission, no cap/queue/slot promise; separate ordinary reservations permitted. B05/E03. |
| Monthly periods/refunds | Fixture-labelled approved rates and floor rounding | Anchored calendar periods, strict A<H, hold15min/renewal7days/reminders7+1/max3 periods; prestart100%, midterm50% unused, failure100%; no autocharge/debt. |
| Monthly closure | Transfer/extension/compensation unresolved | Temporary retains pass/end, once-only verified unavailable-interval refund; permanent cancels remainder and100% eligible unused refund; dedupe overlap, no automatic extension. |
| Monthly voluntary cancellation | No rule for zero-priced dependent future bookings | A-01: list affected future reservations and require Driver explicit cancellation first; atomically recheck; no auto-cancel/reprice/charge. Rebooking is separate at ordinary terms with no capacity promise. |
| Permissions | Broad Owner refund/Admin operational readings | Assigned Operator ordinary cases/emergency/unpaid recovery; Owner escalation/monthly cancellation; Admin explicit account/security/audit/binding functions only. Complete action/resource matrix. |
| Retention/media | Chat anchor unspecified; evidence access/holds incomplete | Chat90days last message, finance7years issuance; image30days/audit7years/log90dayshot+1yearcold; holds reviewed30days/case closure; approved uploads/private proxy/deletion proof. |
| AI/support | Mixed active/future detail and possible autonomous writes/live chat | FAQ/search/own reads/reservation form preparation with ordinary UI confirmation; Admin-approved corpus, asynchronous tickets; detailed LPR/Assistant integration/evaluation POST-v1.0; other AI FUTURE. |
| Architecture | Old boundaries/Kafka/shared access | Six services; Operations rights/claims/occupancy/search/credentials, Payment tariffs/money, IoT Assistant/recognition, Notification/Audit support; separate persistence; RabbitMQ/MassTransit. |
| Acceptance | Production scale and multiple real integrations could appear mandatory | Approved local Docker profile, one real payment sandbox candidate plus labelled fixtures/cash; local performance/recovery recipe. Production scale/HA/on-call retained as future reference. All product scenarios NOT RUN. |
| Stories | Capacity promises; generic staff provisioning; Owner-exclusive refund goal | Four explicit goal edits: US-OW03 create/assign, US-OW06 available-only plans, US-OW07 escalations/monthly, US-D15 dependency resolution. US-OW05 promoted to current read-only diagnostics. Other goal texts retained. |
| Traceability | FR-LOT-05 reverse-edge defect and abbreviated lists | Fix US-O03/O05 reverse edge; normalize FR/BR/UC/US links; record additions in trace-edit-log. No ID renumbering. |

## Explicit technical elaborations in this candidate

These make approved behavior implementable; they are technical designs for normal architecture review, not claims that mechanisms were separately PM-approved or already coded:

- Common idempotency/payload/version envelope, outbox/inbox and one workflow coordinator; UNKNOWN money blocks blind new collection/refund/recovery.
- Refund-balance reservation includes unresolved in-flight refunds; difference corrections retain attributable net-money history.
- Reservation entitlement, physical session, commercial checkout, money/refund, monthly rights and cases remain independent; technical state labels may be attributes.
- Operations serializes monthly coverage dependencies with voluntary cancellation. A-01 applies to future reservations whose accepted price depends on coverage being removed; it does not auto-cancel other records.
- Inclusive minimum/maximum/advance-horizon boundaries use authoritative create time. Ordinary hold expiry/payment use ordered decisions and approved late reacquisition; monthly/offer strict equality rules are not copied into ordinary payments.
- Accepted in-flight bookings survive a later new-booking restriction; new creations after the restriction are blocked. Actual cancellation actions required by A-01 still follow ordinary cancellation terms/counting; no new exemption was invented.
- Domain-owned scoped event contracts and evidence lifecycle coordination are specified; database engine locks, backoff values and wire schemas remain implementation work.

## What was not silently promoted

Historical alternatives are preserved under history/reference, not active constraints. Required commercial prices remain Owner configuration; unapproved deposit30%, monthly1200000, lost-ticket20000, overstay5000, wait-limit15 and other demo seeds remain inactive. No new fine, damage bond, unpaid-balance penalty, automatic transfer on silence, capacity guarantee, autonomous AI payment or broad Admin grant is introduced. The old3–5-business-day refund processing target and non-conflicting security/compatibility/usability/maintainability requirements remain specified; no achieved result is claimed. Legal references are retained verbatim as source references, without claiming fresh legal verification. Historical staffing/budget assumptions are retained as reference, not a new commitment.

## How to compare

1. `comparison/clauses/`: original and replacement text for every FR and BR, grouped by domain.
2. `comparison/use-case-diffs/`: exact full before/after UC diffs; flows were condensed and rewritten against the current contracts, not merely relabelled.
3. `comparison/story-changes.md`: every original/current story row.
4. `comparison/source-section-map.md`: every original SRS heading and migration destination.
5. `comparison/full-source-diffs/`: complete source-to-assembled-RC diffs, including removed prose and restructuring. Assemblies exist only for comparison, not a new monolithic active SRS.
6. `history/`: byte-identical five inputs; manifest hashes prove retained originals.

## Remaining PM work

No new business-policy question was identified after Option A. PM-01: review/sign off this concrete candidate before publishing v1.0. PM-02: name accountable people and delivery dates. PM-03: supply actual support availability/handler rotation only if published to users. PM-04: decide any concrete acceptance exception only after the team records a blocker. No release approval, code migration, repository commit, provider delivery or product test pass is fabricated here.


## Actor-label alignment

FR-AUTH-04 now lists the Owner and activating Operator; FR-PAY-06 explicitly lists assigned Operator alongside Driver, Owner and Payment service; FR-PAY-11 lists assigned Operator/Payment/Device Gateway with Driver, removing an implied Owner checkout-execution role. These labels align with D03/E02 and the scoped permission matrix; they do not add Owner or Admin operational authority.


## RC2 security amendment

PM approved SEC-01/SEC-02 on13 October2026. This patch adds optional current-MVP TOTP for all four roles, backup recovery codes, restricted pre-login ACCOUNT_RECOVERY tickets and reviewed Admin-authorized password/factor recovery. It supersedes TOTP-FUTURE language only; parking, payments, deposits, monthly Option A, capacity and existing business grants are unchanged.

Existing login channels and token lifetimes remain. Enabled TOTP gates full session issuance. Voluntary disable requires primary+current TOTP. Backup-code recovery replaces factor after new proof. Admin password reset uses a single-use user-selected-password flow and preserves TOTP; Admin TOTP reset invalidates old factor/codes/sessions and requires replacement before login. Admin cannot self-approve; last-Admin case uses the controlled maintainer runbook. Existing contact verification plus independent evidence is required; insufficient proof remains pending.

New IDs: FR-AUTH-07–11; BR-AUTH-04–07; UC-AUTH-04–06; US-SEC01–03. Totals:135 FRs,57 BRs+9 monthly rules,41 UCs,46 stories. Existing IDs retained. Acceptance expands from47 to65 scenarios, all NOT RUN.

Technical elaborations: security/factor version checks, replay guards, restricted grants, atomic backup-code consumption and replacement, revoked-session enforcement, outbox/inbox recovery. Numeric TOTP profile/window, challenge TTL, recent-auth age, backup-code count/entropy and throttling remain explicit implementation-review settings. No unapproved numeric defaults or implemented results are claimed.

Historical source files and RC1 before/after reports are unchanged; the updated baseline review carries a supersession banner and new decision section. Use the RC2 diff for this amendment. Unchanged modules may still say RC1; that is their last-edit revision, not current TOTP authority.

## RC3 / PAY-01 — 13 October 2026

payOS QR is now first implementation priority. Controlled low-value payOS production testing plus deterministic failure/race fixtures replaces the one-real-sandbox gate. Native VNPay/MoMo/ZaloPay stay separate planned options with truthful modes; unfinished native integrations may be simulated/deferred. Added adapter clarification and integration test plan. Pricing/refund/capacity/security rules are preserved; refund execution still requires capability evidence. See [complete RC3 changes](decision-log/rc3-change-summary.md) and patch RC2-to-RC3.diff for exact edits. Prior sections describe earlier revision deltas.
