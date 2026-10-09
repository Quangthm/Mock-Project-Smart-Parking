import { useMemo } from 'react';
import { ownerData as store } from '../data/data';
import type { Booking } from '../../../lib/types';
import type { Site, Operator } from '../../../lib/parkingApi';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

function buildRevenueTrend(bookings: Booking[]) {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const now = new Date();

  return Array.from({ length: 6 }, (_, index) => {
    const date = new Date(now.getFullYear(), now.getMonth() - (5 - index), 1);
    const monthlyRevenue = bookings
      .filter(booking => {
        const bookingDate = new Date(booking.createdAt);
        return booking.status === 'completed'
          && bookingDate.getMonth() === date.getMonth()
          && bookingDate.getFullYear() === date.getFullYear();
      })
      .reduce((sum, booking) => sum + booking.amount, 0);

    return { month: months[date.getMonth()], revenue: monthlyRevenue, netRevenue: Math.round(monthlyRevenue * 0.85) };
  });
}

export function OwnerOverview({ selectedSiteId, sites, operators }: { selectedSiteId: string; sites: Site[]; operators: Operator[] }) {

  // Build the view data from the owner's sites, then apply the global site selection before aggregating stats.
  const overview = useMemo(() => {
    const visibleSites = selectedSiteId === 'all' ? sites : sites.filter(site => site.id === selectedSiteId);
    const siteIds = new Set(visibleSites.map(site => site.id));
    const bookings: Booking[] = visibleSites.flatMap(site => store.getBookingsByLot(site.id));
    const completedBookings = bookings.filter(booking => booking.status === 'completed');
    const revenue = completedBookings.reduce((sum, booking) => sum + booking.amount, 0);
    const capacity = visibleSites.reduce((sum, site) => sum + site.totalPhysicalCapacity, 0);
    const visibleOperators = operators.filter(operator => selectedSiteId === 'all' || operator.siteIds.includes(selectedSiteId));
    return { revenue, capacity, employeeCount: visibleOperators.length, siteCount: siteIds.size, revenueTrend: buildRevenueTrend(bookings) };
  }, [selectedSiteId, sites, operators]);

  const stats = [
    { label: 'Total Revenue', value: `${overview.revenue.toLocaleString('vi-VN')} ₫`, accent: 'text-green-600' },
    { label: 'Parking Capacity', value: overview.capacity.toLocaleString('vi-VN'), accent: 'text-blue-600' },
    { label: 'Total Employees', value: overview.employeeCount.toLocaleString('vi-VN'), accent: 'text-violet-600' },
  ];

  return (
    <section className="space-y-6">
      <header>
        <p className="text-sm font-medium text-[var(--primary)]">{selectedSiteId === 'all' ? `${overview.siteCount} sites` : 'Filtered site view'}</p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">Owner Overview</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">Combined performance for the selected parking sites.</p>
      </header>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {stats.map(stat => <article key={stat.label} className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5"><p className="text-sm text-[var(--muted)]">{stat.label}</p><p className={`mt-2 text-2xl font-bold ${stat.accent}`}>{stat.value}</p></article>)}
      </div>
      <section className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="mb-5">
          <h2 className="text-lg font-semibold text-[var(--fg)]">Revenue Trend</h2>
          <p className="mt-1 text-sm text-[var(--muted)]">Completed bookings over the last 6 months</p>
        </div>
        <ResponsiveContainer width="100%" height={260}>
          <AreaChart data={overview.revenueTrend}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
            <XAxis dataKey="month" tick={{ fontSize: 12, fill: 'var(--muted)' }} />
            <YAxis tickFormatter={value => value === 0 ? '0' : `${(Number(value) / 1000).toFixed(0)}K`} tick={{ fontSize: 12, fill: 'var(--muted)' }} />
            <Tooltip formatter={value => `${Number(value).toLocaleString('vi-VN')} ₫`} contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 12 }} />
            <Area type="monotone" dataKey="revenue" stroke="#2563eb" fill="#2563eb18" strokeWidth={2} name="Revenue" />
            <Area type="monotone" dataKey="netRevenue" stroke="#22c55e" fill="#22c55e18" strokeWidth={2} name="Net Revenue (85%)" />
          </AreaChart>
        </ResponsiveContainer>
      </section>
      <p className="text-xs text-[var(--muted)]">Revenue uses completed bookings. Parking Capacity comes from the saved physical layout.</p>
    </section>
  );
}
