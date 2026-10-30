import { useEffect, useState } from "react"
import { ownerApi } from "../../../lib/ownerApi"
import { ensureOwnerRegistrationStorage, validOwnerChallenge, type PendingOwnerRegistration } from "../../../lib/ownerRegistration"

export function OwnerRegistrationRecovery({ registration, onRecovered, onBack }: {
  registration: PendingOwnerRegistration
  onRecovered: (registration: PendingOwnerRegistration) => void
  onBack: () => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [challenge, setChallenge] = useState(validOwnerChallenge(registration.setupRecovery) ? registration.setupRecovery : undefined)
  const [code, setCode] = useState("")
  const [now, setNow] = useState(Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])
  const resendSeconds = challenge ? Math.max(0, Math.ceil((Date.parse(challenge.resendAt) - now) / 1000)) : 0
  const expired = !!challenge && Date.parse(challenge.expiresAt) <= now

  async function recover(resend = false) {
    if (busy || (resend && resendSeconds > 0)) return
    setBusy(true)
    setError("")
    try {
      ensureOwnerRegistrationStorage()
      const pending = resend ? await ownerApi.requestSetupRecovery(registration.email) : await ownerApi.recover(registration.email)
      onRecovered(pending)
      setChallenge(pending.setupRecovery)
      setCode("")
      setNow(Date.now())
    } catch (error) { setError(error instanceof Error ? error.message : "Could not recover registration.") }
    finally { setBusy(false) }
  }

  async function verify(event: React.FormEvent) {
    event.preventDefault()
    if (busy || !challenge || expired) return
    setBusy(true)
    setError("")
    try {
      ensureOwnerRegistrationStorage()
      onRecovered(await ownerApi.completeSetupRecovery(registration.email, challenge.challengeId, code))
    } catch (error) { setError(error instanceof Error ? error.message : "Recovery failed.") }
    finally { setBusy(false) }
  }

  return <div className="card text-center owner-recovery-card">
    <h2>{challenge ? "Verify email to resume setup" : "Resume your Business registration"}</h2>
    <p style={{ overflowWrap: "anywhere" }}>{registration.email}</p>
    {challenge ? <form className="owner-recovery-form" onSubmit={verify}>
      <p>Enter the 6-digit code sent to your registered email. This restores access to your parking lot setup.</p>
      <label className="label" htmlFor="owner-recovery-code">Recovery code</label>
      <input id="owner-recovery-code" className="input text-center" value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
        required pattern="[0-9]{6}" inputMode="numeric" autoComplete="one-time-code" disabled={busy} />
      {expired && <p role="status">This code has expired. Request a new code.</p>}
      <div className="owner-recovery-actions">
        <button className="btn-primary" type="submit" disabled={busy || expired || code.length !== 6}>{busy ? "Checking…" : "Verify and continue setup"}</button>
        <button className="btn-outline" type="button" disabled={busy || resendSeconds > 0} onClick={() => recover(true)}>
          {resendSeconds > 0 ? `Resend in ${resendSeconds}s` : "Resend recovery code"}
        </button>
      </div>
    </form> : <>
      <p>Your setup credential is missing or expired. We will check your registration and email you a recovery code if setup is incomplete.</p>
      <button className="btn-primary" disabled={busy} onClick={() => recover()}>{busy ? "Checking…" : "Check registration status"}</button>
    </>}
    <div className="owner-recovery-actions">
      {challenge && <button className="btn-outline" disabled={busy} onClick={() => recover()}>Check saved registration</button>}
      <button className="btn-outline" disabled={busy} onClick={onBack}>Back to Sign In</button>
    </div>
    {error && <p role="alert">{error}</p>}
  </div>
}
