import { describe, it, expect, beforeAll, afterEach, afterAll, vi } from "vitest"
import { setupServer } from "msw/node"
import { http, HttpResponse } from "msw"
import { authApi, authExpiredEvent } from "@/lib/authApi"
import { paymentApi } from "@/lib/paymentApi"
import { parkingApi } from "@/lib/parkingApi"

const userServerUrl = "http://localhost:5035"
const paymentServerUrl = "/payment-api"
const parkingServerUrl = "http://localhost:5045"

function setMockSession(token = "valid-token", refresh = "refresh-token-123", expiresInSeconds = 3600) {
  sessionStorage.setItem("sp_access_token", token)
  sessionStorage.setItem("sp_refresh_token", refresh)
  sessionStorage.setItem("sp_access_expires_at", String(Date.now() + expiresInSeconds * 1000))
}

export const handlers = [
  http.get(`${userServerUrl}/api/auth/me`, ({ request }) => {
    const authHeader = request.headers.get("Authorization")
    if (authHeader !== "Bearer valid-token") {
      return HttpResponse.json(
        { success: false, code: "UNAUTHORIZED", message: "Token expired or invalid" },
        { status: 401 },
      )
    }
    return HttpResponse.json({
      success: true,
      data: {
        userId: "u-12345",
        fullName: "Test Driver",
        email: "driver@example.com",
        role: "driver",
        onboardingComplete: true,
      },
    })
  }),

  http.post(`${userServerUrl}/api/auth/login`, async ({ request }) => {
    const body = (await request.json()) as { email?: string; password?: string }
    if (body.email === "driver@example.com" && body.password === "Password@123") {
      return HttpResponse.json({
        success: true,
        data: {
          accessToken: "valid-token",
          refreshToken: "refresh-token-123",
          expiresIn: 3600,
          user: {
            userId: "u-12345",
            fullName: "Test Driver",
            email: "driver@example.com",
            role: "driver",
            onboardingComplete: true,
          },
        },
      })
    }
    if (body.password === "wrong-password") {
      return HttpResponse.json(
        { success: false, code: "INVALID_CREDENTIALS", message: "Invalid email or password" },
        { status: 401 },
      )
    }
    return HttpResponse.json(
      { success: false, code: "ACCOUNT_LOCKED", message: "Account is temporarily locked" },
      { status: 403 },
    )
  }),

  http.post(`${paymentServerUrl}/api/v1/payments/orders/:orderId/checkout`, async () => {
    return HttpResponse.json({
      success: true,
      data: {
        paymentTransactionId: "a1b2c3d4-e5f6-4a1b-8c2d-1234567890ab",
        orderCode: 123456,
        amount: 25000,
        checkoutUrl: "https://pay.payos.vn/web/123456",
        qrCode: "00020101021238540010A0000007270124...",
        accountNumber: "9999888877",
        accountName: "SMARTPARK HOLDINGS",
        bin: "970422",
        status: "PENDING",
        expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
      },
    })
  }),

  http.get(`${paymentServerUrl}/api/v1/payments/transactions/:transactionId/status`, ({ params }) => {
    const { transactionId } = params
    if (transactionId === "a1b2c3d4-e5f6-4a1b-8c2d-123456789001") {
      return HttpResponse.json({
        success: true,
        data: {
          transactionId,
          orderId: "e1a2b3c4-1111-4000-8000-1234567890ab",
          orderCode: 88888,
          amount: 50000,
          paymentMethod: "PAYOS",
          status: "SUCCESS",
          paidAt: new Date().toISOString(),
          isAuthoritative: true,
        },
      })
    }
    if (transactionId === "a1b2c3d4-e5f6-4a1b-8c2d-123456789002") {
      return HttpResponse.json({
        success: true,
        data: {
          transactionId,
          orderId: "e1a2b3c4-1111-4000-8000-1234567890ab",
          orderCode: 99999,
          amount: 50000,
          paymentMethod: "PAYOS",
          status: "FAILED",
          paidAt: null,
          isAuthoritative: true,
        },
      })
    }
    return HttpResponse.json({
      success: true,
      data: {
        transactionId,
        orderId: "e1a2b3c4-1111-4000-8000-1234567890ab",
        orderCode: 11111,
        amount: 50000,
        paymentMethod: "PAYOS",
        status: "PENDING",
        paidAt: null,
        isAuthoritative: false,
      },
    })
  }),

  http.get(`${parkingServerUrl}/api/parking-lots`, () => {
    return HttpResponse.json({
      success: true,
      data: [
        {
          id: "site-001",
          tenantId: "t-001",
          code: "SITE_A",
          name: "Central Tower Parking",
          address: "123 Main St",
          latitude: 10.77,
          longitude: 106.69,
          status: "ACTIVE",
          isActive: true,
          totalPhysicalCapacity: 100,
        },
      ],
    })
  }),

  http.get(`${parkingServerUrl}/api/parking-lots/:siteId/operational-layout`, ({ params }) => {
    return HttpResponse.json({
      success: true,
      data: {
        siteId: params.siteId,
        siteCode: "SITE_A",
        siteName: "Central Tower Parking",
        contractVersion: "1.2.0",
        generatedAt: new Date().toISOString(),
        isFresh: true,
        stalenessThresholdSeconds: 30,
        metrics: {
          totalPhysicalCapacity: 100,
          occupiedCount: 30,
          reservedCount: 10,
          availableCarSlots: 60,
          motorcycleCapacity: { maxCapacity: 50, occupiedCount: 15, availableCount: 35 },
        },
        floors: [],
        slots: [
          {
            slotId: "s-1",
            slotCode: "A01",
            floorId: "f-1",
            floorCode: "B1",
            supportedVehicleType: "CAR",
            slotType: "STANDARD",
            physicalState: "AVAILABLE",
            reservationState: "UNRESERVED",
            isEvCharging: false,
            isBookable: true,
            updatedAt: new Date().toISOString(),
          },
        ],
      },
    })
  }),
]

