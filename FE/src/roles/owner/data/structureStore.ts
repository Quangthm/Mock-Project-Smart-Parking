import type {
  ParkingFloorItem,
  ParkingZoneItem,
  ParkingSpaceItem,
  ParkingStructureData,
  SpaceStatus,
  VehicleCategory,
  PhysicalState,
  ReservationProtectionState,
  CapacityAccountingStats,
  CategoryCapacity,
  LotPolicySummary,
} from "../../../lib/structureTypes"
import { store as mainStore } from "../../../lib/store"

const STRUCTURES_KEY = "sp_parking_structures"

function generateInitialSpaces(
  count: number,
  prefix: string,
  zoneId: string,
  floorId: string,
  lotId: string,
  vehicleType: VehicleCategory,
  sampleOccupiedIndexes: number[] = [],
): ParkingSpaceItem[] {
  const spaces: ParkingSpaceItem[] = []
  const now = new Date()

  for (let i = 1; i <= count; i++) {
    const code = `${prefix}${String(i).padStart(2, "0")}`
    let status: SpaceStatus = "available"
    let physicalState: PhysicalState = "AVAILABLE"
    let reservationState: ReservationProtectionState = "NONE"
    let isProtected = false
    let isBackup = false
    let isPendingPayment = false
    let requestedSlotCode: string | undefined = undefined
    let conflictReason: string | undefined = undefined

    if (sampleOccupiedIndexes.includes(i)) {
      status = "occupied"
      physicalState = "OCCUPIED"

      // Demonstrate simultaneous OCCUPIED + PROTECTED state (SRS 8.2 & 3.2.2)
      if (i === sampleOccupiedIndexes[0]) {
        reservationState = "PROTECTED"
        isProtected = true
        conflictReason =
          "Protection window active for incoming booking while previous session completes"
      }
    } else if (i === count && count > 5) {
      status = "maintenance"
      physicalState = "MAINTENANCE"
    } else if (i === count - 1 && count > 7) {
      status = "reserved"
      physicalState = "AVAILABLE"
      reservationState = "RESERVED"
    } else if (i === 3 && count >= 8) {
      // Dynamic Protection Window demonstration (SRS 3.4.4 & 8.2)
      status = "reserved"
      physicalState = "AVAILABLE"
      reservationState = "PROTECTED"
      isProtected = true
    } else if (i === 4 && count >= 8) {
      // Active Pending Payment hold demonstration (SRS 3.4.3 & 8.2)
      status = "reserved"
      physicalState = "AVAILABLE"
      reservationState = "PENDING_PAYMENT"
      isPendingPayment = true
    } else if (i === 5 && count >= 10) {
      // Designated Backup Capacity demonstration (SRS 3.4.5 & 8.2)
      status = "available"
      physicalState = "AVAILABLE"
      reservationState = "BACKUP"
      isBackup = true
    } else if (i === 6 && count >= 10) {
      // Reallocated slot demonstration (SRS 3.4.6 & Section 10)
      status = "reserved"
      physicalState = "AVAILABLE"
      reservationState = "RESERVED"
      requestedSlotCode = `${prefix}01` // Driver requested A01, but is allocated A06
    }

    const item: ParkingSpaceItem = {
      id: `space-${zoneId}-${code.toLowerCase()}`,
      code,
      zoneId,
      floorId,
      lotId,
      vehicleType,
      status,
      physicalState,
      reservationState,
      isProtected,
      isBackup,
      isPendingPayment,
      requestedSlotCode,
      conflictReason,
      protectionStartTime: isProtected
        ? new Date(now.getTime() - 1800000).toISOString()
        : undefined,
      allocationTime:
        status === "reserved" || isProtected
          ? new Date(now.getTime() - 3600000).toISOString()
          : undefined,
      lastUpdated: new Date(now.getTime() - i * 600000).toISOString(),
    }

    if (
      physicalState === "OCCUPIED" ||
      reservationState === "RESERVED" ||
      isProtected
    ) {
      item.currentBooking = {
        bookingId: `BK-20261008-0${i}`,
        driverName: i % 2 === 0 ? "Trần Văn Nam" : "Lê Thị Thu",
        licensePlate:
          vehicleType === "car"
            ? `51A-${10000 + i * 111}`
            : `59-K1 ${20000 + i * 222}`,
        startTime: new Date(now.getTime() - 3600000).toISOString(),
        endTime: new Date(now.getTime() + 7200000).toISOString(),
        isAllocated: true,
        isReallocated: Boolean(requestedSlotCode && requestedSlotCode !== code),
        confirmedAt: new Date(now.getTime() - 86400000).toISOString(),
      }
    } else if (status === "maintenance") {
      item.maintenanceNote =
        "Sensor recalibration and ground marking maintenance in progress."
    }

    spaces.push(item)
  }

  return spaces
}

