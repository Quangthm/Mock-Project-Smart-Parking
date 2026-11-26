# Historical Sprint 2 seed examples

These files are preserved from the database branch for reference. Do not apply
`sprint2-seed.sql` or `sprint2-edge-cases.sql` to the integrated application databases.
They contain a combined-service data set, placeholder password hashes, incomplete
account-role/site mappings and a capacity insert that does not match the required
columns. The manifest describes those examples, not accounts bootstrapped by the APIs.

The runnable integration initializes each service with its own SQL baseline and
overlay through root `compose.schema-integration.yml`. Bootstrap a development
Admin through `SMARTPARK_ADMIN_PASSWORD`, then create Owners, sites, slots and
Operators through the APIs — or, for a ready-to-test demo dataset, run
`scripts/seed-demo-site.ps1` after UserService has seeded the owner/operator
accounts. The demo seed upserts fixed demo identity/membership/grant and Driver vehicle records in User, `DEMO-HQ` structure in Facility (48 CAR slots), and demo capacity in Operations. Each service has its own transaction; reruns may update demo records. Use isolated demo databases. It does not create reservations, payment data or finalized onboarding evidence. Positive discovery requires a normally provisioned and approved site followed by Owner publication; the seed alone is not that approval.

See [schema integration](../../../docs/02-architecture/database/schema-integration.md)
for startup, sample credentials policy, validation and recovery.

## Driver reservation history at DEMO-HQ

Run `.\scripts\seed-demo-site.ps1` from the repository root. The script reuses the
existing demo Owner, Operator, lot, and slots. It looks up the active Driver
Account for `driver@gmail.com`. Pass `-DriverEmail '<existing-demo-driver-email>'`
to use a different existing demo Driver. If the default Driver is missing,
run `.\scripts\repair-demo-identity.ps1 -Apply` and rerun the seed.

The history step inserts four bookings using fixed historical dates and IDs
derived from the Driver Account. Replaying the seed preserves existing rows.
Filter responses by the seeded plates to distinguish these fixtures from other
bookings in the account.

| Plate | Scenario |
|---|---|
| `99A-28601` | Completed booking with a deposit, synthetic order ID, released allocation at a different slot than requested, and a linked closed visit |
| `99A-28602` | Completed zero-price booking with a released allocation and closed visit but no order |
| `99A-28603` | Expired booking with a requested slot and no allocation or visit |
| `99A-28604` | No-show with a synthetic order ID and no allocation or visit |

Use the demo Driver's token with `GET /api/me/reservations?pageSize=1` to
exercise pagination across the two completed bookings' identical creation
times. Use any returned ID with `GET /api/me/reservations/{id}` to check detail.
The fixtures use terminal states and write only to Operations for this history
step. The synthetic order IDs are for display testing. No Payment records,
payment events, or live capacity claims are created.

## CAR tariff for an existing local site

From the repository root, run:

```powershell
.\scripts\seed-site-car-tariff.ps1 -SiteId '8fe65b2e-386e-4bee-b7a4-000527af7d18'
```

This resolves the active site's actual tenant in FacilityDb and its existing active
Owner in UserDb, then inserts only a missing CAR tariff and its initial policy
revision into PaymentDb. It does not create or reassign an Owner/site. The tariff
copies the existing baseline demo CAR tariff (`c0000000-0000-4000-8000-000000000001`):
the unmodified baseline has 20,000 VND/hour by day, 30,000 VND/hour at night and
15 minutes of free grace. Existing active tariffs are preserved on repeated runs.
The schema containers must already be running. Missing site/Owner/template data
stops the script; do not rerun destructive database baselines on an existing DB.

## Reservation policy for an existing local site

```powershell
.\scripts\seed-site-reservation-policy.ps1 -SiteId '8fe65b2e-386e-4bee-b7a4-000527af7d18' -ArrivalLateToleranceEnabled $false
```

Choose `$true` or `$false` explicitly for the site's late-arrival tolerance. This
resolves the tenant from the existing active Facility site, loads the approved
`03b-reservation-policy-defaults.sql` only if no platform policy is effective,
and adds a site revision inheriting all platform numerical values. It does not
modify an effective platform policy. For a legacy site policy, inspect it first;
`-ReplaceExisting` explicitly closes its validity interval and creates an inherited
revision, preserving historical rows and accepted booking snapshots. Replaying
the same inherited choice does not create another revision. Future site policies
are never overwritten. Apply only to local demo data, not as a production policy migration.
