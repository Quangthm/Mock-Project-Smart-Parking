-- realtime-outbox.sql  (operations-db / OperationService)
-- Additive overlay for SPARK-244 realtime operator monitoring.
-- Column set mirrors SmartParking.OperationService.Persistence.Entities.OutboxEventRow.
-- Unique on event_id is the idempotency anchor (Q3).
-- The consumer-side tables live in facility-db (see realtime-projection.sql).
-- Apply via scripts/apply-workflow-migrations.ps1 -Service Operation (also runs on an
-- already-current database, because the script is idempotent).

CREATE TABLE IF NOT EXISTS outbox_events (
    event_id        UUID PRIMARY KEY,
    event_type      TEXT NOT NULL,        -- 'SlotStateChanged' | 'OccupancyReleased' | 'OccupancyChanged' | 'ZoneCapacityChanged'
    occurred_at     TIMESTAMPTZ NOT NULL,
    sequence_number BIGINT NOT NULL,
    site_id         UUID NOT NULL,
    payload         JSONB NOT NULL,
    published_at    TIMESTAMPTZ NULL,
    locked_until    TIMESTAMPTZ NULL
);

-- Lease column for the claim/lease outbox worker (AUD-SAGA-02). Additive so this
-- script stays idempotent for databases created before the lease was introduced.
ALTER TABLE outbox_events
    ADD COLUMN IF NOT EXISTS locked_until TIMESTAMPTZ NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_outbox_events_event_id
    ON outbox_events (event_id);

CREATE INDEX IF NOT EXISTS ix_outbox_events_unpublished
    ON outbox_events (occurred_at)
    WHERE published_at IS NULL;

-- Supports the claim query's ORDER BY occurred_at, sequence_number over pending rows.
CREATE INDEX IF NOT EXISTS ix_outbox_events_claimable
    ON outbox_events (occurred_at, sequence_number)
    WHERE published_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_outbox_events_site_sequence
    ON outbox_events (site_id, sequence_number);

-- Per-site outbox sequence allocator (AUD-SAGA-01).
-- Row-level UPSERT makes sequence allocation atomic; no MAX()+1 read-modify-write race.
CREATE TABLE IF NOT EXISTS site_event_sequences (
    site_id UUID PRIMARY KEY,
    last_sequence BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
