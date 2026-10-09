import { useState, useMemo, type FormEvent } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import { ownerData as store } from "../data/data"
import type { OperatorAccessRole, ParkingLot } from "../../../lib/types"
import { PasswordVisibilityIcon } from "../../../components/forms/PasswordVisibilityIcon"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

interface CreateOperatorFormProps {
  sites: ParkingLot[]
  onCreated: () => void
  onCancel: () => void
}

// Password validation rules matching SignUp.tsx exactly
function validatePassword(pw: string) {
  const errors: string[] = []
  if (pw.length < 8) errors.push("At least 8 characters")
  if (pw.length > 15) errors.push("Maximum 15 characters")
  if (!/[A-Z]/.test(pw)) errors.push("One uppercase letter")
  if (!/[a-z]/.test(pw)) errors.push("One lowercase letter")
  if (!/[0-9]/.test(pw)) errors.push("One number")
  if (!/[^A-Za-z0-9]/.test(pw)) errors.push("One special character")
  return errors
}

function PasswordStrengthMeter({ pw }: { pw: string }) {
  const errs = validatePassword(pw)
  if (!pw) return null

  const ok = 6 - errs.length
  const color = ok <= 2 ? "#ef4444" : ok <= 4 ? "#f59e0b" : "#22c55e"

  const RULES = [
    "At least 8 characters",
    "Maximum 15 characters",
    "One uppercase letter",
    "One lowercase letter",
    "One number",
    "One special character",
  ]

  return (
    <div style={{ marginTop: "0.5rem" }}>
      <div
        style={{
          height: "4px",
          background: "var(--border)",
          borderRadius: "2px",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            height: "100%",
            width: `${(ok / 6) * 100}%`,
            background: color,
            transition: "width 0.3s",
          }}
        />
      </div>
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "0.35rem 0.65rem",
          marginTop: "0.45rem",
        }}
      >
        {RULES.map((r) => {
          const passed = !errs.includes(r)
          return (
            <span
              key={r}
              style={{
                fontSize: "0.72rem",
                color: passed ? "#16a34a" : "var(--muted)",
                display: "flex",
                alignItems: "center",
                gap: "0.25rem",
                fontWeight: passed ? 600 : 400,
              }}
            >
              {passed ? (
                <UntitledIcon name="check" size={13} />
              ) : (
                <span aria-hidden="true" style={{ opacity: 0.6 }}>
                  ○
                </span>
              )}{" "}
              {r}
            </span>
          )
        })}
      </div>
    </div>
  )
}

