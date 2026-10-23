import * as React from "react"
import { LivingCanvas, SlotPreviewCard, MOCK_LOTS, MOCK_SLOTS_3D, type ParkingLotSummary } from "./components/living-canvas"
import { SpatialSidebar, type SpatialNavTab } from "./components/spatial-sidebar"
import { SpatialDock } from "./components/spatial-dock"
import { SpatialBookingDrawer } from "./components/spatial-booking-drawer"
import { SpatialHistoryView, type BookingHistoryItem } from "./components/spatial-history-view"
import { SpatialGarageView } from "./components/spatial-garage-view"
import {
  SpatialAiAssistantModal,
  SpatialSosModal,
} from "./components/spatial-quick-modals"
import type { ParkingSlot, RegisteredVehicle, VehicleType } from "@/types/domain"

const initialVehicles: RegisteredVehicle[] = [
  {
    id: "veh-1",
    rawPlate: "51A12345",
    canonicalPlate: "51A12345",
    vehicleType: "CAR",
    imageReference: null,
  },
  {
    id: "veh-2",
    rawPlate: "59B167890",
    canonicalPlate: "59B167890",
    vehicleType: "MOTORCYCLE",
    imageReference: null,
  },
]

export interface SpatialPortalRootProps {
  onOpenDesignSpec?: () => void
}

