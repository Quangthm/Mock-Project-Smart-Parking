-- REVIEW ONLY: diagnostic inventory, not a migration or readiness certificate.
-- Execute only after approval against the reviewed IoT datasource and schema.
-- Current migrations use unqualified tables: this inventory assumes public.
-- If datasource search_path differs, stop and review schema mapping first.
-- psql: use the selected connection/service securely, ON_ERROR_STOP=1.
-- No application rows, credentials, JWTs, plaintext or ciphertext are returned.
BEGIN READ ONLY;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '1s';

WITH expected(table_name, column_name, type_name, required_not_null) AS (
    VALUES
    ('assistant_management_state','id','integer',true),
    ('assistant_management_state','state','jsonb',true),
    ('assistant_management_credentials','slot_id','uuid',true),
    ('assistant_management_credentials','version','uuid',true),
    ('assistant_management_credentials','protected_value','text',true),
    ('assistant_management_receipts','actor','uuid',true),
    ('assistant_management_receipts','mutation_id','uuid',true),
    ('assistant_management_receipts','fingerprint','text',true),
    ('assistant_management_receipts','revision','bigint',true),
    ('assistant_management_receipts','action','text',true),
    ('assistant_management_receipts','at','timestamp with time zone',true),
    ('assistant_connection_tests','actor','uuid',true),
    ('assistant_connection_tests','request_id','uuid',true),
    ('assistant_connection_tests','configuration_id','uuid',true),
    ('assistant_connection_tests','revision','bigint',true),
    ('assistant_connection_tests','model','text',true),
    ('assistant_connection_tests','admitted_at','timestamp with time zone',true),
    ('assistant_connection_tests','result','jsonb',false),
    ('assistant_conversations','id','uuid',true),
    ('assistant_conversations','owner_account_id','uuid',true),
    ('assistant_conversations','client_conversation_id','uuid',true),
    ('assistant_conversations','locale','character varying(2)',true),
    ('assistant_conversations','revision','bigint',true),
    ('assistant_conversations','expires_at','timestamp with time zone',true),
    ('assistant_conversations','retention_hold','boolean',true),
    ('assistant_messages','id','uuid',true),
    ('assistant_messages','conversation_id','uuid',true),
    ('assistant_messages','sequence','bigint',true),
    ('assistant_messages','client_message_id','uuid',true),
    ('assistant_messages','response','jsonb',true),
    ('assistant_requests','id','uuid',true),
    ('assistant_requests','conversation_id','uuid',true),
    ('assistant_requests','owner_account_id','uuid',true),
    ('assistant_requests','client_message_id','uuid',true),
    ('assistant_requests','fingerprint','character varying(64)',true),
    ('assistant_requests','state','character varying(16)',true),
    ('assistant_requests','deadline','timestamp with time zone',true),
    ('assistant_requests','created_at','timestamp with time zone',true),
    ('assistant_requests','finished_at','timestamp with time zone',false),
    ('assistant_requests','user_message_id','uuid',true),
    ('assistant_requests','answer','jsonb',false),
    ('assistant_requests','error_code','character varying(64)',false),
    ('assistant_requests','snapshot','jsonb',true)
), observed AS (
    SELECT e.*, c.oid AS table_oid, c.relkind, a.attname,
        pg_catalog.format_type(a.atttypid,a.atttypmod) AS actual_type, a.attnotnull
    FROM expected e
    LEFT JOIN pg_catalog.pg_namespace n ON n.nspname='public'
    LEFT JOIN pg_catalog.pg_class c ON c.relnamespace=n.oid AND c.relname=e.table_name
    LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attname=e.column_name
        AND a.attnum>0 AND NOT a.attisdropped
)
SELECT table_name, column_name,
    CASE WHEN table_oid IS NULL THEN 'MISSING_TABLE'
         WHEN relkind NOT IN ('r','p') THEN 'WRONG_RELATION_KIND'
         WHEN attname IS NULL THEN 'MISSING_COLUMN'
         WHEN actual_type<>type_name THEN 'TYPE_MISMATCH'
         WHEN attnotnull<>required_not_null THEN 'NULLABILITY_MISMATCH'
         ELSE 'COLUMN_SHAPE_MATCH' END AS diagnostic
FROM observed ORDER BY table_name,column_name;

-- Inventory definitions for review: matching names alone cannot prove correctness.
SELECT tablename, indexname, indexdef
FROM pg_catalog.pg_indexes
WHERE schemaname='public' AND tablename IN (
    'assistant_management_state','assistant_management_credentials','assistant_management_receipts',
    'assistant_connection_tests','assistant_conversations','assistant_messages','assistant_requests')
ORDER BY tablename,indexname;

SELECT c.relname AS table_name, con.conname, pg_catalog.pg_get_constraintdef(con.oid) AS definition,
    con.convalidated AS validated
FROM pg_catalog.pg_constraint con
JOIN pg_catalog.pg_class c ON c.oid=con.conrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='public' AND c.relname IN (
    'assistant_management_state','assistant_management_credentials','assistant_management_receipts',
    'assistant_connection_tests','assistant_conversations','assistant_messages','assistant_requests')
ORDER BY c.relname,con.conname;
ROLLBACK;
-- Missing new ownership/sequence fields on existing conversation/message tables
-- identifies a legacy/partial shape, not permission to reset or infer Account IDs.
-- Even all COLUMN_SHAPE_MATCH does not establish complete schema, privileges,
-- JSON state migration, legacy-row conversion, locks/concurrency or runtime readiness.
