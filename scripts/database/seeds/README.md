# Historical Sprint 2 seed examples

These files are preserved from the database branch for reference. Do not apply
`sprint2-seed.sql` or `sprint2-edge-cases.sql` to the integrated application databases.
They contain a combined-service data set, placeholder password hashes, incomplete
account-role/site mappings and a capacity insert that does not match the required
columns. The manifest describes those examples, not accounts bootstrapped by the APIs.

The runnable integration initializes each service with its own SQL baseline and
overlay through root `compose.schema-integration.yml`. Bootstrap a development
Admin through `SMARTPARK_ADMIN_PASSWORD`, then create Owners, sites, slots and
Operators through the APIs so lifecycle and ownership records are consistent.

See [schema integration](../../../docs/02-architecture/database/schema-integration.md)
for startup, sample credentials policy, validation and recovery.
