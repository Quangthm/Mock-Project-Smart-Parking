import type {
  User,
  ParkingLot,
  Booking,
  OwnerApplication,
  AuditLog,
  SupportTicket,
  ParkingSlot,
  DriverVehicle,
  AppNotification,
  NotificationType,
  Role,
  OwnerPolicyConfig,
  ParkingPass,
  WalletTransaction,
  PackageOrder,
  LotPolicyPdf,
} from "./types"

const KEYS = {
  users: "sp_users",

  lots: "sp_lots",

  bookings: "sp_bookings",

  applications: "sp_applications",

  auditLogs: "sp_audit_logs",

  tickets: "sp_tickets",

  vehicles: "sp_vehicles",

  notifications: "sp_notifications",

  passes: "sp_passes",

  packageOrders: "sp_package_orders",

  walletTransactions: "sp_wallet_transactions",

  currentUserId: "sp_current_user",
}

function get<T>(key: string): T[] {
  try {
    return JSON.parse(localStorage.getItem(key) || "[]")
  } catch {
    return []
  }
}

function set<T>(key: string, data: T[]) {
  localStorage.setItem(key, JSON.stringify(data))

  window.dispatchEvent(new Event("sp-data-change"))
}

function generateId() {
  return Math.random().toString(36).slice(2) + Date.now().toString(36)
}

function generateSlots(total: number, floors: number): ParkingSlot[] {
  const slots: ParkingSlot[] = []

  const perFloor = Math.ceil(total / floors)

  for (let f = 1; f <= floors; f++) {
    const section = String.fromCharCode(64 + Math.min(f, 26))

    for (let s = 1; s <= perFloor && slots.length < total; s++) {
      const statuses: ParkingSlot["status"][] = [
        "available",
        "available",
        "available",
        "occupied",
        "reserved",
      ]

      slots.push({
        id: generateId(),

        number: `${section}-${String(s).padStart(3, "0")}`,

        floor: f,

        status: statuses[Math.floor(Math.random() * statuses.length)],
      })
    }
  }

  return slots
}

