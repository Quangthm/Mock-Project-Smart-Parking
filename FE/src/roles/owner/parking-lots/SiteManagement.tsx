import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { parkingApi, operatorsApi, type Site, type Operator } from '../../../lib/parkingApi';
import { SiteForm } from './SiteForm';

export function SiteManagement({ onSiteCreated }: { onSiteCreated: () => void }) {
  const { user } = useApp();
  const [sites, setSites] = useState<Site[]>([]);
  const [operators, setOperators] = useState<Operator[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const [editingSite, setEditingSite] = useState<Site>();
  const [showForm, setShowForm] = useState(false);
  useEffect(() => {
    let current = true;
    setLoading(true); setError(''); setSites([]); setOperators([]); setShowForm(false);
    Promise.all([parkingApi.list(), operatorsApi.list()])
      .then(([sites, operators]) => { if (current) { setSites(sites); setOperators(operators); } })
      .catch(err => { if (current) setError(err instanceof Error ? err.message : 'Cannot load sites.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, reload]);
  function saved(site: Site) {
    setSites(current => [...current.filter(value => value.id !== site.id), site].sort((a, b) => a.code.localeCompare(b.code)));
    setShowForm(false); onSiteCreated();
  }
  return <section className="space-y-5">
    <header className="flex flex-wrap items-end justify-between gap-3">
      <div><h1 className="text-2xl font-bold text-[var(--fg)]">Site Management</h1><p className="mt-2 text-sm text-[var(--muted)]">Manage parking branches and assigned employees.</p></div>
      <div className="flex gap-2"><button className="btn-outline" disabled={loading || showForm} onClick={() => setReload(value => value + 1)}>Refresh</button><button className="btn-primary" disabled={loading || !!error || showForm} onClick={() => { setEditingSite(undefined); setShowForm(true); }}>Add New Site</button></div>
    </header>
    {loading && <p role="status">Loading sites…</p>}
    {error && <p role="alert" className="text-red-600">{error}</p>}
    {showForm && <SiteForm key={editingSite?.id ?? 'new'} site={editingSite} onSaved={saved} onCancel={() => setShowForm(false)} />}
    <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead className="border-b border-[var(--border)] text-xs uppercase tracking-wide text-[var(--muted)]"><tr><th className="px-4 py-3">Site</th><th className="px-4 py-3">Address</th><th className="px-4 py-3">Capacity</th><th className="px-4 py-3">Employees</th><th className="px-4 py-3">Actions</th></tr></thead>
        <tbody>{sites.map(site => <tr key={site.id}>
          <td className="px-4 py-4 font-semibold">{site.code} · {site.name}<div className="text-xs text-[var(--muted)]">{site.status}</div></td>
          <td className="px-4 py-4">{site.address}</td><td className="px-4 py-4">{site.totalPhysicalCapacity} slots</td>
          <td className="px-4 py-4">{operators.filter(operator => operator.siteIds.includes(site.id)).length}</td>
          <td className="px-4 py-4"><button className="btn-outline" disabled={showForm} onClick={() => { setEditingSite(site); setShowForm(true); }}>Edit</button></td>
        </tr>)}{!loading && !error && !sites.length && <tr><td colSpan={5} className="px-4 py-10 text-center text-[var(--muted)]">No sites found. Add your first parking site.</td></tr>}</tbody>
      </table>
    </div>
  </section>;
}
