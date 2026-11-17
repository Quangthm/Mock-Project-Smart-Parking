# SPARK-340 — UAT environment repair, 16 November 2026

## Completed repairs

1. Applied the approved additive block from `scripts/database/microservices/03b-reservation-operations-service-db.sql` to the existing operations database, inside one transaction. Existing columns were retained; `operational_policy_drafts` and its index were created. There were 36 operational policy rows before and after this schema operation. No volume/database was deleted.
2. Generated a matching 2048-bit RSA pair in the ignored local `.env` for `SMARTPARK_OPERATION_FACILITY_PRIVATE_KEY_PEM` and `SMARTPARK_FACILITY_OPERATION_PUBLIC_KEY_PEM`. Set `SMARTPARK_RESERVATION_FACILITY_ENABLED=true` and `SMARTPARK_RESERVATION_FACILITY_ALLOW_HTTP=true` for the local Docker network. Existing admission and messaging configuration was already enabled and retained. Keys/tokens were not printed or written to tracked evidence.
3. Fixed a newly exposed signer lifetime bug. `ReservationFacilityClient` used an RSA object disposed after each request while IdentityModel cached its signature provider. Repeated calls could fail with ObjectDisposedException/HTTP500. Signing now supplies exported RSA parameters so the cached provider owns its key lifetime. The new test validates four consecutive signed requests, including signatures and service permission.
4. Unified Notification JWT ClockSkew to 30 seconds, matching Operation and Payment. The report's earlier claim of a five-minute Notification setting did not match source, which used one minute. Authentication regression suite passed 18/18 after alignment.
5. Fixed invalid RabbitMQ connection URIs when passwords contain URI delimiters. Compose connection-string consumers accept `SMARTPARK_RABBITMQ_PASSWORD_URI_ENCODED` with backward-compatible raw-password fallback; the local encoded value was prepared without printing it. Raw host/password consumers retain their original credentials. Operation and Facility both report valid broker URIs after recreation.
6. Docker Desktop was manually paused during verification. Resume UI automation failed; the official `docker desktop restart` restored the engine. Existing containers and volumes were preserved and restarted. All five backend services are healthy and frontend remains available at http://localhost:5173.

## Runtime proofs

| Case | Actual result |
| --- | --- |
| Owner draft read | 200 |
| Owner invalid arrival tolerance 31 | 400 POLICY_OUT_OF_BOUNDS |
| Owner valid draft | 200 |
| Owner publish | 200 |
| Effective SITE override | 200; ARRIVAL_LATE_TOLERANCE=20, inherited=false, source=SITE_OVERRIDE |
| Signed Facility facts request | 200; the actual configured RSA pair matches |
| Repeated RSA signing regression | PASS, 1 test with four signed requests |
| Notification authentication suite | PASS, 18/18 |
| Newman folders 00/01/02/03/05 | 44 requests; 71 assertions passed, 1 skipped, 0 failed |

The only skipped assertion in the scoped Newman run needs real optional SANDBOX credentials. Folder04 was explicitly excluded as BLOCKED by the commercial producer dependency below; the scoped PASS does not mean full P4 acceptance. Evidence files are `evidence/SPARK-370-environment-repair-20261116.json` and `evidence/SPARK-370-repaired-environment-Newman-20261116.json`.

The seed site's arrival tolerance was changed from inheritance to an explicit 20-minute override solely to exercise the supported Owner API. Other site values were preserved. Fixture ids for the owner site, driver vehicle and free CAR slot are in the sanitized environment-repair evidence. The original Owner configuration is retained locally in ignored `.cache/spark340-owner-before.json`.

## Remaining blocker — commercial integration implementation

After the signed Facility request succeeded, two real terms requests returned 503 OWNER_UNAVAILABLE with the specific message **Reservation commercial producer is not integrated.** This is distinct from the repaired Facility transport failure and the fixed signer500.

`InfrastructureDependencyInjection.cs:49` registers `UnavailableReservationCommercialOwner`. Its Preview, Quote, Accept, EnsureOrder, Collections and Reconcile methods deliberately fail closed. No alternative implementation is registered or present in the Operation tree. Enabling an environment flag cannot replace this implementation.

Payment has pricing preview/quote and internal order/collection/reconciliation endpoints, but Operation's separate acceptance step needs its authoritative, versioned, idempotent contract reconciled with the Payment-owned transaction protocol. A transport adapter must use these approved producer contracts; returning synthetic terms or assuming acceptance would invalidate the snapshot test.

**P4 remains open for the linked booking snapshot case.** Owner policy UAT and real SITE_OVERRIDE are now unblocked. Assign the commercial-adapter dependency to the owning integration work before expecting folder04/U19 to pass. Existing passing policy, authorization, scheduling, persistence and restart proofs remain valid.

## Retest handoff

- Use the current `.env` and current Compose; do not replace generated local key material with blank example values.
- Confirm all service health endpoints and Owner draft/publish before UAT U09–U11.
- Use the response-derived site/vehicle/slot ids in the environment-repair evidence for local fixtures.
- Run folders00/01/02/03/05 and report optional credential SKIP honestly.
- For folder04, report BLOCKED until a real IReservationCommercialOwner adapter is available. Once delivered, review terms, submit exactly those terms, obtain the linked booking and compare its persisted accepted snapshot before/after policy activation.
- Verify the backend pipeline rejects invalid min/bounds and maps storage failures to503; the environment repair does not substitute for these fault-injection cases.
- Preserve all volumes and redact tokens, keys and populated environment values from evidence.
