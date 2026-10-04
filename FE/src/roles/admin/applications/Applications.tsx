import { useEffect, useState } from 'react';
import { adminData as store } from '../data/data';
import type { OwnerApplication } from '../../../lib/types';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';

export function Applications() {
  const [apps, setApps] = useState<OwnerApplication[]>(store.getApplications);
  const [note, setNote] = useState('');

  useEffect(() => {
    const refresh = () => setApps(store.getApplications());
    window.addEventListener('sp-data-change', refresh);
    window.addEventListener('storage', refresh);
    return () => { window.removeEventListener('sp-data-change', refresh); window.removeEventListener('storage', refresh); };
  }, []);

  function review(app: OwnerApplication, status: 'approved' | 'rejected') {
    const updated = { ...app, status, reviewedAt: new Date().toISOString(), reviewNote: note };
    store.saveApplication(updated);
    if (status === 'approved') {
      const owner = store.findUserById(app.ownerId);
      if (owner) store.saveUser({ ...owner, policyAccepted: true });
    }
    store.addAuditLog({ userId: 'admin-001', userName: 'System Admin', userRole: 'admin', action: `APPLICATION_${status.toUpperCase()}`, details: `Owner application ${app.id} ${status}` });
    setApps(store.getApplications());
    setNote('');
  }

  const pending = apps.filter(a => a.status === 'pending');
  const reviewed = apps.filter(a => a.status !== 'pending');

  return (
    <div>
      <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.3rem', marginBottom: '1.5rem', color: 'var(--fg)' }}>Owner Partnership Applications</h2>
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
                    <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>Type: {app.lotType.replace('-', ' ')}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: '0.625rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                  <div style={{ flex: 1, minWidth: '200px' }}>
                    <label className="label">Review Note (optional)</label>
                    <input className="input" value={note} onChange={e => setNote(e.target.value)} placeholder="Add a note..." />
                  </div>
                  <button onClick={() => review(app, 'approved')} style={{ padding: '0.5rem 1rem', background: '#22c55e', color: '#fff', border: 'none', borderRadius: 'var(--radius)', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem' }}>
                    <><UntitledIcon name="check" size={16} /> Approve</>
                  </button>
                  <button onClick={() => review(app, 'rejected')} style={{ padding: '0.5rem 1rem', background: '#fee2e2', color: '#dc2626', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem' }}>
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
