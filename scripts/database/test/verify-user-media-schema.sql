-- Read-only post-upgrade check. Run after the canonical UserDb media section.
BEGIN READ ONLY;
DO $$ BEGIN
    IF to_regclass('public.media_assets') IS NULL OR to_regclass('public.media_holds') IS NULL
        OR to_regclass('public.uq_media_upload_key') IS NULL OR to_regclass('public.uq_media_provider_locator') IS NULL
        OR to_regclass('public.idx_media_pending') IS NULL OR to_regclass('public.idx_media_due') IS NULL THEN
        RAISE EXCEPTION 'User media schema missing; apply the reviewed media section of microservices/01-user-access-service-db.sql';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='users'::regclass AND conname='fk_users_avatar_asset')
        OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='vehicles'::regclass AND conname='fk_vehicles_image_asset')
        OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='media_assets'::regclass AND conname='fk_media_creator') THEN
        RAISE EXCEPTION 'User media foreign keys missing; reconcile reviewed migration';
    END IF;
END $$;
ROLLBACK;
