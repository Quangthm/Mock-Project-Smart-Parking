import {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  type ReactNode,
} from "react"

import type { User } from "../lib/types"

import { store } from "../lib/store"

import { authApi, authExpiredEvent } from "../lib/authApi"

export type View = "landing" | "pricing" | "support" | "faq" | "sign-in" | "sign-up" | "pending-approval" | "business" | "about" | "affiliates" | "careers" | "driver" | "owner" | "operator" | "admin" | "terms" | "privacy" | "payment-refund"

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

export function AppProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<User | null>(null)

  const [view, setView] = useState<View>("landing")

  const [theme, setTheme] = useState<"light" | "dark">("light")

  const [dashboardMenuOpen, setDashboardMenuOpen] = useState(false)

  const [accentColor, setAccentColorState] = useState("blue")

  const [authReady, setAuthReady] = useState(false)

  const [authError, setAuthError] = useState("")

  const logoutPending = useRef(false)

  const authRevision = useRef(0)

  useEffect(() => {
    store.init()

    const savedTheme =
      localStorage.getItem("sp_theme") as "light" | "dark" || "light"

    setTheme(savedTheme)

    if (savedTheme === "dark") document.documentElement.classList.add("dark")

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

      store.setCurrentUserId(null)

      setDashboardMenuOpen(false)

      setView("sign-in")

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
      window.removeEventListener(authExpiredEvent, expire)
    }
  }, [])

  useEffect(() => {
    if (!user) return

    const remaining = authApi.expiresAt() - Date.now()

    const timer = window.setTimeout(
      () => window.dispatchEvent(new Event(authExpiredEvent)),
      Math.max(0, remaining),
    )

    return () => window.clearTimeout(timer)
  }, [user])

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
