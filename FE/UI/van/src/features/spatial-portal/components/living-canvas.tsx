import * as React from "react"
import { MapPin, Navigation, Layers, ArrowLeft, ArrowRight, Sparkles, Search, X } from "lucide-react"
import { Parking3DViewer } from "@/components/3d/parking-3d-viewer"
import { Button, MorphButton } from "@/components/ui/button"
import type { ParkingSlot } from "@/types/domain"
import { MapTilerViewer } from "./maptiler-viewer"

export interface ParkingLotSummary {
  id: string
  name: string
  address: string
  distanceKm: number
  availableSlots: number
  totalSlots: number
  hourlyRate: number
  lat: number
  lng: number
  status: "available" | "almostFull" | "occupied"
  structure: "basement" | "outdoor" | "multi-storey"
}

export const MOCK_LOTS: ParkingLotSummary[] = [
  {
    id: "lot-vincom",
    name: "Vincom Center Đồng Khởi",
    address: "72 Lê Thánh Tôn, P. Bến Nghé, Quận 1",
    distanceKm: 0.8,
    availableSlots: 65,
    totalSlots: 100,
    hourlyRate: 25000,
    lat: 10.7781,
    lng: 106.7018,
    status: "available",
    structure: "basement",
  },
  {
    id: "lot-saigon-centre",
    name: "Saigon Centre (Takashimaya)",
    address: "65 Lê Lợi, P. Bến Nghé, Quận 1",
    distanceKm: 0.5,
    availableSlots: 42,
    totalSlots: 120,
    hourlyRate: 30000,
    lat: 10.7735,
    lng: 106.7012,
    status: "available",
    structure: "basement",
  },
  {
    id: "lot-bitexco",
    name: "Bitexco Financial Tower",
    address: "02 Hải Triều, P. Bến Nghé, Quận 1",
    distanceKm: 1.2,
    availableSlots: 14,
    totalSlots: 80,
    hourlyRate: 35000,
    lat: 10.7716,
    lng: 106.7044,
    status: "almostFull",
    structure: "basement",
  },
  {
    id: "lot-diamond",
    name: "Diamond Plaza",
    address: "34 Lê Duẩn, P. Bến Nghé, Quận 1",
    distanceKm: 1.5,
    availableSlots: 0,
    totalSlots: 90,
    hourlyRate: 25000,
    lat: 10.7812,
    lng: 106.6989,
    status: "occupied",
    structure: "multi-storey",
  },
]

export const MOCK_SLOTS_3D: ParkingSlot[] = [
  { id: "slot-A-01", number: "A-01", floor: 1, status: "available" },
  { id: "slot-A-02", number: "A-02", floor: 1, status: "occupied" },
  { id: "slot-A-03", number: "A-03", floor: 1, status: "available" },
  { id: "slot-A-04", number: "A-04", floor: 1, status: "reserved" },
  { id: "slot-A-05", number: "A-05", floor: 1, status: "available" },
  { id: "slot-A-06", number: "A-06", floor: 1, status: "occupied" },
  { id: "slot-A-07", number: "A-07", floor: 1, status: "available" },
  { id: "slot-A-08", number: "A-08", floor: 1, status: "available" },
  { id: "slot-A-09", number: "A-09", floor: 1, status: "occupied" },
  { id: "slot-A-10", number: "A-10", floor: 1, status: "available" },
  { id: "slot-A-11", number: "A-11", floor: 1, status: "available" },
  { id: "slot-A-12", number: "A-12", floor: 1, status: "available" },
  { id: "slot-A-13", number: "A-13", floor: 1, status: "occupied" },
  { id: "slot-A-14", number: "A-14", floor: 1, status: "available" },
  { id: "slot-A-15", number: "A-15", floor: 1, status: "reserved" },
  { id: "slot-A-16", number: "A-16", floor: 1, status: "available" },
  { id: "slot-A-17", number: "A-17", floor: 1, status: "occupied" },
  { id: "slot-A-18", number: "A-18", floor: 1, status: "available" },
  { id: "slot-A-19", number: "A-019", floor: 1, status: "available" },
  { id: "slot-A-20", number: "A-020", floor: 1, status: "available" },
  { id: "slot-A-21", number: "A-021", floor: 1, status: "available" },
  { id: "slot-A-22", number: "A-022", floor: 1, status: "available" },
  { id: "slot-A-23", number: "A-023", floor: 1, status: "occupied" },
  { id: "slot-A-24", number: "A-024", floor: 1, status: "available" },
]

