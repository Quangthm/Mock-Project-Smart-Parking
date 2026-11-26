# SPARK-273 Driver reservation to Payment order

Use this guide on an isolated local SmartPark stack. **05.1** verifies payable order readiness with a future `holdExpiresAt` and `confirmedAt = null`. **05.2** verifies Operations confirmation after a controlled successful test settlement. Use the current tester checklist for acceptance, durability and evidence capture. [Checkpoint E tester handoff](../reports/SPARK-273-checkpoint-e-tester-handoff.md)

- [Import the collection](./SPARK-273-Reservation-Flow.postman_collection.json)
- [Import the blank local environment](./SPARK-273-Local.postman_environment.example.json)
- [Activation and prior integration evidence](../reports/SPARK-273-reviewed-admission-integration.md#activation)

## 1. Prepare the local stack

Run PowerShell commands from the repository root. Setup and seed commands below apply to approved local development data. The read-only checks can also be repeated against an existing local stack before changing anything.

### Check tools and configuration

**Required · read-only.** Verify the checked-out branch and tool versions. Docker Desktop should be running in Linux-container mode. Use Windows PowerShell 5.1 or PowerShell 7, Docker Compose v2 with `up --wait` support, and Postman Desktop. Use .NET SDK 10 for the standard local setup and direct host builds. The Docker backend image builds with its own SDK.

```powershell
git branch --show-current
git rev-parse --short HEAD
$PSVersionTable.PSVersion
docker info --format '{{.OSType}}'
docker compose version
dotnet --version
Test-Path -LiteralPath .\.env -PathType Leaf
```

Expect `feature/SPARK-273-driver-book-parking-ahead-of-arrival` or the branch under test, `linux`, Compose v2, .NET `10.x` where applicable, and `True` for a configured checkout. Start Docker Desktop if `docker info` cannot reach it. For a missing `.env`, follow [LOCAL-SETUP.md](../../../LOCAL-SETUP.md) to initialize it from [.env.example](../../../.env.example) before bringing up services. Keep a known-good existing `.env` and reconcile missing settings locally.

| Required for this scenario | Where to check |
|---|---|
| Database access and bootstrap identities | Root `.env`. Use `SMARTPARK_DEV_PASSWORD` from the existing local volumes and the `SMARTPARK_ADMIN_*`, `SMARTPARK_OWNER_*`, and `SMARTPARK_OPERATOR_*` settings expected by the app runner. |
| Driver JWT and JWKS trust | UserService signing key, issuer, audience, key ID, and JWKS route. OperationService must trust the same issuer and audience and reach the UserService JWKS from inside its runtime network. |
| Reviewed quote and admission | Set `SMARTPARK_RESERVATION_ADMISSION_ENABLED`, `SMARTPARK_RESERVATION_SAGA_ENABLED`, `SMARTPARK_RESERVATION_FACILITY_ENABLED`, `SMARTPARK_RESERVATION_REVIEWED_SETTLEMENT_ENABLED`, and `Messaging__OutboxDeliveryEnabled` to `true` once the dependencies are ready. |
| Facility and Payment owner calls | Set the dedicated matching Operations signing and verifier material using `SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PATH` and `SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PATH` or the supported PEM inputs. The app runner mounts the configured key files into their respective containers. Verify Facility and Payment URLs from Operations. The base Compose calls use HTTP internally, so set `SMARTPARK_RESERVATION_FACILITY_ALLOW_HTTP=true` only for an approved isolated local HTTP stack. This opt-in also gates the Payment owner call. |
| RabbitMQ and outbox | Verify the configured vhost, pre-provisioned User, Facility, and Operations reservation credentials and permissions, and Payment's broker configuration. The `SMARTPARK_RESERVATION_USER_BROKER_*`, `SMARTPARK_RESERVATION_FACILITY_BROKER_*`, and `SMARTPARK_RESERVATION_OPERATION_BROKER_*` settings select reservation credentials. Provision bindings and permissions through the existing owner workflow. |

Use the [activation matrix](../reports/SPARK-273-reviewed-admission-integration.md#activation) and [messaging deployment checks](../reports/SPARK-273-deployment-checklist.md#3-messaging-and-audit-provisioning) for service-specific details. Postman uses the Driver session and requires no service signing material or provider credentials for this scenario.

### Check UserService Docker TLS

**Required if enabled · read-only.** The User and All app runner modes validate TLS when `SMARTPARK_USER_DOCKER_TLS_ENABLED=true`. Check configuration without displaying passwords or paths.

```powershell
$settings = & .\scripts\load-env.ps1 -EnvPath .\.env -NoExport
$tlsEnabled = [string]$settings['SMARTPARK_USER_DOCKER_TLS_ENABLED']
$pfxPath = [string]$settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PATH']
if ($pfxPath -and ![IO.Path]::IsPathRooted($pfxPath)) {
    $pfxPath = Join-Path (Get-Location).Path $pfxPath
}
[pscustomobject]@{
    Enabled = $tlsEnabled -ceq 'true'
    FlagValid = $tlsEnabled -cin @('', 'true', 'false')
    PfxFileExists = [bool]($pfxPath -and (Test-Path -LiteralPath $pfxPath -PathType Leaf))
    PasswordConfigured = [bool]$settings['SMARTPARK_USER_DOCKER_TLS_CERTIFICATE_PASSWORD']
    AssistantVehicleReadEnabled = $settings['Assistant__Reads__User__Enabled'] -eq 'true'
    DiscoveryDockerEnabled = $settings['SMARTPARK_DISCOVERY_DOCKER_ENABLED'] -eq 'true'
}
```

For an enabled overlay, expect a valid flag, an existing PFX file, and a configured password. The runner resolves a repo-relative path to an absolute path. It also opens the PFX with its password and requires a currently valid certificate, private key, and DNS SAN `user-service`. Assistant vehicle reads over HTTPS and Docker Discovery also need the appropriate trusted public User CA. Check those consumers before disabling TLS for a reservation-only stack. If you see `User Docker TLS requires an existing absolute PFX path and its password.`, correct the path or password in your private configuration. Follow the [existing TLS procedure](../../../LOCAL-SETUP.md) for certificate and CA requirements and retain your existing keys and keyring.

### Start and verify the services

**Required when missing · startup changes local state.** On a fresh prepared checkout, use the standard local setup to provision PostgreSQL and RabbitMQ. Review the supported existing-volume upgrade procedure with the service owners before applying schema changes to an existing stack.

```powershell
.\setup.ps1 -SkipSonarQube
```

When the local stacks and runtime inputs are configured, start the services with the app runner. The four commands keep the launch scoped to this flow. Use `-NoBuild` only when the relevant images already match the source under test and you are reapplying configuration.

```powershell
.\scripts\run-app-service.ps1 -Service User -NoLogWindow
.\scripts\run-app-service.ps1 -Service Facility -NoLogWindow
.\scripts\run-app-service.ps1 -Service Operation -NoLogWindow
.\scripts\run-app-service.ps1 -Service Payment -NoLogWindow
```

Expect the runner to wait for backend health. The first UserService startup can write demo identities. When a start fails, check the affected service logs and port bindings. Use the runner again for configuration changes so Compose can recreate the affected container.

**Required · read-only.** Check the database and app projects, then probe the host ports. With the User TLS overlay, internal HTTPS is separate from default host HTTP on `5035`.

```powershell
docker compose --project-name smartpark-schema --env-file .env -f compose.schema-integration.yml ps
docker compose --project-name smartpark-app --env-file .env -f compose.app.yml ps

foreach ($port in 5035, 5045, 5055, 5065) {
    $uri = "http://localhost:$port/health"
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 5
        '{0} -> HTTP {1}' -f $uri, $response.StatusCode
    } catch {
        '{0} -> failed ({1})' -f $uri, $_.Exception.GetType().Name
    }
}
```

| Service | Default base URL |
|---|---|
| UserService | `http://localhost:5035` |
| FacilityService | `http://localhost:5045` |
| OperationService | `http://localhost:5055` |
| PaymentService | `http://localhost:5065` |

Expect HTTP 200 from each `/health`, with the four service databases and RabbitMQ running. For a failure, check `docker compose ps` for the actual port and service state. A 200 establishes process reachability. Use the following checks for workflow readiness.

- **JWT/JWKS.** From the OperationService container, check the configured JWKS route with `docker compose --project-name smartpark-app --env-file .env -f compose.app.yml exec -T operation-service curl --fail --silent --show-error --max-time 5 --output /dev/null --write-out '%{http_code}\n' http://user-service:8080/.well-known/jwks.json` for the base Compose defaults. Expect 200. Adjust the address for configured trust overlays. Verify the Driver profile in Postman to exercise JWT validation.
- **RabbitMQ.** In the local management UI (default `http://localhost:15672`), select the configured vhost and inspect the reservation queues for consumers, backlog, and error or skipped queues. Check owner request, decision, shared settlement and directed financial queues against the [deployment checks](../reports/SPARK-273-deployment-checklist.md#3-messaging-and-audit-provisioning). A running broker or enabled outbox flag alone does not prove messages are delivered.
- **Owner facts and data.** Verify the site, slot, policy, and tariff in the next section. A successful `02.1` exercises signed Facility lookup, Operations policy resolution, and Payment quote issuance. It persists a quote, so run it as a test step.

Use only status and presence checks for credentials. Avoid displaying the full rendered Compose configuration or full container environment.

## 2. Prepare demo data

### Confirm the Driver, owner, and operator

**Required for the demo seed.** The identity precheck in the seed needs exactly one non-deleted matching account per role. Owner and operator emails come from `SMARTPARK_OWNER_EMAIL` and `SMARTPARK_OPERATOR_EMAIL`, with defaults `owner@smartpark.local` and `operator@smartpark.local`. The default Driver is `driver@gmail.com`. Use the [pgAdmin lookup map](#pgadmin-lookup-map) to distinguish a missing user, missing role account, duplicate match, or inactive account. The seed count checks role and deletion status. Sign-in separately needs a usable account.

UserService startup still supports development bootstrap. Owner and operator seeding is independent of `SeedDemoUser`. The Driver startup path requires that flag and an empty users table. A restart cannot backfill the Driver after the other users exist. An `AUTH_FAILED` after editing a bootstrap password in `.env` can indicate that the existing user still has the original password hash. Verify the actual account, sign-in settings, and lockout state before retrying. [Startup logic](../../../src/Services/UserService/SmartParking.UserService.API/Program.cs) · [Seeder](../../../src/Services/UserService/SmartParking.UserService.Persistence/DataSeeder.cs)

**Optional · local demo recovery.** The first command sends login probes for the configured demo accounts. It can create sessions or update failed-attempt counters and lockouts. It cannot establish which database role row is missing. An MFA challenge may also produce a successful HTTP response without a complete session. Use the database lookup for a read-only check.

```powershell
.\scripts\repair-demo-identity.ps1
```

Use `-Apply` only when you explicitly intend to repair the standard local demo identities. It can create the missing Driver or its role account, refresh the configured admin, owner, and operator password hashes, clear their lockouts, and activate and verify the demo operator. It requires the configured demo credentials. Review the resulting account summaries and test login under the expected role.

```powershell
.\scripts\repair-demo-identity.ps1 -Apply
```

### Seed the site and select a real CAR slot

**Required for a new DEMO-HQ fixture · writes local demo data.** Choose one invocation. An existing Driver with the correct role can be used without the identity repair.

```powershell
.\scripts\seed-demo-site.ps1
# Or, to use an existing Driver account:
.\scripts\seed-demo-site.ps1 -DriverEmail '<existing-driver-email>'
```

Expect **Demo data ready**. The script writes demo membership and vehicle records to User, the site and slots to Facility, and structure capacity pools, explicit demo quotas, a legacy site policy, and demo history to Operations. Existing demo records may be updated. The success banner establishes fixture counts. Continue with the reviewed policy and tariff checks. Use a normally created Owner site for the [unconfigured-site acceptance case](#owner-created-site-and-optional-quota-acceptance).

| DEMO-HQ fixture | Where to verify it |
|---|---|
| Tenant `a0000000-0000-4000-8000-000000000001`, site `b0000000-0000-4000-8000-000000000001` | Facility `parking_sites`, filtered by `site_code = 'DEMO-HQ'`. |
| Default Driver `driver@gmail.com`, display plate `51A-123.45`, canonical plate `51A12345` | `01.1 Resolve registered CAR` returns the current Driver's `canonicalPlate`. The [identity seed](../../../scripts/database/seeds/demo/01-identity.sql) attaches the demo vehicle to the selected Driver. |
| Example slot code `CarParkingSpace_001` | With the current Owner's authorized session, call Facility `GET /api/parking-lots/{siteId}/structure` and find the slot in `data.slots`. Alternatively inspect Facility `parking_slots` and `slot_compatibilities` below. Use the current row's `id`. |

The [Facility seed](../../../scripts/database/seeds/demo/02-facility.sql) computes new slot IDs with `md5('demo-slot-' || slot_code)::uuid`. That yields `b12892d2-2118-7542-3eb8-ebf992e6cb46` for `CarParkingSpace_001` on a fresh seed. Verify the actual row is current and CAR-compatible. The live quote's signed Facility lookup checks eligibility. Admission later checks current capacity and overlap before committing a hold.

### Confirm effective reservation policy

**Required · inspect first.** Operations needs one effective platform revision with the required values and one effective site revision for the Facility-resolved tenant and site. The site must explicitly choose late-arrival tolerance. Numerical values can inherit from the platform, and a different override needs approved bounds. The platform cancellation timezone must be `Asia/Ho_Chi_Minh`. The seed's count of one site policy can pass with a null late-tolerance flag or a missing platform revision. [Policy resolver](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs)

Use the [pgAdmin lookup map](#pgadmin-lookup-map) to check the effective rows before running the dedicated seeder. For a missing site policy, omit `-ReplaceExisting`. For a confirmed legacy site revision you intend to supersede on this local DEMO-HQ stack, use this explicit recovery.

```powershell
.\scripts\seed-site-reservation-policy.ps1 -SiteId 'b0000000-0000-4000-8000-000000000001' -ArrivalLateToleranceEnabled:$false -ReplaceExisting
```

`-SiteId` selects the existing Facility site. `-ArrivalLateToleranceEnabled:$false` explicitly turns off late-arrival tolerance for this test fixture. Use `$true` only if the intended local scenario calls for it. `-ReplaceExisting` closes the old effective revision and creates a new inheriting revision while preserving history. With the supplied platform defaults, the inherited payment hold is five minutes. The script loads the [platform defaults](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql) if there is no effective platform policy and the seed can be applied. Expect an `Effective policy: … | version: … | late tolerance enabled: false` line, then confirm both effective rows. Stop if the script reports an invalid or ambiguous platform policy or a scheduled revision. No app restart is needed after a successful data change. [Policy seed guide](../../../scripts/database/seeds/README.md#reservation-policy-for-an-existing-local-site)

### Confirm the reviewed CAR tariff

**Required · inspect first.** In Payment, verify an active non-deleted CAR tariff scoped to the same tenant and site and an effective `TARIFF` revision with usable time blocks. The [current Payment baseline](../../../scripts/database/microservices/04-payment-pricing-service-db.sql) includes a DEMO-HQ CAR tariff and first revision on a fresh initialized volume. Verify both on an existing volume. The legacy price preview can provide an estimate. Reviewed `02.1` uses a separate revision lookup. [Reviewed tariff lookup](../../../src/Services/PaymentService/SmartParking.PaymentService.Persistence/Repositories/ReviewedTariffRepository.cs)

**Optional · writes Payment demo data.** If the test site lacks a CAR tariff and the baseline template exists, the existing helper can add a site tariff and its first revision. It requires an active Facility site and exactly one active owner for its tenant. Use the actual site ID.

```powershell
.\scripts\seed-site-car-tariff.ps1 -SiteId 'b0000000-0000-4000-8000-000000000001'
```

Expect an `Active CAR tariff` summary and verify the matching effective revision separately. The helper can preserve an existing active tariff without repairing its absent or invalid revision. Escalate that case to the Payment owner for a supported configuration repair. Avoid reapplying a schema baseline or editing existing policy rows to make the test pass. [Tariff seed guide](../../../scripts/database/seeds/README.md#car-tariff-for-an-existing-local-site)

### Owner-created site and optional quota acceptance

Use the updated Operations schema and build on a disposable local stack. [Schema adoption](../../02-architecture/database/microservices/03-parking-operations-service-db-docs.md#local-adoption-of-the-reservation-quota-schema)

1. Create an Owner site and its parking structure through the application. Prepare two eligible CAR slots and two eligible Drivers with distinct registered plates. Seed the effective reservation policy and CAR tariff for that site using the existing scripts above, substituting the new site ID. Confirm the tenant/site scope and the absence of an active CAR row in `reservation_quota_configs`. Perform this case without running the DEMO-HQ seed or inserting any quota or pool row by hand.
2. As a Driver, review a positive-deposit quote for a concrete slot, submit the exact selection with a fresh key, and record the Account, plate and slot results. Expect `HOLD_CREATED`, an Operations reservation in `PENDING_PAYMENT` with H, and Payment checkout readiness after asynchronous order creation. Take the other eligible Driver through a fresh review for the same slot and overlapping interval while H is live. Expect `SLOT_TIME_CONFLICT` and no second order. The underlying booking can be checked without making a transfer.
3. On a separate normally created disposable test site, use the same setup and explicitly configure active CAR quota `1` in the Operations database. Use the actual UUIDs and run only on the approved test fixture. The current product has no Owner quota editor or public quota configuration endpoint.

   ```sql
   INSERT INTO public.reservation_quota_configs (tenant_id, site_id, vehicle_type, max_reservation_quota)
   VALUES ('<tenant-uuid>'::uuid, '<site-uuid>'::uuid, 'CAR', 1);
   ```

4. While the first positive hold is live, submit another eligible booking for a different slot with an overlapping effective interval. Expect `RESERVATION_QUOTA_EXCEEDED` with no competing reservation or order. Submit for a non-overlapping interval beyond any accepted booking buffer and expect admission. On a separate quota-zero fixture, even the first eligible booking must reach `RESERVATION_QUOTA_EXCEEDED`. Record the intervals, H, request codes and checkout evidence for the two sites. These cases are pending tester execution.

### pgAdmin lookup map

**Optional · read-only inspection of an authorized local demo database.** In pgAdmin use **Register > Server**, then **Connection**. Set host `127.0.0.1` and the published port below. Use the database as **Maintenance database** and the authorized local read-only account if available. On the standard dev database the bootstrap username matches the database name and its configured password comes from the private root `.env`. Enter it privately in pgAdmin. Obtain the Postman Driver password from your local test credential source. In the browser tree open **Databases > database > Schemas > public > Tables**. Use the Query Tool with named columns and filters for inspection. Keep credentials and raw personal data out of shared output. [pgAdmin server connection](https://www.pgadmin.org/docs/pgadmin4/latest/server_dialog.html) · [pgAdmin Query Tool](https://www.pgadmin.org/docs/pgadmin4/latest/query_tool.html)

| Database / default port | What to inspect |
|---|---|
| `smartpark_user` / `5441` (`SMARTPARK_DB_USER_PORT`) | `users` by `email` and `accounts` by `user_id = users.id`. Check `role_code` (`BUSINESS_OWNER`, `SITE_OPERATOR`, `DRIVER`), `status`, and `deleted_at` in both tables. For the selected Driver use `vehicles.account_id = accounts.id`, `vehicle_type = 'CAR'`, and `deleted_at IS NULL`. Copy `normalized_plate` for comparison with `01.1`. |
| `smartpark_facility` / `5442` (`SMARTPARK_DB_PARKING_PORT`) | `parking_sites` by `site_code = 'DEMO-HQ'` for `id` and `tenant_id`. In `parking_slots` filter that `site_id` and the slot code, require `deleted_at IS NULL`, and inspect `supported_vehicle_type` and `physical_state`. Confirm CAR in `slot_compatibilities` for the same `slot_id`. Copy `parking_slots.id` into `slotId`. |
| `smartpark_operations` / `5443` (`SMARTPARK_DB_RESERVATION_PORT`) | `operational_policies` for the active platform and site revisions as of now. Platform rows have null tenant and site IDs. Site rows use both DEMO-HQ IDs. After submission only, `reservation_requests.id` is the request ID, and `reservations.request_id` links it to the hold. Inspect `status`, `hold_expires_at`, `payment_order_id`, and `confirmed_at` on that hold. |
| `smartpark_payment` / `5444` (`SMARTPARK_DB_PAYMENT_PORT`) | `tariffs` by `tenant_id`, `site_id`, and `vehicle_type = 'CAR'`, with `is_active` and `deleted_at IS NULL`. Join `policy_revisions.policy_id = tariffs.id`, require `policy_type = 'TARIFF'`, and check `effective_from <= now()` with no `effective_to` or one in the future. After the test, `reservation_quotes.id` and `payment_orders.id` can corroborate the API results. Compare `payment_orders.reference_id` with the reservation ID. Use the collection to capture the quote and order values. |

For example, in **smartpark_operations** run this read-only query for DEMO-HQ. Expect one platform row and one site row. Compare `arrival_late_tolerance_enabled` on the site row and inspect the full policy through the owning service if any consistency check still fails.

```sql
BEGIN READ ONLY;
SELECT id, version, tenant_id, site_id, effective_from, effective_until,
       arrival_late_tolerance_enabled, payment_hold_duration_minutes,
       min_reservation_duration_minutes, max_reservation_duration_hours,
       max_advance_booking_days, cancellation_timezone
FROM public.operational_policies
WHERE is_active
  AND effective_from <= CURRENT_TIMESTAMP
  AND (effective_until IS NULL OR CURRENT_TIMESTAMP < effective_until)
  AND ((tenant_id IS NULL AND site_id IS NULL)
    OR (tenant_id = 'a0000000-0000-4000-8000-000000000001'
        AND site_id = 'b0000000-0000-4000-8000-000000000001'))
ORDER BY site_id NULLS FIRST, version;
COMMIT;
```

In **smartpark_payment**, this read-only check follows the reviewed lookup's tariff selection and effective revision ordering. An empty result means no active CAR tariff was selected. A row with a null `revision_id` means the selected tariff lacks an effective revision. If a revision appears, review its `policy_values` in pgAdmin for valid time blocks and rates.

```sql
BEGIN READ ONLY;
WITH selected_tariff AS (
    SELECT id, tenant_id, site_id
    FROM public.tariffs
    WHERE tenant_id = 'a0000000-0000-4000-8000-000000000001'
      AND site_id = 'b0000000-0000-4000-8000-000000000001'
      AND vehicle_type = 'CAR' AND is_active AND deleted_at IS NULL
    ORDER BY created_at DESC, id
    LIMIT 1
)
SELECT t.id AS tariff_id, r.id AS revision_id, r.revision_number,
       r.effective_from, r.effective_to
FROM selected_tariff AS t
LEFT JOIN public.policy_revisions AS r
  ON r.policy_id = t.id AND r.policy_type = 'TARIFF'
  AND r.effective_from <= CURRENT_TIMESTAMP
  AND (r.effective_to IS NULL OR CURRENT_TIMESTAMP < r.effective_to)
ORDER BY r.revision_number DESC
LIMIT 1;
COMMIT;
```

Confirm the actual ports with schema Compose `ps` if your `.env` overrides them. See the [owning schema initialization](../../../compose.schema-integration.yml) for the database names and mounted SQL files.

## 3. Import and fill the Postman environment

In Postman Desktop select **Import** and choose both JSON files from `docs/04-testing/postman/`.

| File | Purpose |
|---|---|
| `SPARK-273-Reservation-Flow.postman_collection.json` | Requests, auth, pre-request checks, and assertions. |
| `SPARK-273-Local.postman_environment.example.json` | Blank environment named **SPARK-273 Reservation Flow - local**, with three base URLs and `PAYOS` prefilled. |

Select that environment before sending any request. Enter credentials in local values. Keep them private and avoid exporting a populated environment. Send requests individually with **Send**. The collection calls `setNextRequest(null)` and expects manual review and consent between requests.

### What you fill in

For the default local stack, enter `driverEmail` and `driverPassword` before login. Supply `slotId`, `start`, and `end` before quote review. Enter `acceptedQuoteId` only after reviewing the quote. The table covers the conditional values.

| Variable | When required | Where the value comes from |
|---|---|---|
| `userBaseUrl` | Before **00.2**. Keep the prefilled value if it matches your stack. | `http://localhost:5035` by default. Confirm UserService host port and `/health`. |
| `operationBaseUrl` | Before **02.1**. Keep the prefilled value if it matches your stack. | `http://localhost:5055` by default. Confirm OperationService host port and `/health`. |
| `paymentBaseUrl` | Before **04.4**. | Payment host, default `http://localhost:5065`. Verify its health and Driver JWT trust. |
| `driverEmail` | Before **00.2**. | The Driver you seeded or registered. Default demo `driver@gmail.com`. Match the email used with `-DriverEmail` if supplied. |
| `driverPassword` | Before **00.2**. | The Driver's actual local sign-in password. Obtain it from your test credential source or the approved local fixture instructions. |
| `driverTurnstileToken` | Conditional before **00.2**. Leave blank when the environment accepts login without Turnstile. | Supply a current genuine token only when the configured UserService login requires one. |
| `licensePlate` | Leave blank before **01.1** if the Driver has exactly one eligible registered CAR. Fill it before resending **01.1** if you need to choose among several CARs. It must be populated by **02.1**. | Copy `canonicalPlate` from **01.1**. For the standard seeded CAR use `51A12345` if that response confirms it. |
| `slotId` | Before **02.1**. | Copy the UUID in Facility `parking_slots.id` from the structure read or pgAdmin. |
| `start` and `end` | Before **02.1**. | Choose a future interval using ISO 8601 with an offset, for example format `YYYY-MM-DDTHH:mm:ss+07:00`. Use the current effective policy and local clock. For the supplied platform defaults, choose a two-hour interval within the seven-day booking horizon, with `end` after `start`. The seeded minimum is 15 minutes and maximum is 24 hours. Keep the exact text unchanged between review and submission. |
| `paymentMethod` | Before **02.1**. Keep the prefilled value. | `PAYOS` for this test. |
| `acceptedQuoteId` | Leave blank through **02.1**. Fill before a fresh **03.1** only after reviewing and accepting the returned terms. | Copy the exact `quoteId` returned by **02.1**. |
| `legacyRequestId` and `legacyReservationId` | Leave blank for the paid scenario. Required only for the corresponding **08** checks. | An existing owned v2 receipt and its corresponding legacy reservation. Obtain the IDs from approved historical test evidence or the Operations lookup above, and verify v2 through the receipt API. |

### What starts blank and is filled automatically

| Filled by | Variables |
|---|---|
| **00.2**, after complete login | `driverToken`, `driverAccountId`, `driverUserId` |
| **01.1**, after eligible selection | `licensePlate`, `selectedCarPlate` |
| **02.1**, after successful review | `quoteId`, `reviewedQuoteExpiresAt`, `reviewedAccountId`, `resolvedSiteId`, `reviewedBookedAmount`, `reviewedDepositAmount`, `reviewedCurrency`, `reviewedSelection` |
| **03.1**, before sending | `reservationIdempotencyKey` (generated if blank), `submittedIntent` |
| **03.1**, after HTTP 202 | `requestId` |
| **04.1**, after `HOLD_CREATED` | `progressReservationId` |
| **04.2**, after matching the committed reservation | `reservationId` |
| **04.3**, after matching the reservation | `reservationState` and `holdExpiresAt`. **04.4** saves `checkoutState` and a payable `paymentOrderId` |

Leave these fields blank for a first run. `requestJson` is a transient request variable created by the scripts. Tenant, site, vehicle, and tariff IDs are resolved from the selected slot, plate, and owner records. Preserve the saved review and submission values for any unresolved attempt.

If **00.2** returns `mfaRequired=true`, finish the supported UserService login using `POST /api/auth/mfa/login` with its unexpired `challengeId`, `challengeToken`, and either `totp` or `recoveryCode`. Enter `driverToken` from the completed response's `data.accessToken` and populate `driverAccountId` and `driverUserId` from the token's `account_id` and `sub` claims using approved local tooling. Then run **00.3**. Keep the session token private.

## 4. Run the paid scenario

| Request | Action and prerequisites | Expected result and recovery |
|---|---|---|
| **00.2 Driver login** | Send with actual Driver credentials and the conditional Turnstile value. Login can write session or lockout state. | HTTP 200, `success=true`, `mfaRequired=false`, and a complete Driver token with Account and User IDs. Complete MFA if challenged. On `AUTH_FAILED` check the credential and account state before retrying. |
| **00.3 Verify Driver identity** | Send with the saved Driver token. | HTTP 200, role `driver` and `userId` matches `driverUserId`. On 401 or role mismatch, verify the selected environment and sign in with the intended Driver. |
| **01.1 Resolve registered CAR** | Send with the Driver token. If needed, choose one returned CAR's `canonicalPlate`, set `licensePlate`, and resend. | HTTP 200 and eligible selection saves `licensePlate` and `selectedCarPlate`. If no CAR matches, check the vehicle binding to the current Account. |
| **02.1 Review Payment quote** | Confirm the quote preflight below and send. Payment persists a reviewed quote. | HTTP 200 with exact plate, slot, site, interval, method, tariff version, disclosure, and future `validUntil`. The payable sample expects `isCovered=false`, `coveredAmount=0`, and a positive whole-VND `depositAmount` equal to `bookedAmount`. Examine the terms before copying `quoteId` to `acceptedQuoteId`. On an error use the troubleshooting table before resending. |
| **03.1 Accept quote and submit** | Accept the exact reviewed quote while valid. Send with the same Account and unchanged selection. The script saves body and key before the network call. The request starts durable admission. | HTTP 202 with v3 receipt and stable `requestId`. If no receipt arrives and the outcome is unknown, resend **03.1** with its saved intent and key. |
| **03.2 Replay exact submission** | Send once after **03.1** has captured `requestId`. It sends the same body and key. | HTTP 202 with the same v3 request identity. A 409 `IDEMPOTENCY_KEY_REUSED` means the original key is bound to another intent. Preserve the original attempt and inspect the saved inputs. |
| **04.1 Read Saga progress** | Send with the saved `requestId`. | HTTP 200. `CHECKING` or `EVALUATING` is interim. Positive admission returns `HOLD_CREATED` and zero admission returns `CONFIRMED` with account, vehicle, facility, and booking all `SUCCEEDED`. The script then saves `progressReservationId`. Repeat this read manually after processing if needed. For a terminal result inspect its checks and outcome code. |
| **04.2 Read durable receipt** | Send after **04.1** records the hold. | HTTP 200, v3 receipt with the same request and reservation as progress. Saves `reservationId`. For a mismatch re-read progress once and inspect both responses. |
| **04.3 Read own reservation** | Verify the accepted interval, slot and deposit. | A positive booking starts in PENDING_PAYMENT with a future H. An accepted zero deposit confirms immediately with no H and can have null order and receipt IDs. |
| **04.4 Read own Payment checkout state** | Use the same Driver session and reservation ID. | MISSING means the financial order has yet to appear. Only PAYABLE and a live Operations hold enable checkout. PAID, CLOSED and FAILED suppress checkout. A transport failure leaves the outcome unknown. |
| **05.1 Verify own Payment order association** | Send after **04.4** saves `paymentOrderId`, while the hold is active, as the same Driver Account. | HTTP 200 with PAYABLE and the original `reservationId` and `orderId`. Continue with the manual acceptance steps below. If the hold has elapsed, resolve the previous attempt before starting a new run. |

### Before sending 02.1

- Confirm `driverToken` is present in the selected environment and **00.3** succeeded for the correct Driver.
- Confirm `licensePlate` equals `selectedCarPlate` from **01.1**, and `slotId` is the current CAR slot UUID.
- Confirm `start` is in the future, `end` is later, both have a timezone, and `paymentMethod` is `PAYOS`.
- For a fresh scenario `requestId` and `submittedIntent` must be empty. If either belongs to an earlier submission, use the recovery steps below.
- Open the Postman Console while diagnosing a send. A collection guard calls `skipRequest()` before HTTP, so Postman can show **No response / 0 tests**. A real response has an HTTP status and may include a `traceId`. A transport failure can show no response with a network error. Check only the necessary status, URL, or error and redact headers, bodies, and resolved secret values before sharing. [Postman scripting](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-execution#pmexecutionskiprequest) · [Postman Console](https://learning.postman.com/docs/use/send-requests/response-data/troubleshooting-api-requests/)

## 5. Troubleshoot and recover

### Trace reviewed payOS settlement

From the repository root, replace the timestamp with the start of the test, including its UTC offset.

```powershell
docker compose --project-name smartpark-app -f compose.app.yml logs --timestamps --since '2026-11-26T15:53:00+07:00' payment-service operation-service
```

1. Find the payOS OrderCode and record the reservation, payment order and transaction IDs. Compare the merchant channel's original delivery attempts, callback destination, HTTP/TLS outcome and ingress records. A browser return and a successful legacy provider-status lookup are separate from callback delivery.
2. Look for `PayOS webhook arrived` and retain its TraceId. Follow `PayOS webhook signature verified` or `INVALID_WEBHOOK_SIGNATURE`, then `selected reviewed settlement` or `selected legacy settlement`. The incoming verifier retains empty signed fields. Checkout request signing uses its existing rules.
3. Look for `INVALID_PROVIDER_EVIDENCE`, `COLLECTION_BINDING_CONFLICT`, `PAYOS_CHECKOUT_LINK_UNAVAILABLE`, `ORDER_NOT_FOUND` or `AMOUNT_MISMATCH`. Reviewed evidence rejection logs the failed category, such as `TRANSFER_TIMESTAMP`, `SIGNED_SUCCESS_CODE`, `CURRENCY` or `CHECKOUT_LINK_MISMATCH`. The callback retains HTTP 400 for supported rejections, 503 for the pending-link retry, and 200 for accepted processing or exact replay. Application API errors use `code`, `message`, `errors` and `traceId` where applicable. The payOS acknowledgement retains its existing response shape.
4. Correlate `Reviewed PaymentSucceededV1 staged` with `Reviewed PayOS transaction committed` and its settled-order flag. Staging alone does not establish commit or publication. `PayOS webhook failed with an unconfirmed outcome` requires ledger and outbox inspection before any recovery action. Distinct additional collections have their own transaction IDs.
5. Follow `Reservation settlement event consumed`, `Reservation entitlement outcome recorded` and independently `ReservationPaymentSucceeded SignalR send attempted` / `send completed`. Match EventId across these logs. Send completion does not establish browser receipt. The browser recovers current financial state on initial load or reconnect. Keep Payment success separate from the reservation outcome.

Before sharing diagnostics, redact raw payloads, signatures, credentials, tokens, bank-account details, bank transfer references and unnecessary customer information. For the original order `314423683246459`, obtain the merchant delivery history and original signed evidence for both distinct transfers. A separately authorized recovery must preserve both transfers. A second transfer or checkout attempt is unnecessary.

The QR remains user-initiated after the Payment order becomes available. Automatic QR preparation is a possible future enhancement.

| Symptom | Where to check and safest next action |
|---|---|
| **User Docker TLS requires PFX path/password** | App runner preflight. Check the flag, file, password presence, and PFX validity in [UserService Docker TLS](#check-userservice-docker-tls). Apply a configuration correction intentionally and rerun the affected service. Repeating startup with the same invalid inputs produces the same failure. |
| **Step 2 cannot resolve demo accounts** or **Driver missing after startup** | UserService. Check each email, role, and deletion status separately. Verify the configured role bootstrap and use an existing Driver with `-DriverEmail` when available. Repair writes local identity state and should be chosen explicitly. |
| **`AUTH_FAILED` after changing `.env`** | UserService. Existing accounts can retain their original hashes. Check lockout, the actual credential, and login requirements. Try a verified credential once. Repeated login attempts can change lockout state. |
| **No response / 0 tests**, **CAR selection fails**, or **invalid interval** | Check the [quote preflight](#before-sending-021) and Console. Re-run **01.1** after selecting a returned canonical CAR plate. Correct invalid ISO input locally. HTTP 400 `BOOKING_WINDOW_INVALID` means the sent interval failed the effective policy. If a quote already exists, an input change requires another review. |
| **`OWNER_UNAVAILABLE` with Facility dependency message** | Operations made or attempted a signed Facility lookup. Check configured HTTP/TLS and the matching key pair, runtime networking, and both service logs. Operations sets a 10-second deadline. Facility may log 499 after Operations cancels. An earlier trace showed that sequence and a later successful lookup, with the initial slowness unresolved. After confirming this failure occurred at Facility before Payment, retry **02.1** once with the same selection. If it recurs, stop and inspect the downstream status, cancellation timing, and Facility schema or connectivity. [Facility client](../../../src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationFacilityClient.cs) |
| **`RESERVATION_POLICY_UNAVAILABLE`** | Operations. Check effective platform and site revisions, required values, and explicit late-arrival tolerance. The quote stops before Payment at this gate. Retry only after correcting the data through the owning policy workflow or the explicitly chosen local seeder. |
| **Payment `QUOTE_PRICING_UNAVAILABLE` or commercial `OWNER_UNAVAILABLE`** | Check Payment logs and tariff scope, effective revision dates, and revision time blocks. Missing and unusable revisions can produce the same Payment error. Operations can also return commercial `OWNER_UNAVAILABLE` for transport, service-auth, schema, or invalid-response failures. Correct the identified dependency first. [Payment issuance](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Reservations/IssueReviewedQuoteHandler.cs) |
| **`PAYMENT_QUOTE_PROOF_UNAVAILABLE`** or **`RESERVATION_ADMISSION_DISABLED`** | Operations. Verify the admission flag, reviewed settlement/Saga composition, signed Payment proof path, broker credentials, and the delivery service configuration. Restore the dependency before retrying. For an unresolved submission keep the original body and key. |
| **HTTP 401 or 403** | Verify the current session, expiry, issuer/audience/JWKS, Driver Account and role for user calls. For owner dependency calls inspect signed service trust separately. Re-signing in can change session state. A different Account cannot recover the original submission. |
| **HTTP 409** | Read the returned code. `IDEMPOTENCY_KEY_REUSED` requires the original intent under that key. `SLOT_INELIGIBLE` calls for a verified CAR slot. `QUOTE_STALE` calls for a fresh review once the earlier submission outcome is known. Inspect other booking conflicts in the receipt or progress before choosing another interval. |
| **`HOLD_CREATED`, order pending or `ORDER_NOT_READY`** | Operations and Payment. `ORDER_NOT_READY` is a collection assertion. Read **04.4** and inspect the Operations transactional outbox, the Payment order queue and its consumer. Repeat the read after processing while the hold remains active. |
| **RabbitMQ timeout / missing progress** | Check the selected vhost, owner consumers, permissions, error queues, and User/Operations outbox delivery. Inspect the first incomplete check in **04.1** and correlate service logs. A broker or container restart alone does not demonstrate delivery. Avoid resetting queues, purging messages, or resubmitting new intent to force progress. |
| **Hold expiry or `PAYMENT_HOLD_EXPIRED`** | Use **04.3** to verify `holdExpiresAt` and the current state. An expired hold cannot pass **05.1**. Resolve the old attempt, then seek a fresh quote and submit a new scenario if appropriate. Positive first-order creation is fenced at H. An accepted zero booking has no H. |

For local service logs, use the existing app project and record only the response time, trace/request ID, downstream status, and redacted error. Compare Operations' outgoing Facility or Payment timing with the owning service at the same time.

```powershell
docker compose --project-name smartpark-app --env-file .env -f compose.app.yml logs --since 10m --tail 200 operation-service facility-service payment-service user-service
```

**Recover an unknown submission.** If **03.1** was sent but did not return a captured receipt, use **03.1** again with the same Account, saved `submittedIntent`, and `reservationIdempotencyKey` even if the quote has since expired. If **03.1** already returned `requestId`, inspect **04.1** or **04.2**, or use **03.2** to verify exact replay. Do one diagnostic read at a time after background processing. A new scenario starts after the previous result is understood. Import a fresh local environment or clear the old review, acceptance, intent, key, and result fields together at that point.

### Temporary Development verification for an existing payOS checkout

The registered webhook destination for this development channel is unavailable. In PaymentService running as `Development`, the existing transaction-status action can verify transfers from the signed payOS payment-link response. Payment matches the order code and link ID, uses the VND currency tag in the provider-issued VietQR already stored with that same checkout, and checks each transfer's reference, amount and timestamp. Conflicting or missing evidence leaves the order pending. Both sources use the same reviewed settlement operation.

For an explicitly selected existing checkout with a known transfer, sign in as its owning Driver and open the original checkout or browser return page. Use **Check payment status** or **Check status again** once. This check can record the selected order's transfers. Use the existing link and the original Payment transaction ID. The Payment status response should show authoritative success after the settlement commit. A repeat should show the same success with one logical `PaymentSucceededV1`. Compare the financial SignalR update and the separate Operations entitlement outcome using the original hold deadline and reservation start. Use the Payment logs for `Active PayOS status verification requested`, evidence acceptance or rejection, replay, and commit. No additional transfer or checkout is needed.

After webhook settlement is verified end to end, remove these Development-only additions:

1. Remove `EnableReviewedVerification` and the environment argument from `GetPaymentStatusQuery` and `PaymentsController.GetTransactionStatus`. Retain owning-account authorization for reviewed status reads and the cache directive.
2. Remove the provider call and settlement dependency from `GetPaymentStatusQueryHandler.ReadReviewedStatusAsync`, and the Development snapshot selection and legacy reload from `Handle`. Remove `GetStatusSnapshotAsync` from `IPaymentTransactionRepository` and `PaymentTransactionRepository`.
3. Remove the reviewed QR argument and evidence extraction from `IPaymentGatewayPort.QueryTransactionAsync` and `PayOsAdapter.QueryTransactionAsync`. Remove `PaymentVerificationResult.ReviewedTransfers`, `PayOsCheckoutCurrency`, and the payment-link-response signing helpers in `PayOsSignatureService`. Keep the legacy status mapping and webhook signature verification.
4. Simplify `ReviewedPayOsSettlement.RecordTransfersAsync` back to its single-transfer call in `RecordAsync`. Retain `RecordTransferAsync` for the webhook, the existing outbox event, and Operations confirmation and SignalR. Retire the temporary active-verification tests and re-run the legacy status and reviewed webhook regressions.

## 6. Optional cases and verification

- **06 — Zero Amount.** Run **06.1** after an accepted zero deposit. An uncovered site can exercise this path through a genuine zero tariff. Subscription-specific coverage requires a genuine plate entitlement.
- **07.1 Reject changed intent under the same key.** Use the original positive scenario's variables. Expect 409 `IDEMPOTENCY_KEY_REUSED`.
- **07.2 Reject anonymous own-reservation read.** Use the original positive scenario's reservation ID. This request sends anonymous auth and expects 401 `UNAUTHENTICATED`. The collection guard still expects a populated `driverToken` in the environment, so retain the scenario values.
- **08.1 Read existing legacy receipt** and **08.2 Read existing legacy booking**. Sign in with the owning Driver, set the historical IDs, and verify v2 receipt and matching booking on the two reads.

**Historical pre-Checkpoint-C manual Postman result — project lead reported PASS.** All executed checks passed after the required local environment was configured. The run covered the paid scenario through **05.1**, with an active `PENDING_PAYMENT` hold and a linked Payment order. **06 — Zero Amount / Subscription Coverage** remains blocked and was excluded from the pass. Browser integration and provider settlement require separate verification.

For a reproducible run record, capture the branch and image build, fixture site and slot, policy and tariff revisions, response statuses, request/reservation/order IDs, and hold expiry. Use the **05.1** result and **04.3** state as pass evidence. The [integration report](../reports/SPARK-273-reviewed-admission-integration.md) records separate disposable-stack acceptance.

## Checkpoint C delivery and recovery

Payment receives the durable order command on `payment-reservation-order-v1`. Operations receives presentation hints on `operations-reservation-checkout-hint-v1` and genuine zero receipts on `operations-reservation-zero-receipt-v1`. Positive settlement continues through the shared `smartpark.payment.payment-succeeded.v1` exchange. Payment consumes financial obligations on `payment-reservation-disposition-v1`. Verify the receiving service has a consumer and the sending identity has access to the intended destination. Review deployment permissions with each queue owner.

A confirmed zero booking can be used before either financial ID arrives. A missing order following a positive admission means preparation is outstanding. Refresh the two authoritative reads on reconnect. Use a fresh isolated schema for C verification. The owning initializers do not upgrade populated databases.

## 7. Manual Checkpoint C acceptance

Use the four service startup commands, health probes, JWKS check, seed checks and site-policy/tariff queries in sections 1–2. Point the frontend at those same User, Operations and Payment hosts (`VITE_API_BASE_URL`, `VITE_RESERVATION_API_BASE_URL`, `VITE_PAYMENT_API_BASE_URL`). With the project's frontend dependencies already installed, run `npm --prefix FE run dev` and sign in as the same Driver. Start a fresh scenario for each booking. The browser and Postman can each inspect their own newly submitted booking. Avoid submitting the same intended visit twice.

| Scenario | Actions and expected evidence |
|---|---|
| Positive deposit | Choose an available CAR slot with a positive effective tariff and a future policy-valid interval. Run **00.2–03.1**. Inspect plate, slot, interval, amount, VND and expiry on the HTTP 200 quote before accepting. Submission returns 202. **04.1–04.3** show `HOLD_CREATED`, `PENDING_PAYMENT`, a future H and null Operations order. **04.4–05.1** return the genuine payable order and matching reservation and H. In the browser, review and accept an equivalent fresh booking. Checkout becomes available on the scoped hint or recovery read. Inspect the QR and order amount using an approved test checkout. |
| Positive settlement and return | For that booking use a supported, noncharging test settlement if available. Preserve its order ID. On return, Payment resolves it even if the Operations binding is still null. Pending confirmation may be visible until delivery. For the Postman scenario run **05.2** after successful settlement. Expect HTTP 200, `CONFIRMED`, the original order ID and one original confirmation time. In Current Booking and History check the same plate, interval, identity and outcome. Use the paired test evidence below when controlled settlement or delayed delivery is unavailable. |
| Zero deposit | Select a different available interval and a site with an effective CAR tariff whose applicable time blocks have `ratePerHourVnd = 0`, or a genuine fully covered plate where that capability exists. Verify the server's explicit booked, covered and deposit amounts and coverage flag in **02.1**. Run **03.1**, **04.1–04.3** and **06.1**. Both terminal admission and booking are `CONFIRMED`, H is null, and the slot is committed. The browser shows confirmation without checkout. A zero deposit describes what is due at admission. Departure charges use the applicable parking rules. |
| Zero receipt and history | Record `confirmedAt` and the confirmation-effect ID using the read below. On the isolated stack, compare **06.1** and History before and after normal delivery. Both financial IDs start null and later appear together. Expect the same slot, confirmation time and effect. If delivery wins the observation race, use the atomic-admission test evidence below for the null-ID window. The linked invoice is settled at zero with no provider transaction. |
| Missing order, checkout error and expiry | Read **04.4**. `MISSING` is pending preparation. A transport error permits reconnect. A failed checkout attempt can be retried under the existing Payment rules while H is live. A genuinely committed `FAILED` or `CLOSED` order suppresses checkout. On a fresh unpaid hold let H elapse. The browser stops offering payment and ordinary expiry releases the slot. The first positive creation at or after H is fenced. |
| Reconnect and account isolation | Leave the booking page offline while readiness changes, then reconnect or reopen the saved attempt. The Driver joins the scoped subscription before fetching current progress and Payment state. It should recover the same order even if it missed the hint. Sign in as another Driver. Neither owner read reveals the first Driver's order and the original progress subscription is inaccessible. |
| Late positive collection and invalidation | If controlled test delivery is available, delay settlement consumption until H has passed but the reservation start T is still future. An ordinary expired hold may reacquire its original slot under the current projection and accepted buffer. A conflicting commitment or independently permanent invalidation prevents confirmation. At or after T the recorded collection has no late entitlement. Check one reconciliation request for the real collection on denial. A requested refund is an outstanding financial obligation. |
| Reserved departure and walk-in | Through the normal operator gate UI, scan the reserved plate and initiate departure checkout for its active visit. Repeat for an ordinary walk-in visit. Inspect each departure order's `reference_type = PARKING_SESSION` and the corresponding session ID. The deposit order remains associated with the reservation. Verify existing checkout/status and gate completion using the site's supported test-payment mode. |

For read-only inspection run each query in its owning database, substituting the captured UUID for the placeholder. Use a new query after delivery. An empty result can be the expected intermediate state.

```sql
-- Operations
SELECT id, status, target_slot_id, active_claim_slot_id, claim_end_time, hold_expires_at,
       payment_order_id, collection_id, zero_receipt_id, confirmed_at,
       confirmation_effect_id, confirmation_state, confirmation_code
FROM reservations WHERE id = '<reservation-id>';

-- Payment
SELECT o.id, o.reference_type, o.status, o.final_amount, o.expires_at,
       o.is_provisioning_fence, i.id AS receipt_id, i.final_amount AS invoice_amount
FROM payment_orders o LEFT JOIN invoices i ON i.payment_order_id = o.id
WHERE o.reference_id = '<reservation-or-parking-session-id>';
SELECT id, collection_id, status, amount, reason FROM refund_requests
WHERE payment_order_id = '<payment-order-id>';
```

For a previously prepared **reserved departure**, compare the session's bound checkout order with the Payment row. Older callers could store the reservation ID as the departure order's reference. A retry uses the original idempotency key. Payment rejects a conflicting existing reference. If this occurs, give the Payment and Operations owners the reservation, session and order IDs before retrying payment. Preserve the original records for reconciliation.

When a timing window cannot be reached manually, use the recorded PostgreSQL and transport regressions. `SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation` covers return lookup before Operations receives settlement. `LatePaidClaimReacquiresOriginalSlot`, `LatePaidConflictCommitsOneDisposition` and `FirstCollectionAfterStartBindsWithoutEntitlementAndStagesOneDisposition` cover delayed decisions. `ZeroAdmissionAndFinancialLinkHaveIndependentAtomicEffects`, `ZeroDispositionUsesTheSameObligationInEitherArrivalOrder`, `CompletedZeroVisitCanLinkWithoutReopeningAnInactiveSite` and `LateZeroOrderInvoiceAndReceiptShareTheCommitAndAreIdempotent` cover the financial-linkage windows. Leave unsupported provider cases marked unexecuted.

## Checkpoint D customer acceptance

Use the existing SPARK-273 Local environment and send individual requests. Confirm scheduler and messaging readiness with the [deployment checklist](../reports/SPARK-273-deployment-checklist.md) first.

1. Complete the normal reviewed positive journey through 04.1–04.4 and 05.1. Keep the original request ID, reservation ID, order ID, slot, interval and H. Use the existing zero scenario and 06.1 on a separate booking. Its payment deadline is null.
2. On a fresh positive booking, leave the order unpaid after verifying PAYABLE. Connect the authenticated Driver to the existing SignalR hub at `/hubs/reservation-progress`, invoke `Subscribe(requestId)` and keep the subscription open. Without refreshing or issuing status queries, observe a newer `ReservationProgress` for the original reservation with `reservationState = EXPIRED`. Delivery can lag H.
3. Send 09.3 and 09.4 once. Check the Driver's reservation history and the original H. An unpaid, previously payable order reports CLOSED. Keep the original actor's evidence. In a separate environment copy, use another eligible car and a fresh quote and key for the **same slot and interval**. Complete booking to demonstrate released capacity.
4. For the missing-response case, prepare a reviewed quote while metadata is available. In an isolated acceptance environment, suspend one owner eligibility consumer while leaving Operations, RabbitMQ, Payment and the other owners available. Submit once. After D and the terminal notification, use 09.1. Restore the owner. A late reply leaves the same terminal result.
5. For the structure case, obtain a reviewed quote and prepare an authorized, controlled structure edit on that site. Submit and read 09.2. Replay the same request/key, release only the edit token you own, then use a fresh key and a still-valid reviewed quote for the next attempt. The rejected request stays terminal. Use the backend regression case if no controlled edit is available.

If no update arrives, reconnect, subscribe and read one scoped snapshot. Compare its version with the last notification. Check the registration queue, Quartz job key, outbox relay and hub connectivity before repeating a booking. An existing hold can cease protecting capacity at H before its persisted expiry notification arrives.
