# Owner email OTP fix and review — 08/10/2026

## Requirements checked

- SRS v0.9 §3.1.1 / C-23: company name, email and phone required; Admin approval gates Owner operations; no corporate-domain restriction.
- SRS §3.1.4: OTP through SMS or email, not mandatory verification of both.
- SRS §4 security / business rules §3: expiry 5 minutes, 3 failures, lock 15 minutes.
- UC-AUTH-01/02: OTP security and authorized Admin review; no two-contact verification gate.
- Solution architecture: external notifications use adapters; collecting a phone does not prove phone ownership.
- Current workflow/QA documents and historical Owner API reviewed. Remote owner-register API design DEC-AUTH-02 states email default/SMS fallback. Its endpoint, field, error and 15-minute expiry differences remain separate contract conflicts; this patch does not claim full alignment with that design.

## Changes reviewed

Owner registration creates only an email challenge and email outbox delivery. Verification records EmailVerifiedAt and moves the user to PENDING_APPROVAL. Admin list and approval use email verification. Phone remains required and PhoneVerifiedAt is not inferred from email verification. Owner phone OTP login remains unavailable unless the phone was independently verified.

Frontend shows one email verification step. Cached registrations from the old two-step UI still restore. Recovery handles a consumed email challenge on a legacy PENDING_VERIFICATION account by moving it to PENDING_APPROVAL; obsolete phone challenges are consumed, their hashes cleared, and pending/failed phone outbox payloads cancelled and cleared. Phone-only legacy accounts must still verify email. No session or operational permission is created by registration verification or recovery.

Removed phoneVerification from the Owner response DTO/frontend type and the Postman phone verify/resend steps. Updated current workflow contract and QA handoff; historical SRS baselines are unchanged. Release backend and frontend together.

## Validation

- dotnet test SmartParking.slnx --artifacts-path .cache/owner-email-tests --no-restore --verbosity minimal: 93 passed, 0 failed, 0 skipped (79 UserService + 14 ParkingService).
- Tests ran against local PostgreSQL with isolated temporary databases.
- Real HTTP test hosts for User/Parking/Reservation exercised email-only registration, verification, pending login denial, Admin approval/tenant provisioning, Owner login, parking creation and later operator/revocation operations.
- Regression cases: no Owner SMS created; pending/unverified Owner cannot approve; phone-only verification cannot approve; replay rejected; resend preserves attempt/lock rules and exact five-minute expiry; legacy email-only recovery cancels phone challenge/payload; unverified phone cannot be used for OTP login; concurrent review and notification retry remain correct.
- Frontend npm run build: passed. Existing large-bundle warning remains.
- git diff --check: passed.
- Manual review of changed backend, DTO, frontend, test, contract and Postman lines found no remaining actionable issue within this fix.

## Limits and rollout

Automated integration tests validate the delivery queue and adapters with controlled test hosts. Local UserService was rebuilt and restarted with the fix; live login/logout and session revocation passed. Gmail SMTP authentication passed. The user subsequently reported completing the manual demo flow test on localhost. External QA has not been deployed or retested. SMS fallback is not implemented for Owner onboarding in this patch.

No schema changes or database renames are needed. Recovery upgrades pending legacy registrations on demand; unrelated local changes from the previous task are preserved. Deploy backend and frontend together.
