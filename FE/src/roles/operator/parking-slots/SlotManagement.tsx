import { useState, useMemo, useEffect } from "react"
import { useApp } from "../../../context/AppContext"
import { operatorData as store } from "../data/data"
import { structureStore } from "../../owner/data/structureStore"
import { operatorsApi, type OperatorAssignment } from "../../../lib/parkingApi"
import { request } from "../../../lib/authApi"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { ParkingLot, ParkingSlot } from "../../../lib/types"

const server = (
  import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045"
).replace(/\/$/, "")

interface SlotManagementProps {
  lot: ParkingLot | null
}

interface OperationalSpaceItem {
  id: string
  code: string
  vehicleType: "car" | "motorcycle"
}

export function SlotManagement({ lot }: SlotManagementProps) {
  const { user } = useApp()

  // Permissions & assignment check
  const [assignments, setAssignments] = useState<OperatorAssignment[]>([])
  const [hasOverridePermission, setHasOverridePermission] = useState(true) // default true for operator unless assignments loaded

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

  // Fetch operator assignments to check granular permissions (SLOT_OVERRIDE)
  useEffect(() => {
    let active = true
    operatorsApi
      .assignments()
      .then((res) => {
        if (!active) return
        setAssignments(res)
        if (lot && res.length > 0) {
          const siteAssignment = res.find((a) => a.siteId === lot.id)
          if (siteAssignment) {
            setHasOverridePermission(
              siteAssignment.permissions.includes("SLOT_OVERRIDE") ||
              siteAssignment.permissions.includes("ADMIN") ||
              user?.role === "owner",
            )
          }
        }
      })
      .catch(() => {
        // If API fails or in local preview, standard operator has default override rights
        if (active) setHasOverridePermission(true)
      })

    return () => {
      active = false
    }
  }, [lot?.id, user])

  // Get structure data from structureStore
  const structure = useMemo(() => {
    if (!lot) return null
    try {
      return structureStore.getStructure(lot.id)
    } catch {
      return null
    }
  }, [lot])

  // Get all spaces across all floors and zones
  const allSpaces = useMemo(() => {
    if (!structure?.floors) return []
    return structure.floors.flatMap((f) => f.zones).flatMap((z) => z.spaces)
  }, [structure])

  // Current backup slots
  const backupSlots = useMemo(() => {
    if (!lot) return []
    // Combine structureStore spaces marked as isBackup with lot slots
    return allSpaces.filter((s) => s.isBackup || s.reservationState === "BACKUP")
  }, [allSpaces, lot])

  // Slots eligible for being designated as backup:
  // Must be AVAILABLE, not physically occupied, not reserved, operational
  const eligibleSlots: OperationalSpaceItem[] = useMemo(() => {
    if (!lot) return []
    if (allSpaces.length > 0) {
      return allSpaces
        .filter(
          (s) =>
            !s.isBackup &&
            s.reservationState !== "BACKUP" &&
            (s.status === "available" || s.physicalState === "AVAILABLE") &&
            s.physicalState !== "OCCUPIED" &&
            s.physicalState !== "MAINTENANCE" &&
            s.physicalState !== "UNAVAILABLE",
        )
        .map((s) => ({
          id: s.id,
          code: s.code,
          vehicleType: s.vehicleType,
        }))
    }
    return lot.slots
      .filter((s) => s.status === "available")
      .map((s) => ({
        id: s.id,
        code: s.number,
        vehicleType: s.number.startsWith("M") ? ("motorcycle" as const) : ("car" as const),
      }))
  }, [allSpaces, lot])

  // Designated backup capacity percentage from owner policy (read-only)
  const backupPolicyPercentage = structure?.policy?.backupCapacityPercentage ?? 10
  const totalSlots = lot?.totalSlots ?? 0
  const maxRecommendedBackup = Math.round((totalSlots * backupPolicyPercentage) / 100)

  // Handle Mark Slot as Backup
  async function handleDesignateBackup(e: React.FormEvent) {
    e.preventDefault()
    if (!lot || !selectedSlotId || !designateReason.trim() || busy) return

    setBusy(true)
    setFeedback(null)

    try {
      // 1. Try calling backend API
      try {
        await request(
          `/${lot.id}/slots/${selectedSlotId}/backup`,
          "POST",
          { reason: designateReason.trim() },
          "/api/parking-lots",
          server,
        )
      } catch {
        // Backend might be mock or offline; proceed to sync local store
      }

      // 2. Update structureStore
      structureStore.updateSpaceStatus(
        lot.id,
        selectedSlotId,
        "available",
        designateReason.trim(),
        {
          reservationState: "BACKUP",
          isBackup: true,
        },
      )

      // 3. Update main store audit log
      store.addAuditLog({
        userId: user?.id ?? "op",
        userName: user?.name ?? "Operator",
        userRole: user?.role ?? "operator",
        action: "BACKUP_SLOT_DESIGNATED",
        details: `Slot ${selectedSlotId} designated as Backup Capacity. Reason: ${designateReason.trim()}`,
      })

      setFeedback({
        type: "success",
        message: `Slot successfully designated as Backup. It is now reserved for emergency and overflow allocation.`,
      })

      setSelectedSlotId("")
      setDesignateReason("")
    } catch (err) {
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
    if (!lot || busy) return
    setBusy(true)
    setFeedback(null)

    try {
      // 1. Update structureStore
      structureStore.updateSpaceStatus(
        lot.id,
        spaceId,
        "available",
        undefined,
        {
          reservationState: "NONE",
          isBackup: false,
        },
      )

      // 2. Audit log
      store.addAuditLog({
        userId: user?.id ?? "op",
        userName: user?.name ?? "Operator",
        userRole: user?.role ?? "operator",
        action: "BACKUP_SLOT_RELEASED",
        details: `Backup designation released for slot ${spaceId}. Returned to vacant pool.`,
      })

      setFeedback({
        type: "success",
        message: `Backup designation released. The slot is now available for regular bookings.`,
      })
      setReleasingSlot(null)
    } catch (err) {
      setFeedback({
        type: "error",
        message: err instanceof Error ? err.message : "Failed to release backup slot.",
      })
    } finally {
      setBusy(false)
    }
  }

  if (!lot) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-sm text-[var(--muted)]">
        No active parking site selected.
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Title */}
      <div>
        <div className="flex items-center gap-2">
          <span className="rounded bg-purple-500/10 px-2 py-0.5 text-xs font-semibold text-purple-600">
            SRS 3.4.5 Capacity Management
          </span>
          <span className="text-xs text-[var(--muted)]">·</span>
          <span className="text-xs text-[var(--muted)]">{lot.name}</span>
        </div>
        <h1 className="mt-1 text-2xl font-bold tracking-tight text-[var(--fg)]">
          Backup Slot & Capacity Operations
        </h1>
        <p className="mt-1 text-sm text-[var(--muted)]">
          Designate and manage operational backup spaces for high-demand buffer, VIP allocations, and emergency overflow.
        </p>
      </div>

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

      {/* SECTION 1: Policy & Threshold Cards */}
      <div className="grid gap-4 sm:grid-cols-3">
        {/* Card 1: Currently Designated */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 flex flex-col justify-between">
          <div className="flex items-center justify-between text-xs text-[var(--muted)]">
            <span>Currently Designated</span>
            <UntitledIcon name="shield" size={16} />
          </div>
          <div className="mt-2">
            <div className="text-2xl font-bold text-purple-600">
              {backupSlots.length} slots
            </div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">
              Active buffer spaces
            </div>
          </div>
        </div>

        {/* Card 2: Configured Policy Target */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 flex flex-col justify-between">
          <div className="flex items-center justify-between text-xs text-[var(--muted)]">
            <span>Owner Target Policy</span>
            <UntitledIcon name="building" size={16} />
          </div>
          <div className="mt-2">
            <div className="text-2xl font-bold text-[var(--fg)]">
              {backupPolicyPercentage}% ({maxRecommendedBackup} slots)
            </div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">
              Read-only owner configuration
            </div>
          </div>
        </div>

        {/* Card 3: Eligible Available Pool */}
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 flex flex-col justify-between">
          <div className="flex items-center justify-between text-xs text-[var(--muted)]">
            <span>Eligible Pool</span>
            <UntitledIcon name="check-circle" size={16} />
          </div>
          <div className="mt-2">
            <div className="text-2xl font-bold text-green-600">
              {eligibleSlots.length} slots
            </div>
            <div className="mt-0.5 text-[11px] text-[var(--muted)]">
              Vacant & ready for designation
            </div>
          </div>
        </div>
      </div>

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
              <label className="block text-xs font-semibold text-[var(--fg)] mb-1">
                Select Vacant Operational Slot:
              </label>
              <select
                className="w-full rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-2 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] font-mono font-semibold"
                value={selectedSlotId}
                disabled={!hasOverridePermission || busy || eligibleSlots.length === 0}
                onChange={(e) => setSelectedSlotId(e.target.value)}
                required
              >
                <option value="">-- Choose an eligible space --</option>
                {eligibleSlots.map((s) => (
                  <option key={s.id} value={s.id}>
                    Slot {s.code} ({s.vehicleType === "motorcycle" ? "Motorcycle" : "Car"})
                  </option>
                ))}
              </select>
              {eligibleSlots.length === 0 && (
                <p className="mt-1 text-[11px] text-amber-600">
                  No vacant spaces currently available to designate.
                </p>
              )}
            </div>

            {/* Reason Input */}
            <div>
              <label className="block text-xs font-semibold text-[var(--fg)] mb-1">
                Designation Reason / Purpose (Required):
              </label>
              <input
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
              disabled={!hasOverridePermission || !selectedSlotId || !designateReason.trim() || busy}
              className="btn-primary text-xs py-2 px-4 bg-purple-600 hover:bg-purple-700 disabled:opacity-50"
            >
              <UntitledIcon name="shield" size={14} />
              {busy ? "Designating..." : "Confirm Backup Designation"}
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
          <span className="text-xs font-mono bg-purple-500/10 text-purple-600 px-2.5 py-1 rounded font-bold">
            BUFFER ACTIVE
          </span>
        </div>

        <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
          <table className="w-full text-left text-xs min-w-[650px]">
            <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
              <tr>
                <th className="px-4 py-3">Slot Code</th>
                <th className="px-4 py-3">Vehicle Type</th>
                <th className="px-4 py-3">Physical State</th>
                <th className="px-4 py-3">Designation Purpose</th>
                <th className="px-4 py-3">Protection Window</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {backupSlots.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-[var(--muted)]">
                    No slots are currently designated as backup in this facility.
                  </td>
                </tr>
              ) : (
                backupSlots.map((space) => (
                  <tr key={space.id} className="hover:bg-[var(--card)]/50 transition">
                    <td className="px-4 py-3 font-mono font-bold text-[var(--fg)]">
                      <span className="bg-[var(--card)] px-2 py-0.5 rounded border border-[var(--border)]">
                        {space.code}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium capitalize">
                      {space.vehicleType}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-green-500/10 px-2 py-0.5 text-[10px] font-bold text-green-600 uppercase">
                        {space.physicalState || "AVAILABLE"}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-[var(--fg)] max-w-xs truncate">
                      {space.maintenanceNote || "Standard operational backup buffer"}
                    </td>
                    <td className="px-4 py-3 text-[var(--muted)]">
                      {space.lastUpdated ? new Date(space.lastUpdated).toLocaleDateString() : "Active"}
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
              <div className="rounded-xl bg-purple-500/10 p-3 text-purple-600">
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
                  {releasingSlot.code}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-[var(--muted)]">Facility:</span>
                <span>{lot.name}</span>
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
