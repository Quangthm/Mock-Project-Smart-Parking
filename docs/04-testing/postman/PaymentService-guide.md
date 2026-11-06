# PaymentService Postman testing

Updated 2026-11-06. Use this guide with:

- [Payment collection](PaymentService.postman_collection.json)
- [Payment LOCAL environment template](PaymentService-Local.postman_environment.example.json)
- [payOS acceptance plan](../payment-integration/payos-test-plan.md) for separate, approved provider testing

## Scope and prerequisites

This procedure tests **local synthetic callbacks** against disposable development Payment data. A valid callback marks an order paid and can publish `PaymentSucceededV1`; use a development stack whose downstream consumers also use test data. Do not point the collection at production. Do not scan or pay the generated QR. Synthetic callback success does not establish actual payOS settlement, bank integration, refunds, or exactly-once downstream processing.

The merged Payment implementation uses its own `DevAuthenticationHandler` in `Development`; its `dev-driver-token`, `dev-admin-token`, and `dev-operator-token` values are development fixtures, not UserService JWTs. Callback authentication uses the **payOS HMAC checksum key**, not a JWT private key or JWKS key. The SPARK-327 environment contains different names and workflow checkpoints, so use the separate Payment environment supplied here. No UserService restart is required by this procedure.

Payment, its development database/schema, and its configured messaging dependencies must be ready. A `/health` response alone does not establish database or gateway readiness. If Payment exits during startup, resolve that first; a MassTransit license error is a startup/dependency issue and is not fixed by changing Postman or JWT keys.

## 1. Configure local Payment once

If Payment is already running with a known non-empty checksum key and simulated gateway settings, keep those settings and go to section 2.

For the Docker app stack, edit your **local, ignored root `.env`**. For this isolated synthetic test, use a `SMARTPARK_PAYOS_CLIENT_ID` starting with `mock` (for example `mock-local`), leave `SMARTPARK_PAYOS_API_KEY` empty, and set `SMARTPARK_PAYOS_CHECKSUM_KEY` to a non-empty local test value of your choice. Use the same checksum value in Postman. Keep any actual provider credentials separate from this test configuration.

The current adapter simulates link creation when its client ID starts with `mock` or required credentials are missing; this is a repository fallback, not a payOS-hosted sandbox. Signature verification still uses `PayOs:ChecksumKey`. Compose explicitly maps `SMARTPARK_PAYOS_CHECKSUM_KEY` to `PayOs__ChecksumKey`; an empty `.env` entry overrides the source `appsettings.json` default. A Postman environment never configures the backend automatically.

After changing `.env`, recreate Payment using the repository runner from the repository root:

```powershell
.\scripts\run-app-service.ps1 -Service Payment -NoBuild
```

**PAUSED_SETUP:** this is a tester action. Wait until Payment has started successfully before sending requests. Docker Desktop Restart reuses the container's existing environment and does not apply changed `.env` values. For Visual Studio/direct `dotnet run`, configure the equivalent `PayOs:ClientId`, `PayOs:ApiKey` and `PayOs:ChecksumKey` in Payment's local configuration and restart that process; the current setup/host scripts do not map these PayOS entries. No configuration or container changes are made by the collection.

## 2. Import and configure Postman

1. Save any local values you need privately. Import the updated `PaymentService.postman_collection.json`. Replace/update the existing collection if offered, or clearly rename the old copy and use the newly imported copy. Existing imported copies do not update when Git files change.
2. Import `PaymentService-Local.postman_environment.example.json` and select **SmartParking Payment LOCAL - configure checksum key** in the environment selector.
3. Open that environment and configure the following values. Keep real credentials local/unshared; do not export populated environments into Git.

| Variable | Local setup |
|---|---|
| `baseUrl` | `http://localhost:5065`, or your actual Payment host port. No trailing slash. |
| `payosChecksumKey` | Exact non-empty checksum key used by the running Payment process. Initially blank on purpose. |
| `accessToken` | Supplied `dev-driver-token` for the current Development handler. |
| `adminToken` / `operatorToken` | Supplied development role markers. |
| `internalServiceKey` | Supplied local fixture marker for folder 02; this is not a production S2S credential. |
| `tenantId` / `siteId` | Supplied test fixture IDs, or IDs agreed for your disposable test data. Folder 01/02 pricing scenarios also require their documented tariff fixtures. |

4. Save your edits. Do not add `onlineOrderId`, `onlineOrderCode`, `onlineAmount`, `payosOrderCode`, `transactionId`, `paymentWebhookBody` or `paymentWebhookReplay` to the environment or iteration data. The collection chains those generated values; duplicate names can shadow them. Do not run concurrent iterations against the same collection state.
5. Check **3.10 → Body → raw → JSON**: its entire text must be `{{paymentWebhookBody}}`, without quotes. 3.11 uses the same template. Both requests have **No Auth** because the signature is in the JSON payload.

### If Postman moves the checksum key to Vault

That is secret storage, not evidence that the backend key changed. The signing script supports an environment value of `{{vault:your-secret-name}}` and resolves it with `await pm.vault.get(...)`. In Desktop, enable scripts to access Vault and grant this collection access when prompted. Keep the reference in the environment; do not replace it with the reference's text as a signing key. Missing/unresolved keys stop the request before sending.

For CLI runs, Desktop's local Vault is not available. Use a private local environment file with a literal test checksum value, outside the checkout, and pass it with `-e`. Never pass real secrets as command arguments or attach a populated environment/console dump to a report. `type: secret` masks the UI; it does not encrypt an exported JSON file.

## 3. Focused regression run for 3.10 and 3.11

