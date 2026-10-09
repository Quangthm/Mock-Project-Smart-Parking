import type { PaymentTransactionRecord, PaymentTransactionStatus } from '../../../lib/structureTypes';

const PAYMENTS_KEY = 'sp_driver_payment_transactions';

function buildInitialTransactions(driverId: string): PaymentTransactionRecord[] {
  const now = Date.now();
  return [
    {
      id: 'TXN-20261008-001',
      driverId,
      bookingId: 'BK-20261008-014',
      lotId: 'lot-002',
      lotName: 'Bitexco Financial Tower Outdoor Lot',
      spaceCode: 'A02',
      amount: 50000,
      paymentMethod: 'qr',
      paymentStatus: 'paid',
      createdAt: new Date(now - 1000 * 60 * 35).toISOString(),
    },
    {
      id: 'TXN-20261008-002',
      driverId,
      bookingId: 'BK-20261008-015',
      lotId: 'lot-001',
      lotName: 'Vinhomes Grand Park Parking',
      spaceCode: 'B12',
      amount: 30000,
      paymentMethod: 'momo',
      paymentStatus: 'failed',
      failureReason: 'Giao dịch bị từ chối: Số dư ví MoMo không đủ hoặc người dùng đã hủy thanh toán.',
      createdAt: new Date(now - 1000 * 60 * 180).toISOString(),
    },
    {
      id: 'TXN-20261008-003',
      driverId,
      bookingId: 'BK-20261008-016',
      lotId: 'lot-002',
      lotName: 'Bitexco Financial Tower Outdoor Lot',
      spaceCode: 'A08',
      amount: 70000,
      paymentMethod: 'vnpay',
      paymentStatus: 'pending',
      createdAt: new Date(now - 1000 * 60 * 15).toISOString(),
    },
    {
      id: 'TXN-20261007-088',
      driverId,
      bookingId: 'BK-20261007-077',
      lotId: 'lot-003',
      lotName: 'Nguyen Hue Boulevard Outdoor Lot',
      spaceCode: 'A15',
      amount: 40000,
      paymentMethod: 'visa',
      paymentStatus: 'paid',
      createdAt: new Date(now - 86400000 * 1 - 3600000 * 2).toISOString(),
    },
    {
      id: 'TXN-20261006-042',
      driverId,
      bookingId: 'BK-20261006-031',
      lotId: 'lot-001',
      lotName: 'Vinhomes Grand Park Parking',
      spaceCode: 'A05',
      amount: 60000,
      paymentMethod: 'vnpay',
      paymentStatus: 'refunded',
      createdAt: new Date(now - 86400000 * 2 - 3600000 * 5).toISOString(),
      refundInfo: {
        refundedAt: new Date(now - 86400000 * 2 - 1800000).toISOString(),
        amount: 60000,
        reason: 'Hủy đơn sớm trong thời gian miễn phí (Grace period 15 phút)',
      },
    },
    {
      id: 'TXN-20261005-019',
      driverId,
      bookingId: 'BK-20261005-010',
      lotId: 'lot-002',
      lotName: 'Bitexco Financial Tower Outdoor Lot',
      spaceCode: 'M03',
      amount: 20000,
      paymentMethod: 'qr',
      paymentStatus: 'paid',
      createdAt: new Date(now - 86400000 * 3 - 3600000 * 4).toISOString(),
    },
    {
      id: 'TXN-20261004-007',
      driverId,
      bookingId: 'BK-20261004-003',
      lotId: 'lot-002',
      lotName: 'Bitexco Financial Tower Outdoor Lot',
      spaceCode: 'A04',
      amount: 45000,
      paymentMethod: 'applepay',
      paymentStatus: 'paid',
      createdAt: new Date(now - 86400000 * 4 - 3600000 * 2).toISOString(),
    },
    {
      id: 'TXN-20261003-012',
      driverId,
      bookingId: 'BK-20261003-009',
      lotId: 'lot-001',
      lotName: 'Vinhomes Grand Park Parking',
      spaceCode: 'B08',
      amount: 35000,
      paymentMethod: 'zalopay',
      paymentStatus: 'paid',
      createdAt: new Date(now - 86400000 * 5 - 3600000 * 6).toISOString(),
    },
  ];
}

function loadPayments(): PaymentTransactionRecord[] {
  try {
    const raw = localStorage.getItem(PAYMENTS_KEY);
    if (raw) return JSON.parse(raw);
  } catch {
    // fallback
  }
  return [];
}

function savePayments(list: PaymentTransactionRecord[]) {
  try {
    localStorage.setItem(PAYMENTS_KEY, JSON.stringify(list));
    window.dispatchEvent(new Event('sp-payment-transactions-change'));
  } catch (err) {
    console.error('Failed to save payments', err);
  }
}

export const paymentStore = {
  getTransactions(driverId: string): PaymentTransactionRecord[] {
    const list = loadPayments();
    const initial = buildInitialTransactions(driverId);
    let updated = false;

    for (const init of initial) {
      if (!list.some(t => t.id === init.id)) {
        list.push(init);
        updated = true;
      }
    }

    if (updated) {
      savePayments(list);
    }

    const forDriver = list.filter(t => t.driverId === driverId);
    return forDriver.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  },

  retryPayment(
    transactionId: string,
    newMethod?: PaymentTransactionRecord['paymentMethod']
  ): PaymentTransactionRecord | null {
    const list = loadPayments();
    const index = list.findIndex(t => t.id === transactionId);
    if (index === -1) return null;

    const existing = list[index];
    const updated: PaymentTransactionRecord = {
      ...existing,
      paymentStatus: 'paid',
      paymentMethod: newMethod || existing.paymentMethod,
      failureReason: undefined,
      createdAt: new Date().toISOString(),
    };

    list[index] = updated;
    savePayments(list);
    return updated;
  },

  addTransaction(record: Omit<PaymentTransactionRecord, 'id' | 'createdAt'> & { id?: string; createdAt?: string }): PaymentTransactionRecord {
    const list = loadPayments();
    const newRecord: PaymentTransactionRecord = {
      ...record,
      id: record.id || `TXN-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(100 + Math.random() * 900)}`,
      createdAt: record.createdAt || new Date().toISOString(),
    };
    list.unshift(newRecord);
    savePayments(list);
    return newRecord;
  },
};