const DEFAULT_POLICY: LotPolicySummary = {
  protectionWindowHours: 1,
  reservationProtectionWindowMinutes: 60,
  allocationLeadTimeMinutes: 30,
  occupancyValidityHours: 4,
  occupancyValidityTimespanHours: 4,
  backupCapacityPercentage: 8,
  priorityPolicy: "EARLIEST_START_TIME_FIRST",
  reallocationCandidateHierarchy: [
    "REQUESTED_SLOT",
    "SAME_ZONE",
    "NEAREST_COMPATIBLE",
  ],
}

function buildDefaultStructure(
  lotId: string,
  lotName: string,
  address: string,
): ParkingStructureData {
  const policy: LotPolicySummary = { ...DEFAULT_POLICY }

  if (lotId === "lot-002") {
    // Bitexco Outdoor Lot (Ground Floor, Car & EV/Motorcycle Zones)
    const floorG: ParkingFloorItem = {
      id: "fl-lot-002-g",
      lotId,
      name: "Ground Level (Mặt đất)",
      code: "G",
      status: "active",
      zones: [
        {
          id: "zn-002-car-a",
          floorId: "fl-lot-002-g",
          lotId,
          name: "Zone A - Ô Tô & Xe Điện",
          vehicleType: "car",
          status: "active",
          spaces: generateInitialSpaces(
            24,
            "A",
            "zn-002-car-a",
            "fl-lot-002-g",
            lotId,
            "car",
            [2, 7, 12, 18],
          ),
        },
        {
          id: "zn-002-car-b",
          floorId: "fl-lot-002-g",
          lotId,
          name: "Zone B - Ô Tô Tiêu Chuẩn",
          vehicleType: "car",
          status: "active",
          spaces: generateInitialSpaces(
            16,
            "B",
            "zn-002-car-b",
            "fl-lot-002-g",
            lotId,
            "car",
            [1, 5, 9],
          ),
        },
        {
          id: "zn-002-moto",
          floorId: "fl-lot-002-g",
          lotId,
          name: "Zone M - Xe Máy",
          vehicleType: "motorcycle",
          status: "active",
          spaces: generateInitialSpaces(
            8,
            "M",
            "zn-002-moto",
            "fl-lot-002-g",
            lotId,
            "motorcycle",
            [2, 4],
          ),
        },
      ],
    }

    return {
      lotId,
      lotName,
      address,
      floors: [floorG],
      policy,
    }
  }

  if (lotId === "lot-001") {
    // Vinhomes Grand Park Multi-storey: 2 floors
    const floorB1: ParkingFloorItem = {
      id: "fl-001-b1",
      lotId,
      name: "Tầng Hầm B1",
      code: "B1",
      status: "active",
      zones: [
        {
          id: "zn-001-b1-car",
          floorId: "fl-001-b1",
          lotId,
          name: "Khu Ô Tô B1",
          vehicleType: "car",
          status: "active",
          spaces: generateInitialSpaces(
            20,
            "A",
            "zn-001-b1-car",
            "fl-001-b1",
            lotId,
            "car",
            [1, 4, 8, 12],
          ),
        },
        {
          id: "zn-001-b1-moto",
          floorId: "fl-001-b1",
          lotId,
          name: "Khu Xe Máy B1",
          vehicleType: "motorcycle",
          status: "active",
          spaces: generateInitialSpaces(
            20,
            "M",
            "zn-001-b1-moto",
            "fl-001-b1",
            lotId,
            "motorcycle",
            [3, 6, 9],
          ),
        },
      ],
    }

    const floorB2: ParkingFloorItem = {
      id: "fl-001-b2",
      lotId,
      name: "Tầng Hầm B2",
      code: "B2",
      status: "active",
      zones: [
        {
          id: "zn-001-b2-car",
          floorId: "fl-001-b2",
          lotId,
          name: "Khu Ô Tô B2",
          vehicleType: "car",
          status: "active",
          spaces: generateInitialSpaces(
            25,
            "B",
            "zn-001-b2-car",
            "fl-001-b2",
            lotId,
            "car",
            [2, 5, 11],
          ),
        },
      ],
    }

    return {
      lotId,
      lotName,
      address,
      floors: [floorB1, floorB2],
      policy,
    }
  }

  // Generic fallback for any other site
  const defaultFloor: ParkingFloorItem = {
    id: `fl-${lotId}-1`,
    lotId,
    name: "Tầng 1 (Trệt)",
    code: "1F",
    status: "active",
    zones: [
      {
        id: `zn-${lotId}-car`,
        floorId: `fl-${lotId}-1`,
        lotId,
        name: "Khu Ô Tô",
        vehicleType: "car",
        status: "active",
        spaces: generateInitialSpaces(
          18,
          "A",
          `zn-${lotId}-car`,
          `fl-${lotId}-1`,
          lotId,
          "car",
          [1, 6],
        ),
      },
      {
        id: `zn-${lotId}-moto`,
        floorId: `fl-${lotId}-1`,
        lotId,
        name: "Khu Xe Máy",
        vehicleType: "motorcycle",
        status: "active",
        spaces: generateInitialSpaces(
          12,
          "M",
          `zn-${lotId}-moto`,
          `fl-${lotId}-1`,
          lotId,
          "motorcycle",
          [2, 5],
        ),
      },
    ],
  }

  return {
    lotId,
    lotName,
    address,
    floors: [defaultFloor],
    policy,
  }
}

