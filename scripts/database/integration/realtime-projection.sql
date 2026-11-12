-- realtime-projection.sql  (facility-db / FacilityService)
-- Additive overlay for SPARK-244 realtime operator monitoring (G5 idempotency).
-- Consumer-side tables for the OccupancyProjectionWorker. The outbox table lives
-- in operations-db (see realtime-outbox.sql). All statements are additive.
-- Apply via scripts/apply-workflow-migrations.ps1 -Service Parking (also runs on an
-- already-current database, because the script is idempotent).

-- Consumer-side idempotency ledger. Unique on event_id.
CREATE TABLE IF NOT EXISTS processed_events (
    event_id     UUID PRIMARY KEY,
    event_type   TEXT NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_processed_events_event_id
    ON processed_events (event_id);

-- Per-slot sequence watermark — prevents stale/replay regression.
-- Table name matches ReservationDbContext's "slot_sequence_watermarks" mapping.
CREATE TABLE IF NOT EXISTS slot_sequence_watermarks (
    slot_id              UUID PRIMARY KEY,
    last_sequence_number BIGINT NOT NULL,
    updated_at           TIMESTAMPTZ NOT NULL
);

-- Release-once ledger — a session may be released at most once.
CREATE TABLE IF NOT EXISTS occupancy_release_ledger (
    event_id     UUID PRIMARY KEY,
    session_id   UUID NOT NULL,
    slot_id      UUID NOT NULL,
    released_at  TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_occupancy_release_session
    ON occupancy_release_ledger (session_id);
