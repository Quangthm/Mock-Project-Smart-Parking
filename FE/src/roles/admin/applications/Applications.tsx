import { useEffect, useState } from 'react';
import { ownerApi, type OwnerApplicationRecord } from '../../../lib/ownerApi';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';

export function Applications() {
  const [apps, setApps] = useState<OwnerApplicationRecord[]>([]);
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    ownerApi.list().then(data => { if (active) setApps(data); })
      .catch(e => { if (active) setError(e instanceof Error ? e.message : 'Cannot load applications.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reload]);

  async function review(app: OwnerApplicationRecord, status: 'approved' | 'rejected') {
    if (busy) return;
    setBusy(app.id);
    setError('');
    try {
      const updated = await ownerApi.review(app.id, status, notes[app.id] ?? '');
      setApps(current => current.map(a => a.id === updated.id ? updated : a));
      setNotes(current => ({ ...current, [app.id]: '' }));
    } catch (e) { setError(e instanceof Error ? e.message : 'Review failed. Refresh and try again.'); }
    finally { setBusy(null); }
  }

  const pending = apps.filter(a => a.status === 'pending');
  const reviewed = apps.filter(a => a.status !== 'pending');

  return (
    <div>
      <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.3rem', marginBottom: '1.5rem', color: 'var(--fg)' }}>Owner Partnership Applications</h2>
      <button className="btn-secondary" disabled={loading || !!busy} onClick={() => setReload(n => n + 1)}>Refresh applications</button>
      {loading && <p role="status">Loading applications...</p>}
      {error && <p role="alert" style={{ color: '#dc2626' }}>{error}</p>}
      {!loading && !error && apps.length === 0 && <p>No applications yet.</p>}
      {pending.length > 0 && (
        <div style={{ marginBottom: '2rem' }}>
          <h3 style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.9rem', fontWeight: 600, color: '#f59e0b', marginBottom: '0.875rem' }}><UntitledIcon name="clock" size={16} /> Pending Review ({pending.length})</h3>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
            {pending.map(app => (
              <div key={app.id} className="card" style={{ border: '1.5px solid #fde68a' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.75rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)' }}>{app.businessName}</div>
                    <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>{app.ownerName} · {new Date(app.submittedAt).toLocaleDateString('en-GB')}</div>
                    <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>{app.email} - {app.phone}</div>
                    <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>Type: {app.lotType.replace('-', ' ')}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: '0.625rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                  <div style={{ flex: 1, minWidth: '200px' }}>
                    <label className="label">Review Note (optional)</label>
                    <input className="input" value={notes[app.id] ?? ''} maxLength={2000} onChange={e => setNotes(current => ({ ...current, [app.id]: e.target.value }))} placeholder="Add a note..." />
                  </div>
                  <button disabled={!!busy || loading} onClick={() => review(app, 'approved')} style={{ padding: '0.5rem 1rem', background: '#22c55e', color: '#fff', border: 'none', borderRadius: 'var(--radius)', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem' }}>
                    <><UntitledIcon name="check" size={16} /> Approve</>
                  </button>
                  <button disabled={!!busy || loading} onClick={() => review(app, 'rejected')} style={{ padding: '0.5rem 1rem', background: '#fee2e2', color: '#dc2626', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem' }}>
                    <><UntitledIcon name="x" size={16} /> Reject</>
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
      <div>
        <h3 style={{ fontSize: '0.9rem', fontWeight: 600, color: 'var(--muted)', marginBottom: '0.875rem' }}>Previously Reviewed</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          {reviewed.map(app => (
            <div key={app.id} className="card" style={{ padding: '0.75rem 1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <div style={{ fontWeight: 600, fontSize: '0.875rem', color: 'var(--fg)' }}>{app.businessName}</div>
                <div style={{ fontSize: '0.78rem', color: 'var(--muted)' }}>{app.ownerName} · {app.lotType}</div>
                {app.reviewNote && <div style={{ fontSize: '0.78rem', color: 'var(--muted)' }}>{app.reviewNote}</div>}
              </div>
              <span style={{ padding: '0.2rem 0.625rem', borderRadius: '999px', fontSize: '0.75rem', fontWeight: 700, background: app.status === 'approved' ? '#22c55e20' : '#ef444420', color: app.status === 'approved' ? '#22c55e' : '#ef4444' }}>
                {app.status.toUpperCase()}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
