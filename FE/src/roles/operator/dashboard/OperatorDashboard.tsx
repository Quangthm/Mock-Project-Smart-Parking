import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { operatorData as store } from '../data/data';
import { CheckInOut } from '../check-in-out/CheckInOut';
import { LotStatus } from '../parking-lot/LotStatus';
import { EmergencyPanel } from '../emergency/EmergencyPanel';
import { SlotManagement } from '../parking-slots/SlotManagement';
import { TicketManagement } from '../tickets/TicketManagement';
import type { OperatorAccessRole } from '../../../lib/types';
import { DashboardSidebar } from '../../../components/layout/DashboardSidebar';
import { PaymentMethodLogo } from '../../../components/payment/PaymentMethodLogo';

type Tab = 'checkin' | 'status' | 'emergency' | 'slots' | 'tickets' | 'finance';

export function OperatorDashboard({ accessRoleOverride }: { accessRoleOverride?: OperatorAccessRole } = {}) {
  const { user } = useApp();
  const [tab, setTab] = useState<Tab>('checkin');
  useEffect(() => {
    const onNavigate = (event: Event) => setTab((event as CustomEvent<Tab>).detail);
    window.addEventListener('sp:dashboard-nav', onNavigate);
    return () => window.removeEventListener('sp:dashboard-nav', onNavigate);
  }, []);
  const accessRole = accessRoleOverride ?? user?.operatorRole ?? 'operation';
  const assignedOwnerId = user?.role === 'owner' ? user.id : user?.ownerId;
  const ownerLots = store.getLots().filter(lot =>
    lot.ownerId === assignedOwnerId
    && (user?.role === 'owner' || !user?.operatorSiteId || user.operatorSiteId === 'all' || lot.id === user?.operatorSiteId)
  );
  const [activeSiteId, setActiveSiteId] = useState<string>('');
  const selectedLot = (activeSiteId ? ownerLots.find(l => l.id === activeSiteId) : null) ?? ownerLots[0] ?? null;

  if (accessRole === 'cashier') {
    return <CashierPOS lotName={selectedLot?.name ?? 'Assigned parking lot'} />;
  }

  const reportLots = accessRole === 'financial' ? ownerLots : selectedLot ? [selectedLot] : [];
  const bookings = reportLots.flatMap(lot => store.getBookingsByLot(lot.id));
  const completedBookings = bookings.filter(booking => booking.status === 'completed');
  const revenue = completedBookings.reduce((sum, booking) => sum + booking.amount, 0);
  const nav: { id: Tab; label: string; icon: string }[] = accessRole === 'financial'
    ? [{ id: 'finance', label: 'Reports & Reconciliation', icon: '₫' }]
    : [
        { id: 'checkin', label: 'Check-In / Out', icon: '✓' },
        { id: 'status', label: 'Lot Status', icon: '▦' },
        { id: 'slots', label: 'Manage Slots', icon: '↔' },
        { id: 'tickets', label: 'Driver Tickets', icon: '✉' },
        { id: 'emergency', label: 'Emergency', icon: '!' },
      ];

  return (
    <DashboardSidebar groups={accessRole === 'financial' ? [
      { items: [{ id: 'finance', label: 'Financial reports', icon: '▤', active: tab === 'finance', onClick: () => setTab('finance') }] },
    ] : [
      { items: [{ id: 'checkin', label: 'Dashboard', icon: '⌂', active: tab === 'checkin', onClick: () => setTab('checkin') }] },
      { label: 'Parking operations', items: [
        { id: 'status', label: 'Lot status', icon: '▦', active: tab === 'status', onClick: () => setTab('status') },
        { id: 'slots', label: 'Manage slots', icon: '↔', active: tab === 'slots', onClick: () => setTab('slots') },
        { id: 'tickets', label: 'Driver tickets', icon: '▤', active: tab === 'tickets', onClick: () => setTab('tickets') },
        { id: 'emergency', label: 'Emergency', icon: '!', active: tab === 'emergency', onClick: () => setTab('emergency') },
      ] },
    ]}>
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
        {user?.operatorSiteId === 'all' && ownerLots.length > 1 && (
          <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[var(--border)] bg-[var(--card)] p-3">
            <span className="text-xs font-semibold text-[var(--muted)]">All Sites Operator — Currently Operating at:</span>
            <select
              className="input text-sm py-1 px-3"
              value={selectedLot?.id ?? ''}
              onChange={e => setActiveSiteId(e.target.value)}
            >
              {ownerLots.map(lot => <option key={lot.id} value={lot.id}>{lot.name}</option>)}
            </select>
          </div>
        )}
        <div className="animate-in">
          {accessRole === 'operation' && tab === 'checkin' && <CheckInOut lot={selectedLot} />}
          {accessRole === 'operation' && tab === 'status' && <LotStatus lot={selectedLot} />}
          {accessRole === 'operation' && tab === 'slots' && <SlotManagement lot={selectedLot} />}
          {accessRole === 'operation' && tab === 'tickets' && <TicketManagement />}
          {accessRole === 'operation' && tab === 'emergency' && <EmergencyPanel />}
          {accessRole === 'financial' && tab === 'finance' && (
            <section className="mx-auto w-full max-w-5xl space-y-6">
              <header>
                <p className="text-sm font-medium text-[var(--primary)]">{accessRole === 'financial' && (user?.role === 'owner' || user?.operatorSiteId === 'all') ? 'All assigned sites' : selectedLot?.name ?? 'Assigned parking lot'}</p>
                <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">Financial Reports</h1>
                <p className="mt-2 text-sm text-[var(--muted)]">Completed booking revenue and reconciliation overview.</p>
              </header>
              <div className="grid gap-4 sm:grid-cols-3">
                <FinanceMetric label="Completed bookings" value={completedBookings.length.toLocaleString('vi-VN')} />
                <FinanceMetric label="Recorded revenue" value={`${revenue.toLocaleString('vi-VN')} ₫`} />
                <FinanceMetric label="Bookings to reconcile" value={bookings.filter(booking => booking.status === 'completed' && !booking.paymentMethod).length.toLocaleString('vi-VN')} />
              </div>
              <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
                <table className="w-full min-w-[600px] text-left text-sm">
                  <thead className="border-b border-[var(--border)] text-[var(--muted)]"><tr><th className="px-4 py-3">Booking</th><th className="px-4 py-3">Vehicle</th><th className="px-4 py-3">Payment</th><th className="px-4 py-3 text-right">Amount</th></tr></thead>
                  <tbody className="divide-y divide-[var(--border)]">
                    {completedBookings.map(booking => <tr key={booking.id}><td className="px-4 py-3 text-[var(--fg)]">{booking.id}</td><td className="px-4 py-3 text-[var(--fg)]">{booking.licensePlate}</td><td className="px-4 py-3 text-[var(--muted)]">{booking.paymentMethod ? <PaymentMethodLogo method={booking.paymentMethod} size="xs" showName /> : 'Pending reconciliation'}</td><td className="px-4 py-3 text-right font-medium text-[var(--fg)]">{booking.amount.toLocaleString('vi-VN')} ₫</td></tr>)}
                    {!completedBookings.length && <tr><td colSpan={4} className="px-4 py-8 text-center text-[var(--muted)]">No completed bookings to report.</td></tr>}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </div>
      </main>
    </DashboardSidebar>
  );
}

function FinanceMetric({ label, value }: { label: string; value: string }) {
  return <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5"><p className="text-sm text-[var(--muted)]">{label}</p><p className="mt-2 text-xl font-bold text-[var(--fg)]">{value}</p></div>;
}

function CashierPOS({ lotName }: { lotName: string }) {
  const [plate, setPlate] = useState('');
  const [amount, setAmount] = useState('');
  const [notice, setNotice] = useState('');

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!plate.trim() || Number(amount) <= 0) {
      setNotice('Enter a license plate and a payment amount greater than zero.');
      return;
    }
    console.log('Mock cashier payment', { plate: plate.trim().toUpperCase(), amount: Number(amount), lotName });
    setNotice('Payment recorded in this frontend preview.');
    setPlate('');
    setAmount('');
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-60px)] w-full max-w-xl items-center px-5 py-10">
      <section className="w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-sm sm:p-8">
        <p className="text-sm font-medium text-[var(--primary)]">{lotName}</p>
        <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">Cashier Checkout</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">Record a parking payment. This role has a focused checkout screen without dashboard navigation.</p>
        <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
          <label className="block space-y-2 text-sm font-medium text-[var(--fg)]">License plate
            <input className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)]" value={plate} onChange={event => setPlate(event.target.value)} placeholder="e.g. 51A-123.45" required />
          </label>
          <label className="block space-y-2 text-sm font-medium text-[var(--fg)]">Amount (₫)
            <input className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)]" type="number" min="1" value={amount} onChange={event => setAmount(event.target.value)} placeholder="Enter amount" required />
          </label>
          {notice && <p role="status" className="text-sm text-[var(--muted)]">{notice}</p>}
          <button type="submit" className="btn-primary w-full justify-center">Collect Payment</button>
        </form>
      </section>
    </main>
  );
}
