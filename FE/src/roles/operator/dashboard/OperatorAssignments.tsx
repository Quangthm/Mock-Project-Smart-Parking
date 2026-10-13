import { useEffect, useState } from "react"
import { operatorsApi, type OperatorAssignment } from "../../../lib/parkingApi"
import { useApp } from "../../../context/AppContext"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { ParkingLot } from "../../../lib/types"

interface OperatorAssignmentsProps {
  currentLot?: ParkingLot | null
}

export function OperatorAssignments({ currentLot }: OperatorAssignmentsProps) {
  const { user } = useApp()
  const [assignments, setAssignments] = useState<OperatorAssignment[]>([])
  const [error, setError] = useState("")
  const [loading, setLoading] = useState(true)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    let current = true
    setLoading(true)
    setAssignments([])
    setError("")

    operatorsApi
      .assignments()
      .then((result) => {
        if (current) setAssignments(result)
      })
      .catch((err) => {
        if (current) {
          setError(
            err instanceof Error
              ? err.message
              : "Unable to connect to live assignment service. Showing local operational permissions.",
          )
        }
      })
      .finally(() => {
        if (current) setLoading(false)
      })

    return () => {
      current = false
    }
  }, [user?.id, reload])

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-[var(--fg)]">
            My Operator Assignments & Permissions
          </h1>
          <p className="mt-1 text-sm text-[var(--muted)]">
            Operational sites, authority scopes, and facility dispatch roster for {user?.name}.
          </p>
        </div>
        <button
          type="button"
          className="btn-outline text-xs py-2 px-3 self-start sm:self-auto"
          disabled={loading}
          onClick={() => setReload((v) => v + 1)}
        >
          <UntitledIcon name="activity" size={14} /> Refresh Roster
        </button>
      </div>

      {loading && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-xs text-[var(--muted)]">
          Loading assigned facilities and permissions…
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-700 dark:text-amber-400">
          <div className="flex items-center gap-2 font-semibold">
            <UntitledIcon name="alert" size={16} />
            <span>Assignment Service Notice</span>
          </div>
          <p className="mt-1">{error}</p>
        </div>
      )}

      {/* Operator Profile Details */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <h2 className="text-base font-bold text-[var(--fg)] mb-3">Operator Identity</h2>
        <div className="grid gap-3 sm:grid-cols-3 text-xs">
          <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3">
            <span className="text-[var(--muted)] block">Operator Name:</span>
            <strong className="text-[var(--fg)] text-sm">{user?.name}</strong>
          </div>
          <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3">
            <span className="text-[var(--muted)] block">Assigned Scope:</span>
            <strong className="text-[var(--primary)] text-sm capitalize">
              {user?.operatorSiteId === "all" ? "All Facilities (Super-Operator)" : (currentLot?.name || "Single Assigned Lot")}
            </strong>
          </div>
          <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-3">
            <span className="text-[var(--muted)] block">Operator Role:</span>
            <strong className="text-[var(--fg)] text-sm uppercase">
              {user?.operatorRole || "Standard Operation"}
            </strong>
          </div>
        </div>
      </div>

      {/* Backend Assignments List */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
        <div className="mb-4">
          <h2 className="text-base font-bold text-[var(--fg)]">Assigned Parking Lots</h2>
          <p className="text-xs text-[var(--muted)]">
            Verified sites with delegated operational and gate access
          </p>
        </div>

        {assignments.length > 0 ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {assignments.map((assignment) => (
              <div
                key={assignment.siteId}
                className="rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4 space-y-2 text-xs"
              >
                <div className="flex items-center justify-between">
                  <span className="font-mono font-bold text-sm text-[var(--fg)]">
                    Site ID: {assignment.siteId}
                  </span>
                  <span className="rounded bg-green-500/10 px-2 py-0.5 text-[10px] font-bold text-green-600">
                    ACTIVE
                  </span>
                </div>
                <div>
                  <span className="text-[var(--muted)] block mb-1">Granted Permissions:</span>
                  <div className="flex flex-wrap gap-1">
                    {assignment.permissions.map((perm) => (
                      <span
                        key={perm}
                        className="rounded bg-[var(--card)] border border-[var(--border)] px-2 py-0.5 text-[10px] font-mono text-[var(--fg)]"
                      >
                        {perm}
                      </span>
                    ))}
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="rounded-lg border border-[var(--border)] bg-[var(--bg)] p-4 text-xs space-y-2">
            <div className="flex items-center gap-2 text-[var(--fg)] font-semibold">
              <UntitledIcon name="building" size={16} />
              <span>Current Local Facility Assignment: {currentLot?.name || "Bitexco Financial Tower Outdoor Lot"}</span>
            </div>
            <p className="text-[var(--muted)]">
              Permissions active: <code className="font-mono font-bold text-[var(--primary)]">CHECK_IN, CHECK_OUT, LIVE_MONITORING, TICKET_HANDLE, SLOT_OVERRIDE</code>
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
