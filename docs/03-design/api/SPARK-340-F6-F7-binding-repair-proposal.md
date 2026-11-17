# SPARK-340 — F6/F7 concrete repair proposal

Status: APPROVED by user on17November2026: "Duyệt bảng riêng và công cụ offline". This authorizes the additive Schema Freeze exception and implementation/testing on temporary databases. It does not authorize applying DDL/import to the running user database. F8 validation proceeds independently.

## Proposed implementation

Payment will own a dedicated `checkout_signing_bindings` table instead of writing checkout credentials into pricing history. Each immutable row binds one numeric provider order code to its existing Payment order and a DataProtection-protected payload containing the exact mode, configuration version and checksum key. Payment DataProtection application/purpose remain compatible with already-issued bindings.

Proposed columns: `order_code bigint PRIMARY KEY`, `payment_order_id uuid NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT`, `protected_binding text NOT NULL`, `created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP`, `source varchar(24) NOT NULL CHECK (source IN ('CHECKOUT','LEGACY_IMPORT','LEGACY_POLICY'))`, `evidence_reference varchar(256) NULL`. Source/evidence are operational provenance; no plaintext credential is stored. Validate that numeric order code belongs to the referenced order before insert. Conflicting payloads never overwrite the first binding.

The repository writes only to the dedicated table. It can recover already-encrypted bindings from the exact existing reserved policy namespace and migrate them idempotently after decrypt/identity verification. Pricing history is never treated as an arbitrary key source and no key is inferred from an order timestamp. No automatic deletion/pruning is introduced. Dedicated binding rows remain retained with financial history until an approved archival/reconciliation policy exists; RESTRICT prevents silent deletion of the parent order while its binding exists.

For F6, add an offline operator repair/import tool with dry-run by default and an explicit apply switch. Its protected local input supplies the exact existing order code, provider link identity, issuing mode/version, checksum key and audit/evidence reference obtained from trusted owner records. Validate the existing PAYOS transaction/provider link before accepting the mapping. Input lives outside Git and values are never logged or returned. This is not a new Admin HTTP payment privilege.

Import is immutable, idempotent and read-back verified. Missing evidence, unknown order/provider link, conflict or storage failure rejects the mapping. Unknown historical keys remain a manual provider reconciliation case; the tool never guesses or accepts unsigned callbacks. Tests will cover a checkout issued before binding deployment, trusted repair, successful signature verification after restart, invalid/tampered mapping, conflict and zero outbound on persistence failure.

## Schema and deployment boundary

This proposal requires an additive exception to Schema Freeze. After approval, prepare reviewable Payment-owned DDL and mapping, plus tests on generated temporary PostgreSQL databases. Do not automatically apply DDL or import to the user's running database. Deploy order is reviewed DDL → preserve key volume → migrate existing exact encrypted bindings / import trusted legacy mappings → verify callbacks → publish new service binary. A dry-run inventory must identify unresolved pre-feature checkouts before a production rollout.

Alternative: retain Schema Freeze. F8 can ship, while F6/F7 stay OPEN and legacy checkout handling remains a release gate. Reusing unrelated pricing fields or a process-local/file cache is not proposed as a workaround.

Affected code: Payment Application binding/repository ports and import use case, Infrastructure binding encryption, Persistence repository/mapping, a small offline .NET operator tool, Payment-owned DDL and tests. No changes to SPARK-273 orchestration, no new dependencies, no real provider call or new end-user permission.

The shared coding skill's Architecture/direction approval gate is satisfied by the explicit approval above for this concrete storage/security design. Production/shared-database rollout remains a separate operator action requiring the reviewed DDL and trusted legacy evidence.
