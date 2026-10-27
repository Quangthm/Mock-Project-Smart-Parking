# RC1 → RC2 patch: optional TOTP and Admin account recovery

PM approved SEC-01/SEC-02 on13 October2026. This patch adds optional current-MVP TOTP for all four roles, backup recovery codes, restricted pre-login ACCOUNT_RECOVERY tickets and reviewed Admin-authorized password/factor recovery. It supersedes TOTP-FUTURE language only; parking, payments, deposits, monthly Option A, capacity and existing business grants are unchanged.

Existing login channels and token lifetimes remain. Enabled TOTP gates full session issuance. Voluntary disable requires primary+current TOTP. Backup-code recovery replaces factor after new proof. Admin password reset uses a single-use user-selected-password flow and preserves TOTP; Admin TOTP reset invalidates old factor/codes/sessions and requires replacement before login. Admin cannot self-approve; last-Admin case uses the controlled maintainer runbook. Existing contact verification plus independent evidence is required; insufficient proof remains pending.

New IDs: FR-AUTH-07–11; BR-AUTH-04–07; UC-AUTH-04–06; US-SEC01–03. Totals:135 FRs,57 BRs+9 monthly rules,41 UCs,46 stories. Existing IDs retained. Acceptance expands from47 to65 scenarios, all NOT RUN.

Technical elaborations: security/factor version checks, replay guards, restricted grants, atomic backup-code consumption and replacement, revoked-session enforcement, outbox/inbox recovery. Numeric TOTP profile/window, challenge TTL, recent-auth age, backup-code count/entropy and throttling remain explicit implementation-review settings. No unapproved numeric defaults or implemented results are claimed.

Historical source files and RC1 before/after reports are unchanged; the updated baseline review carries a supersession banner and new decision section. Use the RC2 diff for this amendment. Unchanged modules may still say RC1; that is their last-edit revision, not current TOTP authority.
