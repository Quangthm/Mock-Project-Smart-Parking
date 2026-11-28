-- Runs inside the entry point's single UserDb transaction.
SELECT pg_temp.require(current_database()='smartpark_user' AND current_user='smartpark_user','Unexpected UserDb identity');
LOCK TABLE public.users, public.accounts, public.tenants, public.vehicles, public.owner_applications,
    public.operator_grants IN SHARE ROW EXCLUSIVE MODE;
SELECT pg_temp.require((SELECT count(*)=3 FROM roles WHERE code IN ('BUSINESS_OWNER','SITE_OPERATOR','DRIVER')),
    'Missing role catalogue');
SELECT pg_temp.require((SELECT count(*)=3 FROM pg_index WHERE indisvalid AND indisunique AND indexrelid IN
    (to_regclass('uq_active_user_email_normalized'),to_regclass('uq_active_vehicle_plate'),to_regclass('uq_active_tenant_code'))),
    'Missing aligned User identity indexes');
SELECT pg_temp.require(EXISTS(SELECT FROM pg_trigger WHERE tgrelid='user_refresh_tokens'::regclass
    AND tgname='tr_auth_session_account_binding' AND tgenabled IN ('A','O')), 'Missing account/session immutability trigger');
SELECT pg_temp.require(EXISTS(SELECT FROM pg_constraint WHERE conrelid='vehicles'::regclass AND contype='c'
    AND convalidated AND pg_get_constraintdef(oid) LIKE '%binding_revision%> 0%'), 'Missing positive vehicle binding revision constraint');

UPDATE run_context SET established=EXISTS(SELECT FROM public.tenants WHERE id=(SELECT id FROM site_input WHERE n='01'));
CREATE TEMP TABLE e_tenants AS SELECT id,tenant_code AS code,business AS name,email AS contact_email,phone AS contact_phone,
    'ACTIVE'::text AS status,NULL::timestamptz AS deleted_at,t-interval '120 days' AS created_at FROM owner_input,run_context;
SELECT pg_temp.sync('tenants','id',ARRAY['created_at']);

CREATE TEMP TABLE e_users AS SELECT uid AS id,email,phone,hash AS password_hash,'MVPSEED '||role||' '||n AS full_name,
    CASE WHEN role='owner' THEN 'MVPSEED Owner '||n END AS company_name,'ACTIVE'::text AS status,
    t-interval '120 days' AS created_at,t-interval '120 days' AS email_verified_at,NULL::timestamptz AS deleted_at
    FROM person_input,run_context;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM e_users e JOIN users u ON lower(u.email)=e.email OR u.phone=e.phone
    WHERE u.id<>e.id), 'Fixture email or phone already belongs to another identity');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM e_users e JOIN users u USING(id)
    WHERE u.password_hash !~ '^\$2[aby]\$12\$[./A-Za-z0-9]{53}$' OR u.access_version<0 OR u.locked_until>statement_timestamp()),
    'Existing identity has an incompatible credential, version or lock');
SELECT pg_temp.sync('users','id',ARRAY['created_at','email_verified_at'],ARRAY['password_hash']);

CREATE TEMP TABLE e_accounts AS SELECT id,uid AS user_id,CASE role WHEN 'owner' THEN 'BUSINESS_OWNER'
    WHEN 'operator' THEN 'SITE_OPERATOR' ELSE 'DRIVER' END AS role_code,tenant AS tenant_id,site AS site_id,
    CASE WHEN role='owner' THEN ARRAY['PARKING_MANAGE','OPERATOR_MANAGE','BACKUP_CONFIGURE','BACKUP_MARK'] END AS permissions,
    'ACTIVE'::text AS status,NULL::timestamptz AS deleted_at,t-interval '120 days' AS created_at FROM person_input,run_context;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM accounts a JOIN e_accounts e ON a.user_id=e.user_id WHERE a.id<>e.id AND a.deleted_at IS NULL),
    'Fixture user has another Account');
SELECT pg_temp.sync('accounts','id',ARRAY['created_at']);

