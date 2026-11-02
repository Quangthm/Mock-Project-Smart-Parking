import {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  useCallback,
  type ReactNode,
} from "react"

import type { User } from "../lib/types"

import { store } from "../lib/store"

import { authApi, authExpiredEvent } from "../lib/authApi"
import { showUiToast } from "../lib/uiToast"
import { restoreDriverRegistration } from "../roles/authentications/Sign Up/DriverOtpForm"
import { restoreOwnerRegistration } from "../lib/ownerRegistration"

export type View = "landing" | "pricing" | "support" | "faq" | "sign-in" | "sign-up" | "parking-lot-setup" | "pending-approval" | "business" | "about" | "affiliates" | "careers" | "driver" | "owner" | "operator" | "admin" | "terms" | "privacy" | "payment-refund"

interface AppCtx {
  user: User | null

  view: View

  theme: "light" | "dark"

  dashboardMenuOpen: boolean

  accentColor: string

  authReady: boolean

  authError: string

  setUser: (u: User | null) => void

  setView: (v: View) => void

  toggleTheme: () => void

  setDashboardMenuOpen: (open: boolean) => void

  setAccentColor: (color: string) => void

  signOut: () => Promise<void>
}

const Ctx = createContext<AppCtx>({} as AppCtx)

export const useApp = () => useContext(Ctx)

function hasPendingVerification() {
  if (restoreDriverRegistration()) return true
  return restoreOwnerRegistration() !== null
}

function initialView(): View {
  if (hasPendingVerification()) return "sign-up"
  try {
    if (sessionStorage.getItem("sp_pending_driver_registration") ||
        sessionStorage.getItem("sp_pending_owner_registration"))
      return "sign-in"
  } catch {
    /* Login remains available through the navigation. */
  }
  const savedView = window.history.state?.smartParkingView
  if (isView(savedView) && !["driver", "owner", "operator", "admin"].includes(savedView)) return savedView
  return "landing"
}

const views: ReadonlySet<string> = new Set<View>([
  "landing", "pricing", "support", "faq", "sign-in", "sign-up",
  "parking-lot-setup", "pending-approval", "business", "about",
  "affiliates", "careers", "driver", "owner", "operator", "admin",
  "terms", "privacy", "payment-refund",
])

function isView(value: unknown): value is View {
  return typeof value === "string" && views.has(value)
}

