import { useState } from "react"
import { operatorData as store } from "../data/data"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import type { ParkingSlot } from "../../../lib/types"

export function LotStatus({
  lot,
}: {
  lot: ReturnType<typeof store.getLots>[0] | null
}) {
  const [viewMode, setViewMode] = useState<"3d" | "grid">("3d")
  const [selectedSlot, setSelectedSlot] = useState<ParkingSlot | null>(null)

  if (!lot) return <div style={{ color: "var(--muted)" }}>No lot assigned.</div>
  const available = lot.slots.filter((s) => s.status === "available").length
  const occupied = lot.slots.filter((s) => s.status === "occupied").length
  const reserved = lot.slots.filter((s) => s.status === "reserved").length
  const utilization = Math.round(((occupied + reserved) / lot.totalSlots) * 100)

  return (
    <div>
      <h2
        style={{
          fontFamily: "Outfit",
          fontWeight: 700,
          fontSize: "1.3rem",
          marginBottom: "1.5rem",
          color: "var(--fg)",
        }}
      >
        Lot Status — {lot.name}
      </h2>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(4, 1fr)",
          gap: "1rem",
          marginBottom: "1.5rem",
        }}
      >
        {[
          { label: "Available", value: available, color: "#22c55e" },
          { label: "Occupied", value: occupied, color: "#ef4444" },
          { label: "Reserved", value: reserved, color: "#f59e0b" },
          {
            label: "Utilization",
            value: `${utilization}%`,
            color: "var(--primary)",
          },
        ].map((s) => (
          <div key={s.label} className="card" style={{ textAlign: "center" }}>
            <div
              style={{
                fontFamily: "Outfit",
                fontWeight: 800,
                fontSize: "1.4rem",
                color: s.color,
              }}
            >
              {s.value}
            </div>
            <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
              {s.label}
            </div>
          </div>
        ))}
      </div>
      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem", flexWrap: "wrap", gap: "0.5rem" }}>
          <div>
            <h3
              style={{
                fontFamily: "Outfit",
                fontWeight: 600,
                fontSize: "0.95rem",
                margin: 0,
                color: "var(--fg)",
              }}
            >
              Live Slot Map — Giám Sát Thời Gian Thực
            </h3>
            <span style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
              {viewMode === "3d" ? "Mô hình 3D Digital Twin với trạng thái ô xe real-time" : "Sơ đồ lưới 2D phân tầng"}
            </span>
          </div>

          <div style={{ display: "flex", gap: "0.25rem", padding: "0.2rem", background: "var(--bg)", borderRadius: "var(--radius)", border: "1px solid var(--border)" }}>
            <button
              type="button"
              onClick={() => setViewMode("3d")}
              style={{
                padding: "0.3rem 0.65rem",
                fontSize: "0.76rem",
                fontWeight: 600,
                borderRadius: "calc(var(--radius) - 2px)",
                border: "none",
                cursor: "pointer",
                background: viewMode === "3d" ? "var(--primary)" : "transparent",
                color: viewMode === "3d" ? "white" : "var(--muted)",
                transition: "all 0.2s",
              }}
            >
              🎮 3D Digital Twin
            </button>
            <button
              type="button"
              onClick={() => setViewMode("grid")}
              style={{
                padding: "0.3rem 0.65rem",
                fontSize: "0.76rem",
                fontWeight: 600,
                borderRadius: "calc(var(--radius) - 2px)",
                border: "none",
                cursor: "pointer",
                background: viewMode === "grid" ? "var(--primary)" : "transparent",
                color: viewMode === "grid" ? "white" : "var(--muted)",
                transition: "all 0.2s",
              }}
            >
              ▦ Sơ Đồ Lưới
            </button>
          </div>
        </div>

        {viewMode === "3d" && (
          <div>
            <Parking3DViewer
              lotType={lot.type}
              modelUrl={lot.modelUrl}
              slots={lot.slots}
              selectedSlotId={selectedSlot?.id}
              onSelectSlot={(slot) => setSelectedSlot(slot)}
              height={380}
              interactive={true}
            />
            {selectedSlot && (
              <div style={{ marginTop: "0.75rem", padding: "0.6rem 0.85rem", background: "var(--primary)12", borderRadius: "var(--radius)", display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: "0.82rem" }}>
                <span>
                  Đang kiểm tra: <strong>Slot {selectedSlot.number}</strong> · Tầng {selectedSlot.floor}
                </span>
                <span style={{ fontWeight: 600, color: selectedSlot.status === "available" ? "#22c55e" : selectedSlot.status === "occupied" ? "#ef4444" : "#f59e0b" }}>
                  Trạng thái: {selectedSlot.status.toUpperCase()}
                </span>
              </div>
            )}
          </div>
        )}

        {viewMode === "grid" && (
          <div>
            {Array.from({ length: lot.floors }, (_, f) => f + 1).map((floor) => (
              <div key={floor} style={{ marginBottom: "1rem" }}>
                {lot.floors > 1 && (
                  <div
                    style={{
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      color: "var(--muted)",
                      marginBottom: "0.375rem",
                    }}
                  >
                    Floor {floor}
                  </div>
                )}
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(10, 1fr)",
                    gap: "0.25rem",
                  }}
                >
                  {lot.slots
                    .filter((s) => s.floor === floor)
                    .map((slot) => (
                      <div
                        key={slot.id}
                        title={`Slot ${slot.number}`}
                        style={{
                          height: "28px",
                          borderRadius: "3px",
                          border: `1.5px solid ${
                            slot.status === "available"
                              ? "#22c55e"
                              : slot.status === "occupied"
                                ? "#ef4444"
                                : "#f59e0b"
                          }`,
                          background:
                            slot.status === "available"
                              ? "#22c55e18"
                              : slot.status === "occupied"
                                ? "#ef444418"
                                : "#f59e0b18",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          fontSize: "0.55rem",
                          fontWeight: 600,
                          color:
                            slot.status === "available"
                              ? "#22c55e"
                              : slot.status === "occupied"
                                ? "#ef4444"
                                : "#f59e0b",
                        }}
                      >
                        {slot.number}
                      </div>
                    ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
