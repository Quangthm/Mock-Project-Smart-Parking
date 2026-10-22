# Partner onboarding: FE/BE handoff — v1.1

SPARK-296 · updated 21 October 2026 from [approved decisions, 20 October](../decision-log/2026-10-20-approved-v1.1-changes.md). **v1.1 documentation review candidate based on approved decisions**. This is a target integration handoff, not evidence of delivered BE/FE behavior or successful runtime tests.

## Required sequence and scope

Partner Registration (existing Owner role, immediate pending identity/account) → mandatory consented simulated VNeID → first-lot setup and agreement uploads saved → email OTP issued and verified → combined Admin approval/rejection → successful provisioning → normal Owner sign-in eligibility. Operational opening follows separate readiness and permission checks. No normal Owner session exists during pending review or pending/failed initial provisioning.

Canonical [registration API](../../03-design/api/owner-register-api-design.md), [review API](../../03-design/api/admin-approve-owner-api-design.md), [shared auth/integration contracts](../../02-architecture/api/v1.1-contracts.md), [identity SRS](../../01-requirements/srs/modules/01-identity-access.md), [facility SRS](../../01-requirements/srs/modules/02-facility-capacity.md) govern this handoff. Exact new wire shapes remain design-pending with the [tracked owners/actions](../decision-log/deferred-and-implementation-register.md#v11-design-and-implementation-actions).

## FE touchpoints and previous implementation evidence

The earlier handoff described reuse of `OnboardingWizard` in `src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx`, with registration metadata in `sessionStorage` and no lot draft/password in browser storage. It reported that policy-document content was not uploaded and the unchanged BE could not save setup/provision the site. These remain **historical reported limitations**, not a fresh source/runtime assessment. New scope requires real agreement uploads and mandatory simulation; a filename/acceptance checkbox does not satisfy upload.

| FE location from the earlier handoff (relative to FE/) | Required v1.1 alignment |
|---|---|
| `src/roles/authentications/Sign Up/SignUp.tsx` | Partner terminology, pending registration, simulation and first-lot setup before email OTP. |
| `src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx` | Scoped registration mode; submit proposal under same application, explicit product enablement and agreement uploads. Preserve no false local save/completion. |
| `src/App.tsx`, `src/context/AppContext.tsx` | Public restricted onboarding navigation does not imply a normal Owner session. |
| `src/lib/ownerApi.ts` | Agreed setup/recovery/media contracts, safe retry and explicit pending/failed states. |
| `src/roles/authentications/Sign Up/OwnerVerification.tsx` | OTP only after server-confirmed setup/simulation; required email verification. |
| `src/roles/admin/applications/Applications.tsx` | Combined application, protected original/replacement agreement versions, server prerequisites and concurrency-aware review. |
| `src/public pages/PendingApproval.tsx` | Email explains review decision separately from provisioning/sign-in readiness. No additional status API required merely to duplicate it. |
| `src/lib/authApi.ts`, `src/roles/owner/dashboard/OwnerDashboard.tsx` | Necessary tokens on completed normal login; separately retrieve authorized profile/existing sites. Do not prompt approved Owner to recreate the first lot. |

## Registration, setup, recovery and login contract

1. **Register:** retain `POST /api/auth/register/owner` field family (`fullName`, `businessName`, email, phone, lot type, policy acceptance, optional password). Persist pending User/Account/application immediately. The proposed response carries stable application identity/progress and a scoped setup credential; exact `setupToken` delivery/lifetime/storage and schema are V11-DES-03. Do not send the email OTP before setup is saved.
2. **Simulation:** Driver and Partner registration require explicit sharing consent and successful clearly labelled simulated VNeID. Operator completes it in email activation with password setup optional. Only the sole seeded Admin is exempt; no Admin registration route. The [21 October clarification](../decision-log/2026-10-21-vneid-simulation-guid.md) requires a fake API to return a GUID substitute tied to the user, persisted by User & Access in its database. This GUID is a mock identifier, not a government identity number or authentication/setup credential. API path/property name, other fields, repeated-call GUID behavior, failure/completion evidence and retention remain V11-DES-02.
3. **Save setup:** proposed `POST /api/auth/register/owner/lot-setup` with `{registrationId, setupToken, lot}` remains an integration candidate pending V11-DES-03. It saves the first-lot proposal into the same application and then queues email OTP. Server validates credential scope/expiry, setup fields and required simulation; retries cannot create duplicate application/site records. Omit setup secrets from Admin views. Application ID alone is not authorization.
4. **Recover:** established `POST /api/auth/register/owner/recover` may initiate recovery, but unverified email alone cannot return a setup secret. Agree a verified recovery capability and safe resumable metadata under V11-DES-03. Runtime credential recovery remains required independently of the selected development reset.
5. **Verify/review:** established Owner verify/resend routes retain purpose-bound challenge handling. Required saved setup, simulation and verified email are server guards. The required phone need not be verified for review; verify separately before phone OTP login. Admin list/detail expose safe application/version/progress evidence, not credentials.
6. **Normal login:** `/api/auth/login` and `/api/auth/otp/login` return necessary tokens only on completed login. Retrieve required profile/onboarding information separately through authorized `/api/auth/me` or agreed resource. Remove unconditional `onboardingComplete`/profile requirements from success. Keep required MFA challenge and safe actionable pending/rejected/provisioning failures after sufficient credential validation. Pending/failed provisioning never grants a restricted normal Owner session.

## Setup field ownership and media

The earlier FE proposal named `name`, `address`, `lotType`, `totalSlots`, `floors`, `devices` (`type`, `label`, `enabled`), `modelFileName`, `hourlyRate`, `dailyRate`, `nightRate`, `gracePeriodMinutes`, `subscriptionMonthly`, `subscriptionYearly`, `policyAccepted`. These are **submitted proposal fields**, not an approved cross-service operational DTO. V11-DES-04/05 must reconcile validation/names with owning contracts; no authority moves because one form collects them.

| Submitted group | Authoritative service and target constraint |
|---|---|
| Applicant/company/application/review and documents | User: identity, organization/tenant identity, membership, submitted snapshot/review and agreement versions. |
| Lot/address/type/capacity/floors/model/readiness configuration | Facility: configuration/spatial structure and fixed deployment-model reference. No GLB upload is introduced by this scope. |
| Monthly/yearly products and coverage | Operations: separate explicit enablement from price; Driver products, not Owner platform subscriptions. Annual = 12 calendar months, maximum one yearly period ahead; existing monthly limits remain. |
| Rates, grace/financial terms and refund calculations | Payment: versioned authoritative financial terms. Separate yearly policy set; no invented percentages or yearly renewal defaults. |
| Device proposal/integration | IoT: raw observations/integrations; exact device-versus-facility configuration mapping V11-DES-04. Operations accepts occupancy/claims. |

Actual policy/agreement uploads are required. User-owned versioned assets use existing [media ownership/access/lifecycle](../../02-architecture/service-boundaries/media-contract.md) and [media SRS](../../01-requirements/srs/modules/09-file-media.md). Upload/finalization, protected read, allowed formats/limits and retention classification are V11-DES-01; no arbitrary document URL shortcut. Preserve original/replacement bytes and actor/time/reason evidence under lifecycle/holds; never silently overwrite the approved version.

## Review, provisioning and additional lots

Admin may replace **only uploaded agreement/policy documents on the initial pending application**. Other fields and pending additional-lot applications are not editable. Final approved/rejected records are immutable. Bind the review to expected application/document versions to prevent concurrent unseen changes. Approval after replacement sends **“Your application was approved with some modifications”**; it is ordinary approval, not a third outcome. Discussions occur outside SmartPark; no in-app corrections/re-acceptance workflow.

Approval commits separately from provisioning. User coordinates service-owned organization/membership and submitted configuration; Facility provisions the same first lot with durable identifiers and retry/unknown-outcome handling. Operations/Payment/IoT own their accepted records. Initial pending/failed provisioning blocks login and remains visible; never duplicate sites or represent a failure as business rejection. Approval email distinguishes decision from login readiness. Activation authorizes a lot; Facility/Operations readiness and valid Payment terms where required govern opening. See [facility API](../../03-design/api/parking-structure-api-design.md).

An approved Owner submits each additional lot separately for Admin approval/rejection. Rejection affects neither Owner nor other approved lots. Rejected record remains immutable; **Draft as new lot application** creates a separate proposal, including identical content. Request retries still create only one new proposal/site. No Admin document-edit permission applies to these applications.

Initial Partner rejection requires recorded reason and safe email/login explanation; the same normalized email cannot register again in MVP. Re-enable removes only administrative disablement and cannot reverse rejection. Notification failure preserves committed account/application/review/provisioning work; track pending/failed delivery and retry without repeating business effects.

## BE implementation work and development transition

The prior handoff identified AuthController, AccountWorkflowController, OwnerApplicationsController, OwnerRegistrationDto and OwnerRegistrationService under UserService as implementation touchpoints. These are investigation starting points, not approved orchestration placement. Use API → Application → Domain with local Persistence and Infrastructure ports; no external orchestration in Domain or direct cross-service database writes. Legacy Parking Service tenant/site integration must be reconciled to User/Facility/Operations/Payment/IoT authority, not copied as target architecture.

The selected transition is **development database reset after this documentation update is reviewed and approved**, replacing migration/grandfathering of invalid pending applications. V11-DES-12 requires target environments/databases, named responsible teammate, reviewed reset/seed scripts and execution date before execution. Fix schema/seed/application incompatibilities first; this documentation task performs no reset, application edit or runtime test. New acceptance scenarios are NOT RUN in the [catalogue](../../04-testing/requirements-traceability/acceptance-scenarios.md).
