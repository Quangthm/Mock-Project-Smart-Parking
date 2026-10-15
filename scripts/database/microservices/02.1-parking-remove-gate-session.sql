DROP INDEX IF EXISTS uq_parking_slots_gate_session;
ALTER TABLE parking_slots DROP COLUMN IF EXISTS gate_session_id;
