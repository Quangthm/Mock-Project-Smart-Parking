# SPARK-179 / SPARK-196 — Owner registration and Admin review

Implements UC-AUTH-02: company name, email and phone are required; Admin review gates Owner operations. Contract uses the existing FE partnership form. Rejection criteria remain a manual Admin decision; no automatic eligibility rules or notification promise is introduced.

## Register

`POST /api/auth/register/owner` — anonymous; shared registration rate limit of 10 requests/minute/IP per instance.

```json
{
  "fullName": "Nguyen Van A",
  "businessName": "ABC Parking",
  "email": "owner@example.com",
  "phone": "0912345678",
  "password": "Password@123",
  "lotType": "basement",
  "agreedToPolicy": true
}
```

All fields are required. Name/company/email maximum 255 characters; phone optional leading `+` followed by 9–15 digits; lot type `outdoor`, `basement`, or `multi-storey`. Password follows existing registration policy: 8–15 characters, upper/lowercase, digit and special character. Terms acceptance must be true. Email is trimmed and lowercased. Password is stored as a bcrypt hash.

`201 { "success": true, "data": <application> }`. No session is issued. User is `PENDING_APPROVAL`; its `BUSINESS_OPERATOR` account is `SUSPENDED` with role `BUSINESS_OWNER`. Pending/rejected users cannot log in, refresh or make protected requests. Contacts remain reserved after rejection; resubmission/account deletion is a separate workflow.

## Admin list and review

- `GET /api/owner-applications` → `200 { "success": true, "data": [<application>] }`, newest first. Currently returns the full list; pagination is a future extension.
- `PATCH /api/owner-applications/{id}/review` with `{ "status": "approved", "reviewNote": "Checked company details" }` → `200 { "success": true, "data": <application> }`.

Both endpoints require an active Admin session. Identity comes from the validated bearer token, never the request body. Current database role is checked during authentication and again in the service. Canonical roles `PLATFORM_ADMIN`, `BUSINESS_OWNER`, `SITE_OPERATOR` map to FE/JWT `admin`, `owner`, `operator`; short auth role aliases remain supported.

Review status must be `approved` or `rejected`. Note is optional, maximum 2000 characters. Only pending applications/users can be reviewed. Approval atomically activates the user and Owner account; rejection records `REJECTED` and keeps the account suspended. Row locks serialize competing decisions; repeated/finalized reviews return 409. Reviewer ID, timestamp and note persist with the application as review evidence.

Application fields: `id`, `ownerId`, `ownerName`, `businessName`, `email`, `phone`, `lotType`, `status`, `submittedAt`, nullable `reviewedAt`, `reviewedBy`, `reviewNote`. Passwords/hashes/tokens are never returned.

## Errors

| HTTP | Code / behavior |
|---|---|
| 400 | DTO validation (ASP.NET validation response), or service `VALIDATION_FAILED` |
| 401 | Missing/expired/revoked bearer, inactive account or changed role; `INVALID_TOKEN` |
| 403 | Non-Admin bearer (framework forbidden); service `FORBIDDEN` |
| 404 | `APPLICATION_NOT_FOUND` |
| 409 | `CONTACT_EXISTS`, `APPLICATION_CLOSED` |
| 429 | Registration rate limit |

Service errors use `{ "success": false, "code": "...", "message": "..." }`. Login retains its generic `AUTH_FAILED` response for pending/rejected users.

## Database and operation

Apply `scripts/database/05.5-Owner-Registration-Approval.sql` after existing 05.2/05.4 migrations. It adds `owner_applications`, ensures the existing canonical Owner role and normalized email uniqueness; it does not create an Admin, alter existing user permissions or create a tenant/parking site. It is re-runnable. Existing environments need an active account with `PLATFORM_ADMIN` (or supported `ADMIN` alias) to use the Admin screen.

FE Business Sign Up and Admin Applications now use these APIs. They no longer create/review localStorage accounts. Admin has explicit refresh, loading/error states and a separate note per application. Partnership screen does not promise an email delivery or an SLA. Notifications, automatic acceptance criteria, tenant/site provisioning, user administration and other dashboard fixture data are outside these two tasks.
