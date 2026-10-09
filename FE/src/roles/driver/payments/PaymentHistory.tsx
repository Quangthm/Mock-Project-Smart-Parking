import { useState, useMemo, useEffect } from 'react';
import { useApp } from '../../../context/AppContext';
import { paymentStore } from '../data/paymentStore';
import type { PaymentTransactionRecord, PaymentTransactionStatus } from '../../../lib/structureTypes';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import { PaymentDetailModal } from './PaymentDetailModal';
import { PaymentMethodLogo } from '../../../components/payment/PaymentMethodLogo';

export function PaymentHistory() {
  const { user } = useApp();
  const driverId = user?.id || 'usr-driver-001';

  const [transactions, setTransactions] = useState<PaymentTransactionRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);

  // Search & Filter State
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | PaymentTransactionStatus>('all');
  const [methodFilter, setMethodFilter] = useState<'all' | PaymentTransactionRecord['paymentMethod']>('all');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  // Selected for Modal
  const [selectedTxn, setSelectedTxn] = useState<PaymentTransactionRecord | null>(null);

  // Load Transactions
  const reloadData = () => {
    const data = paymentStore.getTransactions(driverId);
    setTransactions(data);
  };

  useEffect(() => {
    // Simulate brief realistic load
    const timer = setTimeout(() => {
      reloadData();
      setLoading(false);
    }, 150);

    const onTxnChange = () => reloadData();
    window.addEventListener('sp-payment-transactions-change', onTxnChange);
    return () => {
      clearTimeout(timer);
      window.removeEventListener('sp-payment-transactions-change', onTxnChange);
    };
  }, [driverId]);

  // Statistics
  const stats = useMemo(() => {
    const totalCount = transactions.length;
    const paidList = transactions.filter(t => t.paymentStatus === 'paid');
    const pendingList = transactions.filter(t => t.paymentStatus === 'pending');
    const failedList = transactions.filter(t => t.paymentStatus === 'failed');
    const refundedList = transactions.filter(t => t.paymentStatus === 'refunded');

    const totalPaidAmount = paidList.reduce((acc, curr) => acc + curr.amount, 0);

    return {
      totalCount,
      paidCount: paidList.length,
      pendingCount: pendingList.length,
      failedCount: failedList.length,
      refundedCount: refundedList.length,
      totalPaidAmount,
    };
  }, [transactions]);

  // Filtered list
  const filteredTransactions = useMemo(() => {
    return transactions.filter(item => {
      // Search
      if (searchQuery.trim()) {
        const query = searchQuery.trim().toLowerCase();
        const matchesId = item.id.toLowerCase().includes(query);
        const matchesBooking = item.bookingId.toLowerCase().includes(query);
        const matchesLot = item.lotName.toLowerCase().includes(query);
        const matchesSpace = item.spaceCode ? item.spaceCode.toLowerCase().includes(query) : false;
        if (!matchesId && !matchesBooking && !matchesLot && !matchesSpace) {
          return false;
        }
      }

      // Status
      if (statusFilter !== 'all' && item.paymentStatus !== statusFilter) {
        return false;
      }

      // Method
      if (methodFilter !== 'all' && item.paymentMethod !== methodFilter) {
        return false;
      }

      // Date Range
      if (dateFrom) {
        const fromTime = new Date(`${dateFrom}T00:00:00`).getTime();
        const itemTime = new Date(item.createdAt).getTime();
        if (itemTime < fromTime) return false;
      }

      if (dateTo) {
        const toTime = new Date(`${dateTo}T23:59:59`).getTime();
        const itemTime = new Date(item.createdAt).getTime();
        if (itemTime > toTime) return false;
      }

      return true;
    });
  }, [transactions, searchQuery, statusFilter, methodFilter, dateFrom, dateTo]);

  // Retry Handler
  const handleRetryPayment = (
    transactionId: string,
    newMethod: PaymentTransactionRecord['paymentMethod']
  ) => {
    const updated = paymentStore.retryPayment(transactionId, newMethod);
    if (updated) {
      reloadData();
      setSelectedTxn(updated);
      setFeedbackMessage(`Payment of ${updated.amount.toLocaleString('vi-VN')} ₫ via ${newMethod.toUpperCase()} completed successfully!`);
      setTimeout(() => setFeedbackMessage(null), 5000);
    }
  };

  const getStatusBadge = (status: PaymentTransactionStatus) => {
    switch (status) {
      case 'paid':
        return {
          bg: '#22c55e18',
          border: '#22c55e',
          text: '#16a34a',
          label: 'Paid',
          icon: 'check',
        };
      case 'pending':
        return {
          bg: '#f59e0b18',
          border: '#f59e0b',
          text: '#d97706',
          label: 'Pending',
          icon: 'clock',
        };
      case 'failed':
        return {
          bg: '#ef444418',
          border: '#ef4444',
          text: '#dc2626',
          label: 'Failed',
          icon: 'alert',
        };
      case 'refunded':
        return {
          bg: '#8b5cf618',
          border: '#8b5cf6',
          text: '#7c3aed',
          label: 'Refunded',
          icon: 'arrow-left',
        };
    }
  };

  const getMethodBadge = (method: PaymentTransactionRecord['paymentMethod']) => {
    switch (method) {
      case 'qr':
        return { icon: 'grid', label: 'VietQR' };
      case 'momo':
        return { icon: 'wallet', label: 'MoMo' };
      case 'vnpay':
        return { icon: 'credit-card', label: 'VNPay' };
      case 'visa':
        return { icon: 'credit-card', label: 'Card' };
      default:
        return { icon: 'banknote', label: method.toUpperCase() };
    }
  };

  const hasFilters = searchQuery || statusFilter !== 'all' || methodFilter !== 'all' || dateFrom || dateTo;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Title Header */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.2rem' }}>
          <span style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
            Driver Account · Billing & Payments
          </span>
        </div>
        <h2 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.35rem', margin: 0, color: 'var(--fg)' }}>
          Payment History
        </h2>
        <p style={{ margin: '0.25rem 0 0', fontSize: '0.85rem', color: 'var(--muted)' }}>
          Detailed record of your parking fee transactions, electronic receipts, refunds, and payment retry options.
        </p>
      </div>

      {/* Feedback Toast Banner */}
      {feedbackMessage && (
        <div
          role="status"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '0.75rem 1rem',
            background: '#22c55e18',
            border: '1px solid #22c55e',
            color: '#16a34a',
            borderRadius: '0.625rem',
            fontSize: '0.85rem',
            fontWeight: 600,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <UntitledIcon name="check-circle" size={16} />
            <span>{feedbackMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedbackMessage(null)}
            style={{ background: 'transparent', border: 'none', color: '#16a34a', cursor: 'pointer' }}
          >
            <UntitledIcon name="x" size={15} />
          </button>
        </div>
      )}

      {/* SECTION A: COMPACT SUMMARY STATISTICS */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.75rem' }}>
        {/* Total Transactions */}
        <div className="card" style={{ padding: '0.875rem 1rem' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <UntitledIcon name="clipboard" size={14} /> Total Transactions
          </div>
          <div style={{ fontFamily: 'Outfit', fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: 'var(--fg)' }}>
            {stats.totalCount}
          </div>
        </div>

        {/* Successful Payments */}
        <div className="card" style={{ padding: '0.875rem 1rem', borderColor: '#22c55e40' }}>
          <div style={{ fontSize: '0.75rem', color: '#16a34a', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <UntitledIcon name="check-circle" size={14} /> Successful Payments
          </div>
          <div style={{ fontFamily: 'Outfit', fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: '#16a34a' }}>
            {stats.paidCount}
          </div>
        </div>

        {/* Pending Payments */}
        <div className="card" style={{ padding: '0.875rem 1rem', borderColor: '#f59e0b40' }}>
          <div style={{ fontSize: '0.75rem', color: '#d97706', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <UntitledIcon name="clock" size={14} /> Pending Payments
          </div>
          <div style={{ fontFamily: 'Outfit', fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: '#d97706' }}>
            {stats.pendingCount}
          </div>
        </div>

        {/* Failed Payments */}
        <div className="card" style={{ padding: '0.875rem 1rem', borderColor: '#ef444440' }}>
          <div style={{ fontSize: '0.75rem', color: '#dc2626', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <UntitledIcon name="alert" size={14} /> Failed Payments
          </div>
          <div style={{ fontFamily: 'Outfit', fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: '#dc2626' }}>
            {stats.failedCount}
          </div>
        </div>

        {/* Total Amount Paid */}
        <div className="card" style={{ padding: '0.875rem 1rem', borderColor: 'var(--primary)' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--primary)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <UntitledIcon name="banknote" size={14} /> Total Amount Paid
          </div>
          <div style={{ fontFamily: 'Outfit', fontSize: '1.35rem', fontWeight: 800, marginTop: '0.25rem', color: 'var(--fg)' }}>
            {stats.totalPaidAmount.toLocaleString('vi-VN')} ₫
          </div>
        </div>
      </div>

      {/* SECTION C & D: SEARCH & FILTER CONTROLS */}
      <div className="card" style={{ padding: '1rem', display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'center', justifyContent: 'space-between' }}>
          {/* Search Box */}
          <div style={{ position: 'relative', flex: '1 1 240px', maxWidth: 360 }}>
            <span style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--muted)' }}>
              <UntitledIcon name="search" size={15} />
            </span>
            <input
              className="input"
              style={{ paddingLeft: '2.25rem', fontSize: '0.85rem' }}
              placeholder="Search by Transaction ID, Booking ID, or Lot..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
            />
          </div>

          {/* Payment Method Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.82rem' }}>
            <span style={{ color: 'var(--muted)', fontWeight: 600 }}>Method:</span>
            <select
              className="input"
              style={{ width: 'auto', fontSize: '0.82rem', padding: '0.4rem 0.65rem' }}
              value={methodFilter}
              onChange={e => setMethodFilter(e.target.value as any)}
            >
              <option value="all">All Methods</option>
              <option value="qr">VietQR Banking</option>
              <option value="momo">MoMo Wallet</option>
              <option value="vnpay">VNPay</option>
              <option value="visa">Visa / Mastercard</option>
              <option value="applepay">Apple Pay</option>
              <option value="zalopay">ZaloPay</option>
            </select>
          </div>

          {/* Date Range Inputs */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.82rem', flexWrap: 'wrap' }}>
            <span style={{ color: 'var(--muted)', fontWeight: 600 }}>Date:</span>
            <input
              type="date"
              className="input"
              style={{ width: 'auto', fontSize: '0.8rem', padding: '0.35rem 0.5rem' }}
              value={dateFrom}
              onChange={e => setDateFrom(e.target.value)}
              title="From date"
            />
            <span style={{ color: 'var(--muted)' }}>→</span>
            <input
              type="date"
              className="input"
              style={{ width: 'auto', fontSize: '0.8rem', padding: '0.35rem 0.5rem' }}
              value={dateTo}
              onChange={e => setDateTo(e.target.value)}
              title="To date"
            />
          </div>
        </div>

        {/* Status Filter Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem', borderTop: '1px solid var(--border)', paddingTop: '0.75rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--muted)', marginRight: '0.25rem' }}>Status:</span>
            {(['all', 'paid', 'pending', 'failed', 'refunded'] as const).map(st => {
              const active = statusFilter === st;
              const label = st === 'all' ? 'All' : st.charAt(0).toUpperCase() + st.slice(1);
              return (
                <button
                  key={st}
                  type="button"
                  onClick={() => setStatusFilter(st)}
                  style={{
                    padding: '0.3rem 0.7rem',
                    borderRadius: '999px',
                    fontSize: '0.78rem',
                    fontWeight: 600,
                    border: `1.5px solid ${active ? 'var(--primary)' : 'var(--border)'}`,
                    background: active ? 'var(--primary)' : 'transparent',
                    color: active ? 'var(--primary-fg)' : 'var(--fg)',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                >
                  {label}
                </button>
              );
            })}
          </div>

          {hasFilters && (
            <button
              type="button"
              className="btn-outline"
              style={{ fontSize: '0.75rem', padding: '0.25rem 0.65rem' }}
              onClick={() => {
                setSearchQuery('');
                setStatusFilter('all');
                setMethodFilter('all');
                setDateFrom('');
                setDateTo('');
              }}
            >
              Clear Filters
            </button>
          )}
        </div>
      </div>

      {/* SECTION B: PAYMENT TRANSACTION LIST */}
      <div className="card" style={{ padding: '0', overflow: 'hidden' }}>
        {loading ? (
          /* Loading Skeleton State */
          <div style={{ padding: '1.5rem', display: 'grid', gap: '1rem' }}>
            {[1, 2, 3, 4].map(n => (
              <div
                key={n}
                style={{
                  height: 48,
                  borderRadius: '0.5rem',
                  background: 'color-mix(in srgb, var(--border) 40%, transparent)',
                  animation: 'pulse 1.5s infinite ease-in-out',
                }}
              />
            ))}
          </div>
        ) : filteredTransactions.length > 0 ? (
          <>
            {/* Desktop Table View (Hidden on Small screens) */}
            <div className="hidden md:block" style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
                <thead>
                  <tr style={{ background: 'var(--card)', borderBottom: '1px solid var(--border)', color: 'var(--muted)', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Transaction ID</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Booking ID</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Parking Lot</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Date & Time</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Amount</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Method</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600 }}>Status</th>
                    <th style={{ padding: '0.85rem 1rem', fontWeight: 600, textAlign: 'right' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredTransactions.map(txn => {
                    const badge = getStatusBadge(txn.paymentStatus);
                    const methodInfo = getMethodBadge(txn.paymentMethod);

                    return (
                      <tr
                        key={txn.id}
                        style={{ borderBottom: '1px solid var(--border)', transition: 'background 0.15s' }}
                        className="hover:bg-[var(--card)]"
                      >
                        <td style={{ padding: '0.85rem 1rem', fontFamily: 'monospace', fontWeight: 700 }}>
                          {txn.id}
                        </td>
                        <td style={{ padding: '0.85rem 1rem', color: 'var(--muted)' }}>
                          {txn.bookingId}
                        </td>
                        <td style={{ padding: '0.85rem 1rem' }}>
                          <span style={{ fontWeight: 600, color: 'var(--fg)', display: 'block' }}>{txn.lotName}</span>
                          {txn.spaceCode && (
                            <span style={{ fontSize: '0.75rem', color: 'var(--primary)' }}>Slot {txn.spaceCode}</span>
                          )}
                        </td>
                        <td style={{ padding: '0.85rem 1rem', color: 'var(--muted)', whiteSpace: 'nowrap' }}>
                          <div>{new Date(txn.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div>
                          <div style={{ fontSize: '0.75rem' }}>{new Date(txn.createdAt).toLocaleDateString('vi-VN')}</div>
                        </td>
                        <td style={{ padding: '0.85rem 1rem', fontWeight: 700, fontFamily: 'Outfit', fontSize: '0.95rem' }}>
                          {txn.amount.toLocaleString('vi-VN')} ₫
                        </td>
                        <td style={{ padding: '0.85rem 1rem' }}>
                          <PaymentMethodLogo method={txn.paymentMethod} size="sm" showName />
                        </td>
                        <td style={{ padding: '0.85rem 1rem' }}>
                          <span
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '0.3rem',
                              fontSize: '0.72rem',
                              fontWeight: 700,
                              padding: '0.15rem 0.5rem',
                              borderRadius: '999px',
                              background: badge.bg,
                              color: badge.text,
                              border: `1px solid ${badge.border}40`,
                              textTransform: 'uppercase',
                            }}
                          >
                            <UntitledIcon name={badge.icon} size={12} />
                            {badge.label}
                          </span>
                        </td>
                        <td style={{ padding: '0.85rem 1rem', textAlign: 'right' }}>
                          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
                            <button
                              type="button"
                              className="btn-outline"
                              style={{ fontSize: '0.75rem', padding: '0.3rem 0.65rem' }}
                              onClick={() => setSelectedTxn(txn)}
                            >
                              Details
                            </button>
                            {txn.paymentStatus === 'failed' && (
                              <button
                                type="button"
                                className="btn-primary"
                                style={{ fontSize: '0.75rem', padding: '0.3rem 0.65rem' }}
                                onClick={() => setSelectedTxn(txn)}
                              >
                                Retry
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Mobile Card Stack View */}
            <div className="block md:hidden" style={{ display: 'grid', gap: '0.75rem', padding: '1rem' }}>
              {filteredTransactions.map(txn => {
                const badge = getStatusBadge(txn.paymentStatus);
                const methodInfo = getMethodBadge(txn.paymentMethod);

                return (
                  <div
                    key={txn.id}
                    onClick={() => setSelectedTxn(txn)}
                    style={{
                      border: '1px solid var(--border)',
                      borderRadius: '0.75rem',
                      background: 'var(--bg)',
                      padding: '1rem',
                      display: 'grid',
                      gap: '0.5rem',
                      cursor: 'pointer',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                      <div>
                        <span style={{ fontFamily: 'monospace', fontWeight: 700, fontSize: '0.88rem' }}>{txn.id}</span>
                        <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>Booking: {txn.bookingId}</div>
                      </div>
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '0.25rem',
                          fontSize: '0.7rem',
                          fontWeight: 700,
                          padding: '0.15rem 0.45rem',
                          borderRadius: '999px',
                          background: badge.bg,
                          color: badge.text,
                        }}
                      >
                        <UntitledIcon name={badge.icon} size={11} />
                        {badge.label}
                      </span>
                    </div>

                    <div style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--fg)' }}>
                      {txn.lotName} {txn.spaceCode ? `• Slot ${txn.spaceCode}` : ''}
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '0.25rem', borderTop: '1px solid var(--border)', paddingTop: '0.5rem' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.75rem', color: 'var(--muted)' }}>
                        <PaymentMethodLogo method={txn.paymentMethod} size="xs" showName />
                        <span>· {new Date(txn.createdAt).toLocaleDateString('vi-VN')}</span>
                      </div>
                      <div style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.05rem', color: 'var(--fg)' }}>
                        {txn.amount.toLocaleString('vi-VN')} ₫
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        ) : (
          /* Empty State */
          <div style={{ textAlign: 'center', padding: '3.5rem 1.5rem', color: 'var(--muted)' }}>
            <div style={{ color: 'var(--primary)', marginBottom: '0.75rem' }}>
              <UntitledIcon name="banknote" size={34} />
            </div>
            <h4 style={{ fontFamily: 'Outfit', fontSize: '1.1rem', margin: '0 0 0.35rem', color: 'var(--fg)' }}>
              No payment transactions found
            </h4>
            <p style={{ margin: 0, fontSize: '0.85rem' }}>
              {hasFilters ? 'No records match your selected filter parameters.' : 'You have not made any parking payments yet.'}
            </p>
            {hasFilters && (
              <button
                type="button"
                className="btn-outline"
                style={{ marginTop: '1rem', fontSize: '0.82rem' }}
                onClick={() => {
                  setSearchQuery('');
                  setStatusFilter('all');
                  setMethodFilter('all');
                  setDateFrom('');
                  setDateTo('');
                }}
              >
                Reset Filters
              </button>
            )}
          </div>
        )}
      </div>

      {/* SECTION E: PAYMENT DETAILS MODAL */}
      <PaymentDetailModal
        transaction={selectedTxn}
        onClose={() => setSelectedTxn(null)}
        onRetryPayment={handleRetryPayment}
      />
    </div>
  );
}
