-- SPARK-248: exact Account binding for newly issued sessions.
-- Review/deploy on the User & Access database BEFORE deploying account-bound auth code.
-- No startup migration. Legacy rows deliberately remain NULL and require sign-in again;
-- guessing or backfilling the account would make a multi-account session ambiguous.
BEGIN;

ALTER TABLE user_refresh_tokens ADD COLUMN IF NOT EXISTS account_id UUID NULL;
CREATE INDEX IF NOT EXISTS "IX_user_refresh_tokens_account_id" ON user_refresh_tokens(account_id);
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
        WHERE conrelid = 'user_refresh_tokens'::regclass
          AND conname = 'FK_user_refresh_tokens_accounts_account_id') THEN
        ALTER TABLE user_refresh_tokens ADD CONSTRAINT "FK_user_refresh_tokens_accounts_account_id"
            FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE RESTRICT;
    END IF;
END $$;

-- Enforce immutable binding for every database writer, including NULL legacy sessions.
-- EF also marks the property immutable after save. Session rotation changes only token
-- identity/hash and access expiry, never account ownership or absolute refresh expiry.
CREATE OR REPLACE FUNCTION protect_auth_session_account_binding() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.account_id IS DISTINCT FROM OLD.account_id THEN
        RAISE EXCEPTION 'Session account binding is immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS tr_auth_session_account_binding ON user_refresh_tokens;
CREATE TRIGGER tr_auth_session_account_binding BEFORE UPDATE OF account_id ON user_refresh_tokens
    FOR EACH ROW EXECUTE FUNCTION protect_auth_session_account_binding();

COMMIT;

-- Rollback is a reviewed deployment operation: first roll application code back, then
-- decide how to invalidate bound sessions. Do not drop this column while new code runs.
-- This script neither deletes sessions nor mutates any legacy account association.