function seedInitialData() {
  const users = get<User>(KEYS.users)

  if (users.find((u) => u.email === "admin@smartparking.vn")) {
    const lots = get<ParkingLot>(KEYS.lots)

    const updatedLots = lots.map((lot) => {
      if (lot.id === "lot-001") {
        return {
          ...lot,
          address: "RRQJ+72 Long Bình, Ho Chi Minh, Vietnam",
          lat: 10.838178,
          lng: 106.830064,
        }
      }

      if (lot.id === "lot-002") {
        if (
          lot.type !== "outdoor" ||
          lot.modelFileName !== "outdoor_parking_lot.glb" ||
          lot.modelUrl !== "/models/parking/outdoor_parking_lot.glb" ||
          lot.totalSlots !== 48 ||
          lot.slots?.length !== 48
        ) {
          const slots = generateSlots(48, 1)

          return {
            ...lot,

            name: "Bitexco Financial Tower Outdoor Lot",

            type: "outdoor" as const,

            floors: 1,

            totalSlots: 48,

            slots,

            devices: [
              { type: "camera", label: "AI Camera", enabled: true },

              { type: "barrier", label: "Barrier Gate", enabled: true },

              { type: "sensor", label: "Slot Sensor", enabled: true },
            ],

            modelFileName: "outdoor_parking_lot.glb",

            modelUrl: "/models/parking/outdoor_parking_lot.glb",
          }
        }
      }

      return lot
    })

    if (updatedLots.some((lot, index) => lot !== lots[index]))
      set(KEYS.lots, updatedLots)

    const bookings = get<Booking>(KEYS.bookings)

    const updatedBookings = bookings.map((b) => {
      if (
        b.lotId === "lot-002" &&
        (b.lotName.includes("B2") || b.slotNumber === "201")
      ) {
        return {
          ...b,

          lotName: "Bitexco Financial Tower Outdoor Lot",

          slotNumber: b.slotNumber === "201" ? "A-002" : b.slotNumber,
        }
      }

      return b
    })

    if (updatedBookings.some((b, i) => b !== bookings[i]))
      set(KEYS.bookings, updatedBookings)

    return
  }

  const admin: User = {
    id: "admin-001",

    email: "admin@smartparking.vn",

    name: "System Admin",

    role: "admin",

    password: "Admin@123",

    createdAt: new Date().toISOString(),

    onboardingComplete: true,

    policyAccepted: true,
  }

  set(KEYS.users, [admin])

  const sampleOwner: User = {
    id: "owner-sample",

    email: "owner@demo.com",

    name: "Nguyen Van A",

    role: "owner",

    password: "Owner@123!",

    phone: "0901234567",

    createdAt: new Date(Date.now() - 86400000 * 30).toISOString(),

    onboardingComplete: true,

    policyAccepted: true,
  }

  const sampleDriver: User = {
    id: "driver-sample",

    email: "driver@demo.com",

    name: "Tran Thi B",

    role: "driver",

    password: "Driver@123!",

    phone: "0912345678",

    createdAt: new Date(Date.now() - 86400000 * 7).toISOString(),

    onboardingComplete: true,

    policyAccepted: true,

    wallet: 250000,
  }

  const sampleOperator: User = {
    id: "operator-sample",

    email: "operator@demo.com",

    name: "Le Van C",

    role: "operator",

    password: "Operator@123!",

    ownerId: "owner-sample",

    createdAt: new Date(Date.now() - 86400000 * 14).toISOString(),

    onboardingComplete: true,
  }

  set(KEYS.users, [admin, sampleOwner, sampleDriver, sampleOperator])

  const lot1: ParkingLot = {
    id: "lot-001",

    ownerId: "owner-sample",

    name: "Vinhomes Grand Park Parking",

    type: "multi-storey",

    address: "RRQJ+72 Long Bình, Ho Chi Minh, Vietnam",

    totalSlots: 120,

    floors: 3,

    slots: generateSlots(120, 3),

    devices: [
      { type: "camera", label: "AI Camera", enabled: true },

      { type: "barrier", label: "Barrier Gate", enabled: true },

      { type: "sensor", label: "Slot Sensor", enabled: true },
    ],

    hourlyRate: 15000,

    dailyRate: 100000,

    nightRate: 50000,

    gracePeriodMinutes: 15,

    subscriptionMonthly: 350000,

    subscriptionYearly: 3500000,

    status: "active",

    lat: 10.838178,

    lng: 106.830064,

    modelFileName: "indoor_parking_lot.glb",

    modelUrl: "/models/parking/indoor_parking_lot.glb",

    createdAt: new Date(Date.now() - 86400000 * 30).toISOString(),
  }

  const lot2: ParkingLot = {
    id: "lot-002",

    ownerId: "owner-sample",

    name: "Bitexco Financial Tower Outdoor Lot",

    type: "outdoor",

    address: "2 Hải Triều, Bến Nghé, Quận 1, TP.HCM",

    totalSlots: 48,

    floors: 1,

    slots: generateSlots(48, 1),

    devices: [
      { type: "camera", label: "AI Camera", enabled: true },

      { type: "barrier", label: "Barrier Gate", enabled: true },

      { type: "sensor", label: "Slot Sensor", enabled: true },
    ],

    hourlyRate: 20000,

    dailyRate: 150000,

    nightRate: 60000,

    gracePeriodMinutes: 10,

    subscriptionMonthly: 450000,

    subscriptionYearly: 4500000,

    status: "active",

    lat: 10.771917,

    lng: 106.704894,

    modelFileName: "outdoor_parking_lot.glb",

    modelUrl: "/models/parking/outdoor_parking_lot.glb",

    createdAt: new Date(Date.now() - 86400000 * 25).toISOString(),
  }

  const lot3: ParkingLot = {
    id: "lot-003",

    ownerId: "owner-sample",

    name: "Nguyen Hue Boulevard Outdoor Lot",

    type: "outdoor",

    address: "Nguyễn Huệ, Bến Nghé, Quận 1, TP.HCM",

    totalSlots: 50,

    floors: 1,

    slots: generateSlots(50, 1),

    devices: [
      { type: "camera", label: "AI Camera", enabled: true },

      { type: "barrier", label: "Barrier Gate", enabled: true },
    ],

    hourlyRate: 10000,

    dailyRate: 80000,

    nightRate: 40000,

    gracePeriodMinutes: 15,

    subscriptionMonthly: 300000,

    subscriptionYearly: 3000000,

    status: "active",

    lat: 10.7745,

    lng: 106.7035,

    modelFileName: "outdoor_parking_lot.glb",

    modelUrl: "/models/parking/outdoor_parking_lot.glb",

    createdAt: new Date(Date.now() - 86400000 * 15).toISOString(),
  }

  set(KEYS.lots, [lot1, lot2, lot3])

  if (lot1.slots[0]) lot1.slots[0].status = "occupied"

  if (lot2.slots[1]) lot2.slots[1].status = "occupied"

  const bookings: Booking[] = [
    {
      id: "bk-001",

      driverId: "driver-sample",

      lotId: "lot-001",

      lotName: "Vinhomes Grand Park Parking",

      slotId: lot1.slots[0]?.id || "slot-1",

      slotNumber: lot1.slots[0]?.number || "A-001",

      licensePlate: "51A-12345",

      plateType: "vn",

      startTime: new Date(Date.now() - 3600000 * 3).toISOString(),

      endTime: new Date(Date.now() + 3600000).toISOString(),

      status: "checked-in",

      paymentMethod: "momo",

      amount: 60000,

      depositAmount: 30000,

      createdAt: new Date(Date.now() - 3600000 * 4).toISOString(),

      otpAttempts: 0,
    },

    {
      id: "bk-002",

      driverId: "driver-sample",

      lotId: "lot-002",

      lotName: "Bitexco Financial Tower Outdoor Lot",

      slotId: lot2.slots[1]?.id || "slot-2",

      slotNumber: lot2.slots[1]?.number || "A-002",

      licensePlate: "51AB-67890",

      plateType: "vn",

      startTime: new Date(Date.now() - 86400000 * 2).toISOString(),

      endTime: new Date(Date.now() - 86400000 * 2 + 3600000 * 2).toISOString(),

      status: "completed",

      paymentMethod: "vnpay",

      amount: 40000,

      depositAmount: 20000,

      createdAt: new Date(Date.now() - 86400000 * 2 - 1800000).toISOString(),

      otpAttempts: 0,
    },
  ]

  set(KEYS.bookings, bookings)

  const logs: AuditLog[] = [
    {
      id: "log-001",
      userId: "admin-001",
      userName: "System Admin",
      userRole: "admin",
      action: "SYSTEM_INIT",
      details: "System initialized",
      timestamp: new Date().toISOString(),
    },

    {
      id: "log-002",
      userId: "owner-sample",
      userName: "Nguyen Van A",
      userRole: "owner",
      action: "LOT_CREATED",
      details: "Created lot: Vinhomes Grand Park Parking",
      timestamp: new Date(Date.now() - 86400000 * 30).toISOString(),
    },

    {
      id: "log-003",
      userId: "driver-sample",
      userName: "Tran Thi B",
      userRole: "driver",
      action: "BOOKING_CREATED",
      details: "Booked slot 101 at Vinhomes Grand Park",
      timestamp: new Date(Date.now() - 3600000 * 4).toISOString(),
    },
  ]

  set(KEYS.auditLogs, logs)

  const apps: OwnerApplication[] = [
    {
      id: "app-001",

      ownerId: "owner-sample",

      ownerName: "Nguyen Van A",

      businessName: "Vinhomes Parking Management",

      lotType: "multi-storey",

      applicationData: { slots: 120, floors: 3 },

      status: "approved",

      submittedAt: new Date(Date.now() - 86400000 * 31).toISOString(),

      reviewedAt: new Date(Date.now() - 86400000 * 30).toISOString(),
    },
  ]

  set(KEYS.applications, apps)
}

