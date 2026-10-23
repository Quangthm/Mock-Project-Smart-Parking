-- Demo identity seed for UserDb.
-- Run through scripts/seed-demo-site.ps1; psql variables provide the fixed IDs and emails.
BEGIN;

-- UserDb owns the canonical tenant row; insert it before assigning accounts.
INSERT INTO tenants (id, code, name, contact_email, contact_phone, status)
VALUES (
    :'tenant_id'::uuid,
    'DEMO-OWNER',
    'SmartPark Demo Owner',
    :'owner_email',
    '0900000001',
    'ACTIVE'
)
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name,
    contact_email = EXCLUDED.contact_email,
    contact_phone = EXCLUDED.contact_phone,
    status = 'ACTIVE',
    updated_at = CURRENT_TIMESTAMP;

-- Owner account: must already be created by UserService DataSeeder.
UPDATE accounts a
SET tenant_id = :'tenant_id'::uuid,
    status = 'ACTIVE',
    updated_at = CURRENT_TIMESTAMP
FROM users u
WHERE a.user_id = u.id
  AND lower(u.email) = lower(:'owner_email')
  AND a.deleted_at IS NULL
  AND a.site_id IS NULL
  AND EXISTS (
      SELECT 1
      FROM account_roles r
      WHERE r.account_id = a.id
        AND r.role_code = 'BUSINESS_OWNER'
  );

-- Operator account: assign tenant + demo site.
UPDATE accounts a
SET tenant_id = :'tenant_id'::uuid,
    site_id = :'site_id'::uuid,
    status = 'ACTIVE',
    updated_at = CURRENT_TIMESTAMP
FROM users u
WHERE a.user_id = u.id
  AND lower(u.email) = lower(:'operator_email')
  AND a.deleted_at IS NULL
  AND EXISTS (
      SELECT 1
      FROM account_roles r
      WHERE r.account_id = a.id
        AND r.role_code = 'SITE_OPERATOR'
  );

-- Delegable operator permissions supported by the current overlay.
INSERT INTO operator_grants (account_id, created_by, permissions)
SELECT a.id,
       (
           SELECT u2.id
           FROM users u2
           WHERE lower(u2.email) = lower(:'owner_email')
             AND u2.deleted_at IS NULL
       ),
       ARRAY[
           'DEVICE_MANAGE',
           'DEVICE_STATUS_VIEW',
           'CASH_COLLECT',
           'APPEAL_REVIEW',
           'SLOT_OVERRIDE'
       ]::text[]
FROM accounts a
JOIN users u ON u.id = a.user_id
WHERE lower(u.email) = lower(:'operator_email')
  AND a.deleted_at IS NULL
  AND EXISTS (
      SELECT 1
      FROM account_roles r
      WHERE r.account_id = a.id
        AND r.role_code = 'SITE_OPERATOR'
  )
ON CONFLICT (account_id) DO UPDATE
SET permissions = EXCLUDED.permissions,
    created_by = EXCLUDED.created_by;

COMMIT;
