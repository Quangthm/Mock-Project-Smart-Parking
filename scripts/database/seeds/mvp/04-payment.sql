-- Payment owns pricing and money. Plan and subscription UUIDs are logical Operations references.
SELECT pg_temp.require(current_database()='smartpark_payment' AND current_user='smartpark_payment','Unexpected PaymentDb identity');
LOCK TABLE tariffs,policy_revisions,subscription_financial_terms,subscription_financial_term_revisions,
    payment_orders,payment_transactions,invoices IN SHARE ROW EXCLUSIVE MODE;
UPDATE run_context SET established=EXISTS(SELECT FROM policy_revisions
    WHERE id=(SELECT tariffrev FROM commercial_input WHERE n='01'));

-- The reviewed quote reader considers even inactive/deleted same-site CAR tariffs.
SELECT pg_temp.require(NOT EXISTS(SELECT FROM tariffs t JOIN commercial_input c ON t.site_id=c.site
    WHERE t.vehicle_type='CAR' AND t.id<>c.tariff),'Competing same-site CAR tariff');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM policy_revisions r JOIN tariffs t ON t.id=r.policy_id
    JOIN commercial_input c ON r.scope_type='GLOBAL' OR (r.scope_type='TENANT' AND t.tenant_id=c.tenant)
    WHERE t.is_active AND t.deleted_at IS NULL AND t.vehicle_type='CAR' AND r.policy_type='TARIFF'
    AND (r.effective_to IS NULL OR r.effective_to>c.inception)), 'Wider-scope discovery pricing conflicts with fixture interval');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM policy_revisions r JOIN commercial_input c ON r.policy_id IN (c.term,c.tariff)
    WHERE (r.policy_id,r.id) NOT IN ((c.tariff,c.tariffrev),(c.term,c.auditrev))),
    'Existing or scheduled revision on a fixture tariff or subscription term');
CREATE TEMP TABLE e_tariffs AS SELECT tariff AS id,tenant AS tenant_id,site AS site_id,code||' CAR' AS name,
    'CAR'::text AS vehicle_type,grace AS free_parking_grace_minutes,rules AS tariff_rules,true AS is_active,
    inception AS created_at,inception AS updated_at,NULL::timestamptz AS deleted_at FROM commercial_input;
SELECT pg_temp.sync('tariffs','id',ARRAY['created_at','updated_at']);

SELECT pg_temp.require(NOT EXISTS(SELECT FROM subscription_financial_terms f JOIN commercial_input c ON f.plan_id=c.plan
    WHERE f.id<>c.term) AND NOT EXISTS(SELECT FROM subscription_financial_term_revisions r JOIN commercial_input c
    ON r.financial_term_id=c.term OR r.plan_id=c.plan WHERE r.id<>c.termrev),'Competing monthly term or financial revision');
CREATE TEMP TABLE e_subscription_financial_terms AS SELECT term AS id,tenant AS tenant_id,site AS site_id,plan AS plan_id,
    'MONTHLY'::text AS product_type,1 AS active_revision,'VND'::text AS currency,true AS is_active,actor AS created_by,
    inception AS created_at,inception AS updated_at FROM commercial_input;
SELECT pg_temp.sync('subscription_financial_terms','id',ARRAY['created_at','updated_at']);
CREATE TEMP TABLE e_subscription_financial_term_revisions AS SELECT termrev AS id,term AS financial_term_id,plan AS plan_id,
    1 AS plan_revision,1 AS revision_number,jsonb_build_array(jsonb_build_object('vehicleType','CAR','fullPeriodPrice',monthly)) AS vehicle_prices,
    '{"voluntaryBeforeStartPercent":100,"voluntaryInPeriodPercent":50,"facilityClosurePercent":100,"roundingMode":"HALF_UP_THOUSAND"}'::jsonb AS refund_policy,
    inception AS effective_from,NULL::timestamptz AS effective_to,actor AS created_by,inception AS created_at FROM commercial_input;
SELECT pg_temp.sync('subscription_financial_term_revisions','id',ARRAY['effective_from','created_at']);

CREATE TEMP TABLE e_policy_revisions AS SELECT tariffrev AS id,tariff AS policy_id,'TARIFF'::text AS policy_type,1 AS revision_number,
    inception AS effective_from,NULL::timestamptz AS effective_to,'SITE'::text AS scope_type,site AS scope_id,
    rules AS policy_values,actor AS created_by,inception AS created_at FROM commercial_input
