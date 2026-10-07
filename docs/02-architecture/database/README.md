# Database Documentation

Store database models, schema decisions, ownership notes, migrations
documentation, and data-boundary documentation here.

The SRS baseline states that each microservice owns its own persistence boundary
and that services should not directly access another service's database.

See [schema integration](schema-integration.md) for the pinned `arch/database-schema`
baseline, per-service overlays, local startup, validation scope and hold recovery.
