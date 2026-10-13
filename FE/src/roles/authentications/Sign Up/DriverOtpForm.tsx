import { useEffect, useState } from "react"

import { authApi, type DriverRegistration } from "../../../lib/authApi"

export const pendingDriverKey = "sp_pending_driver_registration"

export function restoreDriverRegistration(): DriverRegistration | null {
  try {
    const value = JSON.parse(localStorage.getItem(pendingDriverKey) ?? "null")

    return value &&
      typeof value.registrationId === "string" &&
      ["email", "sms"].includes(value.channel) &&
      Number.isFinite(Date.parse(value.expiresAt)) &&
      Number.isFinite(Date.parse(value.resendAvailableAt))
      ? value
      : null
  } catch {
    return null
  }
}

export function DriverOtpForm({
  registration,
  onUpdate,
  onVerified,
  onBack,
  onDifferentContact,
}: {
  registration: DriverRegistration
  onUpdate: (value: DriverRegistration) => void

  onVerified: () => void
  onBack: () => void
  onDifferentContact: () => void
}) {
  const [code, setCode] = useState("")

  const [error, setError] = useState("")

  const [message, setMessage] = useState("")

  const [busy, setBusy] = useState(false)

  const [now, setNow] = useState(Date.now())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])

  const resendSeconds = Math.max(
    0,
    Math.ceil((Date.parse(registration.resendAvailableAt) - now) / 1000),
  )

  const expired = Date.parse(registration.expiresAt) <= now

  async function verify(e: React.FormEvent) {
    e.preventDefault()

    if (busy) return

    setBusy(true)
    setError("")
    setMessage("")

    try {
      await authApi.verifyDriver(registration.registrationId, code)
      onVerified()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Verification failed.")
    } finally {
      setBusy(false)
    }
  }

  async function resend() {
    if (busy || resendSeconds) return

    setBusy(true)
    setError("")
    setMessage("")

    try {
      onUpdate(await authApi.resendDriver(registration.registrationId))
      setCode("")
      setMessage("A new code is queued for delivery.")
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cannot resend code.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <form
      className="card auth-form-background"
      onSubmit={verify}
      style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
    >
      <h2 style={{ margin: 0 }}>Verify your account</h2>
      <p style={{ margin: 0 }}>
        Enter the 6-digit code delivered by{" "}
        {registration.channel === "email" ? "email" : "SMS"}. Delivery may take
        a moment. The code is valid for 5 minutes.
      </p>
      <label className="label" htmlFor="driver-otp">
        Verification code
      </label>
      <input
        id="driver-otp"
        className="input"
        value={code}
        onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
        inputMode="numeric"
        autoComplete="one-time-code"
        pattern="[0-9]{6}"
        maxLength={6}
        required
        disabled={busy}
      />
      {expired && (
        <p role="status">Your code has expired. Request a new code below.</p>
      )}
      {error && (
        <p role="alert" style={{ color: "#ef4444", margin: 0 }}>
          {error}
        </p>
      )}
      {message && <p role="status">{message}</p>}
      <button
        type="submit"
        className="btn-primary"
        disabled={busy || expired || code.length !== 6}
      >
        {busy ? "Please wait…" : "Verify Account"}
      </button>
      <button
        type="button"
        className="btn-outline"
        disabled={busy || resendSeconds > 0}
        onClick={resend}
      >
        {resendSeconds ? `Resend in ${resendSeconds}s` : "Resend Code"}
      </button>
      <button
        type="button"
        className="btn-outline"
        onClick={onBack}
        disabled={busy}
      >
        Back to Sign In
      </button>
      <button
        type="button"
        className="btn-outline"
        onClick={onDifferentContact}
        disabled={busy}
      >
        Register with a different contact
      </button>
    </form>
  )
}
