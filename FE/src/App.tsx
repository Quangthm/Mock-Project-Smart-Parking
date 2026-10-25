import { useEffect, useLayoutEffect } from "react"

import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom"

import { AppProvider, useApp } from "./context/AppContext"

import { Navbar } from "./components/layout/Navbar"

import { DashboardNavbar } from "./components/layout/DashboardNavbar"

import { RoleRoute } from "./components/navigation/RoleRoute"

import { Footer } from "./components/layout/Footer"

import { FloatingChatbot } from "./components/chat/FloatingChatbot"

import { Landing } from "./public pages/Landing"

import { Pricing } from "./public pages/Pricing"

import { Support } from "./public pages/Support"

import { FAQ } from "./public pages/FAQ"

import { SignIn } from "./roles/authentications/Sign In/SignIn"

import { SignUp } from "./roles/authentications/Sign Up/SignUp"
import { BusinessLotSetupPage } from "./roles/owner/parking-lots/CreateParkingLotForOwner"

import { BootstrapPassword } from "./roles/authentications/BootstrapPassword"

import { DriverDashboard } from "./roles/driver/dashboard/DriverDashboard"

import { OwnerDashboard } from "./roles/owner/dashboard/OwnerDashboard"

import { OperatorDashboard } from "./roles/operator/dashboard/OperatorDashboard"

import { AdminDashboard } from "./roles/admin/dashboard/AdminDashboard"

import { TermsOfService } from "./public pages/policies/TermsOfService"

import { PrivacyPolicy } from "./public pages/policies/PrivacyPolicy"

import { PaymentRefund } from "./public pages/policies/PaymentRefund"

import { Business } from "./public pages/Business"

import { About } from "./public pages/About"

import { Affiliates } from "./public pages/Affiliates"

import { Careers } from "./public pages/Careers"

import { PendingApproval } from "./public pages/PendingApproval"

import { getUserHomePath } from "./roles/operator/data/roleRoutes"
import { scrollPageToTop } from "./lib/scrollPageToTop"

function AppContent() {
  const { view, user, authReady, authError, accentColor } = useApp()

  const location = useLocation()

  const navigate = useNavigate()

  useLayoutEffect(() => {
    scrollPageToTop()
  }, [location.pathname, view])

  useEffect(() => {
    const previous = window.history.scrollRestoration
    window.history.scrollRestoration = "manual"
    return () => { window.history.scrollRestoration = previous }
  }, [])

  const isOperatorPath = [
    "/dashboard/finance",
    "/dashboard/operation",
    "/pos",
  ].includes(location.pathname)

  useEffect(() => {
    if (
      authReady &&
      location.pathname === "/" &&
      view === "operator" &&
      user?.role === "operator"
    ) {
      navigate(getUserHomePath(user), { replace: true })
    }
  }, [authReady, location.pathname, navigate, user, view])

  const isDashboard =
    (user && ["driver", "owner", "operator", "admin"].includes(view)) ||
    isOperatorPath

  if (location.pathname === "/change-password") return <BootstrapPassword />

  const usesDashboardNavbar =
    ["driver", "owner", "operator", "admin"].includes(view) || isOperatorPath

  return (
    <div
      className={isDashboard ? `dashboard-theme${view === "driver" ? " driver-theme" : ""}` : view === "sign-in" || view === "sign-up" ? "auth-route" : undefined}
      data-accent={accentColor}
      style={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        background: "var(--bg)",
        color: "var(--fg)",
      }}
    >
      <a className="skip-link" href="#main-content">Skip to main content</a>
      {usesDashboardNavbar ? <DashboardNavbar /> : <Navbar />}
      <main id="main-content" tabIndex={-1} style={{ flex: 1 }}>
        {authError && (
          <div role="alert" className="p-4 text-center text-red-600">
            {authError}
          </div>
        )}
        {!authReady && (
          <div className="p-8 text-center text-sm text-[var(--muted)]">
            Loading account…
          </div>
        )}
        {authReady && (
          <div key={`${location.pathname}-${view}`} className="animate-in">
            <Routes>
              <Route path="/" element={<LegacyView view={view} />} />
              <Route
                path="/dashboard/finance"
                element={
                  <RoleRoute allowedRoles={["financial", "owner"]}>
                    <OperatorDashboard accessRoleOverride="financial" />
                  </RoleRoute>
                }
              />
              <Route
                path="/dashboard/operation"
                element={
                  <RoleRoute allowedRoles={["operation"]}>
                    <OperatorDashboard accessRoleOverride="operation" />
                  </RoleRoute>
                }
              />
              <Route
                path="/pos"
                element={
                  <RoleRoute allowedRoles={["cashier"]}>
                    <OperatorDashboard accessRoleOverride="cashier" />
                  </RoleRoute>
                }
              />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </div>
        )}
      </main>
      {authReady && user && usesDashboardNavbar && view !== "driver" && (
        <FloatingChatbot key={user.id} role={user.role} />
      )}
      {!isDashboard && <Footer />}
    </div>
  )
}

function LegacyView({ view }: { view: ReturnType<typeof useApp>["view"] }) {
  return (
    <>
      {view === "landing" && <Landing />}
      {view === "pricing" && <Pricing />}
      {view === "support" && <Support />}
      {view === "faq" && <FAQ />}
      {view === "sign-in" && <SignIn />}
      {view === "sign-up" && <SignUp />}
      {view === "parking-lot-setup" && <BusinessLotSetupPage />}
      {view === "driver" && <DriverDashboard />}
      {view === "owner" && <OwnerDashboard />}
      {view === "operator" && <OperatorDashboard />}
      {view === "admin" && <AdminDashboard />}
      {view === "terms" && <TermsOfService />}
      {view === "privacy" && <PrivacyPolicy />}
      {view === "payment-refund" && <PaymentRefund />}
      {view === "business" && <Business />}
      {view === "about" && <About />}
      {view === "affiliates" && <Affiliates />}
      {view === "careers" && <Careers />}
      {view === "pending-approval" && <PendingApproval />}
    </>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AppProvider>
        <AppContent />
      </AppProvider>
    </BrowserRouter>
  )
}
