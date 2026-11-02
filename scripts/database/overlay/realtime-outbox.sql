-- realtime-outbox.sql  (operations-db / OperationService)
-- Additive overlay for SPARK-244 realtime operator monitoring.
-- Column set mirrors SmartParking.OperationService.Domain.Entities.OutboxEventRow.
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
    published_at    TIMESTAMPTZ NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_outbox_events_event_id
    ON outbox_events (event_id);

CREATE INDEX IF NOT EXISTS ix_outbox_events_unpublished
    ON outbox_events (occurred_at)
    WHERE published_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_outbox_events_site_sequence
    ON outbox_events (site_id, sequence_number);
