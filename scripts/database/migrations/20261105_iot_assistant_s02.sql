-- REVIEW ONLY. Do not apply automatically or from app startup.
-- IoT owner DB, after 20261103_iot_assistant_management.sql. Pause Assistant writers during rollout.
-- Backup state + credentials + DataProtection key-ring together before manual deployment.
-- Keeps key IDs, encrypted credentials/versions, toggle, priorities and revoked timestamps intact.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
LOCK TABLE assistant_management_state IN EXCLUSIVE MODE;
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM assistant_management_state s,
      LATERAL jsonb_array_elements(s.state->'Keys') k
      WHERE NOT EXISTS (SELECT 1 FROM jsonb_array_elements(s.state->'Providers') p WHERE p->>'Id'=k->>'Provider'))
  THEN RAISE EXCEPTION 'Assistant configuration has an unknown provider; abort migration'; END IF;
END $$;
UPDATE assistant_management_state s SET state = jsonb_set(s.state, '{Keys}',
  COALESCE((SELECT jsonb_agg(CASE WHEN k->>'Model' IS NOT NULL THEN k ELSE
    k || jsonb_build_object('Model',(SELECT p->>'Model' FROM jsonb_array_elements(s.state->'Providers') p WHERE p->>'Id'=k->>'Provider')) END ORDER BY ord)
    FROM jsonb_array_elements(s.state->'Keys') WITH ORDINALITY AS keys(k,ord)), '[]'::jsonb));
CREATE TABLE IF NOT EXISTS assistant_connection_tests (
  actor uuid NOT NULL, request_id uuid NOT NULL, configuration_id uuid NOT NULL,
  revision bigint NOT NULL, model text NOT NULL, admitted_at timestamptz NOT NULL,
  result jsonb NULL, PRIMARY KEY(actor,request_id)
);
CREATE INDEX IF NOT EXISTS ix_assistant_connection_tests_rate ON assistant_connection_tests(actor,configuration_id,admitted_at);
CREATE INDEX IF NOT EXISTS ix_assistant_connection_tests_expiry ON assistant_connection_tests(admitted_at);
COMMIT;
-- Runtime cleans receipts older than 1h on admission. Optional owner maintenance may do the same.
-- Runtime role requires SELECT/INSERT/UPDATE/DELETE on this table; no cross-schema grants.
-- Rollback: retain new table/model fields and key-ring. Old binary ignores Model and would route
-- using provider.Model: do NOT downgrade after per-key model edits without a reviewed conversion.
-- No DROP/credential rewrite/reset is supplied. This migration has NOT been applied by S02.