UNION ALL
SELECT auditrev,term,'SUBSCRIPTION',r.revision_number,inception,NULL,'SITE',site,
    jsonb_build_object('financialTermId',term,'siteId',site,'planId',plan,'planRevision',r.plan_revision,
        'financialTermRevision',r.revision_number,'productType','MONTHLY','currency','VND','vehiclePrices',r.vehicle_prices,
        'refundPolicy',r.refund_policy),actor,inception
FROM commercial_input c JOIN e_subscription_financial_term_revisions r ON r.id=c.termrev;
SELECT pg_temp.sync('policy_revisions','id',ARRAY['effective_from','created_at']);

-- Start-now: order at S-2 minutes, cash settled at S-1 minute, rights committed at S.
CREATE TEMP TABLE e_payment_orders AS SELECT purchase AS id,tenant AS tenant_id,site AS site_id,account AS account_id,
    'MVP-B-'||purchase AS order_code,'MVP-B-'||purchase AS idempotency_key,'MONTHLY_PASS_FEE'::text AS order_type,
    'MONTHLY_PASS'::text AS reference_type,subscription AS reference_id,plate AS plate_number,
    NULL::uuid AS quote_id,NULL::text AS reviewed_method,NULL::text AS reviewed_currency,false AS is_provisioning_fence,
    NULL::text AS zero_disposition_reason,NULL::text AS terms_version,
    monthly AS amount,0 AS discount_amount,0 AS deposit_deducted,monthly AS final_amount,'PAID'::text AS status,
    starts+interval '13 minutes' AS expires_at,starts-interval '2 minutes' AS created_at,starts-interval '1 minute' AS updated_at
FROM purchase_input;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM payment_orders p JOIN e_payment_orders e ON p.id<>e.id AND
    (p.order_code=e.order_code OR p.idempotency_key=e.idempotency_key OR (p.reference_type='MONTHLY_PASS' AND p.reference_id=e.reference_id))),
    'Monthly order code, idempotency key or subscription reference already used');
SELECT pg_temp.sync('payment_orders','id',ARRAY['expires_at','created_at','updated_at']);

CREATE TEMP TABLE e_payment_transactions AS SELECT cash AS id,tenant AS tenant_id,purchase AS payment_order_id,
    'PAYMENT'::text AS transaction_type,'CASH'::text AS payment_method,NULL::text AS gateway_transaction_id,
    NULL::text AS provider_collection_reference,NULL::timestamptz AS provider_verified_at,'MVP-B-'||cash AS idempotency_key,
    monthly AS amount,'SUCCESS'::text AS status,starts-interval '1 minute' AS paid_at,starts-interval '1 minute' AS created_at
FROM purchase_input;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM payment_transactions p JOIN e_payment_transactions e ON p.id<>e.id AND
    (p.payment_order_id=e.payment_order_id OR p.idempotency_key=e.idempotency_key)), 'Another transaction exists for this monthly purchase');
SELECT pg_temp.sync('payment_transactions','id',ARRAY['paid_at','created_at']);

-- InvoiceConfiguration uses the default (PascalCase) serializer. Financial terms use web casing above.
CREATE TEMP TABLE e_invoices AS SELECT invoice AS id,tenant AS tenant_id,purchase AS payment_order_id,site AS site_id,
    'MONTHLY_PASS'::text AS reference_type,subscription AS reference_id,NULL::uuid AS session_id,'MVP-B-'||invoice AS invoice_number,
    0 AS total_parked_minutes,0 AS base_parking_fee,0 AS overstay_penalty_fee,monthly AS final_amount,
    jsonb_build_object('OrderCode',o.order_code,'AccountId',account,'CustomerType','REGISTERED_DRIVER','SiteName',name,
        'VehicleType','CAR','PlateNumber',plate,'EntryTime',o.created_at,'PricingCutoffAt',o.updated_at,
        'PolicyName','MONTHLY','FinalTotalAmount',monthly,'Currency','VND','Status','PAID','IsSettled',true,
        'PaidAt',o.updated_at,'PaymentMethod','CASH','TransactionReference',cash,
        'LineItems',jsonb_build_array(jsonb_build_object('ItemType','MONTHLY_PASS_FEE','Description','Monthly CAR pass',
            'StartTime',starts,'EndTime',ends,'Unit','MONTH','UnitRate',monthly,'Amount',monthly))) AS pricing_snapshot,
    o.updated_at AS created_at FROM purchase_input p JOIN e_payment_orders o ON o.id=p.purchase;
