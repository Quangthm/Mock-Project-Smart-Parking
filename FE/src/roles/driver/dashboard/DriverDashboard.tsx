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
  const [securityForm, setSecurityForm] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" })
  const [profileMessage, setProfileMessage] = useState("")
  const [vehicles, setVehicles] = useState(() => user ? store.getVehiclesByDriver(user.id) : [])
  const [vehicleForm, setVehicleForm] = useState({ type: "motorcycle" as "motorcycle" | "car", plateType: "vn" as "vn" | "foreign", plate: "", plateImageUrl: "" })
  const [editingVehicleId, setEditingVehicleId] = useState<string | null>(null)

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

  function addVehicle(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!user) return
    const plate = vehicleForm.plate.trim().toUpperCase().replace(/\s+/g, "")
    const valid = vehicleForm.plateType === "foreign"
      ? /^(?=.*[A-Z])(?=.*\d)[A-Z0-9-]{4,12}$/.test(plate)
      : vehicleForm.type === "car"
        ? /^\d{2}[A-Z]{1,2}-?\d{4,5}$/.test(plate)
        : /^(\d{2}-?[A-Z]\d?-?\d{4,5}|\d{2}V[A-Z]-?\d{5})$/.test(plate)
    if (!valid) {
      setProfileMessage("Please enter a valid license plate for the selected vehicle and region.")
      return
    }
    if (editingVehicleId) {
      const existing = vehicles.find(vehicle => vehicle.id === editingVehicleId)
      if (!existing || !store.updateVehicle({ ...existing, type: vehicleForm.type, plateType: vehicleForm.plateType, licensePlate: plate, plateImageUrl: vehicleForm.plateImageUrl || undefined })) {
        setProfileMessage("This license plate is already saved.")
        return
      }
      setVehicles(store.getVehiclesByDriver(user.id))
      setEditingVehicleId(null)
      setVehicleForm(form => ({ ...form, plate: "", plateImageUrl: "" }))
      setProfileMessage("Vehicle updated.")
      return
    }
    const created = store.createVehicle({ driverId: user.id, type: vehicleForm.type, plateType: vehicleForm.plateType, licensePlate: plate, plateImageUrl: vehicleForm.plateImageUrl || undefined })
    if (!created) {
      setProfileMessage(vehicles.length >= 3 ? "You can save up to 3 vehicles." : "This license plate is already saved.")
      return
    }
    setVehicles((current) => [...current, created])
    setVehicleForm((form) => ({ ...form, plate: "", plateImageUrl: "" }))
    setProfileMessage("Vehicle saved. You can select it when booking.")
  }

  function removeVehicle(id: string) {
    if (!user) return
    store.deleteVehicle(id, user.id)
    setVehicles(store.getVehiclesByDriver(user.id))
    setProfileMessage("Vehicle removed.")
  }

  function editVehicle(id: string) {
    const vehicle = vehicles.find(item => item.id === id)
    if (!vehicle) return
    setEditingVehicleId(id)
    setVehicleForm({ type: vehicle.type, plateType: vehicle.plateType, plate: vehicle.licensePlate, plateImageUrl: vehicle.plateImageUrl ?? "" })
    setProfileMessage("")
  }

  function openProfileSection(section: ProfileSection) {
    setProfileSection(section)
    setProfileMessage("")
    if (section === "account") {
      setAccountForm({ name: user?.name ?? "", phone: user?.phone ?? "" })
    }
    if (section === "security") {
      setSecurityForm({ currentPassword: "", newPassword: "", confirmPassword: "" })
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

  function saveSecurity(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!user) return
    if (securityForm.currentPassword !== user.password) {
      setProfileMessage("Your current password is incorrect.")
      return
    }
    if (securityForm.newPassword.length < 8) {
      setProfileMessage("Your new password must be at least 8 characters.")
      return
    }
    if (securityForm.newPassword !== securityForm.confirmPassword) {
      setProfileMessage("The new passwords do not match.")
      return
    }
    const updatedUser = { ...user, password: securityForm.newPassword }
    store.saveUser(updatedUser)
    setUser(updatedUser)
    setSecurityForm({ currentPassword: "", newPassword: "", confirmPassword: "" })
    setProfileMessage("Your password has been updated.")
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

              {profileSection === "security" && (
                <form onSubmit={saveSecurity} style={{ display: "grid", gap: "1rem", padding: "0 1.5rem 1.5rem" }}>
                  <p style={{ margin: 0, color: "var(--muted)", fontSize: "0.85rem", lineHeight: 1.5 }}>Choose a strong password to help protect your SmartParking account.</p>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    Current password
                    <input className="input" type="password" value={securityForm.currentPassword} onChange={(e) => setSecurityForm((form) => ({ ...form, currentPassword: e.target.value }))} autoComplete="current-password" />
                  </label>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    New password
                    <input className="input" type="password" value={securityForm.newPassword} onChange={(e) => setSecurityForm((form) => ({ ...form, newPassword: e.target.value }))} autoComplete="new-password" />
                  </label>
                  <label style={{ display: "grid", gap: "0.4rem", fontSize: "0.82rem", fontWeight: 600 }}>
                    Confirm new password
                    <input className="input" type="password" value={securityForm.confirmPassword} onChange={(e) => setSecurityForm((form) => ({ ...form, confirmPassword: e.target.value }))} autoComplete="new-password" />
                  </label>
                  {profileMessage && <p role="status" style={{ margin: 0, color: profileMessage.includes("updated") ? "var(--primary)" : "#dc2626", fontSize: "0.82rem" }}>{profileMessage}</p>}
                  <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.625rem", marginTop: "0.25rem" }}>
                    <button className="btn-outline" type="button" onClick={() => setProfileSection(null)}>Cancel</button>
                    <button className="btn-primary" type="submit">Update Password</button>
                  </div>
                </form>
              )}

              {profileSection === "vehicles" && (
                <div style={{ display: "grid", gap: "1rem", padding: "0 1.5rem 1.5rem" }}>
                  <p style={{ margin: 0, color: "var(--muted)", fontSize: "0.85rem", lineHeight: 1.5 }}>Save up to 3 vehicles. Choose a saved vehicle during booking so you do not need to enter its plate again.</p>
                  {!vehicles.length && <p style={{ margin: 0, padding: "0.75rem 0", color: "var(--muted)", fontSize: "0.82rem" }}>No vehicles saved yet.</p>}
                  {vehicles.map((vehicle) => (
                    <div key={vehicle.id} style={{ display: "flex", alignItems: "center", gap: "0.75rem", padding: "0.85rem", border: "1px solid var(--border)", borderRadius: "0.875rem", background: "var(--card)" }}>
                      {vehicle.plateImageUrl && <img src={vehicle.plateImageUrl} alt={`License plate ${vehicle.licensePlate}`} style={{ width: 72, height: 48, objectFit: "cover", borderRadius: "0.4rem", border: "1px solid var(--border)" }} />}
                      <span aria-hidden="true" style={{ color: 'var(--primary)', display: 'inline-flex' }}><UntitledIcon name={vehicle.type === "car" ? "car" : "motorcycle"} size={20} /></span>
                      <span style={{ minWidth: 0, flex: 1 }}>
                        <span style={{ display: "block", fontWeight: 700, letterSpacing: "0.04em" }}>{vehicle.licensePlate}</span>
                        <span style={{ color: "var(--muted)", fontSize: "0.75rem" }}>{vehicle.type === "car" ? "Car" : "Motorcycle"} · {vehicle.plateType === "foreign" ? "International" : "Vietnam"}</span>
                      </span>
                      <button className="btn-outline" type="button" onClick={() => editVehicle(vehicle.id)} style={{ padding: "0.35rem 0.6rem", fontSize: "0.75rem" }}>Edit</button>
                      <button className="btn-outline" type="button" onClick={() => removeVehicle(vehicle.id)} style={{ padding: "0.35rem 0.6rem", fontSize: "0.75rem" }}>Delete</button>
                    </div>
                  ))}
                  {vehicles.length < 3 || editingVehicleId ? (
                    <form onSubmit={addVehicle} style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem", padding: "1rem", border: "1px solid var(--border)", borderRadius: "0.875rem" }}>
                      <label style={{ display: "grid", gap: "0.35rem", fontSize: "0.78rem", fontWeight: 600 }}>Vehicle type
                        <select className="input" value={vehicleForm.type} onChange={(event) => setVehicleForm((form) => ({ ...form, type: event.target.value as "motorcycle" | "car" }))}>
                          <option value="motorcycle">Motorcycle</option><option value="car">Car</option>
                        </select>
                      </label>
                      <label style={{ display: "grid", gap: "0.35rem", fontSize: "0.78rem", fontWeight: 600 }}>Plate region
                        <select className="input" value={vehicleForm.plateType} onChange={(event) => setVehicleForm((form) => ({ ...form, plateType: event.target.value as "vn" | "foreign" }))}>
                          <option value="vn">Vietnam</option><option value="foreign">International</option>
                        </select>
                      </label>
                      <label style={{ display: "grid", gap: "0.35rem", gridColumn: "1 / -1", fontSize: "0.78rem", fontWeight: 600 }}>License plate
                        <input className="input" value={vehicleForm.plate} onChange={(event) => { setVehicleForm((form) => ({ ...form, plate: event.target.value })); setProfileMessage("") }} placeholder={vehicleForm.plateType === "vn" ? "e.g. 51A-12345" : "e.g. ABC-1234"} />
                      </label>
                      <label style={{ display: "grid", gap: "0.35rem", gridColumn: "1 / -1", fontSize: "0.78rem", fontWeight: 600 }}>License plate image (optional)
                        <input className="input" type="file" accept="image/*" onChange={(event) => {
                          const file = event.target.files?.[0]
                          if (!file) return
                          if (file.size > 1_500_000) { setProfileMessage("Choose an image smaller than 1.5 MB."); return }
                          const reader = new FileReader()
                          reader.onload = () => setVehicleForm((form) => ({ ...form, plateImageUrl: String(reader.result ?? "") }))
                          reader.readAsDataURL(file)
                        }} />
                        {vehicleForm.plateImageUrl && <img src={vehicleForm.plateImageUrl} alt="License plate preview" style={{ width: 160, height: 80, objectFit: "cover", borderRadius: "0.5rem", border: "1px solid var(--border)" }} />}
                      </label>
                      {profileMessage && <p role="status" style={{ gridColumn: "1 / -1", margin: 0, color: profileMessage.includes("saved") || profileMessage.includes("removed") ? "var(--primary)" : "#dc2626", fontSize: "0.78rem" }}>{profileMessage}</p>}
                      <button className="btn-primary" type="submit" style={{ gridColumn: "1 / -1", justifyContent: "center" }}>{editingVehicleId ? "Save Vehicle" : "Add Vehicle"}</button>
                    </form>
                  ) : <p style={{ margin: 0, color: "var(--muted)", fontSize: "0.8rem" }}>Vehicle limit reached (3/3).</p>}
                  <div style={{ display: "flex", justifyContent: "flex-end" }}>
                    <button className="btn-outline" type="button" onClick={() => setProfileSection(null)}>Done</button>
                  </div>
                </div>
              )}
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