export function SpatialPortalRoot({ onOpenDesignSpec }: SpatialPortalRootProps = {}) {
  // Navigation & Spatial Mode State
  const [navTab, setNavTab] = React.useState<SpatialNavTab>("explore")
  const [viewMode, setViewMode] = React.useState<"2d-city" | "3d-lot">("2d-city")
  const [isDark, setIsDark] = React.useState<boolean>(true)
  const [isSidebarExpanded, setIsSidebarExpanded] = React.useState<boolean>(false)
  const [isMobileViewport, setIsMobileViewport] = React.useState(() =>
    typeof window !== "undefined" && window.matchMedia("(max-width: 639px)").matches
  )

  React.useEffect(() => {
    const viewportQuery = window.matchMedia("(max-width: 639px)")
    const updateViewport = () => setIsMobileViewport(viewportQuery.matches)
    updateViewport()
    viewportQuery.addEventListener("change", updateViewport)
    return () => viewportQuery.removeEventListener("change", updateViewport)
  }, [])

  const sidebarOffset = isMobileViewport ? 80 : isSidebarExpanded ? 272 : 80

  // Selection States
  const [selectedLot, setSelectedLot] = React.useState<ParkingLotSummary>(MOCK_LOTS[0])
  const [selectedSlot, setSelectedSlot] = React.useState<ParkingSlot | null>(null)
  const [selectedFloor, setSelectedFloor] = React.useState<number>(1)
  const [isBookingDrawerOpen, setIsBookingDrawerOpen] = React.useState<boolean>(false)

  // Vehicle States
  const [vehicles, setVehicles] = React.useState<RegisteredVehicle[]>(initialVehicles)
  const [selectedVehicle, setSelectedVehicle] = React.useState<RegisteredVehicle>(initialVehicles[0])

  // Quick Action Modal States
  const [isAiModalOpen, setIsAiModalOpen] = React.useState<boolean>(false)
  const [isSosModalOpen, setIsSosModalOpen] = React.useState<boolean>(false)

  // Handlers
  React.useEffect(() => {
    document.documentElement.classList.toggle("dark", isDark)
    document.documentElement.setAttribute("data-theme", isDark ? "dark" : "light")
  }, [isDark])

  const handleSelectLotFromCity = (lot: ParkingLotSummary) => {
    setSelectedLot(lot)
    setSelectedSlot(MOCK_SLOTS_3D[0])
    setViewMode("3d-lot")
    setNavTab("explore")
  }

  const handleBackToCity = () => {
    setViewMode("2d-city")
    setIsBookingDrawerOpen(false)
    setSelectedSlot(null)
  }

  const handleFindNearestLot = () => {
    // 1-Click GPS selects nearest lot (Saigon Centre or Vincom)
    const nearest = MOCK_LOTS.reduce((prev, curr) => (curr.distanceKm < prev.distanceKm ? curr : prev))
    handleSelectLotFromCity(nearest)
  }

  const handleAddVehicle = (newVeh: { plate: string; type: VehicleType; brand: string }) => {
    const created: RegisteredVehicle = {
      id: `veh-${Date.now()}`,
      rawPlate: newVeh.plate.replace(/[\s.-]/g, ""),
      canonicalPlate: newVeh.plate.replace(/[\s.-]/g, ""),
      vehicleType: newVeh.type,
      imageReference: null,
      brand: newVeh.brand || undefined,
    }
    setVehicles((prev) => [created, ...prev])
    setSelectedVehicle(created)
  }

  const handleDeleteVehicle = (vehicleId: string) => {
    const remaining = vehicles.filter((vehicle) => vehicle.id !== vehicleId)
    setVehicles(remaining)

    if (selectedVehicle.id === vehicleId && remaining.length > 0) {
      setSelectedVehicle(remaining[0])
    }
  }

  return (
    <div
      className={`w-screen h-screen overflow-hidden fixed inset-0 select-none transition-colors duration-300 ${
        isDark ? "bg-[#181918] text-white dark" : "bg-[#f8f8f7] text-slate-900"
      }`}
    >

      {/* ====================================================================
          LỚP 0 (Z-INDEX: 0): THE LIVING CANVAS
          Chiếm trọn 100vw x 100vh. Chuyển đổi mượt mà giữa Bản đồ 2D và 3D
          ==================================================================== */}
      <LivingCanvas
        viewMode={viewMode}
        selectedLot={selectedLot}
        selectedSlot={selectedSlot}
        selectedFloor={selectedFloor}
        isBookingDrawerOpen={isBookingDrawerOpen}
        isWorkspaceOpen={navTab !== "explore"}
        isDark={isDark}
        sidebarOffset={sidebarOffset}
        onSelectLot={handleSelectLotFromCity}
        onSelectSlot={(slot) => {
          setSelectedSlot(slot)
        }}
        onBackToCity={handleBackToCity}
        onChangeFloor={setSelectedFloor}
      />

      {/* ====================================================================
          LỚP 1 (Z-INDEX: 10): CONTEXTUAL DRAWERS & WORKSPACES
          Ngăn kéo trượt kính mờ (Slide-over) hoặc Workspaces bán trong suốt
          ==================================================================== */}
      {/* 1.1 Left Slide-over Booking Form */}
      <SpatialBookingDrawer
        isOpen={isBookingDrawerOpen && navTab === "explore"}
        onClose={() => setIsBookingDrawerOpen(false)}
        lotName={selectedLot.name}
        lotAddress={selectedLot.address}
        slot={selectedSlot}
        floor={selectedFloor}
        hourlyRate={selectedLot.hourlyRate}
        vehicles={vehicles}
        selectedVehicle={selectedVehicle}
        sidebarOffset={sidebarOffset}
        onSelectVehicle={setSelectedVehicle}
        onConfirmBooking={(data) => {
          setIsBookingDrawerOpen(false)
          setNavTab("history") // Transition to active ticket pass view
        }}
      />

      {/* 1.2 Translucent Booking History & Active Pass Workspace */}
      {navTab === "history" && (
        <SpatialHistoryView
          onClose={() => setNavTab("explore")}
          sidebarOffset={sidebarOffset}
          onRebookSlot={(item) => {
            setNavTab("explore")
            setViewMode("3d-lot")
            setSelectedSlot({
              id: `slot-${item.slotNumber}`,
              number: item.slotNumber,
              floor: item.floor,
              status: "available",
            })
            setIsBookingDrawerOpen(true)
          }}
          onOpen3DMap={(item) => {
            setNavTab("explore")
            setViewMode("3d-lot")
          }}
        />
      )}

      {/* 1.3 Translucent Profile & My Garage Workspace */}
      {navTab === "garage" && (
        <SpatialGarageView
          vehicles={vehicles}
          defaultVehicleId={selectedVehicle.id}
          onClose={() => setNavTab("explore")}
          onAddVehicle={handleAddVehicle}
          onDeleteVehicle={handleDeleteVehicle}
          onSetDefaultVehicle={setSelectedVehicle}
          sidebarOffset={sidebarOffset}
        />
      )}

      {/* ====================================================================
          LỚP 2 (Z-INDEX: 20): PERSISTENT HUD
          Floating Left Sidebar + Floating Bottom Quick-Action Dock & Slot Preview Card
          ==================================================================== */}
      <SpatialSidebar
        activeTab={navTab}
        onSelectTab={(tab) => {
          setNavTab(tab)
          if (tab !== "explore") {
            setIsBookingDrawerOpen(false)
          }
        }}
        isMobileViewport={isMobileViewport}
        isDark={isDark}
        onToggleDark={() => setIsDark((prev) => !prev)}
        onOpenDesignSpec={onOpenDesignSpec}
        onExpandedChange={setIsSidebarExpanded}
      />

      {/* Selected slot preview stays centered in the available map area. */}
      <div
        className={`fixed bottom-6 left-1/2 -translate-x-1/2 z-20 flex flex-col items-center pointer-events-none transition-all duration-300 ${
          isBookingDrawerOpen || navTab !== "explore" ? "translate-y-28 opacity-0 pointer-events-none" : "translate-y-0 opacity-100"
        }`}
        style={{
          left: `calc(50% + ${sidebarOffset / 2}px)`,
          width: `calc(100vw - ${sidebarOffset}px - 32px)`,
        }}
      >
        {selectedSlot && !isBookingDrawerOpen && navTab === "explore" && (
          <div className="mb-3 animate-in fade-in slide-in-from-bottom-3 duration-200">
            <SlotPreviewCard
              slot={selectedSlot}
              floor={selectedFloor}
              hourlyRate={selectedLot.hourlyRate}
              onOpenBookingDrawer={() => setIsBookingDrawerOpen(true)}
            />
          </div>
        )}
      </div>

      {/* Map shortcuts float at the lower right, clear of the left navigation rail. */}
      <div
        className={`fixed ${selectedSlot ? "bottom-40 sm:bottom-6" : "bottom-6"} right-4 z-20 transition-all duration-300 ${
          isBookingDrawerOpen || navTab !== "explore" ? "translate-y-28 opacity-0 pointer-events-none" : "translate-y-0 opacity-100"
        }`}
      >
        <SpatialDock
          onFindNearest={handleFindNearestLot}
          onOpenAiAssistant={() => setIsAiModalOpen(true)}
          onOpenEmergencySos={() => setIsSosModalOpen(true)}
        />
      </div>

      {/* ====================================================================
          LỚP 3 (Z-INDEX: 50): QUICK ACTION POPUPS / MODALS
          ==================================================================== */}
      <SpatialAiAssistantModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
      />

      <SpatialSosModal
        isOpen={isSosModalOpen}
        onClose={() => setIsSosModalOpen(false)}
      />

    </div>
  )
}
