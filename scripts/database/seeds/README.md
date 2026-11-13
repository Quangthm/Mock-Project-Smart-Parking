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
