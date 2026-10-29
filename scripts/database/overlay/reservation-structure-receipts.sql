-- ======================================================================================
-- OVERLAY 3/4 — Reservation structure-release receipts (smartpark_reservation)
-- Purpose : structure_release_receipts — idempotency receipt for the structure-edit
--           commit flow. Missing from the post-split base schema (plan gap R2).
-- Target  : smartpark_reservation
-- Order   : APPLY THIRD (3rd) — after reservation-structure-safety.sql (same flow the
--           BE uses: holds/guards, then receipts), before reservation-confirmation-time.sql.
-- Plan ref: T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md §2.2 R2, §3.3.
-- Evidence: StructureSafetyStore.cs:92 (token lookup before taking a hold), :156, :214.
-- ======================================================================================
-- Reproduced from scripts/database/05.12-Reservation-Structure-Receipts.sql.
-- OperationService only; apply after the structure safety overlay.
-- ======================================================================================
BEGIN;
CREATE TABLE IF NOT EXISTS structure_release_receipts (
    token UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    outcome JSONB NOT NULL,
    completed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
COMMIT;
