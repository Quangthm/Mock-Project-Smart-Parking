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

    function Wrapper() {
      const [value, setValue] = React.useState("")
      const [recovery, setRecovery] = React.useState(false)

      const handleFilteredChange = (raw: string) => {
        // Enforce numeric only if not recovery mode
        const filtered = recovery ? raw : raw.replace(/\D/g, "").slice(0, 6)
        setValue(filtered)
      }

      return (
        <MfaCodeInput
          recovery={recovery}
          value={value}
          onChange={handleFilteredChange}
          onMethodChange={setRecovery}
        />
      )
    }

    render(<Wrapper />)

    const input = screen.getByRole("textbox") as HTMLInputElement
    expect(input).toBeDefined()
    expect(input.getAttribute("pattern")).toBe("[0-9]{6}")
    expect(input.getAttribute("inputmode")).toBe("numeric")

    // Attempt to type non-numeric characters and digits
    await user.type(input, "abc123xyz456")

    // Assert that non-numeric characters are rejected/filtered and max 6 digits are kept
    expect(input.value).toBe("123456")

    // Switch to recovery code mode
    const toggleButton = screen.getByRole("button", { name: /use a recovery code/i })
    await user.click(toggleButton)

    // Verify recovery mode attributes and reset behavior
    expect(input.value).toBe("")
    expect(input.getAttribute("inputmode")).toBe("text")
  })
})

