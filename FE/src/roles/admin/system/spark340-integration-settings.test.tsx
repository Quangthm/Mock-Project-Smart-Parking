import React from "react"
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest"
import { IntegrationSettingsView } from "./IntegrationSettingsView"
import { SystemConfig } from "./SystemConfig"
import { PolicyDraftModal } from "./PolicyDraftModal"
import { EffectivePolicyView } from "./EffectivePolicyView"
import { CANONICAL_POLICY_GROUPS } from "../../../lib/policyDisplay"
import type { PolicyRevisionListItem } from "../../../lib/adminPolicyApi"

const api = vi.hoisted(() => ({
  getPaymentIntegration: vi.fn(), updatePaymentIntegration: vi.fn(), getNotificationIntegrations: vi.fn(),
  getCatalogue: vi.fn(), getRevisions: vi.fn(), getRevisionById: vi.fn(), getEffective: vi.fn(),
  createDraft: vi.fn(), updateDraft: vi.fn(), activateRevision: vi.fn(),
  getAdminFacilityDirectory: vi.fn(),
}))
vi.mock("../../../lib/adminIntegrationApi", () => api)
vi.mock("../../../lib/adminPolicyApi", () => api)
// Supply UI context only; all tested components, tabs, modal and effects are real.
vi.mock("../../../context/AppContext", () => ({ useApp: () => ({ accentColor: "#2563eb" }) }))

const channels = ["IN_APP", "PUSH", "SMS", "EMAIL"].map(channel => ({
  channel, provider: `TEST_${channel}`, isReady: true, status: "ACTIVE", description: channel, lastTestedAt: null,
}))
const catalogue = {
  revisionId: "platform-1", version: 1, effectiveFrom: "2026-01-01T00:00:00Z", cancellationTimezone: "Asia/Ho_Chi_Minh",
  policies: CANONICAL_POLICY_GROUPS.flatMap(group => group.keys).map(key => ({
    key, name: key, description: key, dataType: "duration", unit: "minutes", defaultValue: 15,
    isOverridableByOwner: false, bound: null,
  })),
}

const directory = {
  tenants: [
    { id: "tenant-test", code: "DEMO-OWNER", name: "SmartPark Demo Owner", status: "ACTIVE" },
    { id: "tenant-other", code: "", name: "Other Owner", status: "ACTIVE" },
  ],
  sites: [
    { id: "site-test", tenantId: "tenant-test", code: "DEMO-HQ", name: "SmartPark Demo Site", status: "ACTIVE", isActive: true },
    { id: "site-other", tenantId: "tenant-other", code: "", name: "Other Site", status: "INACTIVE", isActive: false },
  ],
}

function assertPaymentUnavailable(container: HTMLElement) {
  expect(screen.getByRole("heading", { name: "Payment configuration deferred" })).toBeDefined()
  expect(screen.getByText("PayOS configuration is unavailable in this MVP.")).toBeDefined()
  const card = screen.getByRole("heading", { name: "Payment configuration deferred" }).closest("section")!
  expect(card.querySelector("form, input, select, button")).toBeNull()
  expect(within(card).queryByText(/HEALTHY|LIVE|SANDBOX|FIXTURE/)).toBeNull()
  expect(container.querySelector('[id^="payment-"]')).toBeNull()
  expect(api.getPaymentIntegration).not.toHaveBeenCalled()
  expect(api.updatePaymentIntegration).not.toHaveBeenCalled()
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal("fetch", vi.fn(() => { throw new Error("Unmocked HTTP is forbidden in SPARK-340 component tests") }))
  api.getPaymentIntegration.mockImplementation(() => { throw new Error("Deferred Payment GET called") })
  api.updatePaymentIntegration.mockImplementation(() => { throw new Error("Deferred Payment PUT called") })
  api.getNotificationIntegrations.mockResolvedValue({ channels })
  api.getCatalogue.mockResolvedValue(catalogue)
  api.getAdminFacilityDirectory.mockResolvedValue(directory)
  api.getRevisions.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
  api.getEffective.mockResolvedValue({
    scope: "PLATFORM", tenantId: null, siteId: null, evaluatedAt: "2026-01-01T00:00:00Z",
    platformRevision: { id: "platform-1", version: 1 }, siteRevision: null, values: {},
    arrivalLateTolerance: { enabled: null, minutes: 20, inherited: true }, cancellationTimezone: "Asia/Ho_Chi_Minh",
  })
  api.createDraft.mockResolvedValue({ id: "new-draft", version: 2 })
  api.updateDraft.mockResolvedValue({ id: "existing-draft", version: 2 })
})
afterEach(() => { vi.unstubAllGlobals() })

