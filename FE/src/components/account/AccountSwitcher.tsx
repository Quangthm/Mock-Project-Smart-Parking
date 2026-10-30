import { useEffect, useRef, useState } from "react"
import { createPortal } from "react-dom"
import { AnimatePresence, motion, useReducedMotion } from "motion/react"

import type { User } from "../../lib/types"
import { BrandLogo } from "../brand/BrandLogo"
import { UntitledIcon } from "../icon/UntitledIcon"
import "./AccountSwitcher.css"

const roleDetails = {
  driver: { label: "Driver", icon: "car" },
  owner: { label: "Owner", icon: "building" },
  operator: { label: "Operator", icon: "parking" },
  admin: { label: "Admin", icon: "shield" },
} as const

type AddableRole = "driver" | "owner" | "operator"
const addableRoles: Record<User["role"], AddableRole[]> = {
  driver: ["owner"],
  owner: ["driver"],
  operator: ["driver"],
  admin: [],
}
const roleDescriptions: Record<AddableRole, string> = {
  driver: "Find parking, manage bookings, and review your trips.",
  owner: "Manage parking sites, policies, and finances after business registration and approval.",
  operator: "Manage assigned parking operations after an Owner grants access.",
}

interface AccountSwitcherProps {
  open: boolean
  user: User | null
  accentColor: string
  authError: string
  onClose: () => void
  onSignOut: () => Promise<void>
}

