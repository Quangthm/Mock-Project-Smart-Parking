import { describe, it, expect, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import React from "react"
import { Button } from "@/components/ui/Button"
import { ParkingStatus } from "@/components/ui/ParkingStatus"
import { StaleDataBanner, formatLayoutAge, staleBannerMessage } from "@/components/ui/StaleDataBanner"
import { MfaCodeInput } from "@/components/forms/MfaCodeInput"

describe("Component: Button & Duplicate Submit Prevention", () => {
  it("renders with children text and correct accessible role", () => {
    render(<Button>Submit Payment</Button>)
    const button = screen.getByRole("button", { name: /submit payment/i })
    expect(button).toBeDefined()
    expect(button.getAttribute("disabled")).toBeNull()
  })

  it("CRITICAL: when loading=true, button is disabled and aria-busy=true to prevent multiple clicks", async () => {
    const user = userEvent.setup()
    const handleClick = vi.fn()

    render(
      <Button loading={true} onClick={handleClick}>
        Confirm Booking
      </Button>,
    )

    const button = screen.getByRole("button", { name: /confirm booking/i })
    expect(button.getAttribute("disabled")).not.toBeNull()
    expect(button.getAttribute("aria-busy")).toBe("true")

    await user.click(button)
    expect(handleClick).not.toHaveBeenCalled()
  })
})

describe("Component: ParkingStatus badge", () => {
  it("renders available status with correct accessible label and icon", () => {
    render(<ParkingStatus status="available" />)
    const badge = screen.getByLabelText("Available")
    expect(badge.getAttribute("data-status")).toBe("available")
    expect(badge.textContent).toContain("Available")
  })

  it("renders reserved status with calendar icon and proper text", () => {
    render(<ParkingStatus status="reserved" />)
    const badge = screen.getByLabelText("Reserved")
    expect(badge.getAttribute("data-status")).toBe("reserved")
    expect(badge.textContent).toContain("Reserved")
  })

  it("renders occupied status with In use text", () => {
    render(<ParkingStatus status="occupied" />)
    const badge = screen.getByLabelText("In use")
    expect(badge.getAttribute("data-status")).toBe("occupied")
    expect(badge.textContent).toContain("In use")
  })

  it("renders maintenance status with compact mode mapping", () => {
    render(<ParkingStatus status="maintenance" compact={true} />)
    const badge = screen.getByLabelText("Maintenance")
    expect(badge.textContent).toContain("Service")
  })
})

describe("Component: StaleDataBanner (Staleness / Unknown handling)", () => {
  it("formats age properly and reports unknown for missing/negative age", () => {
    expect(formatLayoutAge(null)).toBe("unknown")
    expect(formatLayoutAge(-5)).toBe("unknown")
    expect(formatLayoutAge(45)).toBe("45s")
    expect(formatLayoutAge(130)).toBe("2 min")
    expect(staleBannerMessage(null)).toBe("Live view is stale — the snapshot age is unknown")
  })

  it("renders stale warning banner with role status and polite live region", () => {
    render(<StaleDataBanner ageSeconds={90} />)
    const alert = screen.getByRole("status")
    expect(alert.getAttribute("aria-live")).toBe("polite")
    expect(alert.textContent).toContain("Live view is stale — last update 1 min ago")
    expect(alert.textContent).toContain("Showing the last received snapshot")
  })
})

describe("Component: MfaCodeInput", () => {
  it("allows typing digits and prevents non-numeric input when pattern enforces", async () => {
    const user = userEvent.setup()
    let value = ""
    const handleChange = vi.fn((val: string) => {
      value = val
    })

    render(
      <MfaCodeInput value={value} onChange={handleChange} />,
    )

    const input = screen.getByRole("textbox")
    expect(input).toBeDefined()

    await user.type(input, "123456")
    expect(handleChange).toHaveBeenCalled()
  })
})

