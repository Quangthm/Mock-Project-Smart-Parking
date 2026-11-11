import { useConfirmation } from "../../../components/ui/useConfirmation"
import { motion, useReducedMotion } from "motion/react"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react"
import {
  parkingApi,
  type AccessPath,
  type Site,
  type Slot,
  type Structure,
  type Unit,
} from "../../../lib/parkingApi"
import { BackupCapacityPanel } from "../dashboard/BackupCapacityPanel"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { PageHeader } from "../../../components/ui/PageHeader"
import { Badge } from "../../../components/ui/Badge"
import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { ErrorBoundary } from "../../../components/feedback/ErrorBoundary"
import { useApp } from "../../../context/AppContext"
import { useServerRefresh } from "../../../lib/serverRefresh"
import { showUiToast } from "../../../lib/uiToast"
import { ownerData as store } from "../data/data"
import type { LotType } from "../../../lib/types"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"
import { loadOwnerModelUrl } from "../../../lib/3d/ownerModelStorage"
import { useModelStructure } from "../../../lib/3d/modelSlotCapacity"
import { formatSlotLabel, mapSlotVisualState, type PhysicalState, type ReservationState } from "../../../lib/operationalLayout"
import { suggestSlotCode } from "../../../lib/3d/meshSlotMapping"
import { resolveActiveSiteId } from "../../../lib/structureRules"
import { AccessPathsPanel } from "./AccessPathsPanel"
import { CapacitySummary } from "./CapacitySummary"
import { RegisteredSlotsPanel } from "./RegisteredSlotsPanel"
import { ServiceAvailabilityPanel } from "./ServiceAvailabilityPanel"
import { SlotCompatibilityDialog } from "./SlotCompatibilityDialog"
import { SlotRegistrationPanel, type RegistrationMesh } from "./SlotRegistrationPanel"
import { StructureTree } from "./StructureTree"
import { structureErrorMessage } from "./StructureFeedback"

interface ParkingStructureProps {
  sites?: Site[]
  selectedSiteId?: string
  /**
   * Lets the parent follow the site the Owner picks in this screen's dropdown.
   * Kept optional so the screen still works when it is rendered on its own.
   */
  onSiteChange?: (siteId: string) => void
}

const inputClass = "input w-full text-sm"

// Stable empty defaults: a fresh `[]` per render would change the `modelSlots`
// memo identity on every render while the structure is still loading, which in
// turn re-arms the viewer's slot-sync effect and its census report.
const EMPTY_UNITS: Unit[] = []
const EMPTY_SLOTS: Slot[] = []
const EMPTY_PATHS: AccessPath[] = []

interface MeshCensus {
  registeredMeshes: number
  unregisteredMeshes: number
  unmatchedSlots: number
  unregistered: Array<{ key: string; name: string }>
}

const EMPTY_CENSUS: MeshCensus = {
  registeredMeshes: 0,
  unregisteredMeshes: 0,
  unmatchedSlots: 0,
  unregistered: [],
}

/**
 * Stable signature for a census report. The viewer publishes a brand-new object
 * on every model remap, so comparing the numbers and mesh keys is what makes the
 * update idempotent: an unchanged report must not push new state (and therefore
 * a new render) back into this screen.
 */
function censusSignature(census: MeshCensus): string {
  return [
    census.registeredMeshes,
    census.unregisteredMeshes,
    census.unmatchedSlots,
    census.unregistered.map((mesh) => `${mesh.key}\u0000${mesh.name}`).join("\u0001"),
  ].join("\u0002")
}

// Reservation appearances SPARK-228 keeps distinct from a normal reservation.
const RESERVATION_APPEARANCE_MAP: Record<string, { label: string; status: "reserved" }> = {
  PROTECTED: { label: "Protected", status: "reserved" },
  PENDING_PAYMENT: { label: "Pending payment", status: "reserved" },
}

const ACTIVE_OPERATIONAL_SET = new Set(["ACTIVE", "AVAILABLE", "OPERATIONAL"])

/**
 * Derives the two authoritative axes once, so the 3D model and the slot cards
 * cannot disagree. Both halves fail closed: an unrecognised operational status
 * is never AVAILABLE, and an unrecognised non-empty reservation is a BACKUP hold
 * (mirrors the backend's OperationalStateMapping).
 */
