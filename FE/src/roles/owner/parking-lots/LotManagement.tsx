import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import type { ParkingLot } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Lot management

export function LotManagement({
  siteFilterId = "all",
}: {
  siteFilterId?: string
}) {
  const { user } = useApp()

  function getVisibleLots() {
    const ownerLots = store.getLotsByOwner(user?.id ?? "")

    return siteFilterId === "all"
      ? ownerLots
      : ownerLots.filter((lot) => lot.id === siteFilterId)
  }

  const [lots, setLots] = useState(getVisibleLots)

  const [editLot, setEditLot] = useState<ParkingLot | null>(null)

  useEffect(() => setLots(getVisibleLots()), [siteFilterId, user?.id])

  function toggleStatus(lot: ParkingLot) {
    const updated = {
      ...lot,
      status: lot.status === "active" ? "inactive" as const : "active" as const,
    }

    store.saveLot(updated)

    setLots(getVisibleLots())
  }

  function saveLot() {
    if (!editLot) return

    store.saveLot(editLot)

    setLots(getVisibleLots())

    setEditLot(null)
  }

  return (
    <div>
      {editLot ? (
        <div className="animate-in">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              marginBottom: "1.25rem",
            }}
          >
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.05rem",
                color: "var(--fg)",
              }}
            >
              Edit Lot: {editLot.name}
            </h3>
            <button
              className="btn-outline"
              style={{ fontSize: "0.82rem" }}
              onClick={() => setEditLot(null)}
            >
              Cancel
            </button>
          </div>
          <div
            className="card"
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.875rem",
            }}
          >
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 1fr",
                gap: "0.875rem",
              }}
            >
              <div>
                <label className="label">Lot Name</label>
                <input
                  className="input"
                  value={editLot.name}
                  onChange={(e) =>
                    setEditLot((l) => l && { ...l, name: e.target.value })
                  }
                />
              </div>
              <div>
                <label className="label">Address</label>
                <input
                  className="input"
                  value={editLot.address}
                  onChange={(e) =>
                    setEditLot((l) => l && { ...l, address: e.target.value })
                  }
                />
              </div>
              <div>
                <label className="label">Hourly Rate (₫)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.hourlyRate}
                  onChange={(e) =>
                    setEditLot(
                      (l) => l && { ...l, hourlyRate: +e.target.value },
                    )
                  }
                />
              </div>
              <div>
                <label className="label">Daily Rate (₫)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.dailyRate}
                  onChange={(e) =>
                    setEditLot((l) => l && { ...l, dailyRate: +e.target.value })
                  }
                />
              </div>
              <div>
                <label className="label">Night Rate (₫)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.nightRate}
                  onChange={(e) =>
                    setEditLot((l) => l && { ...l, nightRate: +e.target.value })
                  }
                />
              </div>
              <div>
                <label className="label">Grace Period (min)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.gracePeriodMinutes}
                  onChange={(e) =>
                    setEditLot(
                      (l) => l && { ...l, gracePeriodMinutes: +e.target.value },
                    )
                  }
                />
              </div>
              <div>
                <label className="label">Monthly Pass (₫)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.subscriptionMonthly}
                  onChange={(e) =>
                    setEditLot(
                      (l) =>
                        l && { ...l, subscriptionMonthly: +e.target.value },
                    )
                  }
                />
              </div>
              <div>
                <label className="label">Annual Pass (₫)</label>
                <input
                  className="input"
                  type="number"
                  value={editLot.subscriptionYearly}
                  onChange={(e) =>
                    setEditLot(
                      (l) => l && { ...l, subscriptionYearly: +e.target.value },
                    )
                  }
                />
              </div>
            </div>
            <button
              className="btn-primary"
              style={{ alignSelf: "flex-start" }}
              onClick={saveLot}
            >
              Save Changes
            </button>
          </div>
        </div>
      ) : (
        <div>
          <h3
            style={{
              fontFamily: "Outfit",
              fontWeight: 700,
              fontSize: "1.05rem",
              marginBottom: "1.25rem",
              color: "var(--fg)",
            }}
          >
            Your Parking Lots
          </h3>
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.875rem",
            }}
          >
            {lots.map((lot) => {
              const available = lot.slots.filter(
                (s) => s.status === "available",
              ).length

              const occupied = lot.slots.filter(
                (s) => s.status === "occupied",
              ).length

              return (
                <div
                  key={lot.id}
                  className="card"
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "1rem",
                    flexWrap: "wrap",
                  }}
                >
                  <div style={{ flex: 1 }}>
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "0.5rem",
                        marginBottom: "0.25rem",
                      }}
                    >
                      <span
                        style={{
                          fontFamily: "Outfit",
                          fontWeight: 700,
                          fontSize: "0.95rem",
                          color: "var(--fg)",
                        }}
                      >
                        {lot.name}
                      </span>
                      <span
                        style={{
                          fontSize: "0.72rem",
                          background:
                            lot.status === "active" ? "#22c55e20" : "#ef444420",
                          color:
                            lot.status === "active" ? "#22c55e" : "#ef4444",
                          padding: "0.1rem 0.5rem",
                          borderRadius: "999px",
                          fontWeight: 600,
                        }}
                      >
                        {lot.status.toUpperCase()}
                      </span>
                    </div>
                    <div
                      style={{
                        fontSize: "0.8rem",
                        color: "var(--muted)",
                        marginBottom: "0.5rem",
                      }}
                    >
                      {lot.address}
                    </div>
                    <div
                      style={{
                        display: "flex",
                        gap: "1rem",
                        fontSize: "0.8rem",
                      }}
                    >
                      <span
                        style={{
                          color: "#22c55e",
                          display: "inline-flex",
                          alignItems: "center",
                          gap: 3,
                        }}
                      >
                        <UntitledIcon name="check" size={13} /> {available} free
                      </span>
                      <span
                        style={{
                          color: "#ef4444",
                          display: "inline-flex",
                          alignItems: "center",
                          gap: 3,
                        }}
                      >
                        <UntitledIcon name="x" size={13} /> {occupied} occupied
                      </span>
                      <span style={{ color: "var(--muted)" }}>
                        Total: {lot.totalSlots}
                      </span>
                    </div>
                  </div>
                  <div
                    style={{
                      display: "flex",
                      gap: "0.5rem",
                      alignItems: "center",
                    }}
                  >
                    <button
                      className="btn-outline"
                      style={{ fontSize: "0.8rem", padding: "0.35rem 0.75rem" }}
                      onClick={() => setEditLot(lot)}
                    >
                      Edit
                    </button>
                    <button
                      style={{
                        fontSize: "0.8rem",
                        padding: "0.35rem 0.75rem",
                        background:
                          lot.status === "active" ? "#fee2e2" : "#f0fdf4",
                        color: lot.status === "active" ? "#dc2626" : "#16a34a",
                        border: `1px solid ${
                          lot.status === "active" ? "#fca5a5" : "#86efac"
                        }`,
                        borderRadius: "var(--radius)",
                        cursor: "pointer",
                      }}
                      onClick={() => toggleStatus(lot)}
                    >
                      {lot.status === "active" ? "Deactivate" : "Activate"}
                    </button>
                  </div>
                </div>
              )
            })}
            {!lots.length && (
              <div
                style={{
                  textAlign: "center",
                  padding: "3rem",
                  color: "var(--muted)",
                }}
              >
                <div
                  style={{ color: "var(--primary)", marginBottom: "0.5rem" }}
                >
                  <UntitledIcon name="parking" size={30} />
                </div>
                <p>No lots yet. Complete onboarding to add your first lot.</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
