import { useState } from 'react';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import type { PaymentTransactionRecord } from '../../../lib/structureTypes';
import { PaymentMethodLogo } from '../../../components/payment/PaymentMethodLogo';

interface PaymentDetailModalProps {
  transaction: PaymentTransactionRecord | null;
  onClose: () => void;
  onRetryPayment: (transactionId: string, newMethod: PaymentTransactionRecord['paymentMethod']) => void;
}

export function PaymentDetailModal({
  transaction,
  onClose,
  onRetryPayment,
}: PaymentDetailModalProps) {
  const [isRetrying, setIsRetrying] = useState(false);
  const [selectedMethod, setSelectedMethod] = useState<PaymentTransactionRecord['paymentMethod']>('qr');
  const [isProcessing, setIsProcessing] = useState(false);
  const [copied, setCopied] = useState(false);

  if (!transaction) return null;

  const handleCopy = (text: string) => {
    navigator.clipboard?.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleExecuteRetry = () => {
    setIsProcessing(true);
    setTimeout(() => {
      onRetryPayment(transaction.id, selectedMethod);
      setIsProcessing(false);
      setIsRetrying(false);
    }, 600);
  };

  const getStatusBadge = (status: PaymentTransactionRecord['paymentStatus']) => {
    switch (status) {
      case 'paid':
        return {
          bg: '#22c55e18',
          border: '#22c55e',
          text: '#16a34a',
          label: 'Paid (Thành công)',
          icon: 'check-circle',
        };
      case 'pending':
        return {
          bg: '#f59e0b18',
          border: '#f59e0b',
          text: '#d97706',
          label: 'Pending (Đang chờ xử lý)',
          icon: 'clock',
        };
      case 'failed':
        return {
          bg: '#ef444418',
          border: '#ef4444',
          text: '#dc2626',
          label: 'Failed (Thất bại)',
          icon: 'alert',
        };
      case 'refunded':
        return {
          bg: '#8b5cf618',
          border: '#8b5cf6',
          text: '#7c3aed',
          label: 'Refunded (Đã hoàn tiền)',
          icon: 'arrow-left',
        };
    }
  };

  const getMethodLabel = (method: PaymentTransactionRecord['paymentMethod']) => {
    switch (method) {
      case 'qr':
        return 'QR Code Payment (VietQR)';
      case 'momo':
        return 'MoMo E-Wallet';
      case 'vnpay':
        return 'VNPAY Gateway';
      case 'visa':
        return 'Credit / Debit Card';
      case 'applepay':
        return 'Apple Pay';
      case 'zalopay':
        return 'ZaloPay';
      default:
        return method;
    }
  };

  const badge = getStatusBadge(transaction.paymentStatus);

  return (
    <div
      role="presentation"
      onMouseDown={e => {
        if (e.target === e.currentTarget) onClose();
      }}
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '1rem',
        background: 'rgba(2, 6, 23, 0.65)',
        backdropFilter: 'blur(4px)',
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="payment-modal-title"
        className="card"
        style={{
          width: '100%',
          maxWidth: 520,
          background: 'var(--bg)',
          color: 'var(--fg)',
          border: '1px solid var(--border)',
          borderRadius: '1rem',
          boxShadow: '0 20px 50px rgba(0, 0, 0, 0.25)',
          padding: '1.5rem',
          maxHeight: '90vh',
          overflowY: 'auto',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1.25rem' }}>
          <div>
            <span style={{ fontSize: '0.72rem', fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.08em', textTransform: 'uppercase' }}>
              Transaction Details
            </span>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginTop: '0.2rem' }}>
              <h3 id="payment-modal-title" style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.35rem', margin: 0 }}>
                {transaction.id}
              </h3>
              <button
                type="button"
                onClick={() => handleCopy(transaction.id)}
                title="Copy Transaction ID"
                style={{
                  background: 'transparent',
                  border: 'none',
                  color: copied ? '#16a34a' : 'var(--muted)',
                  cursor: 'pointer',
                  fontSize: '0.75rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.2rem',
                }}
              >
                <UntitledIcon name={copied ? 'check' : 'clipboard'} size={14} />
                <span>{copied ? 'Copied' : 'Copy'}</span>
              </button>
            </div>
          </div>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            style={{
              width: 32,
              height: 32,
              borderRadius: '50%',
              border: '1px solid var(--border)',
              background: 'var(--card)',
              color: 'var(--fg)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <UntitledIcon name="x" size={16} />
          </button>
        </div>

        {/* Status & Amount Highlight Banner */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            padding: '1rem 1.25rem',
            background: 'var(--card)',
            borderRadius: '0.75rem',
            border: '1px solid var(--border)',
            marginBottom: '1.25rem',
          }}
        >
          <div>
            <span style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block' }}>Total Paid Amount</span>
            <div style={{ fontFamily: 'Outfit', fontSize: '1.5rem', fontWeight: 800, color: 'var(--fg)' }}>
              {transaction.amount.toLocaleString('vi-VN')} ₫
            </div>
          </div>
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.35rem',
              fontSize: '0.78rem',
              fontWeight: 700,
              padding: '0.35rem 0.75rem',
              borderRadius: '999px',
              background: badge.bg,
              color: badge.text,
              border: `1px solid ${badge.border}40`,
            }}
          >
            <UntitledIcon name={badge.icon} size={14} />
            {badge.label}
          </span>
        </div>

        {/* Failure Details & Retry Action */}
        {transaction.paymentStatus === 'failed' && (
          <div
            style={{
              padding: '1rem',
              borderRadius: '0.75rem',
              background: '#ef444412',
              border: '1px solid #ef444435',
              marginBottom: '1.25rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#dc2626', marginBottom: '0.4rem' }}>
              <UntitledIcon name="alert" size={16} />
              <strong style={{ fontSize: '0.875rem' }}>Payment Processing Error</strong>
            </div>
            <p style={{ margin: '0 0 0.85rem', fontSize: '0.82rem', color: 'var(--fg)', lineHeight: 1.5 }}>
              {transaction.failureReason || 'Transaction declined by the payment provider or timeout.'}
            </p>

            {isRetrying ? (
              <div style={{ display: 'grid', gap: '0.75rem', paddingTop: '0.5rem', borderTop: '1px solid #ef444430' }}>
                <label style={{ fontSize: '0.8rem', fontWeight: 600 }}>Select New Payment Method to Retry:</label>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '0.5rem' }}>
                  {[
                    { id: 'qr', label: 'VietQR / Banking' },
                    { id: 'momo', label: 'MoMo Wallet' },
                    { id: 'vnpay', label: 'VNPay Gateway' },
                    { id: 'visa', label: 'Visa / Mastercard' },
                    { id: 'applepay', label: 'Apple Pay' },
                    { id: 'zalopay', label: 'ZaloPay' },
                  ].map(method => (
                    <button
                      key={method.id}
                      type="button"
                      onClick={() => setSelectedMethod(method.id as any)}
                      style={{
                        padding: '0.5rem 0.6rem',
                        borderRadius: 'var(--radius)',
                        border: `1.5px solid ${selectedMethod === method.id ? 'var(--primary)' : 'var(--border)'}`,
                        background: selectedMethod === method.id ? 'color-mix(in srgb, var(--primary) 12%, var(--bg))' : 'var(--bg)',
                        color: 'var(--fg)',
                        fontSize: '0.8rem',
                        fontWeight: 600,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.45rem',
                      }}
                    >
                      <PaymentMethodLogo method={method.id} size="xs" />
                      <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{method.label}</span>
                    </button>
                  ))}
                </div>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.25rem' }}>
                  <button type="button" className="btn-outline" style={{ fontSize: '0.8rem', padding: '0.35rem 0.75rem' }} onClick={() => setIsRetrying(false)}>
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn-primary"
                    disabled={isProcessing}
                    style={{ fontSize: '0.8rem', padding: '0.35rem 0.85rem' }}
                    onClick={handleExecuteRetry}
                  >
                    {isProcessing ? 'Processing...' : 'Confirm & Pay Now'}
                  </button>
                </div>
              </div>
            ) : (
              <button
                type="button"
                className="btn-primary"
                style={{ fontSize: '0.82rem', padding: '0.45rem 0.95rem' }}
                onClick={() => setIsRetrying(true)}
              >
                <UntitledIcon name="banknote" size={15} /> Retry Payment
              </button>
            )}
          </div>
        )}

        {/* Refund Details */}
        {transaction.paymentStatus === 'refunded' && transaction.refundInfo && (
          <div
            style={{
              padding: '1rem',
              borderRadius: '0.75rem',
              background: '#8b5cf612',
              border: '1px solid #8b5cf635',
              marginBottom: '1.25rem',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#7c3aed', marginBottom: '0.4rem' }}>
              <UntitledIcon name="arrow-left" size={16} />
              <strong style={{ fontSize: '0.875rem' }}>Refund Completed</strong>
            </div>
            <div style={{ display: 'grid', gap: '0.35rem', fontSize: '0.82rem', color: 'var(--fg)' }}>
              <div>
                <span style={{ color: 'var(--muted)' }}>Refunded Amount:</span>{' '}
                <strong style={{ color: '#7c3aed' }}>{transaction.refundInfo.amount.toLocaleString('vi-VN')} ₫</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Refund Timestamp:</span>{' '}
                <span>{new Date(transaction.refundInfo.refundedAt).toLocaleString('vi-VN')}</span>
              </div>
              {transaction.refundInfo.reason && (
                <div>
                  <span style={{ color: 'var(--muted)' }}>Reason:</span>{' '}
                  <span>{transaction.refundInfo.reason}</span>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Transaction Key Value Pairs */}
        <div
          style={{
            display: 'grid',
            gap: '0.75rem',
            fontSize: '0.85rem',
            padding: '1rem',
            background: 'var(--card)',
            borderRadius: '0.75rem',
            border: '1px solid var(--border)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
            <span style={{ color: 'var(--muted)' }}>Booking Reference</span>
            <span style={{ fontWeight: 700, fontFamily: 'monospace' }}>{transaction.bookingId}</span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
            <span style={{ color: 'var(--muted)' }}>Parking Facility</span>
            <span style={{ fontWeight: 600, textAlign: 'right' }}>{transaction.lotName}</span>
          </div>

          {transaction.spaceCode && (
            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
              <span style={{ color: 'var(--muted)' }}>Allocated Space</span>
              <span style={{ fontWeight: 700, color: 'var(--primary)' }}>Slot {transaction.spaceCode}</span>
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
            <span style={{ color: 'var(--muted)' }}>Payment Method</span>
            <PaymentMethodLogo method={transaction.paymentMethod} size="sm" showName />
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
            <span style={{ color: 'var(--muted)' }}>Transaction Date</span>
            <span>{new Date(transaction.createdAt).toLocaleString('vi-VN')}</span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--muted)' }}>Driver Account ID</span>
            <span style={{ fontFamily: 'monospace', fontSize: '0.8rem', color: 'var(--muted)' }}>{transaction.driverId}</span>
          </div>
        </div>

        {/* Footer */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1.25rem' }}>
          <button
            type="button"
            className="btn-outline"
            style={{ fontSize: '0.8rem', padding: '0.4rem 0.85rem' }}
            onClick={() => window.print()}
          >
            <UntitledIcon name="file" size={14} /> Print Receipt
          </button>
          <button type="button" className="btn-primary" style={{ fontSize: '0.8rem' }} onClick={onClose}>
            Done
          </button>
        </div>
      </div>
    </div>
  );
}
