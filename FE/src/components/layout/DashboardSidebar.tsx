import { useEffect, useLayoutEffect, useState, type FocusEvent, type MouseEvent, type ReactNode } from "react"
import { createPortal } from "react-dom"

import { useApp } from "../../context/AppContext"
import { UntitledIcon } from "../icon/UntitledIcon"
import { scrollPageToTop } from "../../lib/scrollPageToTop"

export type DashboardNavItem = {
  id: string
  label: string
  icon?: string
  active: boolean
  onClick: () => void
  keepMenuOpen?: boolean
}

export type DashboardNavGroup = { label?: string; items: DashboardNavItem[] }

type NavHint = { label: string; top: number; left: number }

export function DashboardSidebar({ groups, children }: { groups: DashboardNavGroup[]; children: ReactNode }) {
  const { user } = useApp()
  const [hint, setHint] = useState<NavHint | null>(null)
  const [toolbar, setToolbar] = useState<Element | null>(null)
  const activePage = groups.flatMap((group) => group.items).find((item) => item.active)?.id

  useEffect(() => { setToolbar(document.querySelector(".dashboard-topbar")) }, [])
  useLayoutEffect(() => { scrollPageToTop() }, [activePage])

  function showHint(event: MouseEvent<HTMLButtonElement> | FocusEvent<HTMLButtonElement>, label: string) {
    const rect = event.currentTarget.getBoundingClientRect()
    setHint({ label, top: rect.top + rect.height / 2, left: rect.right + 12 })
  }

  const navigation = (
    <aside id="dashboard-navigation" className="dashboard-sidebar" aria-label={`${user?.role ?? "Dashboard"} navigation`}>
        <div className="dashboard-nav-scroll">
          {groups.map((group, index) => (
            <div className="dashboard-nav-group" key={group.label ?? index}>
              {group.label && <span className="sr-only">{group.label}</span>}
              {group.items.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={`dashboard-nav-item${item.active ? " active" : ""}`}
                  aria-label={item.label}
                  aria-current={item.active ? "page" : undefined}
                  onMouseEnter={(event) => showHint(event, item.label)}
                  onMouseLeave={() => setHint(null)}
                  onFocus={(event) => showHint(event, item.label)}
                  onBlur={() => setHint(null)}
                  onClick={() => { setHint(null); item.onClick() }}
                >
                  <UntitledIcon name={item.icon ?? item.label} size={20} />
                  <span className="dashboard-nav-mobile-label" aria-hidden="true">{item.label}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
    </aside>
  )

  return (
    <div className="dashboard-workspace">
      {toolbar ? createPortal(navigation, toolbar) : navigation}
      {hint && typeof document !== "undefined" && createPortal(
        <span className="dashboard-nav-hint" role="presentation" style={{ top: hint.top, left: hint.left }}>
          {hint.label}
        </span>,
        document.body,
      )}
      <div className="dashboard-content">{children}</div>
    </div>
  )
}
