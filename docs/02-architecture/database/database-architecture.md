# SmartPark Database Architecture

## 1. Overview
SmartPark uses a **Database-per-Service** microservices architecture. There is no single monolithic database. Each microservice completely owns its persistence layer, enforcing strict service boundaries. Cross-service data references are handled logically via UUIDs rather than physical PostgreSQL foreign keys.

The architecture consists of 6 distinct databases:
1. **User Service DB** (`01-user-service-db.sql`): Manages authentication, identity, contextual accounts, roles, and registered vehicles.
2. **Parking Service DB** (`02-parking-service-db.sql`): Manages the physical parking infrastructure, spatial units, capacity, hardware mapping, and pricing rules.
3. **Reservation & Session Service DB** (`03-reservation-session-service-db.sql`): The transactional core managing dynamic capacity allocations, reservations, and active parking sessions.
4. **Payment Service DB** (`04-payment-service-db.sql`): Handles payment orders, transactions, billing invoices, and refund lifecycles.
5. **Notification Service DB** (`05-notification-service-db.sql`): Manages notification templates and delivery logs (Email/SMS/Push).
6. **IoT Service DB** (`06-iot-service-db.sql`): Stores high-volume telemetry, LPR events, and device statuses.

## 2. Multi-Tenancy & Security Boundary (RLS)
The database is designed with **Row-Level Security (RLS)** in mind. 
- `tenant_id` is intentionally denormalized across almost all business tables.
- **Composite Foreign Keys** are used extensively (e.g., `FOREIGN KEY (parent_id, tenant_id, site_id)`) to guarantee that child records cannot cross tenant or site boundaries.
- This creates an impenetrable structural hierarchy where a parking slot, a transaction, or an IoT event is securely locked to its specific tenant context.

## 3. Spatial Hierarchy
The Parking Service models facilities using a flexible hierarchy: `parking_sites -> spatial_units -> parking_slots`.
- Avoids rigid `building -> floor -> zone` hardcoding.
- Uses a **Materialized Path** (`path TEXT`) to optimize hierarchical subtree queries without requiring complex recursive CTEs.
- Enforces strict site containment using `UNIQUE(id, tenant_id, site_id)`.

## 4. Hard Invariants & Enterprise Concurrency
To ensure data integrity under high concurrent loads, the databases enforce "hard invariants" natively via SQL, minimizing reliance solely on application-level locks:
- **Anti-Double Booking:** Uses PostgreSQL `EXCLUDE USING gist` with `TSTZRANGE` to mathematically guarantee that no two overlapping reservations can claim the same specific parking slot.
- **Capacity Concurrency:** Uses `CHECK (current_reserved_count <= max_reservation_quota)` to prevent zone-level overbooking.
- **Idempotency:** Payment webhooks and transactions enforce uniqueness (e.g., `UNIQUE(provider, provider_event_id)` and `idempotency_key`) to prevent duplicate billing on network retries.
- **Financial Correctness:** Strict `CHECK (amount >= 0)` and formula constraints ensure money values cannot be corrupted.
- **State Validation:** Status and type columns are guarded by inline `CHECK (status IN (...))` constraints to prevent invalid state transitions from application bugs.

## 5. Decoupled Lifecycles
The database cleanly separates distinct dimensions of state:
- **Physical vs Logical State:** A slot has a `physical_state` (Occupied/Available) driven by IoT, and a separate `reservation_state` (Reserved/Protected) driven by business logic.
- **Auditability:** Tables maintain `created_at` and `updated_at` (to be managed by application lifecycle hooks), with Soft Delete (`deleted_at`) applied to sensitive identities while retaining historical integrity.
