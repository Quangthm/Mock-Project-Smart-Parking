-- Apply only to User DB, after the unmodified 01-user-service-db.sql from arch/database-schema.
-- Explicit lifecycle states preserve FR-AUTH-01/05; do not activate unverified identities.
BEGIN;
ALTER TABLE users DROP CONSTRAINT IF EXISTS users_status_check;
ALTER TABLE users ADD CONSTRAINT users_status_check CHECK (status IN
    ('ACTIVE','INACTIVE','LOCKED','PENDING_APPROVAL','PENDING_VERIFICATION','REJECTED'));
INSERT INTO roles(code,name) VALUES ('ADMIN','System Administrator'),('DRIVER','Driver'),
    ('BUSINESS_OWNER','Parking Business Owner'),('SITE_OPERATOR','Site Operator') ON CONFLICT DO NOTHING;
COMMIT;
