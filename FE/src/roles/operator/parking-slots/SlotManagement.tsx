import { useEffect, useState } from "react"

import { useApp } from "../../../context/AppContext"

import type { ParkingLot, ParkingSlot, SlotStatus } from "../../../lib/types"

import { operatorData as store } from "../data/data"

const inputClass =
  "rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-sm text-[var(--fg)] outline-none focus:border-[var(--primary)]"

export function SlotManagement({ lot }: { lot: ParkingLot | null }) {
  const { user } = useApp()

  const [slots, setSlots] = useState<ParkingSlot[]>(lot?.slots ?? [])

  const [error, setError] = useState("")

  const [saved, setSaved] = useState(false)

  useEffect(() => {
    setSlots(lot?.slots ?? [])

    setError("")

    setSaved(false)
  }, [lot?.id])

  function updateSlot(id: string, patch: Partial<ParkingSlot>) {
    setSlots((current) =>
      current.map((slot) => (slot.id === id ? { ...slot, ...patch } : slot)),
    )

    setSaved(false)

    setError("")
  }

  function saveChanges() {
    if (!lot) return

    const slotNumbers = slots.map((slot) => slot.number.trim().toLowerCase())

    if (
      slots.some((slot) => !slot.number.trim()) ||
      new Set(slotNumbers).size !== slotNumbers.length
    ) {
      setError("Every slot must have a unique, non-empty slot number.")

      setSaved(false)

      return
    }

    store.saveLot({ ...lot, slots })

    store.addAuditLog({
      userId: user?.id ?? "",
      userName: user?.name ?? "",
      userRole: user?.role ?? "operator",
      action: "SLOTS_UPDATED",
      details: `Updated slot locations or statuses for ${lot.name}`,
    })

    setSaved(true)

    setError("")
  }

  if (!lot)
    return (
      <p className="text-sm text-[var(--muted)]">No assigned parking lot.</p>
    )

  const statusStyles: Record<SlotStatus, string> = {
    available: "bg-green-500/10 text-green-600",

    occupied: "bg-red-500/10 text-red-600",

    reserved: "bg-amber-500/10 text-amber-600",
  }

  return (
    <section className="mx-auto w-full max-w-6xl space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-[var(--primary)]">
            {lot.name}
          </p>
          <h1 className="mt-1 text-2xl font-bold text-[var(--fg)]">
            Slot Management
          </h1>
          <p className="mt-2 text-sm text-[var(--muted)]">
            Move a slot to another floor, update its number, or change its
            current status.
          </p>
        </div>
        <button type="button" className="btn-primary" onClick={saveChanges}>
          Save Changes
        </button>
      </header>

      {error && (
        <p
          role="alert"
          className="rounded-lg bg-red-500/10 px-4 py-3 text-sm text-red-600"
        >
          {error}
        </p>
      )}
      {saved && (
        <p
          role="status"
          className="rounded-lg bg-green-500/10 px-4 py-3 text-sm text-green-600"
        >
          Slot updates saved.
        </p>
      )}

      <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
        <table className="w-full min-w-[680px] text-left text-sm">
          <thead className="border-b border-[var(--border)] text-xs uppercase tracking-wide text-[var(--muted)]">
            <tr>
              <th className="px-4 py-3">Slot</th>
              <th className="px-4 py-3">Move to floor</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Current</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border)]">
            {slots.map((slot) => (
              <tr key={slot.id}>
                <td className="px-4 py-3">
                  <label className="sr-only" htmlFor={`slot-number-${slot.id}`}>
                    Slot number
                  </label>
                  <input
                    id={`slot-number-${slot.id}`}
                    className={`${inputClass} w-32`}
                    value={slot.number}
                    onChange={(event) =>
                      updateSlot(slot.id, { number: event.target.value })
                    }
                  />
                </td>
                <td className="px-4 py-3">
                  <label className="sr-only" htmlFor={`slot-floor-${slot.id}`}>
                    Floor for slot {slot.number}
                  </label>
                  <select
                    id={`slot-floor-${slot.id}`}
                    className={inputClass}
                    value={slot.floor}
                    onChange={(event) =>
                      updateSlot(slot.id, { floor: Number(event.target.value) })
                    }
                  >
                    {Array.from(
                      { length: lot.floors },
                      (_, index) => index + 1,
                    ).map((floor) => (
                      <option key={floor} value={floor}>
                        Floor {floor}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="px-4 py-3">
                  <label className="sr-only" htmlFor={`slot-status-${slot.id}`}>
                    Status for slot {slot.number}
                  </label>
                  <select
                    id={`slot-status-${slot.id}`}
                    className={inputClass}
                    value={slot.status}
                    onChange={(event) =>
                      updateSlot(slot.id, {
                        status: event.target.value as SlotStatus,
                      })
                    }
                  >
                    <option value="available">Available</option>
                    <option value="occupied">Occupied</option>
                    <option value="reserved">Reserved</option>
                  </select>
                </td>
                <td className="px-4 py-3">
                  <span
                    className={`rounded-full px-2.5 py-1 text-xs font-semibold capitalize ${statusStyles[slot.status]}`}
                  >
                    {slot.status}
                  </span>
                  <span className="ml-2 text-xs text-[var(--muted)]">
                    Floor {slot.floor}
                  </span>
                </td>
              </tr>
            ))}
            {!slots.length && (
              <tr>
                <td
                  colSpan={4}
                  className="px-4 py-8 text-center text-[var(--muted)]"
                >
                  This lot has no slots to manage.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}