export function CreateOperatorForm({
  sites,
  onCreated,
  onCancel,
}: CreateOperatorFormProps) {
  const { user } = useApp()

  const [name, setName] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const [operatorSiteId, setOperatorSiteId] = useState(sites[0]?.id ?? "all")
  const [error, setError] = useState("")
  const [submitting, setSubmitting] = useState(false)

  // Name uniqueness check within staff
  const isNameDuplicate = useMemo(() => {
    const trimmed = name.trim().toLowerCase()
    if (!trimmed) return false
    return store.getUsers().some((u) => u.name.trim().toLowerCase() === trimmed)
  }, [name])

  // One email can have multiple accounts (except admin role)
  const isAdminEmailConflict = useMemo(() => {
    const trimmed = email.trim().toLowerCase()
    if (!trimmed) return false
    return store
      .getUsers()
      .some(
        (u) => u.role === "admin" && u.email.trim().toLowerCase() === trimmed,
      )
  }, [email])

  const isSharedEmail = useMemo(() => {
    const trimmed = email.trim().toLowerCase()
    if (!trimmed) return false
    return store
      .getUsers()
      .some(
        (u) => u.role !== "admin" && u.email.trim().toLowerCase() === trimmed,
      )
  }, [email])

  const passwordErrors = useMemo(() => validatePassword(password), [password])
  const passwordsMatch = password.length > 0 && password === confirmPassword

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")

    const trimmedName = name.trim()
    const trimmedEmail = email.trim()

    if (!trimmedName) {
      setError("Please enter the operator's full name.")
      return
    }

    if (!trimmedEmail) {
      setError("Please enter a valid email address.")
      return
    }

    // Name uniqueness check against fresh store
    const currentUsers = store.getUsers()
    if (
      currentUsers.some(
        (u) => u.name.trim().toLowerCase() === trimmedName.toLowerCase(),
      )
    ) {
      setError(
        "This operator name already exists in the system. Please choose a different name.",
      )
      return
    }

    // One email can have multiple accounts (except admin role)
    if (
      currentUsers.some(
        (u) =>
          u.role === "admin" &&
          u.email.trim().toLowerCase() === trimmedEmail.toLowerCase(),
      )
    ) {
      setError(
        "This email belongs to an Admin account and cannot be reused for other roles.",
      )
      return
    }

    // Password validation matching SignUp rules
    if (passwordErrors.length > 0) {
      setError(
        `Password does not meet requirements: ${passwordErrors.join(", ")}`,
      )
      return
    }

    // Confirm password match
    if (password !== confirmPassword) {
      setError("Passwords do not match. Please verify your password.")
      return
    }

    if (!operatorSiteId) {
      setError("Please select an assigned parking facility.")
      return
    }

    setSubmitting(true)

    const payload = {
      name: trimmedName,
      email: trimmedEmail,
      password,
      role: "operator" as const,
      operatorRole: "operation" as OperatorAccessRole,
      operatorSiteId,
      ownerId: user?.id,
    }

    store.createUser(payload)

    if (user) {
      store.notifyUser(
        user.id,
        "EMPLOYEE_CREATED",
        "Operator account created",
        `Operator account for ${trimmedName} has been created successfully.`,
      )
    }

    store.addAuditLog({
      userId: user?.id ?? "",
      userName: user?.name ?? "",
      userRole: "owner",
      action: "OPERATOR_CREATED",
      details: `Created operator at ${operatorSiteId}: ${trimmedName} (${trimmedEmail})`,
    })

    setSubmitting(false)
    onCreated()
  }

  return createPortal(
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 99999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "rgba(0, 0, 0, 0.75)",
        backdropFilter: "blur(6px)",
        WebkitBackdropFilter: "blur(6px)",
        padding: "1rem",
      }}
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-operator-title"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget && !submitting) onCancel()
      }}
    >
      <form
        onSubmit={handleSubmit}
        className="card"
        style={{
          width: "100%",
          maxWidth: "580px",
          maxHeight: "90vh",
          overflowY: "auto",
          display: "flex",
          flexDirection: "column",
          gap: "1.25rem",
          border: "1.5px solid var(--border)",
          boxShadow: "0 25px 50px -12px rgba(0, 0, 0, 0.7)",
          background: "var(--card)",
          position: "relative",
        }}
      >
        {/* Form Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            borderBottom: "1px solid var(--border)",
            paddingBottom: "0.75rem",
          }}
        >
          <div>
            <h4
              id="create-operator-title"
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                margin: 0,
                color: "var(--fg)",
              }}
            >
              Create New Operator
            </h4>
            <p
              style={{
                margin: "0.15rem 0 0",
                fontSize: "0.78rem",
                color: "var(--muted)",
              }}
            >
              Create an operational staff account with site access and secure
              credentials.
            </p>
          </div>
          <button
            type="button"
            onClick={onCancel}
            style={{
              background: "none",
              border: "none",
              color: "var(--muted)",
              cursor: "pointer",
              display: "flex",
              padding: "4px",
            }}
          >
            <UntitledIcon name="x" size={18} />
          </button>
        </div>

        {/* Grid: Name & Email */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
            gap: "1rem",
          }}
        >
          {/* Full Name */}
          <div>
            <label className="label" htmlFor="operator-name">
              Full Name <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <input
              id="operator-name"
              className="input"
              placeholder="e.g. Tran Van Tuan"
              value={name}
              onChange={(event) => setName(event.target.value)}
              style={{
                borderColor: isNameDuplicate ? "#ef4444" : undefined,
              }}
              required
              autoFocus
            />
            {isNameDuplicate && (
              <p
                style={{
                  margin: "0.25rem 0 0",
                  fontSize: "0.75rem",
                  color: "#dc2626",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                }}
              >
                <UntitledIcon name="alert" size={12} />
                An operator with this name already exists. Please choose a
                different name.
              </p>
            )}
          </div>

          {/* Email */}
          <div>
            <label className="label" htmlFor="operator-email">
              Email Address <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <input
              id="operator-email"
              className="input"
              type="email"
              placeholder="operator@smartpark.vn"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              style={{
                borderColor: isAdminEmailConflict ? "#ef4444" : undefined,
              }}
              required
            />
            {isAdminEmailConflict && (
              <p
                style={{
                  margin: "0.25rem 0 0",
                  fontSize: "0.75rem",
                  color: "#dc2626",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                }}
              >
                <UntitledIcon name="alert" size={12} />
                This email belongs to an Admin account and cannot be used for
                staff accounts.
              </p>
            )}
            {isSharedEmail && !isAdminEmailConflict && (
              <p
                style={{
                  margin: "0.25rem 0 0",
                  fontSize: "0.74rem",
                  color: "var(--muted)",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                }}
              >
                <UntitledIcon name="check" size={12} />
                Multiple accounts can share this email (except Admin).
              </p>
            )}
          </div>
        </div>

        {/* Grid: Password & Confirm Password */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
            gap: "1rem",
            alignItems: "start",
          }}
        >
          {/* Password */}
          <div>
            <label className="label" htmlFor="operator-password">
              Password <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <div className="relative w-full">
              <input
                id="operator-password"
                className="input w-full pr-10"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="8–15 characters"
                minLength={8}
                maxLength={15}
                required
              />
              <button
                type="button"
                aria-label={showPassword ? "Hide password" : "Show password"}
                title={showPassword ? "Hide password" : "Show password"}
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)] transition-colors p-0.5 cursor-pointer bg-transparent border-0"
              >
                <PasswordVisibilityIcon
                  visible={showPassword}
                  className="w-5 h-5 shrink-0"
                />
              </button>
            </div>
          </div>

          {/* Confirm Password */}
          <div>
            <label className="label" htmlFor="operator-confirm-password">
              Confirm Password <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <div className="relative w-full">
              <input
                id="operator-confirm-password"
                className="input w-full pr-10"
                type={showConfirmPassword ? "text" : "password"}
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                placeholder="Re-enter password"
                minLength={8}
                maxLength={15}
                style={{
                  borderColor:
                    confirmPassword && !passwordsMatch ? "#ef4444" : undefined,
                }}
                required
              />
              <button
                type="button"
                aria-label={
                  showConfirmPassword ? "Hide password" : "Show password"
                }
                title={showConfirmPassword ? "Hide password" : "Show password"}
                onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 flex items-center justify-center text-[var(--muted)] hover:text-[var(--fg)] transition-colors p-0.5 cursor-pointer bg-transparent border-0"
              >
                <PasswordVisibilityIcon
                  visible={showConfirmPassword}
                  className="w-5 h-5 shrink-0"
                />
              </button>
            </div>
            {confirmPassword && (
              <p
                style={{
                  margin: "0.35rem 0 0",
                  fontSize: "0.74rem",
                  color: passwordsMatch ? "#16a34a" : "#dc2626",
                  display: "flex",
                  alignItems: "center",
                  gap: "0.25rem",
                  fontWeight: 600,
                }}
              >
                {passwordsMatch ? (
                  <>
                    <UntitledIcon name="check" size={13} /> Passwords match
                  </>
                ) : (
                  <>
                    <UntitledIcon name="alert" size={13} /> Passwords do not
                    match
                  </>
                )}
              </p>
            )}
          </div>

          {/* Full-width Password Strength Meter matching SignUp */}
          {password && (
            <div style={{ gridColumn: "1 / -1" }}>
              <PasswordStrengthMeter pw={password} />
            </div>
          )}
        </div>

        {/* Grid: Site Assignment & Role */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
            gap: "1rem",
          }}
        >
          {/* Site Assignment */}
          <div>
            <label className="label" htmlFor="operator-site">
              Assigned Facility / Site{" "}
              <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <select
              id="operator-site"
              className="input"
              value={operatorSiteId}
              onChange={(event) => setOperatorSiteId(event.target.value)}
              required
            >
              <option value="all">All Sites</option>
              {sites.map((site) => (
                <option key={site.id} value={site.id}>
                  {site.name}
                </option>
              ))}
            </select>
          </div>

          {/* Access Role */}
          <div>
            <label className="label" htmlFor="operator-access-role">
              Operator Access Role
            </label>
            <input
              id="operator-access-role"
              className="input"
              value="Operation (Staff)"
              disabled
              readOnly
              style={{
                background: "var(--bg)",
                color: "var(--fg)",
                fontWeight: 600,
                cursor: "default",
              }}
            />
          </div>
        </div>

        {/* Error alert */}
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.85rem",
              borderRadius: "0.5rem",
              background: "#ef444415",
              border: "1px solid #ef444440",
              color: "#dc2626",
              fontSize: "0.825rem",
              display: "flex",
              alignItems: "center",
              gap: "0.4rem",
            }}
          >
            <UntitledIcon name="alert" size={15} />
            <span>{error}</span>
          </div>
        )}

        {/* Action buttons */}
        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: "0.625rem",
            paddingTop: "0.5rem",
            borderTop: "1px solid var(--border)",
          }}
        >
          <button
            type="button"
            className="btn-outline"
            style={{ fontSize: "0.85rem" }}
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="submit"
            className="btn-primary"
            style={{ fontSize: "0.85rem" }}
            disabled={
              submitting ||
              isNameDuplicate ||
              isAdminEmailConflict ||
              passwordErrors.length > 0 ||
              !passwordsMatch
            }
          >
            {submitting ? "Creating..." : "Create Operator"}
          </button>
        </div>
      </form>
    </div>,
    document.body,
  )
}
