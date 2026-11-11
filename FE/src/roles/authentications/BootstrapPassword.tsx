import { VNeIDSimulation } from "../../components/ui/VNeIDSimulation"
import { useRef, useState } from "react"
import { useNavigate } from "react-router-dom"

import { request } from "../../lib/authApi"
import { showUiToast } from "../../lib/uiToast"
import { BrandLogo, BrandWordmark } from "../../components/brand/BrandLogo"
import { PasswordVisibilityIcon } from "../../components/forms/PasswordVisibilityIcon"
import { useApp } from "../../context/AppContext"
import "./bootstrap-password.css"

export function BootstrapPassword() {
  const navigate = useNavigate()
  const { setView } = useApp()
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [busy, setBusy] = useState(false)
  const [activation, setActivation] = useState<"password" | "simulation" | "paused">("password")
  const complete = activation !== "password"
  const [message, setMessage] = useState("")
  const submitting = useRef(false)
  const params = new URLSearchParams(window.location.search)
  const hasInvitation = Boolean(params.get("challengeId") && params.get("token"))
  const passwordChecks = [
    { label: "At least 8 characters", passed: password.length >= 8 },
    { label: "Maximum 15 characters", passed: password.length <= 15 },
    { label: "One uppercase letter", passed: /[A-Z]/.test(password) },
    { label: "One lowercase letter", passed: /[a-z]/.test(password) },
    { label: "One number", passed: /\d/.test(password) },
    { label: "One special character", passed: /[^A-Za-z0-9]/.test(password) },
  ]
  const passwordReady = passwordChecks.every(check => check.passed)
  const passedChecks = passwordChecks.filter(check => check.passed).length
  const strengthColor = passedChecks <= 2 ? "#ef4444" : passedChecks <= 4 ? "#f59e0b" : "#22c55e"
  const currentStep = complete ? 3 : passwordReady || busy ? 2 : 1

  function goToSignIn() {
    setView("sign-in")
    navigate("/", { replace: true })
  }

  function finishOnboarding() {
    showUiToast("Account activated", "Your email is verified. You can sign in with your new password.")
    goToSignIn()
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (submitting.current || complete || !hasInvitation) return
    if (!passwordReady) {
      setMessage("Use 8–15 characters with uppercase and lowercase letters, a number, and a special character.")
      return
    }
    setMessage("")
    submitting.current = true
    setBusy(true)
    try {
      await request("/bootstrap-password", "POST", {
        challengeId: params.get("challengeId"),
        token: params.get("token"),
        password,
      })
      window.history.replaceState(null, "", "/change-password")
      setPassword("")
      setActivation("simulation")
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Password change failed.")
    } finally {
      submitting.current = false
      setBusy(false)
    }
  }

  return (
    <main className="operator-activation-page">
      {activation === "simulation" && <VNeIDSimulation context="operator_activation"
        onComplete={finishOnboarding} onCancel={() => setActivation("paused")} />}
      <section className="card operator-activation-card" aria-labelledby="operator-activation-title">
        <div className="operator-activation-brand">
          <BrandLogo height={42} />
          <BrandWordmark height={22} />
        </div>
        <div className="operator-activation-heading">
          <h1 id="operator-activation-title">Activate your Operator account</h1>
          <p>Choose a password to verify your email and start using your Operator workspace.</p>
        </div>
        <ol className="operator-activation-steps" aria-label="Activation steps">
          {["Create password", "Activate account", "Sign in"].map((label, index) => {
            const step = index + 1
            return <li key={label} className={step < currentStep ? "is-done" : step === currentStep ? "is-current" : "is-next"} aria-current={step === currentStep ? "step" : undefined}>
              <span className="operator-activation-step-number" aria-hidden="true">{step < currentStep ? "✓" : step}</span>
              <span>{label}</span>
            </li>
          })}
        </ol>
        {complete ? <div className="space-y-4" inert={activation === "simulation"}>
          <p role="status">Your password was created and your email was verified. Complete the demo to continue to Sign In.</p>
          <button type="button" className="btn-primary" onClick={() => setActivation("simulation")}>Resume identity demo</button>
        </div> : !hasInvitation ? <div className="space-y-4">
          <p role="status">Open the invitation link to activate your account. If you already created your password, sign in to continue.</p>
          <button type="button" className="btn-outline" onClick={goToSignIn}>Back to Sign In</button>
        </div> : <form onSubmit={submit} className="operator-activation-form">
          <label className="label" htmlFor="bootstrap-password">New password</label>
          <div className="operator-activation-password-field">
            <input
              className="input"
              id="bootstrap-password"
              name="newPassword"
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              aria-describedby="operator-password-hint"
              required
              minLength={8}
              maxLength={15}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={busy || complete}
            />
            <button type="button" className="operator-activation-password-toggle" aria-label={showPassword ? "Hide password" : "Show password"} aria-pressed={showPassword} onClick={() => setShowPassword(value => !value)} disabled={busy || complete}>
              <PasswordVisibilityIcon visible={showPassword} />
            </button>
          </div>
          <p id="operator-password-hint" className="operator-activation-hint">8–15 characters with uppercase and lowercase letters, a number, and a special character.</p>
          {password && <div className="operator-activation-strength" role="group" aria-label="Password requirements">
            <div className="operator-activation-strength-track" aria-hidden="true"><span style={{ width: `${passedChecks / passwordChecks.length * 100}%`, backgroundColor: strengthColor }} /></div>
            <ul>{passwordChecks.map(check => <li key={check.label} className={check.passed ? "is-met" : ""}><span aria-hidden="true">{check.passed ? "✓" : "○"}</span>{check.label}</li>)}</ul>
          </div>}
          {message && <p role="status" className={`operator-activation-message${complete ? " is-complete" : ""}`}>{message}</p>}
          <div className="operator-activation-actions">
            <button type="submit" className="btn-primary" disabled={busy || complete}>
              {busy ? "Activating…" : complete ? "Account activated" : "Verify email and activate"}
            </button>
          </div>
        </form>}
        <p className="operator-activation-footnote">Your invitation is linked to this email. You can sign in after activation.</p>
      </section>
    </main>
  )
}
