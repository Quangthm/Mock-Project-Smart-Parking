import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useRef, useState } from "react"
import { showUiToast } from "../../../lib/uiToast"

import { request } from "../../../lib/authApi"

import {
  operatorsApi,
  type OperatorAssignment,
} from "../../../lib/parkingApi"
import {
  formatSlotLabel,
  type OperationalLayoutProjection,
  type SlotVisualStateDto,
} from "../../../lib/operationalLayout"
import {
  invalidateOperatorLayout,
  reconcileOperatorLayout,
  registerOperatorSiteMeta,
  subscribeOperatorLayout,
} from "../../../lib/operatorRealtimeResources"

const server = (
  import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045"
).replace(/\/$/, "")

/**
 * BACKUP may only be designated on an unreserved bay that is either physically
 * free and bookable, or currently occupied (operator override). Everything else
 * fails closed.
 */
function isBackupCandidate(slot: SlotVisualStateDto): boolean {
  if (slot.reservationState !== "UNRESERVED") return false
  if (slot.physicalState === "OCCUPIED") return true
  return slot.physicalState === "AVAILABLE" && slot.isBookable
}

export function OperatorBackupPanel() {
  const [sites, setSites] = useState<OperatorAssignment[]>([])
  const [site, setSite] = useState("")
  const [layout, setLayout] = useState<OperationalLayoutProjection | null>(null)

  const [reason, setReason] = useState("")
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const sitesRef = useRef(sites)
  sitesRef.current = sites

  useEffect(() => {
    let active = true
    operatorsApi
      .assignments()
      .then((values) => {
        if (active)
          setSites(
            values.filter((v) => v.permissions.includes("SLOT_OVERRIDE")),
          )
      })
      .catch((e) => {
        if (active)
          setMessage(
            e instanceof Error ? e.message : "Cannot load assigned lots.",
          )
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true
    setLayout(null)
    if (!site) return () => { active = false }
    const assignment = sitesRef.current.find((item) => item.siteId === site)
    if (assignment) {
      registerOperatorSiteMeta(site, {
        tenantId: assignment.tenantId,
        permissions: assignment.permissions,
      })
    }
    const unsubscribe = subscribeOperatorLayout(site, (snapshot) => {
      if (!active) return
      if (snapshot.lastOutcome === "error") {
        setMessage(
          snapshot.lastError instanceof Error ? snapshot.lastError.message : "Cannot load slots.",
        )
        return
      }
      const nextLayout = snapshot.data?.operationalLayout ?? null
      if (snapshot.lastOutcome === "synced" && nextLayout) {
        setLayout(nextLayout)
      }
    })
    void reconcileOperatorLayout(site, "initial")
    return () => {
      active = false
      unsubscribe()
    }
  }, [site])

  async function mark(id: string) {
    if (busy) return
    const slot = layout?.slots.find((item) => item.slotId === id)
    if (!slot || !isBackupCandidate(slot)) return
    setBusy(true)
    setMessage("")
    try {
      await request(
        `/${site}/slots/${id}/backup`,
        "POST",
        { reason },
        "/api/parking-lots",
        server,
      )
      invalidateOperatorLayout(site, "mutation")
      setMessage("Slot marked as backup.")
      showUiToast("Backup slot updated", "Slot marked as backup.")
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Cannot mark backup.")
    } finally {
      setBusy(false)
    }
  }

  if (!sites.length) return message ? <p role="alert">{message}</p> : null

  return (
    <section className="card">
      <h3>Operational backup slots</h3>
      <label className="label">
        Assigned lot
        <RoundedSelect
          className="input"
          disabled={busy}
          value={site}
          onChange={(e) => {
            setSite(e.target.value)
            setMessage("")
          }}
        >
          <option value="">Select a lot</option>
          {sites.map((s) => (
            <option key={s.siteId} value={s.siteId}>
              {s.siteId}
            </option>
          ))}
        </RoundedSelect>
      </label>
      <label className="label">
        Reason
        <input
          className="input"
          maxLength={2000}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          disabled={busy}
        />
      </label>
      {layout?.slots.map((slot) => (
        <p key={slot.slotId}>
          <strong>{formatSlotLabel(slot.slotCode)}</strong> · {slot.supportedVehicleType} ·{" "}
          {slot.reservationState === "UNRESERVED"
            ? "Not reserved"
            : slot.reservationState}{" "}
          <button
            className="btn-outline"
            disabled={busy || !reason.trim() || !isBackupCandidate(slot)}
            onClick={() => void mark(slot.slotId)}
          >
            Mark backup
          </button>
        </p>
      ))}
      <p>
        Backup remains designated until the applicable release policy is
        fulfilled.
      </p>
      <p role="status">{message}</p>
    </section>
  )
}
