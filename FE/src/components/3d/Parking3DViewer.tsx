import React, { useEffect, useMemo, useRef, useState, useCallback } from "react"

import * as THREE from "three"

import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js"

import { loadGltfWithCache } from "../../lib/3d/gltfModelCache"

import type { LotType, ParkingSlot } from "../../lib/types"

import {
  formatSlotLabel,
  isLayoutStale,
  layoutAgeSeconds,
  type OperationalLayoutProjection,
  type PhysicalState,
  type ReservationState,
  type SlotVisualState,
  type SlotVisualStateDto,
} from "../../lib/operationalLayout"

import { UntitledIcon } from "../icon/UntitledIcon"
import { StaleDataBanner, STALE_DIM_OPACITY } from "../ui/StaleDataBanner"

import {
  ALLOWED_PARKING_MODELS,
  SLOT_VISUAL_STATE_HEX,
  SLOT_VISUAL_STATE_LABELS,
  inferLotType,
} from "../../lib/3d/parking3DConfig"
import {
  describeBindingDiagnostics,
  isObjectVisible,
  applyFloorIsolation,
  type UnregisteredMeshRef,
} from "../../lib/3d/floorIsolation"

// SPARK-228 raw-exact contract matcher: the sole source of operational-overlay state.
import {
  mapModelSlots,
  findSlotMesh,
  normalizeSlotIdentifier,
  SlotMatchResult,
} from "../../lib/3d/slotMatcher"

// SPARK-238 tolerant matcher: the sole source of registration/interactivity.
import {
  mapRegisteredMeshes,
  findRegisteredSlotMesh,
  type RegisteredMeshMapping,
} from "../../lib/3d/registeredMeshMapping"

import {
  resolveSlotVisualState,
  SlotMaterialManager,
} from "../../lib/3d/slotMaterialManager"

import { CameraController } from "../../lib/3d/cameraController"

import { generateProceduralParkingLot } from "../../lib/3d/proceduralLotGenerator"

type ViewerSlotData = ParkingSlot & {
  physicalState?: PhysicalState | null
  reservationState?: ReservationState | null
}

/**
 * A mesh with no bound slot on the decision's own mapping is not evidence of a
 * free bay. It fails closed to the `unknown` appearance (A4).
 *
 * The bound slot is passed in explicitly so each caller feeds exactly one
 * mapping: the operational-overlay colour reads the SPARK-228 raw-exact binding,
 * while hover/pick reads the SPARK-238 tolerant binding. `mesh.userData` is never
 * consulted here, so the two matchers cannot contaminate each other's decision.
 */
function viewerSlotFor(
  mesh: THREE.Object3D,
  boundSlot: ParkingSlot | null | undefined,
): ViewerSlotData {
  const bound = boundSlot as ViewerSlotData | undefined

  // `mapModelSlots` once invented `slot-auto-*` records for meshes it could not
  // bind. Those are not evidence of a free bay, so they fail closed too.
  if (!bound || String(bound.id ?? "").startsWith("slot-auto-")) {
    return {
      id: mesh.name,
      number: mesh.name,
      floor: 1,
      status: "available",
      physicalState: "UNKNOWN",
      reservationState: "UNRESERVED",
    }
  }

  return bound
}

function withProjectedState(
  slotData: ViewerSlotData,
  projected: SlotVisualStateDto | undefined,
): ViewerSlotData {
  if (!projected) return slotData
  return {
    ...slotData,
    physicalState: projected.physicalState,
    reservationState: projected.reservationState,
  }
}

// Legend order mirrors the 2D KPI strip so the same reading order applies in
// every view. `backup` sits next to `reserved`: both are intentional holds, and
// the amber/cyan pair is the whole point of the state.
const SLOT_LEGEND_STATES: SlotVisualState[] = [
  "available",
  "occupied",
  "reserved",
  "backup",
  "maintenance",
  "unknown",
  "unavailable",
]

const BINDING_WARNING_TAG = "[3d]"

function logBindingDiagnostics(
  match: SlotMatchResult,
  slotCount: number,
  lastSignatureRef: { current: string | null },
): void {
  // SPARK-228's matcher reports the unmatched slot codes and unmatched mesh
  // names directly, so they are forwarded straight into the diagnostic shape; the
  // one-shot deduplication below is preserved unchanged.
  const diagnostics = describeBindingDiagnostics(match, slotCount)

  if (!diagnostics) {
    lastSignatureRef.current = null
    return
  }

  // Remapping identical data (e.g. a layout refresh that did not change the
  // slot list) must not repeat the same warning.
  if (diagnostics.signature === lastSignatureRef.current) return
  lastSignatureRef.current = diagnostics.signature

  const { signature: _signature, ...payload } = diagnostics
  console.warn(`${BINDING_WARNING_TAG} slot/mesh binding incomplete`, payload)
}

