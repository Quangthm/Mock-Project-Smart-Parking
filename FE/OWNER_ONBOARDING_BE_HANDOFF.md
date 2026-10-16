# Owner onboarding: FE changes and BE handoff

## Required sequence

Business registration → parking lot setup → email verification → Admin approval → Owner sign-in and use.

The FE reuses `OnboardingWizard` in `src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx`. It does not save a parking lot draft in browser storage. Only registration metadata (without password or lot data) is kept in `sessionStorage` so a page refresh can resume the current step.

## FE changes

| File | Change |
| --- | --- |
| `src/roles/authentications/Sign Up/SignUp.tsx` | Keep Driver and Business sign-up tabs. After Business registration, navigate to the parking lot setup page; show OTP only after the server confirms setup. |
| `src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx` | Add a separate page wrapper and registration mode around the existing five-step form. Send lot setup through `ownerApi` instead of creating an authenticated site. No second form was created. |
| `src/App.tsx`, `src/context/AppContext.tsx` | Add the separate public parking lot setup view. |
| `src/lib/ownerApi.ts` | Define the lot setup contract and call the new registration setup endpoint. |
| `src/roles/authentications/Sign Up/OwnerVerification.tsx` | Explain that lot setup precedes email verification. |
| `src/roles/admin/applications/Applications.tsx` | Show submitted lot details and disable approval when setup or email verification is missing. |
| `src/public pages/PendingApproval.tsx` | Describe the new steps after email verification. |
| `src/lib/authApi.ts`, `src/roles/owner/dashboard/OwnerDashboard.tsx` | Use server onboarding state and existing sites so an approved Owner is not asked to create the same site again. |

## API contract expected by the FE

1. **Register:** `POST /api/auth/register/owner` keeps its existing request fields. Its `data` response must also include `setupToken` (an opaque, server-issued registration credential), `lotSetupComplete: false`, `id`, `lotType`, and `email`. `verification` can remain absent until setup is saved. The FE does not use the Owner login token for this step. The server should delay sending the email OTP until setup is saved, matching the requested sequence.
2. **Save setup:** Add `POST /api/auth/register/owner/lot-setup` with JSON body `{ "registrationId": "<application-id>", "setupToken": "<opaque-token>", "lot": { ... } }`. Return the updated Owner application in the existing `{ "success": true, "data": ... }` envelope. The response must preserve the same `id`, include `lotSetupComplete: true`, and include the email `verification` challenge. Make retries safe so a repeated request cannot create duplicate lots. Reject invalid or expired tokens and invalid lot data on the server.
   `setupToken` is only for the registering browser; omit it from Admin application responses.
3. **Lot payload:** `lot` has `name`, `address`, `lotType` (`outdoor`, `basement`, or `multi-storey`), `totalSlots`, `floors`, `devices` (`type`, `label`, `enabled`), `modelFileName`, `hourlyRate`, `dailyRate`, `nightRate`, `gracePeriodMinutes`, `subscriptionMonthly`, `subscriptionYearly`, and `policyAccepted`. Counts and rates are JSON numbers. The lot type selected in the setup form is authoritative; update the application lot type to match it or reject a mismatch. `modelFileName` identifies one of the supported bundled 3D models; the current FE does not upload GLB binary data or policy document content in this flow. If those files must be persisted, a separate upload API and FE work are required.
4. **Recover:** `POST /api/auth/register/owner/recover` needs to return `id`, `lotType`, `lotSetupComplete`, `verification`, and a safe way to resume setup. Do not return a setup credential based solely on an unverified email address; use a server-issued, verified recovery channel. Until this is available, registration can only resume setup in the same browser session.
5. **Verify and review:** `POST /api/auth/register/owner/verify` must reject verification until lot setup is complete. `GET /api/owner-applications` must include `lotSetupComplete` and `lotSetup` for Admin. `PATCH /api/owner-applications/{id}/review` must reject approval unless both `contactVerified` and `lotSetupComplete` are true; the FE button state is only a UI safeguard.
6. **Activate:** Keep the submitted lot inactive before approval. On approval, provision the tenant and the submitted parking site without requiring Owner sign-in, then activate the site. The site must become visible in `GET /api/parking-lots` when the approved Owner signs in. Return `onboardingComplete: true` in the authenticated user object from `/api/auth/login`, `/api/auth/otp/login`, and `/api/auth/me`. Keep the account unable to sign in before approval.

## BE locations to review (no BE files changed here)

- `src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs` — registration response.
- `src/Services/UserService/SmartParking.UserService.API/Controllers/AccountWorkflowController.cs` — setup, recovery, and verification endpoints.
- `src/Services/UserService/SmartParking.UserService.API/Controllers/OwnerApplicationsController.cs` — Admin list and review.
- `src/Services/UserService/SmartParking.UserService.Application/DTOs/OwnerRegistrationDto.cs` — request and response DTOs.
- `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs` — setup persistence, OTP timing, approval guard, and tenant/site provisioning.
- Parking Service site creation and identity integration — provision the submitted site during approval and expose it to the approved Owner. Confirm the exact integration point in BE before implementation.

## Current integration limit

The existing BE does not return `setupToken`, store this lot setup payload, or provision a site before Owner login. With the unchanged BE, the FE opens the existing parking lot form after Business registration, but cannot save setup or proceed to OTP. The form shows a server-support notice; submitting shows an error instead of falsely claiming that the lot was saved. End-to-end completion requires the BE contract above.

Existing pending Owner applications that lack lot setup need a migration or a secure setup recovery path before they can be approved under the new rule.
