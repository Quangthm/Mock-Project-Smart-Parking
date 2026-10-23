import { useState } from "react"
import { useApp } from "../../../context/AppContext"
import { authData as store } from "../data/data"
import { useNavigate } from "react-router-dom"
import { getUserHomePath } from "../../operator/data/roleRoutes"
import { BrandLogo, BrandWordmark } from "../../../components/brand/BrandLogo"
import { PageHeader } from "../../../components/ui/PageHeader"
import { PasswordVisibilityIcon } from "../../../components/forms/PasswordVisibilityIcon"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { authApi, AuthApiError, request } from "../../../lib/authApi"
import { pendingDriverKey } from "../Sign Up/DriverOtpForm"
import { isValidAccountEmail } from "../../../lib/signInContact"

export function SignIn() {
  const { setUser, setView } = useApp()
  const navigate = useNavigate()

  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [mode, setMode] = useState<"otp" | "password">("password")

  const [challenge, setChallenge] = useState<{
    challengeId: string
    expiresAt: string
    resendAt: string
  } | null>(null)

  const [code, setCode] = useState("")
  const [totp, setTotp] = useState("")

  // Forgot Password Modal State
  const [showForgotPassword, setShowForgotPassword] = useState(false)
  const [fpEmail, setFpEmail] = useState("")
  const [fpStep, setFpStep] = useState<"request" | "verify" | "success">(
    "request",
  )
  const [fpOtp, setFpOtp] = useState("")
  const [fpNewPassword, setFpNewPassword] = useState("")
  const [fpConfirmPassword, setFpConfirmPassword] = useState("")
  const [fpShowPassword, setFpShowPassword] = useState(false)
  const [fpError, setFpError] = useState("")
  const [fpSubmitting, setFpSubmitting] = useState(false)

  const [requireTotp, setRequireTotp] = useState(false)
  const [totpCode, setTotpCode] = useState("")

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (submitting) return
    setError("")

    if (mode === "password" && !isValidAccountEmail(email)) {
      setError("Please enter a valid email address.")
      return
    }

    setSubmitting(true)

    try {
      const user =
        mode === "otp"
          ? await authApi.loginOtp(
              challenge!.challengeId,
              code,
              totp || undefined,
            )
          : await authApi.login(email, password, totpCode || undefined)

      const signedInUser = { ...user, lastLoginAt: new Date().toISOString() }
      store.saveUser(signedInUser)

      store.addAuditLog({
        userId: user.id,
        userName: user.name,
        userRole: user.role,
        action: "SIGN_IN",
        details: `User signed in as ${user.role}`,
      })

      store.notifyUser(
        user.id,
        "LOGIN_SUCCESS",
        "Successful login",
        "You have successfully logged in.",
      )

      sessionStorage.removeItem(pendingDriverKey)
      sessionStorage.removeItem("sp_pending_owner_registration")
      setPassword("")
      setUser(signedInUser)
      setView(user.role)
      navigate(getUserHomePath(user), { replace: true })
    } catch (error) {
      if (
        mode === "password" &&
        error instanceof AuthApiError &&
        error.code === "ACCOUNT_PENDING_VERIFICATION"
      ) {
        try {
          const contact = email.trim()
          try {
            const registration = await authApi.recoverDriver(contact)
            sessionStorage.setItem(
              pendingDriverKey,
              JSON.stringify({ ...registration, contact }),
            )
            sessionStorage.removeItem("sp_pending_owner_registration")
            localStorage.removeItem("sp_pending_owner_registration")
          } catch (driverError) {
            if (!(driverError instanceof AuthApiError) || driverError.code !== "REGISTRATION_NOT_FOUND")
              throw driverError

            const recovered = await request<{
              success: boolean
              data: {
                verification?: { challengeId: string; expiresAt: string; resendAt: string }
                emailVerified: boolean
              }
            }>("/register/owner/recover", "POST", { contact })
            if (!recovered.success || recovered.data.emailVerified || !recovered.data.verification?.challengeId)
              throw new Error("Email verification is not available yet. Please contact support.")
            sessionStorage.setItem(
              "sp_pending_owner_registration",
              JSON.stringify({ email: contact, lotSetupComplete: true, verification: recovered.data.verification }),
            )
            sessionStorage.removeItem(pendingDriverKey)
          }
          setPassword("")
          setView("sign-up")
          return
        } catch (recoveryError) {
          setError(
            recoveryError instanceof Error
              ? recoveryError.message
              : "Unable to resume verification. Please try again.",
          )
          return
        }
      }
      const msg =
        error instanceof Error
          ? error.message
          : "Sign in failed. Please try again."

      // MFA accounts must complete the OTP challenge endpoint. The password endpoint
      // deliberately returns MFA_REQUIRED and cannot accept a TOTP on retry.
      if (
        mode === "password" &&
        msg.toLowerCase().includes("use otp sign-in") &&
        !requireTotp
      ) {
        try {
          const nextChallenge = await authApi.requestOtp(email)
          setMode("otp")
          setChallenge(nextChallenge)
          setCode("")
          setTotp("")
          setRequireTotp(false)
          setTotpCode("")
          setError(
            "Two-factor authentication is enabled. Enter the sign-in code sent to you and your authenticator code.",
          )
        } catch (otpError) {
          setError(
            otpError instanceof Error
              ? otpError.message
              : "Unable to start OTP sign-in. Please try again.",
          )
        }
      } else {
        setError(msg)
      }
    } finally {
      setSubmitting(false)
    }
  }

  async function requestEmailOtp() {
    if (submitting) return
    if (!isValidAccountEmail(email)) {
      setError("Please enter a valid email address.")
      return
    }

    setError("")
    setSubmitting(true)
    try {
      setChallenge(await authApi.requestOtp(email.trim()))
      setMode("otp")
      setCode("")
      setTotp("")
      setPassword("")
      setRequireTotp(false)
      setTotpCode("")
    } catch (otpError) {
      setError(
        otpError instanceof Error
          ? otpError.message
          : "Unable to send a sign-in code. Please try again.",
      )
    } finally {
      setSubmitting(false)
    }
  }

  // Forgot Password Handlers
  const handleOpenForgotPassword = () => {
    setFpEmail(email || "")
    setFpStep("request")
    setFpOtp("")
    setFpNewPassword("")
    setFpConfirmPassword("")
    setFpError("")
    setShowForgotPassword(true)
  }

  const handleRequestResetOtp = (e: React.FormEvent) => {
    e.preventDefault()
    if (!fpEmail.trim()) {
      setFpError("Please enter your registered email or phone number.")
      return
    }
    setFpSubmitting(true)
    setFpError("")

    setTimeout(() => {
      const existingUser = store.findUserByEmail(fpEmail.trim())
      if (existingUser) {
        store.addAuditLog({
          userId: existingUser.id,
          userName: existingUser.name,
          userRole: existingUser.role,
          action: "PASSWORD_RESET_REQUEST",
          details: "User initiated password reset request",
        })
      }
      setFpSubmitting(false)
      setFpStep("verify")
    }, 400)
  }

  const handleConfirmResetPassword = (e: React.FormEvent) => {
    e.preventDefault()
    if (!fpOtp.trim() || fpOtp.trim().length < 4) {
      setFpError(
        "Please enter the verification code sent to your email or SMS.",
      )
      return
    }
    if (fpNewPassword.length < 8) {
      setFpError("New password must be at least 8 characters long.")
      return
    }
    if (fpNewPassword !== fpConfirmPassword) {
      setFpError("Passwords do not match. Please re-enter.")
      return
    }

    setFpSubmitting(true)
    setFpError("")

    setTimeout(() => {
      const allUsers = store.getUsers()
      const matchingUsers = allUsers.filter(
        (u) => u.email.toLowerCase() === fpEmail.trim().toLowerCase(),
      )

      if (matchingUsers.length) {
        matchingUsers.forEach((u) => {
          store.saveUser({ ...u, password: fpNewPassword })
          store.addAuditLog({
            userId: u.id,
            userName: u.name,
            userRole: u.role,
            action: "PASSWORD_RESET_SUCCESS",
            details: `Password was successfully reset by user (${u.role})`,
          })
          store.notifyUser(
            u.id,
            "PASSWORD_RESET",
            "Password Changed",
            "Your account password has been updated successfully.",
          )
        })
      }
      setFpSubmitting(false)
      setFpStep("success")
    }, 500)
  }

  const handleCompleteForgotPassword = () => {
    setShowForgotPassword(false)
    setMode("password")
    setEmail(fpEmail)
    setPassword(fpNewPassword)
  }

  return (
    <div
      className="auth-page-background auth-stage auth-stage-sign-in"
    >
      <div className="auth-panel">
        <div className="auth-panel-heading">
          <BrandLogo height={64} style={{ margin: "0 auto 0.5rem" }} />
          <BrandWordmark height={21} />
          <PageHeader title="Sign In" variant="compact" align="center" description="Welcome back to SmartParking" />
        </div>

        <form
          onSubmit={submit}
          className="card auth-form-background auth-form-card"
          style={{ display: "flex", flexDirection: "column", gap: "1.125rem" }}
        >
          <div>
            <label className="label" htmlFor="sign-in-email" style={{ fontSize: "0.925rem" }}>
              Email
            </label>
            <input
              id="sign-in-email"
              name="email"
              className="input"
              type="email"
              autoComplete="username"
              value={email}
              disabled={submitting || !!challenge}
              onChange={(e) => {
                setEmail(e.target.value)
                setError("")
              }}
              placeholder="you@example.com"
              required
              style={{ fontSize: "1rem" }}
            />
          </div>

          {mode === "password" && (
            <div>
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginBottom: "0.25rem",
                }}
              >
                <label
                  className="label"
                  htmlFor="sign-in-password"
                  style={{ fontSize: "0.925rem", margin: 0 }}
                >
                  Password
                </label>
                <button
                  type="button"
                  onClick={handleOpenForgotPassword}
                  style={{
                    background: "none",
                    border: "none",
                    color: "var(--primary)",
                    cursor: "pointer",
                    fontSize: "0.825rem",
                    fontWeight: 600,
                    padding: 0,
                  }}
                >
                  Forgot password?
                </button>
              </div>
              <div style={{ position: "relative" }}>
                <input
                  id="sign-in-password"
                  name="password"
                  className="input"
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Enter your password"
                  required
                  style={{ paddingRight: "2.5rem", fontSize: "1rem" }}
                />
                <button
                  type="button"
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  title={showPassword ? "Hide password" : "Show password"}
                  onClick={() => setShowPassword(!showPassword)}
                  style={{
                    position: "absolute",
                    right: "0.75rem",
                    top: "50%",
                    transform: "translateY(-50%)",
                    display: "flex",
                    width: 20,
                    height: 20,
                    padding: 0,
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    color: "var(--muted)",
                  }}
                >
                  <PasswordVisibilityIcon visible={showPassword} />
                </button>
              </div>
            </div>
          )}

          {mode === "password" && requireTotp && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}>
              <div
                style={{
                  padding: "0.5rem 0.75rem",
                  borderRadius: "var(--radius)",
                  background: "#f59e0b10",
                  border: "1px solid #f59e0b40",
                  color: "#b45309",
                  fontSize: "0.825rem",
                  lineHeight: 1.5,
                }}
              >
                Your account has two-factor authentication enabled. Please enter your security code below.
              </div>
              <label className="label" style={{ fontSize: "0.925rem" }}>
                Security Code (2FA)
                <input
                  className="input"
                  placeholder="Enter your 6-digit security code"
                  inputMode="numeric"
                  pattern="[0-9]{6}"
                  maxLength={6}
                  autoComplete="one-time-code"
                  autoFocus
                  value={totpCode}
                  onChange={(e) => setTotpCode(e.target.value)}
                  style={{ fontSize: "1rem" }}
                />
              </label>
            </div>
          )}

          {mode === "otp" && challenge && (
            <>
              <p
                className="text-center"
                style={{
                  margin: 0,
                  fontSize: "0.85rem",
                  color: "var(--muted)",
                }}
              >
                Check {email} for your code. Expires in 5 minutes.
              </p>
              <label className="label text-center">
                Verification code
                <input
                  className="input text-center"
                  required
                  pattern="[0-9]{6}"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                />
              </label>
              <label className="label text-center">
                Authenticator code (if MFA enabled)
                <input
                  className="input text-center"
                  inputMode="numeric"
                  maxLength={6}
                  value={totp}
                  onChange={(e) => setTotp(e.target.value)}
                />
              </label>
              <button
                className="btn-outline justify-center"
                type="button"
                disabled={submitting}
                onClick={requestEmailOtp}
              >
                Request another code
              </button>
            </>
          )}

          {error && (
            <div
              role="alert"
              style={{
                background: "#fee2e2",
                border: "1px solid #fca5a5",
                borderRadius: "var(--radius)",
                padding: "0.625rem 0.875rem",
                color: "#dc2626",
                fontSize: "0.875rem",
              }}
            >
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="btn-primary"
            style={{
              width: "100%",
              justifyContent: "center",
              fontSize: "1rem",
              padding: "0.75rem",
            }}
          >
            {submitting ? "Please wait…" : "Sign In"}
          </button>

          {mode === "password" && (
            <div className="auth-alt-actions" aria-label="Other sign-in options">
              <button
                type="button"
                disabled={submitting}
                className="btn-outline justify-center"
                onClick={requestEmailOtp}
                aria-label="Sign in with email OTP"
              >
                <span className="auth-email-icon" aria-hidden="true">✉</span> Email OTP
              </button>
              <button
                type="button"
                disabled
                className="btn-outline justify-center"
                aria-label="Sign in with Google (not available yet)"
                title="Google sign-in is not available yet"
              >
                <svg className="auth-google-icon" viewBox="0 0 48 48" aria-hidden="true">
                  <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5Z" />
                  <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.27 5.48-4.8 7.18l7.73 6c4.51-4.17 7.11-10.52 7.11-17.65Z" />
                  <path fill="#FBBC05" d="M10.53 28.59A14.5 14.5 0 0 1 9.75 24c0-1.6.28-3.15.77-4.59l-7.98-6.2A24.1 24.1 0 0 0 0 24c0 3.87.93 7.52 2.55 10.78l7.98-6.19Z" />
                  <path fill="#34A853" d="M24 48c6.48 0 11.92-2.13 15.89-5.8l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48Z" />
                </svg> Google
              </button>
            </div>
          )}

          <p
            style={{
              textAlign: "center",
              fontSize: "0.9rem",
              color: "var(--muted)",
              margin: 0,
            }}
          >
            Don't have an account?{" "}
            <button
              type="button"
              onClick={() => setView("sign-up")}
              style={{
                background: "none",
                border: "none",
                color: "var(--primary)",
                cursor: "pointer",
                fontWeight: 600,
                fontSize: "0.9rem",
                padding: 0,
              }}
            >
              Sign Up
            </button>
          </p>
        </form>
      </div>

      {/* FORGOT PASSWORD MODAL */}
      {showForgotPassword && (
        <div
          role="presentation"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) setShowForgotPassword(false)
          }}
          style={{
            position: "fixed",
            inset: 0,
            zIndex: 1000,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: "1rem",
            background: "rgba(2, 6, 23, 0.7)",
            backdropFilter: "blur(6px)",
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="forgot-password-title"
            className="card"
            style={{
              width: "100%",
              maxWidth: 440,
              background: "var(--card)",
              color: "var(--fg)",
              border: "1px solid var(--border)",
              borderRadius: "1rem",
              padding: "1.75rem",
              boxShadow: "0 25px 60px rgba(0, 0, 0, 0.5)",
            }}
          >
            {/* Modal Header */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "flex-start",
                marginBottom: "1.25rem",
              }}
            >
              <div>
                <span
                  style={{
                    fontSize: "0.72rem",
                    fontWeight: 700,
                    color: "var(--primary)",
                    letterSpacing: "0.08em",
                    textTransform: "uppercase",
                  }}
                >
                  Account Recovery
                </span>
                <h3
                  id="forgot-password-title"
                  style={{
                    fontFamily: "Be Vietnam Pro",
                    fontWeight: 800,
                    fontSize: "1.4rem",
                    margin: "0.2rem 0 0",
                    color: "var(--fg)",
                  }}
                >
                  Reset Password
                </h3>
              </div>
              <button
                type="button"
                aria-label="Close"
                onClick={() => setShowForgotPassword(false)}
                style={{
                  width: 32,
                  height: 32,
                  borderRadius: "50%",
                  border: "1px solid var(--border)",
                  background: "var(--bg)",
                  color: "var(--fg)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  cursor: "pointer",
                }}
              >
                <UntitledIcon name="x" size={16} />
              </button>
            </div>

            {/* Error banner */}
            {fpError && (
              <div
                style={{
                  padding: "0.625rem 0.85rem",
                  borderRadius: "0.5rem",
                  background: "#ef444415",
                  border: "1px solid #ef444440",
                  color: "#dc2626",
                  fontSize: "0.825rem",
                  marginBottom: "1rem",
                }}
              >
                {fpError}
              </div>
            )}

            {/* Step 1: Request Reset Code */}
            {fpStep === "request" && (
              <form
                onSubmit={handleRequestResetOtp}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "1rem",
                }}
              >
                <p
                  style={{
                    margin: 0,
                    fontSize: "0.85rem",
                    color: "var(--muted)",
                    lineHeight: 1.5,
                  }}
                >
                  Enter the email address or phone number associated with your
                  account. We will send you a verification code to reset your
                  password.
                </p>

                <div>
                  <label className="label" style={{ fontSize: "0.875rem" }}>
                    Email or Phone Number
                  </label>
                  <input
                    className="input"
                    type="text"
                    value={fpEmail}
                    onChange={(e) => setFpEmail(e.target.value)}
                    placeholder="you@example.com or phone"
                    required
                    autoFocus
                  />
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "flex-end",
                    gap: "0.625rem",
                    marginTop: "0.5rem",
                  }}
                >
                  <button
                    type="button"
                    className="btn-outline"
                    onClick={() => setShowForgotPassword(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="btn-primary"
                    disabled={fpSubmitting}
                  >
                    {fpSubmitting ? "Sending..." : "Send Reset Code"}
                  </button>
                </div>
              </form>
            )}

            {/* Step 2: Verify Code & Set New Password */}
            {fpStep === "verify" && (
              <form
                onSubmit={handleConfirmResetPassword}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.95rem",
                }}
              >
                <div
                  className="text-center"
                  style={{
                    padding: "0.625rem 0.75rem",
                    borderRadius: "0.5rem",
                    background: "#22c55e10",
                    border: "1px solid #22c55e30",
                    fontSize: "0.8rem",
                    color: "#16a34a",
                  }}
                >
                  A 6-digit code was dispatched to <strong>{fpEmail}</strong>.
                </div>

                <div>
                  <label className="label text-center" style={{ fontSize: "0.85rem" }}>
                    Verification Code (OTP)
                  </label>
                  <input
                    className="input text-center"
                    placeholder="Enter 6-digit code (e.g. 123456)"
                    value={fpOtp}
                    onChange={(e) => setFpOtp(e.target.value)}
                    maxLength={6}
                    required
                    autoFocus
                  />
                </div>

                <div>
                  <label className="label" style={{ fontSize: "0.85rem" }}>
                    New Password
                  </label>
                  <div style={{ position: "relative" }}>
                    <input
                      className="input"
                      type={fpShowPassword ? "text" : "password"}
                      placeholder="Minimum 8 characters"
                      value={fpNewPassword}
                      onChange={(e) => setFpNewPassword(e.target.value)}
                      minLength={8}
                      required
                      style={{ paddingRight: "2.5rem" }}
                    />
                    <button
                      type="button"
                      onClick={() => setFpShowPassword(!fpShowPassword)}
                      style={{
                        position: "absolute",
                        right: "0.75rem",
                        top: "50%",
                        transform: "translateY(-50%)",
                        background: "none",
                        border: "none",
                        cursor: "pointer",
                        color: "var(--muted)",
                        display: "flex",
                      }}
                    >
                      <PasswordVisibilityIcon visible={fpShowPassword} />
                    </button>
                  </div>
                </div>

                <div>
                  <label className="label" style={{ fontSize: "0.85rem" }}>
                    Confirm New Password
                  </label>
                  <input
                    className="input"
                    type={fpShowPassword ? "text" : "password"}
                    placeholder="Re-enter new password"
                    value={fpConfirmPassword}
                    onChange={(e) => setFpConfirmPassword(e.target.value)}
                    minLength={8}
                    required
                  />
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    marginTop: "0.5rem",
                  }}
                >
                  <button
                    type="button"
                    className="btn-outline"
                    onClick={() => setFpStep("request")}
                    style={{ fontSize: "0.8rem", padding: "0.4rem 0.75rem" }}
                  >
                    Back
                  </button>
                  <button
                    type="submit"
                    className="btn-primary"
                    disabled={fpSubmitting}
                  >
                    {fpSubmitting ? "Updating..." : "Update Password"}
                  </button>
                </div>
              </form>
            )}

            {/* Step 3: Success State */}
            {fpStep === "success" && (
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  textAlign: "center",
                  gap: "0.85rem",
                  padding: "0.5rem 0",
                }}
              >
                <div
                  style={{
                    width: 52,
                    height: 52,
                    borderRadius: "50%",
                    background: "#22c55e18",
                    color: "#16a34a",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <UntitledIcon name="check-circle" size={30} />
                </div>
                <h4
                  style={{
                    fontFamily: "Be Vietnam Pro",
                    fontSize: "1.2rem",
                    fontWeight: 700,
                    margin: 0,
                    color: "var(--fg)",
                  }}
                >
                  Password Reset Complete
                </h4>
                <p
                  style={{
                    margin: 0,
                    fontSize: "0.85rem",
                    color: "var(--muted)",
                    lineHeight: 1.5,
                  }}
                >
                  Your account password has been successfully updated. You can
                  now sign in with your new credentials.
                </p>

                <button
                  type="button"
                  className="btn-primary"
                  style={{
                    width: "100%",
                    justifyContent: "center",
                    marginTop: "0.5rem",
                  }}
                  onClick={handleCompleteForgotPassword}
                >
                  Sign In With New Password
                </button>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
