# Service authentication — future direction and SPARK-243 options

**Date:** 7 November 2026 (Asia/Saigon)
**Source:** User instruction to backlog option 2 as a future/refactoring direction and provide a simpler current plan or defer service security.
**Status:** Future direction and deferral APPROVED. Subsequent user approval selects a small protected lookup using the **existing shared service key** temporarily. The earlier dedicated-key proposal and endpoint-deferral alternative below are superseded for this slice.

## Approved temporary implementation — 8 November 2026

The user explicitly selected reuse of existing `Services:Key`, supplied as `X-Service-Key`, for the binding lookup. Missing, invalid or unconfigured keys deny access. No human Account/session is fabricated. The lookup exposes only exact Account/vehicle binding, real category and revision; it does not authorize photo access or transfers.

**Accepted limitation:** every holder of the existing shared key can call this lookup. It cannot distinguish Operations from other key-holding services and is not the originally proposed least-privileged service identity. This explicit temporary exception supersedes the earlier proposal to require a dedicated credential. Keep secrets out of source/browser/logs and use protected transport outside isolated development. SERVICE-AUTH-01 retains the deferred replacement, caller isolation and migration work. No provider rollout is authorized now.


## Approved future direction: SERVICE-AUTH-01

This is a repository backlog identifier, **not a created Jira issue**.

- **Disposition:** FUTURE / refactoring direction; implementation DEFERRED outside the current SPARK-243 slice.
- **Direction:** Standardize internal HTTP service identity on established identity-provider-issued, short-lived service tokens using OAuth client credentials, destination restrictions and explicit endpoint permissions. Certificate/asymmetric client authentication can be evaluated as part of that future design; no certificate infrastructure is selected now.
- **Owner:** Architecture/security and infrastructure owners with affected service owners; named assignees and schedule unassigned.
- **Unselected details:** Provider, hosting, dependencies, token lifetime, credential/certificate issuance and rotation, revocation semantics, outage/key-cache policy, migration order and deployment date.
- **Scope:** Shared contract and setup, token acquisition/caching and API validation, a first tested service pair, then an inventoried gradual migration. Existing human Account/session authentication and resource ownership checks remain distinct. RabbitMQ trust requires its own transport-specific design.
- **Acceptance required:** Real issuance and authenticated lookup; correct caller/destination/permission enforcement; human-token rejection on machine-only endpoints; expired/invalid tokens; concurrent refresh; issuer outage/recovery; signing-key and client-credential rotation; safe logs; documented setup and endpoint cutover without an authentication bypass.
- **Migration boundary:** Preserve the binding lookup's business response and Account/vehicle checks when replacing its authentication adapter. Retire any interim credential only through an explicit, tested cutover. Future standardization does not authorize a current whole-system migration.

## Historical proposal A — dedicated credential (not selected)

1. Complete the authorized registration slice: exact Account scope, CAR/MOTORCYCLE registration, atomic pending-claim intake, owned photo association, raw input, safe edits and CAR-only booking selection.
2. Protect the new binding lookup with one dedicated Operations credential, accepted only for this lookup. Use the normal API authentication/policy mechanism, separate from human session authentication. Do not add a multi-client administration platform or identity-provider deployment.
3. Keep the credential in environment-owned secret configuration, outside source and the browser. Deny access when absent/invalid; do not fall back to the existing widely shared service key. Use protected transport outside the documented isolated development boundary. Document replacement/rotation and redact the credential from logs.
4. Return only exact Account/vehicle binding, real category and binding revision. No plate, photo or claimant evidence is exposed by this permission. Existing ownership checks and identity-change guards still apply.
5. Verify configured access, missing/wrong credentials, wrong Account/vehicle, pending IDs, removed bindings and stale revisions. Publish the caller setup for the Operations owner; its reservation workflow remains a later integration.

**Current impact:** UserService producer/policy, credential provisioning, and the Operations caller contract. Other services and the frontend do not need an authentication migration. Credential distribution/rotation is manual and possession-based; this is not the future centralized identity system. No new provider or package is authorized by this proposal.

## Historical alternative B — defer lookup (not selected)

Complete registration, pending claims/photos and the Driver UI with their existing authenticated user controls. Retain the binding-read design, but defer its HTTP endpoint and service-authentication implementation together. Do not expose an unauthenticated resolver, use direct cross-service database access or present a fake resolver as integration evidence.

**Consequence:** Driver registration can be delivered independently, but the original plan's live authoritative binding-read acceptance and reservation backend handoff remain blocked. Record SPARK-243 as partial against the original full plan. Existing security controls are not disabled by this deferral.

## Approval boundary

The user subsequently approved the shared-key variant described above and explicitly requested implementation. Do not reopen this settled temporary choice or silently treat it as caller-isolated OAuth security.

## References

- [Authorization ADR, service trust](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md#18-service-to-service-trust)
- [Vehicle API design](../../03-design/api/vehicle-registration-api-design.md)
- [Claim-photo decision](2026-11-07-vehicle-claim-images.md)
- [Implementation register](deferred-and-implementation-register.md)
