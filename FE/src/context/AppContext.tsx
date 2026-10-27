import {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  type ReactNode,
} from "react"

import type { User, LotType } from "../lib/types"
import type { OwnerApplicationRecord } from "../lib/ownerApi"

import { store } from "../lib/store"

import { authApi, authExpiredEvent } from "../lib/authApi"
import { restoreDriverRegistration } from "../roles/authentications/Sign Up/DriverOtpForm"

export type View = "landing" | "pricing" | "support" | "faq" | "sign-in" | "sign-up" | "parking-lot-setup" | "pending-approval" | "business" | "about" | "affiliates" | "careers" | "driver" | "owner" | "operator" | "admin" | "terms" | "privacy" | "payment-refund"

export type PendingOwnerSetup = {
  id: string
  email: string
  lotType: LotType
  setupToken?: string
  verification?: OwnerApplicationRecord["verification"]
  lotSetupComplete?: boolean
}

interface AppCtx {
  user: User | null

  view: View

  theme: "light" | "dark"

  dashboardMenuOpen: boolean

  accentColor: string

  authReady: boolean

  authError: string

  pendingOwnerSetup: PendingOwnerSetup | null

  setPendingOwnerSetup: (val: PendingOwnerSetup | null) => void

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
  try {
    const owner = JSON.parse(
      sessionStorage.getItem("sp_pending_owner_registration") ?? "null",
    )
    return (
      typeof owner?.email === "string" &&
      owner?.lotSetupComplete === true &&
      typeof owner?.verification?.challengeId === "string" &&
      Number.isFinite(Date.parse(owner.verification.expiresAt)) &&
      Number.isFinite(Date.parse(owner.verification.resendAt))
    )
  } catch {
    return false
  }
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
  return "landing"
}

export function AppProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<User | null>(null)

  const [view, setView] = useState<View>(initialView)

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

  const [pendingOwnerSetup, setPendingOwnerSetup] = useState<PendingOwnerSetup | null>(null)

  const logoutPending = useRef(false)

  const authRevision = useRef(0)

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
      systemTheme.removeEventListener("change", followSystemTheme)
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
        pendingOwnerSetup,
        setPendingOwnerSetup,
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
