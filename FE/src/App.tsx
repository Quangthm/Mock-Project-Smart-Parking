import { useEffect, useLayoutEffect } from "react"
import { AnimatePresence, motion, useReducedMotion } from "motion/react"

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
import { AppToastHost } from "./components/feedback/AppToastHost"

import { Landing } from "./public pages/Landing"

import { Pricing } from "./public pages/Pricing"

import { Support } from "./public pages/Support"

import { FAQ } from "./public pages/FAQ"

import { SignIn } from "./roles/authentications/Sign In/SignIn"

import { SignUp } from "./roles/authentications/Sign Up/SignUp"
import { BusinessLotSetupPage } from "./roles/owner/parking-lots/CreateParkingLotForOwner"
import { OwnerContractPage } from "./roles/owner/parking-lots/OwnerContractPage"

import { BootstrapPassword } from "./roles/authentications/BootstrapPassword"
import { PaymentResultPage } from "./roles/driver/payments/PaymentResultPage"

import { DriverDashboard } from "./roles/driver/dashboard/DriverDashboard"

import { OwnerDashboard } from "./roles/owner/dashboard/OwnerDashboard"

import { OperatorDashboard } from "./roles/operator/dashboard/OperatorDashboard"

import { AdminDashboard } from "./roles/admin/dashboard/AdminDashboard"
import { AdminContractPage } from "./roles/admin/applications/AdminContractPage"

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
  const reduceMotion = useReducedMotion()

  const sessionExpired = authError === "Your session has expired. Please sign in again."
  const accessRevoked = !user && view === "sign-in" && authError !== "" && !sessionExpired

  useEffect(() => {
    if ((sessionExpired || accessRevoked) && location.pathname !== "/") navigate("/", { replace: true })
  }, [location.pathname, navigate, sessionExpired, accessRevoked])

  useLayoutEffect(() => {
    scrollPageToTop()
  }, [location.pathname, view])

  useEffect(() => {
    const previous = window.history.scrollRestoration
    window.history.scrollRestoration = "manual"
    return () => { window.history.scrollRestoration = previous }
  }, [])

  useEffect(() => {
    const isWorkspacePath = ["/dashboard/finance", "/dashboard/operation", "/pos"].includes(location.pathname)
    if (!authReady || (location.pathname !== "/" && !isWorkspacePath) || window.history.state?.smartParkingView) return
    window.history.replaceState({
      ...window.history.state,
      smartParkingView: view,
    }, "", window.location.href)
  }, [authReady, location.pathname, view])

  const isOperatorPath = [
    "/dashboard/finance",
    "/dashboard/operation",
    "/pos",
  ].includes(location.pathname)
  const isEmailOtpPath = location.pathname === "/sign-in/email-otp"
  const isOwnerContractPath = location.pathname.startsWith("/owner/contract/")
  const isAdminContractPath = /^\/admin\/applications\/[^/]+\/contract$/.test(location.pathname)
  const isContractPath = isOwnerContractPath || isAdminContractPath

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

  const isDashboard = !isEmailOtpPath && !isContractPath && (
    (user && ["driver", "owner", "operator", "admin"].includes(view)) ||
    isOperatorPath
  )

  if (location.pathname === "/change-password") return <BootstrapPassword />
  if (location.pathname === "/payment/success" || location.pathname === "/payment/fail") return <PaymentResultPage />

  const usesDashboardNavbar = !isEmailOtpPath && !isContractPath && (
    ["driver", "owner", "operator", "admin"].includes(view) || isOperatorPath
  )

  return (
    <div
      className={isDashboard ? `dashboard-theme${view === "driver" ? " driver-theme" : ""}` : view === "sign-in" || view === "sign-up" || isEmailOtpPath ? "auth-route" : undefined}
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
      {!isContractPath && (usesDashboardNavbar ? <DashboardNavbar /> : <Navbar />)}
      <main id="main-content" tabIndex={-1} style={{ flex: 1 }}>
        {!authReady && (
          <div className="p-8 text-center text-sm text-[var(--muted)]">
            Loading account…
          </div>
        )}
        <AnimatePresence mode="wait" initial={false}>
        {authReady && (
          <motion.div key={`${location.pathname}-${view}`} className="app-route-view"
            initial={{ opacity: 0, y: reduceMotion ? 0 : 8 }}
            animate={{ opacity: 1, y: 0, transition: { duration: reduceMotion ? 0.12 : 0.26, ease: "easeOut" } }}
            exit={{ opacity: 0, y: reduceMotion ? 0 : -6, transition: { duration: 0.14, ease: "easeOut" } }}>
            <Routes>
              <Route path="/" element={<LegacyView view={view} />} />
              <Route path="/sign-in/email-otp" element={<SignIn flow="email-otp" />} />
              <Route path="/owner/contract/:applicationId" element={<OwnerContractPage />} />
              <Route path="/admin/applications/:applicationId/contract" element={<RoleRoute allowedRoles={["admin"]}><AdminContractPage /></RoleRoute>} />
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
          </motion.div>
        )}
        </AnimatePresence>
      </main>
      {authReady && user && usesDashboardNavbar && view !== "driver" && (
        <FloatingChatbot key={user.id} role={user.role} />
      )}
      {authReady && !user && view === "landing" && <FloatingChatbot role="driver" />}
      {!isDashboard && !isContractPath && <Footer />}
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
        <AppToastHost />
      </AppProvider>
    </BrowserRouter>
  )
}
