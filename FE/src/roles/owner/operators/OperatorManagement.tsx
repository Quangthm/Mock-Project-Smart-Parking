import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { operatorsApi, parkingApi, type Operator, type Site } from '../../../lib/parkingApi';
import { CreateOperatorForm } from './CreateOperatorForm';
import { SiteForm } from '../parking-lots/SiteForm';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';

export function OperatorManagement({ selectedSiteId, onOperatorsChanged }: { selectedSiteId: string; onOperatorsChanged: () => void }) {
  const { user } = useApp();
  const [operators, setOperators] = useState<Operator[]>([]);
  const [sites, setSites] = useState<Site[]>([]);
  const [showCreate, setShowCreate] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reload, setReload] = useState(0);

  useEffect(() => {
    let current = true;
    setLoading(true); setError(''); setOperators([]); setSites([]); setShowCreate(false); setMessage('');
    Promise.all([operatorsApi.list(), parkingApi.list()])
      .then(([accounts, parkingSites]) => {
        if (!current) return;
        setOperators(accounts);
        setSites(parkingSites.filter(site => site.isActive && site.status === 'ACTIVE'));
      })
      .catch(err => { if (current) setError(err instanceof Error ? err.message : 'Cannot load operators and sites.'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [user?.id, reload]);

  function onCreated(operator: Operator) {
    setOperators(current => [...current, operator].sort((a, b) => a.fullName.localeCompare(b.fullName)));
    setShowCreate(false);
    setMessage(`Account created for ${operator.email}. The operator can sign in with the password you entered. Email delivery is processed separately.`);
    onOperatorsChanged();
  }

  const filterSiteId = sites.some(site => site.id === selectedSiteId) ? selectedSiteId : 'all';
  const visibleOperators = operators.filter(operator => filterSiteId === 'all' || operator.siteIds.includes(filterSiteId));
  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem', gap: '0.5rem' }}>
        <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.05rem', color: 'var(--fg)', margin: 0 }}>Operator Accounts</h3>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button className="btn-outline" disabled={loading || showCreate} onClick={() => setReload(value => value + 1)}>Refresh</button>
          <button className="btn-primary" disabled={loading || !!error || showCreate} onClick={() => setShowCreate(true)}>
            <UntitledIcon name="plus" size={16} /> New Operator
          </button>
        </div>
      </div>
      {loading && <p role="status">Loading operators and sites…</p>}
      {error && <p role="alert" style={{ color: '#dc2626' }}>{error}</p>}
      {message && <p role="status">{message}</p>}
      {!loading && !error && !sites.length && <p>Create your first parking site using New Operator, then add your operator account.</p>}
      {showCreate && (sites.length ? <CreateOperatorForm sites={sites} onCreated={onCreated} onCancel={() => setShowCreate(false)} /> : <SiteForm onSaved={site => { setSites(current => [...current, site]); onOperatorsChanged(); }} onCancel={() => setShowCreate(false)} />)}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
        {visibleOperators.map(op => (
          <div key={op.id} className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem', padding: '0.875rem 1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <div style={{ width: 32, height: 32, background: '#a855f720', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, color: '#a855f7' }}>{op.fullName.charAt(0)}</div>
              <div>
                <div style={{ fontWeight: 600, fontSize: '0.875rem' }}>{op.fullName}</div>
                <div style={{ fontSize: '0.77rem', color: 'var(--muted)' }}>{op.email} · Operation · {op.status}</div>
                <div style={{ fontSize: '0.77rem', color: 'var(--muted)' }}>{op.siteIds.map(id => sites.find(site => site.id === id)?.name ?? id).join(', ')}</div>
                <div style={{ fontSize: '0.77rem', color: 'var(--muted)' }}>Permissions: {op.permissions.join(', ')}</div>
              </div>
            </div>
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <span style={{ color: 'var(--primary)', fontSize: '0.75rem' }}>Operation</span>
              <button className="btn-outline" disabled title="Operator locking is not available yet.">Lock</button>
              <button className="btn-outline" disabled title="Operator removal is not available yet.">Remove</button>
            </div>
          </div>
        ))}
        {!loading && !error && !visibleOperators.length && <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--muted)' }}>No operators yet. Create an account for your staff.</div>}
      </div>
    </div>
  );
}