export function AppProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<User | null>(null)

  const [view, setViewState] = useState<View>(initialView)
  const viewRef = useRef(view)
  const userRef = useRef(user)
  const historyReady = useRef(false)

  const setView = useCallback((next: View) => {
    if (viewRef.current === next) return
    if (historyReady.current && window.location.pathname === "/") {
      const current = window.history.state ?? {}
      window.history.pushState({
        ...current,
        idx: typeof current.idx === "number" ? current.idx + 1 : 1,
        smartParkingView: next,
        smartParkingPage: undefined,
      }, "", window.location.href)
    }
    viewRef.current = next
    setViewState(next)
  }, [])

  const [theme, setTheme] = useState<"light" | "dark">(() => {
    try {
      const saved = localStorage.getItem("sp_theme")
      if (saved === "light" || saved === "dark") return saved
    } catch {
      /* The system preference still works when storage is unavailable. */
    }
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
  })

  const [dashboardMenuOpen, setDashboardMenuOpen] = useState(false)

  const [accentColor, setAccentColorState] = useState("blue")

  const [authReady, setAuthReady] = useState(false)

  const [authError, setAuthError] = useState("")

  const logoutPending = useRef(false)

  const authRevision = useRef(0)

  useEffect(() => {
    if (!authReady) return
    if (window.location.pathname === "/") {
      window.history.replaceState({
        ...window.history.state,
        smartParkingView: viewRef.current,
      }, "", window.location.href)
    }
    historyReady.current = true
  }, [authReady])

  useEffect(() => {
    const restoreView = (event: PopStateEvent) => {
      if (!["/", "/dashboard/finance", "/dashboard/operation", "/pos"].includes(window.location.pathname) || !isView(event.state?.smartParkingView)) return
      const requested = event.state.smartParkingView as View
      const isProtected = ["driver", "owner", "operator", "admin"].includes(requested)
      const next = isProtected && userRef.current?.role !== requested ? "landing" : requested
      if (next !== requested) {
        window.history.replaceState({ ...event.state, smartParkingView: next }, "", window.location.href)
      }
      viewRef.current = next
      setViewState(next)
    }
    window.addEventListener("popstate", restoreView)
    return () => window.removeEventListener("popstate", restoreView)
  }, [])

  useEffect(() => {
    store.init()

    const savedTheme = localStorage.getItem("sp_theme")
    const systemTheme = window.matchMedia("(prefers-color-scheme: dark)")
    const initialTheme = savedTheme === "light" || savedTheme === "dark"
      ? savedTheme
      : systemTheme.matches ? "dark" : "light"
    setTheme(initialTheme)
    document.documentElement.classList.toggle("dark", initialTheme === "dark")

    const followSystemTheme = (event: MediaQueryListEvent) => {
      if (localStorage.getItem("sp_theme")) return
      setTheme(event.matches ? "dark" : "light")
      document.documentElement.classList.toggle("dark", event.matches)
    }
    systemTheme.addEventListener("change", followSystemTheme)

    const savedAccent = localStorage.getItem("sp_accent") || "blue"

    setAccentColorState(savedAccent)

    delete document.documentElement.dataset.accent

    // Remove the old local-only login marker. It must never authenticate a user.

    store.setCurrentUserId(null)

    let active = true

    const revision = authRevision.current

    const expire = () => {
      authRevision.current++

      authApi.clearSession()

      setUserState(null)
      userRef.current = null

      store.setCurrentUserId(null)

      setDashboardMenuOpen(false)

      setView("landing")

      setAuthError("Your session has expired. Please sign in again.")
    }

    window.addEventListener(authExpiredEvent, expire)

    void authApi
      .currentUser()
      .then((account) => {
        if (!active || revision !== authRevision.current) return

        if (account) {
          store.saveUser(account)

          store.setCurrentUserId(account.id)

          setUserState(account)
          userRef.current = account

          setView(account.role)
        }
      })
      .catch((error) => {
        if (active && revision === authRevision.current) {
          // The API layer clears invalid tokens on 401; temporary failures keep them for retry.

          setAuthError(
            error instanceof Error
              ? error.message
              : "Cannot restore your session.",
          )
        }
      })
      .finally(() => {
        if (active) setAuthReady(true)
      })

    return () => {
      active = false
      systemTheme.removeEventListener("change", followSystemTheme)
      window.removeEventListener(authExpiredEvent, expire)
    }
  }, [])

  useEffect(() => {
    if (authError !== "Your session has expired. Please sign in again.") return
    const timer = window.setTimeout(() => setAuthError(""), 5_200)
    return () => window.clearTimeout(timer)
  }, [authError])

  // Requests refresh an expiring access token on demand. An unattended tab must
  // neither discard a valid refresh session nor keep it alive with a timer.

  useEffect(() => {
    store.remindExpiringBookings()

    const timer = window.setInterval(
      () => store.remindExpiringBookings(),
      30_000,
    )

    return () => window.clearInterval(timer)
  }, [])

  const setUser = (u: User | null) => {
    authRevision.current++

    setUserState(u)
    userRef.current = u

    setAuthError("")

    store.setCurrentUserId(u?.id ?? null)
  }

  const toggleTheme = () => {
    const next = theme === "light" ? "dark" : "light"

    setTheme(next)

    localStorage.setItem("sp_theme", next)

    if (next === "dark") document.documentElement.classList.add("dark")
    else document.documentElement.classList.remove("dark")
  }

  useEffect(() => {
    const themeMeta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    if (themeMeta) themeMeta.content = theme === "dark" ? "#151C1B" : "#F5F7F5"
  }, [theme])

  const setAccentColor = (color: string) => {
    setAccentColorState(color)

    localStorage.setItem("sp_accent", color)
  }

  const signOut = async () => {
    if (logoutPending.current) return

    logoutPending.current = true

    const revision = authRevision.current

    setAuthError("")

    try {
      await authApi.logout()

      // A completed request for an older session must not sign out a newer login.

      if (revision !== authRevision.current) return

      setUser(null)

      setView("landing")

      setDashboardMenuOpen(false)

      store.addAuditLog({
        userId: user?.id ?? "",
        userName: user?.name ?? "",
        userRole: user?.role ?? "driver",
        action: "SIGN_OUT",
        details: "User signed out",
      })
      showUiToast("Signed out", "You have signed out of SmartParking.")
    } catch (error) {
      // Keep the session visible so the user can retry; do not claim revocation succeeded.

      if (revision === authRevision.current) {
        setAuthError(
          error instanceof Error
            ? error.message
            : "Sign out failed. Please try again.",
        )
      }
    } finally {
      logoutPending.current = false
    }
  }

  return (
    <Ctx.Provider
      value={{
        user,
        view,
        theme,
        accentColor,
        dashboardMenuOpen,
        authReady,
        authError,
        setUser,
        setView,
        toggleTheme,
        setDashboardMenuOpen,
        setAccentColor,
        signOut,
      }}
    >
      {children}
    </Ctx.Provider>
  )
}
