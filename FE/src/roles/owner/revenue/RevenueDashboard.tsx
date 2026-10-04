import { useApp } from '../../../context/AppContext';
import { ownerData as store } from '../data/data';
import type { Booking } from '../../../lib/types';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

function buildRevenueData(bookings: Booking[]) {
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const now = new Date();
  const result = [];
  for (let i = 5; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    const month = d.getMonth();
    const year = d.getFullYear();
    const monthly = bookings.filter(b => {
      const bd = new Date(b.createdAt);
      return bd.getMonth() === month && bd.getFullYear() === year && b.status === 'completed';
    });
    const revenue = monthly.reduce((s, b) => s + b.amount, 0);
    result.push({ month: MONTHS[month], revenue, bookings: monthly.length, profit: Math.round(revenue * 0.85) });
  }
  return result;
}

// Revenue dashboard
import { UntitledIcon } from '../../../components/icon/UntitledIcon';

export function RevenueDashboard() {
  const { user } = useApp();
  const lots = store.getLotsByOwner(user?.id ?? '');
  const allBookings = lots.flatMap(l => store.getBookingsByLot(l.id));
  const completedBookings = allBookings.filter(b => b.status === 'completed');
  const totalRevenue = completedBookings.reduce((s, b) => s + b.amount, 0);
  const revenueData = buildRevenueData(allBookings);
  const hasData = revenueData.some(d => d.revenue > 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem' }}>
        {[
          { label: 'Total Revenue', value: `${totalRevenue.toLocaleString('vi-VN')}₫`, icon: '💰', color: '#22c55e' },
          { label: 'Active Lots', value: String(lots.filter(l => l.status === 'active').length), icon: '🅿️', color: '#2563eb' },
          { label: 'Total Bookings', value: String(allBookings.length), icon: '📋', color: '#f59e0b' },
          { label: 'Platform Fee (15%)', value: `${Math.round(totalRevenue * 0.15).toLocaleString('vi-VN')}₫`, icon: '📊', color: '#a855f7' },
        ].map(stat => (
          <div key={stat.label} className="card">
            <div style={{ color: stat.color, marginBottom: '0.375rem' }}><UntitledIcon name={stat.icon} size={22} /></div>
            <div style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.3rem', color: stat.color }}>{stat.value}</div>
            <div style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>{stat.label}</div>
          </div>
        ))}
      </div>
      <div className="card">
        <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1rem', marginBottom: '1.25rem', color: 'var(--fg)' }}>Revenue Trend (Last 6 Months)</h3>
        {hasData ? (
          <ResponsiveContainer width="100%" height={240}>
            <AreaChart data={revenueData}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis dataKey="month" tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <YAxis tickFormatter={v => v === 0 ? '0' : `${(Number(v) / 1000).toFixed(0)}K`} tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <Tooltip formatter={(v) => `${Number(v).toLocaleString('vi-VN')}₫`} contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '0.8rem' }} />
              <Area type="monotone" dataKey="revenue" stroke="#2563eb" fill="#2563eb18" strokeWidth={2} name="Revenue" />
              <Area type="monotone" dataKey="profit" stroke="#22c55e" fill="#22c55e18" strokeWidth={2} name="Net Revenue" />
            </AreaChart>
          </ResponsiveContainer>
        ) : (
          <div style={{ height: 200, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', color: 'var(--muted)', gap: '0.5rem' }}>
            <div style={{ color: 'var(--primary)' }}><UntitledIcon name="chart" size={30} /></div>
            <div style={{ fontWeight: 500, fontSize: '0.9rem' }}>No booking data yet</div>
            <div style={{ fontSize: '0.8rem' }}>Revenue trend will appear once you receive your first bookings.</div>
          </div>
        )}
      </div>
    </div>
  );
}
