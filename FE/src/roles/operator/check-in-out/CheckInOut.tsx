import { useState } from "react"

import { operatorData as store } from "../data/data"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

import type { Booking } from "../../../lib/types"

export function CheckInOut({
  lot,
}: {
  lot: ReturnType<typeof store.getLots>[0] | null
}) {
  const [plateInput, setPlateInput] = useState("")

  const [result, setResult] = useState<{
    booking: Booking

    action: "check-in" | "check-out"
  } | null>(null)

  const [error, setError] = useState("")

  function search() {
    setError("")

    setResult(null)

    if (!lot || !plateInput.trim()) return

    const bookings = store.getBookingsByLot(lot.id)

    const match = bookings.find(
      (b) =>
        b.licensePlate.toUpperCase().replace(/[^A-Z0-9]/g, "") ===
          plateInput.toUpperCase().replace(/[^A-Z0-9]/g, "") &&
        ["confirmed", "checked-in"].includes(b.status),
    )

    if (!match) {
      setError("No active booking found for this plate.")

      return
    }

    const action = match.status === "confirmed" ? "check-in" : "check-out"

    setResult({ booking: match, action })
  }

  function processAction() {
    if (!result) return

    const updated = {
      ...result.booking,

      status:
        result.action === "check-in"
          ? "checked-in" as const
          : "completed" as const,

      completedAt:
        result.action === "check-out"
          ? new Date().toISOString()
          : result.booking.completedAt,
    }

    store.saveBooking(updated)

    if (lot) {
      const updatedLot = {
        ...lot,

        slots: lot.slots.map((s) =>
          s.id === result.booking.slotId
            ? {
                ...s,
                status:
                  result.action === "check-in"
                    ? "occupied" as const
                    : "available" as const,
                reservedUntil: undefined,
                reservedBy: undefined,
              }
            : s,
        ),
      }

      store.saveLot(updatedLot)

      if (result.action === "check-in") {
        store.notifyUser(
          lot.ownerId,
          "DRIVER_PARKED",
          "New parking session",
          `A Driver has parked at one of your parking lots. Parking lot: ${lot.name}; Slot: ${result.booking.slotNumber}; Booking ID: ${result.booking.id}; Time: ${new Date().toLocaleString()}.`,
          result.booking.id,
        )
      }
    }

    store.addAuditLog({
      userId: "op",

      userName: "Operator",

      userRole: "operator",

      action: result.action.toUpperCase(),

      details: `${result.action} for plate ${result.booking.licensePlate}`,
    })

    alert(
      `${
        result.action === "check-in" ? "Check-in" : "Check-out"
      } processed for ${result.booking.licensePlate}`,
    )

    setPlateInput("")

    setResult(null)
  }

  return (
    <div style={{ maxWidth: "600px" }}>
      <h2
        style={{
          fontFamily: "Outfit",

          fontWeight: 700,

          fontSize: "1.3rem",

          marginBottom: "1.5rem",

          color: "var(--fg)",
        }}
      >
        Check-In / Check-Out
      </h2>
      <div className="card" style={{ marginBottom: "1.25rem" }}>
        <label className="label">Search by License Plate</label>
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <input
            className="input"
            value={plateInput}
            onChange={(e) => setPlateInput(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && search()}
            placeholder="e.g. 51A-12345"
            style={{ flex: 1 }}
          />
          <button className="btn-primary" onClick={search}>
            Search
          </button>
        </div>
        {error && (
          <div
            style={{
              marginTop: "0.625rem",

              color: "#dc2626",

              fontSize: "0.85rem",
            }}
          >
            {error}
          </div>
        )}
      </div>
      {result && (
        <div
          className="card animate-in"
          style={{
            border: `2px solid ${
              result.action === "check-in" ? "#22c55e" : "#2563eb"
            }`,
          }}
        >
          <div
            style={{
              display: "flex",

              justifyContent: "space-between",

              marginBottom: "1rem",
            }}
          >
            <h3
              style={{
                fontFamily: "Outfit",

                fontWeight: 700,

                fontSize: "1.05rem",

                color: "var(--fg)",

                margin: 0,
              }}
            >
              <UntitledIcon
                name={result.action === "check-in" ? "check-circle" : "car"}
                size={18}
              />{" "}
              {result.action === "check-in" ? "Check-In" : "Check-Out"}
            </h3>
            <span
              style={{
                fontFamily: "Outfit",

                fontWeight: 700,

                fontSize: "0.9rem",

                color: result.action === "check-in" ? "#22c55e" : "#2563eb",
              }}
            >
              {result.booking.licensePlate}{" "}
              <UntitledIcon
                name={
                  result.booking.plateType === "foreign" ? "globe" : "map-pin"
                }
                size={14}
              />
            </span>
          </div>
          <div
            style={{
              display: "grid",

              gridTemplateColumns: "1fr 1fr",

              gap: "0.5rem",

              marginBottom: "1rem",
            }}
          >
            {[
              ["Slot", result.booking.slotNumber],

              ["Lot", result.booking.lotName],

              ["Driver ID", result.booking.driverId.slice(0, 8) + "..."],

              ["Amount", `${result.booking.amount.toLocaleString("vi-VN")}₫`],

              [
                "Start",

                new Date(result.booking.startTime).toLocaleString("en-GB", {
                  dateStyle: "short",

                  timeStyle: "short",
                }),
              ],

              [
                "End",

                new Date(result.booking.endTime).toLocaleString("en-GB", {
                  dateStyle: "short",

                  timeStyle: "short",
                }),
              ],
            ].map(([k, v]) => (
              <div key={k}>
                <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
                  {k}
                </div>
                <div
                  style={{
                    fontSize: "0.875rem",

                    fontWeight: 600,

                    color: "var(--fg)",
                  }}
                >
                  {v}
                </div>
              </div>
            ))}
          </div>
          <button
            className="btn-primary"
            style={{
              width: "100%",

              justifyContent: "center",

              background: result.action === "check-in" ? "#22c55e" : "#2563eb",
            }}
            onClick={processAction}
          >
            <>
              <UntitledIcon
                name={result.action === "check-in" ? "check-circle" : "car"}
                size={17}
              />{" "}
              Confirm {result.action === "check-in" ? "Check-In" : "Check-Out"}
            </>
          </button>
        </div>
      )}
    </div>
  )
}