function slotAxes(slot: Slot): { physicalState: PhysicalState; reservationState: ReservationState } {
  const physicalState: PhysicalState = slot.isPhysicallyOccupied ? "OCCUPIED"
    : slot.operationalStatus === "MAINTENANCE" ? "MAINTENANCE"
    : slot.operationalStatus === "UNKNOWN" ? "UNKNOWN"
    : slot.operationalStatus === "BLOCKED" ? "UNAVAILABLE"
    : ACTIVE_OPERATIONAL_SET.has(slot.operationalStatus) ? "AVAILABLE"
    : "UNKNOWN"

  const reservation = (slot.reservationState ?? "").trim().toUpperCase()
  const reservationState: ReservationState = reservation === "" || reservation === "NONE" ? "UNRESERVED"
    : reservation === "RESERVED" || reservation === "PROTECTED" || reservation === "BACKUP" ? reservation
    : "BACKUP"

  return { physicalState, reservationState }
}

function slotAppearance(slot: Slot) {
  const { physicalState, reservationState } = slotAxes(slot)
  const visualState = mapSlotVisualState({ physicalState, reservationState })

  if (slot.isPhysicallyOccupied) {
    return slot.reservationState === "PROTECTED"
      ? { label: "Occupied + protected", status: "occupied" as const }
      : { label: "Occupied", status: "occupied" as const }
  }
  if (slot.reservationState && slot.reservationState !== "NONE") {
    const mapped = RESERVATION_APPEARANCE_MAP[slot.reservationState]
    if (mapped) return mapped
    // The shared mapper decides when a hold is a backup appearance: BACKUP only
    // shows as backup while the bay is physically AVAILABLE.
    if (visualState === "backup") return { label: "Backup", status: "backup" }
    return {
      label: slot.reservationState.replace(/_/g, " "),
      status: "reserved" as const,
    }
  }
  if (!ACTIVE_OPERATIONAL_SET.has(slot.operationalStatus)) {
    return {
      label: (slot.operationalStatus || "MAINTENANCE").replace(/_/g, " "),
      status: "maintenance" as const,
    }
  }
  return { label: "Available", status: "available" as const }
}

function categoriesOf(slot: Slot) {
  return slot.vehicleTypes && slot.vehicleTypes.length > 0
    ? slot.vehicleTypes
    : [slot.primaryVehicleType ?? slot.vehicleType].filter(Boolean)
}

