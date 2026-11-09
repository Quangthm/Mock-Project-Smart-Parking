# SPARK-243 deployment acceptance checklist

Use this for the remaining checks in an approved **development/staging** environment with synthetic identities, photos and plates. No deployment has been performed by the agent. Record the deployed commit/build identifiers, URLs, date, tester and sanitized results. The working-tree changes must be included in the build you deploy; the baseline SHA alone does not contain them.

## 1. Prepare the release

1. Review the [API](../../03-design/api/vehicle-registration-api-design.md) and [database boundary](../../02-architecture/database/microservices/01-user-access-service-db-docs.md#spark-243-canonical-schema-and-persistence-boundary). Use a newly provisioned staging database or one the database owner has confirmed already matches the current declarations. Do not reset or silently adapt an existing/shared database.
2. The sole feature schema is **`scripts/database/microservices/01-user-access-service-db.sql`**. Review and apply it through the normal schema-setup workflow. It uses CREATE IF NOT EXISTS and inline constraints; ALTER is limited to the three circular media FKs. The pre-existing session immutability trigger and fixed role seed are explicitly retained. There is no `06` overlay or vehicle revision trigger. Re-run the same script against the isolated aligned database to check idempotency and preservation of accepted fixture IDs.
3. `IF NOT EXISTS` does not add missing columns or remove an old prototype trigger. Stop if an existing database differs; obtain a reviewed transition plan before deployment. Deploy the matching UserService/frontend build only after schema setup. Preserve legacy/null-owner records and accepted IDs. No existing data reset or migration is authorized by this checklist.
4. Reuse the existing server-side service key; no new OAuth provider is needed. Compose maps `SMARTPARK_SERVICE_KEY` to `Services__Key`. It must be at least 32 characters. Keep it out of browser configuration, source, screenshots and logs.
5. Enable approved test photo storage using the existing configuration: `SMARTPARK_USER_MEDIA_ENABLED=true` and `SMARTPARK_CLOUDINARY_CLOUD_NAME`, `SMARTPARK_CLOUDINARY_API_KEY`, `SMARTPARK_CLOUDINARY_API_SECRET`. Compose maps these to `MediaStorage__Enabled` and `MediaStorage__Cloudinary__*`; direct hosting uses those destination names. Use the approved secret store. Do not reuse production photos for synthetic tests.
6. Prepare separate Driver Accounts A, B and C with valid sessions. If available, also prepare two eligible Driver Accounts for the same User and an existing synthetic booking for A. Never paste credentials into the acceptance report.

## 2. Test the two real screens

Choose unused plates supported by the application; the examples below are suitable only if unused in your test environment.

| Action | Expected result |
|---|---|
| A opens vehicle management, types ` 51a-123.45 `, selects CAR and uploads a real JPEG/PNG/WebP | Successful active vehicle; reload retains original raw input (including spaces/case) and canonical `51A12345`; owned image reads through the API |
| A registers `59-x1 123.45` as MOTORCYCLE | Both categories appear in management; only the CAR appears/autoselects in Specific Slot |
| B uploads its own photo and submits `51A12345` | HTTP 409; one separate pending registration after refresh/reopening; no success toast or selectable vehicle from that submission |
| B repeats with `51-A-12345` and another photo | Same pending ID/time/original photo, no new active vehicle; use the private GET response to compare |
| A refreshes | Original vehicle ID and image remain unchanged; if A had a real synthetic booking, record its unchanged ID/status separately |
| C signs in and requests B's image URL | Common 404; C sees no B claim in its pending list |
| Use each account in both manager and booking form; switch Account while save/upload is pending | Old response cannot populate new Account's form, list or image; pending/MOTORCYCLE cannot advance slot booking to payment |
| Browser network offline during save or during the refresh after a 409, then reconnect | No false saved/pending confirmation; refresh obtains authoritative active/pending status; typed input survives retryable failures |
| Safe own plate-format/image edit | Stable ID; same canonical/category; expected image replacement semantics |
| Change canonical plate or category on an existing vehicle | 503 `VEHICLE_DEPENDENCY_UNAVAILABLE`; original binding unchanged. Full removal remains deferred |
| Old URL-only/missing image record | Safe reupload-required behavior; arbitrary URL is not accepted as a managed image |

The exact pending text is: “Registration pending dispute review. This vehicle can’t be used for booking.” A photo expires 30 days from upload admission; an unresolved claim does not reset or extend that deadline. Do not alter a shared database clock or delete evidence to test expiry; use an isolated fixture or wait for a controlled test record.

## 3. Verify live image behavior

The browser uses `POST /api/vehicles/images`, multipart `file` + a nonempty `draftIntentId`, with a stable `Idempotency-Key` per upload attempt. Only a ready asset ID can be attached. A 202 upload is still pending, not a usable photo. Check metadata via `GET /api/media/images/{assetId}` and content via `/content` using the same Account.

Verify a valid JPEG/PNG/WebP; a real image exactly 5,242,880 bytes; one byte over; a text/HTML file renamed `.jpg`; a foreign Account's asset; an avatar-purpose asset; and an interrupted/provider-failed upload. Expected: valid owned bytes accepted; oversized/disguised rejected; missing/foreign/wrong-purpose references share safe media errors; failures create no active or confirmed pending registration. Capture status/code and redacted response only. Reuse safe fixture image files for boundary cases; do not assume an arbitrary renamed file is a valid exactly-sized image.

## 4. Verify the service lookup from a trusted backend client

Use Postman or your existing server-side API client. The service key must never be sent by the frontend. For real Account/vehicle IDs from the synthetic fixtures:

```http
POST /internal/vehicle-bindings/resolve
X-Service-Key: <existing server secret supplied privately>
Content-Type: application/json

{"accountId":"<A Account UUID>","vehicleId":"<A vehicle UUID>","expectedRevision":1}
```

| Request/condition | Expected |
|---|---|
| Missing/wrong service key, even with a human token | 401 `INVALID_SERVICE_KEY` |
| Exact active CAR | 200 ACTIVE, correct IDs, CAR, revision; no plate/image/claimant data |
| Exact active MOTORCYCLE | 200 ACTIVE with MOTORCYCLE; Operations must independently reject Specific Slot use |
| B Account + A vehicle; missing vehicle; B claim ID; controlled removed/unbound fixture | Common 200 INELIGIBLE, no other-holder details |
| A active vehicle + deliberately different positive `expectedRevision` | 409 `VEHICLE_BINDING_STALE` |
| Restart an isolated instance with service key unconfigured | Startup refuses required configuration or lookup denies; never anonymous access |
| UserService unavailable to an actual Operations test caller | Caller records unavailable/unknown, does not accept a booking based on guessed eligibility |

Shared-key holders all have lookup access temporarily. SERVICE-AUTH-01 will replace that trust mechanism later. A successful read is not a distributed reservation lock; Operations must implement its own current binding/revision revalidation and dependency coordination before reservation acceptance can be signed off.

## 5. Current PostgreSQL acceptance after the architecture rewrite

The previous 70-pass result predates this rewrite. Re-run the focused integration cases on a separate disposable PostgreSQL server using the real handlers/repositories/UoW. Set `SMARTPARK_AUTH_TEST_CONNECTION` through your secret configuration; tests create/drop uniquely named fixture databases, so never use a shared deployed application database.

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --nologo -m:1 --filter 'FullyQualifiedName~VehicleRegistrationTests|FullyQualifiedName~MediaAsset' --logger 'trx;LogFileName=spark243-repository-postgres.trx'
```

Require zero failed/skipped cases before treating PostgreSQL behavior as verified. This covers real claim/active races, atomic rollback, exact Account HTTP scope, media, binding resolver, UoW revision and replay of the canonical schema. The updated tests inject failures in test code or test constraints, without creating vehicle functions/triggers. Also verify existing installations through their owner-approved transition; fresh-schema replay is not migration evidence.

## 6. Historical broad-suite timeout follow-up

Use a **separate disposable PostgreSQL test server**, not the deployed application's database. The tests create/drop uniquely named test databases and require appropriate privileges. Configure `SMARTPARK_AUTH_TEST_CONNECTION` privately in the test shell. Run each separately, without another suite competing for that instance:

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --nologo -m:1 --filter 'FullyQualifiedName~SessionSchemaReconciliationTests.ConsolidatedBaseIsCompleteAndLegacyNullSessionsRemainImmutable' --logger 'trx;LogFileName=spark243-schema-recheck.trx'
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --nologo -m:1 --filter 'FullyQualifiedName~ProfileOutboxTests.ScopedPublicationCommitsWithNameAndRollsBackWithName' --logger 'trx;LogFileName=spark243-outbox-recheck.trx'
```

Attach sanitized failure details if they recur. Do not classify either as fixed until the rerun passes. Additional isolated QA can force both race-winning orders. Schema mismatches require owner review; CREATE IF NOT EXISTS is not an automatic schema verifier or repair mechanism.

## 7. Return evidence

Report each checklist item as PASS / FAIL / NOT RUN, deployed build ID, synthetic Account aliases, status/error codes, stable vehicle/claim IDs and redacted screenshots where useful. Exclude tokens, service keys, provider credentials and real personal photos. A build or synthetic-provider test is not proof of live provider/reviewer/Operations integration.

Acceptance remains pending until the relevant live items pass. Review/adjudication, evidence preservation holds, automatic cleanup, removal/transfer and OAuth rollout remain deferred; this checklist does not authorize those implementations.
