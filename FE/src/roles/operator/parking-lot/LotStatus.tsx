import { useEffect, type CSSProperties } from "react"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { ParkingStatus, type ParkingStatusKind } from "../../../components/ui/ParkingStatus"
import { StaleDataBanner, STALE_DIM_OPACITY } from "../../../components/ui/StaleDataBanner"
import {
  SLOT_VISUAL_STATE_HEX,
  SLOT_VISUAL_STATE_LABELS,
} from "../../../lib/3d/parking3DConfig"
import {
  formatSlotLabel,
  type OperationalLayoutProjection,
  type PhysicalState,
  type ReservationState,
  type SlotVisualState,
} from "../../../lib/operationalLayout"
import { SlotInspectionModal } from "./SlotInspectionModal"
import type { OperatorSite } from "../../../lib/operatorApi"
import { useLotStatus, type LotVisualFilter } from "./useLotStatus"

interface LotStatusProps {
  lot: OperatorSite | null
  initialSlotCode?: string
  initialStatusFilter?: LotVisualFilter
  layout?: OperationalLayoutProjection | null
  onRefreshLayout?: () => Promise<OperationalLayoutProjection | null | void>
}

const KPI_ORDER: SlotVisualState[] = [
  "available",
  "occupied",
  "reserved",
  "backup",
  "maintenance",
  "unknown",
  "unavailable",
]

const PARKING_STATUS_KINDS: Partial<Record<SlotVisualState, ParkingStatusKind>> = {
  available: "available",
  occupied: "occupied",
  reserved: "reserved",
  maintenance: "maintenance",
}

const PHYSICAL_LABELS: Record<PhysicalState, string> = {
  AVAILABLE: "Available",
  OCCUPIED: "Occupied",
  UNKNOWN: "Unknown",
  MAINTENANCE: "Maintenance",
  UNAVAILABLE: "Unavailable",
}

const RESERVATION_LABELS: Record<ReservationState, string> = {
  UNRESERVED: "None",
  RESERVED: "Reserved",
  PROTECTED: "Protected",
  BACKUP: "Backup",
}

const RESERVATION_TONE: Record<ReservationState, string> = {
  UNRESERVED: "text-[var(--muted)]",
  RESERVED: "bg-[var(--status-reserved-bg)] text-[var(--reserved)]",
  PROTECTED: "bg-[var(--status-reserved-bg)] text-[var(--reserved)]",
  // BACKUP is painted from SLOT_VISUAL_STATE_HEX.backup so the list column, the
  // 2D grid, the 3D scene and the legend all use one cyan. The theme's
  // `--backup` token is a different, muted violet and would read as `unknown`.
  BACKUP: "",
}

/**
 * Inline tone for the reservation cell. Only BACKUP needs one: the other
 * reservation states are already covered by theme classes, and giving BACKUP a
 * class would need a CSS rule outside this module's ownership.
 */
function reservationToneStyle(state: ReservationState | null): CSSProperties | undefined {
  if (state !== "BACKUP") return undefined
  const hex = SLOT_VISUAL_STATE_HEX.backup
  return { color: hex, backgroundColor: `${hex}1f` }
}

function VisualStateChip({ state }: { state: SlotVisualState }) {
  const hex = SLOT_VISUAL_STATE_HEX[state]
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide"
      style={{ color: hex, backgroundColor: `${hex}1f` }}
      aria-label={SLOT_VISUAL_STATE_LABELS[state]}
    >
      <span
        aria-hidden="true"
        className="h-2 w-2 rounded-full"
        style={{ backgroundColor: hex }}
      />
      {SLOT_VISUAL_STATE_LABELS[state]}
    </span>
  )
}

function slotStateStyle(state: SlotVisualState): CSSProperties | undefined {
  // Theme CSS covers available/occupied/reserved/maintenance by `data-status`.
  // The remaining states — including `backup` — have no rule, so they are
  // painted inline from the shared hex vocabulary and stay recognisable at the
  // small tile size.
  if (state !== "unknown" && state !== "unavailable" && state !== "backup") return undefined
  const hex = SLOT_VISUAL_STATE_HEX[state]
  return { borderColor: hex, backgroundColor: `${hex}1f`, color: hex }
}

