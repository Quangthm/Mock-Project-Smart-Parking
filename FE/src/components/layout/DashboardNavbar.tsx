import { useMemo, useState, useEffect } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../context/AppContext"
import { NotificationBell } from "../navigation/NotificationBell"
import { BrandLogo } from "../brand/BrandLogo"
import { ThemeIcon } from "../icon/ThemeIcon"
import { UntitledIcon } from "../icon/UntitledIcon"
import { AccountSecurityPanel } from "../forms/AccountSecurityPanel"
import { store } from "../../lib/store"

const rolePages: Record<string, { id: string; label: string }[]> = {
  driver: [
    { id: "find", label: "Find parking" },
    { id: "bookings", label: "Current bookings" },
    { id: "history", label: "Booking history" },
    { id: "subscriptions", label: "Passes" },
    { id: "support", label: "Support" },
  ],
  owner: [
    { id: "dashboard", label: "Overview" },
    { id: "sites", label: "Parking sites" },
    { id: "lots", label: "Parking lots" },
    { id: "structure", label: "Parking Structure" },
    { id: "operators", label: "Operators" },
    { id: "policy", label: "Policies" },
  ],
  admin: [
    { id: "overview", label: "Overview" },
    { id: "applications", label: "Applications" },
    { id: "drivers", label: "Driver accounts" },
    { id: "audit", label: "Audit log" },
    { id: "system", label: "System configuration" },
  ],
  operator: [
    { id: "overview", label: "Dashboard overview" },
    { id: "checkin", label: "Check-in and check-out" },
    { id: "status", label: "Lot status & structure" },
    { id: "slots", label: "Backup & slot operations" },
    { id: "tickets", label: "Driver tickets" },
    { id: "emergency", label: "Emergency & incidents" },
    { id: "assignments", label: "My assignments" },
    { id: "finance", label: "Financial reports" },
  ],
}

const accents = [
  { id: "blue", label: "Default", color: "#2563eb" },
  { id: "pink", label: "Pink", color: "#db2777" },
  { id: "sky", label: "Blue", color: "#0284c7" },
  { id: "green", label: "Green", color: "#16a34a" },
  { id: "red", label: "Red", color: "#dc2626" },
  { id: "black", label: "Black", color: "#111827" },
]