// Stable empty default: a fresh `[]` on every render would make the registration
// repaint effect run on unrelated renders (for example on every hover).
const NO_MESH_KEYS: string[] = []

export interface Parking3DViewerProps {
  modelUrl?: string

  lotType?: LotType

  slots?: ParkingSlot[]

  /**
   * Two-axis operational snapshot. When omitted the viewer keeps its previous
   * behaviour (legacy single-axis slots, no stale treatment). Passing `null`
   * means "snapshot expected but unavailable" and fails closed as stale.
   */
  layout?: OperationalLayoutProjection | null

  /** Suppress the in-viewer banner when the screen already announces staleness. */
  showStaleBanner?: boolean

  selectedSlotId?: string | null

  onSelectSlot?: (slot: ParkingSlot) => void

  focusSlotId?: string | null

  interactive?: boolean

  height?: string | number

  className?: string

  showLegend?: boolean

  showControls?: boolean

  onModelLoaded?: (info: {
    detectedSlotKeys: string[]
    isCustomGlb: boolean
    modelName: string
  }) => void

  /** Structure editing: select unregistered template meshes instead of claiming slots. */
  registerMode?: boolean

  selectedMeshKeys?: string[]

  onToggleMesh?: (mesh: UnregisteredMeshRef) => void

  onMeshCensus?: (info: {
    registeredMeshes: number
    unregisteredMeshes: number
    unmatchedSlots: number
    unregistered: UnregisteredMeshRef[]
  }) => void

  /** Floor grouping isolation: isolates and renders only the selected floor group. */
  activeFloor?: string | null
}