-- The normal review checks a live Admin with ACCOUNT_ADMIN (NULL is role-default access).
-- Preserve the original reviewer on replay; alternative active admins must not replace evidence.
CREATE TEMP TABLE e_owner_applications AS
SELECT o.id,o.uid AS user_id,o.business AS business_name,o.type AS lot_type,o.id AS tenant_id,o.id AS site_id,
    (SELECT jsonb_object_agg(upper(left(key,1))||substr(key,2),value) FROM jsonb_each(o.lot)) AS lot_setup,
    'approved'::text AS status,t-interval '120 days' AS submitted_at,t-interval '120 days' AS reviewed_at,
    coalesce(prior.reviewed_by,reviewer.id) AS reviewed_by, coalesce(prior.reviewed_by,reviewer.id) AS approval_requested_by,
    'MVP-SEED-V1 initial owner onboarding'::text AS review_note,
    prior.terms_id AS terms_id,t-interval '120 days' AS agreed_at
FROM owner_input o CROSS JOIN run_context
LEFT JOIN owner_applications prior ON prior.id=o.id
LEFT JOIN LATERAL (SELECT u.id FROM users u JOIN accounts a ON a.user_id=u.id
    WHERE u.status='ACTIVE' AND u.deleted_at IS NULL AND a.status='ACTIVE' AND a.deleted_at IS NULL
      AND a.role_code IN ('ADMIN','PLATFORM_ADMIN') AND (a.permissions IS NULL OR 'ACCOUNT_ADMIN'=ANY(a.permissions))
      AND NOT EXISTS(SELECT FROM accounts other WHERE other.user_id=u.id AND other.deleted_at IS NULL AND other.status='ACTIVE'
        AND other.id<a.id AND other.role_code NOT IN ('ADMIN','PLATFORM_ADMIN'))
    ORDER BY u.id LIMIT 1) reviewer ON true;
-- Current OwnerRegistrationService records PolicyAccepted in the setup. Where applicable,
-- attach a platform agreement effective at the fixture agreement instant.
UPDATE e_owner_applications e SET terms_id=(SELECT id FROM system_terms_and_conditions
    WHERE term_code='OWNER_ONBOARDING' AND is_active AND effective_from<=e.agreed_at
    ORDER BY effective_from DESC,id LIMIT 1) WHERE NOT EXISTS(SELECT FROM owner_applications p WHERE p.id=e.id);
SELECT pg_temp.require(NOT EXISTS(SELECT FROM e_owner_applications WHERE reviewed_by IS NULL),
    'An existing authorized development Admin reviewer is required');
SELECT pg_temp.sync('owner_applications','id',ARRAY['submitted_at','agreed_at','reviewed_at']);

CREATE TEMP TABLE e_operator_grants AS SELECT a.id AS account_id,o.uid AS created_by,
    CASE WHEN a.n::int%2=0 THEN ARRAY['DEVICE_STATUS_VIEW','CASH_COLLECT','SLOT_OVERRIDE']
    ELSE ARRAY['DEVICE_STATUS_VIEW','CASH_COLLECT'] END AS permissions
    FROM person_input a JOIN owner_input o ON o.id=a.tenant WHERE a.role='operator';
SELECT pg_temp.sync('operator_grants','account_id');

CREATE TEMP TABLE e_vehicles AS SELECT id,account AS account_id,plate AS original_plate,canonical AS normalized_plate,
    'CAR'::text AS vehicle_type,1::bigint AS binding_revision,NULL::timestamptz AS deleted_at,
    t-interval '120 days' AS created_at FROM vehicle_input,run_context;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM vehicles v JOIN e_vehicles e USING(normalized_plate) WHERE v.id<>e.id)
    AND NOT EXISTS(SELECT FROM vehicle_registration_claims c JOIN e_vehicles e USING(normalized_plate)),
    'A fixture plate has conflicting active, historical, or disputed ownership');
SELECT pg_temp.sync('vehicles','id',ARRAY['created_at']);
SELECT format('User persisted: %s tenants, %s Owner, %s Operator, %s Driver accounts, %s CAR vehicles',
    (SELECT count(*) FROM tenants JOIN e_tenants USING(id)),
    count(*) FILTER (WHERE a.role_code='BUSINESS_OWNER'), count(*) FILTER (WHERE a.role_code='SITE_OPERATOR'),
    count(*) FILTER (WHERE a.role_code='DRIVER'),(SELECT count(*) FROM vehicles JOIN e_vehicles USING(id)))
FROM accounts a JOIN e_accounts e USING(id) HAVING (SELECT mode='verify' FROM run_context);

