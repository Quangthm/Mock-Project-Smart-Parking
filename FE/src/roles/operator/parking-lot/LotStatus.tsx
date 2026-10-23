import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { SlotInspectionModal } from "./SlotInspectionModal"
import type { OperatorSite } from "../../../lib/operatorApi"
import { useLotStatus, type SlotStatusFilter } from "./useLotStatus"

interface LotStatusProps {
  lot: OperatorSite | null
  initialSlotCode?: string
  initialStatusFilter?: SlotStatusFilter
}

export function LotStatus({
  lot,
  initialSlotCode = "",
  initialStatusFilter = "all",
}: LotStatusProps) {
  const {
    viewMode, setViewMode, selectedFloor, setSelectedFloor, selectedZone, setSelectedZone,
    vehicleTypeFilter, setVehicleTypeFilter, statusFilter, setStatusFilter, searchQuery, setSearchQuery,
    inspectedSlot, setInspectedSlot, threeError, setThreeError, isOutdoor, availableZones,
    allSlots, filteredSlots, availableCount, occupiedCount, reservedCount, maintenanceCount,
  } = useLotStatus(lot, initialSlotCode, initialStatusFilter)

  if (!lot) {
    return (
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-8 text-center text-sm text-[var(--muted)]">
        No active parking lot selected.
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Parking Structure & Slot Monitoring" eyebrow={<>{isOutdoor ? "Single-Level Facility" : `Multi-Level Facility (${lot.floors} Floors)`} · {lot.totalSlots} total slots</>} description="Live digital twin, floor navigation, and real-time physical space supervision." actions={<div className="flex items-center gap-1 rounded-lg border border-[var(--border)] bg-[var(--card)] p-1">
          <button
            type="button"
            className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition ${
              viewMode === "3d"
                ? "bg-[var(--primary)] text-white shadow-sm"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => {
              setViewMode("3d")
              setThreeError(false)
            }}
          >
            <span>🎮</span> 3D Digital Twin
          </button>
          <button
            type="button"
            className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition ${
              viewMode === "grid"
                ? "bg-[var(--primary)] text-white shadow-sm"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => setViewMode("grid")}
          >
            <UntitledIcon name="grid" size={13} /> 2D Grid Map
          </button>
          <button
            type="button"
            className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition ${
              viewMode === "list"
                ? "bg-[var(--primary)] text-white shadow-sm"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => setViewMode("list")}
          >
            <UntitledIcon name="clipboard" size={13} /> Slot List
          </button>
        </div>} />

      {/* Mini Stat Summary */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 flex items-center justify-between">
          <div>
            <span className="text-xs text-green-600 font-semibold block">Available</span>
            <span className="text-xl font-bold text-green-600">{availableCount}</span>
          </div>
          <span className="h-3 w-3 rounded-full bg-green-500" />
        </div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 flex items-center justify-between">
          <div>
            <span className="text-xs text-red-600 font-semibold block">Occupied</span>
            <span className="text-xl font-bold text-red-600">{occupiedCount}</span>
          </div>
          <span className="h-3 w-3 rounded-full bg-red-500" />
        </div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 flex items-center justify-between">
          <div>
            <span className="text-xs text-amber-600 font-semibold block">Reserved / Hold</span>
            <span className="text-xl font-bold text-amber-600">{reservedCount}</span>
          </div>
          <span className="h-3 w-3 rounded-full bg-amber-500" />
        </div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-3.5 flex items-center justify-between">
          <div>
            <span className="text-xs text-slate-500 font-semibold block">Maintenance</span>
            <span className="text-xl font-bold text-[var(--fg)]">{maintenanceCount}</span>
          </div>
          <span className="h-3 w-3 rounded-full bg-slate-400" />
        </div>
      </div>

      {/* Navigation Filter Bar */}
      <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          {/* Level / Floor Selector: ONLY shown if NOT outdoor */}
          {!isOutdoor && lot.floors > 1 ? (
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-semibold text-[var(--muted)]">Level:</span>
              <div className="flex gap-1">
                {Array.from({ length: lot.floors }, (_, i) => i + 1).map((floor) => (
                  <button
                    key={floor}
                    type="button"
                    onClick={() => {
                      setSelectedFloor(floor)
                      setSelectedZone("all")
                    }}
                    className={`rounded-md px-2.5 py-1 text-xs font-bold transition ${
                      selectedFloor === floor
                        ? "bg-[var(--primary)] text-white"
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
              placeholder="Find Slot (e.g. A-001)"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
        </div>

        {/* Secondary Zone & Status Filter Pills */}
        <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-[var(--border)] text-xs">
          <span className="text-[var(--muted)] font-medium">Filters:</span>

          {/* Vehicle Type */}
          <select
            className="rounded border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs text-[var(--fg)]"
            value={vehicleTypeFilter}
            onChange={(e) => setVehicleTypeFilter(e.target.value as "all" | "car" | "motorcycle")}
          >
            <option value="all">All Vehicle Types</option>
            <option value="car">Car (Ô tô)</option>
            <option value="motorcycle">Motorcycle (Xe máy)</option>
          </select>

          {/* Status dimension */}
          <select
            className="rounded border border-[var(--border)] bg-[var(--bg)] px-2 py-1 text-xs text-[var(--fg)]"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}
          >
            <option value="all">All Status Dimensions</option>
            <option value="available">Vacant (Available)</option>
            <option value="occupied">Occupied (In-Use)</option>
            <option value="reserved">Reserved / Protected</option>
            <option value="backup">Designated Backup</option>
            <option value="maintenance">Maintenance / Disabled</option>
          </select>

          {/* Zone Selector */}
          {availableZones.length > 0 && (
            <select
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
            </select>
          )}

          <span className="ml-auto text-xs text-[var(--muted)]">
            Showing {filteredSlots.length} / {allSlots.length} slots
          </span>
        </div>
      </div>

      {/* VIEW MODE 1: 3D Digital Twin Viewer */}
      {viewMode === "3d" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 relative">
          {threeError ? (
            <div className="p-8 text-center text-xs text-[var(--muted)] space-y-3">
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
                  {lot.modelFileName || "Procedural Lot Engine"}
                </span>
              </div>
              <Parking3DViewer
                modelUrl={lot.modelUrl}
                slots={lot.slots}
                selectedSlotId={inspectedSlot?.id || inspectedSlot?.number}
                focusSlotId={searchQuery.trim() ? searchQuery.trim().toUpperCase() : null}
                onSelectSlot={(slot) => setInspectedSlot(allSlots.find((item) => item.id === slot.id) ?? null)}
                height={420}
                interactive={true}
              />
            </div>
          )}
        </div>
      )}

      {/* VIEW MODE 2: 2D Interactive Grid Map */}
      {viewMode === "grid" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h3 className="font-bold text-sm text-[var(--fg)]">
                2D Schematic Map — {isOutdoor ? "Ground Zone Grid" : `Floor ${selectedFloor}`}
              </h3>
              <p className="text-xs text-[var(--muted)]">
                Color-coded occupancy matrix. Click any space badge to inspect telemetry.
              </p>
            </div>
            <div className="flex items-center gap-3 text-xs text-[var(--muted)]">
              <span className="flex items-center gap-1">
                <span className="h-2.5 w-2.5 rounded bg-green-500" /> Vacant
              </span>
              <span className="flex items-center gap-1">
                <span className="h-2.5 w-2.5 rounded bg-red-500" /> Occupied
              </span>
              <span className="flex items-center gap-1">
                <span className="h-2.5 w-2.5 rounded bg-amber-500" /> Reserved
              </span>
              <span className="flex items-center gap-1">
                <span className="h-2.5 w-2.5 rounded bg-purple-500" /> Backup
              </span>
            </div>
          </div>

          {filteredSlots.length === 0 ? (
            <div className="p-8 text-center text-xs text-[var(--muted)]">
              No parking spaces found matching the active filter.
            </div>
          ) : (
            <div className="grid grid-cols-4 sm:grid-cols-6 md:grid-cols-8 lg:grid-cols-12 gap-2">
              {filteredSlots.map((slot) => {
                const isSelected = inspectedSlot?.id === slot.id
                let bgClass = "bg-green-500/15 border-green-500/40 text-green-700 dark:text-green-400"
                if (slot.status === "occupied") {
                  bgClass = "bg-red-500/15 border-red-500/40 text-red-700 dark:text-red-400"
                } else if (slot.status === "reserved") {
                  bgClass = "bg-amber-500/15 border-amber-500/40 text-amber-700 dark:text-amber-400"
                } else if (slot.status === "maintenance" || slot.status === "disabled") {
                  bgClass = "bg-slate-500/15 border-slate-500/40 text-slate-500"
                }

                return (
                  <button
                    key={slot.id}
                    type="button"
                    onClick={() => setInspectedSlot(slot)}
                    className={`h-12 rounded-lg border text-xs font-mono font-bold flex flex-col items-center justify-center transition-all transform hover:scale-105 ${bgClass} ${
                      isSelected ? "ring-2 ring-[var(--primary)] shadow-md" : ""
                    }`}
                  >
                    <span>{slot.number}</span>
                    <span className="text-[9px] font-normal opacity-80">
                      {slot.vehicleType}
                    </span>
                  </button>
                )
              })}
            </div>
          )}
        </div>
      )}

      {/* VIEW MODE 3: Detailed Slot Table */}
      {viewMode === "list" && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
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
                  filteredSlots.map((slot) => (
                    <tr key={slot.id} className="hover:bg-[var(--card)]/50 transition">
                      <td className="px-4 py-3 font-mono font-bold text-[var(--fg)]">
                        {slot.number}
                      </td>
                      <td className="px-4 py-3 text-[var(--muted)]">
                        {isOutdoor ? "Ground Area" : `Floor ${slot.floor}`}
                      </td>
                      <td className="px-4 py-3 font-medium">
                        {slot.vehicleType}
                      </td>
                      <td className="px-4 py-3 font-semibold">
                        <span
                          className={`rounded px-2 py-0.5 text-[10px] uppercase ${
                            slot.status === "occupied"
                              ? "bg-red-500/10 text-red-600"
                              : slot.status === "available"
                                ? "bg-green-500/10 text-green-600"
                                : "bg-slate-500/10 text-slate-600"
                          }`}
                        >
                          {slot.status === "occupied" ? "Physically Occupied" : slot.status === "maintenance" ? "Maintenance" : "Vacant"}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`rounded px-2 py-0.5 text-[10px] uppercase font-medium ${
                            slot.status === "reserved"
                              ? "bg-amber-500/10 text-amber-600"
                              : "text-[var(--muted)]"
                          }`}
                        >
                          {slot.status === "reserved" ? "Reserved" : "None"}
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
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <SlotInspectionModal lot={lot} inspectedSlot={inspectedSlot} isOutdoor={isOutdoor} onClose={() => setInspectedSlot(null)} />
    </div>
  )
}
