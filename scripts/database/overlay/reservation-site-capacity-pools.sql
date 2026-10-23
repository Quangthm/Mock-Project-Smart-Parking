-- ======================================================================================
-- OVERLAY 1/4 — Reservation capacity pools (smartpark_reservation)
-- Purpose : Recreate site_capacity_pools with the shape the ReservationService BE
--           actually uses. The original DDL (microservices/03-reservation-session-
--           service-db.sql on develop) was lost in the arch SRS split (T4 D7, gap R3);
--           the newer 05.1 variant has a different shape (missing tenant_id,
--           pending_payment_count, occupied_count, protected_count) and is unusable.
-- Target  : smartpark_reservation
-- Order   : APPLY FIRST (1st) — reservation-structure-safety.sql depends on this table.
-- Plan ref: T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md §2.2 R3, §3.3.
-- Evidence: StructureSafetyStore.cs:127, 194-199 (column usage).
-- ======================================================================================
-- SOURCE OF TRUTH: git show develop:scripts/database/microservices/
--                   03-reservation-session-service-db.sql (section 1) — reproduced
--                   below with IF NOT EXISTS for idempotent re-application.
-- ======================================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS site_capacity_pools (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- Cross-domain References (Logical IDs)
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    spatial_unit_id UUID,

    vehicle_type VARCHAR(50) NOT NULL DEFAULT 'CAR' CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE')),
    total_capacity INT NOT NULL,
    max_reservation_quota INT NOT NULL,
    current_reserved_count INT NOT NULL DEFAULT 0,
    pending_payment_count INT NOT NULL DEFAULT 0,
    occupied_count INT NOT NULL DEFAULT 0,
    protected_count INT NOT NULL DEFAULT 0,
    backup_count INT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id, tenant_id, site_id),
    CHECK (current_reserved_count >= 0 AND current_reserved_count <= max_reservation_quota),
    CHECK (max_reservation_quota <= total_capacity)
);

CREATE INDEX IF NOT EXISTS idx_site_capacity_pools_tenant_site ON site_capacity_pools(tenant_id, site_id);

COMMIT;
