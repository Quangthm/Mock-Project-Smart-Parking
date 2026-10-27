import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useMemo, useState } from 'react';
import { ownerData as store } from '../data/data';
import type { Booking } from '../../../lib/types';
import { parkingApi, type Site, type Operator } from '../../../lib/parkingApi';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { PageHeader } from '../../../components/ui/PageHeader';

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

export function OwnerOverview({ selectedSiteId, onSiteChange, sites, operators }: { selectedSiteId: string; onSiteChange: (siteId: string) => void; sites: Site[]; operators: Operator[] }) {
  const [liveCapacity, setLiveCapacity] = useState<{ available: number; total: number } | null>(null);
  const visibleSiteIds = useMemo(() => (selectedSiteId === 'all' ? sites : sites.filter(site => site.id === selectedSiteId)).map(site => site.id), [selectedSiteId, sites]);

  useEffect(() => {
    let active = true;
    setLiveCapacity(null);
    if (!visibleSiteIds.length) { setLiveCapacity({ available: 0, total: 0 }); return; }
    Promise.allSettled(visibleSiteIds.map(id => parkingApi.structure(id))).then(results => {
      if (!active) return;
      const values = results.map(result => {
        if (result.status !== 'fulfilled') return null;
        const siteViews = result.value.capacityViews.filter(view => view.unitId === null);
        return siteViews.length && siteViews.every(view => view.available !== null)
          ? { available: siteViews.reduce((sum, view) => sum + (view.available ?? 0), 0), total: siteViews.reduce((sum, view) => sum + view.total, 0) }
          : null;
      });
      setLiveCapacity(values.every(value => value !== null) ? values.reduce((sum, value) => ({ available: sum.available + (value?.available ?? 0), total: sum.total + (value?.total ?? 0) }), { available: 0, total: 0 }) : null);
    });
    return () => { active = false; };
  }, [visibleSiteIds]);

  // Build the view data from the owner's sites, then apply the global site selection before aggregating stats.
  const overview = useMemo(() => {
    const visibleSites = selectedSiteId === 'all' ? sites : sites.filter(site => site.id === selectedSiteId);
    const siteIds = new Set(visibleSites.map(site => site.id));
    const bookings: Booking[] = visibleSites.flatMap(site => store.getBookingsByLot(site.id));
    const completedBookings = bookings.filter(booking => booking.status === 'completed');
    const revenue = completedBookings.reduce((sum, booking) => sum + booking.amount, 0);
    const capacity = visibleSites.reduce((sum, site) => sum + site.totalPhysicalCapacity, 0);
    const visibleOperators = operators.filter(operator => selectedSiteId === 'all' || operator.siteIds.includes(selectedSiteId));
    return { revenue, capacity, employeeCount: visibleOperators.length, siteCount: siteIds.size, visibleSites, revenueTrend: buildRevenueTrend(bookings) };
  }, [selectedSiteId, sites, operators]);

  const stats = [
    { label: 'Total Revenue', value: `${overview.revenue.toLocaleString('vi-VN')} VND` },
    { label: 'Available Slots', value: `${liveCapacity === null ? '—' : liveCapacity.available.toLocaleString('en-US')}/${(liveCapacity?.total ?? overview.capacity).toLocaleString('en-US')}` },
    { label: 'Total Employees', value: overview.employeeCount.toLocaleString('en-US') },
  ];

  return (
    <section className="space-y-6">
      <div className="management-title-row">
        <PageHeader title="Owner Overview" description="Combined performance for the selected parking sites." />
        <label className="management-site-filter"><span>Site</span><RoundedSelect className="input" value={selectedSiteId} onChange={event => onSiteChange(event.target.value)}><option value="all">All Sites</option>{sites.map(site => <option key={site.id} value={site.id}>{site.name}</option>)}</RoundedSelect></label>
      </div>
      <div className="owner-overview-canvas management-dashboard-grid">
        <div className="owner-kpi-strip management-kpi-strip">
          {stats.map(stat => <article key={stat.label} className="owner-kpi"><p className="text-sm text-[var(--muted)]">{stat.label}</p><p className="mt-2 text-2xl font-semibold tabular-nums text-[var(--fg)]">{stat.value}</p></article>)}
        </div>
        <section className="owner-revenue-panel rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
          <div className="mb-5">
            <h2 className="text-lg font-semibold text-[var(--fg)]">Revenue Trend</h2>
            <p className="mt-1 text-sm text-[var(--muted)]">Completed bookings over the last 6 months</p>
          </div>
          <ResponsiveContainer width="100%" height={260}>
            <AreaChart data={overview.revenueTrend}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis dataKey="month" tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <YAxis ticks={overview.revenueTrend.every(point => point.revenue === 0) ? [0] : undefined} tickFormatter={value => value === 0 ? '0' : `${(Number(value) / 1000).toFixed(0)}K`} tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <Tooltip formatter={value => `${Number(value).toLocaleString('vi-VN')} VND`} contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 12 }} />
              <Area type="linear" dataKey="revenue" stroke="var(--accent)" fill="transparent" strokeWidth={2} name="Revenue" />
              <Area type="linear" dataKey="netRevenue" stroke="var(--muted)" fill="transparent" strokeWidth={2} strokeDasharray="5 4" name="Net Revenue (85%)" />
            </AreaChart>
          </ResponsiveContainer>
        </section>
        <section className="management-side-panel" aria-labelledby="owner-sites-heading">
          <div className="management-panel-heading"><div><h2 id="owner-sites-heading">Parking sites</h2><p>Capacity across the current selection</p></div><span className="management-panel-count">{overview.siteCount}</span></div>
          <div className="management-site-list">
            {overview.visibleSites.length ? overview.visibleSites.slice(0, 5).map(site => (
              <div className="management-site-row" key={site.id}>
                <span className="management-site-symbol" aria-hidden="true">P</span>
                <span className="management-site-copy"><strong>{site.name}</strong><small>{site.address}</small></span>
                <span className="management-site-number">{site.totalPhysicalCapacity.toLocaleString('en-US')}<small>spaces</small></span>
              </div>
            )) : <p className="management-empty">No parking sites in this selection.</p>}
          </div>
        </section>
      </div>
      <p className="text-xs text-[var(--muted)]">Revenue uses completed bookings. Available slots come from the live site structure; the denominator is physical capacity.</p>
    </section>
  );
}