export function LotStatus({
  lot,
  initialSlotCode = "",
  initialStatusFilter = "all",
  layout: layoutProp,
  onRefreshLayout,
}: LotStatusProps) {
  const {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    site, allSlots, filteredSlots, counts, slotView, layout,
    isStale, staleAgeSeconds, isRefreshing, refreshLayout,
  } = useLotStatus(lot, initialSlotCode, initialStatusFilter, {
    layout: layoutProp,
    refreshLayout: onRefreshLayout,
  })

  // A filter can hide the bay that is currently being inspected. Drop the
  // selection instead of keeping an id that is no longer on screen.
  useEffect(() => {
    if (!inspectedSlot) return
    if (filteredSlots.some((slot) => slot.id === inspectedSlot.id)) return
    setInspectedSlot(null)
  }, [filteredSlots, inspectedSlot, setInspectedSlot])

  if (!site) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-sm text-[var(--muted)]">
        No active parking lot selected.
      </div>
    )
  }

  const dimStyle: CSSProperties | undefined = isStale
    ? { opacity: STALE_DIM_OPACITY }
    : undefined

  return (
    <div className="space-y-6">
      <PageHeader title="Parking Structure & Slot Monitoring" eyebrow={<>{isOutdoor ? "Single-Level Facility" : `Multi-Level Facility (${site.floors} Floors)`} · {site.totalSlots} total slots</>} description="Live digital twin, floor navigation, and real-time physical space supervision." actions={<div className="flex flex-wrap items-center gap-1 rounded-lg border border-[var(--border)] bg-[var(--card)] p-1">
          <button
            type="button"
            className={`flex min-h-[44px] grow items-center justify-center gap-1.5 whitespace-nowrap rounded-md px-2 py-1.5 text-xs font-semibold transition sm:min-h-0 sm:px-3 ${
              viewMode === "3d"
                ? "bg-[var(--action)] text-[var(--action-fg)]"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => {
              setViewMode("3d")
              setThreeError(false)
            }}
          >
            <UntitledIcon name="parking" size={15} /> 3D Digital Twin
          </button>
          <button
            type="button"
            className={`flex min-h-[44px] grow items-center justify-center gap-1.5 whitespace-nowrap rounded-md px-2 py-1.5 text-xs font-semibold transition sm:min-h-0 sm:px-3 ${
              viewMode === "grid"
                ? "bg-[var(--action)] text-[var(--action-fg)]"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => setViewMode("grid")}
          >
            <UntitledIcon name="grid" size={13} /> 2D Grid Map
          </button>
          <button
            type="button"
            className={`flex min-h-[44px] grow items-center justify-center gap-1.5 whitespace-nowrap rounded-md px-2 py-1.5 text-xs font-semibold transition sm:min-h-0 sm:px-3 ${
              viewMode === "list"
                ? "bg-[var(--action)] text-[var(--action-fg)]"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => setViewMode("list")}
          >
            <UntitledIcon name="clipboard" size={13} /> Slot List
          </button>
        </div>} />

      {isStale && (
        <div className="flex flex-wrap items-start gap-2">
          <StaleDataBanner ageSeconds={staleAgeSeconds} className="flex-1" />
          <button
            type="button"
            className="btn-outline text-xs py-1.5 px-3"
            onClick={() => void refreshLayout()}
            disabled={isRefreshing}
          >
            {isRefreshing ? "Refreshing…" : "Retry now"}
          </button>
        </div>
      )}

      {/* Mini Stat Summary — two-axis counts */}
      <div className="parking-kpi-strip transition-opacity duration-300" style={dimStyle} data-stale={isStale ? "true" : undefined}>
        {KPI_ORDER.map((state) => {
          const kind = PARKING_STATUS_KINDS[state]
          return (
            <div className="parking-kpi" key={state}>
              {kind ? <ParkingStatus status={kind} /> : <VisualStateChip state={state} />}
              <strong>{counts[state]}</strong>
            </div>
          )
        })}
      </div>

      {/* Navigation Filter Bar */}
      <div
        className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-3 transition-opacity duration-300"
        style={dimStyle}
        data-stale={isStale ? "true" : undefined}
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          {/* Level / Floor Selector: ONLY shown if NOT outdoor */}
          {!isOutdoor && site.floors > 1 ? (
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-semibold text-[var(--muted)]">Level:</span>
              <div className="flex gap-1">
                {Array.from({ length: site.floors }, (_, i) => i + 1).map((floor) => (
                  <button
                    key={floor}
                    type="button"
                    onClick={() => {
                      setSelectedFloor(floor)
                      setSelectedZone("all")
                    }}
                    className={`rounded-md px-2.5 py-1 text-xs font-bold transition ${
                      selectedFloor === floor
                        ? "bg-[var(--action)] text-[var(--action-fg)]"
                        : "bg-[var(--bg)] text-[var(--muted)] hover:text-[var(--fg)] border border-[var(--border)]"
                    }`}
                  >
                    Floor {floor}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className="flex items-center gap-1 text-xs font-semibold text-[var(--muted)]">
              <UntitledIcon name="map-pin" size={14} />
              <span>Ground Single-Level Facility</span>
            </div>
          )}

          {/* Search by Slot Code */}
          <div className="relative">
            <input
              className="rounded-lg border border-[var(--border)] bg-[var(--bg)] px-3 py-1.5 text-xs text-[var(--fg)] outline-none focus:border-[var(--primary)] uppercase font-mono w-44"
              aria-label="Find a parking space by code"
              placeholder="Find space (e.g. A-001)"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
        </div>

        {/* Secondary Zone & Status Filter Pills */}
        <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-[var(--border)] text-xs">
          <span className="text-[var(--muted)] font-medium">Filters:</span>

          {/* Vehicle Type */}
          <RoundedSelect
            className="rounded border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs text-[var(--fg)]"
            value={vehicleTypeFilter}
            onChange={(e) => setVehicleTypeFilter(e.target.value as "all" | "car" | "motorcycle")}
          >
            <option value="all">All Vehicle Types</option>
            <option value="car">Car</option>
            <option value="motorcycle">Motorcycle</option>
          </RoundedSelect>

          {/* Status dimension */}
          <RoundedSelect
            className="rounded border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs text-[var(--fg)]"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
          >
            <option value="all">All Status Dimensions</option>
            <option value="available">Vacant (Available)</option>
            <option value="occupied">Occupied (In-Use)</option>
            <option value="reserved">Reserved / Protected</option>
            <option value="backup">Designated Backup</option>
            <option value="maintenance">Maintenance</option>
            <option value="unknown">Unknown / Not Reporting</option>
            <option value="unavailable">Unavailable / Blocked</option>
          </RoundedSelect>

          {/* Zone Selector */}
          {availableZones.length > 0 && (
            <RoundedSelect
              className="rounded border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs text-[var(--fg)]"
              value={selectedZone}
              onChange={(e) => setSelectedZone(e.target.value)}
            >
              <option value="all">All Zones</option>
              {availableZones.map((z) => (
                <option key={z.id} value={z.name.slice(0, 1)}>
                  {z.name}
                </option>
              ))}
            </RoundedSelect>
          )}

          <span className="ml-auto flex flex-wrap items-center gap-2 text-xs text-[var(--muted)]">
            <span>Filters apply to the 3D map, grid and list.</span>
            <span>Showing {filteredSlots.length} / {allSlots.length} slots</span>
          </span>
        </div>
      </div>

      {/* VIEW MODE 1: 3D Digital Twin Viewer */}
      {viewMode === "3d" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 relative">
          {threeError ? (
            <div className="p-8 text-center text-xs text-[var(--muted)] space-y-3" style={dimStyle}>
              <UntitledIcon name="alert" size={24} />
              <p>3D model asset could not be initialized. Switched to fallback 2D grid.</p>
              <button
                type="button"
                className="btn-primary text-xs py-1.5 px-3"
                onClick={() => setViewMode("grid")}
              >
                Open 2D Grid Map
              </button>
            </div>
          ) : (
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold text-[var(--muted)]">
                  Three.js Real-time View · Click any slot mesh to inspect or highlight
                </span>
                <span className="text-[11px] text-[var(--primary)] font-mono">
                  {site.modelFileName || "Procedural Lot Engine"}
                </span>
              </div>
              <Parking3DViewer
                modelUrl={site.modelUrl}
                slots={filteredSlots}
                layout={layout}
                showStaleBanner={false}
                selectedSlotId={inspectedSlot?.id || inspectedSlot?.number}
                focusSlotId={searchQuery.trim() || null}
                onSelectSlot={(slot) => setInspectedSlot(filteredSlots.find((item) => item.id === slot.id) ?? null)}
                height={420}
                interactive={true}
              />
            </div>
          )}
        </div>
      )}

      {/* VIEW MODE 2: 2D Interactive Grid Map */}
      {viewMode === "grid" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 transition-opacity duration-300" style={dimStyle}>
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h3 className="font-bold text-sm text-[var(--fg)]">
                2D Schematic Map — {isOutdoor ? "Ground Zone Grid" : `Floor ${selectedFloor}`}
              </h3>
              <p className="text-xs text-[var(--muted)]">
                Physical and reservation states are shown separately. Select a space for details.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <ParkingStatus status="available" compact />
              <ParkingStatus status="occupied" compact />
              <ParkingStatus status="reserved" compact />
              <VisualStateChip state="backup" />
              <ParkingStatus status="maintenance" compact />
              <VisualStateChip state="unknown" />
              <VisualStateChip state="unavailable" />
            </div>
          </div>

          {filteredSlots.length === 0 ? (
            <div className="p-8 text-center text-xs text-[var(--muted)]">
              No parking spaces found matching the active filter.
            </div>
          ) : (
            <div className="grid grid-cols-4 sm:grid-cols-6 md:grid-cols-8 lg:grid-cols-12 gap-2">
              {filteredSlots.map((slot) => {
                const view = slotView(slot)
                const kind = PARKING_STATUS_KINDS[view.visualState]
                const isSelected = inspectedSlot?.id === slot.id
                const reservationLabel = view.reservationState
                  ? RESERVATION_LABELS[view.reservationState]
                  : "Unknown"

                return (
                  <button
                    key={slot.id}
                    type="button"
                    onClick={() => setInspectedSlot(slot)}
                    className={`parking-space${isSelected ? " is-selected" : ""}`}
                    data-status={view.visualState}
                    style={slotStateStyle(view.visualState)}
                    aria-label={`Space ${formatSlotLabel(slot.number)}, ${SLOT_VISUAL_STATE_LABELS[view.visualState]}, physical ${PHYSICAL_LABELS[view.physicalState]}, reservation ${reservationLabel}, ${slot.vehicleType}`}
                    aria-pressed={isSelected}
                  >
                    <strong>{formatSlotLabel(slot.number)}</strong>
                    {kind ? <ParkingStatus status={kind} compact /> : <VisualStateChip state={view.visualState} />}
                  </button>
                )
              })}
            </div>
          )}
        </div>
      )}

      {/* VIEW MODE 3: Detailed Slot Table */}
      {viewMode === "list" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5 transition-opacity duration-300" style={dimStyle}>
          <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
            <table className="w-full text-left text-xs min-w-[650px]">
              <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
                <tr>
                  <th className="px-4 py-3">Slot Code</th>
                  <th className="px-4 py-3">Level / Zone</th>
                  <th className="px-4 py-3">Vehicle Type</th>
                  <th className="px-4 py-3">Physical State</th>
                  <th className="px-4 py-3">Reservation State</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border)]">
                {filteredSlots.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-[var(--muted)]">
                      No slots found matching the criteria.
                    </td>
                  </tr>
                ) : (
                  filteredSlots.map((slot) => {
                    const view = slotView(slot)
                    const hex = SLOT_VISUAL_STATE_HEX[view.visualState]
                    const reservationLabel = view.reservationState
                      ? RESERVATION_LABELS[view.reservationState]
                      : "Unknown"

                    return (
                      <tr key={slot.id} className="hover:bg-[var(--card)]/50 transition">
                        <td className="px-4 py-3 font-mono font-bold text-[var(--fg)]">
                          {formatSlotLabel(slot.number)}
                        </td>
                        <td className="px-4 py-3 text-[var(--muted)]">
                          {isOutdoor ? "Ground Area" : `Floor ${slot.floor}`}
                        </td>
                        <td className="px-4 py-3 font-medium">
                          {slot.vehicleType}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className="rounded px-2 py-0.5 text-[10px] uppercase font-semibold"
                            style={{ color: hex, backgroundColor: `${hex}1f` }}
                          >
                            {PHYSICAL_LABELS[view.physicalState]}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={`rounded px-2 py-0.5 text-[10px] uppercase font-medium ${
                              view.reservationState
                                ? RESERVATION_TONE[view.reservationState]
                                : "text-[var(--muted)]"
                            }`}
                            style={reservationToneStyle(view.reservationState)}
                          >
                            {reservationLabel}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <button
                            type="button"
                            className="btn-outline text-[11px] py-1 px-2.5"
                            onClick={() => setInspectedSlot(slot)}
                          >
                            Inspect
                          </button>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <SlotInspectionModal lot={site} inspectedSlot={inspectedSlot} isOutdoor={isOutdoor} onClose={() => setInspectedSlot(null)} />
    </div>
  )
}