SELECT pg_temp.require(NOT EXISTS(SELECT FROM invoices p JOIN e_invoices e ON p.id<>e.id AND
    (p.invoice_number=e.invoice_number OR p.payment_order_id=e.payment_order_id OR
    (p.reference_type='MONTHLY_PASS' AND p.reference_id=e.reference_id) OR
    p.pricing_snapshot->>'TransactionReference'=e.pricing_snapshot->>'TransactionReference')), 'Invoice number, order or transaction reference collision');
SELECT pg_temp.sync('invoices','id',ARRAY['created_at','pricing_snapshot']);
SELECT pg_temp.require((SELECT count(*)=3 AND sum(o.final_amount)=2250000 AND bool_and(
    o.created_at<=x.paid_at AND x.paid_at<=p.starts AND p.starts<o.expires_at
    AND f.effective_from<=o.created_at AND (f.effective_to IS NULL OR o.created_at<f.effective_to)
    AND o.final_amount=x.amount AND x.amount=i.final_amount)
    FROM purchase_input p JOIN payment_orders o ON o.id=p.purchase AND o.reference_id=p.subscription
    JOIN payment_transactions x ON x.id=p.cash AND x.payment_order_id=o.id
    JOIN invoices i ON i.id=p.invoice AND i.payment_order_id=o.id AND i.reference_id=p.subscription
    JOIN subscription_financial_term_revisions f ON f.id=p.termrev AND f.financial_term_id=p.term AND f.plan_id=p.plan)
    ,'Persisted monthly purchase chain or effective financial revision does not match the manifest')
WHERE (SELECT mode='verify' FROM run_context);
SELECT format('Payment verified: %s fixture monthly invoices, %s VND',count(*),sum(i.final_amount))
FROM invoices i JOIN purchase_input p ON p.invoice=i.id HAVING (SELECT mode='verify' FROM run_context);

-- CHECKPOINT C
SELECT pg_temp.require(current_database()='smartpark_payment' AND current_user='smartpark_payment','Unexpected PaymentDb identity');
LOCK TABLE reservation_quotes,payment_orders,payment_transactions,invoices,tariffs,policy_revisions IN SHARE ROW EXCLUSIVE MODE;
UPDATE run_context SET established=EXISTS(SELECT FROM reservation_quotes
    WHERE id=(SELECT (j->>'quote_anchor')::uuid FROM lifecycle_manifest));
SELECT pg_temp.require(NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_populate_recordset(NULL::payment_orders,m.j->'payment_orders') e
    JOIN payment_orders p ON p.id<>e.id AND (p.order_code=e.order_code OR p.idempotency_key=e.idempotency_key
    OR p.quote_id=e.quote_id OR (p.reference_type=e.reference_type AND p.reference_id=e.reference_id))),
    'C order code, quote, idempotency or logical reference collision');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_populate_recordset(NULL::payment_transactions,m.j->'payment_transactions') e
    JOIN payment_transactions p ON p.id<>e.id AND (p.payment_order_id=e.payment_order_id OR
    (p.payment_method=e.payment_method AND (p.gateway_transaction_id=e.gateway_transaction_id OR p.provider_collection_reference=e.provider_collection_reference)))),
    'C checkout-link or collection collision');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM lifecycle_manifest m,jsonb_populate_recordset(NULL::invoices,m.j->'invoices') e
    JOIN invoices p ON p.id<>e.id AND (p.invoice_number=e.invoice_number OR p.payment_order_id=e.payment_order_id)),
    'C invoice number or order collision');
SELECT pg_temp.require(NOT EXISTS(SELECT FROM
    jsonb_populate_recordset(NULL::reservation_quotes,pg_temp.lifecycle_rows('reservation_quotes')) q
    WHERE NOT EXISTS(SELECT FROM tariffs t JOIN policy_revisions r ON r.policy_id=t.id
        WHERE t.id=q.tariff_id AND t.site_id=q.site_id AND t.tenant_id=q.tenant_id
        AND t.is_active AND t.deleted_at IS NULL AND t.vehicle_type='CAR'
        AND r.id=q.tariff_revision_id AND r.policy_type='TARIFF' AND r.effective_from<=q.created_at
        AND (r.effective_to IS NULL OR q.end_time<=r.effective_to))),
    'Reviewed C quote is outside the installed tariff revision')
WHERE (SELECT mode<>'check' FROM run_context) AND NOT current_setting('mvp.prior_timeline')::bool;
SELECT pg_temp.sync_lifecycle('reservation_quotes');
SELECT pg_temp.sync_lifecycle('payment_orders');
SELECT pg_temp.sync_lifecycle('payment_transactions');
SELECT pg_temp.sync_lifecycle('invoices');
