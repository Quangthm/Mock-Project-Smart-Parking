import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useState, useEffect, type FormEvent } from "react"
import { Modal } from "../../../components/ui/Modal"
import { useConfirmation } from "../../../components/ui/useConfirmation"
import { operatorsApi, type Operator, type Site } from "../../../lib/parkingApi"
import { isValidAccountEmail } from "../../../lib/signInContact"
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
    siteId: sites[0]?.id ?? "",
  })
  const { confirm, confirmation } = useConfirmation()
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

  const isDirty = Boolean(form.name.trim() || form.email.trim())

  async function handleAttemptClose() {
    if (busy) return
    if (isDirty) {
      const confirmDiscard = await confirm({ title: "Discard operator details?", description: "You have unsaved operator information. Close without saving?", confirmLabel: "Discard changes", danger: true })
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

    // 3. Sites check
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
        siteIds,
        permissions: DEFAULT_OPERATOR_PERMISSIONS,
      })

      // Reset form on success
      setForm({
        name: "",
        email: "",
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

  return <>
    <Modal title="Create Operator" onClose={() => void handleAttemptClose()}>
      <p className="mb-5 text-sm leading-relaxed text-[var(--muted)]">Invite an operator to a work site. They must verify their email and set their password before signing in.</p>
        <form onSubmit={handleSubmit} className="space-y-5">
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

          {/* Assigned Facility */}
          <div>
            <label className="label" htmlFor="op-site">
              Site <span className="text-red-500">*</span>
            </label>
            <RoundedSelect
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
                  {site.name} ({site.totalPhysicalCapacity} slots)
                </option>
              ))}
            </RoundedSelect>
            {!sites.length && (
              <p role="status" className="mt-1 text-xs text-[var(--muted)]">
                No active sites are available. Create an active site in Sites before creating this account.
              </p>
            )}
          </div>

          {/* Modal Footer Controls */}
          <div className="flex flex-wrap items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
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
                !form.email.trim()
              }
            >
              {busy ? "Creating Operator…" : "Create Operator"}
            </button>
          </div>
        </form>
    </Modal>
    {confirmation}
  </>
}
