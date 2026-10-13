# SEC-01 / SEC-02 — TOTP and account recovery

**PM CONFIRMED on13 October2026; RC2 document amendment.**

- TOTP enters current MVP, optional for Driver, Operator, Owner and Admin. Six-digit enrollment proof activates the factor; enabled accounts require second-step proof before normal session issuance.
- Backup recovery codes selected instead of emailed-code-only recovery. Restricted replacement requires primary proof plus an unused backup code; old factor survives until successful replacement.
- Admin-handled ACCOUNT_RECOVERY type and limited pre-login form approved. Generic acknowledgement grants no account access.
- Admin authorizes single-use password/TOTP recovery; holder chooses new password. Password-only preserves TOTP; factor reset requires verified replacement and revokes old sessions/codes.
- Different Admin handles Admin account recovery; no self-approval. No-other-Admin case uses controlled deployment-maintainer runbook.
- At least one existing verified contact plus independent evidence review required. OTP alone insufficient; no contact/insufficient evidence stays pending/escalated. No new routine national-ID collection.
- Current role/account/Owner approval/lot grants remain unchanged. Admin recovery grants no blanket operational override.

These decisions supersede all earlier TOTP-FUTURE statements in RC1, including baseline review14.5/16.5 and ARCH-DEF-07 commentary. Original history and RC1 comparison reports are retained as historical evidence, not current TOTP scope. Other deferred AI/hardware work remains deferred.

Technical elaborations explicitly disclosed: account security/factor-version serialization; purpose-bound restricted contexts; TOTP/backup replay protection; session invalidation on completed factor/password changes; separate reviewed case, authorized grant and executed result. Numeric challenge/window/backup-count/throttle parameters are security implementation-review items, not newly PM-approved values. No implementation or acceptance pass is asserted.