1. Send **0.1 Health Check** and expect HTTP 200 with `ok`.
2. Open the collection's **Run** view, select the Payment LOCAL environment, **one iteration**, and no iteration data file.
3. Select only **3.1, 3.4, 3.10, 3.11, 3.12**, in that order. Leave other requests unchecked for this focused run. Do not run 3.9 or 3.13 first: reconciliation/demo-pay can settle the order before the callback, weakening the callback test.
4. Run. 3.1 creates a fresh order and clears previous callback state; 3.4 obtains checkout state. Stop and investigate if either fails rather than proceeding with old IDs.
5. Expect these results:

| Request | Expected evidence |
|---|---|
| 3.1 | 200; new order in `PENDING`; saves the order code and amount. |
| 3.4 | 200; checkout in `PENDING`; saves the matching checkout order code. |
| 3.10 | 200 and `success: true`; signed callback accepted. |
| 3.11 | 200 and `success: true`; same accepted payload, signature and bank reference replayed exactly. |
| 3.12 | 400, `success: false`, `errorCode: INVALID_WEBHOOK_SIGNATURE`. This is an expected rejection and a passing test. |

The focused run currently has **11 assertions**. A request skipped due to missing configuration is not a pass. The HTTP-level replay check alone cannot prove there was only one database/outbox event; use the backend handler/integration tests and downstream evidence for that claim.

You may instead use **Send** sequentially in the same order. 3.10 saves its accepted callback in the collection variable `paymentWebhookReplay` so that a later Send of 3.11 can replay it even after run-local variables disappear. This cache contains test transaction metadata and a signature, never the checksum key. Do not export populated collection state for sharing; use the clean repository file. Run 3.1 to start a new scenario. Do not rerun 3.10 between the original callback and the duplicate being tested.

For the broader suite, run folders 00–03 in order only after confirming their fixture requirements. Folder 01 contains fixed pricing expectations; folder 02 creates invoice/cash-payment test records. A pass of the focused webhook run is not a pass of all 40 requests. Record those results separately.

## 4. Troubleshooting

| Symptom | Check / resolution |
|---|---|
| `INVALID_WEBHOOK_SIGNATURE` at 3.10 | Confirm updated Body template, selected Payment environment, resolved Vault access if used, and exact checksum match with the running process. Recreate the container after `.env` edits. Do not change JWT keys. |
| Script says run 3.1 and 3.4 | Complete both for the same fresh order. No fixed order-code or amount fallback is used. |
| Script says run 3.10 before replay | 3.11 needs an accepted 3.10 response. Reimporting the collection or running 3.1 clears old replay state. |
| `ORDER_NOT_FOUND` / `AMOUNT_MISMATCH` | Check target environment and order chain; start with a fresh 3.1. These occur after signature verification. |
| 401/403 at checkout | Verify Development runtime and the supplied development authentication settings; this procedure does not certify production authentication. |
| Health cannot connect | Fix Payment startup/port first. The collection cannot repair container startup. |
| Vault permission or missing key error | Grant Desktop script access or use a local, unshared literal test value. Never sign the literal `{{vault:...}}` text. |

Share only request number, HTTP status, `errorCode`, failed assertion text and redacted run counts. Keep checksum keys, Authorization headers, populated environment exports and full payload logs private.

## 5. Root cause and prevention

The original 3.10/3.11 assigned `pm.request.body.raw`, while the saved body retained a literal `to-be-replaced-by-prerequest-script` signature. Postman's [request API documentation](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-request) documents the request body as immutable. The fix serializes the complete signed payload once into `pm.variables`, and the saved body references it through interpolation. This avoids discrepancies between the data being signed and the data being transmitted. It does not depend on an unverified explanation of Electron process internals.

Two additional defects were found: the old checksum lookup read collection variables only, ignoring a configured environment; and each request generated a new `reference`, so 3.11 was not an exact duplicate event. The fix reads the checksum using scope-aware lookup (including explicit Desktop Vault resolution), rejects missing setup rather than signing with an empty key, and reuses the exact accepted payload for replay. Generated order state stays in its deliberate collection scope; the new environment contains configuration only.

For future signed requests: use a saved body template, sign the same serialized data you send, test environment precedence, fail before sending unresolved secrets, preserve event identity for duplicate tests, and verify the on-wire request with a disposable receiver. Do not infer transport correctness just from logging an in-memory `pm.request` object.

## 6. Repeatable local verification and evidence

These commands use synthetic values and do not start Payment or contact payOS:

```powershell
node --test tests/postman/payment-webhook.test.cjs
node tests/postman/payment-webhook-runner.cjs
# If Postman is not on PATH:
node tests/postman/payment-webhook-runner.cjs "$env:LOCALAPPDATA/Microsoft/WindowsApps/postman.exe"
```

The first command tests signing, scope precedence, secret resolution, invalid setup and replay behavior. The second runs the actual Postman CLI against a temporary loopback receiver; it verifies signatures independently, compares replay bytes, checks the invalid-signature control, disables cloud reporting and removes its temporary fixture files. Neither proves live Payment acceptance or real provider behavior.

Verification on 2026-11-06: **8/8 Node regression checks passed**; the actual Postman CLI receiver run completed **3 requests and 7/7 assertions**, exit code 0, with independent HMAC and byte-identical replay checks passing. The first restricted execution could not connect to loopback (`EACCES`); rerunning with local network access passed. Desktop Vault resolution was tested with a script fixture; access to a tester's actual Vault remains unverified. No live Payment callback, real payment, container restart or backend change was performed.

For live evidence record: date/tester, commit, runtime mode (Docker/host, simulated gateway), selected environment name, request selection, assertion totals and failures, and redacted status/error codes. Mark the live retest **NOT RUN / awaiting tester** until performed. Keep broader provider acceptance governed by the separate payOS acceptance plan.
