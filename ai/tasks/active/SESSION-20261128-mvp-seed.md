# Session review — independent MVP seed, Checkpoint A

## Session information and objective

2026-11-28, SPARK-272 feature checkout, HEAD `59742a8714f1aa3cd6b4aacc592e4f151c1b5159`. Initially clean. The user approved implementing A and offline verification. Database writes require a separately identified and authorized isolated target.

## Work completed and files

`scripts/seed-mvp-data.ps1` generates UUIDv5 identities, reads the three built-in GLB inventories, checks the local Compose context, labels, volumes, port exposure, database identity and service schemas, and orchestrates local transactions. The three service files in `scripts/database/seeds/mvp/` are `01-user.sql`, `02-facility.sql` and `03-operations.sql`. The last initializes only the site activity projection. The existing synchronization handler adjusts capacity pools already present. The fixture needs no initial pool or synchronization receipts.

## Decisions and evidence

- Two tenant Owners, ten site-scoped Operators, six Drivers, sixteen registered CARs, five sites, nine floors and child zones, 256 registered bays, and twenty marked backups. Initial ownership applications are limited to 01 and 04. **Explicit isolated development exception** permits publication rows for 02, 03 and 05 without additional applications or provision receipts. Additional-lot publication remains unsupported through normal onboarding.
- Owner setup stores the application's Pascal-case DTO. The Facility onboarding receipt holds the serialized service body with its camel-case nested lot. Review requires an existing active development Admin. The application records policy acceptance in the setup. A pre-existing applicable Owner onboarding terms revision can be linked. The seed does not bootstrap privileged users, terms, media, authentication sessions or external payments.
- The existing-vehicle list, binding resolver and SPARK-273 canonical plate check read Account, plate, vehicle type, deletion and binding state. They accept nullable media. New vehicle registration has separate upload requirements. Login honors deployed bot/MFA settings.
- Normal execution recovers UTC T from the validated first tenant and checks both service anchors. An explicit isolated reset can rebaseline timestamps in place. Complete service phases commit atomically. Missing rows inside a committed phase are ambiguous. A missing publication stops replay; a scoped reset can restore it. A time-changing reset interrupted between anchors requires the explicit target time on retry. No fixture parent deletion, truncation, auth-session invalidation or provider cleanup is implemented.
- Reset is conservative. It checks attached services, active database clients and dependent references in the local service databases. Keep host clients, workers and other writers quiescent throughout. Auth sessions, audit records or later business references can block reset. Human review is required for ambiguous dependencies. Credential mismatches and progressed operational state are preserved and reported.

## Verification and limitations

- PowerShell AST parse and offline preview passed. Independent Python GLB decoding and UUIDv5 comparison matched raw bay codes, per-floor groupings, classifications, all 256 slot IDs, the handoff bay and backup counts. Account, Operator assignment and plate uniqueness checks passed. Rounded distances from 10.775, 106.700 are 1239, 3195, 7194, 13905 and 30061 metres. Geographic membership at 5/10/20/50 km is 2/3/4/5.
- Exercised the restored BCrypt.Net-Next 4.2.0 implementation using an ephemeral password, fresh salts and cost 12. All 18 generated hashes verified. SQL and PL/pgSQL parser checks passed for the assembled service transactions. This does not execute generated dynamic SQL or establish installed-schema compatibility.
- Docker returned no running containers. Database insertion, repeat/reset, negative dependency tests, login, service reads and visual acceptance remain unexecuted. Earlier 121 tests and 32 Postman requests were not rerun. No staging or Git history changes.

## Handoff

Run `./scripts/seed-mvp-data.ps1 -Preview` for identifiers and backup selections. For authorized execution use PowerShell 7.2+, Docker Compose, the restored UserService BCrypt dependency and an active authorized development Admin. Set up the existing service schemas through the normal project workflow. Supply the isolated Compose project name and a fixture password at the prompts. Reset additionally checks Payment, Notification and IoT databases for dependencies.

In the Owner's rehearsal browser, edit the existing lot setup, choose the matching built-in type/model and retain the public profile fields. Reopen Structure and verify the model and overlays. The form can issue a profile edit; changes to publication-sensitive fields invalidate the grant. Confirm catalogue membership afterward. Recover initial sites through normal publication or use an authorized fixture reset. Additional sites need the approved fixture reset. For Operator verification, sign in as a dedicated Operator, open the assigned site's operational layout and inspect floor and slot bindings. Custom browser model state can override the built-in choice.

Next approval boundary is B. B covers tariffs, quotas, site policies, five monthly offers and three monthly entitlements. Yearly remains deferred; positive-buffer testing is conditional. C covers booking, session and financial history, walk-ins attributed by registered plate, and schedule compression where effective history requires it. D covers the Postman guide, regression and recovery evidence, and tester handoff.
