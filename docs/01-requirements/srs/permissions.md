# Action-by-role permission matrix — v1.1

Every action below requires current account/grant state and target resource scope in the owning service. D=Driver acting on own records; O=assigned Operator; W=Owner of the lot/organization; A=Admin with the named platform function. A dash means no grant from this baseline. Multiple roles do not imply unrestricted role inheritance. Stable action names below are technical contract identifiers, not newly granted product privileges. Retries and private cached-result reads reauthorize the caller. Worker execution continues already-authorized durable business work under service credentials; it is not a human privilege bypass.

| Action/resource | D | O | W | A | Executor / guard / delegator |
|---|---|---|---|---|---|
| profile.read/update | own | own account | own account | own account | User & Access; permitted fields only, contact reverification separate |
| owner-registration.review | — | — | scoped applicant submits Partner + first-lot proposal | approve/reject combined initial application | User & Access; saved setup, mandatory simulation and verified email; immutable final decision; provisioning separate |
| owner-registration.setup/verify/resend | — | — | own pending application through scoped onboarding credential | — | Application ID alone insufficient; expiry/recovery design-pending; never a normal Owner session |
| owner-registration.documents.replace | — | — | scoped initial upload/read | replace initial pending uploaded documents only | User owns original/replacement versions + actor/time/reason; approval version guard; no other fields/additional-lot/finalized edits |
| additional-lot.submit/review/draft-new | — | — | submit own proposal; Draft as new from immutable rejection | approve/reject, no document editing | Separate outcome; no effect on Owner/other lots; distinct proposal identity and retry idempotency |
| operator.create/activate/assign/revoke | — | own invitation activation | own organization/lots create/assign/revoke | no per-hire approval | User & Access; email activation + mandatory simulation; optional password; no immediate active account |
| platform-account.security/re-enable | — | — | — | explicit scope; reason required | Remove only administrative disable; no contact verification/approval/grant/session/security-restriction restoration |
| platform-account.role-management | — | — | — | — | Outside MVP; fixed registration/activation assignment only |
| seeded-admin.disable/lock/delete/role-change | — | — | — | — | Sole seeded Admin protected, including self and last-Admin removal; ordinary own profile/security allowed; authentication lockouts remain |
| vehicle.manage | own, soft removal only | session correction only | — | binding adjudication only | User & Access retains binding history; reject active dependencies; cleanup follows retention; removal is not transfer |
| vehicle-binding.dispute/adjudicate | submit own | local findings | — | evidence-backed adjudication | User & Access mutation; Support case in Notification/Audit; no silence transfer |
| facility.configure/layout/publish | — | no ownership grant | owned lot | — | Facility; planned-change handshake; Owner grant constrained |
| facility.deactivate.batch | — | no new configuration grant | owned lot, explicit descendants | — | One lot/all-or-nothing; current dependencies/versions; per-item errors and no partial mutation |
| facility.restriction/incident | — | assigned operational scope | owned configuration | — | Facility facts + Operations admission guards; no erasure of occupancy |
| device.manage | — | assigned lot | — | platform adapter settings only | IoT & Recognition; Owner assigns Operator |
| device.diagnostics.read | — | assigned lot | owned lot read-only | explicit integration/security scope | IoT; view cannot mutate or reveal secrets |
| policy.operational.publish | — | execute only | owned lot approved choices | platform defaults/bounds | Operations or Facility owner of specific policy |
| policy.financial.publish | — | — | owned lot within bounds | platform bounds only | Payment; immutable accepted terms; failure refund100% locked |
| reservation.create/read/cancel | own eligible | operational records, no Driver impersonation | scoped business read | no blanket grant | Operations; current rights, capacity, lifecycle, abuse/cap guards |
| reservation.reallocation.request | own up to2 | system recovery/decision | escalation | — | Operations; accepted submissions counted, no system-recovery debit |
| allocation.arrival/admit/verify | present/request only | assigned lot | no implied execution | no implied execution | Operations; evidence, version, compatibility, genuine tie review |
| checkout.accept/cash.collect | pay own | assigned lot | no implied execution | no implied execution | Operations accepts cutoff; Payment records audited collection |
| checkout.unpaid-recover | — | explicit assigned-lot recovery | escalation | — | Operations + reconciled Payment state; reason; never paid reopen |
| departure.emergency | — | explicit assigned-lot emergency | escalation | — | Operations actual movement/incident/debt; audited |
| charge.miscalculation-correct | submit case | assigned lot, system error only | escalation | — | Payment calculates/executes difference at original cutoff |
| incident/violation.review | own case projection | assigned case | escalated owned case | no blanket lot grant | Operations; human evidence review, limited consequences |
| appeal/refund.handle | submit own | ordinary assigned-lot case | escalation/monthly case | no ordinary lot refund | Operations case authorization, Payment balance reservation/execution |
| refund.surplus.execute | view own result | cash return if original cash | scoped escalation | — | Automatic Payment E01; original online route where supported |
| monthly.purchase/renew | own eligible | cash/visit support | enable owned monthly/yearly product separately from price | — | Operations rights / Payment terms; stable monthly action label also describes yearly scope; product-specific advance limits/policies; monthly strict A<H, yearly deadline design-pending |
| monthly.cancel | own request and dependent booking cancellation | scoped findings | owned-lot handling | — | A-01 voluntary guard; Payment independent payout |
| evidence.upload/read/hold | own allowed/request via case | handled case | escalated/monthly case | authorized investigation only | Asset owner acknowledges hold; private proxy; no unrelated evidence |
| evidence.hold.release | request via case | own originating hold | own originating hold | own originating authorized hold | Asset owner preserves other holds and audit |
| reports/export/SignalR | own authorized projection | assigned operational need | owned business scope | explicit account/security/audit scope | Each owner; scope/filter/redaction applies equally to exports/live |
| audit.search | — | case-needed facts only | owned staff/case facts only | authorized audit search | Notification/Audit; no log-edit grant |
| support.case.handle | submit/view own | assigned lot matters | escalated owned case | platform/account/global dispute | Notification/Audit; scoped context references, asynchronous |
| knowledge.publish | — | — | supply content outside publication workflow | publish versioned approved corpus | IoT Assistant; no Developer actor or Owner publication |
| assistant.tools | own authorized calls | no extra role powers | no extra role powers | no extra role powers | Domain APIs reauthorize; UI confirms normal reservation commands |
| cancellation-abuse.configure/dispose-alert | — | — | — | platform policy/disposition | Operations authoritative restriction; Audit projects; no automatic ban |
| retention.execute | — | request scoped holds | request scoped holds | policy/investigation within baseline | Data-owning service; automatic due processing, no blanket private read |