describe("SPARK-340 PayOS-only MVP release", () => {
  it("mounts and remounts with zero Payment calls while retaining sorted Notification channels", async () => {
    const first = render(<IntegrationSettingsView />)
    await screen.findByText("Provider: TEST_EMAIL")
    assertPaymentUnavailable(first.container)
    const labels = Array.from(first.container.querySelectorAll("strong")).map(node => node.textContent)
    expect(labels).toEqual(["EMAIL", "SMS", "PUSH", "IN-APP"])
    expect(api.getNotificationIntegrations).toHaveBeenCalledTimes(1)
    first.unmount()
    const second = render(<IntegrationSettingsView />)
    await screen.findByText("Provider: TEST_IN_APP")
    expect(api.getNotificationIntegrations).toHaveBeenCalledTimes(2)
    assertPaymentUnavailable(second.container)
    expect(fetch).not.toHaveBeenCalled()
  })

  it("retains Notification loading, error and retry without touching Payment", async () => {
    let reject!: (error: Error) => void
    api.getNotificationIntegrations.mockImplementationOnce(() => new Promise((_, fail) => { reject = fail }))
    const view = render(<IntegrationSettingsView />)
    expect(view.container.querySelectorAll(".animate-pulse").length).toBe(4)
    assertPaymentUnavailable(view.container)
    await act(async () => { reject(new Error("Notification unavailable")) })
    expect((await screen.findByRole("alert")).textContent).toContain("Notification unavailable")
    await userEvent.click(screen.getByRole("button", { name: "Retry" }))
    await screen.findByText("Provider: TEST_EMAIL")
    expect(api.getNotificationIntegrations).toHaveBeenCalledTimes(2)
    expect(screen.queryByRole("alert")).toBeNull()
    assertPaymentUnavailable(view.container)
  })

  it("retains Notification empty state", async () => {
    api.getNotificationIntegrations.mockResolvedValue({ channels: [] })
    const view = render(<IntegrationSettingsView />)
    await screen.findByText("No notification channels reported.")
    assertPaymentUnavailable(view.container)
  })

  it("keeps real policy/effective/integration tab navigation and policy draft creation", async () => {
    const user = userEvent.setup()
    const view = render(<SystemConfig />)
    await screen.findByRole("heading", { name: "Platform Policy Catalogue" })
    await waitFor(() => expect(api.getCatalogue).toHaveBeenCalled())
    expect(screen.getByRole("heading", { name: "Policy Revisions" })).toBeDefined()
    await user.click(screen.getByRole("button", { name: "Create draft" }))
    const dialog = await screen.findByRole("dialog", { name: "Create policy draft" })
    await user.click(within(dialog).getByRole("button", { name: "Create draft" }))
    await waitFor(() => expect(api.createDraft).toHaveBeenCalledTimes(1))
    // F8 permits explicit null or omission, never a Site-owned boolean.
    expect(api.createDraft.mock.calls[0][0].arrivalLateToleranceEnabled).toBeNull()
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull())

    const policies = screen.getByRole("tab", { name: "Operational Policies" })
    policies.focus()
    fireEvent.keyDown(policies, { key: "ArrowRight" })
    const effective = screen.getByRole("tab", { name: "Effective Policy" })
    expect(document.activeElement).toBe(effective)
    expect(effective.getAttribute("aria-selected")).toBe("true")
    await screen.findByRole("heading", { name: "Effective Policy Simulation" })
    await waitFor(() => expect(api.getEffective).toHaveBeenCalled())
    expect(await screen.findByRole("option", { name: "SmartPark Demo Owner (DEMO-OWNER)" })).toBeDefined()
    expect(screen.getByRole("option", { name: "SmartPark Demo Site (DEMO-HQ)" })).toBeDefined()
    await user.selectOptions(screen.getByLabelText("Tenant (optional)"), "tenant-test")
    expect(screen.getByRole("button", { name: "Evaluate" }).hasAttribute("disabled")).toBe(true)
    await user.selectOptions(screen.getByLabelText("Parking site (optional)"), "site-test")
    await user.click(screen.getByRole("button", { name: "Evaluate" }))
    await waitFor(() => expect(api.getEffective).toHaveBeenLastCalledWith(expect.objectContaining({ tenantId: "tenant-test", siteId: "site-test" })))

    await user.click(screen.getByRole("tab", { name: "Integrations" }))
    await screen.findByText("Provider: TEST_EMAIL")
    assertPaymentUnavailable(view.container)
    await user.click(policies)
    await screen.findByRole("heading", { name: "Platform Policy Catalogue" })
    expect(api.getCatalogue.mock.calls.length).toBeGreaterThan(1)
    expect(fetch).not.toHaveBeenCalled()
  })

  it("filters sites, synchronizes the tenant, clears stale pairs and resets to platform", async () => {
    const user = userEvent.setup()
    render(<EffectivePolicyView />)
    await screen.findByRole("option", { name: "SmartPark Demo Site (DEMO-HQ)" })
    const tenant = screen.getByLabelText<HTMLSelectElement>("Tenant (optional)")
    const site = screen.getByLabelText<HTMLSelectElement>("Parking site (optional)")
    expect(within(site).getByRole("option", { name: "Other Site [INACTIVE]" })).toBeDefined()
    expect(Array.from(tenant.options).some(option => option.text.includes("tenant-test"))).toBe(false)
    expect(Array.from(site.options).some(option => option.text.includes("site-test"))).toBe(false)

    await user.selectOptions(site, "site-test")
    expect(tenant.value).toBe("tenant-test")
    expect(within(site).queryByRole("option", { name: "Other Site [INACTIVE]" })).toBeNull()
    await user.selectOptions(tenant, "tenant-other")
    expect(site.value).toBe("")
    expect(screen.getByRole("button", { name: "Evaluate" }).hasAttribute("disabled")).toBe(true)
    expect(within(site).queryByRole("option", { name: "SmartPark Demo Site (DEMO-HQ)" })).toBeNull()
    await user.selectOptions(site, "site-other")
    await user.selectOptions(site, "")
    expect(tenant.value).toBe("")
    expect(site.value).toBe("")
    await user.selectOptions(site, "site-test")
    await user.selectOptions(tenant, "")
    expect(site.value).toBe("")
    await user.selectOptions(site, "site-test")
    await user.click(screen.getByRole("button", { name: "Reset to platform" }))
    expect(tenant.value).toBe("")
    expect(site.value).toBe("")
    await waitFor(() => expect(api.getEffective).toHaveBeenLastCalledWith({ tenantId: undefined, siteId: undefined, at: undefined }))
    expect(api.getPaymentIntegration).not.toHaveBeenCalled()
  })

  it.each(["pending", "failed"])("keeps platform evaluation available when the directory is %s", async state => {
    if (state === "pending") api.getAdminFacilityDirectory.mockReturnValue(new Promise(() => {}))
    else api.getAdminFacilityDirectory.mockRejectedValue(new Error("Directory unavailable"))
    render(<EffectivePolicyView />)
    await screen.findByText("Platform revision v1")
    expect(screen.getByRole("status").textContent).toContain(state === "pending" ? "Loading tenants" : "Platform evaluation is still available")
    expect(screen.getByLabelText("Tenant (optional)").hasAttribute("disabled")).toBe(true)
    expect(screen.getByRole("button", { name: "Evaluate" }).hasAttribute("disabled")).toBe(false)
    await userEvent.click(screen.getByRole("button", { name: "Evaluate" }))
    await waitFor(() => expect(api.getEffective).toHaveBeenCalledTimes(2))
    expect(screen.queryByRole("alert")).toBeNull()
  })

  it("retains PolicyDraftModal update with its version token", async () => {
    const onSaved = vi.fn(), onClose = vi.fn()
    const revision: PolicyRevisionListItem = {
      id: "existing-draft", version: 1, scope: "PLATFORM", status: "DRAFT", isActive: false,
      tenantId: null, siteId: null, effectiveFrom: "2026-01-01T00:00:00Z", effectiveUntil: null,
      createdAt: "2026-01-01T00:00:00Z", updatedAt: "2026-01-01T00:00:00Z",
      paymentHoldDurationMinutes: 15, minReservationDurationMinutes: 45, maxReservationDurationHours: 24,
      maxAdvanceBookingDays: 7, interBookingBufferMinutes: 0, reservationProtectionWindowHours: 2,
      allocationLeadTimeMinutes: 10, facilityFailureRefundLeadMinutes: 5, occupancyValidityTimespanHours: 1,
      arrivalLateToleranceMinutes: 20, arrivalLateToleranceEnabled: null, rapidCancellationCount: 3,
      rapidCancellationWindowMinutes: 60, rapidCancellationRestrictionMinutes: 1440, dailyCancellationLimit: 5,
      cancellationTimezone: "Asia/Ho_Chi_Minh", overrideBounds: {},
    }
    render(<PolicyDraftModal mode="edit" revision={revision} onSaved={onSaved} onClose={onClose} />)
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }))
    await waitFor(() => expect(api.updateDraft).toHaveBeenCalledTimes(1))
    expect(api.updateDraft.mock.calls[0][0]).toBe("existing-draft")
    expect(api.updateDraft.mock.calls[0][1]).toMatchObject({ minReservationDurationMinutes: 45, interBookingBufferMinutes: 0, arrivalLateToleranceEnabled: null })
    expect(api.updateDraft.mock.calls[0][2]).toBe("2026-01-01T00:00:00Z")
    expect(onSaved).toHaveBeenCalledOnce()
    expect(onClose).toHaveBeenCalledOnce()
  })
})
