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
accounts. The demo seed is idempotent and writes only `DEMO-HQ` demo data.

See [schema integration](../../../docs/02-architecture/database/schema-integration.md)
for startup, sample credentials policy, validation and recovery.

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