export function ParkingStructure({ sites = [], selectedSiteId, onSiteChange }: ParkingStructureProps) {
  const { user } = useApp()
  const { confirm, confirmation } = useConfirmation()
  const [siteId, setSiteId] = useState("")
  const [structure, setStructure] = useState<Structure | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const [reload, setReload] = useState(0)
  const [savedModelUrl, setSavedModelUrl] = useState<string | null>(null)
  const [modelStorageReady, setModelStorageReady] = useState(false)
  // FE v2.3 (develop) collapsible 3D section, kept on merge. The SPARK-238 screen
  // renders its own viewer, so this only controls whether that section is shown.
  const [showModel, setShowModel] = useState(true)
  const [registerMode, setRegisterMode] = useState(false)
  const [registrationSelection, setRegistrationSelection] = useState<RegistrationMesh[]>([])
  const [census, setCensus] = useState<MeshCensus>(EMPTY_CENSUS)
  const [selectedSlotId, setSelectedSlotId] = useState<string | null>(null)
  const [compatibilitySlotId, setCompatibilitySlotId] = useState<string | null>(null)
  const [backupBusy, setBackupBusy] = useState(false)
  const [backgroundRevision, setBackgroundRevision] = useState(0)
  const [refreshing, setRefreshing] = useState(false)
  useServerRefresh(user?.id && siteId ? user.id + ":" + siteId : undefined, () => {
    if (!loading && !refreshing && !backupBusy && !registerMode && !compatibilitySlotId) setBackgroundRevision(value => value + 1)
  })
  const loadedSiteRef = useRef<string | null>(null)
  // Signature of the last (requested site + available site ids) pair the site effect
  // resolved. It lets the effect bail out — without touching state at all — when the
  // parent hands us a brand-new `sites` array with the same contents.
  const resolvedSiteSignatureRef = useRef<string | null>(null)

  // The site dropdown is authoritative. The parent's `selectedSiteId` is applied on
  // every change of the request OR of the available site set, so a site that was
  // requested before the server list included it is still honoured once it appears.
  // A background site-list refresh with identical contents produces the same
  // signature and returns early, so it can neither churn `siteId` nor reset state.
  useEffect(() => {
    const siteIds = sites.map((site) => site.id)
    const signature = `${selectedSiteId ?? ""}\u0000${siteIds.join("\u0001")}`
    if (resolvedSiteSignatureRef.current === signature) return
    resolvedSiteSignatureRef.current = signature
    setSiteId((current) => resolveActiveSiteId(selectedSiteId, current, siteIds))
  }, [selectedSiteId, sites])

  useEffect(() => {
    let active = true
    setSavedModelUrl(null)
    setModelStorageReady(false)
    loadOwnerModelUrl(siteId).then(url => { if (active) setSavedModelUrl(url) }).catch(() => {}).finally(() => { if (active) setModelStorageReady(true) })
    return () => { active = false }
  }, [siteId])

  useEffect(() => {
    let current = true
    const siteChanged = loadedSiteRef.current !== siteId
    if (siteChanged) {
      loadedSiteRef.current = siteId
      setActiveFloor("")
      setStructure(null)
      setSelectedSlotId(null)
      setCompatibilitySlotId(null)
      setRegisterMode(false)
      setRegistrationSelection([])
      setCensus(EMPTY_CENSUS)
    }
    setError("")
    if (!siteId) {
      setLoading(false)
      return () => { current = false }
    }
    if (siteChanged || !structure) setLoading(true)
    setRefreshing(true)
    parkingApi.structure(siteId)
      .then((data) => { if (current) setStructure(data) })
      .catch((cause) => { if (current) setError(structureErrorMessage(cause, "Cannot load parking structure.")) })
      .finally(() => { if (current) { setLoading(false); setRefreshing(false) } })
    return () => { current = false }
  }, [siteId, reload, backgroundRevision])

  const units = structure?.units ?? EMPTY_UNITS
  const slots = structure?.slots ?? EMPTY_SLOTS
  const paths = structure?.paths ?? EMPTY_PATHS
  const ownerLot = useMemo(() => store.getLotsByOwner(user?.id ?? "").find(lot => lot.id === siteId), [user?.id, siteId, reload])
  const unitById = useMemo(() => new Map(units.map((unit) => [unit.id, unit])), [units])

  const safeLotType: LotType =
    ownerLot?.type && ownerLot.type in ALLOWED_PARKING_MODELS
      ? (ownerLot.type as LotType)
      : "outdoor"
  const defaultModelPath = ALLOWED_PARKING_MODELS[safeLotType].path

  // One resolved model URL drives both the 3D viewer and the capacity upper bound in
  // the unit dialog, so the two can never disagree about which template is in use.
  const modelUrl =
    savedModelUrl ??
    (ownerLot?.modelUrl?.startsWith("blob:") ? undefined : ownerLot?.modelUrl) ??
    defaultModelPath

  const modelStructure = useModelStructure(modelUrl)

  const modelFloors = modelStructure.analysis?.floors ?? []
  const maxAllowedFloors = ownerLot?.floors
    ? Math.min(ownerLot.floors, modelFloors.length)
    : modelFloors.length
  const floors = useMemo(
    () => modelFloors.slice(0, Math.max(1, maxAllowedFloors)),
    [modelFloors, maxAllowedFloors],
  )

  const [activeFloor, setActiveFloor] = useState<string>("")

  useEffect(() => {
    if (floors.length > 0) {
      if (!activeFloor || (activeFloor !== "all" && !floors.some((f) => f.floorKey === activeFloor))) {
        setActiveFloor(floors[0].floorKey)
      }
    } else if (activeFloor) {
      setActiveFloor("")
    }
  }, [floors, activeFloor])

  // Auto-initialize floor units if site has no FLOOR units in structure tree
  const autoInitRef = useRef<Record<string, boolean>>({})

  useEffect(() => {
    if (!siteId || loading || !structure || structure.units.some((u) => u.type === "FLOOR")) return
    if (!modelStructure.analysis || modelStructure.analysis.floors.length === 0) return
    if (autoInitRef.current[siteId]) return
    autoInitRef.current[siteId] = true

    let active = true
    const init = async () => {
      const targetFloors = modelStructure.analysis!.floors.slice(
        0,
        ownerLot?.floors
          ? Math.min(ownerLot.floors, modelStructure.analysis!.floors.length)
          : modelStructure.analysis!.floors.length,
      )
      for (const fl of targetFloors) {
        if (!active) break
        try {
          await parkingApi.unit(siteId, {
            type: "FLOOR",
            name: fl.displayName,
            parentId: null,
            capacity: fl.capacity,
          })
        } catch (err) {
          console.warn("Auto-initializing floor unit failed:", err)
        }
      }
      if (active) refresh()
    }
    void init()
    return () => {
      active = false
    }
  }, [siteId, loading, structure, modelStructure.analysis, ownerLot?.floors])

  // The 3D viewer needs the two-axis projection, not only a single status, so a
  // BACKUP hold keeps its cyan appearance (SPARK-228). The single-axis `status`
  // stays inside the legacy vocabulary; `slotAxes` carries the reservation axis.
  const modelSlots = useMemo(
    () =>
      slots.map((slot) => ({
        id: slot.id,
        number: slot.code,
        floor: 1,
        status: slot.isPhysicallyOccupied
          ? "occupied" as const
          : !ACTIVE_OPERATIONAL_SET.has(slot.operationalStatus)
            ? "maintenance" as const
            : slot.reservationState && slot.reservationState !== "NONE"
              ? "reserved" as const
              : "available" as const,
        ...slotAxes(slot),
      })),
    [slots],
  )

  const selectedMeshKeySignature = registrationSelection.map((item) => item.key).join("|")

  const selectedMeshKeys = useMemo(
    () => (selectedMeshKeySignature ? selectedMeshKeySignature.split("|") : []),
    [selectedMeshKeySignature],
  )

  const selectedSlot = slots.find((slot) => slot.id === selectedSlotId) ?? null
  const compatibilitySlot = slots.find((slot) => slot.id === compatibilitySlotId) ?? null

  // Idempotent census sink. The viewer reports a new object on every remap; returning
  // the current state for an unchanged report makes React bail out, which is what
  // stops the remap -> report -> re-render feedback cycle.
  const applyCensus = useCallback((next: MeshCensus) => {
    setCensus((current) =>
      censusSignature(current) === censusSignature(next) ? current : next,
    )
  }, [])

  // Selecting a site is both a local change and a signal to the parent, so the
  // parent's `selectedSiteId` follows the Owner instead of fighting them.
  const handleSiteSelect = useCallback(
    (nextSiteId: string) => {
      setSiteId(nextSiteId)
      onSiteChange?.(nextSiteId)
    },
    [onSiteChange],
  )

  function location(unitId: string) {
    const names: string[] = []
    const seen = new Set<string>()
    let cursor: Unit | undefined = unitById.get(unitId)
    while (cursor && !seen.has(cursor.id)) {
      seen.add(cursor.id)
      names.unshift(cursor.name)
      cursor = cursor.parentId ? unitById.get(cursor.parentId) : undefined
    }
    return names.length ? names.join(" / ") : "Unknown location"
  }

  function toggleMesh(mesh: { key: string; name: string }) {
    setRegistrationSelection((current) => {
      if (current.some((item) => item.key === mesh.key))
        return current.filter((item) => item.key !== mesh.key)
      return [...current, { key: mesh.key, name: mesh.name, code: suggestSlotCode(mesh.name) }]
    })
  }

  // A structural change only needs to re-read the STRUCTURE. It must not ask the
  // parent to reload its site list: that produced a new `sites` identity on every
  // edit and let the parent's refresh race the screen's own state.
  function refresh() {
    setReload((value) => value + 1)
  }

  // SPARK-228 backup mark/clear, carried into the P3 selected-slot card so the
  // capability survives without resurrecting the old slot modal.
  async function changeSelectedSlotBackup(slot: Slot) {
    if (backupBusy) return
    const marked = slot.reservationState === "BACKUP"
    if (!await confirm({ title: "Change backup designation?", description: `${marked ? "Release backup designation" : "Designate as backup"} for slot ${formatSlotLabel(slot.code)}?`, confirmLabel: "Confirm" })) return
    setBackupBusy(true)
    setError("")
    try {
      await parkingApi.edit(siteId, { action: "markBackup", resourceId: slot.id, active: !marked })
      showUiToast("Parking structure updated", marked ? "Backup designation released." : "Slot designated as backup.")
      refresh()
    } catch (cause) {
      const message = structureErrorMessage(cause, "Cannot change the backup designation.")
      setError(message)
      showUiToast("Structure not saved", message, "error")
    } finally {
      setBackupBusy(false)
    }
  }

  const reduceMotion = useReducedMotion()

  return (
    <section className="min-w-0 space-y-6 text-[var(--fg)]">
      {confirmation}
      <PageHeader
        title="Parking Structure"
        variant="compact"
        description="Inspect your site model, manage parking bays, and control access and availability."
        actions={
          <label className="min-w-0 space-y-1 text-xs font-medium text-[var(--muted)] sm:min-w-56">
            <span>Site</span>
            <RoundedSelect className={inputClass} value={siteId} onChange={(event) => handleSiteSelect(event.target.value)} disabled={!sites.length}>
              {!sites.length && <option value="">No sites available</option>}
              {sites.map((site) => <option key={site.id} value={site.id}>{site.name}</option>)}
            </RoundedSelect>
          </label>
        }
      />

      {error && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-700 dark:text-red-300">
          <span>{error}</span>
          {siteId && <button type="button" className="underline" onClick={() => setReload((value) => value + 1)}>Retry</button>}
        </div>
      )}
      {!siteId && <p className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">No parking sites are available for this account.</p>}
      {loading && !structure && <p role="status" className="rounded-xl border border-[var(--border)] p-6 text-sm text-[var(--muted)]">Loading parking structure…</p>}

      {structure && (
        <Fragment key={siteId}>
          {loading && <p role="status" className="text-xs text-[var(--muted)]">Refreshing the structure after the last change…</p>}

          <section className="space-y-3" aria-label="Site capacity">
            <div className="min-w-0">
              <h2 className="break-words text-lg font-semibold">{structure.site.name}</h2>
              <p className="break-words text-sm text-[var(--muted)]">{structure.site.address}</p>
            </div>
            <CapacitySummary structure={structure} />
          </section>

          {!slots.length && (
            <p className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-xs text-amber-700 dark:text-amber-300">
              No live parking slots are registered for this site yet. Unregistered template meshes are inert: they carry
              no capacity and cannot be booked.
            </p>
          )}

          <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,2fr)_minmax(20rem,1fr)]">
            <div className="min-w-0 space-y-4">
              <section className="min-w-0 overflow-hidden rounded-2xl bg-[var(--card)] p-4 sm:p-5" aria-label="Owner parking 3D model">
                <div className="mb-3 flex flex-wrap items-end justify-between gap-2">
                  <div>
                    <h2 className="text-lg font-semibold">Site 3D Model</h2>
                    <p className="mt-1 text-sm text-[var(--muted)]">
                      {registerMode
                        ? "Registration mode: click unregistered template bays to add them to the selection."
                        : "Registered bays are interactive. Unregistered template bays stay inert."}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge tone={registerMode ? "warning" : "muted"}>
                      {registerMode ? "Selecting meshes" : "Live slots only"}
                    </Badge>
                    <button
                      type="button"
                      className="btn-outline min-h-11 text-sm"
                      aria-controls="owner-parking-model"
                      aria-expanded={showModel}
                      onClick={() => setShowModel((value) => !value)}
                    >
                      {showModel ? "Hide model" : "Show model"}
                    </button>
                  </div>
                </div>
                <motion.div id="owner-parking-model" hidden={!showModel} initial={false}
                  animate={{ opacity: showModel ? 1 : 0 }}
                  transition={{ duration: reduceMotion ? 0 : 0.2, ease: [0.23, 1, 0.32, 1] }}>
                  {floors.length > 1 && (
                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-[var(--border)] bg-[var(--raised)] px-3 py-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-xs font-semibold text-[var(--muted)]">Floor or area</span>
                        <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Model floor">
                          <button
                            type="button"
                            aria-pressed={activeFloor === "all"}
                            onClick={() => setActiveFloor("all")}
                            className={`flex items-center gap-1.5 min-h-11 rounded-lg px-3 text-sm font-medium transition-colors cursor-pointer ${
                              activeFloor === "all"
                                ? "bg-[var(--primary)] text-[var(--primary-fg)]"
                                : "border border-[var(--border)] bg-[var(--card)] text-[var(--fg)] hover:bg-[var(--bg)]"
                            }`}
                          >
                            <span>All floors</span>
                            <span
                              className={`rounded px-1.5 py-0.2 text-xs tabular-nums ${
                                activeFloor === "all" ? "text-[var(--primary-fg)]" : "bg-[var(--raised)] text-[var(--muted)]"
                              }`}
                            >
                              {floors.reduce((sum, f) => sum + f.capacity, 0)} slots
                            </span>
                          </button>
                          {floors.map((floor) => {
                            const isActive = activeFloor === floor.floorKey
                            return (
                              <button
                                key={floor.floorKey}
                                type="button"
                                aria-pressed={isActive}
                                onClick={() => setActiveFloor(floor.floorKey)}
                                className={`flex items-center gap-1.5 min-h-11 rounded-lg px-3 text-sm font-medium transition-colors cursor-pointer ${
                                  isActive
                                    ? "bg-[var(--primary)] text-[var(--primary-fg)]"
                                    : "border border-[var(--border)] bg-[var(--card)] text-[var(--fg)] hover:bg-[var(--bg)]"
                                }`}
                              >
                                <span>{floor.displayName.replace(/^Tầng\s+/u, "Floor ").replace(/^Khu vực ngoài trời$/u, "Outdoor area")}</span>
                                <span
                                  className={`rounded px-1.5 py-0.2 text-xs tabular-nums ${
                                    isActive ? "text-[var(--primary-fg)]" : "bg-[var(--raised)] text-[var(--muted)]"
                                  }`}
                                >
                                  {floor.capacity} slots
                                </span>
                              </button>
                            )
                          })}
                        </div>
                      </div>
                      <span className="text-[11px] text-[var(--muted)]">
                        {activeFloor === "all"
                          ? "Showing the full model."
                          : "Showing the selected floor only."}
                      </span>
                    </div>
                  )}
                  {!modelStorageReady ? (
                    <p role="status" className="rounded-xl border border-dashed border-[var(--border)] p-8 text-sm text-[var(--muted)]">Loading the saved Owner model…</p>
                  ) : (
                    <ErrorBoundary name="Site 3D Viewer">
                      <Parking3DViewer
                        key={siteId}
                        modelUrl={modelUrl}
                        lotType={safeLotType}
                        slots={modelSlots}
                        selectedSlotId={registerMode ? null : selectedSlotId}
                        onSelectSlot={(slot) => setSelectedSlotId(slot.id)}
                        registerMode={registerMode}
                        selectedMeshKeys={selectedMeshKeys}
                        onToggleMesh={toggleMesh}
                        onMeshCensus={applyCensus}
                        height={380}
                        activeFloor={activeFloor}
                      />
                    </ErrorBoundary>
                  )}
                  {!ownerLot && modelStorageReady && (
                    <p className="mt-2 text-[11px] text-[var(--muted)]">
                      No lot-specific model is bound to this site, so the default outdoor template is shown. The template
                      is a fixed deployment asset: its extra bays are unregistered and inert until you register them.
                    </p>
                  )}
                </motion.div>

                {selectedSlot && !registerMode && (
                  <div className="mt-3 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-3">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0 break-words">
                        <p className="text-sm font-semibold tabular-nums">{selectedSlot.code}</p>
                        <p className="mt-0.5 text-xs text-[var(--muted)]">{location(selectedSlot.unitId)}</p>
                        <div className="mt-1.5 flex flex-wrap items-center gap-1">
                          {categoriesOf(selectedSlot).map((category) => (
                            <Badge key={category} tone={category === (selectedSlot.primaryVehicleType ?? selectedSlot.vehicleType) ? "primary" : "muted"}>
                              {category}
                              {category === (selectedSlot.primaryVehicleType ?? selectedSlot.vehicleType) ? " · primary" : ""}
                            </Badge>
                          ))}
                          <Badge tone="muted">{selectedSlot.type}</Badge>
                          <Badge tone={slotAppearance(selectedSlot).status === "available" ? "success" : slotAppearance(selectedSlot).status === "occupied" ? "danger" : "warning"}>
                            {slotAppearance(selectedSlot).label}
                          </Badge>
                        </div>
                      </div>
                      <div className="flex flex-wrap gap-1.5">
                        <button type="button" className="btn-outline min-h-11 text-sm" onClick={() => setCompatibilitySlotId(selectedSlot.id)}>
                          Edit compatibility
                        </button>
                        <button
                          type="button"
                          className="btn-outline min-h-11 text-sm"
                          disabled={backupBusy || selectedSlot.isPhysicallyOccupied || !ACTIVE_OPERATIONAL_SET.has(selectedSlot.operationalStatus) || Boolean(selectedSlot.reservationState && !["NONE", "BACKUP"].includes(selectedSlot.reservationState))}
                          onClick={() => void changeSelectedSlotBackup(selectedSlot)}
                        >
                          {selectedSlot.reservationState === "BACKUP" ? "Release backup" : "Mark as backup"}
                        </button>
                        <button type="button" className="btn-ghost min-h-11 text-sm" onClick={() => setSelectedSlotId(null)}>
                          Clear
                        </button>
                      </div>
                    </div>
                  </div>
                )}
              </section>

            </div>
            <aside className="min-w-0" aria-label="Parking structure hierarchy">
              <StructureTree siteId={siteId} units={units} slots={slots} paths={paths} modelUrl={modelUrl} onChanged={refresh} />
            </aside>
          </div>

          <SlotRegistrationPanel
            siteId={siteId}
            units={units}
            registeredSlots={slots}
            registerMode={registerMode}
            onRegisterModeChange={setRegisterMode}
            selection={registrationSelection}
            onSelectionChange={setRegistrationSelection}
            census={census}
            onChanged={refresh}
            activeFloor={activeFloor}
          />

          <RegisteredSlotsPanel
            siteId={siteId}
            units={units}
            slots={slots}
            selectedSlotId={selectedSlotId}
            onSelect={(slotId) => { setSelectedSlotId(slotId); setRegisterMode(false) }}
            onEditCompatibility={(slot) => setCompatibilitySlotId(slot.id)}
            onChanged={refresh}
          />

          <section className="min-w-0 space-y-4" aria-label="Access and service settings">
            <h2 className="text-lg font-semibold">Access and Service</h2>
            <ServiceAvailabilityPanel siteId={siteId} structure={structure} onChanged={refresh} />
            <AccessPathsPanel siteId={siteId} units={units} paths={paths} onChanged={refresh} />

            <details className="group overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--card)]">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-4 marker:hidden hover:bg-[var(--raised)]">
                <span className="flex min-w-0 items-center gap-3">
                  <span className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-[var(--status-maintenance-bg)] text-[var(--maintenance)]">
                    <UntitledIcon name="shield" size={17} />
                  </span>
                  <span>
                    <strong className="block text-base">Capacity and backup settings</strong>
                    <span className="mt-0.5 block text-sm text-[var(--muted)]">Configure backup capacity; occupancy stays Operations-owned.</span>
                  </span>
                </span>
                <UntitledIcon name="chevron-down" size={16} className="shrink-0 text-[var(--muted)] transition-transform group-open:rotate-180 motion-reduce:transition-none" />
              </summary>
              <div className="bg-[var(--bg)] p-4">
                <BackupCapacityPanel structure={structure} onChanged={refresh} />
              </div>
            </details>
          </section>
        </Fragment>
      )}

      {compatibilitySlot && (
        <SlotCompatibilityDialog
          key={compatibilitySlot.id}
          siteId={siteId}
          slot={compatibilitySlot}
          onClose={() => setCompatibilitySlotId(null)}
          onChanged={refresh}
        />
      )}
    </section>
  )
}
