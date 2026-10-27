import React, { useEffect, useRef, useState, useCallback } from "react"

import * as THREE from "three"

import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js"

import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js"

import type { LotType, ParkingSlot } from "../../lib/types"
import { UntitledIcon } from "../icon/UntitledIcon"
import { ParkingStatus } from "../ui/ParkingStatus"

import {
  ALLOWED_PARKING_MODELS,
  SLOT_STATUS_HEX,
  validateModelUpload,
} from "../../lib/3d/parking3DConfig"

import {
  mapModelSlots,
  findSlotMesh,
  SlotMatchResult,
} from "../../lib/3d/slotMatcher"

import { SlotMaterialManager } from "../../lib/3d/slotMaterialManager"

import { CameraController } from "../../lib/3d/cameraController"

import { generateProceduralParkingLot } from "../../lib/3d/proceduralLotGenerator"

export interface Parking3DViewerProps {
  modelUrl?: string

  lotType?: LotType

  slots?: ParkingSlot[]

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
}

export function Parking3DViewer({
  modelUrl,

  lotType = "outdoor",

  slots = [],

  selectedSlotId = null,

  onSelectSlot,

  focusSlotId = null,

  interactive = true,

  height = 420,

  className = "",

  showLegend = true,

  showControls = true,

  onModelLoaded,
}: Parking3DViewerProps) {
  const containerRef = useRef<HTMLDivElement>(null)

  const canvasRef = useRef<HTMLCanvasElement>(null)

  // Three.js instances ref

  const threeRef = useRef<{
    renderer: THREE.WebGLRenderer

    scene: THREE.Scene

    camera: THREE.PerspectiveCamera

    controls: OrbitControls

    cameraController: CameraController

    materialManager: SlotMaterialManager

    currentSceneContent: THREE.Object3D | null

    slotMapping: SlotMatchResult | null

    animFrameId: number
  } | null>(null)

  const [loading, setLoading] = useState(true)

  const [loadingError, setLoadingError] = useState<string | null>(null)

  const [hoveredSlot, setHoveredSlot] = useState<{
    slot: ParkingSlot
    screenX: number
    screenY: number
  } | null>(null)

  const [isFullscreen, setIsFullscreen] = useState(false)

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

  const targetUrl = modelUrl || ALLOWED_PARKING_MODELS[lotType]?.path

  const expectedFileName =
    ALLOWED_PARKING_MODELS[lotType]?.fileName || "outdoor_parking_lot.glb"

  // Apply slot statuses & highlights

  const refreshSlotVisuals = useCallback(() => {
    const inst = threeRef.current

    if (!inst || !inst.slotMapping) return

    const { meshBySlotKey } = inst.slotMapping

    meshBySlotKey.forEach((mesh, key) => {
      if (mesh instanceof THREE.Mesh) {
        const slotData = mesh.userData.slotData as ParkingSlot | undefined

        const isSelected =
          !!selectedSlotId &&
          (slotData?.id === selectedSlotId ||
            slotData?.number === selectedSlotId ||
            key === selectedSlotId)

        const isFocused =
          !!focusSlotId &&
          (slotData?.id === focusSlotId ||
            slotData?.number === focusSlotId ||
            key === focusSlotId)

        inst.materialManager.applySlotState(
          mesh,
          slotData,
          isSelected,
          isFocused,
        )
      }
    })
  }, [selectedSlotId, focusSlotId])

  const lastFocusedKeyRef = useRef<string | null>(null)

  // Focus camera on slot

  const focusOnSlotKey = useCallback((targetSlotKey: string) => {
    const inst = threeRef.current

    if (!inst || !inst.slotMapping) return

    if (lastFocusedKeyRef.current === targetSlotKey) return

    lastFocusedKeyRef.current = targetSlotKey

    const mesh = findSlotMesh(inst.slotMapping.meshBySlotKey, targetSlotKey)

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

    controls.maxDistance = 120

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

      slotMapping: null,

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

      if (selectedSlotId && threeRef.current?.slotMapping) {
        const mesh = findSlotMesh(
          threeRef.current.slotMapping.meshBySlotKey,
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

  const loadedModelUrlRef = useRef<string | null>(null)

  const refreshSlotVisualsRef = useRef(refreshSlotVisuals)

  refreshSlotVisualsRef.current = refreshSlotVisuals

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
    }

    const applySceneContent = (
      loadedObject: THREE.Object3D,

      isGlb: boolean,

      resolvedName: string,
    ) => {
      inst.scene.add(loadedObject)

      inst.currentSceneContent = loadedObject

      loadedModelUrlRef.current = targetUrl || ""

      // Map slots using latest slots

      const mapping = mapModelSlots(loadedObject, slotsRef.current)

      inst.slotMapping = mapping

      // Visuals

      refreshSlotVisualsRef.current()

      // Frame camera overview only on initial model load

      inst.cameraController.resetToOverview(
        loadedObject,
        inst.camera,
        inst.controls,
      )

      if (onModelLoadedRef.current) {
        onModelLoadedRef.current({
          detectedSlotKeys: mapping.detectedSlotKeys,

          isCustomGlb: isGlb,

          modelName: resolvedName,
        })
      }

      setLoading(false)
    }

    // Attempt to load GLB file

    const loader = new GLTFLoader()

    const cleanUrl = targetUrl

    if (cleanUrl) {
      loader.load(
        cleanUrl,

        (gltf) => {
          // Successfully loaded GLB!

          const modelScene = gltf.scene

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
        },

        undefined,

        (err) => {
          // File not found or not yet put in folder: Fallback to procedural model with slots

          console.info(
            `[3D Viewer] Model file "${cleanUrl}" not yet found on disk. Initializing high-fidelity procedural 3D model with matching naming convention (${expectedFileName}).`,
          )

          const proceduralLot = generateProceduralParkingLot(
            lotType,
            slotsRef.current,
            1,
          )

          applySceneContent(proceduralLot, false, expectedFileName)
        },
      )
    } else {
      const proceduralLot = generateProceduralParkingLot(
        lotType,
        slotsRef.current,
        1,
      )

      applySceneContent(proceduralLot, false, expectedFileName)
    }
  }, [targetUrl, lotType, expectedFileName])

  // Sync slot data when slots array updates without reloading the 3D model

  useEffect(() => {
    const inst = threeRef.current

    if (!inst || !inst.currentSceneContent) return

    const mapping = mapModelSlots(inst.currentSceneContent, slots)

    inst.slotMapping = mapping


    refreshSlotVisuals()
  }, [slots, refreshSlotVisuals])

  // Update visuals whenever selectedSlotId changes

  useEffect(() => {
    refreshSlotVisuals()
  }, [selectedSlotId, focusSlotId, refreshSlotVisuals])

  // Camera focus when focusSlotId or selectedSlotId changes

  useEffect(() => {
    const target = focusSlotId || selectedSlotId

    if (target) {
      focusOnSlotKey(target)
    }
  }, [focusSlotId, selectedSlotId, focusOnSlotKey])

  // Raycasting for click selection and hover

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!interactive) return

    const inst = threeRef.current

    if (!inst || !inst.slotMapping) return

    const canvas = canvasRef.current

    if (!canvas) return

    const rect = canvas.getBoundingClientRect()

    const x = ((e.clientX - rect.left) / rect.width) * 2 - 1

    const y = -((e.clientY - rect.top) / rect.height) * 2 + 1

    const raycaster = new THREE.Raycaster()

    raycaster.setFromCamera(new THREE.Vector2(x, y), inst.camera)

    const slotMeshes: THREE.Object3D[] = inst.slotMapping.allSlotMeshes || []

    const intersects = raycaster.intersectObjects(slotMeshes, true)

    if (intersects.length > 0) {
      let targetMesh: THREE.Object3D | null = intersects[0].object

      while (
        targetMesh &&
        !targetMesh.userData.isSlot &&
        targetMesh.parent &&
        targetMesh.parent !== inst.scene
      ) {
        targetMesh = targetMesh.parent
      }

      if (targetMesh && targetMesh.userData.isSlot) {
        const slotData = targetMesh.userData.slotData as ParkingSlot || {
          id: targetMesh.userData.slotId || targetMesh.name,

          number: targetMesh.userData.slotNumber || targetMesh.name,

          floor: 1,

          status: "available",
        }

        if (onSelectSlot) {
          onSelectSlot(slotData)
        }

        lastFocusedKeyRef.current = slotData.id || slotData.number

        inst.cameraController.focusOnObject(
          targetMesh,
          inst.camera,
          inst.controls,
        )
      }
    }
  }

  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const inst = threeRef.current

    if (!inst || !inst.slotMapping) return

    const canvas = canvasRef.current

    if (!canvas) return

    const rect = canvas.getBoundingClientRect()

    const x = ((e.clientX - rect.left) / rect.width) * 2 - 1

    const y = -((e.clientY - rect.top) / rect.height) * 2 + 1

    const raycaster = new THREE.Raycaster()

    raycaster.setFromCamera(new THREE.Vector2(x, y), inst.camera)

    const slotMeshes: THREE.Object3D[] = inst.slotMapping.allSlotMeshes || []

    const intersects = raycaster.intersectObjects(slotMeshes, true)

    if (intersects.length > 0) {
      let targetMesh: THREE.Object3D | null = intersects[0].object

      while (
        targetMesh &&
        !targetMesh.userData.isSlot &&
        targetMesh.parent &&
        targetMesh.parent !== inst.scene
      ) {
        targetMesh = targetMesh.parent
      }

      if (targetMesh && targetMesh.userData.isSlot) {
        const slotData = targetMesh.userData.slotData as ParkingSlot || {
          id: targetMesh.userData.slotId || targetMesh.name,

          number: targetMesh.userData.slotNumber || targetMesh.name,

          floor: 1,

          status: "available",
        }

        setHoveredSlot({
          slot: slotData,

          screenX: e.clientX - rect.left,

          screenY: e.clientY - rect.top,
        })

        canvas.style.cursor = interactive ? "pointer" : "default"

        return
      }
    }

    setHoveredSlot(null)

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
        onPointerLeave={() => setHoveredSlot(null)}
        className="w-full h-full block touch-none"
      />

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
            <span>Slot: {hoveredSlot.slot.number}</span>
            <span
              className="w-2 h-2 rounded-full"
              style={{
                backgroundColor:
                  hoveredSlot.slot.id === selectedSlotId
                    ? SLOT_STATUS_HEX.selected
                    : SLOT_STATUS_HEX[hoveredSlot.slot.status || "available"],
              }}
            />
          </div>
          <div className="text-[10px] text-slate-300 capitalize">
            {hoveredSlot.slot.id === selectedSlotId
              ? "Selected"
              : hoveredSlot.slot.status === "available"
                ? "Available"
                : hoveredSlot.slot.status === "occupied"
                  ? "Occupied"
                  : hoveredSlot.slot.status === "maintenance"
                    ? "Under maintenance"
                    : hoveredSlot.slot.status === "disabled"
                      ? "Disabled"
                      : "Reserved"}
          </div>
          {interactive && (
            <div className="text-[9px] text-sky-400 mt-0.5">
              Click to select this slot
            </div>
          )}
        </div>
      )}

      {/* Bottom Legend */}
      {showLegend && (
        <div className="absolute bottom-3 left-3 right-3 flex items-center justify-between flex-wrap gap-2 pointer-events-none z-10">
          <div className="flex flex-wrap items-center gap-1 px-3 py-1.5 rounded-lg bg-[#151c1b] border border-white/10 text-[11px] text-slate-200">
            <ParkingStatus status="available" compact />
            <ParkingStatus status="occupied" compact />
            <ParkingStatus status="reserved" compact />
            <ParkingStatus status="maintenance" compact />
            <span className="px-2">Selected</span>
          </div>

          <div className="text-xs text-white px-2.5 py-1 rounded-md bg-[#151c1b] hidden sm:block">
            Drag to rotate · Right drag to pan · Scroll to zoom
          </div>
        </div>
      )}
    </div>
  )
}
