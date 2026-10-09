import { useState } from "react"
import { useApp } from "../../../context/AppContext"
import { authData as store } from "../data/data"
import { useNavigate } from "react-router-dom"
import { getUserHomePath } from "../../operator/data/roleRoutes"
import { BrandLogo } from "../../../components/brand/BrandLogo"
import { PasswordVisibilityIcon } from "../../../components/forms/PasswordVisibilityIcon"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { authApi } from "../../../lib/authApi"

export function SignIn() {
  const { setUser, setView } = useApp()
  const navigate = useNavigate()

  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [mode, setMode] = useState<"otp" | "password">("otp")

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

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (submitting) return
    setError("")
    setSubmitting(true)

    try {
      if (mode === "otp" && !challenge) {
        setChallenge(await authApi.requestOtp(email))
        return
      }

      const user =
        mode === "otp"
          ? await authApi.loginOtp(
              challenge!.challengeId,
              code,
              totp || undefined,
            )
          : await authApi.login(email, password)

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

      setPassword("")
      setUser(signedInUser)
      setView(user.role)
      navigate(getUserHomePath(user), { replace: true })
    } catch (error) {
      setError(
        error instanceof Error
          ? error.message
          : "Sign in failed. Please try again.",
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
      className="auth-page-background"
      style={{
        minHeight: "80vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "2rem 1rem",
      }}
    >
      <div style={{ width: "100%", maxWidth: "420px" }}>
        <div style={{ textAlign: "center", marginBottom: "2rem" }}>
          <BrandLogo height={100} style={{ margin: "0 auto 1rem" }} />
          <h1
            style={{
              fontFamily: "Outfit",
              fontWeight: 800,
              fontSize: "1.75rem",
              color: "var(--fg)",
              margin: "0 0 0.375rem",
            }}
          >
            Sign In
          </h1>
          <p style={{ color: "var(--muted)", fontSize: "0.925rem" }}>
            Welcome back to SmartParking
          </p>
        </div>

        <form
          onSubmit={submit}
          className="card auth-form-background"
          style={{ display: "flex", flexDirection: "column", gap: "1.125rem" }}
        >
          <label className="label">
            Sign-in method
            <select
              className="input"
              value={mode}
              disabled={submitting}
              onChange={(e) => {
                setMode(e.target.value as "otp" | "password")
                setChallenge(null)
                setCode("")
                setTotp("")
                setError("")
              }}
            >
              <option value="otp">Email / SMS code</option>
              <option value="password">Password</option>
            </select>
          </label>

          <div>
            <label className="label" style={{ fontSize: "0.925rem" }}>
              Email or Phone Number
            </label>
            <input
              className="input"
              type="text"
              autoComplete="username"
              value={email}
              disabled={submitting || !!challenge}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@example.com or phone number"
              required
              autoFocus
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
                  className="input"
                  type={showPassword ? "text" : "password"}
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

          {mode === "otp" && challenge && (
            <>
              <p
                style={{
                  margin: 0,
                  fontSize: "0.85rem",
                  color: "var(--muted)",
                }}
              >
                Your code is queued for delivery. It expires in five minutes.
              </p>
              <label className="label">
                Verification code
                <input
                  className="input"
                  required
                  pattern="[0-9]{6}"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                />
              </label>
              <label className="label">
                Authenticator code (if MFA enabled)
                <input
                  className="input"
                  inputMode="numeric"
                  maxLength={6}
                  value={totp}
                  onChange={(e) => setTotp(e.target.value)}
                />
              </label>
              <button
                className="btn-outline"
                type="button"
                disabled={submitting}
                onClick={() => {
                  setChallenge(null)
                  setCode("")
                }}
              >
                Request another code
              </button>
            </>
          )}

          {error && (
            <div
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
            {submitting
              ? "Please wait…"
              : mode === "otp" && !challenge
                ? "Send sign-in code"
                : "Sign In"}
          </button>

          {/* Additional helper for OTP mode to also reset password */}
          {mode === "otp" && !challenge && (
            <div style={{ textAlign: "center" }}>
              <button
                type="button"
                onClick={handleOpenForgotPassword}
                style={{
                  background: "none",
                  border: "none",
                  color: "var(--primary)",
                  cursor: "pointer",
                  fontSize: "0.85rem",
                  fontWeight: 600,
                  padding: 0,
                }}
              >
                Forgot your password?
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
                    fontFamily: "Outfit",
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
                  <label className="label" style={{ fontSize: "0.85rem" }}>
                    Verification Code (OTP)
                  </label>
                  <input
                    className="input"
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
                    fontFamily: "Outfit",
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
