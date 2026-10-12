import { useState, useEffect, type FormEvent } from "react"
import { createPortal } from "react-dom"
import { operatorsApi, type Operator, type Site } from "../../../lib/parkingApi"
import { isValidAccountEmail } from "../../../lib/signInContact"
import { PasswordVisibilityIcon } from "../../../components/forms/PasswordVisibilityIcon"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

interface CreateOperatorFormProps {
  isOpen: boolean
  sites: Site[]
  existingOperators?: Operator[]
  onCreated: (operator: Operator) => void
  onClose: () => void
}

const DEFAULT_OPERATOR_PERMISSIONS = [
  "DEVICE_STATUS_VIEW",
  "DEVICE_MANAGE",
  "CASH_COLLECT",
  "APPEAL_REVIEW",
  "SLOT_OVERRIDE",
]

function validatePassword(pw: string): string[] {
  const errors: string[] = []
  if (pw.length < 8) errors.push("At least 8 characters")
  if (pw.length > 15) errors.push("Maximum 15 characters")
  if (!/[A-Z]/.test(pw)) errors.push("One uppercase letter")
  if (!/[a-z]/.test(pw)) errors.push("One lowercase letter")
  if (!/[0-9]/.test(pw)) errors.push("One number")
  if (!/[^A-Za-z0-9]/.test(pw)) errors.push("One special character")
  return errors
}

const PASSWORD_RULES = [
  "At least 8 characters",
  "Maximum 15 characters",
  "One uppercase letter",
  "One lowercase letter",
  "One number",
  "One special character",
]

