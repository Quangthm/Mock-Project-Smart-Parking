export type SpaceStatus = "available" | "occupied" | "reserved" | "maintenance" | "disabled"
export type VehicleCategory = "car" | "motorcycle"

// SRS Canonical State Dimensions (SRS 3.2.2 & 3.4.2)
export type PhysicalState = "AVAILABLE" | "OCCUPIED" | "UNKNOWN" | "MAINTENANCE" | "UNAVAILABLE"
export type ReservationProtectionState = "RESERVED" | "PROTECTED" | "BACKUP" | "PENDING_PAYMENT" | "NONE"
export type ReservationLifecycleState = "PENDING_PAYMENT" | "CONFIRMED" | "ALLOCATED" | "PARKING" | "COMPLETED" | "EXPIRED" | "CANCELLED" | "NO_SHOW" | "UNFULFILLABLE"

export interface ParkingSpaceItem {
  id: string
  code: string
  zoneId: string
  floorId: string
  lotId: string
  vehicleType: VehicleCategory
  // Legacy status preserved for backwards compatibility:
  status: SpaceStatus
  // Canonical SRS states:
  physicalState?: PhysicalState
  reservationState?: ReservationProtectionState
  reservationLifecycle?: ReservationLifecycleState
  isBackup?: boolean
  isProtected?: boolean
  isPendingPayment?: boolean
  requestedSlotCode?: string // requested parking position vs actual allocated position
  protectionStartTime?: string
  allocationTime?: string
  conflictReason?: string
  currentBooking?: {
    bookingId: string
    driverName: string
    licensePlate: string
    startTime: string
    endTime: string
    isAllocated?: boolean
    isReallocated?: boolean
    confirmedAt?: string
  }
  maintenanceNote?: string
  lastUpdated: string
}

export interface ParkingZoneItem {
  id: string
  floorId: string
  lotId: string
  name: string
  vehicleType: VehicleCategory
  status: "active" | "inactive"
  spaces: ParkingSpaceItem[]
}

export interface ParkingFloorItem {
  id: string
  lotId: string
  name: string
  code: string
  status: "active" | "inactive"
  zones: ParkingZoneItem[]
}

export interface LotPolicySummary {
  protectionWindowHours?: number
  reservationProtectionWindowMinutes?: number
  allocationLeadTimeMinutes?: number
  occupancyValidityHours?: number
  occupancyValidityTimespanHours?: number
  backupCapacityPercentage?: number
  priorityPolicy?: string
  reallocationCandidateHierarchy?: string[]
}

export interface ParkingStructureData {
  lotId: string
  lotName: string
  address: string
  floors: ParkingFloorItem[]
  policy?: LotPolicySummary
}

export interface CategoryCapacity {
  total: number
  occupied: number
  reserved: number
  protected: number
  pendingPayment: number
  backup: number
  maintenance: number
  available: number
}

export interface CapacityAccountingStats {
  totalFloors: number
  totalSpaces: number
  occupiedSpaces: number
  reservedSpaces: number
  protectedSpaces: number
  pendingPaymentHolds: number
  backupCapacity: number
  maintenanceSpaces: number
  disabledSpaces: number
  availableCapacity: number // Invariant: Total - Occupied - Protected - PendingPayment - Backup (counted once)
  byCategory: {
    car: CategoryCapacity
    motorcycle: CategoryCapacity
  }
}

export type PaymentTransactionStatus = "paid" | "pending" | "failed" | "refunded"

export interface PaymentTransactionRecord {
  id: string
  driverId: string
  bookingId: string
  lotId: string
  lotName: string
  spaceCode?: string
  amount: number
  paymentMethod: "qr" | "momo" | "vnpay" | "visa" | "applepay" | "zalopay"
  paymentStatus: PaymentTransactionStatus
  createdAt: string
  failureReason?: string
  refundInfo?: {
    refundedAt: string
    amount: number
    reason?: string
  }
}
