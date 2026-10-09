import { useState, useMemo } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"
import type { ParkingLot, PackageOrder, PaymentMethod, User } from "../../../lib/types"
import { driverData as store } from "../data/data"
import { PAYMENT_OPTIONS } from "../data/paymentOptions"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"

function getLotEnterprise(lot: ParkingLot, users: User[]): string {
  const owner = users.find(u => u.id === lot.ownerId)
  if (owner?.name && !owner.name.includes("demo")) return owner.name
  const name = lot.name.toLowerCase()
  if (name.includes("vinhomes") || name.includes("vincom")) return "Vingroup Real Estate Corp"
  if (name.includes("bitexco")) return "Bitexco Group"
  if (name.includes("nguyen hue") || name.includes("outdoor")) return "Saigon Urban Infrastructure"
  return "Urban SmartParking Network"
}

function getLotDistance(lot: ParkingLot): number {
  if (lot.id === "lot-002") return 0.4
  if (lot.id === "lot-003") return 0.8
  if (lot.id === "lot-001") return 11.5
  return 1.8
}

function isHotLot(lot: ParkingLot): boolean {
  return lot.id === "lot-001" || lot.id === "lot-002" || lot.totalSlots >= 70
}

function isNearLot(lot: ParkingLot): boolean {
  return getLotDistance(lot) <= 3.0
}

type TabMode = "store" | "my-passes" | "orders"

