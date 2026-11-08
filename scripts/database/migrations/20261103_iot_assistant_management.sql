-- Apply manually to IoT DB only after review. Additive; preserve tables and key-ring on rollback.
BEGIN;
CREATE TABLE IF NOT EXISTS assistant_management_state (id integer PRIMARY KEY CHECK(id=1),state jsonb NOT NULL);
CREATE TABLE IF NOT EXISTS assistant_management_credentials (slot_id uuid NOT NULL,version uuid PRIMARY KEY,protected_value text NOT NULL);
CREATE TABLE IF NOT EXISTS assistant_management_receipts (actor uuid NOT NULL,mutation_id uuid NOT NULL,fingerprint text NOT NULL,revision bigint UNIQUE NOT NULL,action text NOT NULL,at timestamptz NOT NULL,PRIMARY KEY(actor,mutation_id));
INSERT INTO assistant_management_state(id,state) VALUES(1,'{"Revision":0,"Enabled":false,"MaintenanceMessage":"Trợ lý đang bảo trì. Vui lòng thử lại sau.","Providers":[{"Id":"gemini","Model":"gemini-3.8-flash","Enabled":false,"Priority":1},{"Id":"openai","Model":"gpt-5.4-mini-2026-03-17","Enabled":false,"Priority":2}],"Keys":[]}') ON CONFLICT(id) DO NOTHING;
COMMIT;
