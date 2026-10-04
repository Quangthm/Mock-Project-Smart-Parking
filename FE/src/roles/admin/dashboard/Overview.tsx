import { adminData as store } from '../data/data';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, Cell } from 'recharts';

import { UntitledIcon } from '../../../components/icon/UntitledIcon';

export function Overview() {
  const users = store.getUsers();
  const lots = store.getLots();
  const bookings = store.getBookings();
  const apps = store.getApplications();
  const logs = store.getAuditLogs().slice(0, 5);

  const totalRevenue = bookings.filter(b => b.status === 'completed').reduce((s, b) => s + b.amount, 0);
  const pendingApps = apps.filter(a => a.status === 'pending').length;

  // Build booking trend: last 7 days
  const trendData = Array.from({ length: 7 }, (_, i) => {
    const d = new Date();
    d.setDate(d.getDate() - (6 - i));
    const label = d.toLocaleDateString('en-GB', { weekday: 'short' });
    const dayBookings = bookings.filter(b => {
      const bd = new Date(b.createdAt);
      return bd.toDateString() === d.toDateString();
    });
    return { day: label, bookings: dayBookings.length, revenue: dayBookings.reduce((s, b) => s + b.amount, 0) };
  });

  const userRoleData = [
    { role: 'Drivers', count: users.filter(u => u.role === 'driver').length, color: '#2563eb' },
    { role: 'Owners', count: users.filter(u => u.role === 'owner').length, color: '#f59e0b' },
    { role: 'Operators', count: users.filter(u => u.role === 'operator').length, color: '#a855f7' },
  ];

  const ROLE_COLORS: Record<string, string> = { admin: '#ef4444', owner: '#f59e0b', driver: '#2563eb', operator: '#a855f7' };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      {/* Top KPI bar */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem' }}>
        {[
          { label: 'Platform Revenue', value: `${totalRevenue.toLocaleString('vi-VN')}₫`, sub: 'All time from bookings', icon: '💰', gradient: 'linear-gradient(135deg,#1d4ed8,#7c3aed)', text: '#fff' },
          { label: 'Total Users', value: users.length, sub: `${users.filter(u => u.role === 'driver').length} drivers · ${users.filter(u => u.role === 'owner').length} owners`, icon: '👥', gradient: 'linear-gradient(135deg,#059669,#0d9488)', text: '#fff' },
          { label: 'Active Lots', value: lots.filter(l => l.status === 'active').length, sub: `${lots.reduce((s, l) => s + l.totalSlots, 0)} total slots`, icon: '🅿️', gradient: 'linear-gradient(135deg,#d97706,#dc2626)', text: '#fff' },
          { label: 'Pending Applications', value: pendingApps, sub: pendingApps > 0 ? 'Requires attention' : 'All reviewed', icon: '⏳', gradient: pendingApps > 0 ? 'linear-gradient(135deg,#dc2626,#9f1239)' : 'linear-gradient(135deg,#4b5563,#1f2937)', text: '#fff' },
        ].map(k => (
          <div key={k.label} style={{ borderRadius: 'var(--radius)', padding: '1.25rem', background: k.gradient, color: k.text, position: 'relative', overflow: 'hidden' }}>
            <div style={{ position: 'absolute', top: '0.75rem', right: '0.875rem', opacity: 0.35 }}><UntitledIcon name={k.icon} size={26} /></div>
            <div style={{ fontSize: '0.72rem', opacity: 0.85, marginBottom: '0.25rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>{k.label}</div>
            <div style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.6rem', marginBottom: '0.2rem' }}>{k.value}</div>
            <div style={{ fontSize: '0.72rem', opacity: 0.75 }}>{k.sub}</div>
          </div>
        ))}
      </div>

      {/* Charts row */}
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '1.25rem' }}>
        <div className="card">
          <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '1rem' }}>Booking Activity — Last 7 Days</div>
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={trendData}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis dataKey="day" tick={{ fontSize: 11, fill: 'var(--muted)' }} />
              <YAxis tick={{ fontSize: 11, fill: 'var(--muted)' }} allowDecimals={false} />
              <Tooltip contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '0.8rem' }} />
              <Area type="monotone" dataKey="bookings" stroke="#2563eb" fill="#2563eb18" strokeWidth={2} name="Bookings" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
        <div className="card">
          <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '1rem' }}>User Distribution</div>
          <ResponsiveContainer width="100%" height={140}>
            <BarChart data={userRoleData} barSize={32}>
              <XAxis dataKey="role" tick={{ fontSize: 11, fill: 'var(--muted)' }} />
              <YAxis tick={{ fontSize: 11, fill: 'var(--muted)' }} allowDecimals={false} />
              <Tooltip contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '0.8rem' }} />
              <Bar dataKey="count" name="Users" radius={[4, 4, 0, 0]}>
                {userRoleData.map((entry, i) => (
                  <Cell key={i} fill={entry.color} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
          <div style={{ display: 'flex', gap: '0.875rem', marginTop: '0.5rem', justifyContent: 'center' }}>
            {userRoleData.map(r => (
              <div key={r.role} style={{ display: 'flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.72rem' }}>
                <div style={{ width: 8, height: 8, borderRadius: '2px', background: r.color }} />
                <span style={{ color: 'var(--muted)' }}>{r.role}: <strong style={{ color: 'var(--fg)' }}>{r.count}</strong></span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* System health + recent activity */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.25rem' }}>
        <div className="card">
          <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '1rem' }}>System Health</div>
          {[
            { name: 'API Gateway', status: 'operational', latency: '12ms' },
            { name: 'Database Cluster', status: 'operational', latency: '4ms' },
            { name: 'IoT Sensor Network', status: 'operational', latency: '—' },
            { name: 'Payment Gateway', status: 'operational', latency: '89ms' },
            { name: 'Notification Service', status: 'operational', latency: '—' },
          ].map(s => (
            <div key={s.name} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0.5rem 0', borderBottom: '1px solid var(--border)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <div style={{ width: 7, height: 7, borderRadius: '50%', background: '#22c55e', boxShadow: '0 0 0 2px #22c55e30' }} />
                <span style={{ fontSize: '0.82rem', color: 'var(--fg)' }}>{s.name}</span>
              </div>
              <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                {s.latency !== '—' && <span style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>{s.latency}</span>}
                <span style={{ fontSize: '0.7rem', color: '#22c55e', fontWeight: 600 }}>ONLINE</span>
              </div>
            </div>
          ))}
        </div>
        <div className="card">
          <div style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '0.95rem', color: 'var(--fg)', marginBottom: '1rem' }}>Recent Activity</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
            {logs.map(log => (
              <div key={log.id} style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-start' }}>
                <div style={{ width: 28, height: 28, borderRadius: '50%', background: (ROLE_COLORS[log.userRole] || '#64748b') + '20', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, fontSize: '0.65rem', fontWeight: 700, color: ROLE_COLORS[log.userRole] || '#64748b' }}>
                  {log.userName.charAt(0)}
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: '0.8rem', color: 'var(--fg)', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{log.details}</div>
                  <div style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>{new Date(log.timestamp).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })}</div>
                </div>
              </div>
            ))}
            {!logs.length && <div style={{ textAlign: 'center', color: 'var(--muted)', fontSize: '0.85rem', padding: '1rem' }}>No recent activity.</div>}
          </div>
        </div>
      </div>
    </div>
  );
}
