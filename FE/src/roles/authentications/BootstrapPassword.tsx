import { useState } from "react"
import { useNavigate } from "react-router-dom"

import { request } from "../../lib/authApi"
import { showUiToast } from "../../lib/uiToast"
import { BrandLogo, BrandWordmark } from "../../components/brand/BrandLogo"
import { useApp } from "../../context/AppContext"
import "./bootstrap-password.css"

export function BootstrapPassword() {
  const navigate = useNavigate()
  const { setView } = useApp()
  const [password, setPassword] = useState("")
  const [busy, setBusy] = useState(false)
  const [complete, setComplete] = useState(false)
  const [message, setMessage] = useState("")
  const params = new URLSearchParams(window.location.search)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    try {
      await request("/bootstrap-password", "POST", {
        challengeId: params.get("challengeId"),
        token: params.get("token"),
        password,
      })
      window.history.replaceState(null, "", "/change-password")
      setComplete(true)
      setMessage("Email verified and account activated. Sign in with your new password.")
      showUiToast("Account activated", "Your email is verified. You can sign in with your new password.")
      setPassword("")
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Password change failed.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="operator-activation-page">
      <section className="card operator-activation-card" aria-labelledby="operator-activation-title">
        <div className="operator-activation-brand">
          <BrandLogo height={42} />
          <BrandWordmark height={22} />
        </div>
        <div className="operator-activation-heading">
          <span className="operator-activation-eyebrow">Operator invitation</span>
          <h1 id="operator-activation-title">Activate your account</h1>
          <p>Choose a password to verify your email and start using your Operator workspace.</p>
        </div>
        <ol className="operator-activation-steps" aria-label="Activation steps">
          <li><span>01</span> Create password</li>
          <li><span>02</span> Activate account</li>
          <li><span>03</span> Sign in</li>
        </ol>
        <form onSubmit={submit} className="operator-activation-form">
          <label className="label" htmlFor="bootstrap-password">New password</label>
          <input
            className="input"
            id="bootstrap-password"
            type="password"
            autoComplete="new-password"
            aria-describedby="operator-password-hint"
            required
            minLength={8}
            maxLength={15}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={busy || complete}
          />
          <p id="operator-password-hint" className="operator-activation-hint">8–15 characters with uppercase and lowercase letters, a number, and a special character.</p>
          {message && <p role="status" className={`operator-activation-message${complete ? " is-complete" : ""}`}>{message}</p>}
          <div className="operator-activation-actions">
            <button type="submit" className="btn-primary" disabled={busy || complete}>
              {busy ? "Activating…" : complete ? "Account activated" : "Verify email and activate"}
            </button>
            <button type="button" className="btn-outline" onClick={() => { setView("sign-in"); navigate("/") }}>Sign in</button>
          </div>
        </form>
        <p className="operator-activation-footnote">Your invitation is linked to this email. You can sign in after activation.</p>
      </section>
    </main>
  )
}
