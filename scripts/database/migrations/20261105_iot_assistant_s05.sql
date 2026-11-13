-- REVIEW ONLY. Do not run automatically. Backup/rollback: docs/03-design/ai/conversation-and-recovery.md.
-- Run in the IoT database/schema used by the existing management datasource.
BEGIN;
SET LOCAL lock_timeout = '5s';
CREATE TABLE IF NOT EXISTS assistant_conversations (
 id uuid PRIMARY KEY, user_id uuid NOT NULL, session_token varchar(255), title varchar(255) NOT NULL,
 context_scope jsonb, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, deleted_at timestamptz
);
CREATE TABLE IF NOT EXISTS assistant_messages (
 id uuid PRIMARY KEY, conversation_id uuid NOT NULL REFERENCES assistant_conversations(id) ON DELETE CASCADE,
 sender_role varchar(50) NOT NULL, content text NOT NULL, tool_call_id varchar(100), tokens_count integer,
 created_at timestamptz NOT NULL
);
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS owner_account_id uuid;
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS client_conversation_id uuid;
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS locale varchar(2);
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS revision bigint NOT NULL DEFAULT 0;
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS last_message_at timestamptz;
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS expires_at timestamptz;
ALTER TABLE assistant_conversations ADD COLUMN IF NOT EXISTS retention_hold boolean NOT NULL DEFAULT false;
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM assistant_conversations WHERE owner_account_id IS NULL OR client_conversation_id IS NULL OR locale IS NULL OR expires_at IS NULL OR session_token IS NOT NULL)
 THEN RAISE EXCEPTION 'Legacy Assistant data requires reviewed Account mapping and credential removal before S05; no UserId inference'; END IF;
END $$;
ALTER TABLE assistant_conversations ALTER COLUMN owner_account_id SET NOT NULL;
ALTER TABLE assistant_conversations ALTER COLUMN client_conversation_id SET NOT NULL;
ALTER TABLE assistant_conversations ALTER COLUMN locale SET NOT NULL;
ALTER TABLE assistant_conversations ALTER COLUMN expires_at SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_assistant_conversation_client ON assistant_conversations(owner_account_id,client_conversation_id);
CREATE INDEX IF NOT EXISTS ix_assistant_conversation_activity ON assistant_conversations(owner_account_id,(COALESCE(last_message_at,created_at)) DESC,id DESC);
CREATE INDEX IF NOT EXISTS ix_assistant_conversation_expiry ON assistant_conversations(expires_at) WHERE NOT retention_hold;
ALTER TABLE assistant_messages ADD COLUMN IF NOT EXISTS sequence bigint;
ALTER TABLE assistant_messages ADD COLUMN IF NOT EXISTS client_message_id uuid;
ALTER TABLE assistant_messages ADD COLUMN IF NOT EXISTS response jsonb;
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM assistant_messages WHERE sequence IS NULL OR client_message_id IS NULL OR response IS NULL)
 THEN RAISE EXCEPTION 'Legacy Assistant messages require reviewed conversion before S05'; END IF;
END $$;
ALTER TABLE assistant_messages ALTER COLUMN sequence SET NOT NULL;
ALTER TABLE assistant_messages ALTER COLUMN client_message_id SET NOT NULL;
ALTER TABLE assistant_messages ALTER COLUMN response SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_assistant_message_sequence ON assistant_messages(conversation_id,sequence);
CREATE TABLE IF NOT EXISTS assistant_requests (
 id uuid PRIMARY KEY, conversation_id uuid NOT NULL REFERENCES assistant_conversations(id) ON DELETE CASCADE,
 owner_account_id uuid NOT NULL, client_message_id uuid NOT NULL, fingerprint varchar(64) NOT NULL,
 state varchar(16) NOT NULL CHECK(state IN ('running','completed','failed','cancelled')),
 deadline timestamptz NOT NULL, created_at timestamptz NOT NULL, finished_at timestamptz,
 user_message_id uuid NOT NULL REFERENCES assistant_messages(id) ON DELETE CASCADE,
 answer jsonb, error_code varchar(64), snapshot jsonb NOT NULL,
 UNIQUE(owner_account_id,conversation_id,client_message_id), UNIQUE(conversation_id,client_message_id),
 CHECK ((state='running' AND finished_at IS NULL AND answer IS NULL AND error_code IS NULL)
 OR (state='completed' AND finished_at IS NOT NULL AND answer IS NOT NULL AND error_code IS NULL)
 OR (state='failed' AND finished_at IS NOT NULL AND answer IS NULL AND error_code IS NOT NULL)
 OR (state='cancelled' AND finished_at IS NOT NULL AND answer IS NULL AND error_code IS NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_assistant_request_running ON assistant_requests(conversation_id) WHERE state='running';
CREATE INDEX IF NOT EXISTS ix_assistant_request_deadline ON assistant_requests(deadline) WHERE state='running';
COMMIT;
