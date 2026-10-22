import { useEffect, type ReactNode } from "react"

import { useApp } from "../../context/AppContext"

import { UntitledIcon } from "../icon/UntitledIcon"

export type DashboardNavItem = {
  id: string
  label: string
  icon?: string
  active: boolean
  onClick: () => void
  keepMenuOpen?: boolean
}

export type DashboardNavGroup = { label?: string; items: DashboardNavItem[] }

export function DashboardSidebar({
  groups,
  children,
}: {
  groups: DashboardNavGroup[]
  children: ReactNode
}) {
  const { dashboardMenuOpen, setDashboardMenuOpen, user } = useApp()
  const navigationVisible = dashboardMenuOpen

  useEffect(() => {
    if (!dashboardMenuOpen) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setDashboardMenuOpen(false)
    }
    window.addEventListener("keydown", closeOnEscape)
    return () => window.removeEventListener("keydown", closeOnEscape)
  }, [dashboardMenuOpen, setDashboardMenuOpen])

  return (
    <div
      className={`dashboard-workspace${
        dashboardMenuOpen ? " is-menu-open" : ""
      }`}
    >
      <aside
        id="dashboard-navigation"
        className={`dashboard-sidebar${dashboardMenuOpen ? " is-open" : ""}`}
        aria-label={`${user?.role ?? "Dashboard"} navigation`}
        aria-hidden={!navigationVisible}
      >
        <div className="dashboard-sidebar-heading">
          <div><strong>Menu</strong><span>{user?.role ? `${user.role.charAt(0).toUpperCase()}${user.role.slice(1)} workspace` : "Workspace"}</span></div>
          <button type="button" className="btn-ghost dashboard-sidebar-close" aria-label="Close navigation" onClick={() => setDashboardMenuOpen(false)} tabIndex={navigationVisible ? 0 : -1}>
            <UntitledIcon name="x" size={18} />
          </button>
        </div>
        {groups.map((group, index) => (
          <div className="dashboard-nav-group" key={group.label ?? index}>
            {group.label && (
              <div className="dashboard-nav-label">{group.label}</div>
            )}
            {group.items.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`btn-ghost dashboard-nav-item${item.active ? " active" : ""}`}
                aria-current={item.active ? "page" : undefined}
                onClick={() => {
                  item.onClick()
                  if (!item.keepMenuOpen) setDashboardMenuOpen(false)
                }}
                tabIndex={navigationVisible ? 0 : -1}
              >
                <span className="dashboard-nav-icon">
                  <UntitledIcon name={item.icon ?? item.label} size={18} />
                </span>
                <span>{item.label}</span>
              </button>
            ))}
          </div>
        ))}
      </aside>
      {dashboardMenuOpen && (
        <button
          className="dashboard-scrim"
          aria-label="Close navigation"
          onClick={() => setDashboardMenuOpen(false)}
        />
      )}
      <div className="dashboard-content">{children}</div>
    </div>
  )
}