/* ========================================================================== 
   SLOT PREVIEW CARD (Unified Design System Standard)
   - min-w-[76px] h-12 whitespace-nowrap prevents line break
   - Height h-10 button
   ========================================================================== */
export function SlotPreviewCard({
  slot,
  floor,
  hourlyRate,
  onOpenBookingDrawer,
}: {
  slot: ParkingSlot
  floor: number
  hourlyRate: number
  onOpenBookingDrawer: () => void
}) {
  return (
    <div className="p-3 rounded-2xl backdrop-blur-2xl bg-white/95 text-slate-900 border border-slate-200/90 shadow-xl dark:bg-[#181918]/95 dark:text-white dark:border-white/15 dark:shadow-2xl flex items-center gap-4 min-w-[340px] sm:min-w-[380px] pointer-events-auto">
      {/* Slot Hero Badge: min-w-[76px] prevents line break */}
      <div className="min-w-[76px] px-3.5 h-12 whitespace-nowrap rounded-xl bg-[#B4E33F] text-[#1E3B27] font-bold text-base flex items-center justify-center shrink-0 shadow-sm">
        {slot.number}
      </div>

      <div className="flex-1 overflow-hidden">
        <div className="flex items-center gap-2">
          <p className="font-bold text-sm text-slate-900 dark:text-white truncate">
            Ô đỗ {slot.number}
          </p>
          <span className="text-sm font-semibold px-2.5 py-0.5 rounded-full bg-emerald-500/15 text-emerald-700 dark:text-emerald-400 whitespace-nowrap">
            Sẵn sàng
          </span>
        </div>
        <p className="text-sm text-slate-500 dark:text-slate-400 mt-0.5 truncate">
          Tầng B{floor} · {hourlyRate.toLocaleString("vi-VN")} VND / giờ
        </p>
      </div>

      <MorphButton
        intent="primary"
        size="md"
        text="Đặt chỗ này"
        icon={<ArrowRight size={18} />}
        minWidth="min-w-[136px]"
        onClick={onOpenBookingDrawer}
        className="h-10 px-4 rounded-xl text-sm font-semibold whitespace-nowrap shrink-0 shadow-sm"
      />
    </div>
  )
}

interface LivingCanvasProps {
  viewMode: "2d-city" | "3d-lot"
  selectedLot: ParkingLotSummary
  selectedSlot: ParkingSlot | null
  selectedFloor: number
  isBookingDrawerOpen: boolean
  isWorkspaceOpen?: boolean
  isDark: boolean
  sidebarOffset: number
  onSelectLot: (lot: ParkingLotSummary) => void
  onSelectSlot: (slot: ParkingSlot) => void
  onBackToCity: () => void
  onChangeFloor: (floor: number) => void
}

