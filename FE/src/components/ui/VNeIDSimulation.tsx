import { useEffect, useRef, useState } from "react"
import { motion, useReducedMotion } from "motion/react"
import { Modal } from "./Modal"

const steps = ["Introduction", "Document scan", "Face scan", "Review"] as const
const actions = {
  driver_registration: "Continue Registration",
  owner_registration: "Continue Parking Lot Setup",
  operator_activation: "Complete Operator Onboarding",
  account_settings: "Save demo completion",
}

type Step = 0 | 1 | 2 | 3
type State = { step: Step; status: "ready" | "scanning" | "passed" | "error" }

export function VNeIDSimulation({ context, fullName, onComplete, onCancel }: {
  context: keyof typeof actions
  fullName?: string
  onComplete: () => void
  onCancel: () => void
}) {
  const [state, setState] = useState<State>({ step: 0, status: "ready" })
  const [consent, setConsent] = useState(false)
  const completed = useRef(false)
  const heading = useRef<HTMLHeadingElement>(null)
  const reduced = useReducedMotion()
  const { step, status } = state
  useEffect(() => { if (step > 0) heading.current?.focus({ preventScroll: true }) }, [step])
  useEffect(() => {
    if (status !== "scanning") return
    const timer = window.setTimeout(() => setState(current => ({ ...current, status: "passed" })), reduced ? 50 : 1100)
    return () => window.clearTimeout(timer)
  }, [status, reduced])

  function advance() {
    if (step === 3) {
      if (!consent) {
        setState({ step, status: "error" })
        return
      }
      if (completed.current) return
      completed.current = true
      onComplete()
      return
    }
    if ((step === 1 || step === 2) && status !== "passed") {
      if (status !== "scanning") setState({ step, status: "scanning" })
      return
    }
    setState({ step: (step + 1) as Step, status: "ready" })
  }

  const isScan = step === 1 || step === 2
  const action = step === 3 ? actions[context] : status === "scanning" ? "Scanning…" :
    isScan && status !== "passed" ? (step === 1 ? "Simulate document scan" : "Simulate face scan") : "Continue"

  return <Modal title="VNeID Simulation — Demo Verification" onClose={onCancel}>
    <div className="space-y-5">
      <div>
        <p className="text-sm text-[var(--muted)]">Step {step + 1} of {steps.length} · {steps[step]}</p>
        <progress aria-label="Simulation progress" className="mt-2 block h-2 w-full accent-[var(--primary)]" value={step + 1} max={steps.length} />
      </div>
      <motion.section key={step} initial={reduced ? false : { opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: reduced ? 0 : 0.18 }}>
        <h3 ref={heading} tabIndex={-1} className="text-lg font-bold">
          {["Identity Verification", "Document verification", "Face verification", "Review simulation details"][step]}
        </h3>
        {step === 0 && <p className="mt-2 leading-relaxed">Walk through identity verification using a sample profile. This demo uses no official VNeID service, real ID or camera.</p>}
        {step === 3 && <>
          <p className="mt-2 text-sm text-[var(--muted)]">Both simulated scans passed. The date of birth and citizen ID below are demo values.</p>
          <dl className="my-4 grid gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 sm:grid-cols-2">
            <div className="sm:col-span-2"><dt className="text-sm text-[var(--muted)]">Full name</dt><dd>{fullName?.trim() || "Available after sign-in"}</dd></div>
            <div><dt className="text-sm text-[var(--muted)]">Date of birth</dt><dd>15 March 1995</dd></div>
            <div><dt className="text-sm text-[var(--muted)]">Citizen ID (demo)</dt><dd>DEMO-0001</dd></div>
          </dl>
          <label className="flex min-h-11 items-center gap-3"><input type="checkbox" checked={consent}
            aria-describedby={status === "error" ? "vneid-consent-error" : undefined}
            onChange={event => { setConsent(event.target.checked); setState({ step, status: "ready" }) }} />I have reviewed these simulation details.</label>
          {status === "error" && <p id="vneid-consent-error" role="alert" className="text-[var(--occupied)]">Confirm that you reviewed the details to continue.</p>}
        </>}
        {isScan && <>
          <p className="mt-2 text-[var(--muted)]">{step === 1 ? "Run a check on the sample card." : "Run a liveness check on a simulated face."}</p>
          <div aria-hidden="true" className={`relative mx-auto my-4 grid h-24 w-36 place-items-center overflow-hidden border border-[var(--border)] bg-[var(--bg)] ${step === 1 ? "rounded-xl" : "rounded-full"}`}>
            {step === 1 ? <span className="text-sm font-bold">DEMO ID</span> : <span className="text-3xl">☺</span>}
            {status === "scanning" && !reduced && <motion.span className="absolute inset-x-0 top-0 h-0.5 bg-[var(--primary)]"
              initial={{ y: 0 }} animate={{ y: 94 }} transition={{ duration: 1.05, ease: "easeInOut" }} />}
          </div>
          <p role="status" className="min-h-6 text-sm">{status === "scanning" ? "Running simulated check…" : status === "passed" ? "Demo check passed." : "Ready to scan."}</p>
        </>}
      </motion.section>
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="btn-outline min-h-11" onClick={() => {
          if (step === 0) return onCancel()
          setConsent(false)
          setState({ step: (step - 1) as Step, status: "ready" })
        }}>
          {step === 0 ? "Exit demo" : "Back"}
        </button>
        {isScan && status === "passed" && <button type="button" className="btn-outline min-h-11" onClick={() => setState({ step, status: "scanning" })}>Retry scan</button>}
        <button type="button" className="btn-primary min-h-11" disabled={status === "scanning"} onClick={advance}>{action}</button>
      </div>
    </div>
  </Modal>
}
