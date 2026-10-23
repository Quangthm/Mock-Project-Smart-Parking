import { useEffect, useState } from "react"
import { QRCodeSVG } from "qrcode.react"
import { useNavigate } from "react-router-dom"
import {
  authApi,
  authExpiredEvent,
  type MfaRecoveryCodes,
  type MfaSetup,
  type MfaStatus,
} from "../../lib/authApi"
import { MfaCodeInput } from "./MfaCodeInput"

type SecurityAction = "setup" | "disable" | "recovery-codes"

interface AccountSecurityPanelProps {
  onDismissBlockedChange?: (blocked: boolean) => void
}

export function AccountSecurityPanel({
  onDismissBlockedChange,
}: AccountSecurityPanelProps = {}) {
  const navigate = useNavigate()
  const [status, setStatus] = useState<MfaStatus | null>(null)
  const [action, setAction] = useState<SecurityAction | null>(null)
  const [setup, setSetup] = useState<MfaSetup | null>(null)
  const [recoveryCodes, setRecoveryCodes] = useState<MfaRecoveryCodes | null>(null)
  const [password, setPassword] = useState("")
  const [verificationCode, setVerificationCode] = useState("")
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)
  const [activationCode, setActivationCode] = useState("")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [savedCodes, setSavedCodes] = useState(false)

  useEffect(() => {
    const blocked = busy || recoveryCodes !== null
    onDismissBlockedChange?.(blocked)
    if (!blocked) return
    const preventLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ""
    }
    window.addEventListener("beforeunload", preventLeaving)
    return () => window.removeEventListener("beforeunload", preventLeaving)
  }, [busy, recoveryCodes, onDismissBlockedChange])

  useEffect(() => () => onDismissBlockedChange?.(false), [onDismissBlockedChange])

  useEffect(() => {
    let active = true
    authApi.mfaStatus()
      .then((result) => {
        if (active) setStatus(result)
      })
      .catch((failure) => {
        if (active)
          setError(failure instanceof Error ? failure.message : "Unable to load security settings.")
      })
    return () => {
      active = false
    }
  }, [])

  function startAction(next: SecurityAction) {
    setAction(next)
    setPassword("")
    setVerificationCode("")
    setUseRecoveryCode(false)
    setError("")
    setMessage("")
  }

  function showRecoveryCodes(result: MfaRecoveryCodes) {
    setRecoveryCodes(result)
    setSavedCodes(false)
    setSetup(null)
    setAction(null)
    setActivationCode("")
    setStatus({ enabled: true, recoveryCodesRemaining: result.recoveryCodes.length })
    sessionStorage.removeItem("sp_mfa_recovery_remaining")
  }

  function returnToSignIn() {
    authApi.clearSession()
    window.dispatchEvent(new Event(authExpiredEvent))
    navigate("/sign-in", { replace: true })
  }

  async function verifyAction(event: React.FormEvent) {
    event.preventDefault()
    if (!action || busy) return
    setBusy(true)
    setError("")
    const verification = {
      password,
      ...(status?.enabled && !useRecoveryCode ? { totp: verificationCode } : {}),
      ...(status?.enabled && useRecoveryCode ? { recoveryCode: verificationCode } : {}),
    }
    try {
      if (action === "setup") {
        setSetup(await authApi.setupMfa(verification))
        setAction(null)
        setActivationCode("")
        if (status?.enabled && useRecoveryCode)
          setStatus({ ...status, recoveryCodesRemaining: Math.max(0, status.recoveryCodesRemaining - 1) })
      } else if (action === "disable") {
        await authApi.disableMfa(verification)
        returnToSignIn()
      } else {
        showRecoveryCodes(await authApi.regenerateRecoveryCodes(verification))
      }
      setPassword("")
      setVerificationCode("")
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Security change failed.")
    } finally {
      setBusy(false)
    }
  }

  async function activate(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    setBusy(true)
    setError("")
    try {
      showRecoveryCodes(await authApi.enableMfa(activationCode))
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Activation failed.")
    } finally {
      setBusy(false)
    }
  }

  async function copy(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setMessage("Copied. Store this information in a safe place.")
    } catch {
      setMessage("Select the text and copy it manually.")
    }
  }

  function downloadRecoveryCodes() {
    if (!recoveryCodes) return
    const contents = "SmartPark recovery codes\nEach code can be used only once. Keep them private.\n\n" + recoveryCodes.recoveryCodes.join("\n")
    const url = URL.createObjectURL(new Blob([contents], { type: "text/plain" }))
    const link = document.createElement("a")
    link.href = url
    link.download = "smartpark-recovery-codes.txt"
    link.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  return (
    <section className="card" style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <h3 style={{ margin: 0 }}>Two-Factor Authentication (2FA)</h3>
      <p style={{ margin: 0, color: "var(--muted)" }}>
        Protect sign-in with a code from your authenticator app or a saved recovery code.
      </p>
      {(busy || recoveryCodes) && <p role="status">Complete this step and save your recovery codes before closing settings.</p>}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {!status && <p>{error ? "Close and reopen this panel to retry." : "Loading security settings…"}</p>}

      {status && !action && !setup && !recoveryCodes && (
        <>
          <p role="status">Status: <strong>{status.enabled ? "Enabled" : "Not enabled"}</strong></p>
          {status.enabled && <p>Unused recovery codes: {status.recoveryCodesRemaining}</p>}
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
            <button className="btn-primary" onClick={() => startAction("setup")}>
              {status.enabled ? "Replace authenticator" : "Set up 2FA"}
            </button>
            {status.enabled && (
              <>
                <button className="btn-outline" onClick={() => startAction("recovery-codes")}>Generate new recovery codes</button>
                <button className="btn-outline" onClick={() => startAction("disable")}>Turn off 2FA</button>
              </>
            )}
          </div>
        </>
      )}

      {action && (
        <form onSubmit={verifyAction} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <p>{action === "disable"
            ? "Turning off 2FA removes your authenticator and all recovery codes. You will need to sign in again."
            : action === "recovery-codes"
              ? "Generating new recovery codes invalidates every code in your previous set."
              : status?.enabled
                ? "Your current authenticator remains active until the new one is verified."
                : "Confirm your password to start setup."}</p>
          <label className="label">Current password
            <input className="input" type="password" autoComplete="current-password" required
              disabled={busy} value={password} onChange={(event) => setPassword(event.target.value)} />
          </label>
          {status?.enabled && <MfaCodeInput recovery={useRecoveryCode} value={verificationCode}
            onChange={setVerificationCode} onMethodChange={setUseRecoveryCode} disabled={busy} />}
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button type="button" className="btn-outline" disabled={busy} onClick={() => {
              setAction(null)
              setPassword("")
              setVerificationCode("")
              setError("")
            }}>Cancel</button>
            <button className="btn-primary" disabled={busy}>{busy ? "Verifying…" : "Confirm"}</button>
          </div>
        </form>
      )}

      {setup && (
        <form onSubmit={activate} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <p>Scan this QR with Google Authenticator or Microsoft Authenticator, or enter the full security key manually. Setup expires in 10 minutes.</p>
          <div style={{ background: "white", padding: "1rem", width: "fit-content", maxWidth: "100%" }}>
            <QRCodeSVG value={setup.uri} size={192} title="Scan to add SmartPark to your authenticator" />
          </div>
          <code style={{ overflowWrap: "anywhere", userSelect: "all" }}>{setup.secret}</code>
          <button type="button" className="btn-outline" onClick={() => void copy(setup.secret)}>Copy security key</button>
          <label className="label">Code from the new authenticator
            <input className="input" required pattern="[0-9]{6}" maxLength={6} inputMode="numeric"
              autoComplete="one-time-code" disabled={busy} value={activationCode}
              onChange={(event) => setActivationCode(event.target.value)} />
          </label>
          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button type="button" className="btn-outline" disabled={busy} onClick={() => {
              setSetup(null)
              setActivationCode("")
              setError("")
              setMessage("")
            }}>Cancel</button>
            <button className="btn-primary" disabled={busy}>{busy ? "Activating…" : "Verify and activate"}</button>
          </div>
        </form>
      )}

      {recoveryCodes && (
        <>
          <p role="status">2FA is enabled. Save these 8 recovery codes now. They are shown only once, and each code works only once.</p>
          <pre style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", userSelect: "all", fontSize: "0.8rem" }}>
            {recoveryCodes.recoveryCodes.join("\n")}
          </pre>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
            <button className="btn-outline" onClick={() => void copy(recoveryCodes.recoveryCodes.join("\n"))}>Copy codes</button>
            <button className="btn-outline" onClick={downloadRecoveryCodes}>Download codes</button>
          </div>
          <label><input type="checkbox" checked={savedCodes} onChange={(event) => setSavedCodes(event.target.checked)} /> I saved my recovery codes.</label>
          <button className="btn-primary" disabled={!savedCodes} onClick={() => {
            const signInRequired = recoveryCodes.signInRequired
            setRecoveryCodes(null)
            setMessage("")
            if (signInRequired) returnToSignIn()
          }}>{recoveryCodes.signInRequired ? "Continue to sign-in" : "Done"}</button>
        </>
      )}
    </section>
  )
}
