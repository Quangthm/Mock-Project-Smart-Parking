import { describe, it, expect } from "vitest"
import { formatStatus } from "@/lib/formatStatus"
import {
  mapSlotVisualState,
  isSlotBookable,
  SLOT_CODE_PATTERN,
  type PhysicalState,
  type ReservationState,
} from "@/lib/operationalLayout"
import {
  isPaymentOrderId,
  isTrustedPayOsUrl,
  classifyPaymentStatus,
  type PaymentStatusResponse,
} from "@/lib/paymentApi"

describe("Unit: formatStatus", () => {
  it("returns 'Unknown' for null, undefined, or empty string", () => {
    expect(formatStatus(null)).toBe("Unknown")
    expect(formatStatus(undefined)).toBe("Unknown")
    expect(formatStatus("")).toBe("Unknown")
  })

  it("formats camelCase and kebab/snake_case properly with capitalized first letter", () => {
    expect(formatStatus("pendingVerification")).toBe("Pending verification")
    expect(formatStatus("PENDING_PAYMENT")).toBe("Pending payment")
    expect(formatStatus("in-progress-review")).toBe("In progress review")
    expect(formatStatus("AVAILABLE")).toBe("Available")
  })
})

describe("Unit: Operational Layout & Fail-Closed Safety (SPARK-228)", () => {
  it("validates slot mesh code pattern", () => {
    expect(SLOT_CODE_PATTERN.test("CarParkingSpace_008")).toBe(true)
    expect(SLOT_CODE_PATTERN.test("CarParkingSpace_008 2")).toBe(true)
    expect(SLOT_CODE_PATTERN.test("Invalid#Space!")).toBe(false)
  })

  it("CRITICAL: never maps null, undefined or unknown physical states to available", () => {
    expect(mapSlotVisualState(null)).toBe("unknown")
    expect(mapSlotVisualState(undefined)).toBe("unknown")
    expect(
      mapSlotVisualState({
        physicalState: "UNKNOWN" as PhysicalState,
        reservationState: "UNRESERVED",
      }),
    ).toBe("unknown")
    expect(
      mapSlotVisualState({
        physicalState: "INVALID_STATUS" as unknown as PhysicalState,
        reservationState: "UNRESERVED",
      }),
    ).toBe("unknown")
  })

  it("keeps occupied, maintenance, or unavailable slots in their physical visual state regardless of reservation state", () => {
    expect(
      mapSlotVisualState({
        physicalState: "OCCUPIED",
        reservationState: "PROTECTED",
      }),
    ).toBe("occupied")
    expect(
      mapSlotVisualState({
        physicalState: "MAINTENANCE",
        reservationState: "UNRESERVED",
      }),
    ).toBe("maintenance")
    expect(
      mapSlotVisualState({
        physicalState: "UNAVAILABLE",
        reservationState: "UNRESERVED",
      }),
    ).toBe("unavailable")
  })

  it("correctly overlays reservation states ONLY when physical state is AVAILABLE", () => {
    expect(
      mapSlotVisualState({
        physicalState: "AVAILABLE",
        reservationState: "UNRESERVED",
      }),
    ).toBe("available")
    expect(
      mapSlotVisualState({
        physicalState: "AVAILABLE",
        reservationState: "RESERVED",
      }),
    ).toBe("reserved")
    expect(
      mapSlotVisualState({
        physicalState: "AVAILABLE",
        reservationState: "PROTECTED",
      }),
    ).toBe("reserved")
    // BACKUP is distinct from RESERVED
    expect(
      mapSlotVisualState({
        physicalState: "AVAILABLE",
        reservationState: "BACKUP",
      }),
    ).toBe("backup")
  })

  it("isSlotBookable fails closed: only AVAILABLE + UNRESERVED is bookable by default", () => {
    expect(isSlotBookable(null)).toBe(false)
    expect(
      isSlotBookable({
        physicalState: "AVAILABLE",
        reservationState: "UNRESERVED",
      }),
    ).toBe(true)
    expect(
      isSlotBookable({
        physicalState: "AVAILABLE",
        reservationState: "RESERVED",
      }),
    ).toBe(false)
    expect(
      isSlotBookable({
        physicalState: "AVAILABLE",
        reservationState: "BACKUP",
      }),
    ).toBe(false)
    expect(
      isSlotBookable({
        physicalState: "OCCUPIED",
        reservationState: "UNRESERVED",
      }),
    ).toBe(false)
    expect(
      isSlotBookable({
        physicalState: "UNKNOWN",
        reservationState: "UNRESERVED",
      }),
    ).toBe(false)
  })

  it("isSlotBookable respects explicit server isBookable flag when provided", () => {
    expect(
      isSlotBookable({
        physicalState: "AVAILABLE",
        reservationState: "UNRESERVED",
        isBookable: false,
      }),
    ).toBe(false)
  })
})

describe("Unit: Payment Validations & State Classification", () => {
  it("validates payment order UUID format", () => {
    expect(isPaymentOrderId("a1b2c3d4-e5f6-4a1b-8c2d-1234567890ab")).toBe(true)
    expect(isPaymentOrderId("not-a-valid-uuid")).toBe(false)
    expect(isPaymentOrderId("")).toBe(false)
  })

  it("verifies trusted PayOS URL for checkout redirection", () => {
    expect(isTrustedPayOsUrl("https://pay.payos.vn/web/12345")).toBe(true)
    expect(isTrustedPayOsUrl("https://fake-phishing-payos.vn/web/12345")).toBe(false)
    expect(isTrustedPayOsUrl("http://pay.payos.vn/web/12345")).toBe(false)
    expect(isTrustedPayOsUrl("invalid-url")).toBe(false)
  })

  it("CRITICAL: classifies payment status distinctly - pending/failed/unknown never maps to success", () => {
    const baseStatus: PaymentStatusResponse = {
      transactionId: "tx-1",
      orderId: "ord-1",
      orderCode: 1001,
      amount: 50000,
      paymentMethod: "PAYOS",
      status: "PENDING",
      paidAt: null,
      isAuthoritative: true,
    }

    expect(classifyPaymentStatus(baseStatus)).toBe("pending")
    expect(
      classifyPaymentStatus({
        ...baseStatus,
        status: "SUCCESS",
        isAuthoritative: false,
      }),
    ).toBe("pending")
    expect(
      classifyPaymentStatus({
        ...baseStatus,
        status: "SUCCESS",
        isAuthoritative: true,
        paidAt: "2026-11-05T10:00:00Z",
      }),
    ).toBe("success")
    expect(classifyPaymentStatus({ ...baseStatus, status: "FAILED" })).toBe("failed")
    expect(classifyPaymentStatus({ ...baseStatus, status: "CANCELLED" })).toBe("failed")
    expect(classifyPaymentStatus({ ...baseStatus, status: "UNKNOWN" })).toBe("pending")
  })
})

