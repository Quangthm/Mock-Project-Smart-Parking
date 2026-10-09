import { useEffect, useState, type FormEvent } from 'react';
import { parkingApi, type Site, type Structure } from '../../../lib/parkingApi';

export function ParkingStructure({ sites = [], selectedSiteId = 'all', onChanged }: { sites?: Site[]; selectedSiteId?: string; onChanged?: () => void }) {
  const [selection, setSelection] = useState(selectedSiteId === 'all' ? '' : selectedSiteId);
  useEffect(() => { setSelection(selectedSiteId === 'all' ? '' : selectedSiteId); }, [selectedSiteId]);
  const activeId = sites.some(site => site.id === selection) ? selection : sites[0]?.id ?? '';
  return <section className="space-y-5">
    <header className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-bold">Parking Structure</h1><p className="text-sm text-[var(--muted)]">Configure the physical layout of your parking sites.</p></div>
      <label className="label">Site<select className="input" value={activeId} disabled={!sites.length} onChange={event => setSelection(event.target.value)}>{sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</select></label>
    </header>
    {!activeId ? <p>No sites found. Create a site in Sites first.</p> : <StructureEditor key={activeId} siteId={activeId} onChanged={onChanged} />}
  </section>;
}

function StructureEditor({ siteId, onChanged }: { siteId: string; onChanged?: () => void }) {
  const [structure, setStructure] = useState<Structure | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reload, setReload] = useState(0);
  const [unit, setUnit] = useState({ type: 'FLOOR', name: '', parentId: '', capacity: '0' });
  const [slot, setSlot] = useState({ unitId: '', code: '', vehicleType: 'CAR', type: 'STANDARD' });
  useEffect(() => {
    let current = true;
    setLoading(true); setError('');
    parkingApi.structure(siteId).then(result => { if (current) setStructure(result); })
      .catch(err => { if (current) setError(err instanceof Error ? err.message : 'Cannot load structure.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [siteId, reload]);
  async function mutate(action: () => Promise<unknown>, clear?: () => void) {
    if (busy) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await action(); clear?.(); setMessage('Changes saved.'); onChanged?.();
      try { setStructure(await parkingApi.structure(siteId)); }
      catch { setStructure(null); setError('Changes were saved, but the updated layout could not be loaded. Refresh before making further changes.'); }
    } catch (err) { setError(err instanceof Error ? err.message : 'Cannot save structure.'); }
    finally { setBusy(false); }
  }
  async function addUnit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const capacity = Number(unit.capacity);
    if (!unit.name.trim() || !Number.isInteger(capacity) || capacity < 0) { setError('Enter a unit name and a non-negative integer capacity.'); return; }
    await mutate(() => parkingApi.unit(siteId, { type: unit.type, name: unit.name.trim(), parentId: unit.parentId || null, capacity }), () => setUnit(current => ({ ...current, name: '' })));
  }
  async function addSlot(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!slot.code.trim() || !structure?.units.some(unit => unit.id === slot.unitId)) { setError('Select a unit and enter a slot code.'); return; }
    await mutate(() => parkingApi.slot(siteId, { ...slot, code: slot.code.trim() }), () => setSlot(current => ({ ...current, code: '' })));
  }
  return <div className="space-y-4">
    <button className="btn-outline" disabled={loading || busy} onClick={() => setReload(value => value + 1)}>Refresh</button>
    {loading && <p role="status">Loading structure…</p>}
    {error && <p role="alert" className="text-red-600">{error}</p>}
    {message && <p role="status">{message}</p>}
    {!loading && structure && <fieldset disabled={busy} style={{ border: 0, padding: 0, margin: 0 }} className="space-y-5">
      <div className="card"><h2>{structure.site.name}</h2><p>{structure.site.address}</p><p>{structure.units.length} units · {structure.slots.length} slots · {structure.site.status}</p></div>
      <form className="card space-y-3" onSubmit={addUnit}>
        <h3>Add floor / zone / block</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="label">Type<select className="input" value={unit.type} onChange={e => setUnit(current => ({ ...current, type: e.target.value }))}><option>FLOOR</option><option>ZONE</option><option>BLOCK</option></select></label>
          <label className="label">Name<input className="input" maxLength={100} value={unit.name} onChange={e => setUnit(current => ({ ...current, name: e.target.value }))} required /></label>
          <label className="label">Parent<select className="input" value={unit.parentId} onChange={e => setUnit(current => ({ ...current, parentId: e.target.value }))}><option value="">Site root</option>{structure.units.map(value => <option key={value.id} value={value.id}>{value.name}</option>)}</select></label>
          <label className="label">Capacity<input className="input" type="number" min={0} step={1} value={unit.capacity} onChange={e => setUnit(current => ({ ...current, capacity: e.target.value }))} required /></label>
        </div><button className="btn-primary" type="submit">{busy ? 'Saving…' : 'Add Unit'}</button>
      </form>
      <div className="card space-y-3"><h3>Units</h3>{!structure.units.length && <p>No units yet. Add your first floor or zone.</p>}{structure.units.map(value => <div key={value.id} className="flex flex-wrap items-center justify-between gap-3">
        <span>{value.type} · {value.name} · Capacity {value.maxCapacity}</span>
        <button className="btn-outline" type="button" onClick={() => { if (confirm(`Remove ${value.name}?`)) void mutate(() => parkingApi.edit(siteId, { action: 'removeUnit', resourceId: value.id }), () => { setUnit(current => ({ ...current, parentId: '' })); setSlot(current => ({ ...current, unitId: '' })); }); }}>Remove</button>
      </div>)}</div>
      <form className="card space-y-3" onSubmit={addSlot}>
        <h3>Add parking slot</h3><div className="grid gap-3 sm:grid-cols-2">
          <label className="label">Unit<select className="input" value={slot.unitId} onChange={e => setSlot(current => ({ ...current, unitId: e.target.value }))} required><option value="" disabled>Select a unit</option>{structure.units.map(value => <option key={value.id} value={value.id}>{value.name}</option>)}</select></label>
          <label className="label">Slot code<input className="input" maxLength={50} value={slot.code} onChange={e => setSlot(current => ({ ...current, code: e.target.value }))} required /></label>
          <label className="label">Vehicle<select className="input" value={slot.vehicleType} onChange={e => setSlot(current => ({ ...current, vehicleType: e.target.value }))}><option>CAR</option><option>MOTORCYCLE</option><option>OVERSIZED</option></select></label>
          <label className="label">Type<select className="input" value={slot.type} onChange={e => setSlot(current => ({ ...current, type: e.target.value }))}><option>STANDARD</option><option>VIP</option><option>EV</option><option>DISABLED</option></select></label>
        </div><button className="btn-primary" type="submit" disabled={!structure.units.length}>{busy ? 'Saving…' : 'Add Slot'}</button>
      </form>
      <div className="card space-y-3"><h3>Parking slots</h3>{!structure.slots.length && <p>No slots yet.</p>}{structure.slots.map(value => <div key={value.id} className="flex flex-wrap items-center justify-between gap-3">
        <span>{value.code} · {structure.units.find(unit => unit.id === value.unitId)?.name ?? value.unitId} · {value.vehicleType} · {value.type} · {value.operationalStatus} · {value.isPhysicallyOccupied ? 'Occupied' : 'Unoccupied'}</span>
        <button className="btn-outline" type="button" onClick={() => { if (confirm(`Remove slot ${value.code}?`)) void mutate(() => parkingApi.edit(siteId, { action: 'removeSlot', resourceId: value.id })); }}>Remove</button>
      </div>)}</div>
    </fieldset>}
  </div>;
}
