import { useState } from "react"

import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import type { ParkingLot } from "../../../lib/types"

// Policy management

export function PolicyManagement() {
  const { user } = useApp()

  const lots = store.getLotsByOwner(user?.id ?? "")

  const [selectedLot, setSelectedLot] = useState<ParkingLot | null>(
    lots[0] ?? null,
  )

  const [policyText, setPolicyText] = useState("")

  return (
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
        Policy Management
      </h3>
      {lots.length > 1 && (
        <div style={{ marginBottom: "1rem" }}>
          <label className="label">Select Lot</label>
          <select
            className="input"
            style={{ maxWidth: "300px" }}
            value={selectedLot?.id}
            onChange={(e) =>
              setSelectedLot(lots.find((l) => l.id === e.target.value) ?? null)
            }
          >
            {lots.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </div>
      )}
      {selectedLot && (
        <div className="card">
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "1rem",
              marginBottom: "1.25rem",
            }}
          >
            <div>
              <div
                style={{
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                  marginBottom: "0.25rem",
                }}
              >
                Expiry / Operating Hours
              </div>
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                }}
              >
                06:00 – 22:00 Daily
              </div>
            </div>
            <div>
              <div
                style={{
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                  marginBottom: "0.25rem",
                }}
              >
                Grace Period
              </div>
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "var(--fg)",
                }}
              >
                {selectedLot.gracePeriodMinutes} minutes
              </div>
            </div>
            <div>
              <div
                style={{
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                  marginBottom: "0.25rem",
                }}
              >
                Violation Alert (Red)
              </div>
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "#ef4444",
                }}
              >
                Wrong spot / Expired &gt; 30min
              </div>
            </div>
            <div>
              <div
                style={{
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                  marginBottom: "0.25rem",
                }}
              >
                Warning Alert (Amber)
              </div>
              <div
                style={{
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  color: "#f59e0b",
                }}
              >
                Grace period expiring soon
              </div>
            </div>
          </div>
          <div>
            <label className="label">Custom Policy Notes</label>
            <textarea
              className="input"
              rows={4}
              value={policyText}
              onChange={(e) => setPolicyText(e.target.value)}
              placeholder="Enter special rules, conditions, or notes for this lot..."
              style={{ resize: "vertical", marginBottom: "0.75rem" }}
            />
            <button
              className="btn-primary"
              style={{ fontSize: "0.875rem" }}
              onClick={() => alert("Policy saved (simulated).")}
            >
              Save Policy
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