export function CreateOperatorForm({
  isOpen,
  sites,
  existingOperators = [],
  onCreated,
  onClose,
}: CreateOperatorFormProps) {
  const [form, setForm] = useState({
    name: "",
    email: "",
    password: "",
    confirmPassword: "",
    siteId: sites[0]?.id ?? "",
  })
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)
  const [busy, setBusy] = useState(false)
  const [generalError, setGeneralError] = useState("")
  const [emailError, setEmailError] = useState("")
  const [nameError, setNameError] = useState("")

  useEffect(() => {
    if (isOpen) {
      setGeneralError("")
      setEmailError("")
      setNameError("")
      if (!form.siteId && sites.length > 0) {
        setForm((f) => ({ ...f, siteId: sites[0].id }))
      }
    }
  }, [isOpen, sites])

  if (!isOpen || typeof document === "undefined") return null

  const isDirty = Boolean(
    form.name.trim() ||
      form.email.trim() ||
      form.password ||
      form.confirmPassword,
  )

  const passwordErrors = validatePassword(form.password)
  const isPasswordValid = form.password.length > 0 && passwordErrors.length === 0
  const isConfirmMatch =
    form.password.length > 0 && form.password === form.confirmPassword

  function handleAttemptClose() {
    if (busy) return
    if (isDirty) {
      const confirmDiscard = window.confirm(
        "You have unsaved operator information. Are you sure you want to close?",
      )
      if (!confirmDiscard) return
    }
    onClose()
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return

    setGeneralError("")
    setEmailError("")
    setNameError("")

    const trimmedName = form.name.trim()
    const normalizedEmail = form.email.trim().toLowerCase()

    // 1. Validation checks
    if (!trimmedName) {
      setNameError("Full name is required.")
      return
    }

    if (!isValidAccountEmail(normalizedEmail)) {
      setEmailError("Enter a valid email address format.")
      return
    }

    // 2. Client-side duplicate check against loaded operators
    const duplicateEmail = existingOperators.find(
      (op) => op.email.toLowerCase() === normalizedEmail,
    )
    if (duplicateEmail) {
      setEmailError("This email is already assigned to an existing operator.")
      return
    }

    // 3. Password validation
    if (!isPasswordValid) {
      setGeneralError(
        "Password must be 8–15 characters with uppercase, lowercase, a digit, and a special character.",
      )
      return
    }

    if (!isConfirmMatch) {
      setGeneralError("Password confirmation does not match.")
      return
    }

    // 4. Sites check
    const siteIds = sites.filter((s) => s.id === form.siteId).map((s) => s.id)

    if (!siteIds.length) {
      setGeneralError("Please select an active site.")
      return
    }

    setBusy(true)
    try {
      const operator = await operatorsApi.create({
        fullName: trimmedName,
        email: normalizedEmail,
        password: form.password,
        siteIds,
        permissions: DEFAULT_OPERATOR_PERMISSIONS,
      })

      // Reset form on success
      setForm({
        name: "",
        email: "",
        password: "",
        confirmPassword: "",
        siteId: sites[0]?.id ?? "",
      })
      onCreated(operator)
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : "Operator creation failed."

      if (
        message.toLowerCase().includes("email_exists") ||
        message.toLowerCase().includes("email is already registered")
      ) {
        setEmailError("This email is already registered in the system.")
      } else {
        setGeneralError(message)
      }
    } finally {
      setBusy(false)
    }
  }

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="operator-modal-title"
      className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
      onClick={(e) => {
        if (e.target === e.currentTarget) handleAttemptClose()
      }}
    >
      <div className="relative w-full max-w-lg max-h-[92vh] flex flex-col rounded-2xl border border-[var(--border)] bg-[var(--card)] shadow-2xl text-[var(--fg)] animate-in">
        {/* Modal Header */}
        <div className="flex items-center justify-between border-b border-[var(--border)] px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--primary)]/10 text-[var(--primary)]">
              <UntitledIcon name="users" size={20} />
            </div>
            <div>
              <h2 id="operator-modal-title" className="text-lg font-bold text-[var(--fg)]">
                Create Operator
              </h2>
              <p className="text-xs text-[var(--muted)]">
                Set the operator's login email, password, and work site.
              </p>
            </div>
          </div>
          <button
            type="button"
            className="flex h-8 w-8 items-center justify-center rounded-lg border border-[var(--border)] bg-[var(--bg)] text-[var(--muted)] hover:text-[var(--fg)] transition-colors cursor-pointer"
            onClick={handleAttemptClose}
            aria-label="Close dialog"
            disabled={busy}
          >
            <UntitledIcon name="x" size={16} />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-4.5">
          {generalError && (
            <div role="alert" className="flex items-start gap-2.5 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-600 dark:text-red-400">
              <span className="mt-0.5 shrink-0">
                <UntitledIcon name="alert" size={15} />
              </span>
              <p className="font-medium leading-relaxed">{generalError}</p>
            </div>
          )}

          {/* Full Name */}
          <div>
            <label className="label" htmlFor="op-full-name">
              Full Name <span className="text-red-500">*</span>
            </label>
            <input
              id="op-full-name"
              className={`input ${nameError ? "border-red-500" : ""}`}
              maxLength={255}
              placeholder="e.g. Nguyen Van An"
              value={form.name}
              onChange={(e) => {
                setForm((f) => ({ ...f, name: e.target.value }))
                if (nameError) setNameError("")
              }}
              disabled={busy}
              required
            />
            {nameError && (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400 font-medium">{nameError}</p>
            )}
          </div>

          {/* Email */}
          <div>
            <label className="label" htmlFor="op-email">
              Email <span className="text-red-500">*</span>
            </label>
            <input
              id="op-email"
              type="email"
              className={`input ${emailError ? "border-red-500" : ""}`}
              maxLength={255}
              placeholder="operator@smartparking.vn"
              value={form.email}
              onChange={(e) => {
                setForm((f) => ({ ...f, email: e.target.value }))
                if (emailError) setEmailError("")
              }}
              disabled={busy}
              required
            />
            {emailError && (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400 font-medium">{emailError}</p>
            )}
          </div>

          {/* Password Fields */}
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor="op-password">
                Password <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  id="op-password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="new-password"
                  className="input pr-10"
                  minLength={8}
                  maxLength={15}
                  value={form.password}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, password: e.target.value }))
                  }
                  disabled={busy}
                  required
                />
                <button
                  type="button"
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setShowPassword(!showPassword)}
                  tabIndex={-1}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                >
                  <PasswordVisibilityIcon visible={showPassword} size={18} />
                </button>
              </div>
            </div>

            <div>
              <label className="label" htmlFor="op-confirm-password">
                Re-Password <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  id="op-confirm-password"
                  type={showConfirmPassword ? "text" : "password"}
                  autoComplete="new-password"
                  className={`input pr-10 ${
                    form.confirmPassword && !isConfirmMatch ? "border-red-500" : ""
                  }`}
                  value={form.confirmPassword}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, confirmPassword: e.target.value }))
                  }
                  disabled={busy}
                  required
                />
                <button
                  type="button"
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  tabIndex={-1}
                  aria-label={
                    showConfirmPassword ? "Hide password" : "Show password"
                  }
                >
                  <PasswordVisibilityIcon visible={showConfirmPassword} size={18} />
                </button>
              </div>
              {form.confirmPassword && !isConfirmMatch && (
                <p className="mt-1 text-xs text-red-600 dark:text-red-400 font-medium">
                  Passwords do not match
                </p>
              )}
            </div>
          </div>

          {form.password && (
            <div className="space-y-1.5" aria-label="Password requirements">
              <div className="h-1 overflow-hidden rounded-sm bg-[var(--border)]">
                <div
                  className="h-full transition-all"
                  style={{
                    width: `${((6 - passwordErrors.length) / 6) * 100}%`,
                    background: passwordErrors.length >= 4 ? "#ef4444" : passwordErrors.length >= 2 ? "#f59e0b" : "#22c55e",
                  }}
                />
              </div>
              <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs">
                {PASSWORD_RULES.map((rule) => (
                  <span
                    key={rule}
                    className={`flex items-center gap-1 ${passwordErrors.includes(rule) ? "text-[var(--muted)]" : "text-green-500"}`}
                  >
                    {passwordErrors.includes(rule) ? <span aria-hidden="true">○</span> : <UntitledIcon name="check" size={13} />}
                    {rule}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Assigned Facility */}
          <div>
            <label className="label" htmlFor="op-site">
              Site <span className="text-red-500">*</span>
            </label>
            <select
              id="op-site"
              className="input"
              value={form.siteId}
              onChange={(e) =>
                setForm((f) => ({ ...f, siteId: e.target.value }))
              }
              disabled={busy || !sites.length}
              required
            >
              <option value="" disabled>
                Select a site
              </option>
              {sites.map((site) => (
                <option key={site.id} value={site.id}>
                  {site.code} - {site.name} ({site.totalPhysicalCapacity} slots)
                </option>
              ))}
            </select>
            {!sites.length && (
              <p role="status" className="mt-1 text-xs text-[var(--muted)]">
                No active sites are available. Create an active site in Sites before creating this account.
              </p>
            )}
          </div>

          {/* Modal Footer Controls */}
          <div className="flex items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
            <button
              type="button"
              className="btn-outline text-xs px-4 py-2"
              onClick={handleAttemptClose}
              disabled={busy}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn-primary text-xs px-5 py-2 font-medium"
              disabled={
                busy ||
                !sites.length ||
                !form.name.trim() ||
                !form.email.trim() ||
                !isPasswordValid ||
                !isConfirmMatch
              }
            >
              {busy ? "Creating Operator…" : "Create Operator"}
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}