function normalizeStructure(data: ParkingStructureData): ParkingStructureData {
  if (!data) {
    return buildDefaultStructure(
      "lot-002",
      "Bitexco Financial Tower Outdoor Lot",
      "2 Hải Triều, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
    )
  }

  data.policy = {
    ...DEFAULT_POLICY,
    ...(data.policy || {}),
    reservationProtectionWindowMinutes:
      data.policy?.reservationProtectionWindowMinutes ??
      (data.policy?.protectionWindowHours
        ? data.policy.protectionWindowHours * 60
        : 60),
    allocationLeadTimeMinutes: data.policy?.allocationLeadTimeMinutes ?? 30,
    occupancyValidityTimespanHours:
      data.policy?.occupancyValidityTimespanHours ??
      data.policy?.occupancyValidityHours ??
      4,
    backupCapacityPercentage: data.policy?.backupCapacityPercentage ?? 8,
  }

  if (!data.floors || !Array.isArray(data.floors) || data.floors.length === 0) {
    const fallback = buildDefaultStructure(
      data.lotId || "lot-002",
      data.lotName || "Parking Lot",
      data.address || "Ho Chi Minh City",
    )
    data.floors = fallback.floors
  }

  for (const floor of data.floors) {
    if (!floor.zones || !Array.isArray(floor.zones)) {
      floor.zones = []
    }
    for (const zone of floor.zones) {
      if (!zone.spaces || !Array.isArray(zone.spaces)) {
        zone.spaces = []
      }
      for (const space of zone.spaces) {
        if (!space.physicalState) {
          space.physicalState =
            space.status === "occupied"
              ? "OCCUPIED"
              : space.status === "maintenance"
                ? "MAINTENANCE"
                : space.status === "disabled"
                  ? "UNAVAILABLE"
                  : "AVAILABLE"
        }
        if (!space.reservationState) {
          space.reservationState = space.isProtected
            ? "PROTECTED"
            : space.isPendingPayment
              ? "PENDING_PAYMENT"
              : space.isBackup
                ? "BACKUP"
                : space.status === "reserved"
                  ? "RESERVED"
                  : "NONE"
        }
      }
    }
  }

  return data
}

