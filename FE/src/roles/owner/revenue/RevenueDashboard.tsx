import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useState, useMemo, useEffect } from 'react';
import { useApp } from '../../../context/AppContext';
import { ownerData as store } from '../data/data';
import { store as appStore } from '../../../lib/store';
import type { Booking, ParkingLot } from '../../../lib/types';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import { PaymentMethodLogo } from '../../../components/payment/PaymentMethodLogo';
import { PageHeader } from '../../../components/ui/PageHeader';

function buildRevenueData(bookings: Booking[], endDate?: string) {
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const now = endDate ? new Date(`${endDate}T12:00:00`) : new Date();
  const result = [];
  for (let i = 5; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    const month = d.getMonth();
    const year = d.getFullYear();
    const monthly = bookings.filter(b => {
      const bd = new Date(b.completedAt ?? b.createdAt);
      return bd.getMonth() === month && bd.getFullYear() === year && b.status === 'completed';
    });
    const revenue = monthly.reduce((s, b) => s + b.amount, 0);
    result.push({ month: MONTHS[month], revenue, bookings: monthly.length, profit: Math.round(revenue * 0.85) });
  }
  return result;
}

export function RevenueDashboard({ selectedSiteId = 'all', sites }: { selectedSiteId?: string; sites?: Array<Pick<ParkingLot, 'id' | 'name'>> }) {
  const { user } = useApp();
  const [search, setSearch] = useState('');
  const [filterMethod, setFilterMethod] = useState('all');
  const [paymentStatusFilter, setPaymentStatusFilter] = useState('all');
  const [customerFilter, setCustomerFilter] = useState('all');
  const [passFilter, setPassFilter] = useState('all');
  const [lotFilter, setLotFilter] = useState(selectedSiteId);
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const activeFilterCount = [lotFilter !== 'all', Boolean(dateFrom), Boolean(dateTo), filterMethod !== 'all', paymentStatusFilter !== 'all', customerFilter !== 'all', passFilter !== 'all'].filter(Boolean).length;

  useEffect(() => setLotFilter(selectedSiteId), [selectedSiteId]);

  const ownerLots = useMemo(() => sites ?? store.getLotsByOwner(user?.id ?? ''), [sites, user?.id]);
  const lots = useMemo(() => lotFilter === 'all' ? ownerLots : ownerLots.filter(l => l.id === lotFilter), [ownerLots, lotFilter]);
  const invalidRange = Boolean(dateFrom && dateTo && dateFrom > dateTo);

  const allBookings = useMemo(() => {
    if (invalidRange) return [];
    return lots.flatMap(l => store.getBookingsByLot(l.id)).filter(booking => {
      const date = (booking.completedAt ?? booking.createdAt).slice(0, 10);
      const systemCustomer = Boolean(booking.driverId?.trim());
      const hasPass = systemCustomer && appStore.getPassesByDriver(booking.driverId).some(pass =>
        pass.lotId === booking.lotId && pass.purchasedAt <= booking.startTime && pass.expiresAt >= booking.startTime,
      );
      return (!dateFrom || date >= dateFrom) && (!dateTo || date <= dateTo)
        && (filterMethod === 'all' || (booking.paymentMethod ?? 'unknown') === filterMethod)
        && (paymentStatusFilter === 'all' || (booking.paymentStatus ?? 'unknown') === paymentStatusFilter)
        && (customerFilter === 'all' || (customerFilter === 'system' ? systemCustomer : !systemCustomer))
        && (passFilter === 'all' || (passFilter === 'with-pass' ? hasPass : !hasPass));
    });
  }, [lots, dateFrom, dateTo, invalidRange, filterMethod, paymentStatusFilter, customerFilter, passFilter]);

  const completedBookings = useMemo(() => {
    return allBookings.filter(b => b.status === 'completed');
  }, [allBookings]);

  const totalRevenue = useMemo(() => {
    return completedBookings.reduce((s, b) => s + b.amount, 0);
  }, [completedBookings]);

  const netProfit = Math.round(totalRevenue * 0.85);
  const platformFee = Math.round(totalRevenue * 0.15);
  const revenueData = useMemo(() => buildRevenueData(allBookings, dateTo), [allBookings, dateTo]);
  const hasData = revenueData.some(d => d.revenue > 0);
  const cancellationRecords = allBookings.filter(b => b.status === 'cancelled');
  const failedPaymentRecords = allBookings.filter(b => b.status !== 'cancelled' && b.paymentStatus === 'failed');
  const cancellationExposure = cancellationRecords.reduce((sum, b) => sum + b.amount, 0);
  const failedPaymentExposure = failedPaymentRecords.reduce((sum, b) => sum + b.amount, 0);

  const filteredTransactions = useMemo(() => {
    return completedBookings.filter(b => {
      const q = search.trim().toLowerCase();
      const matchQuery = !q || b.id.toLowerCase().includes(q) || b.licensePlate.toLowerCase().includes(q) || b.lotName.toLowerCase().includes(q);
      return matchQuery;
    });
  }, [completedBookings, search]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <PageHeader eyebrow={lotFilter === 'all' ? `All Sites (${lots.length})` : (lots[0]?.name ?? 'Selected Site')} title="Financial & Revenue Center" description="Revenue, settlement, and loss exposure for your parking sites." />

      <section className="card p-5" aria-label="Financial filters">
        <details className="group">
          <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-lg text-sm font-semibold text-[var(--fg)] focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-[var(--accent)] [&::-webkit-details-marker]:hidden">
            <span className="flex items-center gap-2"><UntitledIcon name="filter" size={17} /> Filters {activeFilterCount > 0 && <span className="rounded-full bg-[var(--accent)] px-2 py-0.5 text-xs text-[var(--accent-fg)] tabular-nums">{activeFilterCount} active</span>}{invalidRange && <span className="text-xs text-[var(--badge-danger-strong)]">Invalid dates</span>}</span>
            <span aria-hidden="true" className="text-[var(--muted)] transition-transform group-open:rotate-180"><UntitledIcon name="chevron-down" size={18} /></span>
          </summary>
          <div className="mt-4 grid gap-4 border-t border-[var(--border)] pt-4 sm:grid-cols-2 xl:grid-cols-3">
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">Parking lot<RoundedSelect className="input" value={lotFilter} onChange={event => setLotFilter(event.target.value)}><option value="all">All parking lots</option>{ownerLots.map(lot => <option key={lot.id} value={lot.id}>{lot.name}</option>)}</RoundedSelect></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">From date<input className="input" type="date" value={dateFrom} max={dateTo || undefined} onChange={event => setDateFrom(event.target.value)} /></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">To date<input className="input" type="date" value={dateTo} min={dateFrom || undefined} onChange={event => setDateTo(event.target.value)} /></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">Payment method<RoundedSelect className="input" value={filterMethod} onChange={event => setFilterMethod(event.target.value)}><option value="all">All payment methods</option><option value="momo">MoMo</option><option value="vnpay">VNPay</option><option value="zalopay">ZaloPay</option><option value="visa">Visa / Card</option><option value="applepay">Apple Pay</option><option value="qr">VietQR</option><option value="unknown">Not recorded</option></RoundedSelect></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">Payment status<RoundedSelect className="input" value={paymentStatusFilter} onChange={event => setPaymentStatusFilter(event.target.value)}><option value="all">All payment statuses</option><option value="paid">Paid</option><option value="pending">Pending</option><option value="failed">Failed</option><option value="unknown">Not recorded</option></RoundedSelect></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">Customer type<RoundedSelect className="input" value={customerFilter} onChange={event => setCustomerFilter(event.target.value)}><option value="all">All customers</option><option value="system">System account</option><option value="walk-in">Walk-in</option></RoundedSelect></label>
        <label className="grid gap-2 text-sm font-semibold text-[var(--fg)]">Parking pass<RoundedSelect className="input" value={passFilter} onChange={event => setPassFilter(event.target.value)}><option value="all">All bookings</option><option value="with-pass">Driver held an active pass</option><option value="without-pass">No active pass found</option></RoundedSelect></label>
        <div className="flex items-end"><button type="button" className="btn-outline w-full justify-center" onClick={() => { setLotFilter('all'); setDateFrom(''); setDateTo(''); setSearch(''); setFilterMethod('all'); setPaymentStatusFilter('all'); setCustomerFilter('all'); setPassFilter('all') }}>Clear filters</button></div>
        {invalidRange && <p role="alert" className="text-sm text-[var(--badge-danger-strong)] sm:col-span-2 xl:col-span-3">The start date must be on or before the end date.</p>}
        <p className="text-xs text-[var(--muted)] sm:col-span-2 xl:col-span-3">Dates use completion time when available. Customer type uses the booking account ID; pass filtering checks whether a pass was active at the booking start, since pass usage is not recorded on bookings.</p>
          </div>
        </details>
      </section>

      {/* KPI METRIC CARDS */}
      <div className="owner-finance-kpis">
        {[
          { label: 'Gross Revenue', value: `${totalRevenue.toLocaleString('vi-VN')} VND`, sub: 'From completed bookings' },
          { label: 'Net Earnings (85%)', value: `${netProfit.toLocaleString('vi-VN')} VND`, sub: 'After platform fee settlement' },
          { label: 'Platform Fee (15%)', value: `${platformFee.toLocaleString('vi-VN')} VND`, sub: 'SmartParking system service fee' },
          { label: 'Completed Transactions', value: String(completedBookings.length), sub: `Across ${lots.length} active sites` },
        ].map(stat => (
          <div key={stat.label} className="owner-finance-kpi">
            <div style={{ fontSize: '0.875rem', color: 'var(--muted)', fontWeight: 500, marginBottom: '0.5rem' }}>{stat.label}</div>
            <div style={{ fontFamily: 'Be Vietnam Pro', fontWeight: 600, fontSize: '1.4rem', color: 'var(--fg)', marginBottom: '0.25rem', fontVariantNumeric: 'tabular-nums' }}>
              {stat.value}
            </div>
            <div style={{ fontSize: '0.74rem', color: 'var(--muted)' }}>{stat.sub}</div>
          </div>
        ))}
      </div>

      <section className="card p-5" aria-labelledby="owner-loss-report-heading">
        <h2 id="owner-loss-report-heading" className="text-lg font-semibold text-[var(--fg)]">Financial Loss Report</h2>
        <p className="mt-1 text-sm text-[var(--muted)]">Booking value at risk in the selected period. These figures are exposure, not confirmed money lost.</p>
        <div className="mt-5 grid gap-3 sm:grid-cols-3">
          <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4"><p className="text-sm text-[var(--muted)]">Cancellations</p><strong className="mt-2 block text-xl tabular-nums text-[var(--fg)]">{cancellationExposure.toLocaleString('vi-VN')} VND</strong><small className="text-[var(--muted)]">{cancellationRecords.length} cancelled bookings</small></div>
          <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4"><p className="text-sm text-[var(--muted)]">Refunds</p><strong className="mt-2 block text-xl text-[var(--fg)]">Not tracked</strong><small className="text-[var(--muted)]">Refund transactions are not available in the current data.</small></div>
          <div className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4"><p className="text-sm text-[var(--muted)]">Other losses · failed payments</p><strong className="mt-2 block text-xl tabular-nums text-[var(--fg)]">{failedPaymentExposure.toLocaleString('vi-VN')} VND</strong><small className="text-[var(--muted)]">{failedPaymentRecords.length} failed payments</small></div>
        </div>
        <div className="mt-5 overflow-x-auto rounded-xl border border-[var(--border)]">
          <table className="w-full min-w-[680px] table-fixed text-left text-sm">
            <colgroup><col className="w-[22%]" /><col className="w-[32%]" /><col className="w-[22%]" /><col className="w-[24%]" /></colgroup>
            <thead className="bg-[var(--bg)] text-[var(--muted)]"><tr className="border-b border-[var(--border)]"><th scope="col" className="px-4 py-3 font-semibold">Booking</th><th scope="col" className="px-4 py-3 font-semibold">Parking lot</th><th scope="col" className="px-4 py-3 font-semibold">Category</th><th scope="col" className="px-4 py-3 text-right font-semibold">Value at risk</th></tr></thead>
            <tbody>{[...cancellationRecords.map(booking => ({ booking, category: 'Cancellation' })), ...failedPaymentRecords.map(booking => ({ booking, category: 'Failed payment' }))].map(({ booking, category }) => <tr key={booking.id} className="border-b border-[var(--border)] last:border-b-0"><td className="break-all px-4 py-3 font-medium tabular-nums">{booking.id}</td><td className="break-words px-4 py-3">{booking.lotName}</td><td className="px-4 py-3">{category}</td><td className="whitespace-nowrap px-4 py-3 text-right font-semibold tabular-nums">{booking.amount.toLocaleString('vi-VN')} VND</td></tr>)}{cancellationRecords.length + failedPaymentRecords.length === 0 && <tr><td colSpan={4} className="px-4 py-6 text-center text-[var(--muted)]">No cancellation or failed payment records match these filters.</td></tr>}</tbody>
          </table>
        </div>
      </section>

      {/* REVENUE TREND CHART */}
      <div className="card" style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem', flexWrap: 'wrap', gap: '0.5rem' }}>
          <div>
            <h3 style={{ fontFamily: 'Be Vietnam Pro', fontWeight: 600, fontSize: '1.05rem', margin: 0, color: 'var(--fg)' }}>
              6-Month Revenue Trend & Net Settlement
            </h3>
            <span style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>
              Six calendar months ending at the selected To date, or today
            </span>
          </div>
          <div style={{ display: 'flex', gap: '1rem', fontSize: '0.78rem' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: 'var(--accent)', fontWeight: 600 }}>
              <span style={{ width: 10, height: 10, borderRadius: 2, background: 'var(--accent)' }} /> Gross Revenue
            </span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: 'var(--muted)', fontWeight: 600 }}>
              <span style={{ width: 10, height: 10, borderRadius: 2, background: 'var(--muted)' }} /> Net Settlement (85%)
            </span>
          </div>
        </div>

        {hasData ? (
          <ResponsiveContainer width="100%" height={260}>
            <AreaChart data={revenueData}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis dataKey="month" tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <YAxis tickFormatter={v => v === 0 ? '0' : `${(Number(v) / 1000).toFixed(0)}K`} tick={{ fontSize: 12, fill: 'var(--muted)' }} />
              <Tooltip formatter={(v) => `${Number(v).toLocaleString('vi-VN')} VND`} contentStyle={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', fontSize: '0.82rem' }} />
              <Area type="linear" dataKey="revenue" stroke="var(--accent)" fill="transparent" strokeWidth={2} name="Gross Revenue" />
              <Area type="linear" dataKey="profit" stroke="var(--muted)" fill="transparent" strokeWidth={2} strokeDasharray="5 4" name="Net Revenue" />
            </AreaChart>
          </ResponsiveContainer>
        ) : (
          <div style={{ height: 200, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', color: 'var(--muted)', gap: '0.5rem' }}>
            <div style={{ color: 'var(--primary)' }}><UntitledIcon name="chart" size={30} /></div>
            <div style={{ fontWeight: 500, fontSize: '0.9rem' }}>No booking revenue data yet</div>
            <div style={{ fontSize: '0.8rem' }}>Revenue charts will automatically reflect completed bookings.</div>
          </div>
        )}
      </div>

      {/* RECONCILIATION & TRANSACTION TABLE */}
      <div className="card" style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.75rem' }}>
          <div>
            <h3 style={{ fontFamily: 'Be Vietnam Pro', fontWeight: 600, fontSize: '1.05rem', margin: 0, color: 'var(--fg)' }}>
              Completed Transactions & Settlement Audit
            </h3>
            <span style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>
              Detailed audit trail of all verified completed parking sessions ({filteredTransactions.length} records)
            </span>
          </div>

          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'center' }}>
            <input
              className="input sp-search-input"
              aria-label="Search transactions by ID, plate, or parking lot"
              name="transaction-search"
              placeholder="Search by ID, plate, or lot…"
              value={search}
              onChange={e => setSearch(e.target.value)}
              style={{ padding: '0.35rem 0.65rem', fontSize: '0.8rem', width: 220 }}
            />
          </div>
        </div>

        <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '0.625rem' }}>
          <table style={{ width: '100%', minWidth: 640, textAlign: 'left', fontSize: '0.82rem', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card)', color: 'var(--muted)' }}>
                <th style={{ padding: '0.65rem 0.85rem' }}>Booking ID</th>
                <th style={{ padding: '0.65rem 0.85rem' }}>Parking Lot</th>
                <th style={{ padding: '0.65rem 0.85rem' }}>Vehicle</th>
                <th style={{ padding: '0.65rem 0.85rem' }}>Payment Method</th>
                <th style={{ padding: '0.65rem 0.85rem', textAlign: 'right' }}>Gross Amount</th>
                <th style={{ padding: '0.65rem 0.85rem', textAlign: 'right' }}>Net (85%)</th>
              </tr>
            </thead>
            <tbody>
              {filteredTransactions.map(booking => {
                const net = Math.round(booking.amount * 0.85);
                return (
                  <tr key={booking.id} style={{ borderBottom: '1px solid var(--border)', color: 'var(--fg)' }}>
                    <td style={{ padding: '0.65rem 0.85rem', fontWeight: 600, fontFamily: 'monospace' }}>
                      {booking.id}
                    </td>
                    <td style={{ padding: '0.65rem 0.85rem' }}>
                      {booking.lotName}
                    </td>
                    <td style={{ padding: '0.65rem 0.85rem' }}>
                      <strong>{booking.licensePlate}</strong>
                    </td>
                    <td style={{ padding: '0.65rem 0.85rem' }}>
                      <PaymentMethodLogo method={booking.paymentMethod ?? 'qr'} size="xs" showName />
                    </td>
                    <td style={{ padding: '0.65rem 0.85rem', textAlign: 'right', fontWeight: 600 }}>
                      {booking.amount.toLocaleString('vi-VN')} VND
                    </td>
                    <td style={{ padding: '0.65rem 0.85rem', textAlign: 'right', fontWeight: 700, color: '#22c55e' }}>
                      {net.toLocaleString('vi-VN')} VND
                    </td>
                  </tr>
                );
              })}
              {filteredTransactions.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: '2rem', textAlign: 'center', color: 'var(--muted)' }}>
                    No completed transactions matching your filter.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
