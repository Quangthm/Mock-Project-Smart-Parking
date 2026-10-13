import { useEffect, useState } from "react"

import { request } from "../../../lib/authApi"

import type { OwnerApplicationRecord } from "../../../lib/ownerApi"

export function OwnerVerification({
  application,
  onVerified,
}: {
  application: Pick<OwnerApplicationRecord, "email" | "verification">
  onVerified: () => void
}) {
  const [code, setCode] = useState("")
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")

  const challenge = application.verification

  useEffect(() => {
    let active = true
    request<{ data: { emailVerified: boolean } }>(
      "/register/owner/recover",
      "POST",
      { contact: application.email },
    )
      .then((r) => {
        if (!active) return
        if (r.data.emailVerified) onVerified()
      })
      .catch(() => {
        /* Existing challenges can still be retried. */
      })
    return () => {
      active = false
    }
  }, [application.email])

  async function verify(e: React.FormEvent) {
    e.preventDefault()
    if (!challenge) return
    setBusy(true)
    setMessage("")
    try {
      await request("/register/owner/verify", "POST", {
        challengeId: challenge.challengeId,
        code,
      })
      onVerified()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Verification failed.")
    } finally {
      setBusy(false)
    }
  }

  async function resend() {
    if (!challenge) return
    setBusy(true)
    try {
      await request("/register/owner/resend", "POST", {
        registrationId: challenge.challengeId,
      })
      setMessage("A new code is queued.")
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Retry failed.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card" onSubmit={verify}>
      <h2>Verify email</h2>
      <p>
        Enter the code sent to your email. After verification, your application
        will await Admin approval.
      </p>
      <label className="label" htmlFor="owner-code">
        Verification code
      </label>
      <input
        id="owner-code"
        className="input"
        required
        pattern="[0-9]{6}"
        maxLength={6}
        inputMode="numeric"
        autoComplete="one-time-code"
        value={code}
        onChange={(e) => setCode(e.target.value)}
        disabled={busy}
      />
      <p role="status">{message}</p>
      <button className="btn-primary" disabled={busy}>
        Verify
      </button>
      <button
        type="button"
        className="btn-outline"
        disabled={busy}
        onClick={() => void resend()}
      >
        Resend code
      </button>
    </form>
  )
}
