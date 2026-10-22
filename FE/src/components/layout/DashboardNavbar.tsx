import { useMemo, useState, useEffect, useRef } from "react"
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
  { id: "blue", label: "SmartParking", color: "#146b61" },
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

  const [panel, setPanel] = useState<"profile" | "search" | null>(null)
  const [query, setQuery] = useState("")
  const profileTriggerRef = useRef<HTMLButtonElement>(null)
  const settingsDialogRef = useRef<HTMLDivElement>(null)
  const settingsCloseRef = useRef<HTMLButtonElement>(null)

  // Universal Account & Security Modal state
  const [accountModalTab, setAccountModalTab] = useState<"profile" | "appearance" | "security" | null>(null)
  const [profileForm, setProfileForm] = useState({ name: user?.name ?? "", phone: user?.phone ?? "" })
  const [profileMsg, setProfileMsg] = useState("")

  useEffect(() => {
    if (user) {
      setProfileForm({ name: user.name ?? "", phone: user.phone ?? "" })
    }
  }, [user])

  const settingsOpen = accountModalTab !== null
  useEffect(() => {
    if (!settingsOpen) return
    settingsCloseRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault()
        setAccountModalTab(null)
        profileTriggerRef.current?.focus()
      }
      if (event.key !== "Tab" || !settingsDialogRef.current) return
      const focusable = Array.from(settingsDialogRef.current.querySelectorAll<HTMLElement>("button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href]"))
      if (!focusable.length) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener("keydown", onKeyDown)
    return () => document.removeEventListener("keydown", onKeyDown)
  }, [settingsOpen])

  function closeSettings() {
    setAccountModalTab(null)
    profileTriggerRef.current?.focus()
  }

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

  function handleOpenAccountModal(tab: "profile" | "appearance" | "security") {
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
              ref={profileTriggerRef}
              type="button"
              className="icon-button menu-toggle"
              aria-controls="dashboard-navigation"
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
                  <span>Settings</span>
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
              if (e.target === e.currentTarget) closeSettings()
            }}
          >
            <div ref={settingsDialogRef} className="settings-dialog animate-in">
              <div className="settings-dialog-header">
                <div>
                  <h2 id="account-dialog-title" className="settings-dialog-title">
                    Settings
                  </h2>
                  <p className="settings-dialog-subtitle">
                    Manage your account and dashboard appearance.
                  </p>
                </div>
                <button
                  ref={settingsCloseRef}
                  type="button"
                  className="btn-ghost settings-close"
                  onClick={closeSettings}
                  aria-label="Close settings"
                >
                  <UntitledIcon name="x" size={18} />
                </button>
              </div>

              <div className="settings-dialog-body">
                <nav className="settings-dialog-nav" aria-label="Settings sections">
                  {([
                    ["profile", "Account", "users"],
                    ["appearance", "Interface", "settings"],
                    ["security", "Security", "shield"],
                  ] as const).map(([id, label, icon]) => (
                    <button
                      key={id}
                      type="button"
                      className={`settings-nav-item${accountModalTab === id ? " selected" : ""}`}
                      aria-current={accountModalTab === id ? "page" : undefined}
                      onClick={() => setAccountModalTab(id)}
                    >
                      <UntitledIcon name={icon} size={18} />
                      <span>{label}</span>
                    </button>
                  ))}
                </nav>
                <div className="settings-dialog-content">
                {accountModalTab === "profile" && (
                  <form onSubmit={handleSaveProfile} className="settings-form">
                    <div className="settings-section-heading">
                      <h3>My account</h3>
                      <p>Keep your contact details ready for parking updates.</p>
                    </div>
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
                        className="input bg-[var(--bg)] cursor-not-allowed"
                        value={user?.email ?? ""}
                        readOnly
                      />
                      <span className="settings-helper">
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
                      <div role="status" className="settings-save-message">
                        {profileMsg}
                      </div>
                    )}

                    <div className="settings-form-actions">
                      <button
                        type="button"
                        className="btn-outline text-xs px-3.5 py-2"
                        onClick={closeSettings}
                      >
                        Close
                      </button>
                      <button type="submit" className="btn-primary text-xs px-4 py-2">
                        Save Changes
                      </button>
                    </div>
                  </form>
                )}

                {accountModalTab === "appearance" && (
                  <div className="settings-appearance">
                    <div className="settings-section-heading">
                      <h3>Interface</h3>
                      <p>Choose how your SmartParking dashboard looks.</p>
                    </div>
                    <section className="settings-option-group" aria-labelledby="settings-theme-title">
                      <div className="settings-option-heading">
                        <h4 id="settings-theme-title">Appearance</h4>
                        <p>Select a display mode that is comfortable for you.</p>
                      </div>
                      <div className="settings-theme-options">
                        {(["light", "dark"] as const).map((option) => (
                          <button
                            key={option}
                            type="button"
                            className={`settings-theme-option${theme === option ? " selected" : ""}`}
                            onClick={() => { if (theme !== option) toggleTheme() }}
                            aria-pressed={theme === option}
                          >
                            <span className="settings-theme-preview" data-theme={option}>
                              <span /><span /><span />
                            </span>
                            <span className="settings-theme-label"><ThemeIcon mode={option} />{option === "light" ? "Light" : "Dark"}</span>
                          </button>
                        ))}
                      </div>
                    </section>
                    <section className="settings-option-group" aria-labelledby="settings-accent-title">
                      <div className="settings-option-heading">
                        <h4 id="settings-accent-title">Accent color</h4>
                        <p>Used for navigation and focus highlights.</p>
                      </div>
                      <div className="settings-accent-options">
                        {accents.map((accent) => (
                          <button
                            key={accent.id}
                            type="button"
                            className={`settings-accent-option${accentColor === accent.id ? " selected" : ""}`}
                            onClick={() => setAccentColor(accent.id)}
                            aria-label={accent.label}
                            aria-pressed={accentColor === accent.id}
                          >
                            <span style={{ background: accent.color }} />
                            {accent.label}
                          </button>
                        ))}
                      </div>
                    </section>
                  </div>
                )}

                {accountModalTab === "security" && (
                  <div className="settings-security">
                    <div className="settings-section-heading"><h3>Security</h3><p>Manage extra protection for your account.</p></div>
                    <AccountSecurityPanel key={user?.id} />
                  </div>
                )}
                </div>
              </div>
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
