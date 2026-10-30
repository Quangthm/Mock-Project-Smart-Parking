import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { ParkingStatus, type ParkingStatusKind } from "../../../components/ui/ParkingStatus"
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
    scanSlots, isScanning, lastScanAt,
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
      <PageHeader title="Parking Structure & Slot Monitoring" eyebrow={<>{isOutdoor ? "Single-Level Facility" : `Multi-Level Facility (${lot.floors} Floors)`} · {lot.totalSlots} total slots</>} description="Live digital twin, floor navigation, and real-time physical space supervision." />

      {/* Mini Stat Summary */}
      <div className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-sm font-bold text-[var(--fg)]">Slot Status</h2>
            <p className="text-xs text-[var(--muted)]">
              AI demo scan assigns a recent check-in image to one unassigned open slot.
            </p>
          </div>
          <div className="flex items-center gap-2">
            {lastScanAt && (
              <span className="hidden text-[11px] text-[var(--muted)] sm:inline">
                Last scan {new Date(lastScanAt).toLocaleTimeString()}
              </span>
            )}
            <button type="button" className="btn-primary flex items-center gap-1.5 text-xs py-1.5 px-3" onClick={scanSlots} disabled={isScanning}>
              <UntitledIcon name="scan" size={14} />
              {isScanning ? "AI scanning..." : "Scan now"}
            </button>
          </div>
        </div>
        <div className="parking-kpi-strip">
          {([
          ["available", availableCount],
          ["occupied", occupiedCount],
          ["reserved", reservedCount],
          ["maintenance", maintenanceCount],
          ] as [ParkingStatusKind, number][]).map(([status, count]) => (
            <div className="parking-kpi" key={status}>
              <ParkingStatus status={status} />
              <strong>{count}</strong>
            </div>
          ))}
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
            <option value="maintenance">Maintenance / Disabled</option>
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

          <span className="ml-auto text-xs text-[var(--muted)]">
            Showing {filteredSlots.length} / {allSlots.length} slots
          </span>
        </div>
      </div>

      {/* 3D Digital Twin Viewer */}
      <div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-4 relative">
          {threeError ? (
            <div className="p-8 text-center text-xs text-[var(--muted)] space-y-3">
              <UntitledIcon name="alert" size={24} />
              <p>3D model asset could not be initialized. The 2D grid below remains available.</p>
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
                slots={allSlots}
                selectedSlotId={inspectedSlot?.id || inspectedSlot?.number}
                focusSlotId={searchQuery.trim() ? searchQuery.trim().toUpperCase() : null}
                onSelectSlot={(slot) => setInspectedSlot(allSlots.find((item) => item.id === slot.id) ?? null)}
                height={420}
                interactive={true}
              />
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center justify-end">
        <div className="flex items-center gap-1 rounded-lg border border-[var(--border)] bg-[var(--card)] p-1">
          <button
            type="button"
            className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition ${
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
            className={`flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition ${
              viewMode === "list"
                ? "bg-[var(--action)] text-[var(--action-fg)]"
                : "text-[var(--muted)] hover:text-[var(--fg)]"
            }`}
            onClick={() => setViewMode("list")}
          >
            <UntitledIcon name="clipboard" size={13} /> Slot List
          </button>
        </div>
      </div>

      {/* 2D Interactive Grid Map */}
      {viewMode === "grid" && <div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h3 className="font-bold text-sm text-[var(--fg)]">
                2D Schematic Map — {isOutdoor ? "Ground Zone Grid" : `Floor ${selectedFloor}`}
              </h3>
              <p className="text-xs text-[var(--muted)]">
                Space status is shown with a label and icon. Select a space for details.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <ParkingStatus status="available" compact />
              <ParkingStatus status="occupied" compact />
              <ParkingStatus status="reserved" compact />
              <ParkingStatus status="maintenance" compact />
            </div>
          </div>

          {filteredSlots.length === 0 ? (
            <div className="p-8 text-center text-xs text-[var(--muted)]">
              No parking spaces found matching the active filter.
            </div>
          ) : (
            <div className="grid grid-cols-4 sm:grid-cols-6 md:grid-cols-8 lg:grid-cols-12 gap-2">
              {filteredSlots.map((slot) => {
                const SLOT_STATUS_KIND_MAP: Record<string, ParkingStatusKind> = {
                  occupied: "occupied",
                  reserved: "reserved",
                  maintenance: "maintenance",
                  disabled: "maintenance",
                  available: "available",
                }
                const isSelected = inspectedSlot?.id === slot.id
                const status: ParkingStatusKind = SLOT_STATUS_KIND_MAP[slot.status] ?? "available"

                return (
                  <button
                    key={slot.id}
                    type="button"
                    onClick={() => setInspectedSlot(slot)}
                    className={`parking-space${isSelected ? " is-selected" : ""}`}
                    data-status={status}
                    aria-label={`Space ${slot.number}, ${status}, ${slot.vehicleType}`}
                    aria-pressed={isSelected}
                  >
                    <strong>{slot.number}</strong>
                    {slot.demoPlate && (
                      <span className="mt-1 block max-w-full truncate font-mono text-[9px] text-[var(--primary)]">
                        {slot.demoPlate}
                      </span>
                    )}
                    {slot.demoImageUrl && !slot.demoPlate && (
                      <span className="mt-1 block truncate text-[9px] text-[var(--primary)]">
                        Image ready
                      </span>
                    )}
                    <ParkingStatus status={status} compact />
                  </button>
                )
              })}
            </div>
          )}
        </div>
      </div>}

      {/* Detailed Slot Table */}
      {viewMode === "list" && <div>
        <div className="rounded-xl border border-[var(--border)] bg-[var(--card)] p-5">
          <div className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--bg)]">
            <table className="w-full text-left text-xs min-w-[650px]">
              <thead className="border-b border-[var(--border)] bg-[var(--card)] text-[var(--muted)] font-semibold">
                <tr>
                  <th className="px-4 py-3">Slot Code</th>
                  <th className="px-4 py-3">Level / Zone</th>
                  <th className="px-4 py-3">Vehicle Type</th>
                  <th className="px-4 py-3">AI Plate</th>
                  <th className="px-4 py-3">Parking Session</th>
                  <th className="px-4 py-3">Physical State</th>
                  <th className="px-4 py-3">Reservation State</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border)]">
                {filteredSlots.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="px-4 py-8 text-center text-[var(--muted)]">
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
                      <td className="px-4 py-3 font-mono font-semibold text-[var(--primary)]">
                        {slot.demoPlate ?? "Not scanned"}
                      </td>
                      <td className="px-4 py-3">
                        {slot.demoSession ? (
                          <div className="max-w-40">
                            <div className="truncate font-mono text-[11px] font-semibold text-[var(--fg)]">
                              {slot.demoSession.id}
                            </div>
                            <div className="truncate text-[10px] text-[var(--muted)]">
                              {slot.demoSession.ticketReference}
                            </div>
                          </div>
                        ) : slot.demoImageUrl ? (
                          <span className="text-[10px] text-[var(--primary)]">Waiting for scan</span>
                        ) : (
                          <span className="text-[var(--muted)]">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 font-semibold">
                        <span
                          className={`rounded px-2 py-0.5 text-[10px] uppercase ${
                            ({
                              occupied: "bg-red-500/10 text-red-600",
                              available: "bg-green-500/10 text-green-600",
                              maintenance: "bg-slate-500/10 text-slate-600",
                              disabled: "bg-slate-500/10 text-slate-600",
                            } as Record<string, string>)[slot.status] ?? "bg-slate-500/10 text-slate-600"
                          }`}
                        >
                          {({
                            occupied: "Physically Occupied",
                            maintenance: "Maintenance",
                            disabled: "Maintenance",
                          } as Record<string, string>)[slot.status] ?? "Vacant"}
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
      </div>}

      <SlotInspectionModal lot={lot} inspectedSlot={inspectedSlot} isOutdoor={isOutdoor} onClose={() => setInspectedSlot(null)} />
    </div>
  )
}
