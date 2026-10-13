export type Role = "driver" | "owner" | "operator" | "admin"

export type OperatorAccessRole = "financial" | "operation" | "cashier"

export type LotType = "outdoor" | "basement" | "multi-storey"

export type SlotStatus = "available" | "occupied" | "reserved" | "maintenance" | "disabled"

export type BookingStatus = "pending" | "confirmed" | "checked-in" | "completed" | "cancelled"

export type PaymentStatus = "pending" | "paid" | "failed"

export type PaymentMethod = "visa" | "applepay" | "momo" | "vnpay" | "zalopay" | "qr"

export type VehicleType = "motorcycle" | "car"

export interface DriverVehicle {
  id: string

  driverId: string

  type: VehicleType

  licensePlate: string

  plateType: "vn" | "foreign"

  plateImageUrl?: string

  createdAt: string
}

export interface User {
  id: string

  email: string

  phone?: string

  name: string

  role: Role

  password: string

  avatar?: string

  ownerId?: string

  operatorRole?: OperatorAccessRole

  operatorSiteId?: string | "all"

  onboardingComplete?: boolean

  policyAccepted?: boolean

  createdAt: string

  lockedUntil?: string

  wallet?: number

  accountStatus?: "active" | "suspended" | "locked"

  lastLoginAt?: string
}

export interface ParkingSlot {
  id: string

  number: string

  floor: number

  status: SlotStatus

  reservedUntil?: string

  reservedBy?: string
}

export interface Device {
  type: "camera" | "rfid" | "ev-charger" | "sensor" | "barrier"

  label: string

  enabled: boolean
}

export interface ParkingLot {
  id: string

  ownerId: string

  name: string

  type: LotType

  address: string

  slots: ParkingSlot[]

  totalSlots: number

  floors: number

  devices: Device[]

  hourlyRate: number

  dailyRate: number

  nightRate: number

  gracePeriodMinutes: number

  subscriptionMonthly: number

  subscriptionYearly: number

  status: "active" | "inactive" | "pending"

  lat: number

  lng: number

  modelUrl?: string

  modelFileName?: string

  createdAt: string
}

export interface Booking {
  id: string

  driverId: string

  lotId: string

  lotName: string

  slotId: string

  slotNumber: string

  licensePlate: string

  plateType: "vn" | "foreign"

  plateImageUrl?: string

  startTime: string

  endTime: string

  status: BookingStatus

  paymentStatus?: PaymentStatus

  completedAt?: string

  paymentMethod?: PaymentMethod

  amount: number

  depositAmount: number

  createdAt: string

  otpAttempts: number
}

export interface OwnerApplication {
  id: string

  ownerId: string

  ownerName: string

  businessName: string

  lotType: LotType

  applicationData: Record<string, unknown>

  status: "pending" | "approved" | "rejected"

  submittedAt: string

  reviewedAt?: string

  reviewNote?: string
}

export interface AuditLog {
  id: string

  userId: string

  userName: string

  userRole: Role

  action: string

  details: string

  timestamp: string
}

export interface SupportTicket {
  id: string

  userId: string

  userName: string

  subject: string

  message: string

  bookingId?: string

  status: "open" | "in-progress" | "resolved"

  createdAt: string
}

export type NotificationType =
  | "PAYMENT_SUCCESS"
  | "PAYMENT_FAILED"
  | "PARKING_EXPIRING"
  | "LOGIN_SUCCESS"
  | "PACKAGE_PURCHASE_SUCCESS"
  | "TICKET_SUBMITTED"
  | "TICKET_RESOLVED"
  | "NEW_APPLICATION"
  | "NEW_DRIVER"
  | "DRIVER_PARKED"
  | "EMPLOYEE_CREATED"
  | "POLICY_UPDATED"
  | "PASSWORD_RESET"
  | "SITE_REASSIGNED"

export interface AppNotification {
  id: string

  recipientId: string

  recipientRole: Role

  type: NotificationType

  title: string

  message: string

  timestamp: string

  isRead: boolean

  relatedEntityId?: string
}

export interface OwnerPolicyConfig {
  pricingEnabled: boolean

  dayRate: string

  nightRate: string

  depositEnabled: boolean

  depositMode: "percentage" | "fixed"

  depositValue: string
}

export interface ParkingPass {
  id: string

  driverId: string

  lotId: string

  lotName: string

  plan: "monthly" | "annual"

  amount: number

  purchasedAt: string

  expiresAt: string
}

export interface PackageOrder {
  id: string

  driverId: string

  lotId: string

  lotName: string

  plan: "monthly" | "annual"

  amount: number

  paymentMethod: PaymentMethod

  paymentStatus: "pending" | "paid" | "failed"

  createdAt: string
}

export interface WalletTransaction {
  id: string

  userId: string

  description: string

  amount: number

  timestamp: string
}

export interface LotPolicyPdf {
  lotId?: string

  ownerId?: string

  fileName: string

  fileSize?: string

  fileData: string

  uploadedAt: string

  title?: string

  summaryText?: string
}
