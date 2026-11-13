-- Apply after S05. Back up IoT data and stop old Assistant instances before upgrading an existing DB.
BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL lock_timeout = '5s';
LOCK TABLE assistant_conversations IN ACCESS EXCLUSIVE MODE;

DO $migration$
BEGIN
    IF to_regclass('public.ux_assistant_conversation_current') IS NOT NULL THEN
        RETURN;
    END IF;

    ALTER TABLE assistant_conversations ADD COLUMN chat_role varchar(8)
        CHECK (chat_role IN ('driver','admin','owner'));
    ALTER TABLE assistant_conversations ALTER COLUMN expires_at DROP NOT NULL;

    -- S05 permitted only Driver. Continue the most recently used eligible conversation.
    -- Other legacy rows keep their existing expiry/holds and are excluded from the current chat.
    WITH latest AS (
        SELECT DISTINCT ON (owner_account_id) id
        FROM assistant_conversations
        WHERE deleted_at IS NULL AND expires_at > clock_timestamp()
        ORDER BY owner_account_id, COALESCE(last_message_at,created_at) DESC, id DESC
    )
    UPDATE assistant_conversations AS chat
    SET chat_role = 'driver', expires_at = NULL
    FROM latest WHERE chat.id = latest.id;

    DROP INDEX ux_assistant_conversation_client;
    CREATE UNIQUE INDEX ux_assistant_conversation_client
        ON assistant_conversations(owner_account_id,chat_role,client_conversation_id);
    CREATE UNIQUE INDEX ux_assistant_conversation_current
        ON assistant_conversations(owner_account_id,chat_role)
        WHERE chat_role IS NOT NULL AND deleted_at IS NULL;
END
$migration$;
COMMIT;
