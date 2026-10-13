import { useState, useEffect } from "react"

import { UntitledIcon } from "../icon/UntitledIcon"

interface Props {
  contact: string

  onSuccess: () => void

  onFailLock: () => void

  onClose: () => void
}

export function OTPModal({ contact, onSuccess, onFailLock, onClose }: Props) {
  const [otp] = useState(() =>
    String(Math.floor(100000 + Math.random() * 900000)),
  )

  const [input, setInput] = useState("")

  const [attempts, setAttempts] = useState(0)

  const [error, setError] = useState("")

  const [shown, setShown] = useState(false)

  useEffect(() => {
    // Simulate sending OTP - show it briefly so user can see it

    setTimeout(() => setShown(true), 500)
  }, [])

  function verify() {
    if (input === otp) {
      onSuccess()
    } else {
      const next = attempts + 1

      setAttempts(next)

      if (next >= 3) {
        onFailLock()
      } else {
        setError(`Incorrect OTP. ${3 - next} attempt(s) remaining.`)

        setInput("")
      }
    }
  }

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,0.6)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 9999,
      }}
    >
      <div
        className="card"
        style={{ width: "100%", maxWidth: "400px", margin: "1rem" }}
      >
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
              fontSize: "1.2rem",
              margin: 0,
            }}
          >
            OTP Verification
          </h3>
          <button
            onClick={onClose}
            aria-label="Close"
            style={{
              background: "none",
              border: "none",
              cursor: "pointer",
              color: "var(--muted)",
              display: "flex",
              padding: 4,
            }}
          >
            <UntitledIcon name="x" size={18} />
          </button>
        </div>

        {shown && (
          <div
            style={{
              background: "#fefce8",
              border: "1px solid #fde68a",
              borderRadius: "var(--radius)",
              padding: "0.75rem",
              marginBottom: "1rem",
            }}
          >
            <p style={{ fontSize: "0.8rem", color: "#92400e", margin: 0 }}>
              <strong>Simulated OTP sent to {contact}:</strong>
            </p>
            <p
              style={{
                fontSize: "1.5rem",
                fontWeight: 800,
                color: "#92400e",
                margin: "0.25rem 0 0",
                letterSpacing: "4px",
              }}
            >
              {otp}
            </p>
          </div>
        )}

        <p
          style={{
            color: "var(--muted)",
            fontSize: "0.9rem",
            marginBottom: "1rem",
          }}
        >
          Enter the 6-digit OTP sent to <strong>{contact}</strong> to confirm
          your booking.
        </p>

        <label className="label">OTP Code</label>
        <input
          className="input"
          value={input}
          onChange={(e) =>
            setInput(e.target.value.replace(/\D/g, "").slice(0, 6))
          }
          onKeyDown={(e) => e.key === "Enter" && verify()}
          placeholder="Enter 6-digit code"
          style={{
            letterSpacing: "6px",
            fontSize: "1.2rem",
            textAlign: "center",
            marginBottom: "0.75rem",
          }}
          maxLength={6}
          autoFocus
        />

        {error && (
          <div
            style={{
              background: "#fee2e2",
              border: "1px solid #fca5a5",
              borderRadius: "var(--radius)",
              padding: "0.5rem 0.75rem",
              marginBottom: "0.75rem",
              color: "#dc2626",
              fontSize: "0.85rem",
            }}
          >
            {error}
          </div>
        )}

        <button
          className="btn-primary"
          style={{ width: "100%" }}
          onClick={verify}
          disabled={input.length !== 6}
        >
          Confirm Booking
        </button>
      </div>
    </div>
  )
}
