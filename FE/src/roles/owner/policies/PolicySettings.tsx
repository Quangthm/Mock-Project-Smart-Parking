import { useEffect, useState } from 'react';
import { useApp } from '../../../context/AppContext';
import { store } from '../../../lib/store';

type DepositMode = 'percentage' | 'fixed';

interface PolicyState {
  pricingEnabled: boolean;
  dayRate: string;
  nightRate: string;
  depositEnabled: boolean;
  depositMode: DepositMode;
  depositValue: string;
}

interface PreviewState {
  total: number;
  deposit: number;
  checkout: number;
}

const initialPolicy: PolicyState = {
  pricingEnabled: true,
  dayRate: '15000',
  nightRate: '25000',
  depositEnabled: true,
  depositMode: 'percentage',
  depositValue: '20',
};

const inputClass = 'w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none transition focus:border-[var(--primary)] focus:ring-2 focus:ring-blue-500/20';

export function PolicySettings() {
  const { user } = useApp();
  const [policy, setPolicy] = useState<PolicyState>(() => user ? store.getOwnerPolicy(user.id) ?? initialPolicy : initialPolicy);
  const [dayHours, setDayHours] = useState('8');
  const [nightHours, setNightHours] = useState('2');
  const [preview, setPreview] = useState<PreviewState>({ total: 170000, deposit: 34000, checkout: 136000 });
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    const dayCost = policy.pricingEnabled ? (Number(policy.dayRate) || 0) * (Number(dayHours) || 0) : 0;
    const nightCost = policy.pricingEnabled ? (Number(policy.nightRate) || 0) * (Number(nightHours) || 0) : 0;
    const total = dayCost + nightCost;

    // Calculate the advance deposit as a percentage of the total or as a fixed amount.
    const requestedDeposit = !policy.depositEnabled
      ? 0
      : policy.depositMode === 'percentage'
        ? total * ((Number(policy.depositValue) || 0) / 100)
        : Number(policy.depositValue) || 0;
    const deposit = Math.min(total, Math.max(0, requestedDeposit));

    // Checkout collects the remaining balance after applying the advance deposit.
    const checkout = Math.max(0, total - deposit);
    setPreview({ total, deposit, checkout });
  }, [policy, dayHours, nightHours]);

  const updatePolicy = <K extends keyof PolicyState>(key: K, value: PolicyState[K]) => {
    setSaved(false);
    setPolicy(current => ({ ...current, [key]: value }));
  };

  function savePolicy() {
    if (!user) return;
    store.saveOwnerPolicy(user.id, policy);
    if (policy.pricingEnabled) {
      store.getLotsByOwner(user.id).forEach(lot => store.saveLot({ ...lot, hourlyRate: Number(policy.dayRate) || 0, nightRate: Number(policy.nightRate) || 0 }));
    }
    store.createNotification({ recipientId: user.id, recipientRole: 'owner', type: 'POLICY_UPDATED', title: 'Parking policy updated', message: 'The parking policy has been updated successfully.' });
    setSaved(true);
  }

  return (
    <section className="mx-auto w-full max-w-5xl space-y-6" aria-labelledby="policy-settings-title">
      <header>
        <p className="text-sm font-medium text-[var(--primary)]">Owner tools</p>
        <h1 id="policy-settings-title" className="mt-1 text-2xl font-bold text-[var(--fg)]">Policy Settings</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">Configure pricing and deposit rules, then preview the amount due for a sample stay.</p>
      </header>

      <section className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)]" aria-label="Pricing and deposit policy settings">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] border-collapse text-left">
            <thead>
              <tr className="border-b border-[var(--border)] bg-[var(--bg)]/50 text-xs uppercase tracking-wide text-[var(--muted)]">
                <th className="px-5 py-3 font-semibold">Policy</th>
                <th className="px-5 py-3 font-semibold">Enabled</th>
                <th className="px-5 py-3 font-semibold">Configuration</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              <tr>
                <td className="px-5 py-4 align-top">
                  <span className="block font-semibold text-[var(--fg)]">Pricing Policy</span>
                  <span className="mt-1 block text-xs text-[var(--muted)]">Day and night rates</span>
                </td>
                <td className="px-5 py-4 align-top">
                  <input type="checkbox" className="mt-1 h-4 w-4 accent-[var(--primary)]" aria-label="Enable pricing policy" checked={policy.pricingEnabled} onChange={event => updatePolicy('pricingEnabled', event.target.checked)} />
                </td>
                <td className="px-5 py-4">
                  {policy.pricingEnabled ? (
                    <div className="grid gap-3 sm:grid-cols-2">
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">Day Rate (₫ / hour)
                        <input className={inputClass} type="number" min="0" step="1000" value={policy.dayRate} onChange={event => updatePolicy('dayRate', event.target.value)} />
                      </label>
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">Night Rate (₫ / hour)
                        <input className={inputClass} type="number" min="0" step="1000" value={policy.nightRate} onChange={event => updatePolicy('nightRate', event.target.value)} />
                      </label>
                    </div>
                  ) : <span className="text-sm text-[var(--muted)]">Pricing is disabled</span>}
                </td>
              </tr>
              <tr>
                <td className="px-5 py-4 align-top">
                  <span className="block font-semibold text-[var(--fg)]">Deposit Policy</span>
                  <span className="mt-1 block text-xs text-[var(--muted)]">Advance payment</span>
                </td>
                <td className="px-5 py-4 align-top">
                  <input type="checkbox" className="mt-1 h-4 w-4 accent-[var(--primary)]" aria-label="Enable deposit policy" checked={policy.depositEnabled} onChange={event => updatePolicy('depositEnabled', event.target.checked)} />
                </td>
                <td className="px-5 py-4">
                  {policy.depositEnabled ? (
                    <div className="grid gap-3 sm:grid-cols-2">
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">Deposit type
                        <select className={inputClass} value={policy.depositMode} onChange={event => updatePolicy('depositMode', event.target.value as DepositMode)}>
                          <option value="percentage">Percentage (%)</option>
                          <option value="fixed">Fixed amount (₫)</option>
                        </select>
                      </label>
                      <label className="space-y-1.5 text-xs font-medium text-[var(--muted)]">{policy.depositMode === 'percentage' ? 'Deposit Percentage (%)' : 'Deposit Amount (₫)'}
                        <input className={inputClass} type="number" min="0" max={policy.depositMode === 'percentage' ? 100 : undefined} step={policy.depositMode === 'percentage' ? 1 : 1000} value={policy.depositValue} onChange={event => updatePolicy('depositValue', event.target.value)} />
                      </label>
                    </div>
                  ) : <span className="text-sm text-[var(--muted)]">Deposit is disabled</span>}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="border-t border-[var(--border)] p-5" aria-labelledby="policy-preview-title">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 id="policy-preview-title" className="text-lg font-semibold text-[var(--fg)]">Payment Preview</h2>
            <p className="mt-1 text-sm text-[var(--muted)]">Adjust the sample stay duration to preview the calculation.</p>
          </div>
          <span className="rounded-full bg-[var(--primary)]/10 px-3 py-1 text-xs font-semibold text-[var(--primary)]">Preview only</span>
        </div>
        <div className="mt-5 grid gap-4 sm:grid-cols-2">
          <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Day hours
            <input className={inputClass} type="number" min="0" step="1" value={dayHours} onChange={event => setDayHours(event.target.value)} />
          </label>
          <label className="space-y-2 text-sm font-medium text-[var(--fg)]">Night hours
            <input className={inputClass} type="number" min="0" step="1" value={nightHours} onChange={event => setNightHours(event.target.value)} />
          </label>
        </div>
        <div className="mt-5 grid gap-3 border-t border-[var(--border)] pt-4 sm:grid-cols-3">
          <PreviewAmount label="Estimated total" amount={preview.total} />
          <PreviewAmount label="Deposit due now" amount={preview.deposit} />
          <PreviewAmount label="Due at checkout" amount={preview.checkout} emphasized />
        </div>
        <div className="mt-4 flex items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
          {saved && <span role="status" className="text-sm font-medium text-green-600">Policy saved.</span>}
          <button type="button" className="btn-primary" onClick={savePolicy}>Save Policy</button>
        </div>
        <p className="mt-4 text-xs leading-5 text-[var(--muted)]">Total = (day rate × day hours) + (night rate × night hours). Deposit is capped at the total; checkout collects the remaining balance.</p>
        </div>
      </section>
    </section>
  );
}

function PreviewAmount({ label, amount, emphasized = false }: { label: string; amount: number; emphasized?: boolean }) {
  return (
    <div className="rounded-lg bg-[var(--bg)] p-4">
      <p className="text-sm text-[var(--muted)]">{label}</p>
      <p className={`mt-2 text-lg font-bold ${emphasized ? 'text-[var(--primary)]' : 'text-[var(--fg)]'}`}>{Math.round(amount).toLocaleString('vi-VN')} ₫</p>
    </div>
  );
}
