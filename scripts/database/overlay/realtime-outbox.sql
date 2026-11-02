-- realtime-outbox.sql
-- Additive overlay for SPARK-244 realtime operator monitoring (G5 idempotency).
-- Tables: outbox_events (ReservationService side) + processed_events +
--         slot_sequence_watermark (ParkingService consumer side).
-- All operations are additive; no existing tables/columns are modified.
-- Unique on event_id is the idempotency anchor (Q3).
-- Apply manually or via merge checklist; NOT wired into apply-workflow-migrations.ps1 yet.

-- Outbox (ReservationService owns this table)
CREATE TABLE IF NOT EXISTS outbox_events (
    event_id        UUID PRIMARY KEY,
    aggregate_type  TEXT NOT NULL,        -- 'slot' | 'zone' | 'site'
    aggregate_id    UUID NOT NULL,
    event_type      TEXT NOT NULL,        -- 'OccupancyChanged' | 'OccupancyReleased' | 'AllocationChanged' | 'ZoneCapacityChanged'
    payload         JSONB NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL,
    published_at    TIMESTAMPTZ NULL,
    retry_count     INT NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_outbox_events_event_id
    ON outbox_events (event_id);

CREATE INDEX IF NOT EXISTS ix_outbox_events_unpublished
    ON outbox_events (occurred_at)
    WHERE published_at IS NULL;

-- Consumer-side idempotency ledger (ParkingService)
CREATE TABLE IF NOT EXISTS processed_events (
    event_id     UUID PRIMARY KEY,
    event_type   TEXT NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_processed_events_event_id
    ON processed_events (event_id);

-- Per-slot sequence watermark (ParkingService) — prevents stale/replay regression
CREATE TABLE IF NOT EXISTS slot_sequence_watermark (
    slot_id              UUID PRIMARY KEY,
    last_sequence_number BIGINT NOT NULL,
    updated_at           TIMESTAMPTZ NOT NULL
);

-- Release-once ledger (ParkingService) — session may be released at most once
CREATE TABLE IF NOT EXISTS occupancy_release_ledger (
    event_id     UUID PRIMARY KEY,
    session_id   UUID NOT NULL,
    slot_id      UUID NOT NULL,
    released_at  TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_occupancy_release_session
    ON occupancy_release_ledger (session_id);
