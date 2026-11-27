namespace SmartParking.FacilityService.Persistence;

// Both reads supply a non-deleted parking_slots row named s and bind @start and @end.
// A null end checks restrictions at the instant. Booking checks always bind both ends.
internal static class SlotEligibilitySql
{
    public const string Decision = """
        CROSS JOIN LATERAL (
            WITH RECURSIVE ancestors AS (
                SELECT u.id,u.parent_id,u.site_id,u.tenant_id,u.deleted_at
                FROM spatial_units u WHERE u.id=s.spatial_unit_id
                UNION ALL
                SELECT u.id,u.parent_id,u.site_id,u.tenant_id,u.deleted_at
                FROM spatial_units u JOIN ancestors a ON u.id=a.parent_id
            ) CYCLE id SET cyclic USING ancestry,
            restrictions AS (
                SELECT w.* FROM structure_maintenance_windows w
                WHERE w.site_id=s.site_id AND w.cancelled_at IS NULL
                    AND CASE WHEN @end IS NULL THEN w.service_window @> @start
                        ELSE w.service_window && tstzrange(@start,@end,'[)') END
                    AND (w.slot_id=s.id OR w.slot_id IS NULL AND
                        (w.unit_id IS NULL OR EXISTS (SELECT 1 FROM ancestors a WHERE a.id=w.unit_id)))
            ), compatibility AS (
                SELECT COALESCE(bool_or(c.vehicle_type='CAR'), s.supported_vehicle_type='CAR') AS car,
                    bool_or(c.site_id<>s.site_id OR c.tenant_id<>s.tenant_id) AS wrong_scope
                FROM slot_compatibilities c WHERE c.slot_id=s.id
            )
            SELECT compatibility.car, CASE
                WHEN p.id IS NULL OR t.id IS NULL OR s.site_id='00000000-0000-0000-0000-000000000000'
                    OR s.tenant_id='00000000-0000-0000-0000-000000000000'
                    OR NOT EXISTS (SELECT 1 FROM ancestors WHERE parent_id IS NULL)
                    OR EXISTS (SELECT 1 FROM ancestors WHERE cyclic OR site_id<>s.site_id OR tenant_id<>s.tenant_id)
                    OR compatibility.wrong_scope THEN 'DEPENDENCY_UNAVAILABLE'
                WHEN p.deleted_at IS NOT NULL OR t.deleted_at IS NOT NULL
                    OR p.status<>'ACTIVE' OR t.status<>'ACTIVE'
                    OR EXISTS (SELECT 1 FROM ancestors WHERE deleted_at IS NOT NULL)
                    OR s.physical_state IN ('MAINTENANCE','UNAVAILABLE') OR s.reservation_state='BACKUP'
                    OR NOT COALESCE(compatibility.car,false) THEN 'INELIGIBLE'
                WHEN EXISTS (SELECT 1 FROM restrictions w WHERE w.tenant_id=s.tenant_id
                    AND w.unit_id IS NULL AND w.slot_id IS NULL AND w.enforcement_state='acknowledged'
                    AND lower_inc(w.service_window) AND NOT upper_inc(w.service_window) AND NOT upper_inf(w.service_window))
                    THEN 'INELIGIBLE'
                WHEN s.physical_state='UNKNOWN' OR EXISTS (SELECT 1 FROM restrictions)
                    THEN 'DEPENDENCY_UNAVAILABLE'
                ELSE 'ELIGIBLE'
            END AS outcome
            FROM compatibility
            LEFT JOIN parking_sites p ON p.id=s.site_id AND p.tenant_id=s.tenant_id
            LEFT JOIN tenants t ON t.id=s.tenant_id
        ) eligibility
        """;
}
