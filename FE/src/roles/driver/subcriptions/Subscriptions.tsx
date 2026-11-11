import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useState, useMemo, useEffect } from "react"
import "../../../booking-support.css"

import { Modal } from "../../../components/ui/Modal"

import { useApp } from "../../../context/AppContext"

import type {
  ParkingLot,
  PackageOrder,
  PaymentMethod,
  User,
} from "../../../lib/types"

import { driverData as store } from "../data/data"
import { showUiToast } from "../../../lib/uiToast"

import { PAYMENT_OPTIONS } from "../data/paymentOptions"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"

import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { formatVietnamesePlate, vehiclesApi, type RegisteredVehicle } from "../../../lib/vehiclesApi"
import { distanceKm, validCoordinates, type LatLng } from "../../../lib/parkingCoordinates"

function getLotEnterprise(lot: ParkingLot, users: User[]): string {
  const owner = users.find((u) => u.id === lot.ownerId)

  if (owner?.name && !owner.name.includes("demo")) return owner.name

  return "Parking operator"
}

type TabMode = "store" | "my-passes" | "orders"
const passesPerPage = 12
const nearbyRadiusKm = 3

function hasPassPrice(price: unknown): price is number {
  return typeof price === "number" && Number.isFinite(price) && price >= 0
}

