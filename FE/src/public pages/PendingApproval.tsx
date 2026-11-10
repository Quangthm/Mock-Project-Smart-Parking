import { PageHeader } from "../components/ui/PageHeader"
import { useApp } from "../context/AppContext"
import { UntitledIcon } from "../components/icon/UntitledIcon"

const steps = [
  { number: "01", label: "Business and lot setup", description: "Your registration and parking lot details have been submitted.", done: true },
  { number: "02", label: "Admin review", description: "An Admin checks your verified contact, parking lot, model and agreement.", done: false },
  { number: "03", label: "Owner access", description: "Once approved, sign in to manage your parking lot.", done: false },
]

export function PendingApproval() {
  const { setView } = useApp()

  return (
    <div className="mx-auto flex min-h-[80vh] w-full max-w-2xl flex-col items-center justify-center px-4 py-12 text-center sm:px-6">
      <div className="flex size-16 items-center justify-center rounded-full border border-amber-400/40 bg-amber-400/15 text-amber-700 dark:text-amber-300">
        <UntitledIcon name="clock" size={26} />
      </div>
      <div className="mt-6">
        <PageHeader variant="default" align="center" title="Application Submitted!" description="Your email is verified and your parking lot setup has been submitted. Your application is now under Admin review." />
      </div>

      <section className="card mt-8 w-full p-5 text-left sm:p-7" aria-labelledby="application-next-steps">
        <h2 id="application-next-steps" className="text-base font-semibold text-[var(--fg)]">What happens next?</h2>
        <ol className="mt-5 grid gap-5">
          {steps.map((step) => (
            <li key={step.number} className="flex items-start gap-4">
              <span className={`flex size-9 shrink-0 items-center justify-center rounded-full border text-xs font-bold ${step.done ? "border-[var(--primary)] bg-[var(--primary)] text-[var(--primary-fg)]" : "border-[var(--border)] text-[var(--muted)]"}`}>
                {step.done ? <UntitledIcon name="check" size={17} /> : step.number}
              </span>
              <div className="min-w-0 space-y-1">
                <h3 className="text-sm font-semibold leading-6 text-[var(--fg)]">{step.label}</h3>
                <p className="text-sm leading-6 text-[var(--muted)]">{step.description}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <p className="mt-6 w-full rounded-2xl border border-[var(--border)] bg-[var(--card)] px-5 py-4 text-left text-sm leading-6 text-[var(--muted)]">
        <strong className="text-[var(--primary)]">Please note:</strong> Your account remains inactive until Admin approval. You can sign in once your application has been approved.
      </p>

      <div className="mt-7 flex w-full flex-wrap justify-center gap-3">
        <button type="button" className="btn-primary" onClick={() => setView("landing")}>Back to Home</button>
        <button type="button" className="btn-outline" onClick={() => setView("support")}>Contact Support</button>
      </div>
    </div>
  )
}