export function LivingCanvas({
  viewMode,
  selectedLot,
  selectedSlot,
  selectedFloor,
  isBookingDrawerOpen,
  isWorkspaceOpen = false,
  isDark,
  sidebarOffset,
  onSelectLot,
  onSelectSlot,
  onBackToCity,
  onChangeFloor,
}: LivingCanvasProps) {
  // Search state in 2D city mode
  const [searchQuery, setSearchQuery] = React.useState("")
  const [isSearchFocused, setIsSearchFocused] = React.useState(false)
  const [focusLot, setFocusLot] = React.useState<ParkingLotSummary | null>(null)

  const matchingLots = React.useMemo(() => {
    if (!searchQuery.trim()) return []
    const q = searchQuery.toLowerCase().trim()
    return MOCK_LOTS.filter((l) => l.name.toLowerCase().includes(q) || l.address.toLowerCase().includes(q))
  }, [searchQuery])

  return (
    <div
      className={`absolute inset-0 w-full h-full overflow-hidden select-none transition-colors duration-300 ${
        isDark ? "bg-[#181918]" : "bg-[#f1f5f9]"
      }`}
    >
      {/* ====================================================================
          MODE A: 2D CITY MAP (Real MapTiler Vector Map)
          ==================================================================== */}
      {viewMode === "2d-city" && (
        <div className="relative w-full h-full">
          {/* Real MapTiler Vector Map Engine */}
          <div className={`w-full h-full transition-all duration-300 ${isWorkspaceOpen ? "filter blur-[2px] opacity-40 pointer-events-none" : ""}`}>
            <MapTilerViewer
              lots={MOCK_LOTS}
              selectedLotId={selectedLot.id}
              isDark={isDark}
              onSelectLot={onSelectLot}
              focusLot={focusLot}
            />
          </div>

          {/* Floating Search Bar (Top Safe Zone with dynamic sidebar offset) */}
          <div
            className={`absolute top-6 z-20 flex flex-col transition-all duration-300 ${
              isWorkspaceOpen ? "-translate-y-16 opacity-0 pointer-events-none" : "translate-y-0 opacity-100 pointer-events-auto"
            }`}
            style={{ left: sidebarOffset }}
          >
            <div className="relative w-[320px] sm:w-[380px]">
              <div className="h-11 px-3.5 rounded-2xl backdrop-blur-xl bg-white/95 text-slate-900 border border-slate-200/90 shadow-xl dark:bg-[#181918]/95 dark:text-white dark:border-white/15 dark:shadow-2xl flex items-center gap-2.5">
                <Search size={18} className="text-slate-400 shrink-0" />
                <input
                  type="text"
                  placeholder="Tìm bãi đỗ (Vincom, Lê Lợi...)"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onFocus={() => setIsSearchFocused(true)}
                  className="w-full bg-transparent text-sm font-medium focus:outline-none placeholder:text-slate-400"
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery("")}
                    className="size-5 rounded-full bg-slate-200 dark:bg-white/10 flex items-center justify-center text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white cursor-pointer"
                  >
                    <X size={12} />
                  </button>
                )}
              </div>

              {/* Search Dropdown Results */}
              {isSearchFocused && matchingLots.length > 0 && (
                <div className="absolute top-full left-0 right-0 mt-2 p-1.5 rounded-2xl backdrop-blur-xl bg-white/95 border border-slate-200/90 shadow-2xl dark:bg-[#181918]/95 dark:border-white/15 z-30 space-y-1 animate-in fade-in zoom-in-95 duration-150">
                  {matchingLots.map((lot) => (
                    <button
                      key={lot.id}
                      type="button"
                      onClick={() => {
                        setFocusLot(lot)
                        onSelectLot(lot)
                        setSearchQuery("")
                        setIsSearchFocused(false)
                      }}
                      className="w-full p-2.5 rounded-xl hover:bg-slate-100 dark:hover:bg-white/10 text-left transition-colors flex items-center justify-between cursor-pointer"
                    >
                      <div>
                        <p className="font-bold text-sm text-slate-900 dark:text-white">{lot.name}</p>
                        <p className="text-sm text-slate-500 dark:text-slate-400 truncate max-w-[240px]">{lot.address}</p>
                      </div>
                      <span className="text-sm font-bold px-2.5 py-0.5 rounded-lg bg-[#B4E33F]/20 text-[#1E3B27] dark:text-[#B4E33F]">
                        {lot.availableSlots} chỗ
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Quick Context Pill */}
            <div className="mt-2 hidden sm:flex items-center gap-2 px-3.5 py-2 rounded-xl backdrop-blur-md bg-white/80 border border-slate-200/70 text-slate-700 dark:bg-black/60 dark:border-white/10 dark:text-slate-300 text-sm font-medium w-fit shadow-xs">
              <Sparkles size={14} className="text-[#285337] dark:text-[#B4E33F]" />
              <span>Click vào bãi đỗ xe trên bản đồ để mở mô hình 3D Digital Twin</span>
            </div>
          </div>
        </div>
      )}

      {/* ====================================================================
          MODE B: 3D DIGITAL TWIN LOT (Three.js WebGL Spatial Environment)
          ==================================================================== */}
      {viewMode === "3d-lot" && (
        <div className="relative w-full h-full">
          {/* Top-Left Floating Safe-Zone Controls: Back to 2D Map & Lot Info */}
          <div
            className="absolute top-6 z-10 flex items-center gap-3 transition-[left] duration-300"
            style={{
              left: isBookingDrawerOpen ? sidebarOffset + 430 : sidebarOffset,
            }}
          >
            <button
              type="button"
              onClick={onBackToCity}
              className="h-10 px-4 rounded-xl backdrop-blur-xl bg-white/95 hover:bg-white text-slate-900 border border-slate-200/80 shadow-md dark:bg-[#181918]/95 dark:hover:bg-[#181918] dark:text-white dark:border-white/20 dark:shadow-xl flex items-center gap-2 text-sm font-semibold whitespace-nowrap transition-all cursor-pointer hover:border-[#B4E33F]"
            >
              <ArrowLeft size={16} />
              <span>Bản đồ 2D</span>
            </button>

            <div className="h-10 px-4 rounded-xl backdrop-blur-xl bg-white/95 text-slate-900 border border-slate-200/80 shadow-md dark:bg-[#181918]/95 dark:text-white dark:border-white/20 dark:shadow-xl flex items-center gap-3">
              <span className="text-sm font-bold truncate max-w-[200px]">{selectedLot.name}</span>
              <span className="text-sm font-semibold px-2.5 py-0.5 rounded-md bg-[#B4E33F]/20 text-[#1E3B27] dark:text-[#B4E33F] border border-[#B4E33F]/30 whitespace-nowrap">
                Tầng B{selectedFloor}
              </span>
            </div>
          </div>

          {/* Top-Right Floating Controls: Floor Switcher */}
          <div className="absolute top-6 right-6 z-10 flex items-center gap-1.5 h-11 px-2.5 rounded-2xl backdrop-blur-xl bg-white/95 text-slate-800 border border-slate-200/80 shadow-md dark:bg-[#181918]/95 dark:text-white dark:border-white/20 dark:shadow-xl">
            <span className="text-sm text-slate-500 dark:text-slate-400 font-semibold px-1.5 flex items-center gap-1">
              <Layers size={16} />
              TẦNG
            </span>
            <button
              type="button"
              onClick={() => onChangeFloor(1)}
              className={`h-8 px-3.5 rounded-lg text-sm font-bold whitespace-nowrap transition-all cursor-pointer ${
                selectedFloor === 1
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white"
              }`}
            >
              Hầm B1
            </button>
            <button
              type="button"
              onClick={() => onChangeFloor(2)}
              className={`h-8 px-3.5 rounded-lg text-sm font-bold whitespace-nowrap transition-all cursor-pointer ${
                selectedFloor === 2
                  ? "bg-[#285337] text-white dark:bg-[#B4E33F] dark:text-[#152A1B] shadow-xs"
                  : "text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white"
              }`}
            >
              Hầm B2
            </button>
          </div>

          {/* Fullscreen Three.js WebGL Engine */}
          <div className="w-full h-full">
            <Parking3DViewer
              lotType={selectedLot.structure === "basement" ? "basement" : "outdoor"}
              slots={MOCK_SLOTS_3D}
              selectedSlotId={selectedSlot?.id}
              focusSlotId={selectedSlot?.id}
              onSelectSlot={(slot) => {
                onSelectSlot(slot)
              }}
              height="100%"
              isDark={isDark}
              className="w-full h-full"
            />
          </div>
        </div>
      )}
    </div>
  )
}
