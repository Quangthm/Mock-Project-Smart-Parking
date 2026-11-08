-- Manual compatibility migration for the observed existing User DB, not base replay.
-- Backup User DB/workflow protection material; restore/rehearse and run full verifier
-- before rollout. Stop old User binaries: they must not write onboarding_complete.
-- Preserves onboarding values under a legacy name; never maps them to Account approval.
-- Preserves refresh idle policy: null last_refreshed_at falls back to created_at,
-- exactly as current PostgresAuthSessionStore; never backfill with migration time.
BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
LOCK TABLE users, user_refresh_tokens, rotated_session_proofs IN ACCESS EXCLUSIVE MODE;

DO $check$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='user_refresh_tokens'::regclass
        AND attname='created_at' AND NOT attisdropped AND attnotnull
        AND atttypid='timestamptz'::regtype) THEN
        RAISE EXCEPTION 'Unexpected refresh created_at schema; reviewed conversion required';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='user_refresh_tokens'::regclass
        AND attname='last_refreshed_at' AND NOT attisdropped AND atttypid<>'timestamptz'::regtype) THEN
        RAISE EXCEPTION 'Unexpected last_refreshed_at type; no conversion attempted';
    END IF;
    IF EXISTS (SELECT 1 FROM rotated_session_proofs p LEFT JOIN users u ON u.id=p.user_id WHERE u.id IS NULL) THEN
        RAISE EXCEPTION 'Orphan session proofs require review; no deletion or fabricated user';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='users'::regclass
        AND attname='onboarding_complete' AND NOT attisdropped) THEN
        IF EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='users'::regclass
            AND attname='legacy_onboarding_complete' AND NOT attisdropped) THEN
            RAISE EXCEPTION 'Both onboarding column names exist; do not overwrite either';
        END IF;
        ALTER TABLE users RENAME COLUMN onboarding_complete TO legacy_onboarding_complete;
    END IF;
END $check$;

ALTER TABLE user_refresh_tokens ADD COLUMN IF NOT EXISTS last_refreshed_at timestamptz;
UPDATE user_refresh_tokens SET last_refreshed_at=created_at WHERE last_refreshed_at IS NULL;
ALTER TABLE user_refresh_tokens ALTER COLUMN last_refreshed_at SET NOT NULL;
ALTER TABLE user_refresh_tokens ALTER COLUMN last_refreshed_at SET DEFAULT CURRENT_TIMESTAMP;

DO $fk$
DECLARE existing pg_constraint%ROWTYPE;
BEGIN
    SELECT * INTO existing FROM pg_constraint
        WHERE conrelid='rotated_session_proofs'::regclass AND conname='rotated_session_proofs_user_id_fkey';
    IF FOUND THEN
        IF existing.contype<>'f' OR existing.confrelid<>'users'::regclass
            OR existing.conkey<>ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid='rotated_session_proofs'::regclass AND attname='user_id')]::smallint[]
            OR existing.confkey<>ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid='users'::regclass AND attname='id')]::smallint[]
            OR existing.confupdtype<>'a' OR existing.confmatchtype<>'s' OR existing.condeferrable
            OR existing.confdeltype NOT IN ('a','c') THEN
            RAISE EXCEPTION 'Unexpected proof user FK; no constraint replacement attempted';
        END IF;
        IF existing.confdeltype='a' THEN
            ALTER TABLE rotated_session_proofs DROP CONSTRAINT rotated_session_proofs_user_id_fkey;
        END IF;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='rotated_session_proofs'::regclass
        AND conname='rotated_session_proofs_user_id_fkey') THEN
        ALTER TABLE rotated_session_proofs ADD CONSTRAINT rotated_session_proofs_user_id_fkey
            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE NOT VALID;
    END IF;
    ALTER TABLE rotated_session_proofs VALIDATE CONSTRAINT rotated_session_proofs_user_id_fkey;
END $fk$;
COMMIT;
-- Rollback: keep the new compatible schema and legacy values; do not resume old
-- binaries or remove last_refreshed_at/defaults as an automatic downgrade.
-- Restore only from the verified backup with writers stopped and a reviewed plan
-- for any post-backup sessions/workflows. No DROP data or reset is supplied.
