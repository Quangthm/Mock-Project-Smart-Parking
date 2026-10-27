-- ======================================================================================
-- MICROSERVICE: PARKING OPERATIONS SERVICE DATABASE (OperationsDb) - PART 03a
-- Domain: Structure backup policies, Operator incident events
-- Mounted into smartpark_parking DB
-- ======================================================================================
-- DEVIATION (T4-approved): BE column names (P2)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 3. Báº¢NG Háº N NGáº CH Dá»° PHĂ’NG (STRUCTURE BACKUP POLICIES)
-- Tiáº¿p nháº­n tá»« FacilityDb: Quáº£n lĂ½ sá»‘ lÆ°á»£ng slot/capacity giá»¯ lĂ m backup dá»± phĂ²ng
CREATE TABLE structure_backup_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    unit_id UUID NULL,
    vehicle_type VARCHAR(50) NOT NULL CHECK (vehicle_type IN ('CAR', 'MOTORCYCLE', 'OVERSIZED')),
    count INT NOT NULL CHECK (count >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX uq_backup_site_policy ON structure_backup_policies(site_id, vehicle_type) WHERE unit_id IS NULL;
CREATE UNIQUE INDEX uq_backup_unit_policy ON structure_backup_policies(site_id, unit_id, vehicle_type) WHERE unit_id IS NOT NULL;

CREATE TABLE operator_incident_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    site_id UUID NOT NULL,
    actor_id UUID NOT NULL,
    incident_type VARCHAR(80) NOT NULL,
    details TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'reported' CHECK (status IN ('reported', 'resolved')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_operator_incident_events_site ON operator_incident_events(site_id, created_at DESC);