export const store = {
  init: seedInitialData,

  getUsers: (): User[] => get(KEYS.users),

  saveUser: (user: User) => {
    const users = get<User>(KEYS.users).filter((u) => u.id !== user.id)

    set(KEYS.users, [...users, user])
  },

  deleteUser: (id: string) => {
    set(
      KEYS.users,
      get<User>(KEYS.users).filter((user) => user.id !== id),
    )
  },

  createUser: (data: Omit<User, "id" | "createdAt">): User => {
    const user: User = {
      ...data,
      id: generateId(),
      createdAt: new Date().toISOString(),
    }

    const users = get<User>(KEYS.users)

    set(KEYS.users, [...users, user])

    return user
  },

  findUserByEmail: (email: string): User | undefined =>
    get<User>(KEYS.users).find(
      (u) => u.email.toLowerCase() === email.toLowerCase(),
    ),

  findUserById: (id: string): User | undefined =>
    get<User>(KEYS.users).find((u) => u.id === id),

  getLots: (): ParkingLot[] => get(KEYS.lots),

  getLotsByOwner: (ownerId: string): ParkingLot[] =>
    get<ParkingLot>(KEYS.lots).filter((l) => l.ownerId === ownerId),

  saveLot: (lot: ParkingLot) => {
    const lots = get<ParkingLot>(KEYS.lots).filter((l) => l.id !== lot.id)

    set(KEYS.lots, [...lots, lot])
  },

  createLot: (data: Omit<ParkingLot, "id" | "createdAt">): ParkingLot => {
    const lot: ParkingLot = {
      ...data,
      id: generateId(),
      createdAt: new Date().toISOString(),
    }

    const lots = get<ParkingLot>(KEYS.lots)

    set(KEYS.lots, [...lots, lot])

    return lot
  },

  deleteLot: (id: string) => {
    set(
      KEYS.lots,
      get<ParkingLot>(KEYS.lots).filter((l) => l.id !== id),
    )
  },

  getBookings: (): Booking[] =>
    get<Booking>(KEYS.bookings).map((booking) => ({
      ...booking,
      paymentStatus:
        booking.paymentStatus ??
        (["completed", "checked-in", "confirmed"].includes(booking.status)
          ? "paid"
          : "pending"),
    })),

  getBookingsByDriver: (driverId: string): Booking[] =>
    get<Booking>(KEYS.bookings)
      .filter((b) => b.driverId === driverId)
      .map((booking) => ({
        ...booking,
        paymentStatus:
          booking.paymentStatus ??
          (["completed", "checked-in", "confirmed"].includes(booking.status)
            ? "paid"
            : "pending"),
      })),

  getBookingsByLot: (lotId: string): Booking[] =>
    get<Booking>(KEYS.bookings)
      .filter((b) => b.lotId === lotId)
      .map((booking) => ({
        ...booking,
        paymentStatus:
          booking.paymentStatus ??
          (["completed", "checked-in", "confirmed"].includes(booking.status)
            ? "paid"
            : "pending"),
      })),

  saveBooking: (booking: Booking) => {
    const bookings = get<Booking>(KEYS.bookings).filter(
      (b) => b.id !== booking.id,
    )

    set(KEYS.bookings, [
      ...bookings,
      {
        ...booking,
        paymentStatus:
          booking.paymentStatus ??
          (booking.status === "completed" ||
          booking.status === "checked-in" ||
          booking.status === "confirmed"
            ? "paid"
            : "pending"),
      },
    ])
  },

  createBooking: (
    data: Omit<Booking, "id" | "createdAt" | "otpAttempts">,
  ): Booking => {
    const booking: Booking = {
      ...data,
      id: generateId(),
      createdAt: new Date().toISOString(),
      otpAttempts: 0,
    }

    const bookings = get<Booking>(KEYS.bookings)

    set(KEYS.bookings, [...bookings, booking])

    return booking
  },

  getVehiclesByDriver: (driverId: string): DriverVehicle[] =>
    get<DriverVehicle>(KEYS.vehicles).filter(
      (vehicle) => vehicle.driverId === driverId,
    ),

  createVehicle: (
    data: Omit<DriverVehicle, "id" | "createdAt">,
  ): DriverVehicle | null => {
    const vehicles = get<DriverVehicle>(KEYS.vehicles)

    const driverVehicles = vehicles.filter(
      (vehicle) => vehicle.driverId === data.driverId,
    )

    const normalizedPlate = data.licensePlate
      .replace(/[^A-Z0-9]/gi, "")
      .toUpperCase()

    if (
      driverVehicles.length >= 3 ||
      driverVehicles.some(
        (vehicle) =>
          vehicle.licensePlate.replace(/[^A-Z0-9]/gi, "").toUpperCase() ===
          normalizedPlate,
      )
    )
      return null

    const vehicle: DriverVehicle = {
      ...data,
      id: generateId(),
      createdAt: new Date().toISOString(),
    }

    set(KEYS.vehicles, [...vehicles, vehicle])

    return vehicle
  },

  updateVehicle: (vehicle: DriverVehicle): boolean => {
    const vehicles = get<DriverVehicle>(KEYS.vehicles)

    const current = vehicles.find(
      (item) => item.id === vehicle.id && item.driverId === vehicle.driverId,
    )

    const normalizedPlate = vehicle.licensePlate
      .replace(/[^A-Z0-9]/gi, "")
      .toUpperCase()

    if (
      !current ||
      vehicles.some(
        (item) =>
          item.id !== vehicle.id &&
          item.driverId === vehicle.driverId &&
          item.licensePlate.replace(/[^A-Z0-9]/gi, "").toUpperCase() ===
            normalizedPlate,
      )
    )
      return false

    set(
      KEYS.vehicles,
      vehicles.map((item) =>
        item.id === vehicle.id && item.driverId === vehicle.driverId
          ? vehicle
          : item,
      ),
    )

    return true
  },

  deleteVehicle: (id: string, driverId: string) => {
    set(
      KEYS.vehicles,
      get<DriverVehicle>(KEYS.vehicles).filter(
        (vehicle) => vehicle.id !== id || vehicle.driverId !== driverId,
      ),
    )
  },

  getApplications: (): OwnerApplication[] => get(KEYS.applications),

  saveApplication: (app: OwnerApplication) => {
    const apps = get<OwnerApplication>(KEYS.applications).filter(
      (a) => a.id !== app.id,
    )

    set(KEYS.applications, [...apps, app])
  },

  createApplication: (
    data: Omit<OwnerApplication, "id" | "submittedAt">,
  ): OwnerApplication => {
    const app: OwnerApplication = {
      ...data,
      id: generateId(),
      submittedAt: new Date().toISOString(),
    }

    const apps = get<OwnerApplication>(KEYS.applications)

    set(KEYS.applications, [...apps, app])

    return app
  },

  getAuditLogs: (): AuditLog[] =>
    get<AuditLog>(KEYS.auditLogs).sort(
      (a, b) =>
        new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
    ),

  addAuditLog: (data: Omit<AuditLog, "id" | "timestamp">) => {
    const log: AuditLog = {
      ...data,
      id: generateId(),
      timestamp: new Date().toISOString(),
    }

    const logs = get<AuditLog>(KEYS.auditLogs)

    set(KEYS.auditLogs, [...logs, log])
  },

  getTickets: (): SupportTicket[] => get(KEYS.tickets),

  saveTicket: (ticket: SupportTicket) => {
    const tickets = get<SupportTicket>(KEYS.tickets).filter(
      (item) => item.id !== ticket.id,
    )

    set(KEYS.tickets, [...tickets, ticket])
  },

  createTicket: (
    data: Omit<SupportTicket, "id" | "createdAt" | "status">,
  ): SupportTicket => {
    const ticket: SupportTicket = {
      ...data,
      id: generateId(),
      status: "open",
      createdAt: new Date().toISOString(),
    }

    const tickets = get<SupportTicket>(KEYS.tickets)

    set(KEYS.tickets, [...tickets, ticket])

    return ticket
  },

  getPassesByDriver: (driverId: string): ParkingPass[] =>
    get<ParkingPass>(KEYS.passes).filter((pass) => pass.driverId === driverId),

  getPackageOrdersByDriver: (driverId: string): PackageOrder[] =>
    get<PackageOrder>(KEYS.packageOrders)
      .filter((order) => order.driverId === driverId)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),

  createPackageOrder: (
    data: Omit<PackageOrder, "id" | "createdAt" | "paymentStatus">,
  ): PackageOrder => {
    const order: PackageOrder = {
      ...data,
      id: generateId(),
      createdAt: new Date().toISOString(),
      paymentStatus: "pending",
    }

    set(KEYS.packageOrders, [...get<PackageOrder>(KEYS.packageOrders), order])

    return order
  },

  createPass: (data: Omit<ParkingPass, "id" | "purchasedAt">): ParkingPass => {
    const pass: ParkingPass = {
      ...data,
      id: generateId(),
      purchasedAt: new Date().toISOString(),
    }

    set(KEYS.passes, [...get<ParkingPass>(KEYS.passes), pass])

    return pass
  },

  updatePackageOrder: (order: PackageOrder) => {
    const orders = get<PackageOrder>(KEYS.packageOrders)

    const index = orders.findIndex((o) => o.id === order.id)

    if (index >= 0) orders[index] = order
    else orders.push(order)

    set(KEYS.packageOrders, orders)
  },

  getWalletTransactions: (userId: string): WalletTransaction[] =>
    get<WalletTransaction>(KEYS.walletTransactions)
      .filter((item) => item.userId === userId)
      .sort((a, b) => b.timestamp.localeCompare(a.timestamp)),

  addWalletTransaction: (data: Omit<WalletTransaction, "id" | "timestamp">) => {
    const transaction: WalletTransaction = {
      ...data,
      id: generateId(),
      timestamp: new Date().toISOString(),
    }

    set(KEYS.walletTransactions, [
      ...get<WalletTransaction>(KEYS.walletTransactions),
      transaction,
    ])
  },

  getNotifications: (recipientId: string): AppNotification[] =>
    get<AppNotification>(KEYS.notifications)

      .filter((notification) => notification.recipientId === recipientId)

      .sort((a, b) => b.timestamp.localeCompare(a.timestamp)),

  createNotification: (
    data: Omit<AppNotification, "id" | "timestamp" | "isRead"> & {
      timestamp?: string
    },
  ): AppNotification => {
    const notification: AppNotification = {
      ...data,
      id: generateId(),
      timestamp: data.timestamp ?? new Date().toISOString(),
      isRead: false,
    }

    set(KEYS.notifications, [
      ...get<AppNotification>(KEYS.notifications),
      notification,
    ])

    return notification
  },

  markNotificationRead: (id: string, recipientId: string) =>
    set(
      KEYS.notifications,
      get<AppNotification>(KEYS.notifications).map((item) =>
        item.id === id && item.recipientId === recipientId
          ? { ...item, isRead: true }
          : item,
      ),
    ),

  markAllNotificationsRead: (recipientId: string) =>
    set(
      KEYS.notifications,
      get<AppNotification>(KEYS.notifications).map((item) =>
        item.recipientId === recipientId ? { ...item, isRead: true } : item,
      ),
    ),

  notifyRole: (
    role: Role,
    type: NotificationType,
    title: string,
    message: string,
    relatedEntityId?: string,
  ) => {
    get<User>(KEYS.users)
      .filter((user) => user.role === role)
      .forEach((user) =>
        store.createNotification({
          recipientId: user.id,
          recipientRole: role,
          type,
          title,
          message,
          relatedEntityId,
        }),
      )
  },

  notifyUser: (
    recipientId: string,
    type: NotificationType,
    title: string,
    message: string,
    relatedEntityId?: string,
  ) => {
    const recipient = get<User>(KEYS.users).find(
      (user) => user.id === recipientId,
    )

    if (recipient)
      store.createNotification({
        recipientId,
        recipientRole: recipient.role,
        type,
        title,
        message,
        relatedEntityId,
      })
  },

  getOwnerPolicy: (ownerId: string): OwnerPolicyConfig | null => {
    try {
      return JSON.parse(
        localStorage.getItem(`sp_owner_policy_${ownerId}`) || "null",
      )
    } catch {
      return null
    }
  },

  saveOwnerPolicy: (ownerId: string, policy: OwnerPolicyConfig) => {
    localStorage.setItem(`sp_owner_policy_${ownerId}`, JSON.stringify(policy))

    window.dispatchEvent(new Event("sp-data-change"))
  },

  getLotPolicyPdf: (lotId: string, fallbackOwnerId?: string): LotPolicyPdf | null => {
    try {
      const stored = localStorage.getItem(`sp_lot_policy_pdf_${lotId}`)
      if (stored) return JSON.parse(stored)

      // Fallback to lot's owner policy PDF if exists
      const lots = get<ParkingLot>(KEYS.lots)
      const lot = lots.find((l) => l.id === lotId)
      const ownerId = fallbackOwnerId || lot?.ownerId
      if (ownerId) {
        const ownerPdf = localStorage.getItem(
          `sp_owner_policy_pdf_${ownerId}`,
        )
        if (ownerPdf) return JSON.parse(ownerPdf)
      }

      // Check if any owner policy PDF exists in storage
      const ownerKeys = Object.keys(localStorage).filter((k) =>
        k.startsWith("sp_owner_policy_pdf_"),
      )
      if (ownerKeys.length > 0) {
        const anyOwnerPdf = localStorage.getItem(ownerKeys[0])
        if (anyOwnerPdf) return JSON.parse(anyOwnerPdf)
      }

      // Default policy document for any parking lot
      if (lot) {
        return {
          lotId,
          ownerId: lot.ownerId,
          fileName: `Chinh_Sach_Quy_Dinh_${lot.name.replace(/\s+/g, "_")}.pdf`,
          fileSize: "186 KB",
          fileData: "",
          uploadedAt: lot.createdAt || new Date().toISOString(),
          title: `Chính sách & Quy định Đỗ xe: ${lot.name}`,
          summaryText: `Quy định vận hành bãi đỗ ${lot.name}. Áp dụng cho toàn bộ phương tiện lưu thông và đỗ tại cơ sở ${lot.address}.`,
        }
      }
      return null
    } catch {
      return null
    }
  },

  saveLotPolicyPdf: (lotId: string, pdf: LotPolicyPdf) => {
    localStorage.setItem(`sp_lot_policy_pdf_${lotId}`, JSON.stringify(pdf))
    window.dispatchEvent(new Event("sp-data-change"))
  },

  getOwnerPolicyPdf: (ownerId: string): LotPolicyPdf | null => {
    try {
      const stored = localStorage.getItem(`sp_owner_policy_pdf_${ownerId}`)
      return stored ? JSON.parse(stored) : null
    } catch {
      return null
    }
  },

  saveOwnerPolicyPdf: (ownerId: string, pdf: LotPolicyPdf) => {
    localStorage.setItem(`sp_owner_policy_pdf_${ownerId}`, JSON.stringify(pdf))
    window.dispatchEvent(new Event("sp-data-change"))
  },

  deleteLotPolicyPdf: (lotId: string) => {
    localStorage.removeItem(`sp_lot_policy_pdf_${lotId}`)
    window.dispatchEvent(new Event("sp-data-change"))
  },

  remindExpiringBookings: () => {
    const now = Date.now()

    const notifications = get<AppNotification>(KEYS.notifications)

    store
      .getBookings()
      .filter(
        (booking) =>
          booking.status === "checked-in" && booking.paymentStatus === "paid",
      )
      .forEach((booking) => {
        const remaining = new Date(booking.endTime).getTime() - now

        if (
          remaining > 0 &&
          remaining <= 15 * 60_000 &&
          !notifications.some(
            (item) =>
              item.type === "PARKING_EXPIRING" &&
              item.relatedEntityId === booking.id,
          )
        ) {
          store.notifyUser(
            booking.driverId,
            "PARKING_EXPIRING",
            "Parking session reminder",
            "Your parking session will expire in 15 minutes.",
            booking.id,
          )
        }
      })
  },

  getCurrentUserId: (): string | null =>
    localStorage.getItem(KEYS.currentUserId),

  setCurrentUserId: (id: string | null) => {
    if (id) localStorage.setItem(KEYS.currentUserId, id)
    else localStorage.removeItem(KEYS.currentUserId)
  },

  generateId,

  generateSlots,
}