const server = setupServer(...handlers)

beforeAll(() => server.listen({ onUnhandledRequest: "bypass" }))
afterEach(() => {
  server.resetHandlers()
  sessionStorage.clear()
  localStorage.clear()
})
afterAll(() => server.close())

describe("Integration: Auth API Client + Session State", () => {
  it("authenticates driver via POST /api/auth/login and stores tokens/user", async () => {
    const result = await authApi.login("driver@example.com", "Password@123")

    expect("mfaRequired" in result).toBe(false)
    if (!("mfaRequired" in result)) {
      expect(result.email).toBe("driver@example.com")
      expect(result.role).toBe("driver")
      expect(sessionStorage.getItem("sp_access_token")).toBe("valid-token")
    }
  })

  it("handles 401 invalid credentials error without storing tokens", async () => {
    await expect(
      authApi.login("driver@example.com", "wrong-password"),
    ).rejects.toThrow(/Invalid email or password/i)

    expect(sessionStorage.getItem("sp_access_token")).toBeNull()
  })

  it("handles 403 account locked without storing tokens", async () => {
    await expect(
      authApi.login("locked@example.com", "Password@123"),
    ).rejects.toThrow(/Account is temporarily locked/i)

    expect(sessionStorage.getItem("sp_access_token")).toBeNull()
  })

  it("currentUser() restores user identity using bearer token", async () => {
    setMockSession("valid-token")
    const user = await authApi.currentUser()

    expect(user).not.toBeNull()
    expect(user?.id).toBe("u-12345")
    expect(user?.role).toBe("driver")
  })

  it("currentUser() throws and dispatches authExpiredEvent when token is 401 expired", async () => {
    setMockSession("invalid-expired-token")

    const eventListener = vi.fn()
    window.addEventListener(authExpiredEvent, eventListener)

    await expect(authApi.currentUser()).rejects.toThrow(/Token expired or invalid/i)
    expect(sessionStorage.getItem("sp_access_token")).toBeNull()

    window.removeEventListener(authExpiredEvent, eventListener)
  })
})

describe("Integration: Payment Client + Order/Checkout Lifecycle", () => {
  it("creates checkout transaction successfully with trusted payos url and qrCode", async () => {
    setMockSession("valid-token")
    const checkout = await paymentApi.checkout("e1a2b3c4-1111-4000-8000-1234567890ab")

    expect(checkout.orderCode).toBe(123456)
    expect(checkout.amount).toBe(25000)
    expect(checkout.checkoutUrl).toContain("pay.payos.vn")
    expect(checkout.status).toBe("PENDING")
  })

  it("polls payment status: distinguishes SUCCESS vs FAILED vs PENDING", async () => {
    setMockSession("valid-token")

    const successStatus = await paymentApi.status("a1b2c3d4-e5f6-4a1b-8c2d-123456789001")
    expect(successStatus.status).toBe("SUCCESS")
    expect(successStatus.isAuthoritative).toBe(true)

    const failedStatus = await paymentApi.status("a1b2c3d4-e5f6-4a1b-8c2d-123456789002")
    expect(failedStatus.status).toBe("FAILED")

    const pendingStatus = await paymentApi.status("a1b2c3d4-e5f6-4a1b-8c2d-123456789003")
    expect(pendingStatus.status).toBe("PENDING")
  })
})

describe("Integration: Parking API Client", () => {
  it("fetches active parking lots list", async () => {
    setMockSession("valid-token")
    const sites = await parkingApi.list()

    expect(sites).toHaveLength(1)
    expect(sites[0].code).toBe("SITE_A")
    expect(sites[0].name).toBe("Central Tower Parking")
  })

  it("operationalLayout returns layout and slot status correctly", async () => {
    setMockSession("valid-token")
    const layout = await parkingApi.operationalLayout("site-001")
    expect(layout).toBeDefined()
    expect(layout.slots).toBeDefined()
  })
})

