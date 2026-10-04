import { operatorData as store } from "../data/data"

export function LotStatus({
  lot,
}: {
  lot: ReturnType<typeof store.getLots>[0] | null
}) {
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
        <h3
          style={{
            fontFamily: "Outfit",
            fontWeight: 600,
            fontSize: "0.95rem",
            marginBottom: "0.875rem",
            color: "var(--fg)",
          }}
        >
          Live Slot Map
        </h3>
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
    </div>
  )
}
