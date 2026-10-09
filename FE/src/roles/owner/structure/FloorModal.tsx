import { useState, useEffect, type FormEvent } from "react"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

interface FloorModalProps {
  isOpen: boolean

  onClose: () => void

  onSave: (name: string, code: string, status?: "active" | "inactive") => void

  initialData?: {
    id?: string
    name: string
    code: string
    status: "active" | "inactive"
  } | null
}

export function FloorModal({
  isOpen,
  onClose,
  onSave,
  initialData,
}: FloorModalProps) {
  const [name, setName] = useState("")

  const [code, setCode] = useState("")

  const [status, setStatus] = useState<"active" | "inactive">("active")

  const [error, setError] = useState("")

  useEffect(() => {
    if (initialData) {
      setName(initialData.name)

      setCode(initialData.code)

      setStatus(initialData.status)
    } else {
      setName("")

      setCode("")

      setStatus("active")
    }

    setError("")
  }, [initialData, isOpen])

  if (!isOpen) return null

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()

    if (!name.trim()) {
      setError("Please enter a floor name (e.g. Basement 1 or Ground Floor).")

      return
    }

    if (!code.trim()) {
      setError("Please enter a short code (e.g. B1, B2, G, 1F).")

      return
    }

    onSave(name.trim(), code.trim().toUpperCase(), status)

    onClose()
  }

  return (
    <div
      role="presentation"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
      style={{
        position: "fixed",

        inset: 0,

        zIndex: 1000,

        display: "flex",

        alignItems: "center",

        justifyContent: "center",

        padding: "1rem",

        background: "rgba(2, 6, 23, 0.65)",

        backdropFilter: "blur(4px)",
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="floor-modal-title"
        className="card"
        style={{
          width: "100%",

          maxWidth: 480,

          background: "var(--bg)",

          color: "var(--fg)",

          border: "1px solid var(--border)",

          borderRadius: "1rem",

          boxShadow: "0 20px 50px rgba(0, 0, 0, 0.25)",

          padding: "1.5rem",
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "1.25rem",
          }}
        >
          <div>
            <span
              style={{
                fontSize: "0.72rem",
                fontWeight: 700,
                color: "var(--primary)",
                letterSpacing: "0.08em",
                textTransform: "uppercase",
              }}
            >
              Parking Structure
            </span>
            <h3
              id="floor-modal-title"
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.25rem",
                margin: "0.2rem 0 0",
              }}
            >
              {initialData ? "Edit Floor" : "Add New Floor"}
            </h3>
          </div>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            style={{
              width: 32,

              height: 32,

              borderRadius: "50%",

              border: "1px solid var(--border)",

              background: "var(--card)",

              color: "var(--fg)",

              display: "flex",

              alignItems: "center",

              justifyContent: "center",

              cursor: "pointer",
            }}
          >
            <UntitledIcon name="x" size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: "grid", gap: "1rem" }}>
          <div>
            <label
              style={{
                display: "block",
                fontSize: "0.82rem",
                fontWeight: 600,
                marginBottom: "0.35rem",
              }}
            >
              Floor Code (Short Identifier){" "}
              <span style={{ color: "var(--occupied)" }}>*</span>
            </label>
            <input
              className="input"
              placeholder="e.g. B1, B2, G, 1F, P3"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              autoFocus
            />
            <span
              style={{
                fontSize: "0.75rem",
                color: "var(--muted)",
                marginTop: "0.25rem",
                display: "block",
              }}
            >
              Appears on parking slot IDs and mobile wayfinding indicators.
            </span>
          </div>

          <div>
            <label
              style={{
                display: "block",
                fontSize: "0.82rem",
                fontWeight: 600,
                marginBottom: "0.35rem",
              }}
            >
              Floor Display Name{" "}
              <span style={{ color: "var(--occupied)" }}>*</span>
            </label>
            <input
              className="input"
              placeholder="e.g. Basement 1 (Tầng Hầm B1) or Ground Level"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div>
            <label
              style={{
                display: "block",
                fontSize: "0.82rem",
                fontWeight: 600,
                marginBottom: "0.35rem",
              }}
            >
              Floor Operating Status
            </label>
            <select
              className="input"
              value={status}
              onChange={(e) =>
                setStatus(e.target.value as "active" | "inactive")
              }
            >
              <option value="active">
                Active (Open for parking & reservations)
              </option>
              <option value="inactive">
                Inactive (Closed / Under renovation)
              </option>
            </select>
          </div>

          {error && (
            <p
              style={{
                margin: 0,
                color: "var(--occupied)",
                fontSize: "0.82rem",
              }}
            >
              {error}
            </p>
          )}

          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              gap: "0.75rem",
              marginTop: "0.5rem",
            }}
          >
            <button type="button" className="btn-outline" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="btn-primary">
              {initialData ? "Save Changes" : "Create Floor"}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
