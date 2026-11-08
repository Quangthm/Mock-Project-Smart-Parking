-- Manual FacilityDb migration. Apply only after operator review. No pre-existing site is published.
-- Requires Facility parking_sites, tenants and owner_onboarding_provisions; no other service access.
-- Deployment sequence for an authorized operator:
-- 1. Verify the Facility DB identity, prerequisite tables/column types and backup. Apply once
--    with a migration role able to create functions/tables and install the site/tenant triggers.
-- 2. Limit grant-table writes to the Facility publication service. Trigger execution also needs
--    DELETE on grants and UPDATE on the version record for authorized site/tenant writer roles.
--    Grant SELECT on source rows and SELECT/UPDATE row-lock rights to the publication service.
-- 3. Deploy the User publication-evidence route, then Facility with fixed HTTPS Discovery:UserOrigin,
--    environment-managed cursor protection keys and access logs with private query/header redaction.
--    Publish a synthetic explicitly approved initial lot in isolated acceptance, compare empty,
--    published, withdrawn and changed-revision reads. Verify an invalidating profile edit atomically
--    under a concurrent publication and confirm reactivation does not restore withdrawn grants.
-- 4. Confirm denial during the gap between provisioning and finalized approval and for additional
--    lots without verified bindings. Perform the PostgreSQL and multi-host checks before release.
BEGIN;
CREATE TABLE facility_discovery_version (
    singleton boolean PRIMARY KEY CHECK (singleton),
    revision uuid NOT NULL
);
INSERT INTO facility_discovery_version VALUES (true, gen_random_uuid());
CREATE TABLE facility_discovery_publications (
    site_id uuid PRIMARY KEY REFERENCES parking_sites(id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    configuration_version uuid NOT NULL DEFAULT gen_random_uuid(),
    lot_type text NOT NULL CHECK (lot_type IN ('outdoor','basement','multi_storey')),
    categories text[] NOT NULL CHECK (cardinality(categories) BETWEEN 1 AND 2 AND categories <@ ARRAY['CAR','MOTORCYCLE']::text[]),
    window_start timestamptz, window_end timestamptz, window_state text,
    published_at timestamptz NOT NULL DEFAULT statement_timestamp(),
    CHECK ((window_start IS NULL AND window_end IS NULL AND window_state IS NULL) OR
           (window_start IS NOT NULL AND window_end IS NOT NULL AND window_end > window_start AND
            window_state IS NOT NULL AND window_state IN ('open','closed')))
);
CREATE FUNCTION facility_discovery_changed() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    UPDATE facility_discovery_version SET revision=gen_random_uuid() WHERE singleton;
    RETURN NULL;
END $$;
CREATE TRIGGER discovery_membership_changed AFTER INSERT OR DELETE OR UPDATE ON facility_discovery_publications
    FOR EACH ROW EXECUTE FUNCTION facility_discovery_changed();
CREATE FUNCTION facility_discovery_forget_site() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM facility_discovery_publications WHERE site_id=OLD.id;
    RETURN NULL;
END $$;
CREATE TRIGGER discovery_public_site_edited AFTER UPDATE OF tenant_id,name,address,latitude,longitude,status,deleted_at ON parking_sites
    FOR EACH ROW WHEN ((OLD.tenant_id,OLD.name,OLD.address,OLD.latitude,OLD.longitude,OLD.status,OLD.deleted_at)
        IS DISTINCT FROM (NEW.tenant_id,NEW.name,NEW.address,NEW.latitude,NEW.longitude,NEW.status,NEW.deleted_at))
    EXECUTE FUNCTION facility_discovery_forget_site();
CREATE FUNCTION facility_discovery_forget_tenant() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    DELETE FROM facility_discovery_publications WHERE tenant_id=OLD.id;
    RETURN NULL;
END $$;
CREATE TRIGGER discovery_tenant_changed AFTER UPDATE OF status,deleted_at ON tenants
    FOR EACH ROW WHEN ((OLD.status, OLD.deleted_at) IS DISTINCT FROM (NEW.status, NEW.deleted_at))
    EXECUTE FUNCTION facility_discovery_forget_tenant();
COMMIT;
-- Rollback: disable new consumers and writers first. Removing these triggers/tables loses all
-- grants and invalidates all cursors. Never backfill public membership from site ACTIVE status.
