import { useState } from "react"
import { createPortal } from "react-dom"
import type { Booking } from "../../../lib/types"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { driverData as store } from "../data/data"

export function BookingCard({ booking, onPayNow }: { booking: Booking; onPayNow?: (booking: Booking) => void }) {
  const [show3DModal, setShow3DModal] = useState(false)
  const lot = store.getLots().find(l => l.id === booking.lotId)
  const paymentStatus = booking.paymentStatus ?? (booking.status === "completed" || booking.status === "checked-in" || booking.status === "confirmed" ? "paid" : "pending")
  const bookingColor = booking.status === "completed" ? "#22c55e" : booking.status === "cancelled" ? "#ef4444" : booking.status === "checked-in" ? "#2563eb" : booking.status === "confirmed" ? "#22c55e" : "#f59e0b"
  const paymentColor = paymentStatus === "paid" ? "#22c55e" : paymentStatus === "failed" ? "#ef4444" : "#f59e0b"
  return (
    <div
      className="card"
      style={{
        display: "flex",
        justifyContent: "space-between",
        gap: "1rem",
        flexWrap: "wrap",
        border: paymentStatus === "pending" && booking.status !== "cancelled"
          ? "1.5px solid rgba(234, 88, 12, 0.35)"
          : "1px solid var(--border)",
      }}
    >
      <div>
        <div style={{ fontSize: "0.72rem", color: "var(--muted)", marginBottom: "0.25rem" }}>
          Booking ID: {booking.id}
        </div>
        <div
          style={{
            fontFamily: "Outfit",
            fontWeight: 600,
            fontSize: "0.95rem",
            color: "var(--fg)",
            marginBottom: "0.25rem",
          }}
        >
          {booking.lotName}
        </div>
        <div style={{ fontSize: "0.82rem", color: "var(--muted)" }}>
          Slot {booking.slotNumber} · {booking.licensePlate}{" "}
          <UntitledIcon name={booking.plateType === "foreign" ? "globe" : "map-pin"} size={14} />
        </div>
        <div
          style={{
            fontSize: "0.78rem",
            color: "var(--muted)",
            marginTop: "0.2rem",
          }}
        >
          {new Date(booking.startTime).toLocaleString("en-GB", {
            dateStyle: "short",
            timeStyle: "short",
          })}{" "}
          –{" "}
          {new Date(booking.endTime).toLocaleString("en-GB", {
            dateStyle: "short",
            timeStyle: "short",
          })}
        </div>
        {booking.status === "completed" && booking.completedAt && (
          <div style={{ marginTop: "0.25rem", fontSize: "0.75rem", color: "var(--muted)" }}>
            Completed: {new Date(booking.completedAt).toLocaleString("en-GB", { dateStyle: "short", timeStyle: "short" })}
          </div>
        )}
      </div>

      <div style={{ textAlign: "right" }}>
        <div
          style={{
            display: "inline-block",
            padding: "0.2rem 0.625rem",
            borderRadius: "999px",
            fontSize: "0.75rem",
            fontWeight: 600,
            background: `${bookingColor}20`,
            color: bookingColor,
          }}
        >
          {booking.status.replace("-", " ").toUpperCase()}
        </div>

        <div>
          <div
            style={{
              display: "inline-block",
              marginTop: "0.35rem",
              padding: "0.22rem 0.65rem",
              borderRadius: "999px",
              fontSize: "0.72rem",
              fontWeight: 600,
              background: paymentStatus === "pending" ? "rgba(234, 88, 12, 0.10)" : `${paymentColor}20`,
              color: paymentStatus === "pending" ? "#ea580c" : paymentColor,
              border: paymentStatus === "pending" ? "1px solid rgba(234, 88, 12, 0.40)" : `1px solid ${paymentColor}30`,
            }}
          >
            Payment {paymentStatus.toUpperCase()}
          </div>
        </div>

        <div
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "0.95rem",
            color: "var(--fg)",
            marginTop: "0.375rem",
          }}
        >
          {booking.amount.toLocaleString("vi-VN")}₫
        </div>

        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", justifyContent: "flex-end", marginTop: "0.6rem", flexWrap: "wrap" }}>
          {booking.status !== "cancelled" && (
            <button
              type="button"
              className="btn-outline"
              onClick={() => setShow3DModal(true)}
              style={{ padding: "0.4rem 0.8rem", fontSize: "0.78rem" }}
            >
              View in 3D
            </button>
          )}
          {onPayNow && paymentStatus !== "paid" && booking.status !== "completed" && booking.status !== "cancelled" && (
            <button
              type="button"
              className="btn-primary"
              onClick={() => onPayNow(booking)}
              style={{
                padding: "0.4rem 0.8rem",
                fontSize: "0.78rem",
                background: "#ea580c",
                color: "#ffffff",
                boxShadow: "0 2px 8px rgba(234, 88, 12, 0.35)",
              }}
            >
              Pay Now
            </button>
          )}
        </div>
      </div>

      {/* 3D Location Pop-up Modal rendered via Portal to escape all card layout/transform limits */}
      {show3DModal && typeof document !== "undefined" && createPortal(
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
            padding: "1.25rem",
          }}
          onClick={() => setShow3DModal(false)}
        >
          <div
            className="card animate-in"
            style={{
              width: "100%",
              maxWidth: 1100,
              maxHeight: "92vh",
              display: "flex",
              flexDirection: "column",
              background: "var(--card)",
              borderRadius: "1rem",
              border: "1px solid var(--border)",
              padding: "1.5rem",
              boxShadow: "0 25px 60px -15px rgba(0, 0, 0, 0.7)",
              overflow: "hidden",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "1rem", gap: "1rem" }}>
              <div>
                <h3 style={{ margin: 0, fontSize: "1.25rem", fontFamily: "Outfit", fontWeight: 700, color: "var(--fg)" }}>
                  3D Parking Slot Location — Slot {booking.slotNumber}
                </h3>
                <span style={{ fontSize: "0.85rem", color: "var(--muted)", marginTop: "0.2rem", display: "block" }}>
                  {booking.lotName} • Camera automatically focused on your reserved slot
                </span>
              </div>
              <button
                type="button"
                className="btn-outline"
                style={{ padding: "0.35rem 0.75rem", fontSize: "0.82rem" }}
                onClick={() => setShow3DModal(false)}
              >
                ✕ Close
              </button>
            </div>

            <div style={{ borderRadius: "0.75rem", overflow: "hidden", border: "1px solid var(--border)", background: "#090d16", flex: 1, minHeight: 0 }}>
              <Parking3DViewer
                lotType={lot?.type || "outdoor"}
                modelUrl={lot?.modelUrl}
                slots={lot?.slots || []}
                selectedSlotId={booking.slotId || booking.slotNumber}
                focusSlotId={booking.slotId || booking.slotNumber}
                height={560}
                interactive={true}
              />
            </div>

            <div style={{ marginTop: "1rem", padding: "0.75rem 1rem", background: "var(--primary)12", borderRadius: "0.5rem", border: "1px solid var(--primary)25", display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "0.85rem", flexWrap: "wrap", gap: "0.5rem" }}>
              <span>
                Reserved Slot: <strong style={{ color: "var(--primary)" }}>Slot {booking.slotNumber}</strong> • Vehicle: <strong>{booking.licensePlate}</strong>
              </span>
              <span style={{ color: "#22c55e", fontWeight: 600 }}>● Accurately Located</span>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  )
}
