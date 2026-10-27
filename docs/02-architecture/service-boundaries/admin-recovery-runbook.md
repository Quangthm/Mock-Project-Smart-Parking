# Sole seeded Admin recovery — controlled deployment-maintainer procedure

SEC-02 and v1.1 D02–04 require this controlled route for the sole seeded MVP Admin; it is not a fifth business role, public endpoint or self-approved Admin ticket. Implementation and operational exercise are pending.

1. Confirm the target is the sole seeded MVP Admin and record the ACCOUNT_RECOVERY ticket/incident. No Admin registration or role-management route may create another reviewer. Reject application-level administrative lock, disable, delete or role-change operations targeting this Admin; ordinary own profile/security operations remain allowed. Authentication lockouts still apply and are not bypassed.
2. The responsible deployment maintainer performs verification through an existing verified contact plus independent account/organization records, preserving the same evidence requirement. No channel or insufficient evidence means pending/escalated, not a database bypass.
3. Use controlled deployment access and a narrowly scoped User & Access recovery operation; record maintainer identity, target, reason, approved scope, time and evidence references. Never manually set a known user password, dump secrets or broadly modify role/account state.
4. Authorize the same single-use user-chosen password and/or replacement-required TOTP recovery as normal assisted flow. Revoke sessions and invalidate old factor/codes as specified; no ordinary access before replacement completion.
5. Record verified outcome, notifications and audit; remove temporary operational access and retain incident evidence under existing policies.

Before offering this route, the delivery team must identify the responsible maintainer, controlled execution mechanism and access/audit procedure and exercise it. No credential, named maintainer or fabricated implementation command is supplied here. Recovery remains pending if the controlled procedure is unavailable.

Administrative re-enable of other accounts (D01) is a separate authorized, reasoned action that removes only administrative disablement. It cannot serve as Admin recovery, reverse Partner rejection, approve/provision an account, restore grants, revive sessions or clear independent security/booking restrictions.
