import { useEffect, useState, type FormEvent } from "react"
import { useApp } from "../../../context/AppContext"
import { driverData as store } from "../data/data"
import type { ParkingLot } from "../../../lib/types"
import type { Booking } from "../../../lib/types"
import { FindParking } from "../parking/FindParking"
import { BookingFlow } from "../booking/BookingFlow"
import { ActiveBookings } from "../booking/ActiveBookings"
import { Subscriptions } from "../subcriptions/Subscriptions"
import { BookingHistory } from "../history/BookingHistory"
import { DriverSupport } from "../support/DriverSupport"
import { DashboardSidebar } from "../../../components/layout/DashboardSidebar"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import { VehicleManager } from "./VehicleManager"
import { AccountSecurityPanel } from "../../../components/forms/AccountSecurityPanel"

type Tab = "home" | "subscriptions" | "support"
type HomePanel = "find" | "bookings" | "history"
type ProfileSection = "account" | "security" | "vehicles"

export function DriverDashboard() {
  const { user, setUser } = useApp()
  const [tab, setTab] = useState<Tab>("home")
  const [homePanel, setHomePanel] = useState<HomePanel>("find")
  const [bookingLot, setBookingLot] = useState<ParkingLot | null>(null)
  const [paymentBooking, setPaymentBooking] = useState<Booking | null>(null)
  const [profileSection, setProfileSection] = useState<ProfileSection | null>(null)
  const [accountForm, setAccountForm] = useState({ name: "", phone: "" })
  const [profileMessage, setProfileMessage] = useState("")

  useEffect(() => {
    const onNavigate = (event: Event) => {
      const id = (event as CustomEvent<string>).detail
      if (id === 'find' || id === 'bookings' || id === 'history') { setTab('home'); setHomePanel(id); setBookingLot(null) }
      if (id === 'subscriptions' || id === 'support') { setTab(id); setBookingLot(null) }
    }
    const onProfile = (event: Event) => openProfileSection((event as CustomEvent<ProfileSection>).detail)
    window.addEventListener('sp:dashboard-nav', onNavigate)
    window.addEventListener('sp:driver-profile', onProfile)
    return () => { window.removeEventListener('sp:dashboard-nav', onNavigate); window.removeEventListener('sp:driver-profile', onProfile) }
  })

  const bookings = store.getBookingsByDriver(user?.id ?? "")
  const active = bookings.filter((b) =>
    !["completed", "cancelled"].includes(b.status),
  )

  function openProfileSection(section: ProfileSection) {
    setProfileSection(section)
    setProfileMessage("")
    if (section === "account") {
      setAccountForm({ name: user?.name ?? "", phone: user?.phone ?? "" })
    }
  }

  function saveAccount(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!user) return
    const name = accountForm.name.trim()
    if (!name) {
      setProfileMessage("Please enter your name.")
      return
    }
    const updatedUser = { ...user, name, phone: accountForm.phone.trim() || undefined }
    store.saveUser(updatedUser)
    setUser(updatedUser)
    setProfileMessage("Your account settings have been saved.")
  }

  const navGroups = [
    { items: [{ id: "find", label: "Dashboard", icon: "⌂", active: tab === "home" && homePanel === "find", onClick: () => { setTab("home"); setHomePanel("find"); setBookingLot(null) } }] },
    { label: "Booking", items: [
      { id: "bookings", label: "Current booking", icon: "▤", active: tab === "home" && homePanel === "bookings", onClick: () => { setTab("home"); setHomePanel("bookings"); setBookingLot(null) } },
      { id: "history", label: "Booking history", icon: "◷", active: tab === "home" && homePanel === "history", onClick: () => { setTab("home"); setHomePanel("history"); setBookingLot(null) } },
      { id: "subscriptions", label: "Passes", icon: "▣", active: tab === "subscriptions", onClick: () => { setTab("subscriptions"); setBookingLot(null) } },
    ] },
    { label: "Help", items: [{ id: "support", label: "Support", icon: "?", active: tab === "support", onClick: () => { setTab("support"); setBookingLot(null) } }] },
  ]
  return (
    <DashboardSidebar groups={navGroups}>
      <main className="min-w-0 overflow-y-auto p-4 sm:p-7">
        {profileSection && (
          <div
            role="presentation"
            onMouseDown={(e) => {
              if (e.target === e.currentTarget) setProfileSection(null)
            }}
            style={{
              position: "fixed",
              inset: 0,
              zIndex: 1000,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              padding: "1rem",
              background: "rgba(2, 6, 23, 0.62)",
              backdropFilter: "blur(4px)",
            }}
          >
            <section
              role="dialog"
              aria-modal="true"
              aria-labelledby="driver-profile-title"
              style={{
                width: "100%",
                maxWidth: profileSection === "vehicles" ? 760 : 480,
                maxHeight: "min(90vh, 760px)",
                overflowY: "auto",
                border: "1px solid var(--border)",
                borderRadius: "1.125rem",
                background: "var(--bg)",
                color: "var(--fg)",
                boxShadow: "0 24px 70px rgba(2, 6, 23, 0.35)",
              }}
            >
              <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: "1rem", padding: "1.5rem 1.5rem 1rem" }}>
                <div>
                  <div style={{ color: "var(--primary)", fontSize: "0.72rem", fontWeight: 700, letterSpacing: "0.08em", textTransform: "uppercase" }}>Driver profile</div>
                  <h2 id="driver-profile-title" style={{ margin: "0.35rem 0 0", fontFamily: "Outfit", fontSize: "1.35rem", fontWeight: 700 }}>
                    {profileSection === "account" ? "My Account Settings" : profileSection === "security" ? "Security Settings" : "My Vehicles"}
                  </h2>
                </div>
                <button type="button" aria-label="Close profile window" onClick={() => setProfileSection(null)} style={{ width: 34, height: 34, border: "1px solid var(--border)", borderRadius: "50%", background: "var(--card)", color: "var(--fg)", cursor: "pointer", display: 'flex', alignItems: 'center', justifyContent: 'center' }}><UntitledIcon name="x" size={18} /></button>
              </div>

              {profileSection === "account" && (
                <form onSubmit={saveAccount} style={{ display: "grid", gap: "1rem", padding: "0 1.5rem 1.5rem" }}>
                  <p style={{ margin: 0, color: "var(--muted)", fontSize: "0.85rem", lineHeight: 1.5 }}>Keep your contact details up to date for parking and booking notifications.</p>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    Full name
                    <input className="input" value={accountForm.name} onChange={(e) => setAccountForm((form) => ({ ...form, name: e.target.value }))} autoComplete="name" />
                  </label>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    Email address
                    <input className="input" type="email" value={user?.email ?? ""} readOnly aria-describedby="driver-email-note" />
                    <span id="driver-email-note" style={{ color: "var(--muted)", fontSize: "0.72rem", fontWeight: 400 }}>Your email is used to sign in and cannot be changed here.</span>
                  </label>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    Phone number
                    <input className="input" type="tel" value={accountForm.phone} onChange={(e) => setAccountForm((form) => ({ ...form, phone: e.target.value }))} autoComplete="tel" />
                  </label>
                  {profileMessage && <p role="status" style={{ margin: 0, color: "var(--primary)", fontSize: "0.82rem" }}>{profileMessage}</p>}
                  <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.625rem", marginTop: "0.25rem" }}>
                    <button className="btn-outline" type="button" onClick={() => setProfileSection(null)}>Cancel</button>
                    <button className="btn-primary" type="submit">Save Changes</button>
                  </div>
                </form>
              )}

              {profileSection === "security" && <AccountSecurityPanel key={user?.id}/>}

              {profileSection === "vehicles" && <VehicleManager/>}
            </section>
          </div>
        )}
        {bookingLot ? (
            <BookingFlow
              lot={bookingLot}
              resumeBooking={paymentBooking ?? undefined}
              onDone={() => {
                setPaymentBooking(null)
                setBookingLot(null)
              setTab("home")
              setHomePanel("bookings")
            }}
          />
        ) : (
          <>
            {tab === "home" && (
              <div className="animate-in">
                {homePanel === "find" ? (
                  <FindParking onBook={(lot) => { setPaymentBooking(null); setBookingLot(lot) }} />
                ) : homePanel === "bookings" ? (
                  <section><h3 style={{ fontFamily: "Outfit", margin: "0 0 0.75rem", color: "var(--fg)" }}>Bookings</h3><ActiveBookings onPayNow={(booking) => { setPaymentBooking(booking); setBookingLot(store.getLots().find((lot) => lot.id === booking.lotId) ?? null) }} /></section>
                ) : (
                  <section><h3 style={{ fontFamily: "Outfit", margin: "0 0 0.75rem", color: "var(--fg)" }}>Booking History</h3><BookingHistory /></section>
                )}
              </div>
            )}
            {tab === "subscriptions" && (
              <div className="animate-in">
                <Subscriptions />
              </div>
            )}
            {tab === "support" && (
              <div className="animate-in">
                <DriverSupport />
              </div>
            )}
          </>
        )}
      </main>
    </DashboardSidebar>
  )
}
