import { useState } from "react"

import { operatorData as store } from "../data/data"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

export function EmergencyPanel() {
  type IncidentStatus = "open" | "resolved"

  const [incidents, setIncidents] = useState<{
    id: number

    type: string

    plate: string

    slot: string

    time: string

    status: IncidentStatus
  }[]>([
    {
      id: 1,

      type: "wrong-spot",

      plate: "51A-99999",

      slot: "105",

      time: "14:23",

      status: "open",
    },

    {
      id: 2,

      type: "overtime",

      plate: "51AB-12345",

      slot: "208",

      time: "13:45",

      status: "open",
    },
  ])

  const [form, setForm] = useState({
    plate: "",

    slot: "",

    type: "wrong-spot",

    notes: "",
  })

  function resolve(id: number) {
    setIncidents((inc) =>
      inc.map((i) => (i.id === id ? { ...i, status: "resolved" as const } : i)),
    )

    store.addAuditLog({
      userId: "op",

      userName: "Operator",

      userRole: "operator",

      action: "INCIDENT_RESOLVED",

      details: `Incident #${id} resolved`,
    })
  }

  function reportIncident(e: React.FormEvent) {
    e.preventDefault()

    setIncidents((inc) => [
      ...inc,

      {
        id: Date.now(),

        type: form.type,

        plate: form.plate,

        slot: form.slot,

        time: new Date().toLocaleTimeString("en-GB", {
          hour: "2-digit",

          minute: "2-digit",
        }),

        status: "open",
      },
    ])

    setForm({ plate: "", slot: "", type: "wrong-spot", notes: "" })
  }

  const open = incidents.filter((i) => i.status === "open")

  return (
    <div
      style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1.5rem" }}
    >
      <div>
        <h2
          style={{
            fontFamily: "Outfit",

            fontWeight: 700,

            fontSize: "1.2rem",

            marginBottom: "1.25rem",

            color: "var(--fg)",
          }}
        >
          Active Incidents
        </h2>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}
        >
          {open.map((inc) => (
            <div
              key={inc.id}
              className="card"
              style={{
                border: `1.5px solid ${
                  inc.type === "wrong-spot" ? "#ef4444" : "#f59e0b"
                }`,
              }}
            >
              <div
                style={{
                  display: "flex",

                  justifyContent: "space-between",

                  marginBottom: "0.375rem",
                }}
              >
                <span
                  style={{
                    fontWeight: 600,

                    color: inc.type === "wrong-spot" ? "#ef4444" : "#f59e0b",

                    fontSize: "0.85rem",
                  }}
                >
                  <>
                    <i
                      aria-hidden="true"
                      style={{
                        display: "inline-block",
                        width: 8,
                        height: 8,
                        marginRight: 5,
                        borderRadius: "50%",
                        background:
                          inc.type === "wrong-spot" ? "#ef4444" : "#f59e0b",
                      }}
                    />
                    {inc.type === "wrong-spot" ? "Wrong Spot" : "Overtime"}
                  </>
                </span>
                <span style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                  {inc.time}
                </span>
              </div>
              <div
                style={{
                  fontSize: "0.85rem",

                  color: "var(--fg)",

                  marginBottom: "0.625rem",
                }}
              >
                Plate: <strong>{inc.plate}</strong> · Slot:{" "}
                <strong>{inc.slot}</strong>
              </div>
              <button
                onClick={() => resolve(inc.id)}
                style={{
                  fontSize: "0.78rem",

                  padding: "0.3rem 0.75rem",

                  background: "#f0fdf4",

                  border: "1px solid #86efac",

                  color: "#166534",

                  borderRadius: "var(--radius)",

                  cursor: "pointer",
                }}
              >
                Mark Resolved
              </button>
            </div>
          ))}
          {!open.length && (
            <div
              style={{
                color: "var(--muted)",

                fontSize: "0.875rem",

                textAlign: "center",

                padding: "2rem",
              }}
            >
              <>
                <UntitledIcon name="check-circle" size={16} /> No active
                incidents
              </>
            </div>
          )}
        </div>
      </div>
      <div>
        <h2
          style={{
            fontFamily: "Outfit",

            fontWeight: 700,

            fontSize: "1.2rem",

            marginBottom: "1.25rem",

            color: "var(--fg)",
          }}
        >
          Report Incident
        </h2>
        <form
          onSubmit={reportIncident}
          className="card"
          style={{ display: "flex", flexDirection: "column", gap: "0.875rem" }}
        >
          <div>
            <label className="label">Type</label>
            <select
              className="input"
              value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value }))}
            >
              <option value="wrong-spot">Wrong spot (Red alert)</option>
              <option value="overtime">Overtime (Amber alert)</option>
              <option value="damage">Vehicle damage</option>
              <option value="other">Other</option>
            </select>
          </div>
          <div>
            <label className="label">License Plate</label>
            <input
              className="input"
              value={form.plate}
              onChange={(e) =>
                setForm((f) => ({ ...f, plate: e.target.value }))
              }
              placeholder="51A-12345"
              required
            />
          </div>
          <div>
            <label className="label">Slot Number</label>
            <input
              className="input"
              value={form.slot}
              onChange={(e) => setForm((f) => ({ ...f, slot: e.target.value }))}
              placeholder="e.g. 105"
              required
            />
          </div>
          <div>
            <label className="label">Notes</label>
            <textarea
              className="input"
              rows={2}
              value={form.notes}
              onChange={(e) =>
                setForm((f) => ({ ...f, notes: e.target.value }))
              }
              placeholder="Additional details..."
              style={{ resize: "none" }}
            />
          </div>
          <button
            type="submit"
            className="btn-primary"
            style={{ fontSize: "0.875rem" }}
          >
            <>
              <UntitledIcon name="alert" size={16} /> Report Incident
            </>
          </button>
        </form>
      </div>
    </div>
  )
}