export function Parking3DViewer({
  modelUrl,

  lotType = "outdoor",

  slots = [],

  layout,

  showStaleBanner = true,

  selectedSlotId = null,

  onSelectSlot,

  focusSlotId = null,

  interactive = true,

  height = 420,

  className = "",

  showLegend = true,

  showControls = true,

  onModelLoaded,

  registerMode = false,

  selectedMeshKeys = NO_MESH_KEYS,

  onToggleMesh,

  onMeshCensus,

  activeFloor = null,
}: Parking3DViewerProps) {
  const resolvedLotType = useMemo(() => {
    if (lotType && lotType !== "outdoor") return lotType
    return inferLotType({
      floors: layout?.floors.length,
      slots: slots.map((s) => ({ slotCode: s.number })),
    })
  }, [lotType, layout, slots])

  const containerRef = useRef<HTMLDivElement>(null)

  const canvasRef = useRef<HTMLCanvasElement>(null)

  // Last logged binding signature, so identical remaps do not repeat warnings.
  const bindingWarningSignatureRef = useRef<string | null>(null)

  // Three.js instances ref

  const threeRef = useRef<{
    renderer: THREE.WebGLRenderer

    scene: THREE.Scene

    camera: THREE.PerspectiveCamera

    controls: OrbitControls

    cameraController: CameraController

    materialManager: SlotMaterialManager

    currentSceneContent: THREE.Object3D | null

    // SPARK-228 raw-exact binding: operational-overlay visual state only.
    overlayMapping: SlotMatchResult | null

    // SPARK-238 tolerant binding: registration, interactivity, census, A12 inert.
    registrationMapping: RegisteredMeshMapping | null

    animFrameId: number
  } | null>(null)

  const [loading, setLoading] = useState(true)

  const [loadingError, setLoadingError] = useState<string | null>(null)

  const [hoveredSlot, setHoveredSlot] = useState<{
    slot: ViewerSlotData
    visualState: SlotVisualState
    screenX: number
    screenY: number
  } | null>(null)

  const [hoveredMesh, setHoveredMesh] = useState<{
    name: string
    key: string
    selected: boolean
    screenX: number
    screenY: number
  } | null>(null)

  const [isFullscreen, setIsFullscreen] = useState(false)

  // Staleness is decided by the server threshold, never a hard-coded value.
  // `layout === undefined` means the caller does not use the operational
  // snapshot at all (e.g. the owner/driver viewers), so no stale treatment.
  const [now, setNow] = useState(() => Date.now())

  const staleEnabled = layout !== undefined
  const isStale = staleEnabled ? isLayoutStale(layout, now) : false
  const staleAgeSeconds = staleEnabled ? layoutAgeSeconds(layout, now) : null

  useEffect(() => {
    if (!staleEnabled) return
    setNow(Date.now())
    const id = window.setInterval(() => setNow(Date.now()), 5000)
    return () => window.clearInterval(id)
  }, [staleEnabled, layout])

  // Exact contract binding: `slotCode` is the raw mesh name (FR-MAP-03).
  const layoutSlots = useMemo(() => {
    const map = new Map<string, SlotVisualStateDto>()
    for (const slot of layout?.slots ?? []) {
      map.set(slot.slotCode, slot)
      if (slot.slotCode) {
        map.set(slot.slotCode.toUpperCase(), slot)
        map.set(slot.slotCode.toLowerCase(), slot)
        const norm = normalizeSlotIdentifier(slot.slotCode)
        if (norm) map.set(norm, slot)
      }
      map.set(slot.slotId, slot)
    }
    return map
  }, [layout])

  // Fullscreen listener

  useEffect(() => {
    const onFsChange = () => {
      setIsFullscreen(!!document.fullscreenElement)
    }

    document.addEventListener("fullscreenchange", onFsChange)

    return () => document.removeEventListener("fullscreenchange", onFsChange)
  }, [])

  const handleToggleFullscreen = () => {
    const container = containerRef.current

    if (!container) return

    if (!document.fullscreenElement) {
      container
        .requestFullscreen?.()
        .then(() => setIsFullscreen(true))
        .catch(() => {})
    } else {
      document
        .exitFullscreen?.()
        .then(() => setIsFullscreen(false))
        .catch(() => {})
    }
  }

  // Determine target model file URL

  const targetUrl = modelUrl || ALLOWED_PARKING_MODELS[resolvedLotType]?.path

  const expectedFileName =
    ALLOWED_PARKING_MODELS[resolvedLotType]?.fileName ||
    "outdoor_parking_lot.glb"

  // Apply slot statuses & highlights

  const refreshSlotVisuals = useCallback(() => {
    const inst = threeRef.current

    if (!inst || !inst.overlayMapping) return

    // Operational-overlay colouring is SPARK-228's decision. Iterate every raw
    // candidate mesh, not only formally bound ones: an unbound bay must still fail
    // closed to `unknown`, never keep its free-looking GLB material (A4). The bound
    // slot comes from the raw-exact mapping, so a mesh the tolerant matcher alone
    // binds has no operational state colour.
    const { allSlotMeshes } = inst.overlayMapping

    for (const mesh of allSlotMeshes) {
      if (!(mesh instanceof THREE.Mesh)) continue

      const slotData = viewerSlotFor(
        mesh,
        inst.overlayMapping.slotByObjectId.get(mesh.id) ??
          inst.registrationMapping?.slotByObjectId.get(mesh.id),
      )

      const projected =
        layoutSlots.get(mesh.name) ??
        layoutSlots.get(mesh.name.toUpperCase()) ??
        layoutSlots.get(normalizeSlotIdentifier(mesh.name)) ??
        layoutSlots.get(slotData.id) ??
        layoutSlots.get(slotData.number)

      const isSelected =
        !!selectedSlotId &&
        (slotData.id === selectedSlotId ||
          slotData.number === selectedSlotId ||
          mesh.name === selectedSlotId ||
          normalizeSlotIdentifier(slotData.number) ===
            normalizeSlotIdentifier(selectedSlotId))

      const isFocused =
        !!focusSlotId &&
        (slotData.id === focusSlotId ||
          slotData.number === focusSlotId ||
          mesh.name === focusSlotId ||
          normalizeSlotIdentifier(slotData.number) ===
            normalizeSlotIdentifier(focusSlotId))

      inst.materialManager.applySlotState(
        mesh,
        withProjectedState(slotData, projected),
        isSelected,
        isFocused,
      )
    }

    // Registration staging is SPARK-238's decision. Unregistered template meshes
    // never receive a live slot colour: while registering they may carry a
    // selection tint (the Owner explicitly asked to pick them), otherwise they are
    // restored to the model's own material so they stay visually inert (A12).
    const selected = new Set(selectedMeshKeysRef.current)
    for (const mesh of inst.registrationMapping?.unregisteredSlotMeshes ?? []) {
      if (!(mesh instanceof THREE.Mesh)) continue
      if (registerModeRef.current)
        inst.materialManager.applyRegistrationState(mesh, selected.has(mesh.uuid))
      else inst.materialManager.restoreOriginal(mesh)
    }
  }, [selectedSlotId, focusSlotId, layoutSlots])

  const lastFocusedKeyRef = useRef<string | null>(null)

  // Focus camera on slot

  const focusOnSlotKey = useCallback((targetSlotKey: string) => {
    const inst = threeRef.current

    if (!inst || !inst.registrationMapping) return

    if (lastFocusedKeyRef.current === targetSlotKey) return

    lastFocusedKeyRef.current = targetSlotKey

    const mesh = findRegisteredSlotMesh(
      inst.registrationMapping.meshBySlotKey,
      targetSlotKey,
    )

    if (mesh) {
      inst.cameraController.focusOnObject(mesh, inst.camera, inst.controls)
    }
  }, [])

  // Reset to overview

  const handleResetCamera = () => {
    const inst = threeRef.current

    if (!inst || !inst.currentSceneContent) return

    lastFocusedKeyRef.current = null

    inst.cameraController.resetToOverview(
      inst.currentSceneContent,
      inst.camera,
      inst.controls,
    )
  }

  // Zoom controls

  const handleZoom = (delta: number) => {
    const inst = threeRef.current

    if (!inst) return

    const { camera, controls } = inst

    const dir = new THREE.Vector3()
      .subVectors(controls.target, camera.position)
      .normalize()

    camera.position.addScaledVector(dir, delta * 3)

    controls.update()
  }

  // Setup Three.js scene

  useEffect(() => {
    const container = containerRef.current

    const canvas = canvasRef.current

    if (!container || !canvas) return

    const width = container.clientWidth || 600

    const heightPx =
      typeof height === "number" ? height : container.clientHeight || 420

    // Renderer

    const renderer = new THREE.WebGLRenderer({
      canvas,

      antialias: true,

      alpha: true,

      powerPreference: "high-performance",
    })

    renderer.setSize(width, heightPx)

    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))

    renderer.shadowMap.enabled = true

    renderer.shadowMap.type = THREE.PCFSoftShadowMap

    // Scene & Camera

    const scene = new THREE.Scene()

    scene.background = new THREE.Color(0x090d16)

    const camera = new THREE.PerspectiveCamera(45, width / heightPx, 0.5, 500)

    camera.position.set(22, 28, 30)

    // Controls

    const controls = new OrbitControls(camera, canvas)

    controls.enableDamping = true

    controls.dampingFactor = 0.05

    controls.maxPolarAngle = Math.PI / 2.05 // Prevent camera going below floor

    controls.minDistance = 3

    controls.maxDistance = 500

    controls.target.set(0, 0, 0)

    const cameraController = new CameraController()

    const materialManager = new SlotMaterialManager()

    threeRef.current = {
      renderer,

      scene,

      camera,

      controls,

      cameraController,

      materialManager,

      currentSceneContent: null,

      overlayMapping: null,

      registrationMapping: null,

      animFrameId: 0,
    }

    // Render loop

    let lastTime = 0

    const animate = (time: number) => {
      threeRef.current!.animFrameId = requestAnimationFrame(animate)

      const seconds = time * 0.001

      // Update camera smooth transition

      cameraController.update(camera, controls)

      controls.update()

      // Pulsing material for selected slot

      if (selectedSlotId && threeRef.current?.overlayMapping) {
        const mesh = findSlotMesh(
          threeRef.current.overlayMapping.meshBySlotKey,
          selectedSlotId,
        )

        if (mesh) {
          materialManager.tick(seconds, mesh.id)
        }
      }

      renderer.render(scene, camera)

      lastTime = time
    }

    threeRef.current.animFrameId = requestAnimationFrame(animate)

    // Resize observer

    const handleResize = () => {
      if (!container || !renderer || !camera) return

      const newWidth = container.clientWidth

      const newHeight =
        typeof height === "number" ? height : container.clientHeight || 420

      camera.aspect = newWidth / newHeight

      camera.updateProjectionMatrix()

      renderer.setSize(newWidth, newHeight)
    }

    const resizeObserver = new ResizeObserver(handleResize)

    resizeObserver.observe(container)

    // Cleanup

    return () => {
      resizeObserver.disconnect()

      if (threeRef.current) {
        cancelAnimationFrame(threeRef.current.animFrameId)

        threeRef.current.controls.dispose()

        threeRef.current.materialManager.dispose()

        threeRef.current.renderer.dispose()
      }

      threeRef.current = null
    }
  }, [])

  // Keep refs for dynamic props to prevent model re-loads on slot selection

  const slotsRef = useRef(slots)

  slotsRef.current = slots

  const onModelLoadedRef = useRef(onModelLoaded)

  onModelLoadedRef.current = onModelLoaded

  const registerModeRef = useRef(registerMode)

  registerModeRef.current = registerMode

  const onToggleMeshRef = useRef(onToggleMesh)

  onToggleMeshRef.current = onToggleMesh

  const onMeshCensusRef = useRef(onMeshCensus)

  onMeshCensusRef.current = onMeshCensus

  const selectedMeshKeysRef = useRef(selectedMeshKeys)

  selectedMeshKeysRef.current = selectedMeshKeys

  const loadedModelUrlRef = useRef<string | null>(null)

  const refreshSlotVisualsRef = useRef(refreshSlotVisuals)

  refreshSlotVisualsRef.current = refreshSlotVisuals

  const lastFramedFloorRef = useRef<string | null>(null)

  const reportCensus = useCallback(
    (mapping: RegisteredMeshMapping, slotCount: number) => {
      const visibleUnregistered = mapping.unregisteredSlotMeshes.filter((mesh) =>
        isObjectVisible(mesh),
      )
      onMeshCensusRef.current?.({
        registeredMeshes: mapping.allSlotMeshes.length,
        unregisteredMeshes: visibleUnregistered.length,
        unmatchedSlots: Math.max(0, slotCount - mapping.allSlotMeshes.length),
        unregistered: visibleUnregistered.map((mesh) => ({
          key: mesh.uuid,
          name: mesh.name || "Template mesh",
        })),
      })
    },
    [],
  )

  // Load Model (.glb file or procedural twin)

  useEffect(() => {
    const inst = threeRef.current

    if (!inst) return

    // Do not reload if this model URL is already loaded and present in the scene

    if (loadedModelUrlRef.current === targetUrl && inst.currentSceneContent) {
      return
    }

    setLoading(true)

    setLoadingError(null)

    // Remove old content

    if (inst.currentSceneContent) {
      inst.scene.remove(inst.currentSceneContent)

      inst.currentSceneContent = null

      lastFramedFloorRef.current = null
    }

    const applySceneContent = (
      loadedObject: THREE.Object3D,

      isGlb: boolean,

      resolvedName: string,
    ) => {
      inst.scene.add(loadedObject)

      inst.currentSceneContent = loadedObject

      loadedModelUrlRef.current = targetUrl || ""

      // Map slots using latest slots. Compose the two mappings: SPARK-228
      // (raw-exact) drives operational-overlay state, SPARK-238 (tolerant) drives
      // registration, pick and census. They are stored separately and never
      // collapsed, so each decision has exactly one source.
      const overlayMapping = mapModelSlots(loadedObject, slotsRef.current)

      const registrationMapping = mapRegisteredMeshes(
        loadedObject,
        slotsRef.current,
      )

      inst.overlayMapping = overlayMapping

      inst.registrationMapping = registrationMapping

      logBindingDiagnostics(
        overlayMapping,
        slotsRef.current.length,
        bindingWarningSignatureRef,
      )

      // Apply floor grouping isolation if an active floor is requested
      const activeTarget = applyFloorIsolation(loadedObject, activeFloor)

      reportCensus(registrationMapping, slotsRef.current.length)

      // Visuals

      refreshSlotVisualsRef.current()

      // Frame camera overview only on initial model load
      lastFramedFloorRef.current = activeFloor || ""
      inst.cameraController.resetToOverview(
        activeTarget || loadedObject,
        inst.camera,
        inst.controls,
      )

      if (onModelLoadedRef.current) {
        onModelLoadedRef.current({
          detectedSlotKeys: registrationMapping.detectedSlotKeys,

          isCustomGlb: isGlb,

          modelName: resolvedName,
        })
      }

      setLoading(false)
    }

    // Attempt to load GLB file from in-memory cache / singleton loader

    const cleanUrl = targetUrl

    if (cleanUrl) {
      loadGltfWithCache(cleanUrl)
        .then((modelScene) => {
          if (!inst) return

          // Add default lighting if model has no lights

          let hasLights = false

          modelScene.traverse((node) => {
            if (node instanceof THREE.Light) hasLights = true
          })

          if (!hasLights) {
            const ambient = new THREE.AmbientLight(0xffffff, 0.8)

            const directional = new THREE.DirectionalLight(0xffffff, 1.2)

            directional.position.set(20, 30, 25)

            inst.scene.add(ambient)

            inst.scene.add(directional)
          }

          applySceneContent(modelScene, true, expectedFileName)
        })
        .catch((err) => {
          if (!inst) return

          console.info(
            `[3D Viewer] Model file "${cleanUrl}" not loaded (${err instanceof Error ? err.message : String(err)}). Initializing procedural 3D model with matching naming convention (${expectedFileName}).`,
          )

          const proceduralLot = generateProceduralParkingLot(
            resolvedLotType,
            slotsRef.current,
            1,
          )

          applySceneContent(proceduralLot, false, expectedFileName)
        })
    } else {
      const proceduralLot = generateProceduralParkingLot(
        resolvedLotType,
        slotsRef.current,
        1,
      )

      applySceneContent(proceduralLot, false, expectedFileName)
    }
  }, [targetUrl, resolvedLotType, expectedFileName, reportCensus])

  // Sync slot data when slots array updates without reloading the 3D model

  useEffect(() => {
    const inst = threeRef.current

    if (!inst || !inst.currentSceneContent) return

    // The matcher writes bindings only for meshes it can match. Clear the old
    // ones so a site switch cannot keep the previous site's slot data.
    inst.currentSceneContent.traverse((node) => {
      if (node.userData?.isSlot) delete node.userData.slotData
    })

    const overlayMapping = mapModelSlots(inst.currentSceneContent, slots)

    const registrationMapping = mapRegisteredMeshes(inst.currentSceneContent, slots)

    inst.overlayMapping = overlayMapping

    inst.registrationMapping = registrationMapping

    logBindingDiagnostics(
      overlayMapping,
      slots.length,
      bindingWarningSignatureRef,
    )

    reportCensus(registrationMapping, slots.length)

    // Read the latest repaint callback through its ref instead of depending on it.
    // Selecting a slot or re-mapping the layout changes `refreshSlotVisuals`'s
    // identity; depending on it here would re-run the whole scene remap (and its
    // census report) for a change that only needs a repaint.
    refreshSlotVisualsRef.current()
  }, [slots, reportCensus])

  // Update visuals whenever selectedSlotId changes

  useEffect(() => {
    refreshSlotVisuals()
  }, [selectedSlotId, focusSlotId, refreshSlotVisuals])

  // Registration selection is owned by the structure screen; repaint the template tint
  // without re-mapping the scene.
  useEffect(() => {
    refreshSlotVisualsRef.current()
  }, [registerMode, selectedMeshKeys])

  // Active floor isolation & camera gliding effect
  useEffect(() => {
    const inst = threeRef.current
    if (!inst || !inst.currentSceneContent) return

    const activeTarget = applyFloorIsolation(inst.currentSceneContent, activeFloor)
    const effectiveFloor = activeFloor || ""
    if (lastFramedFloorRef.current !== effectiveFloor) {
      lastFramedFloorRef.current = effectiveFloor
      inst.cameraController.resetToOverview(
        activeTarget || inst.currentSceneContent,
        inst.camera,
        inst.controls,
      )
    }

    if (inst.registrationMapping) {
      reportCensus(inst.registrationMapping, slotsRef.current.length)
    }
    refreshSlotVisualsRef.current()
  }, [activeFloor, reportCensus])

  // Camera focus when focusSlotId or selectedSlotId changes

  useEffect(() => {
    const target = focusSlotId || selectedSlotId

    if (target) {
      focusOnSlotKey(target)
    }
  }, [focusSlotId, selectedSlotId, focusOnSlotKey])

  // Raycasting helpers

  const pickMesh = (
    event: React.PointerEvent<HTMLCanvasElement>,
    meshes: THREE.Object3D[],
  ): THREE.Mesh | null => {
    const inst = threeRef.current
    const canvas = canvasRef.current
    if (!inst || !canvas || meshes.length === 0) return null

    const rect = canvas.getBoundingClientRect()
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1
    const raycaster = new THREE.Raycaster()
    raycaster.setFromCamera(new THREE.Vector2(x, y), inst.camera)
    const intersects = raycaster.intersectObjects(meshes, true)
    const allowed = new Set(meshes)
    for (const hit of intersects) {
      if (!isObjectVisible(hit.object)) continue
      let target: THREE.Object3D | null = hit.object
      while (target) {
        if (target instanceof THREE.Mesh && allowed.has(target)) return target
        target = target.parent
      }
    }
    return null
  }

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!interactive) return

    const inst = threeRef.current

    if (!inst || !inst.registrationMapping) return

    // Structure editing: a click selects an unregistered template mesh. It never
    // claims a slot and never focuses the camera, so several bays can be picked.
    if (registerMode) {
      const mesh = pickMesh(e, inst.registrationMapping.unregisteredSlotMeshes)
      if (mesh) {
        onToggleMeshRef.current?.({ key: mesh.uuid, name: mesh.name || "Template mesh" })
      }
      return
    }

    const targetMesh = pickMesh(e, inst.registrationMapping.allSlotMeshes)

    if (targetMesh && targetMesh.userData.isSlot) {
      const slotData = viewerSlotFor(
        targetMesh,
        inst.registrationMapping.slotByObjectId.get(targetMesh.id),
      )

      const projected =
        layoutSlots.get(targetMesh.name) ??
        layoutSlots.get(targetMesh.name.toUpperCase()) ??
        layoutSlots.get(normalizeSlotIdentifier(targetMesh.name)) ??
        layoutSlots.get(slotData.id) ??
        layoutSlots.get(slotData.number)

      const visualSlot = withProjectedState(slotData, projected)

      if (onSelectSlot) {
        onSelectSlot(visualSlot)
      }

      lastFocusedKeyRef.current = visualSlot.id || visualSlot.number

      inst.cameraController.focusOnObject(
        targetMesh,
        inst.camera,
        inst.controls,
      )
    }
  }

  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const inst = threeRef.current

    if (!inst || !inst.registrationMapping) return

    const canvas = canvasRef.current

    if (!canvas) return

    const rect = canvas.getBoundingClientRect()

    if (registerMode) {
      const mesh = pickMesh(e, inst.registrationMapping.unregisteredSlotMeshes)
      setHoveredSlot(null)
      if (mesh) {
        setHoveredMesh({
          name: mesh.name || "Template mesh",
          key: mesh.uuid,
          selected: selectedMeshKeys.includes(mesh.uuid),
          screenX: e.clientX - rect.left,
          screenY: e.clientY - rect.top,
        })
        canvas.style.cursor = "pointer"
        return
      }
      setHoveredMesh(null)
      canvas.style.cursor = "grab"
      return
    }

    const targetMesh = pickMesh(e, inst.registrationMapping.allSlotMeshes)

    if (targetMesh && targetMesh.userData.isSlot) {
      const slotData = viewerSlotFor(
        targetMesh,
        inst.registrationMapping.slotByObjectId.get(targetMesh.id),
      )

      const projected =
        layoutSlots.get(targetMesh.name) ??
        layoutSlots.get(targetMesh.name.toUpperCase()) ??
        layoutSlots.get(normalizeSlotIdentifier(targetMesh.name)) ??
        layoutSlots.get(slotData.id) ??
        layoutSlots.get(slotData.number)

      const visualSlot = withProjectedState(slotData, projected)

      setHoveredMesh(null)
      setHoveredSlot({
        slot: visualSlot,
        visualState: resolveSlotVisualState(visualSlot),
        screenX: e.clientX - rect.left,
        screenY: e.clientY - rect.top,
      })
      canvas.style.cursor = interactive ? "pointer" : "default"
      return
    }

    setHoveredSlot(null)
    setHoveredMesh(null)
    canvas.style.cursor = "grab"
  }

  return (
    <div
      ref={containerRef}
      className={`relative w-full rounded-xl overflow-hidden border border-[var(--border)] bg-[#090d16] select-none ${className}`}
      style={{ height }}
    >
      <canvas
        ref={canvasRef}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerLeave={() => { setHoveredSlot(null); setHoveredMesh(null) }}
        className="w-full h-full block touch-none transition-opacity duration-300"
        style={{ opacity: isStale ? STALE_DIM_OPACITY : 1 }}
      />

      {/* Stale snapshot: the scene stays interactive, only brightness drops 30%. */}
      {isStale && showStaleBanner && (
        <StaleDataBanner
          ageSeconds={staleAgeSeconds}
          className="absolute left-3 top-3 right-20 sm:right-auto sm:max-w-md z-20 pointer-events-none shadow-lg"
        />
      )}

      {registerMode && (
        <div className="pointer-events-none absolute left-3 top-3 z-10 max-w-[min(22rem,calc(100%-5rem))] rounded-lg border border-amber-300/40 bg-[#151c1b]/90 px-3 py-2 text-[11px] leading-4 text-amber-100">
          <strong className="block text-xs text-white">Selecting unregistered bays</strong>
          Registered bays stay live and cannot be selected here. Picked template meshes are inert until you register them.
        </div>
      )}

      {/* Loading overlay */}
      {loading && (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-[#090d16]/85 backdrop-blur-xs z-10">
          <div className="w-9 h-9 border-3 border-[var(--primary)] border-t-transparent rounded-full animate-spin mb-3" />
          <p className="text-xs font-medium text-slate-300">
            Loading the 3D parking model...
          </p>
        </div>
      )}

      {/* Floating Controls */}
      {showControls && (
        <div className="absolute top-3 right-3 flex flex-col gap-1.5 z-10">
          <button
            type="button"
            onClick={handleResetCamera}
            title="Reset camera view"
            aria-label="Reset camera view"
            className="w-11 h-11 rounded-lg bg-black/75 hover:bg-black/95 text-white/90 border border-white/15 flex items-center justify-center text-xs transition-colors cursor-pointer"
          >
            <UntitledIcon name="target" size={18} />
          </button>
          <button
            type="button"
            onClick={() => handleZoom(1)}
            title="Zoom in"
            aria-label="Zoom in"
            className="w-11 h-11 rounded-lg bg-black/75 hover:bg-black/95 text-white/90 border border-white/15 flex items-center justify-center text-sm font-bold transition-colors cursor-pointer"
          >
            +
          </button>
          <button
            type="button"
            onClick={() => handleZoom(-1)}
            title="Zoom out"
            aria-label="Zoom out"
            className="w-11 h-11 rounded-lg bg-black/75 hover:bg-black/95 text-white/90 border border-white/15 flex items-center justify-center text-sm font-bold transition-colors cursor-pointer"
          >
            −
          </button>
          <button
            type="button"
            onClick={handleToggleFullscreen}
            title={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
            aria-label={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
            className="min-w-11 h-11 px-2 rounded-lg bg-black/75 hover:bg-black/95 text-white/90 border border-white/15 flex items-center justify-center text-xs transition-colors cursor-pointer"
          >
            {isFullscreen ? "Exit" : "Full"}
          </button>
        </div>
      )}

      {/* Hover Tooltip */}
      {hoveredSlot && (
        <div
          className="absolute pointer-events-none z-20 px-2.5 py-1.5 rounded-md bg-[#151c1b] text-white text-xs border border-white/20 shadow-xl transform -translate-x-1/2 -translate-y-full -mt-2"
          style={{ left: hoveredSlot.screenX, top: hoveredSlot.screenY }}
        >
          <div className="font-bold flex items-center gap-1.5">
            <span>Slot: {formatSlotLabel(hoveredSlot.slot.number)}</span>
            <span
              className="w-2 h-2 rounded-full"
              style={{
                backgroundColor:
                  hoveredSlot.slot.id === selectedSlotId
                    ? SLOT_VISUAL_STATE_HEX.selected
                    : SLOT_VISUAL_STATE_HEX[hoveredSlot.visualState],
              }}
            />
          </div>
          <div className="text-[10px] text-slate-300">
            {hoveredSlot.slot.id === selectedSlotId
              ? "Selected"
              : SLOT_VISUAL_STATE_LABELS[hoveredSlot.visualState]}
          </div>
          {hoveredSlot.visualState === "unknown" && (
            <div className="text-[9px] text-slate-400 mt-0.5">
              No reliable state reported for this bay
            </div>
          )}
          {interactive && (
            <div className="text-[9px] text-sky-400 mt-0.5">
              Click to select this slot
            </div>
          )}
        </div>
      )}

      {/* Hover Tooltip for unregistered template meshes */}
      {hoveredMesh && (
        <div
          className="absolute pointer-events-none z-20 px-2.5 py-1.5 rounded-md bg-[#151c1b] text-white text-xs border border-white/20 shadow-xl transform -translate-x-1/2 -translate-y-full -mt-2"
          style={{ left: hoveredMesh.screenX, top: hoveredMesh.screenY }}
        >
          <div className="font-bold flex items-center gap-1.5">
            <span>{hoveredMesh.name}</span>
            <span className="w-2 h-2 rounded-full bg-slate-400" />
          </div>
          <div className="text-[10px] text-slate-300">
            Not registered — no capacity, no booking
          </div>
          <div className="text-[9px] text-amber-300 mt-0.5">
            {hoveredMesh.selected ? "Click to deselect" : "Click to select for registration"}
          </div>
        </div>
      )}

      {/* Bottom Legend */}
      {showLegend && (
        <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between flex-wrap gap-2 pointer-events-none z-10">
          <div className="flex flex-wrap items-center gap-1 px-3 py-1.5 rounded-lg bg-[#151c1b] border border-white/10 text-[11px] text-slate-200">
            {SLOT_LEGEND_STATES.map((state) => (
              <span
                key={state}
                className="inline-flex items-center gap-1.5 px-1.5 text-[11px] text-slate-200"
              >
                <span
                  className="h-2 w-2 shrink-0 rounded-full"
                  style={{ backgroundColor: SLOT_VISUAL_STATE_HEX[state] }}
                />
                {SLOT_VISUAL_STATE_LABELS[state]}
              </span>
            ))}
            <span className="inline-flex items-center gap-1.5 px-1.5 text-[11px] text-slate-200">
              <span
                className="h-2 w-2 rounded-full"
                style={{ backgroundColor: SLOT_VISUAL_STATE_HEX.selected }}
              />
              Selected
            </span>
          </div>

          <div className="text-xs text-white px-2.5 py-1 rounded-md bg-[#151c1b] hidden sm:block">
            Drag to rotate · Right drag to pan · Scroll to zoom
          </div>
        </div>
      )}
    </div>
  )
}
