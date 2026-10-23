# Authentication and account recovery — v1.1

**v1.1 documentation review candidate based on approved decisions, 20 October 2026.** SEC-01/SEC-02 remain confirmed; implementation and tests pending.

## Account establishment and normal-session gates

Partner Registration retains Owner and immediately persists a pending identity. Mandatory consented VNeID simulation, first-lot setup save, email OTP/verification, combined Admin approval and successful provisioning precede normal Owner login. Setup/verification/resend/recovery credentials are purpose-scoped; application ID alone grants nothing. Phone verification is separate and required only before phone OTP login, not Owner review. Simulation is mandatory for Driver/Owner registration and invited Operator email activation; password setup is optional for Operator. The sole seeded Admin is exempt. Simulation is clearly labelled and does not replace contact verification, approval or MFA. The fake VNeID API returns a GUID substitute associated with the user; User & Access persists that association and GUID in its database. This is a simulated identifier, not a government identity number, authentication token or authorization credential. Additional mock fields, consent/completion evidence encoding and retention remain Q02 design tasks; scoped credential expiry/recovery remains Q03. The [21 October clarification](../../05-project-management/decision-log/2026-10-21-vneid-simulation-guid.md) settles GUID return, user association and database persistence, not an API path or schema field name.

Driver verification does not automatically log in. Normal completed login returns necessary tokens; retrieve authorized account/profile/onboarding information separately (for example through /me), without unconditional profile/onboarding payloads. A required MFA challenge remains a restricted intermediate response, not normal login. Safe standardized actionable failures retain reasons for pending review, rejection and pending/failed initial provisioning after sufficient credential validation; do not reveal raw exceptions, secrets or an account-enumeration oracle. Response envelope, delivery and credential-validation threshold remain API/security design work.

Re-enable is an explicit authorized Admin action with reason that removes only administrative disablement; it cannot reverse initial rejection, approve a pending Owner, verify contacts, restore removed grants, clear independent restrictions or revive sessions. The rejected initial Partner email cannot register again. Sole-Admin administrative lock/disable/delete/role changes are prohibited; ordinary own profile/security and authentication lockouts remain, with maintainer recovery.

## Authentication and factor lifecycle

Four roles remain optional TOTP participants. Initial signup/contact verification, password reset, email/SMS login OTP, TOTP and backup recovery codes are different purposes. Existing password or independently verified email/phone primary login remains. Optional enrollment does not require email: phone-only users may enroll and retain offline backup codes. There is no trusted-device bypass. Do not claim OTP+TOTP has identical factor independence to password+TOTP.

Enrollment requires recent primary authentication. Deliver a pending secret/QR only to its authorized enrollment context; validate a six-digit code before activation. Abandoned enrollment leaves TOTP disabled. Regenerate pending secret with a new version and reject old challenges. Existing enabled-factor replacement requires current factor or approved recovery, not just primary proof.

Normal enabled-account login: primary proof → restricted challenge → valid TOTP → normal session. Restricted challenge/recovery credentials are rejected by all ordinary resource endpoints and SignalR subscriptions. Password reset alone cannot bypass enabled TOTP. Accounts with TOTP disabled follow existing primary-login rules; administratively disabled accounts cannot obtain a normal session.

Disable requires recent primary proof and current TOTP. Successful disable invalidates backup set and challenges, revokes old sessions, records audit and sends notification; the user may subsequently log in through the normal non-TOTP route. This voluntary disable is distinct from Admin reset, which requires replacement.

Backup codes are securely generated at enrollment/replacement, displayed/downloaded once and stored as hashes. Regeneration requires recent full authentication and invalidates the old set. Codes are not emailed recovery OTPs, and support staff cannot retrieve them. If secure delivery of a new set is interrupted, show the failure honestly; retry must not create multiple active sets. Secure regeneration after authentication is the recovery route for a lost display, rather than storing readable codes indefinitely.

Primary proof plus one unused backup code starts restricted replacement. Consume it once and persist the recovery intent atomically. Old Authenticator remains until successful new-code confirmation. At completion atomically switch factor/version, replace backup set and invalidate old challenges/sessions. After interruption resume the authorized pending operation if still valid; a spent code is not reusable on a new operation. If recovery expires and no other proof remains, use Admin ticket recovery. A failed replacement never reports success or silently removes the old factor.

## ACCOUNT_RECOVERY ticket

Notification & Audit owns the support ticket, correspondence and evidence references. User & Access owns challenges, proof outcomes, grants, passwords, factors, backup codes and session revocation. No cross-service direct database access or new service is needed.

Allow a limited pre-login form and authenticated submission. Apply Turnstile/rate controls and generic acknowledgement regardless of account existence. A supplied identifier or reachable new contact is a claim, not identity proof. Verify a previously bound, verified channel through User & Access; do not expose which channels are bound. Case status/correspondence use a restricted capability, never an account session or guessable ticket ID. No arbitrary password, TOTP QR/secret, backup code or reset-token upload is requested.

Admin verifies one existing email/phone and reviews supporting account evidence against existing records. Suggested evidence checklist is an implementation review aid, not an automatic scoring rule: relevant past account/transaction facts verified internally; existing organization membership/onboarding evidence for staff/Owners; corroboration already permitted by the case scope. A payment screenshot, knowledge of public profile information or OTP alone is insufficient. Record evidence checked, discrepancies and reasoned sufficiency decision. Do not import the vehicle-binding evidence standard as automatic account-ownership proof. Existing image limits/access/holds apply; no new routine national-ID collection or general PDF upload is approved.

