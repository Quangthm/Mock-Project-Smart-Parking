import { useEffect, useState } from "react"

import { request } from "../../../lib/authApi"
import { PageHeader } from "../../../components/ui/PageHeader"

import type { OwnerApplicationRecord } from "../../../lib/ownerApi"

export function OwnerVerification({
  application,
  onVerified,
  onUpdate,
  onBack,
}: {
  application: Pick<OwnerApplicationRecord, "email" | "verification">
  onVerified: () => void
  onUpdate: (verification: NonNullable<OwnerApplicationRecord["verification"]>) => void
  onBack: () => void
}) {
  const [code, setCode] = useState("")
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const [now, setNow] = useState(Date.now())

  const challenge = application.verification
  const resendSeconds = challenge
    ? Math.max(0, Math.ceil((Date.parse(challenge.resendAt) - now) / 1000))
    : 0
  const expired = !!challenge && Date.parse(challenge.expiresAt) <= now

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

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
    if (!challenge || busy || expired) return
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
    if (!challenge || busy || resendSeconds > 0) return
    setBusy(true)
    setMessage("")
    try {
      const response = await request<{
        success: boolean
        data: NonNullable<OwnerApplicationRecord["verification"]>
      }>("/register/owner/resend", "POST", {
        registrationId: challenge.challengeId,
      })
      if (!response.success || !response.data?.challengeId)
        throw new Error("Could not resend the code.")
      onUpdate(response.data)
      setCode("")
      setMessage("A new code is queued.")
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Retry failed.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card flex flex-col gap-5 p-6 text-center sm:p-8" onSubmit={verify}>
      <PageHeader title="Verify email" variant="compact" align="center" description="Your parking lot setup is saved. Enter the code sent to your email to submit your application for Admin review." />
      <p className="m-0 break-words font-semibold">{application.email}</p>
      <div className="flex flex-col gap-2 text-left">
        <label className="label" htmlFor="owner-code">
          Verification code
        </label>
        <input
          id="owner-code"
          className="input w-full text-center"
          required
          pattern="[0-9]{6}"
          maxLength={6}
          inputMode="numeric"
          autoComplete="one-time-code"
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
          disabled={busy}
        />
      </div>
      {expired && <p role="status">Your code has expired. Request a new code below.</p>}
      {message && <p role="status">{message}</p>}
      <div className="flex flex-wrap items-center justify-center gap-3 pt-1">
        <button className="btn-primary min-h-11 basis-full justify-center sm:basis-auto sm:flex-1" disabled={busy || expired || code.length !== 6}>
          {busy ? "Please wait…" : "Verify"}
        </button>
        <button
          type="button"
          className="btn-outline min-h-11 basis-full justify-center sm:basis-auto sm:flex-1"
          disabled={busy || resendSeconds > 0}
          onClick={() => void resend()}
        >
          {resendSeconds ? `Resend in ${resendSeconds}s` : "Resend code"}
        </button>
        <button type="button" className="btn-outline min-h-11 basis-full justify-center sm:basis-auto sm:flex-1" disabled={busy} onClick={onBack}>
          Back to Sign In
        </button>
      </div>
    </form>
  )
}