export function DashboardNavbar() {
  const {
    user,
    setUser,
    theme,
    toggleTheme,
    signOut,
    dashboardMenuOpen,
    setDashboardMenuOpen,
    accentColor,
    setAccentColor,
  } = useApp()

  const [panel, setPanel] = useState<"profile" | "settings" | "search" | null>(null)
  const [query, setQuery] = useState("")

  // Universal Account & Security Modal state
  const [accountModalTab, setAccountModalTab] = useState<"profile" | "security" | null>(null)
  const [securityDismissBlocked, setSecurityDismissBlocked] = useState(false)
  const [profileForm, setProfileForm] = useState({ name: user?.name ?? "", phone: user?.phone ?? "" })
  const [profileMsg, setProfileMsg] = useState("")
  const [recoveryNotice, setRecoveryNotice] = useState(() => sessionStorage.getItem("sp_mfa_recovery_remaining"))

  useEffect(() => {
    if (user) {
      setProfileForm({ name: user.name ?? "", phone: user.phone ?? "" })
    }
  }, [user])

  const pages =
    user?.role === "operator"
      ? user.operatorRole === "financial"
        ? rolePages.operator.filter((page) => page.id === "finance")
        : user.operatorRole === "cashier"
          ? []
          : rolePages.operator.filter((page) => page.id !== "finance")
      : (rolePages[user?.role ?? ""] ?? [])

  const results = useMemo(
    () =>
      pages.filter((page) =>
        page.label.toLowerCase().includes(query.trim().toLowerCase()),
      ),
    [pages, query],
  )

  function navigate(id: string) {
    window.dispatchEvent(new CustomEvent("sp:dashboard-nav", { detail: id }))
    setDashboardMenuOpen(false)
    setPanel(null)
    setQuery("")
  }

  function handleOpenAccountModal(tab: "profile" | "security") {
    setPanel(null)
    setDashboardMenuOpen(false)
    setAccountModalTab(tab)
    setProfileMsg("")
  }

  function handleSaveProfile(e: React.FormEvent) {
    e.preventDefault()
    if (!user) return
    const name = profileForm.name.trim()
    if (!name) {
      setProfileMsg("Please enter your full name.")
      return
    }
    const updated = { ...user, name, phone: profileForm.phone.trim() || undefined }
    store.saveUser(updated)
    setUser(updated)
    setProfileMsg("Your account settings have been saved.")
  }

  function toggleNavigation() {
    setPanel(null)
    setDashboardMenuOpen(!dashboardMenuOpen)
  }

  function toggleSearch() {
    const opening = panel !== "search"
    if (opening) setDashboardMenuOpen(false)
    setPanel(opening ? "search" : null)
  }

  function toggleProfile() {
    const opening = panel !== "profile"
    if (opening) setDashboardMenuOpen(false)
    setPanel(opening ? "profile" : null)
  }

  return (
    <>
      <nav className="dashboard-topbar">
        <div className="dashboard-topbar-left">
          {user?.operatorRole !== "cashier" && (
            <button
              type="button"
              className="icon-button menu-toggle"
              aria-label={
                dashboardMenuOpen ? "Close navigation" : "Open navigation"
              }
              aria-expanded={dashboardMenuOpen}
              onClick={toggleNavigation}
            >
              <UntitledIcon name="menu" />
            </button>
          )}
          <button
            type="button"
            className="dashboard-brand"
            onClick={() =>
              navigate(
                user?.role === "driver"
                  ? "find"
                  : user?.role === "operator"
                    ? "overview"
                    : user?.role === "owner"
                      ? "dashboard"
                      : "overview",
              )
            }
            aria-label="SmartParking dashboard"
          >
            <BrandLogo height={60} />
            <span className="brand-wordmark" style={{ fontSize: "1.1rem" }}>
              <span className="brand-smart">Smart</span>
              <span className="brand-parking">Parking</span>
            </span>
          </button>
        </div>

        <div className="dashboard-topbar-actions">
          {pages.length > 0 && (
            <div className="dashboard-popover-anchor">
              <button
                type="button"
                className="icon-button"
                aria-label="Search pages"
                aria-expanded={panel === "search"}
                onClick={toggleSearch}
              >
                <UntitledIcon name="search" />
              </button>
              {panel === "search" && (
                <div className="dashboard-popover dashboard-search-popover">
                  <label className="sr-only" htmlFor="dashboard-search">
                    Search pages and features
                  </label>
                  <input
                    id="dashboard-search"
                    autoFocus
                    className="input"
                    placeholder="Search pages and features"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                  />
                  <div className="dashboard-search-results">
                    {results.map((result) => (
                      <button
                        key={result.id}
                        type="button"
                        onClick={() => navigate(result.id)}
                      >
                        {result.label}
                        <UntitledIcon name="arrow-right" size={16} />
                      </button>
                    ))}
                    {!results.length && <p>No matching pages</p>}
                  </div>
                </div>
              )}
            </div>
          )}

          {user && <NotificationBell userId={user.id} />}

          {/* Profile Dropdown Anchor */}
          <div className="dashboard-popover-anchor">
            <button
              type="button"
              className="profile-trigger"
              aria-label="Open profile menu"
              aria-expanded={panel === "profile"}
              onClick={toggleProfile}
            >
              <span className="profile-avatar">
                {user?.avatar ? (
                  <img src={user.avatar} alt="" />
                ) : (
                  user?.name?.charAt(0).toUpperCase() ?? "?"
                )}
              </span>
              <span className="profile-trigger-name">{user?.name}</span>
              <UntitledIcon name="chevron-down" size={16} />
            </button>

            {/* Stable, Unbroken Popover Menu */}
            {panel === "profile" && (
              <div className="dashboard-popover profile-popover animate-in">
                {/* User Summary Header */}
                <div className="profile-summary">
                  <span className="profile-avatar profile-avatar-large">
                    {user?.name?.charAt(0).toUpperCase() ?? "?"}
                  </span>
                  <div>
                    <strong className="block truncate max-w-[180px]">{user?.name}</strong>
                    <span className="block truncate max-w-[180px] text-xs text-[var(--muted)]">{user?.email}</span>
                    <small className="inline-block px-1.5 py-0.5 rounded text-[10px] uppercase font-semibold bg-[var(--primary)]/10 text-[var(--primary)] mt-1">
                      {user?.role === "operator"
                        ? `${user.operatorRole ?? "Operator"} operator`
                        : user?.role}
                    </small>
                  </div>
                </div>

                <div className="popover-divider" />

                {/* Stable Navigation Items */}
                <button
                  type="button"
                  className="popover-item"
                  onClick={() => handleOpenAccountModal("profile")}
                >
                  <UntitledIcon name="users" size={16} />
                  <span>Account Settings</span>
                </button>

                {user?.role === "driver" && (
                  <button
                    type="button"
                    className="popover-item"
                    onClick={() => {
                      setPanel(null)
                      window.dispatchEvent(
                        new CustomEvent("sp:driver-profile", { detail: "vehicles" }),
                      )
                    }}
                  >
                    <UntitledIcon name="car" size={16} />
                    <span>My Vehicles</span>
                  </button>
                )}

                <button
                  type="button"
                  className="popover-item"
                  onClick={() => setPanel("settings")}
                >
                  <UntitledIcon name="settings" size={16} />
                  <span>Appearance Settings</span>
                </button>

                <div className="popover-divider" />

                <button
                  type="button"
                  className="popover-item signout-item"
                  onClick={signOut}
                >
                  <UntitledIcon name="log-out" size={16} />
                  <span>Sign Out</span>
                </button>
              </div>
            )}

            {/* Appearance Settings Sub-panel */}
            {panel === "settings" && (
              <div className="dashboard-popover settings-popover animate-in">
                <div className="popover-title-row">
                  <strong>Appearance Settings</strong>
                  <button
                    type="button"
                    className="text-button"
                    onClick={() => setPanel("profile")}
                  >
                    Back
                  </button>
                </div>
                <div className="appearance-label">Display Mode</div>
                <div className="theme-options">
                  {(["light", "dark"] as const).map((option) => (
                    <button
                      type="button"
                      key={option}
                      className={`theme-option${theme === option ? " selected" : ""}`}
                      onClick={() => {
                        if (theme !== option) toggleTheme()
                      }}
                      aria-pressed={theme === option}
                    >
                      <span className="theme-option-icon">
                        <ThemeIcon mode={option} />
                      </span>
                      <span>{option === "light" ? "Light" : "Dark"}</span>
                    </button>
                  ))}
                </div>
                <div className="appearance-label accent-label">Accent Color</div>
                <div className="accent-options">
                  {accents.map((accent) => (
                    <button
                      key={accent.id}
                      type="button"
                      className={`accent-option${accentColor === accent.id ? " selected" : ""}`}
                      onClick={() => setAccentColor(accent.id)}
                      aria-label={accent.label}
                      aria-pressed={accentColor === accent.id}
                    >
                      <span style={{ background: accent.color }} />
                      {accent.label}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Backdrop dismissals for popovers */}
        {panel && (
          <button
            type="button"
            className="popover-dismiss"
            aria-label="Close menu"
            onClick={() => setPanel(null)}
          />
        )}
      </nav>

      {recoveryNotice !== null && (
        <div role="status" className="card" style={{ margin: "0.75rem", display: "flex", flexWrap: "wrap", gap: "0.75rem", alignItems: "center" }}>
          <span>You signed in with a recovery code. {recoveryNotice} codes remain. If you lost your authenticator, set up a new one.</span>
          <button className="btn-outline" onClick={() => {
            handleOpenAccountModal("security")
            setRecoveryNotice(null)
            sessionStorage.removeItem("sp_mfa_recovery_remaining")
          }}>Review 2FA settings</button>
          <button className="text-button" onClick={() => {
            setRecoveryNotice(null)
            sessionStorage.removeItem("sp_mfa_recovery_remaining")
          }}>Later</button>
        </div>
      )}

      {/* Universal Account & Security Modal for ALL ROLES */}
      {accountModalTab &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="account-dialog-title"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
            onClick={(e) => {
              if (e.target === e.currentTarget && !securityDismissBlocked) setAccountModalTab(null)
            }}
          >
            <div className="relative w-full max-w-lg max-h-[90vh] flex flex-col rounded-2xl border border-[var(--border)] bg-[var(--card)] shadow-2xl text-[var(--fg)] animate-in">
              {/* Modal Header */}
              <div className="flex items-center justify-between border-b border-[var(--border)] px-6 py-4">
                <div>
                  <h2 id="account-dialog-title" className="text-base font-bold text-[var(--fg)]">
                    Account Settings
                  </h2>
                  <p className="text-xs text-[var(--muted)]">
                    Current role:{" "}
                    <strong className="text-[var(--primary)] uppercase font-mono">
                      {user?.role}
                    </strong>
                  </p>
                </div>
                <button
                  type="button"
                  className="flex h-8 w-8 items-center justify-center rounded-lg border border-[var(--border)] bg-[var(--bg)] text-[var(--muted)] hover:text-[var(--fg)] cursor-pointer"
                  disabled={securityDismissBlocked}
                  onClick={() => { if (!securityDismissBlocked) setAccountModalTab(null) }}
                  aria-label="Close"
                >
                  <UntitledIcon name="x" size={16} />
                </button>
              </div>

              {/* Tabs selector */}
              <div className="flex border-b border-[var(--border)] px-6 bg-[var(--bg)]/40">
                <button
                  type="button"
                  className={`px-4 py-3 text-xs font-semibold border-b-2 transition-colors flex items-center gap-2 ${
                    accountModalTab === "profile"
                      ? "border-[var(--primary)] text-[var(--primary)]"
                      : "border-transparent text-[var(--muted)] hover:text-[var(--fg)]"
                  }`}
                  disabled={securityDismissBlocked}
                  onClick={() => { if (!securityDismissBlocked) setAccountModalTab("profile") }}
                >
                  <UntitledIcon name="users" size={14} />
                  <span>Personal Information</span>
                </button>
                <button
                  type="button"
                  className={`px-4 py-3 text-xs font-semibold border-b-2 transition-colors flex items-center gap-2 ${
                    accountModalTab === "security"
                      ? "border-[var(--primary)] text-[var(--primary)]"
                      : "border-transparent text-[var(--muted)] hover:text-[var(--fg)]"
                  }`}
                  onClick={() => setAccountModalTab("security")}
                >
                  <UntitledIcon name="shield" size={14} />
                  <span>Security (2FA)</span>
                </button>
              </div>

              {/* Modal Content */}
              <div className="flex-1 overflow-y-auto p-6 space-y-4">
                {accountModalTab === "profile" && (
                  <form onSubmit={handleSaveProfile} className="space-y-4 text-xs">
                    <div>
                      <label className="label" htmlFor="acc-name">
                        Full Name
                      </label>
                      <input
                        id="acc-name"
                        className="input"
                        value={profileForm.name}
                        onChange={(e) =>
                          setProfileForm((c) => ({ ...c, name: e.target.value }))
                        }
                        required
                      />
                    </div>

                    <div>
                      <label className="label" htmlFor="acc-email">
                        Email Address
                      </label>
                      <input
                        id="acc-email"
                        className="input bg-[var(--bg)] opacity-80 cursor-not-allowed font-mono"
                        value={user?.email ?? ""}
                        readOnly
                      />
                      <span className="text-[10px] text-[var(--muted)] mt-1 block">
                        Your email is used to sign in and cannot be changed here.
                      </span>
                    </div>

                    <div>
                      <label className="label" htmlFor="acc-phone">
                        Phone Number
                      </label>
                      <input
                        id="acc-phone"
                        className="input"
                        placeholder="0912 345 678"
                        value={profileForm.phone}
                        onChange={(e) =>
                          setProfileForm((c) => ({ ...c, phone: e.target.value }))
                        }
                      />
                    </div>

                    {profileMsg && (
                      <div className="p-3 rounded-lg border border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 font-medium">
                        {profileMsg}
                      </div>
                    )}

                    <div className="flex justify-end gap-2 pt-2 border-t border-[var(--border)]">
                      <button
                        type="button"
                        className="btn-outline text-xs px-3.5 py-2"
                        onClick={() => setAccountModalTab(null)}
                      >
                        Close
                      </button>
                      <button type="submit" className="btn-primary text-xs px-4 py-2">
                        Save Changes
                      </button>
                    </div>
                  </form>
                )}

                {accountModalTab === "security" && (
                  <AccountSecurityPanel key={user?.id} onDismissBlockedChange={setSecurityDismissBlocked} />
                )}
              </div>
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