If neither existing channel can be accessed, or evidence remains insufficient/conflicting, keep pending/escalated. Time or nonresponse cannot auto-approve recovery. Verification of a newly supplied contact alone is insufficient; this feature introduces no contact-rebinding bypass. Support submission remains available without granting account access.

Ticket follows OPEN → IN_PROGRESS → optional WAITING_FOR_CUSTOMER → RESOLVED → CLOSED. Identity review and reset authorization/execution are separate attributes. Mark recovery successful only after User & Access reports the requested credential change completed; rejected/withdrawn cases may resolve with that explicit disposition. Expired/failed grant does not count as recovered. No automatic close-on-silence deadline is added.

## Admin actions

Require explicit account-recovery permission, current Admin access, recent full authentication (including their TOTP if enabled), reviewed case and target security version. Owner/Operator cannot reset another account through their ordinary role. Separate PASSWORD/TOTP/BOTH authorization; record actor, target, reason, evidence references, purpose, time and outcome. Admin cannot see/set the user's new password or read factor secrets/backup codes.

Password: authorize single-use limited flow; holder proves and redeems it, chooses password and satisfies password policy. On mutation revoke sessions/stale reset/login challenges; existing TOTP remains required. Merely sending a grant is not a completed reset.

TOTP: after authorized case decision User & Access invalidates old secret/codes and revokes sessions; enters REPLACEMENT_REQUIRED. Issue restricted single-use enrollment grant through the verified case workflow. Until new secret proof succeeds, block ordinary login even via password/OTP. On completion enable replacement, issue new backup set, consume grant and invalidate prior contexts. BOTH combines the required password and factor changes before ordinary login. No temporary-password handoff or Admin-selected TOTP key is provided.

MVP has one protected seeded Admin; server rejects self-approval even if a grant is replayed. Recover that account only through the [controlled deployment-maintainer runbook](../../02-architecture/service-boundaries/admin-recovery-runbook.md); no Admin registration or second-Admin onboarding is added. Account blocking, Owner approval, role/lot grants and unrelated business rights are unaffected by successful recovery.

## Races, retries and revocation

| Operation | Local atomic guard/effect | Retry/conflict result |
|---|---|---|
| Prove enrollment | Pending secret/version, proof, unexpired context → enabled factor | One activation; stale pending QR cannot overwrite newer factor |
| Complete TOTP challenge | Account/grants and security/factor versions current; accepted time step not used; issue session | One successful use per factor/time step; exact transport replay returns same authorized operation outcome, not another session |
| Redeem backup code | Conditional unused→used and recovery intent persisted | Two tabs have one winner; timeout uses same-operation lookup, not double consumption |
| Replace factor | Guard recovery version/new proof; switch secret and code-set/version | Competing reset/replacement wins once; loser context invalid |
| Admin password reset | Guard scoped grant/target/version; password hash and security-version change | Duplicate request no second mutation; old session/refresh rejected |
| Admin TOTP reset | Guard reviewed ticket/Admin/target/version; invalidate factor/codes; REPLACEMENT_REQUIRED; revoke | If delivery fails, pending recovery remains visible; no ordinary-login bypass |
| Login versus reset/disable | Serialize against account security version | Session from pre-change version cannot authorize subsequent protected action |
| Support timeout versus credential result | User & Access durable result + outbox; Support deduped projection | Lookup same operation; do not issue another reset merely because case projection lags |

Authoritative expiry rejects at equality. Persist bounded-purpose challenges, operation IDs/payload fingerprints, attempts and outcomes; no passwords/codes in idempotency responses or logs. Use outbox/inbox for case notification/projection, but User & Access guards cannot depend on delayed support projection. Resource services enforce revocation using current session/security version; refresh-only revocation is insufficient. Durable already-authorized business workflows remain valid independently of a revoked human session.

## Technical implementation review gates

Select and document TOTP algorithm, 30-second-step recommendation, bounded clock-drift window, replay storage, challenge/enrollment/reset TTLs, recent-auth age, backup-code count/entropy and abuse controls. These are security design parameters, not Owner settings or newly PM-approved numbers. A configured and tested profile is required before acceptance. Six-digit codes are part of the approved proposed flow. Use vetted implementation and RFC test vectors rather than custom cryptography. Protect reversible TOTP secrets with managed encryption keys; password and backup-code verification use appropriate hashes. Never log QR provisioning URI, secret, live code or reset capability. Clear secrets from client state after display/use and prevent caching of sensitive responses.

Run actual TOTP generation/verification and backup-code lifecycle locally; email capture/SMS fixtures remain the declared notification/primary-channel modes, not simulated factor verification. No claim of delivered implementation is made.

References informing technical design: [RFC6238](https://www.rfc-editor.org/rfc/rfc6238) defines time-based OTP, default30-second step and replay considerations; [OWASP MFA guidance](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html) informs protected replacement/recovery and notifications. Project-specific decisions above come from SEC-01/SEC-02, not from those references.


Design/implementation owners and exit criteria: [V11-DES-02–04 and 11](../../05-project-management/decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions). Requirement approval does not imply an approved wire contract or completed implementation.
