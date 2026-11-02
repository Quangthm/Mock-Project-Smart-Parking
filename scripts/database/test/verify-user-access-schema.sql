-- Read-only prerequisite verification for the current User & Access runtime.
-- Run against the explicitly selected User database, never a default connection:
-- psql --dbname=<reviewed-connection-or-service> -v ON_ERROR_STOP=1 -f scripts/database/verify-user-access-schema.sql
-- This script creates, changes and deletes no schema or application data.
-- Fresh install: consolidated 01-user-access-service-db.sql, then this check.
-- Historical 13/14 upgrades are separate reviewed operations on existing databases.
BEGIN READ ONLY;
SET LOCAL search_path = public, pg_catalog;

DO $verify$
DECLARE
    expected RECORD;
    failures TEXT[] := ARRAY[]::TEXT[];
BEGIN
    -- Missing tables/columns are reported as prerequisites, without querying
    -- application rows or guessing a legacy session's Account.
    FOR expected IN
        SELECT * FROM (VALUES
            ('users', 'id', 'uuid', true, ''),
            ('roles', 'code', 'character varying(50)', true, ''),
            ('accounts', 'id', 'uuid', true, ''),
            ('accounts', 'user_id', 'uuid', true, ''),
            ('accounts', 'role_code', 'character varying(50)', true, ''),
            ('user_refresh_tokens', 'id', 'uuid', true, ''),
            ('user_refresh_tokens', 'user_id', 'uuid', true, ''),
            ('user_refresh_tokens', 'account_id', 'uuid', false, ''),
            ('user_refresh_tokens', 'token_hash', 'character varying(255)', true, ''),
            ('user_refresh_tokens', 'device_info', 'character varying(255)', false, ''),
            ('user_refresh_tokens', 'ip_address', 'character varying(45)', false, ''),
            ('user_refresh_tokens', 'expires_at', 'timestamp with time zone', true, ''),
            ('user_refresh_tokens', 'access_token_id', 'uuid', true, ''),
            ('user_refresh_tokens', 'access_expires_at', 'timestamp with time zone', true, ''),
            ('user_refresh_tokens', 'is_revoked', 'boolean', true, ''),
            ('user_refresh_tokens', 'created_at', 'timestamp with time zone', true, ''),
            ('user_refresh_tokens', 'updated_at', 'timestamp with time zone', true, ''),
            ('InboxState', 'Id', 'bigint', true, 'd'),
            ('InboxState', 'MessageId', 'uuid', true, ''),
            ('InboxState', 'ConsumerId', 'uuid', true, ''),
            ('InboxState', 'LockId', 'uuid', true, ''),
            ('InboxState', 'RowVersion', 'bytea', false, ''),
            ('InboxState', 'Received', 'timestamp with time zone', true, ''),
            ('InboxState', 'ReceiveCount', 'integer', true, ''),
            ('InboxState', 'ExpirationTime', 'timestamp with time zone', false, ''),
            ('InboxState', 'Consumed', 'timestamp with time zone', false, ''),
            ('InboxState', 'Delivered', 'timestamp with time zone', false, ''),
            ('InboxState', 'LastSequenceNumber', 'bigint', false, ''),
            ('OutboxState', 'OutboxId', 'uuid', true, ''),
            ('OutboxState', 'LockId', 'uuid', true, ''),
            ('OutboxState', 'RowVersion', 'bytea', false, ''),
            ('OutboxState', 'Created', 'timestamp with time zone', true, ''),
            ('OutboxState', 'Delivered', 'timestamp with time zone', false, ''),
            ('OutboxState', 'LastSequenceNumber', 'bigint', false, ''),
            ('OutboxMessage', 'SequenceNumber', 'bigint', true, 'd'),
            ('OutboxMessage', 'EnqueueTime', 'timestamp with time zone', false, ''),
            ('OutboxMessage', 'SentTime', 'timestamp with time zone', true, ''),
            ('OutboxMessage', 'Headers', 'text', false, ''),
            ('OutboxMessage', 'Properties', 'text', false, ''),
            ('OutboxMessage', 'InboxMessageId', 'uuid', false, ''),
            ('OutboxMessage', 'InboxConsumerId', 'uuid', false, ''),
            ('OutboxMessage', 'OutboxId', 'uuid', false, ''),
            ('OutboxMessage', 'MessageId', 'uuid', true, ''),
            ('OutboxMessage', 'ContentType', 'character varying(256)', true, ''),
            ('OutboxMessage', 'MessageType', 'text', true, ''),
            ('OutboxMessage', 'Body', 'text', true, ''),
            ('OutboxMessage', 'ConversationId', 'uuid', false, ''),
            ('OutboxMessage', 'CorrelationId', 'uuid', false, ''),
            ('OutboxMessage', 'InitiatorId', 'uuid', false, ''),
            ('OutboxMessage', 'RequestId', 'uuid', false, ''),
            ('OutboxMessage', 'SourceAddress', 'character varying(256)', false, ''),
            ('OutboxMessage', 'DestinationAddress', 'character varying(256)', false, ''),
            ('OutboxMessage', 'ResponseAddress', 'character varying(256)', false, ''),
            ('OutboxMessage', 'FaultAddress', 'character varying(256)', false, ''),
            ('OutboxMessage', 'ExpirationTime', 'timestamp with time zone', false, '')
        ) AS columns(table_name, column_name, type_name, required, identity_kind)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_attribute a
            JOIN pg_class t ON t.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public' AND t.relname = expected.table_name
              AND t.relkind = 'r' AND a.attname = expected.column_name
              AND a.attnum > 0 AND NOT a.attisdropped
              AND format_type(a.atttypid, a.atttypmod) = expected.type_name
              AND a.attnotnull = expected.required
              AND a.attidentity::text = expected.identity_kind
        ) THEN
            failures := array_append(failures,
                format('missing/incompatible column %I.%I', expected.table_name, expected.column_name));
        END IF;
    END LOOP;

    IF to_regclass('public.account_roles') IS NOT NULL THEN
        failures := array_append(failures, 'obsolete account_roles relation is still present');
    END IF;

    -- Validate ownership and deletion semantics, not just constraint names.
    FOR expected IN
        SELECT * FROM (VALUES
            ('users', 'p', ARRAY['id'], NULL::text, NULL::text[], NULL::text),
            ('roles', 'p', ARRAY['code'], NULL::text, NULL::text[], NULL::text),
            ('accounts', 'p', ARRAY['id'], NULL, NULL, NULL),
            ('accounts', 'u', ARRAY['id','user_id'], NULL, NULL, NULL),
            ('accounts', 'f', ARRAY['user_id'], 'users', ARRAY['id'], 'c'),
            ('accounts', 'f', ARRAY['role_code'], 'roles', ARRAY['code'], 'r'),
            ('user_refresh_tokens', 'p', ARRAY['id'], NULL, NULL, NULL),
            ('user_refresh_tokens', 'f', ARRAY['user_id'], 'users', ARRAY['id'], 'c'),
            ('user_refresh_tokens', 'f', ARRAY['account_id','user_id'], 'accounts', ARRAY['id','user_id'], 'r'),
            ('InboxState', 'p', ARRAY['Id'], NULL, NULL, NULL),
            ('InboxState', 'u', ARRAY['MessageId','ConsumerId'], NULL, NULL, NULL),
            ('OutboxState', 'p', ARRAY['OutboxId'], NULL, NULL, NULL),
            ('OutboxMessage', 'p', ARRAY['SequenceNumber'], NULL, NULL, NULL),
            ('OutboxMessage', 'f', ARRAY['InboxMessageId','InboxConsumerId'], 'InboxState', ARRAY['MessageId','ConsumerId'], 'a'),
            ('OutboxMessage', 'f', ARRAY['OutboxId'], 'OutboxState', ARRAY['OutboxId'], 'a')
        ) AS constraints(table_name, kind, columns, principal, principal_columns, delete_action)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint c
            WHERE c.conrelid = to_regclass(format('public.%I', expected.table_name))
              AND c.contype::text = expected.kind AND c.convalidated
              AND NOT c.condeferrable
              AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(num, position)
                        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.num
                        ORDER BY k.position) = expected.columns
              AND (expected.principal IS NULL OR (
                  c.confrelid = to_regclass('public.' || quote_ident(expected.principal))
                  AND c.confdeltype::text = expected.delete_action AND c.confupdtype = 'a'
                  AND c.confmatchtype = 's'
                  AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(num, position)
                            JOIN pg_attribute a ON a.attrelid = c.confrelid AND a.attnum = k.num
                            ORDER BY k.position) = expected.principal_columns))
        ) THEN
            failures := array_append(failures,
                format('missing/incompatible constraint on %I(%s)', expected.table_name, array_to_string(expected.columns, ',')));
        END IF;
    END LOOP;

    -- Token uniqueness is required for exact lookup/replay decisions. The
    -- framework indexes below mirror script 14, including both ordering indexes.
    FOR expected IN
        SELECT * FROM (VALUES
            ('accounts', 'idx_accounts_role_code', ARRAY['role_code'], false),
            ('user_refresh_tokens', 'IX_user_refresh_tokens_account_id_user_id', ARRAY['account_id','user_id'], false),
            ('user_refresh_tokens', NULL, ARRAY['token_hash'], true),
            ('user_refresh_tokens', NULL, ARRAY['access_token_id'], true),
            ('InboxState', 'IX_InboxState_Delivered', ARRAY['Delivered'], false),
            ('OutboxState', 'IX_OutboxState_Created', ARRAY['Created'], false),
            ('OutboxMessage', 'IX_OutboxMessage_EnqueueTime', ARRAY['EnqueueTime'], false),
            ('OutboxMessage', 'IX_OutboxMessage_ExpirationTime', ARRAY['ExpirationTime'], false),
            ('OutboxMessage', 'IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber', ARRAY['InboxMessageId','InboxConsumerId','SequenceNumber'], true),
            ('OutboxMessage', 'IX_OutboxMessage_OutboxId_SequenceNumber', ARRAY['OutboxId','SequenceNumber'], true)
        ) AS indexes(table_name, index_name, columns, unique_required)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_index i JOIN pg_class x ON x.oid = i.indexrelid
            JOIN pg_am m ON m.oid = x.relam
            WHERE i.indrelid = to_regclass(format('public.%I', expected.table_name))
              AND (expected.index_name IS NULL OR x.relname = expected.index_name)
              AND i.indisvalid AND i.indisready AND i.indisunique = expected.unique_required
              AND NOT i.indnullsnotdistinct AND i.indpred IS NULL AND i.indexprs IS NULL
              AND i.indnatts = i.indnkeyatts AND m.amname = 'btree'
              AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ORDINALITY k(num, position)
                        JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.num
                        ORDER BY k.position) = expected.columns
        ) THEN
            failures := array_append(failures,
                format('missing/incompatible index on %I(%s)', expected.table_name, array_to_string(expected.columns, ',')));
        END IF;
    END LOOP;

    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger t JOIN pg_proc p ON p.oid = t.tgfoid
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE t.tgrelid = to_regclass('public.user_refresh_tokens')
          AND t.tgname = 'tr_auth_session_account_binding' AND NOT t.tgisinternal
          AND t.tgenabled IN ('O','A') AND t.tgtype = 19 AND t.tgqual IS NULL
          AND ARRAY(SELECT a.attname::text FROM unnest(t.tgattr) WITH ORDINALITY k(num, position)
                    JOIN pg_attribute a ON a.attrelid = t.tgrelid AND a.attnum = k.num
                    ORDER BY a.attname) = ARRAY['account_id','user_id']
          AND n.nspname = 'public' AND p.proname = 'protect_auth_session_account_binding'
          AND btrim(regexp_replace(p.prosrc, '\s+', ' ', 'g')) =
              'BEGIN IF NEW.account_id IS DISTINCT FROM OLD.account_id OR NEW.user_id IS DISTINCT FROM OLD.user_id THEN RAISE EXCEPTION ''Session account binding is immutable'' USING ERRCODE = ''23514''; END IF; RETURN NEW; END'
    ) THEN
        failures := array_append(failures, 'missing/incompatible immutable session Account/User binding trigger');
    END IF;

    IF cardinality(failures) > 0 THEN
        RAISE EXCEPTION USING
            MESSAGE = 'User & Access schema prerequisites are not satisfied',
            DETAIL = array_to_string(failures, E'\n'),
            HINT = 'For an empty database initialize consolidated 01-user-access-service-db.sql once; rerun verify-user-access-schema.sql with ON_ERROR_STOP=1. Existing databases require operator review; do not rerun base/14 blindly or backfill legacy session accounts. This verifier made no changes.';
    END IF;
    RAISE NOTICE 'PASS: User & Access direct-role, session binding and outbox schema prerequisites verified; no changes made.';
END
$verify$;

COMMIT;
