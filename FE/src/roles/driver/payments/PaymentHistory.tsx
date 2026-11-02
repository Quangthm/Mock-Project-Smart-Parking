import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { PaymentCheckout } from "./PaymentCheckout"

export function PaymentHistory() {
  return (
    <section className="space-y-5 text-[var(--fg)]">
      <PageHeader eyebrow="Driver Account · Billing & Payments" title="Payment History" variant="compact" description="Pay an existing order with payOS. Your payment records will appear here when available." />
      <PaymentCheckout />
      <div className="card flex flex-col items-center px-6 py-12 text-center">
        <span className="flex size-12 items-center justify-center rounded-xl bg-[var(--primary)]/10 text-[var(--primary)]"><UntitledIcon name="banknote" size={24} /></span>
        <h3 className="mt-4 text-base font-semibold">No payment records available</h3>
        <p className="mt-1 max-w-md text-sm text-[var(--muted)]">Payment history is not available for your account yet. Please check back later.</p>
      </div>
    </section>
  )
}
