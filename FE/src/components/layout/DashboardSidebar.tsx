import { useEffect, useLayoutEffect, useRef, useState, type FocusEvent, type MouseEvent, type ReactNode } from "react"
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
  const { user, dashboardMenuOpen, setDashboardMenuOpen } = useApp()
  const [hint, setHint] = useState<NavHint | null>(null)
  const [toolbar, setToolbar] = useState<Element | null>(null)
  const activePage = groups.flatMap((group) => group.items).find((item) => item.active)?.id
  const groupsRef = useRef(groups)
  const activePageRef = useRef(activePage)
  const initialHistoryPage = useRef<string | null>(typeof window !== "undefined" && typeof window.history.state?.smartParkingPage === "string" ? window.history.state.smartParkingPage : null)
  const wasMobileMenuOpen = useRef(false)
  groupsRef.current = groups
  activePageRef.current = activePage

  useEffect(() => { setToolbar(document.querySelector(".dashboard-topbar")) }, [])
  useLayoutEffect(() => { scrollPageToTop() }, [activePage])

  useEffect(() => {
    if (!dashboardMenuOpen || !window.matchMedia("(max-width: 640px)").matches) {
      if (wasMobileMenuOpen.current) {
        document.querySelector<HTMLButtonElement>("[data-dashboard-menu-trigger]")?.focus()
        wasMobileMenuOpen.current = false
      }
      return
    }
    wasMobileMenuOpen.current = true
    const priorOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    document.querySelector<HTMLButtonElement>("#dashboard-navigation .dashboard-nav-item")?.focus()
    const mobile = window.matchMedia("(max-width: 640px)")
    const onViewportChange = () => { if (!mobile.matches) setDashboardMenuOpen(false) }
    mobile.addEventListener("change", onViewportChange)
    const onEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setDashboardMenuOpen(false)
      if (event.key !== "Tab") return
      const items = [...document.querySelectorAll<HTMLButtonElement>("#dashboard-navigation button")]
      if (!items.length) return
      if (event.shiftKey && document.activeElement === items[0]) {
        event.preventDefault()
        items[items.length - 1].focus()
      } else if (!event.shiftKey && document.activeElement === items[items.length - 1]) {
        event.preventDefault()
        items[0].focus()
      }
    }
    document.addEventListener("keydown", onEscape)
    return () => {
      document.body.style.overflow = priorOverflow
      document.removeEventListener("keydown", onEscape)
      mobile.removeEventListener("change", onViewportChange)
    }
  }, [dashboardMenuOpen, setDashboardMenuOpen])

  useEffect(() => {
    const restorePage = (event: PopStateEvent) => {
      if (event.state?.smartParkingView !== user?.role) return
      const page = event.state?.smartParkingPage
      if (typeof page !== "string" || page === activePageRef.current) return
      groupsRef.current.flatMap((group) => group.items).find((item) => item.id === page)?.onClick()
    }
    window.addEventListener("popstate", restorePage)
    return () => window.removeEventListener("popstate", restorePage)
  }, [user?.role])

  useEffect(() => {
    if (!activePage || !user?.role) return
    const current = window.history.state ?? {}
    if (!current.smartParkingView) {
      window.history.replaceState({ ...current, smartParkingView: user.role, smartParkingPage: activePage }, "", window.location.href)
      return
    }
    if (current.smartParkingView !== user.role) return
    if (current.smartParkingPage === activePage) {
      initialHistoryPage.current = null
      return
    }
    const previousPage = current.smartParkingPage
    if (initialHistoryPage.current === previousPage) {
      const savedItem = groupsRef.current.flatMap((group) => group.items).find((item) => item.id === previousPage)
      if (savedItem) {
        savedItem.onClick()
        return
      }
      initialHistoryPage.current = null
    }
    if (!previousPage) {
      window.history.replaceState({ ...current, smartParkingPage: activePage }, "", window.location.href)
      return
    }
    window.history.pushState({
      ...current,
      idx: typeof current.idx === "number" ? current.idx + 1 : 1,
      smartParkingPage: activePage,
    }, "", window.location.href)
  }, [activePage, user?.role])

  function showHint(event: MouseEvent<HTMLButtonElement> | FocusEvent<HTMLButtonElement>, label: string) {
    const rect = event.currentTarget.getBoundingClientRect()
    setHint({ label, top: rect.top + rect.height / 2, left: rect.right + 12 })
  }

  const navigation = (
    <aside id="dashboard-navigation" className={`dashboard-sidebar${dashboardMenuOpen ? " is-open" : ""}`} aria-label={`${user?.role ?? "Dashboard"} navigation`}>
        <div className="dashboard-drawer-heading">
          <strong>Navigation</strong>
          <button type="button" className="dashboard-drawer-close" aria-label="Close navigation" onClick={() => setDashboardMenuOpen(false)}><UntitledIcon name="x" size={20} /></button>
        </div>
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
                  onClick={() => { setHint(null); item.onClick(); setDashboardMenuOpen(false) }}
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
    <div className={`dashboard-workspace${dashboardMenuOpen ? " is-menu-open" : ""}`}>
      {toolbar ? createPortal(navigation, toolbar) : navigation}
      {dashboardMenuOpen && <button type="button" className="dashboard-scrim" aria-label="Close navigation" onClick={() => setDashboardMenuOpen(false)} />}
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