function loadAllStructures(): Record<string, ParkingStructureData> {
  try {
    const raw = localStorage.getItem(STRUCTURES_KEY)
    if (raw) return JSON.parse(raw)
  } catch {
    // fallback
  }
  return {}
}

function saveAllStructures(data: Record<string, ParkingStructureData>) {
  try {
    localStorage.setItem(STRUCTURES_KEY, JSON.stringify(data))
    window.dispatchEvent(new Event("sp-structure-change"))
  } catch (err) {
    console.error("Failed to save parking structures", err)
  }
}

export const structureStore = {
  getStructure(
    lotId: string,
    fallbackName = "Parking Lot",
    fallbackAddress = "Ho Chi Minh City",
  ): ParkingStructureData {
    const all = loadAllStructures()
    if (all[lotId]) {
      const normalized = normalizeStructure(all[lotId])
      all[lotId] = normalized
      saveAllStructures(all)
      return JSON.parse(JSON.stringify(normalized))
    }

    // Check main store lots
    const lot = mainStore.getLots().find((l) => l.id === lotId)
    const newStructure = normalizeStructure(
      buildDefaultStructure(
        lotId,
        lot?.name || fallbackName,
        lot?.address || fallbackAddress,
      ),
    )
    all[lotId] = newStructure
    saveAllStructures(all)
    return JSON.parse(JSON.stringify(newStructure))
  },

  saveStructure(data: ParkingStructureData) {
    const all = loadAllStructures()
    all[data.lotId] = normalizeStructure(data)
    saveAllStructures(all)
  },

  // Authoritative SRS Capacity Accounting Invariant:
  // Available Capacity = Total Capacity − Occupied − Protected − Pending Payment − Backup
  // Distinct units are counted without double-subtraction for overlapping dimensions (e.g. Occupied + Protected)
  calculateStats(data?: ParkingStructureData | null): CapacityAccountingStats {
    if (!data || !data.floors || !Array.isArray(data.floors)) {
      return {
        totalFloors: 0,
        totalSpaces: 0,
        occupiedSpaces: 0,
        reservedSpaces: 0,
        protectedSpaces: 0,
        pendingPaymentHolds: 0,
        backupCapacity: 0,
        maintenanceSpaces: 0,
        disabledSpaces: 0,
        availableCapacity: 0,
        byCategory: {
          car: {
            total: 0,
            occupied: 0,
            reserved: 0,
            protected: 0,
            pendingPayment: 0,
            backup: 0,
            maintenance: 0,
            available: 0,
          },
          motorcycle: {
            total: 0,
            occupied: 0,
            reserved: 0,
            protected: 0,
            pendingPayment: 0,
            backup: 0,
            maintenance: 0,
            available: 0,
          },
        },
      }
    }

    const totalFloors = data.floors.length
    let totalSpaces = 0
    let occupiedSpaces = 0
    let reservedSpaces = 0
    let protectedSpaces = 0
    let pendingPaymentHolds = 0
    let backupCapacity = 0
    let maintenanceSpaces = 0
    let disabledSpaces = 0
    let unavailableDistinctCount = 0

    const carStats: CategoryCapacity = {
      total: 0,
      occupied: 0,
      reserved: 0,
      protected: 0,
      pendingPayment: 0,
      backup: 0,
      maintenance: 0,
      available: 0,
    }

    const motoStats: CategoryCapacity = {
      total: 0,
      occupied: 0,
      reserved: 0,
      protected: 0,
      pendingPayment: 0,
      backup: 0,
      maintenance: 0,
      available: 0,
    }

    for (const floor of data.floors) {
      if (!floor.zones || !Array.isArray(floor.zones)) continue
      for (const zone of floor.zones) {
        if (!zone.spaces || !Array.isArray(zone.spaces)) continue
        for (const space of zone.spaces) {
          totalSpaces++
          const isCar = space.vehicleType === "car"
          const target = isCar ? carStats : motoStats
          target.total++

          const isOcc =
            space.physicalState === "OCCUPIED" || space.status === "occupied"
          const isProt =
            space.isProtected || space.reservationState === "PROTECTED"
          const isRes =
            (space.reservationState === "RESERVED" ||
              space.status === "reserved") &&
            !isProt &&
            !space.isPendingPayment
          const isPending = Boolean(
            space.isPendingPayment ||
              space.reservationState === "PENDING_PAYMENT",
          )
          const isBack = Boolean(
            space.isBackup || space.reservationState === "BACKUP",
          )
          const isMaint =
            space.physicalState === "MAINTENANCE" ||
            space.status === "maintenance"
          const isDis =
            space.physicalState === "UNAVAILABLE" || space.status === "disabled"

          if (isOcc) {
            occupiedSpaces++
            target.occupied++
          }
          if (isRes) {
            reservedSpaces++
            target.reserved++
          }
          if (isProt) {
            protectedSpaces++
            target.protected++
          }
          if (isPending) {
            pendingPaymentHolds++
            target.pendingPayment++
          }
          if (isBack) {
            backupCapacity++
            target.backup++
          }
          if (isMaint) {
            maintenanceSpaces++
            target.maintenance++
          }
          if (isDis) {
            disabledSpaces++
          }

          // Any dimension that restricts ordinary walk-in capacity:
          if (
            isOcc ||
            isProt ||
            isRes ||
            isPending ||
            isBack ||
            isMaint ||
            isDis
          ) {
            unavailableDistinctCount++
          } else {
            target.available++
          }
        }
      }
    }

    const availableCapacity = Math.max(
      0,
      totalSpaces - unavailableDistinctCount,
    )

    return {
      totalFloors,
      totalSpaces,
      occupiedSpaces,
      reservedSpaces,
      protectedSpaces,
      pendingPaymentHolds,
      backupCapacity,
      maintenanceSpaces,
      disabledSpaces,
      availableCapacity,
      byCategory: {
        car: carStats,
        motorcycle: motoStats,
      },
    }
  },

  addFloor(lotId: string, name: string, code: string) {
    const all = loadAllStructures()
    const current = structureStore.getStructure(lotId)
    const newFloorId = `fl-${lotId}-${Date.now().toString(36)}`
    const newZoneId = `zn-${newFloorId}-car`

    const newFloor: ParkingFloorItem = {
      id: newFloorId,
      lotId,
      name: name.trim(),
      code: code.trim().toUpperCase(),
      status: "active",
      zones: [
        {
          id: newZoneId,
          floorId: newFloorId,
          lotId,
          name: `Khu Ô Tô ${code.trim().toUpperCase()}`,
          vehicleType: "car",
          status: "active",
          spaces: generateInitialSpaces(
            12,
            code.trim().toUpperCase(),
            newZoneId,
            newFloorId,
            lotId,
            "car",
          ),
        },
      ],
    }

    current.floors.push(newFloor)
    structureStore.saveStructure(current)
    return newFloor
  },

  updateFloor(
    lotId: string,
    floorId: string,
    updates: Partial<Pick<ParkingFloorItem, "name" | "code" | "status">>,
  ) {
    const current = structureStore.getStructure(lotId)
    current.floors = current.floors.map((f) =>
      f.id === floorId ? { ...f, ...updates } : f,
    )
    structureStore.saveStructure(current)
  },

  deleteFloor(lotId: string, floorId: string) {
    const current = structureStore.getStructure(lotId)
    current.floors = current.floors.filter((f) => f.id !== floorId)
    structureStore.saveStructure(current)
  },

  addZone(
    lotId: string,
    floorId: string,
    name: string,
    vehicleType: VehicleCategory,
    initialSpacesCount = 10,
  ) {
    const current = structureStore.getStructure(lotId)
    const floor = current.floors.find((f) => f.id === floorId)
    if (!floor) return

    const newZoneId = `zn-${floorId}-${Date.now().toString(36)}`
    const prefix = vehicleType === "motorcycle" ? "M" : floor.code || "A"

    const newZone: ParkingZoneItem = {
      id: newZoneId,
      floorId,
      lotId,
      name: name.trim(),
      vehicleType,
      status: "active",
      spaces: generateInitialSpaces(
        initialSpacesCount,
        prefix,
        newZoneId,
        floorId,
        lotId,
        vehicleType,
      ),
    }

    floor.zones.push(newZone)
    structureStore.saveStructure(current)
    return newZone
  },

  updateZone(
    lotId: string,
    floorId: string,
    zoneId: string,
    updates: Partial<Pick<ParkingZoneItem, "name" | "vehicleType" | "status">>,
  ) {
    const current = structureStore.getStructure(lotId)
    const floor = current.floors.find((f) => f.id === floorId)
    if (!floor) return

    floor.zones = floor.zones.map((z) => {
      if (z.id !== zoneId) return z
      const updatedZone = { ...z, ...updates }
      if (updates.vehicleType && updates.vehicleType !== z.vehicleType) {
        updatedZone.spaces = updatedZone.spaces.map((s) => ({
          ...s,
          vehicleType: updates.vehicleType!,
        }))
      }
      return updatedZone
    })

    structureStore.saveStructure(current)
  },

  deleteZone(lotId: string, floorId: string, zoneId: string) {
    const current = structureStore.getStructure(lotId)
    const floor = current.floors.find((f) => f.id === floorId)
    if (!floor) return

    floor.zones = floor.zones.filter((z) => z.id !== zoneId)
    structureStore.saveStructure(current)
  },

  updateSpaceStatus(
    lotId: string,
    spaceId: string,
    newStatus: SpaceStatus,
    note?: string,
    options?: {
      physicalState?: PhysicalState
      reservationState?: ReservationProtectionState
      isProtected?: boolean
      isBackup?: boolean
    },
  ) {
    const current = structureStore.getStructure(lotId)
    let found = false

    for (const floor of current.floors) {
      for (const zone of floor.zones) {
        const space = zone.spaces.find((s) => s.id === spaceId)
        if (space) {
          space.status = newStatus
          space.lastUpdated = new Date().toISOString()

          // Physical State mapping
          if (options?.physicalState) {
            space.physicalState = options.physicalState
          } else if (newStatus === "available") {
            space.physicalState = "AVAILABLE"
          } else if (newStatus === "occupied") {
            space.physicalState = "OCCUPIED"
          } else if (newStatus === "maintenance") {
            space.physicalState = "MAINTENANCE"
          } else if (newStatus === "disabled") {
            space.physicalState = "UNAVAILABLE"
          }

          // Reservation / Protection State mapping
          if (options?.reservationState) {
            space.reservationState = options.reservationState
          } else if (newStatus === "available") {
            space.reservationState = "NONE"
            space.isProtected = false
            space.isPendingPayment = false
            space.currentBooking = undefined
          } else if (newStatus === "reserved") {
            space.reservationState = "RESERVED"
          }

          if (options?.isProtected !== undefined) {
            space.isProtected = options.isProtected
          }
          if (options?.isBackup !== undefined) {
            space.isBackup = options.isBackup
          }

          if (newStatus === "maintenance") {
            space.maintenanceNote =
              note || "Maintenance initiated by Owner/Operator"
          } else {
            space.maintenanceNote = undefined
          }

          found = true
          break
        }
      }
      if (found) break
    }

    if (found) {
      structureStore.saveStructure(current)
    }
  },
}