export function Subscriptions() {
  const { user } = useApp()
  const [tabMode, setTabMode] = useState<TabMode>("store")
  const [searchQuery, setSearchQuery] = useState("")
  const [filterTag, setFilterTag] = useState<"all" | "hot" | "near" | "recommended">("recommended")
  const [selectedLotForDetail, setSelectedLotForDetail] = useState<ParkingLot | null>(null)
  const [selectedPlan, setSelectedPlan] = useState<"monthly" | "annual">("monthly")
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod>("momo")
  const [selectedVehiclePlate, setSelectedVehiclePlate] = useState("")
  const [checkoutSuccess, setCheckoutSuccess] = useState<string | null>(null)

  const users = useMemo(() => store.getUsers?.() ?? [], [])
  const lots = useMemo(() => store.getLots().filter(lot => lot.status === "active"), [])
  const passes = useMemo(() => (user ? store.getPassesByDriver(user.id) : []), [user, checkoutSuccess])
  const orders = useMemo(() => (user ? store.getPackageOrdersByDriver(user.id) : []), [user, checkoutSuccess])
  const vehicles = useMemo(() => (user ? store.getVehiclesByDriver(user.id) : []), [user])

  // Set default vehicle plate
  useMemo(() => {
    if (vehicles.length > 0 && !selectedVehiclePlate) {
      setSelectedVehiclePlate(vehicles[0].licensePlate)
    }
  }, [vehicles, selectedVehiclePlate])

  // Filter lots based on store logic:
  // Default: Only show "Near You" and "Hot" parking lots
  // Search: Search exact matches by Enterprise name OR Lot name OR Address
  const displayLots = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()

    if (q) {
      return lots.filter(lot => {
        const enterprise = getLotEnterprise(lot, users).toLowerCase()
        const lotName = lot.name.toLowerCase()
        const address = lot.address.toLowerCase()
        return lotName.includes(q) || enterprise.includes(q) || address.includes(q)
      })
    }

    // Default view: No query entered
    if (filterTag === "recommended") {
      return lots.filter(lot => isHotLot(lot) || isNearLot(lot))
    }
    if (filterTag === "hot") {
      return lots.filter(lot => isHotLot(lot))
    }
    if (filterTag === "near") {
      return lots.filter(lot => isNearLot(lot))
    }
    return lots
  }, [lots, searchQuery, filterTag, users])

  function handleOpenLotDetail(lot: ParkingLot) {
    setSelectedLotForDetail(lot)
    setSelectedPlan("monthly")
    setSelectedMethod("momo")
    setCheckoutSuccess(null)
  }

  function handlePurchasePass() {
    if (!user || !selectedLotForDetail) return

    const amount = selectedPlan === "monthly" ? selectedLotForDetail.subscriptionMonthly : selectedLotForDetail.subscriptionYearly
    const durationDays = selectedPlan === "monthly" ? 30 : 365
    const now = new Date()
    const expires = new Date(now.getTime() + durationDays * 86400000)

    // Create payment order
    store.createPackageOrder({
      driverId: user.id,
      lotId: selectedLotForDetail.id,
      lotName: selectedLotForDetail.name,
      plan: selectedPlan,
      amount,
      paymentMethod: selectedMethod,
    })

    // Instant pass activation
    store.createPass({
      driverId: user.id,
      lotId: selectedLotForDetail.id,
      lotName: selectedLotForDetail.name,
      plan: selectedPlan,
      amount,
      expiresAt: expires.toISOString(),
    })

    setCheckoutSuccess(`Pass activated successfully for ${selectedLotForDetail.name}! Valid until ${expires.toLocaleDateString("en-GB")}.`)
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* STORE HERO BANNER (Style hàng online gây ấn tượng) */}
      <section
        style={{
          borderRadius: "1rem",
          background: "linear-gradient(135deg, #1e3a8a 0%, #0f172a 100%)",
          border: "1px solid rgba(59, 130, 246, 0.35)",
          padding: "1.75rem 2rem",
          color: "#ffffff",
          boxShadow: "0 10px 30px -5px rgba(30, 58, 138, 0.3)",
          position: "relative",
          overflow: "hidden",
        }}
      >
        <div style={{ position: "relative", zIndex: 2, maxWidth: "780px" }}>
          <div style={{ display: "inline-flex", alignItems: "center", gap: "0.45rem", background: "rgba(59, 130, 246, 0.25)", border: "1px solid rgba(96, 165, 250, 0.4)", padding: "0.2rem 0.65rem", borderRadius: "999px", fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#93c5fd", marginBottom: "0.75rem" }}>
            <UntitledIcon name="shopping-bag" size={14} /> SMART PARKING PASS STORE
          </div>
          <h2 style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.75rem", margin: "0 0 0.5rem", color: "#ffffff", letterSpacing: "-0.02em" }}>
            Unlimited Commuter Passes & Memberships
          </h2>
          <p style={{ margin: "0 0 1.25rem", color: "#cbd5e1", fontSize: "0.92rem", lineHeight: 1.5 }}>
            Subscribe to monthly or annual passes directly from top real-estate enterprises. Enjoy guaranteed reserved spots, 24/7 unlimited access, and up to 40% savings.
          </p>

          {/* STORE VALUE PROPOSITION PILLS */}
          <div style={{ display: "flex", gap: "0.6rem", flexWrap: "wrap" }}>
            <span style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem", background: "rgba(255, 255, 255, 0.1)", border: "1px solid rgba(255, 255, 255, 0.18)", borderRadius: "999px", padding: "0.3rem 0.75rem", fontSize: "0.78rem", fontWeight: 600 }}>
              <UntitledIcon name="zap" size={14} style={{ color: "#38bdf8" }} /> 24/7 Unlimited Access
            </span>
            <span style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem", background: "rgba(255, 255, 255, 0.1)", border: "1px solid rgba(255, 255, 255, 0.18)", borderRadius: "999px", padding: "0.3rem 0.75rem", fontSize: "0.78rem", fontWeight: 600 }}>
              <UntitledIcon name="tag" size={14} style={{ color: "#38bdf8" }} /> Save Up To 40%
            </span>
            <span style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem", background: "rgba(255, 255, 255, 0.1)", border: "1px solid rgba(255, 255, 255, 0.18)", borderRadius: "999px", padding: "0.3rem 0.75rem", fontSize: "0.78rem", fontWeight: 600 }}>
              <UntitledIcon name="car" size={14} style={{ color: "#38bdf8" }} /> Automated Gate Camera Scan
            </span>
            <span style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem", background: "rgba(255, 255, 255, 0.1)", border: "1px solid rgba(255, 255, 255, 0.18)", borderRadius: "999px", padding: "0.3rem 0.75rem", fontSize: "0.78rem", fontWeight: 600 }}>
              <UntitledIcon name="shield" size={14} style={{ color: "#38bdf8" }} /> Guaranteed Reserved Slot
            </span>
          </div>
        </div>
      </section>

      {/* TOP NAVIGATION TABS */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem", borderBottom: "1px solid var(--border)", paddingBottom: "0.75rem" }}>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <button
            type="button"
            className={tabMode === "store" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("store")}
            style={{ padding: "0.45rem 1rem", fontSize: "0.85rem", fontWeight: 600, display: "inline-flex", alignItems: "center", gap: "0.45rem" }}
          >
            <UntitledIcon name="shopping-bag" size={15} />
            <span>Pass Marketplace</span>
          </button>
          <button
            type="button"
            className={tabMode === "my-passes" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("my-passes")}
            style={{ padding: "0.45rem 1rem", fontSize: "0.85rem", fontWeight: 600, display: "inline-flex", alignItems: "center", gap: "0.45rem" }}
          >
            <UntitledIcon name="ticket" size={15} />
            <span>My Passes ({passes.length})</span>
          </button>
          <button
            type="button"
            className={tabMode === "orders" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("orders")}
            style={{ padding: "0.45rem 1rem", fontSize: "0.85rem", fontWeight: 600, display: "inline-flex", alignItems: "center", gap: "0.45rem" }}
          >
            <UntitledIcon name="clipboard" size={15} />
            <span>Order History ({orders.length})</span>
          </button>
        </div>
      </div>

      {/* TAB 1: PASS STORE */}
      {tabMode === "store" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* SEARCH & FILTER BAR */}
          <div className="card" style={{ padding: "1rem 1.25rem", display: "flex", flexDirection: "column", gap: "0.85rem" }}>
            <div style={{ display: "flex", gap: "0.75rem", alignItems: "center", flexWrap: "wrap" }}>
              <div style={{ position: "relative", flex: "1 1 320px" }}>
                <span style={{ position: "absolute", left: "0.85rem", top: "50%", transform: "translateY(-50%)", color: "var(--muted)", pointerEvents: "none" }}>
                  <UntitledIcon name="search" size={17} />
                </span>
                <input
                  className="input"
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  placeholder="Search by enterprise (e.g. Vingroup, Bitexco) or parking lot name..."
                  style={{ paddingLeft: "2.4rem", paddingRight: searchQuery ? "2.4rem" : "0.85rem", width: "100%", fontSize: "0.88rem" }}
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery("")}
                    style={{ position: "absolute", right: "0.75rem", top: "50%", transform: "translateY(-50%)", background: "none", border: "none", color: "var(--muted)", cursor: "pointer", display: "inline-flex", alignItems: "center", justifyContent: "center" }}
                  >
                    <UntitledIcon name="x" size={14} />
                  </button>
                )}
              </div>

              {/* STORE FILTER PILLS */}
              {!searchQuery && (
                <div style={{ display: "flex", gap: "0.4rem", flexWrap: "wrap" }}>
                  <button
                    type="button"
                    onClick={() => setFilterTag("recommended")}
                    style={{
                      padding: "0.4rem 0.8rem",
                      borderRadius: "999px",
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      cursor: "pointer",
                      background: filterTag === "recommended" ? "var(--primary)" : "var(--bg)",
                      color: filterTag === "recommended" ? "#ffffff" : "var(--fg)",
                      border: "1px solid var(--border)",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.35rem",
                    }}
                  >
                    <UntitledIcon name="sparkles" size={13} />
                    <span>Recommended (Hot &amp; Near)</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterTag("hot")}
                    style={{
                      padding: "0.4rem 0.8rem",
                      borderRadius: "999px",
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      cursor: "pointer",
                      background: filterTag === "hot" ? "var(--primary)" : "var(--bg)",
                      color: filterTag === "hot" ? "#ffffff" : "var(--fg)",
                      border: "1px solid var(--border)",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.35rem",
                    }}
                  >
                    <UntitledIcon name="flame" size={13} />
                    <span>Hot Hubs</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterTag("near")}
                    style={{
                      padding: "0.4rem 0.8rem",
                      borderRadius: "999px",
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      cursor: "pointer",
                      background: filterTag === "near" ? "var(--primary)" : "var(--bg)",
                      color: filterTag === "near" ? "#ffffff" : "var(--fg)",
                      border: "1px solid var(--border)",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.35rem",
                    }}
                  >
                    <UntitledIcon name="map-pin" size={13} />
                    <span>Near You (&lt; 2km)</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setFilterTag("all")}
                    style={{
                      padding: "0.4rem 0.8rem",
                      borderRadius: "999px",
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      cursor: "pointer",
                      background: filterTag === "all" ? "var(--primary)" : "var(--bg)",
                      color: filterTag === "all" ? "#ffffff" : "var(--fg)",
                      border: "1px solid var(--border)",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.35rem",
                    }}
                  >
                    <UntitledIcon name="grid" size={13} />
                    <span>All Hubs</span>
                  </button>
                </div>
              )}
            </div>

            {/* RESULTS STATUS BAR */}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "0.8rem", color: "var(--muted)" }}>
              <span>
                {searchQuery ? (
                  <>Showing exact matches for "<strong>{searchQuery}</strong>" ({displayLots.length} hubs found)</>
                ) : filterTag === "recommended" ? (
                  <>Showing <strong>Hot Hubs &amp; Parking Lots Near You</strong> ({displayLots.length} hubs)</>
                ) : (
                  <>Showing {displayLots.length} parking hubs</>
                )}
              </span>
              <span style={{ fontSize: "0.75rem", color: "var(--primary)", fontWeight: 600 }}>
                ● Click any parking hub to view detailed pass packages
              </span>
            </div>
          </div>

          {/* STORE PRODUCT CATALOG (E-COMMERCE STORE GRID) */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: "1.25rem" }}>
            {displayLots.map(lot => {
              const enterprise = getLotEnterprise(lot, users)
              const distance = getLotDistance(lot)
              const isHot = isHotLot(lot)
              const isNear = isNearLot(lot)
              const availableSlots = lot.slots.filter(s => s.status === "available").length

              return (
                <div
                  key={lot.id}
                  className="card animate-in"
                  onClick={() => handleOpenLotDetail(lot)}
                  style={{
                    padding: 0,
                    overflow: "hidden",
                    cursor: "pointer",
                    display: "flex",
                    flexDirection: "column",
                    transition: "transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease",
                    border: "1.5px solid var(--border)",
                    borderRadius: "1rem",
                  }}
                  onMouseEnter={e => {
                    e.currentTarget.style.transform = "translateY(-4px)"
                    e.currentTarget.style.boxShadow = "0 14px 28px -6px rgba(0, 0, 0, 0.15)"
                    e.currentTarget.style.borderColor = "var(--primary)"
                  }}
                  onMouseLeave={e => {
                    e.currentTarget.style.transform = "translateY(0)"
                    e.currentTarget.style.boxShadow = "none"
                    e.currentTarget.style.borderColor = "var(--border)"
                  }}
                >
                  {/* CARD STOREFRONT HEADER */}
                  <div
                    style={{
                      padding: "1rem 1.15rem",
                      background: "linear-gradient(135deg, rgba(37, 99, 235, 0.08) 0%, rgba(15, 23, 42, 0.02) 100%)",
                      borderBottom: "1px solid var(--border)",
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "flex-start",
                      gap: "0.5rem",
                    }}
                  >
                    <div>
                      <span
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "0.35rem",
                          fontSize: "0.72rem",
                          fontWeight: 700,
                          textTransform: "uppercase",
                          letterSpacing: "0.04em",
                          color: "var(--primary)",
                          background: "var(--primary)15",
                          padding: "0.15rem 0.5rem",
                          borderRadius: "999px",
                          marginBottom: "0.35rem",
                        }}
                      >
                        <UntitledIcon name="building" size={12} />
                        <span>{enterprise}</span>
                      </span>
                      <h3 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.08rem", margin: 0, color: "var(--fg)" }}>
                        {lot.name}
                      </h3>
                    </div>

                    <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: "0.3rem", flexShrink: 0 }}>
                      {isHot && (
                        <span style={{ fontSize: "0.7rem", fontWeight: 700, color: "#ea580c", background: "rgba(234, 88, 12, 0.12)", border: "1px solid rgba(234, 88, 12, 0.35)", padding: "0.15rem 0.45rem", borderRadius: "999px", display: "inline-flex", alignItems: "center", gap: "0.25rem" }}>
                          <UntitledIcon name="flame" size={11} /> HOT
                        </span>
                      )}
                      {isNear && (
                        <span style={{ fontSize: "0.7rem", fontWeight: 600, color: "#2563eb", background: "rgba(37, 99, 235, 0.1)", border: "1px solid rgba(37, 99, 235, 0.25)", padding: "0.15rem 0.45rem", borderRadius: "999px", display: "inline-flex", alignItems: "center", gap: "0.25rem" }}>
                          <UntitledIcon name="map-pin" size={11} /> {distance} km
                        </span>
                      )}
                    </div>
                  </div>

                  {/* CARD BODY: Address, Features & Capacity */}
                  <div style={{ padding: "1.15rem", display: "flex", flexDirection: "column", gap: "0.85rem", flex: 1 }}>
                    <div style={{ fontSize: "0.82rem", color: "var(--muted)", display: "flex", alignItems: "center", gap: "0.4rem" }}>
                      <UntitledIcon name="map-pin" size={15} />
                      <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{lot.address}</span>
                    </div>

                    {/* PERKS LIST */}
                    <div style={{ display: "flex", flexDirection: "column", gap: "0.35rem", fontSize: "0.78rem", color: "var(--muted)", background: "var(--bg)", padding: "0.65rem 0.85rem", borderRadius: "0.5rem", border: "1px solid var(--border)" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.4rem", color: "var(--fg)" }}>
                        <UntitledIcon name="check" size={13} style={{ color: "#22c55e", strokeWidth: 2.2 }} /> 24/7 Unlimited in &amp; out privileges
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.4rem", color: "var(--fg)" }}>
                        <UntitledIcon name="check" size={13} style={{ color: "#22c55e", strokeWidth: 2.2 }} /> License plate contactless barrier lift
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: "0.4rem", color: "var(--fg)" }}>
                        <UntitledIcon name="check" size={13} style={{ color: "#22c55e", strokeWidth: 2.2 }} /> {availableSlots} available / {lot.totalSlots} total slots
                      </div>
                    </div>

                    {/* PRICING STORE BANNER */}
                    <div style={{ marginTop: "auto", paddingTop: "0.6rem", borderTop: "1px dashed var(--border)", display: "flex", justifyContent: "space-between", alignItems: "flex-end" }}>
                      <div>
                        <span style={{ fontSize: "0.72rem", color: "var(--muted)", display: "block" }}>Starting from</span>
                        <div style={{ display: "flex", alignItems: "baseline", gap: "0.25rem" }}>
                          <strong style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.25rem", color: "var(--primary)" }}>
                            {lot.subscriptionMonthly.toLocaleString("vi-VN")}₫
                          </strong>
                          <span style={{ fontSize: "0.75rem", color: "var(--muted)" }}>/ mo</span>
                        </div>
                      </div>

                      <button
                        type="button"
                        className="btn-primary"
                        style={{ padding: "0.45rem 0.85rem", fontSize: "0.8rem", fontWeight: 600, display: "inline-flex", alignItems: "center", gap: "0.35rem" }}
                      >
                        <span>View Packages</span>
                        <UntitledIcon name="arrow-right" size={13} />
                      </button>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>

          {displayLots.length === 0 && (
            <div className="card" style={{ padding: "3rem 1.5rem", textAlign: "center", color: "var(--muted)" }}>
              <div style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--primary)15", color: "var(--primary)", display: "inline-flex", alignItems: "center", justifyContent: "center", margin: "0 auto 0.75rem" }}>
                <UntitledIcon name="search" size={26} />
              </div>
              <h3 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.2rem", color: "var(--fg)", margin: "0 0 0.5rem" }}>
                No parking hubs matched your search
              </h3>
              <p style={{ margin: "0 0 1rem", fontSize: "0.85rem" }}>
                Try searching with a broader enterprise name (e.g. "Vingroup", "Bitexco") or clear the search field to see hot hubs.
              </p>
              <button type="button" className="btn-outline" onClick={() => { setSearchQuery(""); setFilterTag("recommended") }}>
                Reset Search
              </button>
            </div>
          )}
        </div>
      )}

      {/* TAB 2: MY PASSES */}
      {tabMode === "my-passes" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {passes.length > 0 ? (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1rem" }}>
              {passes.map(pass => {
                const isExpired = new Date(pass.expiresAt) <= new Date()
                return (
                  <div key={pass.id} className="card" style={{ padding: "1.25rem", border: isExpired ? "1px solid var(--border)" : "1.5px solid rgba(34, 197, 94, 0.4)", borderRadius: "0.85rem" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.75rem" }}>
                      <div>
                        <span style={{ fontSize: "0.72rem", fontWeight: 700, textTransform: "uppercase", color: "var(--primary)", background: "var(--primary)15", padding: "0.15rem 0.5rem", borderRadius: "999px" }}>
                          {pass.plan === "monthly" ? "Monthly Pass" : "Annual Pass"}
                        </span>
                        <h4 style={{ margin: "0.4rem 0 0", fontFamily: "Outfit", fontWeight: 700, fontSize: "1.1rem", color: "var(--fg)" }}>
                          {pass.lotName}
                        </h4>
                      </div>
                      <span style={{ fontSize: "0.75rem", fontWeight: 700, padding: "0.2rem 0.6rem", borderRadius: "999px", background: isExpired ? "rgba(100, 116, 139, 0.15)" : "rgba(34, 197, 94, 0.15)", color: isExpired ? "var(--muted)" : "#22c55e", border: `1px solid ${isExpired ? "var(--border)" : "rgba(34, 197, 94, 0.3)"}` }}>
                        {isExpired ? "Expired" : "● Active"}
                      </span>
                    </div>

                    <div style={{ fontSize: "0.82rem", color: "var(--muted)", display: "flex", flexDirection: "column", gap: "0.35rem", marginBottom: "1rem" }}>
                      <div>Purchased: <strong style={{ color: "var(--fg)" }}>{new Date(pass.purchasedAt).toLocaleDateString("en-GB")}</strong></div>
                      <div>Expires: <strong style={{ color: isExpired ? "#ef4444" : "#22c55e" }}>{new Date(pass.expiresAt).toLocaleDateString("en-GB")}</strong></div>
                      <div>Amount: <strong style={{ color: "var(--fg)" }}>{pass.amount.toLocaleString("vi-VN")}₫</strong></div>
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", background: "var(--bg)", padding: "0.6rem 0.85rem", borderRadius: "0.5rem", border: "1px solid var(--border)", fontSize: "0.78rem" }}>
                      <span>Digital Pass ID: <code style={{ color: "var(--primary)", fontWeight: 700 }}>{pass.id.slice(0, 10)}</code></span>
                      <span style={{ color: "#22c55e", fontWeight: 600 }}>Automated Gate Ready</span>
                    </div>
                  </div>
                )
              })}
            </div>
          ) : (
            <div className="card" style={{ padding: "3rem 1.5rem", textAlign: "center", color: "var(--muted)" }}>
              <div style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--primary)15", color: "var(--primary)", display: "inline-flex", alignItems: "center", justifyContent: "center", margin: "0 auto 0.75rem" }}>
                <UntitledIcon name="ticket" size={26} />
              </div>
              <h3 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.2rem", color: "var(--fg)", margin: "0 0 0.5rem" }}>
                You have no active parking passes
              </h3>
              <p style={{ margin: "0 0 1rem", fontSize: "0.85rem" }}>
                Browse our Pass Marketplace to subscribe to monthly or annual packages for your regular parking destinations.
              </p>
              <button type="button" className="btn-primary" onClick={() => setTabMode("store")}>
                Browse Pass Marketplace
              </button>
            </div>
          )}
        </div>
      )}

      {/* TAB 3: ORDER HISTORY */}
      {tabMode === "orders" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {orders.length > 0 ? (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.65rem" }}>
              {orders.map(order => (
                <div key={order.id} className="card" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "1rem", flexWrap: "wrap", padding: "1rem 1.25rem" }}>
                  <div>
                    <div style={{ fontSize: "0.75rem", color: "var(--muted)", marginBottom: "0.2rem" }}>
                      Order ID: {order.id} • {new Date(order.createdAt).toLocaleDateString("en-GB")}
                    </div>
                    <strong style={{ fontSize: "0.95rem", color: "var(--fg)", fontFamily: "Outfit" }}>
                      {order.plan === "monthly" ? "Monthly Pass" : "Annual Pass"} — {order.lotName}
                    </strong>
                    <div style={{ fontSize: "0.78rem", color: "var(--muted)", marginTop: "0.25rem", display: "flex", alignItems: "center", gap: "0.4rem" }}>
                      <span>Payment via:</span>
                      <PaymentMethodLogo method={order.paymentMethod} size="xs" showName />
                    </div>
                  </div>

                  <div style={{ textAlign: "right" }}>
                    <strong style={{ display: "block", fontSize: "1.1rem", fontFamily: "Outfit", color: "var(--primary)" }}>
                      {order.amount.toLocaleString("vi-VN")}₫
                    </strong>
                    <span style={{ fontSize: "0.75rem", fontWeight: 700, padding: "0.15rem 0.5rem", borderRadius: "999px", background: order.paymentStatus === "paid" ? "rgba(34, 197, 94, 0.15)" : "rgba(234, 88, 12, 0.12)", color: order.paymentStatus === "paid" ? "#22c55e" : "#ea580c", border: `1px solid ${order.paymentStatus === "paid" ? "rgba(34, 197, 94, 0.3)" : "rgba(234, 88, 12, 0.35)"}` }}>
                      {order.paymentStatus === "paid" ? "Paid" : "Payment Pending"}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="card" style={{ padding: "3rem 1.5rem", textAlign: "center", color: "var(--muted)" }}>
              <div style={{ width: 56, height: 56, borderRadius: "50%", background: "var(--primary)15", color: "var(--primary)", display: "inline-flex", alignItems: "center", justifyContent: "center", margin: "0 auto 0.75rem" }}>
                <UntitledIcon name="clipboard" size={26} />
              </div>
              <h3 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.2rem", color: "var(--fg)", margin: "0 0 0.5rem" }}>
                No pass purchase orders yet
              </h3>
            </div>
          )}
        </div>
      )}

      {/* DETAIL MODAL: BẤM VÔ BÃI ĐỖ CỦA DOANH NGHIỆP/BÃI ĐỖ MỚI RA GÓI CHI TIẾT */}
      {selectedLotForDetail && typeof document !== "undefined" && createPortal(
        <div
          role="dialog"
          aria-modal="true"
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(2, 6, 23, 0.85)",
            backdropFilter: "blur(6px)",
            zIndex: 99999,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: "1rem",
          }}
          onClick={() => setSelectedLotForDetail(null)}
        >
          <div
            className="card animate-in"
            style={{
              width: "100%",
              maxWidth: 820,
              maxHeight: "92vh",
              overflowY: "auto",
              background: "var(--card)",
              borderRadius: "1rem",
              border: "1px solid var(--border)",
              padding: "1.75rem",
              boxShadow: "0 25px 60px -15px rgba(0, 0, 0, 0.7)",
            }}
            onClick={e => e.stopPropagation()}
          >
            {/* MODAL HEADER */}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "1.25rem", gap: "1rem" }}>
              <div>
                <span style={{ fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", color: "var(--primary)", background: "var(--primary)15", padding: "0.15rem 0.55rem", borderRadius: "999px", display: "inline-flex", alignItems: "center", gap: "0.35rem" }}>
                  <UntitledIcon name="building" size={12} />
                  <span>{getLotEnterprise(selectedLotForDetail, users)}</span>
                </span>
                <h3 style={{ margin: "0.4rem 0 0.2rem", fontFamily: "Outfit", fontWeight: 800, fontSize: "1.4rem", color: "var(--fg)" }}>
                  {selectedLotForDetail.name} — Pass Packages
                </h3>
                <p style={{ margin: 0, fontSize: "0.85rem", color: "var(--muted)" }}>
                  {selectedLotForDetail.address} • Type: <strong style={{ textTransform: "capitalize", color: "var(--fg)" }}>{selectedLotForDetail.type}</strong>
                </p>
              </div>

              <button
                type="button"
                className="btn-outline"
                onClick={() => setSelectedLotForDetail(null)}
                style={{ padding: "0.35rem 0.75rem", fontSize: "0.85rem", display: "inline-flex", alignItems: "center", gap: "0.35rem" }}
              >
                <UntitledIcon name="x" size={14} />
                <span>Close</span>
              </button>
            </div>

            {/* IF CHECKOUT SUCCESS */}
            {checkoutSuccess ? (
              <div style={{ padding: "2rem", textAlign: "center", background: "rgba(34, 197, 94, 0.08)", border: "1.5px solid rgba(34, 197, 94, 0.35)", borderRadius: "0.75rem", margin: "1rem 0" }}>
                <div style={{ width: 56, height: 56, borderRadius: "50%", background: "rgba(34, 197, 94, 0.15)", color: "#22c55e", display: "inline-flex", alignItems: "center", justifyContent: "center", margin: "0 auto 0.75rem" }}>
                  <UntitledIcon name="award" size={30} />
                </div>
                <h4 style={{ fontFamily: "Outfit", fontWeight: 700, fontSize: "1.3rem", color: "#22c55e", margin: "0 0 0.5rem" }}>
                  Pass Activated Successfully!
                </h4>
                <p style={{ fontSize: "0.9rem", color: "var(--fg)", marginBottom: "1.5rem" }}>
                  {checkoutSuccess}
                </p>
                <div style={{ display: "flex", gap: "0.75rem", justifyContent: "center" }}>
                  <button
                    type="button"
                    className="btn-primary"
                    onClick={() => {
                      setSelectedLotForDetail(null)
                      setTabMode("my-passes")
                    }}
                    style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem" }}
                  >
                    <span>View in My Passes</span>
                    <UntitledIcon name="arrow-right" size={14} />
                  </button>
                  <button
                    type="button"
                    className="btn-outline"
                    onClick={() => setSelectedLotForDetail(null)}
                  >
                    Done
                  </button>
                </div>
              </div>
            ) : (
              <>
                {/* PACKAGE SELECTION TIERS (GÓI CHI TIẾT) */}
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "1rem", marginBottom: "1.5rem" }}>
                  {/* MONTHLY PASS */}
                  <div
                    onClick={() => setSelectedPlan("monthly")}
                    style={{
                      padding: "1.25rem",
                      borderRadius: "0.85rem",
                      cursor: "pointer",
                      border: selectedPlan === "monthly" ? "2px solid var(--primary)" : "1.5px solid var(--border)",
                      background: selectedPlan === "monthly" ? "var(--primary)08" : "var(--bg)",
                      transition: "all 0.15s ease",
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                      <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--primary)", textTransform: "uppercase" }}>
                        Flexible 30-Day
                      </span>
                      <input type="radio" checked={selectedPlan === "monthly"} onChange={() => setSelectedPlan("monthly")} />
                    </div>
                    <h4 style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.2rem", margin: "0 0 0.35rem", color: "var(--fg)" }}>
                      Monthly Pass
                    </h4>
                    <div style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.35rem", color: "var(--primary)", marginBottom: "0.75rem" }}>
                      {selectedLotForDetail.subscriptionMonthly.toLocaleString("vi-VN")}₫
                      <span style={{ fontSize: "0.75rem", color: "var(--muted)", fontWeight: 500 }}> / 30 days</span>
                    </div>
                    <ul style={{ paddingLeft: "1.2rem", margin: 0, fontSize: "0.8rem", color: "var(--muted)", display: "flex", flexDirection: "column", gap: "0.35rem" }}>
                      <li>30 days unlimited parking</li>
                      <li>Contactless license plate gate open</li>
                      <li>In &amp; out multiple times daily</li>
                      <li>Renew or cancel anytime</li>
                    </ul>
                  </div>

                  {/* ANNUAL PASS */}
                  <div
                    onClick={() => setSelectedPlan("annual")}
                    style={{
                      padding: "1.25rem",
                      borderRadius: "0.85rem",
                      cursor: "pointer",
                      border: selectedPlan === "annual" ? "2px solid #22c55e" : "1.5px solid var(--border)",
                      background: selectedPlan === "annual" ? "rgba(34, 197, 94, 0.08)" : "var(--bg)",
                      transition: "all 0.15s ease",
                      position: "relative",
                    }}
                  >
                    <span style={{ position: "absolute", top: "-10px", right: "12px", background: "#22c55e", color: "#ffffff", fontSize: "0.68rem", fontWeight: 800, padding: "0.15rem 0.5rem", borderRadius: "999px", display: "inline-flex", alignItems: "center", gap: "0.25rem" }}>
                      <UntitledIcon name="tag" size={11} />
                      <span>SAVE 2 MONTHS</span>
                    </span>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                      <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "#22c55e", textTransform: "uppercase" }}>
                        VIP 365-Day Plan
                      </span>
                      <input type="radio" checked={selectedPlan === "annual"} onChange={() => setSelectedPlan("annual")} />
                    </div>
                    <h4 style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.2rem", margin: "0 0 0.35rem", color: "var(--fg)" }}>
                      Annual Pass
                    </h4>
                    <div style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.35rem", color: "#22c55e", marginBottom: "0.75rem" }}>
                      {selectedLotForDetail.subscriptionYearly.toLocaleString("vi-VN")}₫
                      <span style={{ fontSize: "0.75rem", color: "var(--muted)", fontWeight: 500 }}> / 365 days</span>
                    </div>
                    <ul style={{ paddingLeft: "1.2rem", margin: 0, fontSize: "0.8rem", color: "var(--muted)", display: "flex", flexDirection: "column", gap: "0.35rem" }}>
                      <li>365 days VIP parking access</li>
                      <li>Guaranteed reserved parking space</li>
                      <li>Save 2 full months (~17% discount)</li>
                      <li>Priority hotline concierge support</li>
                    </ul>
                  </div>
                </div>

                {/* CHECKOUT CONFIG: VEHICLE PLATE & PAYMENT METHOD */}
                <div style={{ borderTop: "1px solid var(--border)", paddingTop: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
                  {/* VEHICLE BINDING */}
                  <div>
                    <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 700, color: "var(--fg)", marginBottom: "0.4rem" }}>
                      Select Registered Vehicle Plate:
                    </label>
                    {vehicles.length > 0 ? (
                      <select
                        className="input"
                        value={selectedVehiclePlate}
                        onChange={e => setSelectedVehiclePlate(e.target.value)}
                        style={{ maxWidth: 360, fontSize: "0.85rem" }}
                      >
                        {vehicles.map(v => (
                          <option key={v.id} value={v.licensePlate}>
                            {v.licensePlate} ({v.type === "car" ? "Car" : "Motorcycle"})
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        className="input"
                        value={selectedVehiclePlate}
                        onChange={e => setSelectedVehiclePlate(e.target.value.toUpperCase())}
                        placeholder="e.g. 51A-12345"
                        style={{ maxWidth: 360, fontSize: "0.85rem" }}
                      />
                    )}
                  </div>

                  {/* PAYMENT METHOD SELECTION */}
                  <div>
                    <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 700, color: "var(--fg)", marginBottom: "0.5rem" }}>
                      Choose Payment Method:
                    </label>
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(110px, 1fr))", gap: "0.5rem" }}>
                      {PAYMENT_OPTIONS.map(opt => (
                        <button
                          key={opt.id}
                          type="button"
                          onClick={() => setSelectedMethod(opt.id)}
                          style={{
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            gap: "0.45rem",
                            padding: "0.75rem 0.4rem",
                            borderRadius: "0.6rem",
                            border: selectedMethod === opt.id ? "2px solid var(--primary)" : "1px solid var(--border)",
                            background: selectedMethod === opt.id ? "color-mix(in srgb, var(--primary) 10%, var(--card))" : "var(--card)",
                            cursor: "pointer",
                            transition: "all 0.15s ease",
                          }}
                        >
                          <PaymentMethodLogo method={opt.id} size="md" />
                          <span style={{ fontSize: "0.72rem", fontWeight: 600, color: "var(--fg)", textAlign: "center" }}>{opt.name}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* SUMMARY & SUBMIT */}
                  <div style={{ background: "var(--bg)", padding: "1rem 1.25rem", borderRadius: "0.75rem", border: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem", marginTop: "0.5rem" }}>
                    <div>
                      <span style={{ fontSize: "0.78rem", color: "var(--muted)", display: "block" }}>
                        Total Amount Due ({selectedPlan === "monthly" ? "30 Days" : "365 Days"}):
                      </span>
                      <strong style={{ fontFamily: "Outfit", fontWeight: 800, fontSize: "1.4rem", color: "var(--primary)" }}>
                        {(selectedPlan === "monthly" ? selectedLotForDetail.subscriptionMonthly : selectedLotForDetail.subscriptionYearly).toLocaleString("vi-VN")}₫
                      </strong>
                    </div>

                    <div style={{ display: "flex", gap: "0.6rem" }}>
                      <button
                        type="button"
                        className="btn-outline"
                        onClick={() => setSelectedLotForDetail(null)}
                      >
                        Cancel
                      </button>
                      <button
                        type="button"
                        className="btn-primary"
                        onClick={handlePurchasePass}
                        style={{ padding: "0.55rem 1.25rem", fontSize: "0.85rem", fontWeight: 700, display: "inline-flex", alignItems: "center", gap: "0.4rem" }}
                      >
                        <span>Complete &amp; Activate Pass</span>
                        <UntitledIcon name="arrow-right" size={14} />
                      </button>
                    </div>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>,
        document.body
      )}
    </div>
  )
}
