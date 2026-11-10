-- Read-only, aggregate inventory AFTER the canonical media upgrade. Run only against an
-- explicitly approved UserDb. No URLs, user IDs or provider identifiers emitted.
BEGIN TRANSACTION READ ONLY;
WITH media_refs AS (
    SELECT 'avatar' AS purpose, u.id AS user_id, NULL::uuid AS account_id,
           u.avatar_asset_id AS asset_id, u.avatar_url AS legacy_value
    FROM users u
    UNION ALL
    SELECT 'vehicle', a.user_id, v.account_id, v.image_asset_id, v.image_url
    FROM vehicles v LEFT JOIN accounts a ON a.id = v.account_id
), classified AS (
    SELECT r.purpose, CASE
        WHEN r.user_id IS NULL THEN 'unresolved-ownership'
        WHEN r.asset_id IS NOT NULL AND (m.id IS NULL OR m.user_id <> r.user_id
            OR m.purpose <> r.purpose OR (r.purpose = 'vehicle' AND m.creator_account_id <> r.account_id))
            THEN 'unresolved-ownership'
        WHEN m.state = 'ready' THEN 'managed-ready-metadata'
        WHEN m.id IS NOT NULL THEN 'managed-pending'
        WHEN nullif(btrim(r.legacy_value), '') IS NULL THEN 'missing-reference'
        WHEN r.legacy_value ~* '^https?://' THEN 'legacy-external-url-unverified'
        ELSE 'legacy-unresolved-reference'
    END AS classification
    FROM media_refs r LEFT JOIN media_assets m ON m.id = r.asset_id
)
SELECT purpose, classification, count(*) FROM classified
GROUP BY purpose, classification ORDER BY purpose, classification;
-- Metadata cannot prove remote bytes exist. Verify managed locators through the
-- authorized owning provider adapter; never fetch arbitrary legacy URL strings.
ROLLBACK;
