import { useState } from 'react';
import { adminData as store } from '../data/data';
import type { AuditLog } from '../../../lib/types';

export function AuditLogView() {
  const [logs] = useState<AuditLog[]>(store.getAuditLogs);
  const [filter, setFilter] = useState('');
  const filtered = filter ? logs.filter(l => l.action.includes(filter.toUpperCase()) || l.userName.toLowerCase().includes(filter.toLowerCase())) : logs;

  const ROLE_COLORS: Record<string, string> = { admin: '#ef4444', owner: '#f59e0b', driver: '#2563eb', operator: '#a855f7' };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
        <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.3rem', color: 'var(--fg)', margin: 0 }}>Audit Log</h2>
        <input className="input" style={{ maxWidth: '260px' }} value={filter} onChange={e => setFilter(e.target.value)} placeholder="Search by action or user..." />
      </div>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.82rem' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid var(--border)' }}>
              {['Timestamp', 'User', 'Role', 'Action', 'Details'].map(h => (
                <th key={h} style={{ padding: '0.625rem 0.75rem', textAlign: 'left', color: 'var(--muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.slice(0, 100).map(log => (
              <tr key={log.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td style={{ padding: '0.5rem 0.75rem', color: 'var(--muted)', whiteSpace: 'nowrap' }}>
                  {new Date(log.timestamp).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })}
                </td>
                <td style={{ padding: '0.5rem 0.75rem', fontWeight: 500, color: 'var(--fg)' }}>{log.userName}</td>
                <td style={{ padding: '0.5rem 0.75rem' }}>
                  <span style={{ padding: '0.15rem 0.5rem', borderRadius: '999px', fontSize: '0.7rem', fontWeight: 700, background: (ROLE_COLORS[log.userRole] || 'var(--muted)') + '20', color: ROLE_COLORS[log.userRole] || 'var(--muted)' }}>
                    {log.userRole.toUpperCase()}
                  </span>
                </td>
                <td style={{ padding: '0.5rem 0.75rem', fontFamily: 'monospace', fontSize: '0.75rem', color: 'var(--primary)' }}>{log.action}</td>
                <td style={{ padding: '0.5rem 0.75rem', color: 'var(--muted)', maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{log.details}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {!filtered.length && <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--muted)' }}>No logs found.</div>}
      </div>
    </div>
  );
}
