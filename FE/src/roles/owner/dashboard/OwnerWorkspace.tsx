import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';
import { operatorsApi, parkingApi, type Operator, type Site, type Structure } from '../../../lib/parkingApi';

const permissions = ['DEVICE_MANAGE', 'DEVICE_STATUS_VIEW', 'CASH_COLLECT', 'APPEAL_REVIEW'];
export function OwnerWorkspace() {
  const { user } = useApp();
  const [tab, setTab] = useState('sites');
  const [sites, setSites] = useState<Site[]>([]);
  const [operators, setOperators] = useState<Operator[]>([]);
  const [selected, setSelected] = useState('');
  const [structure, setStructure] = useState<Structure | null>(null);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [layoutLoading, setLayoutLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [layoutError, setLayoutError] = useState('');
  const [notice, setNotice] = useState('');
  const [siteForm, setSiteForm] = useState({ code: '', name: '', address: '' });
  const [unitForm, setUnitForm] = useState({ name: '', type: 'ZONE', parentId: '', capacity: 0 });
  const [slotForm, setSlotForm] = useState({ unitId: '', code: '', vehicleType: 'CAR', type: 'STANDARD' });
  const [operatorForm, setOperatorForm] = useState({ fullName: '', email: '', password: '', siteIds: [] as string[], permissions: [] as string[] });

  useEffect(() => {
    let current = true;
    setLoading(true); setError('');
    Promise.all([parkingApi.list(), operatorsApi.list()]).then(([lots, staff]) => {
      if (!current) return;
      setSites(lots); setOperators(staff);
      setSelected(value => lots.some(lot => lot.id === value) ? value : lots[0]?.id ?? '');
    }).catch(err => { if (current) { setSites([]); setOperators([]); setError(err instanceof Error ? err.message : 'Cannot load workspace.'); } })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, reload]);

  useEffect(() => {
    let current = true;
    setStructure(null); setLayoutError(''); setLayoutLoading(false); setSlotForm(value => ({ ...value, unitId: '' }));
    setUnitForm(value => ({ ...value, parentId: '' }));
    if (!selected) return;
    setLayoutLoading(true);
    parkingApi.structure(selected).then(result => { if (current) { setStructure(result); setSlotForm(value => ({ ...value, unitId: result.units[0]?.id ?? '' })); } })
      .catch(err => { if (current) setLayoutError(err instanceof Error ? err.message : 'Cannot load structure.'); })
      .finally(() => { if (current) setLayoutLoading(false); });
    return () => { current = false; };
  }, [selected, reload, user?.id]);

  async function mutate(action: () => Promise<unknown>, message: string) {
    if (saving) return;
    setSaving(true); setError(''); setNotice('');
    try { await action(); setNotice(message); setReload(value => value + 1); }
    catch (err) { setError(err instanceof Error ? err.message : 'Request failed.'); }
    finally { setSaving(false); }
  }
  const busy = saving || loading;
  const field = (label: string, input: React.ReactNode) => <label className="label" style={{ display: 'block', marginBottom: '0.75rem' }}>{label}{input}</label>;
  return <DashboardSidebar groups={[{ label: 'Parking management', items: [
    { id: 'sites', label: 'Sites', icon: '⌖', active: tab === 'sites', onClick: () => { if (!saving) setTab('sites'); } },
    { id: 'structure', label: 'Parking structure', icon: '▦', active: tab === 'structure', onClick: () => { if (!saving) setTab('structure'); } },
    { id: 'operators', label: 'Operators', icon: '♙', active: tab === 'operators', onClick: () => { if (!saving) setTab('operators'); } },
  ] }]}>
    <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
      <h1 style={{ fontFamily: 'Outfit', color: 'var(--fg)' }}>Owner workspace</h1>
      <button className="btn-outline" disabled={busy} onClick={() => setReload(value => value + 1)}>Refresh</button>
      {loading && <p role="status">Loading sites and staff…</p>}
      {error && <p role="alert" style={{ color: '#dc2626' }}>{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {tab === 'sites' && <>
        <h2>Parking sites</h2>
        {sites.map(site => <div className="card" key={site.id} style={{ marginBottom: '1rem' }}>
          <strong>{site.name}</strong><p>{site.code} · {site.address}</p><p>{site.status} · {site.totalPhysicalCapacity} physical slots</p>
          <form key={`${site.id}-${reload}`} onSubmit={event => {
            event.preventDefault(); const values = new FormData(event.currentTarget);
            void mutate(() => parkingApi.edit(site.id, { action: 'profile', code: values.get('code'), name: values.get('name'), address: values.get('address'), latitude: site.latitude, longitude: site.longitude }), 'Site profile updated.');
          }}>
            {field('Site code', <input className="input" name="code" required maxLength={50} disabled={busy} defaultValue={site.code} />)}
            {field('Name', <input className="input" name="name" required maxLength={255} disabled={busy} defaultValue={site.name} />)}
            {field('Address', <input className="input" name="address" required maxLength={2000} disabled={busy} defaultValue={site.address} />)}
            <button className="btn-primary" disabled={busy}>Save profile</button>
          </form>
          <button className="btn-outline" disabled={busy} onClick={() => { setSelected(site.id); setTab('structure'); }}>Manage structure</button>
        </div>)}
        {!loading && !error && !sites.length && <p>Create your first parking site below.</p>}
        <form className="card" onSubmit={event => { event.preventDefault(); void mutate(async () => { const site = await parkingApi.create(siteForm); setSelected(site.id); setSiteForm({ code: '', name: '', address: '' }); }, 'Parking site created. Add its units and slots in Parking structure.'); }}>
          <h3>Create site</h3>
          {field('Site code', <input className="input" required maxLength={50} disabled={busy} value={siteForm.code} onChange={e => setSiteForm({ ...siteForm, code: e.target.value })} />)}
          {field('Name', <input className="input" required maxLength={255} disabled={busy} value={siteForm.name} onChange={e => setSiteForm({ ...siteForm, name: e.target.value })} />)}
          {field('Address', <input className="input" required maxLength={2000} disabled={busy} value={siteForm.address} onChange={e => setSiteForm({ ...siteForm, address: e.target.value })} />)}
          <button className="btn-primary" disabled={busy}>Create site</button>
        </form>
      </>}
      {tab === 'structure' && <>
        <h2>Parking structure</h2>
        {field('Site', <select className="input" disabled={busy} value={selected} onChange={e => setSelected(e.target.value)}><option value="">Select site</option>{sites.map(site => <option value={site.id} key={site.id}>{site.name}</option>)}</select>)}
        {layoutLoading && <p role="status">Loading structure…</p>}
        {layoutError && <p role="alert">{layoutError}</p>}
        {structure && <>
          <p>{structure.site.name} · {structure.site.status} · {structure.slots.length} physical slots</p>
          <button className="btn-outline" disabled={busy || layoutLoading} onClick={() => void mutate(() => parkingApi.edit(selected, { action: 'active', active: !structure.site.isActive }), 'Site status updated.')}>{structure.site.isActive ? 'Deactivate site' : 'Activate site'}</button>
          <form className="card" style={{ marginTop: '1rem' }} onSubmit={event => { event.preventDefault(); void mutate(() => parkingApi.unit(selected, { ...unitForm, parentId: unitForm.parentId || null }), 'Spatial unit added.'); }}>
            <h3>Add floor, zone or block</h3>
            {field('Name', <input className="input" required maxLength={100} disabled={busy} value={unitForm.name} onChange={e => setUnitForm({ ...unitForm, name: e.target.value })} />)}
            {field('Type', <select className="input" disabled={busy} value={unitForm.type} onChange={e => setUnitForm({ ...unitForm, type: e.target.value })}>{['FLOOR','ZONE','BLOCK'].map(type => <option key={type}>{type}</option>)}</select>)}
            {field('Parent', <select className="input" disabled={busy} value={unitForm.parentId} onChange={e => setUnitForm({ ...unitForm, parentId: e.target.value })}><option value="">No parent (outdoor zones need no floor)</option>{structure.units.map(unit => <option key={unit.id} value={unit.id}>{unit.name} · {unit.type}</option>)}</select>)}
            {field('Maximum physical slots (0 = unlimited)', <input className="input" type="number" min={0} required disabled={busy} value={unitForm.capacity} onChange={e => setUnitForm({ ...unitForm, capacity: Number(e.target.value) })} />)}
            <button className="btn-primary" disabled={busy}>Add unit</button>
          </form>
          {structure.units.map(unit => <div className="card" key={unit.id} style={{ marginTop: '0.75rem' }}><strong>{unit.name} · {unit.type}</strong><p>Capacity: {unit.maxCapacity || 'Unlimited'}</p><button className="btn-outline" disabled={busy} onClick={() => void mutate(() => parkingApi.edit(selected, { action: 'removeUnit', resourceId: unit.id }), 'Unit removed.')}>Remove empty unit</button></div>)}
          <form className="card" style={{ marginTop: '1rem' }} onSubmit={event => { event.preventDefault(); void mutate(() => parkingApi.slot(selected, slotForm), 'Slot added.'); }}>
            <h3>Add physical slot</h3>
            {field('Unit', <select className="input" required disabled={busy} value={slotForm.unitId} onChange={e => setSlotForm({ ...slotForm, unitId: e.target.value })}><option value="">Select unit</option>{structure.units.map(unit => <option key={unit.id} value={unit.id}>{unit.name}</option>)}</select>)}
            {field('Slot code (unique within site)', <input className="input" required maxLength={50} disabled={busy} value={slotForm.code} onChange={e => setSlotForm({ ...slotForm, code: e.target.value })} />)}
            {field('Vehicle type', <select className="input" disabled={busy} value={slotForm.vehicleType} onChange={e => setSlotForm({ ...slotForm, vehicleType: e.target.value })}>{['CAR','MOTORCYCLE','OVERSIZED'].map(type => <option key={type}>{type}</option>)}</select>)}
            {field('Slot type', <select className="input" disabled={busy} value={slotForm.type} onChange={e => setSlotForm({ ...slotForm, type: e.target.value })}>{['STANDARD','VIP','EV','DISABLED'].map(type => <option key={type}>{type}</option>)}</select>)}
            <button className="btn-primary" disabled={busy || !structure.units.length}>Add slot</button>
          </form>
          {structure.slots.map(slot => <div className="card" key={slot.id} style={{ marginTop: '0.75rem' }}><strong>{slot.code} · {slot.vehicleType} · {slot.type}</strong><p>{slot.isPhysicallyOccupied ? 'OCCUPIED' : slot.operationalStatus === 'OPERATIONAL' ? 'AVAILABLE' : slot.operationalStatus}{slot.reservationState ? ` · ${slot.reservationState}` : ''}</p><button className="btn-outline" disabled={busy} onClick={() => void mutate(() => parkingApi.edit(selected, { action: 'removeSlot', resourceId: slot.id }), 'Slot removed.')}>Remove slot</button></div>)}
        </>}
      </>}
      {tab === 'operators' && <>
        <h2>Operators</h2>
        {operators.map(operator => <div className="card" key={operator.id} style={{ marginBottom: '1rem' }}><strong>{operator.fullName}</strong><p>{operator.email} · {operator.status}</p><p>Sites: {operator.siteIds.map(id => sites.find(site => site.id === id)?.name ?? id).join(', ')}</p><p>{operator.permissions.join(', ')}</p></div>)}
        <form className="card" onSubmit={event => { event.preventDefault(); void mutate(async () => { await operatorsApi.create(operatorForm); setOperatorForm({ fullName: '', email: '', password: '', siteIds: [], permissions: [] }); }, 'Operator created. The account can sign in with the initial password.'); }}>
          <h3>Create operator</h3>
          {field('Full name', <input className="input" required maxLength={255} disabled={busy} value={operatorForm.fullName} onChange={e => setOperatorForm({ ...operatorForm, fullName: e.target.value })} />)}
          {field('Email', <input className="input" type="email" required maxLength={255} disabled={busy} value={operatorForm.email} onChange={e => setOperatorForm({ ...operatorForm, email: e.target.value })} />)}
          {field('Initial password (8–15 characters; upper/lowercase, digit, special character)', <input className="input" type="password" autoComplete="new-password" required minLength={8} maxLength={15} disabled={busy} value={operatorForm.password} onChange={e => setOperatorForm({ ...operatorForm, password: e.target.value })} />)}
          <fieldset disabled={busy}><legend>Active sites</legend>{sites.filter(site => site.isActive).map(site => <label key={site.id} style={{ display: 'block' }}><input type="checkbox" checked={operatorForm.siteIds.includes(site.id)} onChange={e => setOperatorForm({ ...operatorForm, siteIds: e.target.checked ? [...operatorForm.siteIds, site.id] : operatorForm.siteIds.filter(id => id !== site.id) })} /> {site.name}</label>)}</fieldset>
          <fieldset disabled={busy}><legend>Permissions for each selected site</legend>{permissions.map(permission => <label key={permission} style={{ display: 'block' }}><input type="checkbox" checked={operatorForm.permissions.includes(permission)} onChange={e => setOperatorForm({ ...operatorForm, permissions: e.target.checked ? [...operatorForm.permissions, permission] : operatorForm.permissions.filter(p => p !== permission) })} /> {permission}</label>)}</fieldset>
          <button className="btn-primary" style={{ marginTop: '1rem' }} disabled={busy || !operatorForm.siteIds.length || !operatorForm.permissions.length}>Create operator</button>
        </form>
      </>}
    </main>
  </DashboardSidebar>;
}