export function AccountSwitcher({
  open,
  user,
  accentColor,
  authError,
  onClose,
  onSignOut,
}: AccountSwitcherProps) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const currentWorkspaceRef = useRef<HTMLButtonElement>(null)
  const [signingOut, setSigningOut] = useState(false)
  const [addOpen, setAddOpen] = useState(false)
  const [previewRole, setPreviewRole] = useState<AddableRole | null>(null)
  const reduceMotion = useReducedMotion()

  useEffect(() => {
    if (!open) {
      setAddOpen(false)
      setPreviewRole(null)
    }
  }, [open])

  useEffect(() => {
    if (!open) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    currentWorkspaceRef.current?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault()
        onClose()
      }
      if (event.key !== "Tab" || !dialogRef.current) return
      const controls = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>("button:not(:disabled), a[href]"),
      )
      if (!controls.length) return
      const first = controls[0]
      const last = controls[controls.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener("keydown", handleKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener("keydown", handleKeyDown)
    }
  }, [open, onClose])

  async function handleSignOut() {
    if (signingOut) return
    setSigningOut(true)
    try {
      await onSignOut()
    } finally {
      setSigningOut(false)
    }
  }

  if (typeof document === "undefined") return null

  return createPortal(
    <AnimatePresence>
      {open && user && (
        <motion.div
          key="account-switcher"
          className="account-switcher-overlay dashboard-theme"
          data-accent={accentColor}
          role="dialog"
          aria-modal="true"
          aria-labelledby="account-switcher-title"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1, transition: { duration: 0.22 } }}
          exit={{ opacity: 0, transition: { duration: 0.18 } }}
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) onClose()
          }}
        >
          <div
            className="account-switcher-frame"
            ref={dialogRef}
            onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}
          >
            <div className="account-switcher-topbar">
              <BrandLogo height={42} />
              <button type="button" className="account-switcher-close" onClick={onClose} aria-label="Close account switcher">
                <UntitledIcon name="x" size={20} />
              </button>
            </div>

            <motion.div
              className="account-switcher-content"
              initial={{ opacity: 0, y: reduceMotion ? 0 : 14 }}
              animate={{ opacity: 1, y: 0, transition: reduceMotion ? { duration: 0.15 } : { type: "spring", stiffness: 150, damping: 25 } }}
              exit={{ opacity: 0, y: reduceMotion ? 0 : 8, transition: { duration: 0.18 } }}
            >
              <p className="account-switcher-eyebrow">SmartParking</p>
              <h2 id="account-switcher-title">Choose your workspace</h2>
              <p className="account-switcher-intro">Continue with your workspace or explore another role.</p>

              <div className="account-switcher-grid">
                <motion.button
                  ref={currentWorkspaceRef}
                  type="button"
                  className="account-switcher-profile"
                  onClick={onClose}
                  whileTap={reduceMotion ? undefined : { scale: 0.985, transition: { type: "spring", stiffness: 550, damping: 30 } }}
                  aria-label={`Continue as ${user.name}, ${roleDetails[user.role].label}`}
                >
                  <motion.span
                    className="account-switcher-avatar"
                    initial={{ opacity: 0, scale: reduceMotion ? 1 : 0.86 }}
                    animate={{ opacity: 1, scale: 1, transition: reduceMotion ? { duration: 0.15 } : { type: "spring", stiffness: 260, damping: 22, delay: 0.08 } }}
                    exit={{ opacity: 0, scale: reduceMotion ? 1 : 0.94, transition: { duration: 0.14 } }}
                  >
                    {user.avatar ? (
                      <img src={user.avatar} alt="" />
                    ) : (
                      <span aria-hidden="true">{user.name.trim().charAt(0).toUpperCase() || roleDetails[user.role].label.charAt(0)}</span>
                    )}
                    <span className="account-switcher-role-icon" aria-hidden="true">
                      <UntitledIcon name={roleDetails[user.role].icon} size={18} />
                    </span>
                  </motion.span>
                  <strong>{user.name}</strong>
                  <span className="account-switcher-role">{roleDetails[user.role].label}</span>
                  <span className="account-switcher-current"><UntitledIcon name="check" size={13} /> Current workspace</span>
                </motion.button>
                {addableRoles[user.role].length > 0 && (
                  <motion.button
                    type="button"
                    className="account-switcher-profile account-switcher-add"
                    aria-expanded={addOpen}
                    aria-controls="account-switcher-options"
                    onClick={() => {
                      setAddOpen((value) => !value)
                      setPreviewRole(null)
                    }}
                    whileTap={reduceMotion ? undefined : { scale: 0.985, transition: { type: "spring", stiffness: 550, damping: 30 } }}
                  >
                    <span className="account-switcher-avatar account-switcher-add-avatar" aria-hidden="true">
                      <UntitledIcon name="plus" size={38} />
                    </span>
                    <strong>Add Account</strong>
                    <span className="account-switcher-role">Explore another role</span>
                  </motion.button>
                )}
              </div>

              <AnimatePresence mode="wait">
                {addOpen && addableRoles[user.role].length > 0 && (
                  <motion.div
                    id="account-switcher-options"
                    key="add-options"
                    className="account-switcher-options"
                    initial={{ opacity: 0, y: reduceMotion ? 0 : 8 }}
                    animate={{ opacity: 1, y: 0, transition: { duration: 0.2 } }}
                    exit={{ opacity: 0, y: reduceMotion ? 0 : 5, transition: { duration: 0.14 } }}
                  >
                    <h3>Explore another role</h3>
                    <div className="account-switcher-option-grid">
                      {addableRoles[user.role].map((role) => (
                        <button
                          key={role}
                          type="button"
                          className="account-switcher-option"
                          aria-pressed={previewRole === role}
                          onClick={() => setPreviewRole(role)}
                        >
                          <span className="account-switcher-option-icon" aria-hidden="true"><UntitledIcon name={roleDetails[role].icon} size={21} /></span>
                          <span>{roleDetails[role].label}</span>
                          <UntitledIcon name="arrow-right" size={17} />
                        </button>
                      ))}
                    </div>
                    {user.role === "driver" && (
                      <p className="account-switcher-eligibility-note">Operator access can appear only after an Owner assigns this account email and SmartParking verifies it.</p>
                    )}
                    <AnimatePresence mode="wait">
                      {previewRole && (
                        <motion.div
                          key={previewRole}
                          className="account-switcher-preview"
                          role="status"
                          initial={{ opacity: 0, y: reduceMotion ? 0 : 6 }}
                          animate={{ opacity: 1, y: 0, transition: { duration: 0.18 } }}
                          exit={{ opacity: 0, transition: { duration: 0.12 } }}
                        >
                          <strong>{roleDetails[previewRole].label} workspace</strong>
                          <p>{roleDescriptions[previewRole]}</p>
                          <span>Preview only. SmartParking must verify this role for your account before you can enter it.</span>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </motion.div>
                )}
              </AnimatePresence>

              <motion.div
                className="account-switcher-footer"
                initial={{ opacity: 0, y: reduceMotion ? 0 : 8 }}
                animate={{ opacity: 1, y: 0, transition: { duration: 0.22, delay: 0.16 } }}
                exit={{ opacity: 0, y: reduceMotion ? 0 : 5, transition: { duration: 0.12 } }}
              >
                <button type="button" className="account-switcher-signout" onClick={handleSignOut} disabled={signingOut}>
                  <UntitledIcon name="log-out" size={17} />
                  {signingOut ? "Signing out…" : "Sign Out"}
                </button>
                {authError && <p className="account-switcher-error" role="alert">{authError}</p>}
              </motion.div>
            </motion.div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>,
    document.body,
  )
}
