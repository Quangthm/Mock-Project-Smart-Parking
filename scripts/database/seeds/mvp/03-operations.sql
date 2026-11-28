-- CompleteStructureEdit creates site state and adjusts only already-existing pools.
-- A fresh site needs no quota/pool, hold, operation receipt, or event for this snapshot.
SELECT pg_temp.require(current_database()='smartpark_operations' AND current_user='smartpark_operations','Unexpected OperationsDb identity');
LOCK TABLE structure_site_state IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE e_structure_site_state AS SELECT id AS site_id,tenant AS tenant_id,true AS active FROM site_input;
UPDATE run_context SET established=EXISTS(SELECT FROM structure_site_state p JOIN e_structure_site_state e USING(site_id));
SELECT pg_temp.require(NOT EXISTS(SELECT FROM structure_edit_holds h JOIN site_input s ON s.id=h.site_id)
    AND NOT EXISTS(SELECT FROM site_capacity_pools p JOIN site_input s ON s.id=p.site_id),
    'Existing structure edits or capacity pools require reconciliation');
SELECT pg_temp.sync('structure_site_state','site_id');
SELECT format('Operations persisted: %s active structure site states',count(*))
FROM structure_site_state p JOIN e_structure_site_state e USING(site_id)
WHERE p.active HAVING (SELECT mode='verify' FROM run_context);

