import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function PaymentHistory() {
  return (
    <section className="space-y-5 text-[var(--fg)]">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--primary)]">Driver Account · Billing & Payments</p>
        <h2 className="mt-1 text-xl font-bold">Payment History</h2>
        <p className="mt-1 text-sm text-[var(--muted)]">Your parking payment records will appear here when available.</p>
      </div>
      <div className="card flex flex-col items-center px-6 py-12 text-center">
        <span className="flex size-12 items-center justify-center rounded-xl bg-[var(--primary)]/10 text-[var(--primary)]"><UntitledIcon name="banknote" size={24} /></span>
        <h3 className="mt-4 text-base font-semibold">No payment records available</h3>
        <p className="mt-1 max-w-md text-sm text-[var(--muted)]">Payment history is not available for your account yet. Please check back later.</p>
      </div>
    </section>
  )
}
