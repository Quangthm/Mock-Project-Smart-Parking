import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useState, useMemo } from "react"
import { showUiToast } from "../../../lib/uiToast"
import { parkingApi } from "../../../lib/parkingApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { formatSlotLabel } from "../../../lib/operationalLayout"
import { SLOT_VISUAL_STATE_HEX } from "../../../lib/3d/parking3DConfig"
import type { OperatorSlot } from "../../../lib/operatorApi"
import type { SlotView } from "../parking-lot/useLotStatus"

interface SlotManagementProps {
  siteId: string
  siteName: string
  slots: OperatorSlot[]
  slotView: (slot: OperatorSlot) => SlotView
  hasOverridePermission: boolean
}

interface OperationalSpaceItem {
  id: string
  code: string
  vehicleType: "car" | "motorcycle"
  physicalState: string
}

export function SlotManagement({ siteId, siteName, slots, slotView, hasOverridePermission }: SlotManagementProps) {
  // Form states for designating a new backup slot
  const [selectedSlotId, setSelectedSlotId] = useState("")
  const [designateReason, setDesignateReason] = useState("")
  const [busy, setBusy] = useState(false)
  const [feedback, setFeedback] = useState<{
    type: "success" | "error"
    message: string
  } | null>(null)

  // Release confirmation modal
  const [releasingSlot, setReleasingSlot] = useState<{ id: string; code: string } | null>(null)

  const backupSlots = useMemo(
    () => slots.filter((slot) => slotView(slot).reservationState === "BACKUP").map((slot) => ({
      id: slot.id,
      code: slot.number,
      vehicleType: slot.vehicleType.toLowerCase().includes("motorcycle") ? "motorcycle" as const : "car" as const,
      physicalState: slotView(slot).physicalState,
    })),
    [slots, slotView],
  )

  // Slots eligible for being designated as backup: physically AVAILABLE,
  // unreserved and bookable. The axis checks are strict on purpose, so a bay
  // with a missing or drifted axis fails closed and never reaches the dropdown.
  const eligibleSlots: OperationalSpaceItem[] = useMemo(() => {
    return slots
      .filter((slot) =>
        slotView(slot).physicalState === "AVAILABLE" &&
        slotView(slot).reservationState === "UNRESERVED" &&
        slot.isBookable === true,
      )
      .map((s) => ({
        id: s.id,
        code: s.number,
        vehicleType: s.vehicleType.toLowerCase().includes("motorcycle") ? "motorcycle" as const : "car" as const,
        physicalState: "AVAILABLE",
      }))
  }, [slots, slotView])

  // Handle Mark Slot as Backup
  async function handleDesignateBackup(e: React.FormEvent) {
    e.preventDefault()
    if (!hasOverridePermission || !eligibleSlots.some(slot => slot.id === selectedSlotId) || !designateReason.trim() || busy) return

    setBusy(true)
    setFeedback(null)

    try {
      await parkingApi.markBackup(siteId, selectedSlotId, designateReason.trim())

      setFeedback({
        type: "success",
        message: `Slot successfully designated as Backup. It is now reserved for emergency and overflow allocation.`,
      })

      setSelectedSlotId("")
      showUiToast("Backup slot designated", "The selected slot is reserved for backup capacity.")
      setDesignateReason("")
      window.dispatchEvent(new Event("sp-operator-refresh"))
    } catch (err) {
      showUiToast("Backup slot not designated", err instanceof Error ? err.message : "Failed to designate backup slot.", "error")
      setFeedback({
        type: "error",
        message: err instanceof Error ? err.message : "Failed to designate backup slot.",
      })
    } finally {
      setBusy(false)
    }
  }

  // Handle Release Backup Slot
  async function handleReleaseBackup(spaceId: string) {
    if (!hasOverridePermission || busy) return
    setBusy(true)
    setFeedback(null)

    try {
      await parkingApi.releaseBackup(siteId, spaceId)

      setFeedback({
        type: "success",
        message: `Backup designation released. The slot is now available for regular bookings.`,
      })
      setReleasingSlot(null)
      showUiToast("Backup slot released", "The slot is available for regular bookings.")
      window.dispatchEvent(new Event("sp-operator-refresh"))
    } catch (err) {
      showUiToast("Backup slot not released", err instanceof Error ? err.message : "Failed to release backup slot.", "error")
      setFeedback({
        type: "error",
        message: err instanceof Error ? err.message : "Failed to release backup slot.",
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-4">
      {/* Permission Alert if missing */}
      {!hasOverridePermission && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-700 dark:text-amber-400">
          <div className="flex items-center gap-2 font-bold">
            <UntitledIcon name="alert" size={16} />
            <span>Restricted Operator Permission</span>
          </div>
          <p className="mt-1">
            Your current operator profile does not possess the <code className="font-mono font-bold">SLOT_OVERRIDE</code> permission for this facility. You may inspect backup allocations, but designating or releasing backup spaces requires elevated rights.
          </p>
        </div>
      )}

      {/* Feedback Banner */}
      {feedback && (
        <div
          role="status"
          className={`flex items-center justify-between rounded-xl border p-4 text-sm ${
            feedback.type === "success"
              ? "border-green-500/30 bg-green-500/10 text-green-600"
              : "border-red-500/30 bg-red-500/10 text-red-600"
          }`}
        >
          <div className="flex items-center gap-2">
            <UntitledIcon
              name={feedback.type === "success" ? "check-circle" : "alert"}
              size={18}
            />
            <span>{feedback.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedback(null)}
            className="text-xs font-semibold hover:underline"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* SECTION 2: Form to Designate New Backup Slot */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="mb-4">
          <h2 className="text-base font-bold text-[var(--fg)]">
            Designate Space as Operational Backup
          </h2>
          <p className="text-xs text-[var(--muted)]">
            Designating a slot as backup withholds it from public driver booking until released or automatically allocated during re-allocation conflicts.
          </p>
        </div>

        <form onSubmit={handleDesignateBackup} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            {/* Select Slot */}
            <div>
              <label htmlFor="operator-backup-slot" className="block text-xs font-semibold text-[var(--fg)] mb-1">
                Select Vacant Operational Slot:
              </label>
              <RoundedSelect
                id="operator-backup-slot"
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono font-semibold"
                value={selectedSlotId}
                disabled={!hasOverridePermission || busy || eligibleSlots.length === 0}
                onChange={(e) => setSelectedSlotId(e.target.value)}
                required
              >
                <option value="">-- Choose an eligible space --</option>
                {eligibleSlots.map((s) => (
                  <option key={s.id} value={s.id}>
                    Slot {formatSlotLabel(s.code)} ({s.vehicleType === "motorcycle" ? "Motorcycle" : "Car"})
                  </option>
                ))}
              </RoundedSelect>
              {eligibleSlots.length === 0 && (
                <p className="mt-1 text-xs text-amber-600">
                  No vacant spaces currently available to designate.
                </p>
              )}
            </div>

            {/* Reason Input */}
            <div>
              <label htmlFor="operator-backup-reason" className="block text-xs font-semibold text-[var(--fg)] mb-1">
                Designation Reason / Purpose (Required):
              </label>
              <input
                id="operator-backup-reason"
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)]"
                maxLength={2000}
                placeholder="e.g. Reserved for emergency services, VIP overflow buffer, or maintenance offset"
                value={designateReason}
                disabled={!hasOverridePermission || busy}
                onChange={(e) => setDesignateReason(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="flex justify-end">
            <button
              type="submit"
              disabled={!hasOverridePermission || !eligibleSlots.some(slot => slot.id === selectedSlotId) || !designateReason.trim() || busy}
              className="btn-primary"
            >
              <UntitledIcon name="shield" size={14} />
              {busy ? "Designating…" : "Confirm Backup Designation"}
            </button>
          </div>
        </form>
      </div>

      {/* SECTION 3: Current Backup Registry Table */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-base font-bold text-[var(--fg)]">
              Current Backup Slots ({backupSlots.length})
            </h2>
            <p className="text-xs text-[var(--muted)]">
              Spaces protected from general bookings and held for operational contingencies
            </p>
          </div>
          <span
            className="rounded px-2.5 py-1 text-sm font-medium"
            style={{
              color: SLOT_VISUAL_STATE_HEX.backup,
              backgroundColor: `${SLOT_VISUAL_STATE_HEX.backup}1f`,
            }}
          >
            Backup active
          </span>
        </div>

        <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
          <table className="w-full text-left text-xs min-w-[650px]">
            <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
              <tr>
                <th className="px-4 py-3">Slot Code</th>
                <th className="px-4 py-3">Vehicle Type</th>
                <th className="px-4 py-3">Physical State</th>
                <th className="px-4 py-3">Reservation State</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {backupSlots.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-[var(--muted)]">
                    No slots are currently designated as backup in this facility.
                  </td>
                </tr>
              ) : (
                backupSlots.map((space) => (
                  <tr key={space.id} className="hover:bg-[var(--card)]/50 transition">
                    <td className="px-4 py-3 font-mono font-bold text-[var(--fg)]">
                      <span className="bg-[var(--card)] px-2 py-0.5 rounded border border-[var(--border)]">
                        {formatSlotLabel(space.code)}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium capitalize">
                      {space.vehicleType}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-green-500/10 px-2 py-0.5 text-xs font-bold text-green-600 uppercase">
                        {space.physicalState || "Unknown"}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className="rounded px-2 py-0.5 text-xs font-bold uppercase"
                        style={{
                          color: SLOT_VISUAL_STATE_HEX.backup,
                          backgroundColor: `${SLOT_VISUAL_STATE_HEX.backup}1f`,
                        }}
                      >
                        BACKUP
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        disabled={!hasOverridePermission || busy}
                        className="text-xs text-red-600 hover:text-red-700 font-semibold px-2 py-1 rounded border border-red-500/30 hover:bg-red-500/10 transition disabled:opacity-50"
                        onClick={() => setReleasingSlot({ id: space.id, code: space.code })}
                      >
                        Release Backup
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* CONFIRMATION MODAL: Release Backup */}
      {releasingSlot && (
        <div
          role="presentation"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
          onMouseDown={(e) => !busy && e.target === e.currentTarget && setReleasingSlot(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="release-backup-title"
            className="w-full max-w-md rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl animate-in text-sm text-[var(--fg)]"
          >
            <div className="flex items-center gap-3">
              <div className="rounded-lg bg-[var(--status-maintenance-bg)] p-3 text-[var(--maintenance)]">
                <UntitledIcon name="shield" size={24} />
              </div>
              <div>
                <h3 id="release-backup-title" className="text-base font-bold text-[var(--fg)]">
                  Release Backup Designation
                </h3>
                <p className="text-xs text-[var(--muted)]">
                  Return slot to regular driver availability pool
                </p>
              </div>
            </div>

            <div className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3 space-y-2 text-xs">
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Slot Code:</span>
                <span className="font-mono font-bold text-[var(--fg)]">
                  {formatSlotLabel(releasingSlot.code)}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Facility:</span>
                <span>{siteName}</span>
              </div>
            </div>

            <p className="mt-3 text-xs text-[var(--muted)]">
              Once released, this slot will instantly be eligible for public reservations by Drivers according to allocation rules.
            </p>

            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                className="btn-outline text-xs py-1.5 px-3"
                disabled={busy}
                onClick={() => setReleasingSlot(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={busy}
                className="text-xs py-1.5 px-4 font-semibold text-white rounded-lg bg-red-600 hover:bg-red-700 transition"
                onClick={() => handleReleaseBackup(releasingSlot.id)}
              >
                {busy ? "Releasing..." : "Confirm Release"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