## SEC-02 account security permissions

| Action | Account holder, any role | Admin | Other staff role | Guard/executor |
|---|---|---|---|---|
| totp.enroll/disable | Own account with required proof | Own account same rules | No authority over others | User & Access; optional all roles |
| backup.regenerate/redeem | Own account with required proof | No read/export of others’ codes | None over others | One-use hashes/current factor version |
| account-recovery.submit | Own request; limited pre-login form allowed | Own request, never self-approve | May submit own request | Generic receipt; no account/session disclosure |
| account-recovery.review | No self-approval | Explicit recovery permission; current access and recent full auth | No Owner/Operator reset authority | Verified existing contact plus independent record-backed evidence |
| password-reset.authorize | Own self-service flow unchanged | Reviewed ticket; user chooses password | None | User & Access single-use scoped grant; TOTP unchanged |
| totp-reset.authorize | Own backup/current-factor route | Reviewed ticket; replacement mandatory | None | Revoke old factor/codes/sessions; no simple disable bypass |
| admin-account.recover | Sole seeded Admin submits own case | No self-approval | None | Controlled maintainer runbook; no second Admin registration; authentication lockout remains |

Support custody does not grant credential mutation. Admin never obtains another user's password, secret, QR payload or backup codes. Recovery does not approve Owner registration, unblock accounts, alter roles or grant lot access.
