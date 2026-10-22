/**
 * ============================================================================
 * Domain Types Contract for SmartParking Frontend
 * Document Ref: tempfe/specs/driver-features-spec.md & tempfe/design-system.md
 * ============================================================================
 */

export type UserRole = "driver" | "owner" | "operator" | "admin"

export interface User {
  id: string
  name: string
  email: string
  phone?: string
  role: UserRole
  onboardingComplete?: boolean
  wallet?: number
  createdAt: string
}

/* --------------------------------------------------------------------------
   Vehicle Domain Types
   -------------------------------------------------------------------------- */
export type VehicleType = "CAR" | "MOTORCYCLE"

export interface RegisteredVehicle {
  id: string
  rawPlate: string
  canonicalPlate: string
  vehicleType: VehicleType
  imageReference: string | null
  brand?: string
}

export interface VehicleInput {
  plate: string
  vehicleType: VehicleType
  imageReference: string
}

/* --------------------------------------------------------------------------
   Parking Lot & Slot Domain Types
   -------------------------------------------------------------------------- */
export type SlotStatus = "available" | "occupied" | "reserved" | "maintenance" | "disabled" | "selected"
export type LotType = "outdoor" | "basement" | "multi-storey"

export interface ParkingSlot {
  id: string
  number: string
  floor: number
  status: SlotStatus
  reservedUntil?: string
}

export type LotStatus = "active" | "maintenance" | "inactive"
export type LotStructureType = "outdoor" | "multilevel" | "underground"

export interface ParkingLot {
  id: string
  name: string
  address: string
  lat: number
  lng: number
  totalSlots: number
  hourlyRate: number
  status: LotStatus
  type: LotStructureType
  floors: number
  slots: ParkingSlot[]
  subscriptionMonthly: number
  subscriptionYearly: number
  modelUrl?: string
}

/* --------------------------------------------------------------------------
   Booking Domain Types
   -------------------------------------------------------------------------- */
export type BookingStatus = "confirmed" | "checked-in" | "completed" | "cancelled"
export type PaymentStatus = "pending" | "paid" | "failed" | "refunded"
export type PaymentMethod = "momo" | "vnpay" | "qr" | "visa" | "applepay" | "zalopay"

export interface Booking {
  id: string
  lotId: string
  lotName: string
  slotId: string
  slotNumber: string
  licensePlate: string
  plateType?: "domestic" | "foreign"
  startTime: string
  endTime: string
  amount: number
  status: BookingStatus
  paymentStatus: PaymentStatus
  paymentMethod?: PaymentMethod
  completedAt?: string
}

/* --------------------------------------------------------------------------
   Commuter Pass / Subscription Types
   -------------------------------------------------------------------------- */
export type PassPlan = "monthly" | "annual"
export type PassStatus = "active" | "expired" | "suspended"

export interface PassPackage {
  id: string
  driverId: string
  lotId: string
  lotName: string
  vehiclePlate: string
  plan: PassPlan
  amount: number
  qrCodeToken: string
  expiresAt: string
  status: PassStatus
}

/* --------------------------------------------------------------------------
   Support & Incident Ticket Types
   -------------------------------------------------------------------------- */
export type TicketStatus = "processing" | "resolved"

export interface SupportTicket {
  id: string
  userId: string
  userName: string
  subject: string
  message: string
  status: TicketStatus
  bookingId?: string
  createdAt: string
  resolvedAt?: string
}