export function Subscriptions() {
  const { user } = useApp()

  const [tabMode, setTabMode] = useState<TabMode>("store")
  const [passPage, setPassPage] = useState(1)
  const [storePage, setStorePage] = useState(1)

  const [searchQuery, setSearchQuery] = useState("")

  const [filterTag, setFilterTag] =
    useState<"all" | "near">("all")
  const [location, setLocation] = useState<LatLng | null>(null)
  const [locationError, setLocationError] = useState("")
  const [locationRetry, setLocationRetry] = useState(0)

  const [selectedLotForDetail, setSelectedLotForDetail] =
    useState<ParkingLot | null>(null)

  const [selectedPlan, setSelectedPlan] = useState<"monthly" | "annual">(
    "monthly",
  )

  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod>("momo")

  const [selectedVehicleId, setSelectedVehicleId] = useState("")
  const [vehicleRetry, setVehicleRetry] = useState(0)
  const [vehicleResult, setVehicleResult] = useState<{ scope: string; items: RegisteredVehicle[]; error?: string } | null>(null)

  const [checkoutSuccess, setCheckoutSuccess] = useState<string | null>(null)

  const users = useMemo(() => store.getUsers?.() ?? [], [])

  const lots = useMemo(
    () => store.getLots().filter((lot) => lot.status === "active"),
    [],
  )

  const passes = useMemo(
    () => (user ? store.getPassesByDriver(user.id) : []),
    [user, checkoutSuccess],
  )
  const passPageCount = Math.max(1, Math.ceil(passes.length / passesPerPage))
  const currentPassPage = Math.min(passPage, passPageCount)
  const visiblePasses = passes.slice((currentPassPage - 1) * passesPerPage, currentPassPage * passesPerPage)

  const orders = useMemo(
    () => (user ? store.getPackageOrdersByDriver(user.id) : []),
    [user, checkoutSuccess],
  )

  const vehicleScope = user && selectedLotForDetail ? JSON.stringify([user.id, selectedLotForDetail.id, vehicleRetry]) : ""
  const currentVehicles = vehicleResult?.scope === vehicleScope ? vehicleResult : null
  const vehicles = currentVehicles?.items ?? []
  const selectedVehicle = vehicles.find(vehicle => vehicle.id === selectedVehicleId) ?? vehicles[0]

  useEffect(() => {
    if (!vehicleScope) return
    let active = true
    vehiclesApi.list()
      .then((items) => {
        if (active) setVehicleResult({ scope: vehicleScope, items })
      })
      .catch(() => {
        if (active) setVehicleResult({ scope: vehicleScope, items: [], error: "Không tải được danh sách xe đã đăng ký." })
      })
    return () => { active = false }
  }, [vehicleScope])

  useEffect(() => {
    if (tabMode !== "store" || filterTag !== "near") return
    let active = true
    setLocation(null)
    setLocationError("")
    if (!navigator.geolocation) {
      setLocationError("Location is unavailable in this browser.")
      return
    }
    navigator.geolocation.getCurrentPosition(({ coords }) => {
      if (!active) return
      const point = { lat: coords.latitude, lng: coords.longitude }
      if (validCoordinates(point)) setLocation(point)
      else setLocationError("Could not determine your location.")
    }, error => {
      if (!active) return
      setLocationError(error.code === 1 ? "Allow location access in your browser to find nearby parking." : "Could not determine your location. Try again.")
    }, { enableHighAccuracy: true, timeout: 12_000, maximumAge: 30_000 })
    return () => { active = false }
  }, [tabMode, filterTag, locationRetry, user?.id])

  const displayLots = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()

    const matches = q ? lots.filter(lot => [lot.name, lot.address, getLotEnterprise(lot, users)].some(value => value.toLowerCase().includes(q))) : lots
    if (filterTag !== "near") return matches
    if (!location) return []
    return matches.filter(validCoordinates)
      .map(lot => ({ lot, distance: distanceKm(location, lot) }))
      .filter(item => item.distance <= nearbyRadiusKm)
      .sort((a, b) => a.distance - b.distance)
      .map(item => item.lot)
  }, [lots, searchQuery, filterTag, users, location])
  const storePageCount = Math.max(1, Math.ceil(displayLots.length / passesPerPage))
  const currentStorePage = Math.min(storePage, storePageCount)
  const visibleDisplayLots = displayLots.slice((currentStorePage - 1) * passesPerPage, currentStorePage * passesPerPage)

  useEffect(() => setStorePage(1), [searchQuery, filterTag])

  function handleOpenLotDetail(lot: ParkingLot) {
    setVehicleResult(null)
    setSelectedVehicleId("")
    setSelectedLotForDetail(lot)

    setSelectedPlan(hasPassPrice(lot.subscriptionYearly) && !hasPassPrice(lot.subscriptionMonthly) ? "annual" : "monthly")

    setSelectedMethod("momo")

    setCheckoutSuccess(null)
  }

  const selectedPassPrice = selectedPlan === "monthly"
    ? selectedLotForDetail?.subscriptionMonthly
    : selectedLotForDetail?.subscriptionYearly

  function handlePurchasePass() {
    if (!user || !selectedLotForDetail || !selectedVehicle || !hasPassPrice(selectedPassPrice)) return

    const amount = selectedPassPrice
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
      vehiclePlate: formatVietnamesePlate(selectedVehicle),
    })

    setCheckoutSuccess(
      `Gói gửi xe tại ${selectedLotForDetail.name} đã được kích hoạt. Có hiệu lực đến ${expires.toLocaleDateString("vi-VN")}.`,
    )
    showUiToast("Pass activated", `Your pass for ${selectedLotForDetail.name} is ready.`)
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <section className="driver-pass-header">
        <div className="relative z-10 min-w-0">
          <PageHeader title="Parking Passes" description="Choose a parking location to compare monthly and annual plans." />
        </div>
        <span aria-hidden="true" className="driver-pass-header-mark"><UntitledIcon name="ticket" size={72} /></span>
      </section>

      {/* TOP NAVIGATION TABS */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          borderBottom: "1px solid var(--border)",
          paddingBottom: "0.75rem",
        }}
      >
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className={tabMode === "store" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("store")}
            style={{
              padding: "0.45rem 1rem",
              fontSize: "0.85rem",
              fontWeight: 600,
              display: "inline-flex",
              alignItems: "center",
              gap: "0.45rem",
            }}
          >
            <UntitledIcon name="shopping-bag" size={15} />
            <span>Pass Marketplace</span>
          </button>
          <button
            type="button"
            className={tabMode === "my-passes" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("my-passes")}
            style={{
              padding: "0.45rem 1rem",
              fontSize: "0.85rem",
              fontWeight: 600,
              display: "inline-flex",
              alignItems: "center",
              gap: "0.45rem",
            }}
          >
            <UntitledIcon name="ticket" size={15} />
            <span>My Passes ({passes.length})</span>
          </button>
          <button
            type="button"
            className={tabMode === "orders" ? "btn-primary" : "btn-outline"}
            onClick={() => setTabMode("orders")}
            style={{
              padding: "0.45rem 1rem",
              fontSize: "0.85rem",
              fontWeight: 600,
              display: "inline-flex",
              alignItems: "center",
              gap: "0.45rem",
            }}
          >
            <UntitledIcon name="clipboard" size={15} />
            <span>Order History ({orders.length})</span>
          </button>
        </div>
      </div>

      {/* TAB 1: PASS STORE */}
      {tabMode === "store" && (
        <div
          style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}
        >
          {/* SEARCH & FILTER BAR */}
          <div
            className="card"
            style={{
              padding: "1rem 1.25rem",
              display: "flex",
              flexDirection: "column",
              gap: "0.85rem",
            }}
          >
            <div
              style={{
                display: "flex",
                gap: "0.75rem",
                alignItems: "center",
                flexWrap: "wrap",
              }}
            >
              <div style={{ position: "relative", flex: "1 1 320px" }}>
                <span
                  style={{
                    position: "absolute",
                    left: "0.85rem",
                    top: "50%",
                    transform: "translateY(-50%)",
                    color: "var(--muted)",
                    pointerEvents: "none",
                  }}
                >
                  <UntitledIcon name="search" size={17} />
                </span>
                <input
                  className="input sp-search-input"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search by enterprise or parking lot name…"
                  style={{
                    paddingLeft: "2.4rem",
                    paddingRight: searchQuery ? "2.4rem" : "0.85rem",
                    width: "100%",
                    fontSize: "0.88rem",
                  }}
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery("")}
                    style={{
                      position: "absolute",
                      right: "0.75rem",
                      top: "50%",
                      transform: "translateY(-50%)",
                      background: "none",
                      border: "none",
                      color: "var(--muted)",
                      cursor: "pointer",
                      display: "inline-flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    <UntitledIcon name="x" size={14} />
                  </button>
                )}
              </div>

            </div>
              <div role="group" aria-label="Filter parking locations" className="driver-pass-filters">
                {([
                  { id: "all", label: "All Hubs", icon: "grid" },
                  { id: "near", label: "Near You", icon: "map-pin" },
                ] as const).map(filter => (
                  <button key={filter.id} type="button" aria-pressed={filterTag === filter.id}
                    onClick={() => setFilterTag(filter.id)}>
                    <UntitledIcon name={filter.icon} size={15} />
                    {filter.label}
                  </button>
                ))}
              </div>
            {filterTag === "near" && !location ? (
              <div className="flex flex-wrap items-center gap-3 text-sm text-[var(--muted)]">
                <p role="status">{locationError || "Finding your location…"}</p>
                {locationError && <button type="button" className="btn-outline" onClick={() => setLocationRetry(value => value + 1)}>Try again</button>}
              </div>
            ) : <p role="status" className="text-sm text-[var(--muted)]">
              {displayLots.length} {displayLots.length === 1 ? "parking location" : "parking locations"}{filterTag === "near" ? " within 3 km, nearest first" : ""}
            </p>}
          </div>

          <div className="driver-passes-grid">
            {visibleDisplayLots.map((lot) => (
              <button key={lot.id} type="button" className="driver-pass-location driver-pass-reveal"
                aria-label={"View pass packages for " + lot.name} aria-haspopup="dialog"
                onClick={() => handleOpenLotDetail(lot)}>
                <span aria-hidden="true" className="driver-pass-location-band">
                  <span className="driver-pass-location-icon"><UntitledIcon name="parking" size={23} /></span>
                  <span className="driver-pass-location-arrow"><UntitledIcon name="arrow-right" size={18} /></span>
                </span>
                <span className="driver-pass-location-copy">
                  <strong>{lot.name}</strong>
                  <span><UntitledIcon name="map-pin" size={14} aria-hidden="true" />{lot.address}</span>
                </span>
              </button>
            ))}
          </div>

          {storePageCount > 1 && <nav className="driver-passes-pagination" aria-label="Pass marketplace pages">
            <span>Showing {(currentStorePage - 1) * passesPerPage + 1}–{Math.min(currentStorePage * passesPerPage, displayLots.length)} of {displayLots.length} parking hubs</span>
            <div>
              <button type="button" className="btn-outline" disabled={currentStorePage === 1} onClick={() => setStorePage(Math.max(1, currentStorePage - 1))}>Previous</button>
              <span aria-current="page">Page {currentStorePage} of {storePageCount}</span>
              <button type="button" className="btn-outline" disabled={currentStorePage === storePageCount} onClick={() => setStorePage(Math.min(storePageCount, currentStorePage + 1))}>Next</button>
            </div>
          </nav>}

          {displayLots.length === 0 && (filterTag !== "near" || location) && (
            <div
              className="card"
              style={{
                padding: "3rem 1.5rem",
                textAlign: "center",
                color: "var(--muted)",
              }}
            >
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: "50%",
                  background: "var(--primary)15",
                  color: "var(--primary)",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  margin: "0 auto 0.75rem",
                }}
              >
                <UntitledIcon name="search" size={26} />
              </div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "1.2rem",
                  color: "var(--fg)",
                  margin: "0 0 0.5rem",
                }}
              >
                {filterTag === "near" ? "No matching parking locations within 3 km" : "No parking locations match your filters"}
              </h3>
              <p style={{ margin: "0 0 1rem", fontSize: "0.85rem" }}>
                Try another search or browse all parking locations.
              </p>
              <button
                type="button"
                className="btn-outline"
                onClick={() => {
                  setSearchQuery("")
                  setFilterTag("all")
                }}
              >
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
            <>
            <div className="driver-passes-grid">
              {visiblePasses.map((pass) => {
                const isExpired = new Date(pass.expiresAt) <= new Date()

                return (
                  <div
                    key={pass.id}
                    className="card driver-pass-reveal"
                    style={{
                      padding: "1.25rem",
                      border: isExpired
                        ? "1px solid var(--border)"
                        : "1.5px solid rgba(34, 197, 94, 0.4)",
                      borderRadius: "0.85rem",
                    }}
                  >
                    <div
                      style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "flex-start",
                        marginBottom: "0.75rem",
                      }}
                    >
                      <div>
                        <span
                          style={{
                            fontSize: "0.72rem",
                            fontWeight: 700,
                            textTransform: "uppercase",
                            color: "var(--primary)",
                            background: "var(--primary)15",
                            padding: "0.15rem 0.5rem",
                            borderRadius: "999px",
                          }}
                        >
                          {pass.plan === "monthly"
                            ? "Monthly Pass"
                            : "Annual Pass"}
                        </span>
                        <h4
                          style={{
                            margin: "0.4rem 0 0",
                            fontFamily: "Be Vietnam Pro",
                            fontWeight: 700,
                            fontSize: "1.1rem",
                            color: "var(--fg)",
                          }}
                        >
                          {pass.lotName}
                        </h4>
                        {pass.vehiclePlate && <p className="mt-1 text-sm text-[var(--muted)]">{pass.vehiclePlate}</p>}
                      </div>
                      <span
                        style={{
                          fontSize: "0.75rem",
                          fontWeight: 700,
                          padding: "0.2rem 0.6rem",
                          borderRadius: "999px",
                          background: isExpired
                            ? "rgba(100, 116, 139, 0.15)"
                            : "rgba(34, 197, 94, 0.15)",
                          color: isExpired ? "var(--muted)" : "#22c55e",
                          border: `1px solid ${
                            isExpired
                              ? "var(--border)"
                              : "rgba(34, 197, 94, 0.3)"
                          }`,
                        }}
                      >
                        {isExpired ? "Expired" : "● Active"}
                      </span>
                    </div>

                    <div
                      style={{
                        fontSize: "0.82rem",
                        color: "var(--muted)",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.35rem",
                        marginBottom: "1rem",
                      }}
                    >
                      <div>
                        Purchased:{" "}
                        <strong style={{ color: "var(--fg)" }}>
                          {new Date(pass.purchasedAt).toLocaleDateString(
                            "en-GB",
                          )}
                        </strong>
                      </div>
                      <div>
                        Expires:{" "}
                        <strong
                          style={{ color: isExpired ? "#ef4444" : "#22c55e" }}
                        >
                          {new Date(pass.expiresAt).toLocaleDateString("en-GB")}
                        </strong>
                      </div>
                      <div>
                        Amount:{" "}
                        <strong style={{ color: "var(--fg)" }}>
                          {pass.amount.toLocaleString("vi-VN")} VND
                        </strong>
                      </div>
                    </div>

                    <div
                      style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        background: "var(--bg)",
                        padding: "0.6rem 0.85rem",
                        borderRadius: "0.5rem",
                        border: "1px solid var(--border)",
                        fontSize: "0.78rem",
                      }}
                    >
                      <span>
                        Digital Pass ID:{" "}
                        <code
                          style={{ color: "var(--primary)", fontWeight: 700 }}
                        >
                          {pass.id.slice(0, 10)}
                        </code>
                      </span>
                      <span style={{ color: "#22c55e", fontWeight: 600 }}>
                        Automated Gate Ready
                      </span>
                    </div>
                  </div>
                )
              })}
            </div>
            {passPageCount > 1 && <nav className="driver-passes-pagination" aria-label="Pass pages">
              <span>Showing {(currentPassPage - 1) * passesPerPage + 1}–{Math.min(currentPassPage * passesPerPage, passes.length)} of {passes.length} passes</span>
              <div>
                <button type="button" className="btn-outline" disabled={currentPassPage === 1} onClick={() => setPassPage(Math.max(1, currentPassPage - 1))}>Previous</button>
                <span aria-current="page">Page {currentPassPage} of {passPageCount}</span>
                <button type="button" className="btn-outline" disabled={currentPassPage === passPageCount} onClick={() => setPassPage(Math.min(passPageCount, currentPassPage + 1))}>Next</button>
              </div>
            </nav>}
            </>
          ) : (
            <div
              className="card"
              style={{
                padding: "3rem 1.5rem",
                textAlign: "center",
                color: "var(--muted)",
              }}
            >
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: "50%",
                  background: "var(--primary)15",
                  color: "var(--primary)",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  margin: "0 auto 0.75rem",
                }}
              >
                <UntitledIcon name="ticket" size={26} />
              </div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "1.2rem",
                  color: "var(--fg)",
                  margin: "0 0 0.5rem",
                }}
              >
                You have no active parking passes
              </h3>
              <p style={{ margin: "0 0 1rem", fontSize: "0.85rem" }}>
                Browse our Pass Marketplace to subscribe to monthly or annual
                packages for your regular parking destinations.
              </p>
              <button
                type="button"
                className="btn-primary"
                onClick={() => setTabMode("store")}
              >
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
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.65rem",
              }}
            >
              {orders.map((order) => (
                <div
                  key={order.id}
                  className="card"
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: "1rem",
                    flexWrap: "wrap",
                    padding: "1rem 1.25rem",
                  }}
                >
                  <div>
                    <div
                      style={{
                        fontSize: "0.75rem",
                        color: "var(--muted)",
                        marginBottom: "0.2rem",
                      }}
                    >
                      Order ID: {order.id} •{" "}
                      {new Date(order.createdAt).toLocaleDateString("en-GB")}
                    </div>
                    <strong
                      style={{
                        fontSize: "0.95rem",
                        color: "var(--fg)",
                        fontFamily: "Be Vietnam Pro",
                      }}
                    >
                      {order.plan === "monthly"
                        ? "Monthly Pass"
                        : "Annual Pass"}{" "}
                      — {order.lotName}
                    </strong>
                    <div
                      style={{
                        fontSize: "0.78rem",
                        color: "var(--muted)",
                        marginTop: "0.25rem",
                        display: "flex",
                        alignItems: "center",
                        gap: "0.4rem",
                      }}
                    >
                      <span>Payment via:</span>
                      <PaymentMethodLogo
                        method={order.paymentMethod}
                        size="xs"
                        showName
                      />
                    </div>
                  </div>

                  <div style={{ textAlign: "right" }}>
                    <strong
                      style={{
                        display: "block",
                        fontSize: "1.1rem",
                        fontFamily: "Be Vietnam Pro",
                        color: "var(--primary)",
                      }}
                    >
                      {order.amount.toLocaleString("vi-VN")} VND
                    </strong>
                    <span
                      style={{
                        fontSize: "0.75rem",
                        fontWeight: 700,
                        padding: "0.15rem 0.5rem",
                        borderRadius: "999px",
                        background:
                          order.paymentStatus === "paid"
                            ? "rgba(34, 197, 94, 0.15)"
                            : "rgba(234, 88, 12, 0.12)",
                        color:
                          order.paymentStatus === "paid"
                            ? "#22c55e"
                            : "#ea580c",
                        border: `1px solid ${
                          order.paymentStatus === "paid"
                            ? "rgba(34, 197, 94, 0.3)"
                            : "rgba(234, 88, 12, 0.35)"
                        }`,
                      }}
                    >
                      {order.paymentStatus === "paid"
                        ? "Paid"
                        : "Payment Pending"}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div
              className="card"
              style={{
                padding: "3rem 1.5rem",
                textAlign: "center",
                color: "var(--muted)",
              }}
            >
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: "50%",
                  background: "var(--primary)15",
                  color: "var(--primary)",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  margin: "0 auto 0.75rem",
                }}
              >
                <UntitledIcon name="clipboard" size={26} />
              </div>
              <h3
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "1.2rem",
                  color: "var(--fg)",
                  margin: "0 0 0.5rem",
                }}
              >
                No pass purchase orders yet
              </h3>
            </div>
          )}
        </div>
      )}

      {selectedLotForDetail && (
        <Modal title={selectedLotForDetail.name + " — Gói gửi xe"} lang="vi" className="max-w-[820px]!" onClose={() => setSelectedLotForDetail(null)}>
          <p className="mb-4 flex items-start gap-2 text-sm text-[var(--muted)]">
            <UntitledIcon name="map-pin" size={16} /> {selectedLotForDetail.address}
          </p>
          {checkoutSuccess ? (
            <div
              style={{
                padding: "2rem",
                textAlign: "center",
                background: "rgba(34, 197, 94, 0.08)",
                border: "1.5px solid rgba(34, 197, 94, 0.35)",
                borderRadius: "0.75rem",
                margin: "1rem 0",
              }}
            >
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: "50%",
                  background: "rgba(34, 197, 94, 0.15)",
                  color: "#22c55e",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  margin: "0 auto 0.75rem",
                }}
              >
                <UntitledIcon name="award" size={30} />
              </div>
              <h4
                style={{
                  fontFamily: "Be Vietnam Pro",
                  fontWeight: 700,
                  fontSize: "1.3rem",
                  color: "#22c55e",
                  margin: "0 0 0.5rem",
                }}
              >
                Đã kích hoạt gói gửi xe
              </h4>
              <p
                style={{
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                  marginBottom: "1.5rem",
                }}
              >
                {checkoutSuccess}
              </p>
              <div
                style={{
                  display: "flex",
                  gap: "0.75rem",
                  justifyContent: "center",
                }}
              >
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => {
                    setSelectedLotForDetail(null)

                    setTabMode("my-passes")
                  }}
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.4rem",
                  }}
                >
                  <span>Xem trong My Passes</span>
                  <UntitledIcon name="arrow-right" size={14} />
                </button>
                <button
                  type="button"
                  className="btn-outline"
                  onClick={() => setSelectedLotForDetail(null)}
                >
                  Xong
                </button>
              </div>
            </div>
          ) : (
            <>
              <fieldset className="driver-pass-plan-picker">
                <legend>Chọn gói gửi xe</legend>
                <div className="driver-pass-plan-list">
                  {([
                    { id: "monthly", label: "Gói tháng", description: "Phù hợp khi cần gửi xe trong thời gian ngắn.", days: 30, price: selectedLotForDetail.subscriptionMonthly },
                    { id: "annual", label: "Gói năm", description: "Phù hợp khi gửi xe thường xuyên trong cả năm.", days: 365, price: selectedLotForDetail.subscriptionYearly },
                  ] as const).map(plan => {
                    const available = hasPassPrice(plan.price)
                    const active = available && selectedPlan === plan.id
                    const price = available ? plan.price.toLocaleString("vi-VN") + " VND" : "Chưa có giá"
                    return <label key={plan.id} className="driver-pass-plan" data-selected={active}>
                      <span className="driver-pass-plan-row">
                        <input type="radio" name="parking-pass-plan" value={plan.id} checked={active}
                          aria-label={plan.label + ", " + plan.days + " ngày, " + price}
                          disabled={!available} onChange={() => setSelectedPlan(plan.id)} />
                        <span className="driver-pass-plan-heading">
                          <strong>{plan.label}</strong>
                          <span>{plan.days} ngày kể từ khi kích hoạt</span>
                        </span>
                        <span className="driver-pass-plan-price">{price}{available && <small>mỗi gói</small>}</span>
                      </span>
                      <span className="driver-pass-plan-details" aria-hidden={!active}>
                        <span className="driver-pass-plan-details-body">
                          <span>{plan.description}</span>
                          {available && <span><UntitledIcon name="banknote" size={15} /> Bình quân {Math.round(plan.price / plan.days).toLocaleString("vi-VN")} VND/ngày</span>}
                          <span><UntitledIcon name="map-pin" size={15} /> Áp dụng tại {selectedLotForDetail.name}</span>
                          <span><UntitledIcon name="car" size={15} /> Gắn với biển số xe bạn chọn khi thanh toán</span>
                        </span>
                      </span>
                    </label>
                  })}
                </div>
              </fieldset>

              {/* CHECKOUT CONFIG: VEHICLE PLATE & PAYMENT METHOD */}
              <div
                style={{
                  borderTop: "1px solid var(--border)",
                  paddingTop: "1.25rem",
                  display: "flex",
                  flexDirection: "column",
                  gap: "1rem",
                }}
              >
                {/* VEHICLE BINDING */}
                <div>
                  <label htmlFor="pass-registered-vehicle"
                    style={{
                      display: "block",
                      fontSize: "0.82rem",
                      fontWeight: 700,
                      color: "var(--fg)",
                      marginBottom: "0.4rem",
                    }}
                  >
                    Chọn biển số xe đã đăng ký
                  </label>
                  {!currentVehicles ? <p role="status" className="text-sm text-[var(--muted)]">Đang tải danh sách xe…</p>
                    : currentVehicles.error ? <div className="flex flex-wrap items-center gap-3">
                        <p role="alert" className="text-sm text-[var(--muted)]">{currentVehicles.error}</p>
                        <button type="button" className="btn-outline" onClick={() => setVehicleRetry(value => value + 1)}>Thử lại</button>
                      </div>
                    : vehicles.length > 0 ? <RoundedSelect id="pass-registered-vehicle" className="input"
                        aria-label="Biển số xe đã đăng ký" value={selectedVehicle?.id ?? ""}
                        onChange={event => setSelectedVehicleId(event.target.value)}
                        style={{ maxWidth: 360, fontSize: "0.85rem" }}>
                        {vehicles.map(vehicle => <option key={vehicle.id} value={vehicle.id}>
                          {formatVietnamesePlate(vehicle)} ({vehicle.vehicleType === "CAR" ? "Ô tô" : "Xe máy"})
                        </option>)}
                      </RoundedSelect>
                    : <p className="text-sm text-[var(--muted)]">Bạn chưa có xe đã đăng ký. Hãy thêm xe trong My Vehicles rồi mở lại gói gửi xe.</p>}
                </div>

                {/* PAYMENT METHOD SELECTION */}
                <div>
                  <label
                    style={{
                      display: "block",
                      fontSize: "0.82rem",
                      fontWeight: 700,
                      color: "var(--fg)",
                      marginBottom: "0.5rem",
                    }}
                  >
                    Chọn phương thức thanh toán
                  </label>
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns:
                        "repeat(auto-fit, minmax(110px, 1fr))",
                      gap: "0.5rem",
                    }}
                  >
                    {PAYMENT_OPTIONS.map((opt) => (
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

                          border:
                            selectedMethod === opt.id
                              ? "2px solid var(--primary)"
                              : "1px solid var(--border)",

                          background:
                            selectedMethod === opt.id
                              ? "color-mix(in srgb, var(--primary) 10%, var(--card))"
                              : "var(--card)",

                          cursor: "pointer",

                          transition: "all 0.15s ease",
                        }}
                      >
                        <PaymentMethodLogo method={opt.id} size="md" />
                        <span
                          style={{
                            fontSize: "0.72rem",
                            fontWeight: 600,
                            color: "var(--fg)",
                            textAlign: "center",
                          }}
                        >
                          {opt.name}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>

                {/* SUMMARY & SUBMIT */}
                <div
                  style={{
                    background: "var(--bg)",
                    padding: "1rem 1.25rem",
                    borderRadius: "0.75rem",
                    border: "1px solid var(--border)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: "1rem",
                    marginTop: "0.5rem",
                  }}
                >
                  <div>
                    <span
                      style={{
                        fontSize: "0.78rem",
                        color: "var(--muted)",
                        display: "block",
                      }}
                    >
                      Tổng tiền phải trả (
                      {selectedPlan === "monthly" ? "30 ngày" : "365 ngày"}
                      ):
                    </span>
                    <strong
                      style={{
                        fontFamily: "Be Vietnam Pro",
                        fontWeight: 800,
                        fontSize: "1.4rem",
                        color: "var(--primary)",
                      }}
                    >
                      {hasPassPrice(selectedPassPrice) ? selectedPassPrice.toLocaleString("vi-VN") + " VND" : "Chưa có giá"}
                    </strong>
                  </div>

                  <div className="flex flex-wrap gap-2.5">
                    <button
                      type="button"
                      className="btn-outline"
                      onClick={() => setSelectedLotForDetail(null)}
                    >
                      Hủy
                    </button>
                    <button
                      type="button"
                      className="btn-primary"
                      onClick={handlePurchasePass}
                        disabled={!selectedVehicle || !hasPassPrice(selectedPassPrice)}
                      style={{
                        padding: "0.55rem 1.25rem",
                        fontSize: "0.85rem",
                        fontWeight: 700,
                        display: "inline-flex",
                        alignItems: "center",
                        gap: "0.4rem",
                      }}
                    >
                      <span>Thanh toán và kích hoạt gói</span>
                      <UntitledIcon name="arrow-right" size={14} />
                    </button>
                  </div>
                </div>
              </div>
            </>
          )}
        </Modal>
      )}
    </div>
  )
}
