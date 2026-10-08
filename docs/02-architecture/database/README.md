# Database Documentation

Store database models, schema decisions, ownership notes, migrations
documentation, and data-boundary documentation here.

The SRS baseline states that each microservice owns its own persistence boundary
and that services should not directly access another service's database.

Application code is maintained on `develop`; canonical service SQL and the
database compose are maintained on `arch/database-schema`. See
[the integration guide](../../04-testing/database-integration-guide.md) for
running code against that schema checkout, tests and the two-branch workflow.
