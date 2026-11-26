import { useState, useMemo, useEffect } from "react"
import "../../../booking-support.css"


import { useApp } from "../../../context/AppContext"

import type {
  ParkingLot,
  PackageOrder,
  User,
} from "../../../lib/types"

import { driverData as store } from "../data/data"


import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"

import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"
import { distanceKm, validCoordinates, type LatLng } from "../../../lib/parkingCoordinates"

import { PassOffersDialog } from "./PassOffers"

function getLotEnterprise(lot: ParkingLot, users: User[]): string {
  const owner = users.find((u) => u.id === lot.ownerId)

  if (owner?.name && !owner.name.includes("demo")) return owner.name

  return "Parking operator"
}

type TabMode = "store" | "my-passes" | "orders"
const passesPerPage = 12
const nearbyRadiusKm = 3

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

  const users = useMemo(() => store.getUsers?.() ?? [], [])

  const lots = useMemo(
    () => store.getLots().filter((lot) => lot.status === "active"),
    [],
  )

  const passes = useMemo(
    () => (user ? store.getPassesByDriver(user.id) : []),
    [user],
  )
  const passPageCount = Math.max(1, Math.ceil(passes.length / passesPerPage))
  const currentPassPage = Math.min(passPage, passPageCount)
  const visiblePasses = passes.slice((currentPassPage - 1) * passesPerPage, currentPassPage * passesPerPage)

  const orders = useMemo(
    () => (user ? store.getPackageOrdersByDriver(user.id) : []),
    [user],
  )

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

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <section className="driver-pass-header">
        <div className="relative z-10 min-w-0">
          <PageHeader title="Parking Passes" description="Choose a parking location to see its available pass offers." />
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
                onClick={() => setSelectedLotForDetail(lot)}>
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
        <PassOffersDialog siteId={selectedLotForDetail.id} name={selectedLotForDetail.name}
          address={selectedLotForDetail.address} onClose={() => setSelectedLotForDetail(null)} />
      )}
    </div>
  )
}
