import { useState, type FormEvent } from "react"
import { useApp } from "../../../context/AppContext"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { ownerData as store } from "../data/data"
import type { LotType, ParkingLot, ParkingSlot, User } from "../../../lib/types"
import { ALLOWED_PARKING_MODELS } from "../../../lib/3d/parking3DConfig"

interface SiteFormState {
  name: string
  address: string
  type: LotType
  totalSlots: string
  floors: string
  hourlyRate: string
  dailyRate: string
  nightRate: string
  gracePeriodMinutes: string
  lat: string
  lng: string
  status: "active" | "inactive"
}

const emptyForm: SiteFormState = {
  name: "",
  address: "",
  type: "outdoor",
  totalSlots: "",
  floors: "1",
  hourlyRate: "15000",
  dailyRate: "100000",
  nightRate: "50000",
  gracePeriodMinutes: "15",
  lat: "10.7769",
  lng: "106.7009",
  status: "active",
}

const fieldClass = "input w-full text-sm py-2"

export function SiteManagement({
  sites,
  operators,
  onSiteCreated,
}: {
  sites: ParkingLot[]
  operators: User[]
  onSiteCreated: () => void
}) {
  const { user } = useApp()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingSite, setEditingSite] = useState<ParkingLot | null>(null)
  const [form, setForm] = useState<SiteFormState>(emptyForm)
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  function openCreateModal() {
    setEditingSite(null)
    setForm(emptyForm)
    setError("")
    setIsModalOpen(true)
  }

  function openEditModal(site: ParkingLot) {
    setEditingSite(site)
    setForm({
      name: site.name,
      address: site.address,
      type: site.type,
      totalSlots: String(site.totalSlots),
      floors: String(site.floors),
      hourlyRate: String(site.hourlyRate || 15000),
      dailyRate: String(site.dailyRate || 100000),
      nightRate: String(site.nightRate || 50000),
      gracePeriodMinutes: String(site.gracePeriodMinutes || 15),
      lat: String(site.lat || 10.7769),
      lng: String(site.lng || 106.7009),
      status: site.status === "inactive" ? "inactive" : "active",
    })
    setError("")
    setIsModalOpen(true)
  }

  function closeModal() {
    setIsModalOpen(false)
    setEditingSite(null)
    setError("")
  }

  function buildSlots(existing: ParkingSlot[], count: number, floors: number) {
    const protectedSlots = existing.filter(
      (slot) => slot.status !== "available",
    )
    if (protectedSlots.length > count) return null

    const keptSlots = [
      ...protectedSlots,
      ...existing.filter((slot) => slot.status === "available"),
    ].slice(0, count)
    const newCount = count - keptSlots.length
    const generated = newCount > 0 ? store.generateSlots(newCount, floors) : []
    const usedNumbers = new Set(keptSlots.map((slot) => slot.number))
    let nextNumber =
      Math.max(
        0,
        ...keptSlots.map((slot) => Number(slot.number.replace(/\D/g, "")) || 0),
      ) + 1
    const newSlots = generated.map((slot, index) => {
      while (usedNumbers.has(String(nextNumber))) nextNumber += 1
      const number = String(nextNumber++)
      usedNumbers.add(number)
      return {
        ...slot,
        number,
        status: "available" as const,
        reservedBy: undefined,
        reservedUntil: undefined,
        floor: ((keptSlots.length + index) % floors) + 1,
      }
    })

    const slotsPerFloor = Math.ceil(count / floors)
    return [...keptSlots, ...newSlots].map((slot, index) => ({
      ...slot,
      floor: Math.min(floors, Math.floor(index / slotsPerFloor) + 1),
    }))
  }

  async function saveSite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)
    setError("")

    const totalSlots = Number(form.totalSlots)
    const floors = form.type === "outdoor" ? 1 : Number(form.floors)
    const hourlyRate = Number(form.hourlyRate) || 15000
    const dailyRate = Number(form.dailyRate) || 100000
    const nightRate = Number(form.nightRate) || 50000
    const gracePeriodMinutes = Number(form.gracePeriodMinutes) || 15
    const lat = Number(form.lat) || 10.7769
    const lng = Number(form.lng) || 106.7009

    if (!user) {
      setError("You must be signed in as an Owner to configure parking sites.")
      setIsSubmitting(false)
      return
    }

    if (!form.name.trim() || !form.address.trim()) {
      setError("Please provide both the site name and street address.")
      setIsSubmitting(false)
      return
    }

    if (!Number.isInteger(totalSlots) || totalSlots < 1 || totalSlots > 2000) {
      setError("Total slots must be an integer between 1 and 2,000.")
      setIsSubmitting(false)
      return
    }

    if (!Number.isInteger(floors) || floors < 1 || floors > 20) {
      setError("Levels/floors count must be between 1 and 20.")
      setIsSubmitting(false)
      return
    }

    const slots = buildSlots(editingSite?.slots ?? [], totalSlots, floors)
    if (!slots) {
      setError(
        "The new slot count cannot be lower than the currently occupied or reserved slots.",
      )
      setIsSubmitting(false)
      return
    }

    try {
      const modelDef = ALLOWED_PARKING_MODELS[form.type]

      if (editingSite) {
        const updatedSite: ParkingLot = {
          ...editingSite,
          name: form.name.trim(),
          address: form.address.trim(),
          type: form.type,
          totalSlots,
          floors,
          slots,
          hourlyRate,
          dailyRate,
          nightRate,
          gracePeriodMinutes,
          lat,
          lng,
          status: form.status,
          modelFileName: editingSite.modelFileName || modelDef.fileName,
          modelUrl: editingSite.modelUrl || modelDef.path,
        }
        store.saveLot(updatedSite)
      } else {
        const newSite: Omit<ParkingLot, "id" | "createdAt"> = {
          ownerId: user.id,
          name: form.name.trim(),
          type: form.type,
          address: form.address.trim(),
          slots,
          totalSlots,
          floors,
          devices: [],
          hourlyRate,
          dailyRate,
          nightRate,
          gracePeriodMinutes,
          subscriptionMonthly: 800000,
          subscriptionYearly: 8000000,
          modelFileName: modelDef.fileName,
          modelUrl: modelDef.path,
          status: form.status,
          lat,
          lng,
        }
        store.createLot(newSite)
      }

      closeModal()
      onSiteCreated()
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not save parking site.",
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-[var(--fg)]">
            Site Management
          </h1>
          <p className="mt-1 text-sm text-[var(--muted)]">
            Manage parking locations, structural layout, operating hours, and
            assigned operators.
          </p>
        </div>
        <button type="button" className="btn-primary" onClick={openCreateModal}>
          <UntitledIcon name="plus" size={16} /> Add New Site
        </button>
      </header>

      {/* Sites Overview Table */}
      <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--card)]">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="border-b border-[var(--border)] bg-[var(--bg)]/50 text-xs uppercase tracking-wide text-[var(--muted)]">
            <tr>
              <th className="px-4 py-3">Site / Facility</th>
              <th className="px-4 py-3">Type</th>
              <th className="px-4 py-3">Capacity</th>
              <th className="px-4 py-3">Rates (Day / Night)</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Employees</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--border)]">
            {sites.map((site) => {
              const employeeCount = operators.filter(
                (operator) =>
                  operator.operatorSiteId === site.id ||
                  operator.operatorSiteId === "all",
              ).length
              return (
                <tr
                  key={site.id}
                  className="hover:bg-[var(--bg)]/40 transition-colors"
                >
                  <td className="px-4 py-3.5">
                    <div className="font-semibold text-[var(--fg)]">
                      {site.name}
                    </div>
                    <div className="text-xs text-[var(--muted)] truncate max-w-[280px]">
                      {site.address}
                    </div>
                  </td>
                  <td className="px-4 py-3.5 text-xs text-[var(--fg)] capitalize">
                    {site.type}
                  </td>
                  <td className="px-4 py-3.5 text-xs text-[var(--fg)]">
                    <div>{site.totalSlots} slots</div>
                    <div className="text-[var(--muted)]">
                      {site.floors} {site.floors === 1 ? "level" : "levels"}
                    </div>
                  </td>
                  <td className="px-4 py-3.5 text-xs">
                    <div>
                      {(site.hourlyRate || 15000).toLocaleString("vi-VN")}₫/h
                    </div>
                    <div className="text-[var(--muted)]">
                      {(site.nightRate || 50000).toLocaleString("vi-VN")}₫/night
                    </div>
                  </td>
                  <td className="px-4 py-3.5">
                    <span
                      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-semibold ${
                        site.status === "active"
                          ? "bg-green-500/15 text-green-600"
                          : "bg-red-500/15 text-red-600"
                      }`}
                    >
                      {site.status === "active" ? "Active" : "Closed"}
                    </span>
                  </td>
                  <td className="px-4 py-3.5 text-right font-medium text-[var(--fg)]">
                    {employeeCount}
                  </td>
                  <td className="px-4 py-3.5 text-right">
                    <button
                      type="button"
                      className="btn-outline text-xs px-2.5 py-1"
                      onClick={() => openEditModal(site)}
                    >
                      Edit
                    </button>
                  </td>
                </tr>
              )
            })}
            {!sites.length && (
              <tr>
                <td
                  colSpan={7}
                  className="px-4 py-10 text-center text-[var(--muted)]"
                >
                  No parking sites configured. Click "Add New Site" to create
                  your first facility.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Centered Modal: Add / Edit Parking Site */}
      {isModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget && !isSubmitting)
              closeModal()
          }}
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="site-modal-title"
            className="relative max-h-[88vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-[var(--border)] bg-[var(--card)] p-5 shadow-2xl sm:p-7 text-[var(--fg)]"
          >
            {/* Modal Header */}
            <div className="mb-5 flex items-start justify-between border-b border-[var(--border)] pb-4">
              <div>
                <span className="text-xs font-semibold uppercase tracking-wider text-[var(--primary)]">
                  SmartParking · Facility Profile
                </span>
                <h2
                  id="site-modal-title"
                  className="mt-1 font-['Outfit'] text-2xl font-bold text-[var(--fg)]"
                >
                  {editingSite ? "Edit Parking Site" : "Add New Parking Site"}
                </h2>
                <p className="mt-1 text-xs text-[var(--muted)]">
                  Configure site metadata, layout capacity, and parking policy
                  rates.
                </p>
              </div>
              <button
                type="button"
                className="rounded-lg p-1 text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)] transition"
                onClick={closeModal}
                disabled={isSubmitting}
                aria-label="Close dialog"
              >
                <UntitledIcon name="x" size={18} />
              </button>
            </div>

            {/* Error Notification */}
            {error && (
              <div
                role="alert"
                className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600"
              >
                {error}
              </div>
            )}

            <form onSubmit={saveSite} className="space-y-5">
              {/* Group 1: General & Location */}
              <div>
                <h3 className="mb-2 text-xs font-bold uppercase tracking-wider text-[var(--muted)]">
                  1. Identity & Location
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="sm:col-span-2">
                    <label className="label">Site Name *</label>
                    <input
                      className={fieldClass}
                      value={form.name}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, name: e.target.value }))
                      }
                      placeholder="e.g. Vincom Center - Thu Duc Branch"
                      required
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">Facility Type *</label>
                    <select
                      className={fieldClass}
                      value={form.type}
                      onChange={(e) => {
                        const newType = e.target.value as LotType
                        setForm((c) => ({
                          ...c,
                          type: newType,
                          floors:
                            newType === "outdoor"
                              ? "1"
                              : c.floors === "1"
                                ? "2"
                                : c.floors,
                        }))
                      }}
                      disabled={isSubmitting}
                    >
                      <option value="outdoor">Outdoor Ground Lot</option>
                      <option value="basement">Basement Parking</option>
                      <option value="multi-storey">
                        Multi-Storey Structure
                      </option>
                    </select>
                  </div>

                  <div>
                    <label className="label">Operational Status</label>
                    <select
                      className={fieldClass}
                      value={form.status}
                      onChange={(e) =>
                        setForm((c) => ({
                          ...c,
                          status: e.target.value as "active" | "inactive",
                        }))
                      }
                      disabled={isSubmitting}
                    >
                      <option value="active">Active (Open for Booking)</option>
                      <option value="inactive">
                        Inactive (Temporarily Closed)
                      </option>
                    </select>
                  </div>

                  <div className="sm:col-span-2">
                    <label className="label">Street Address *</label>
                    <input
                      className={fieldClass}
                      value={form.address}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, address: e.target.value }))
                      }
                      placeholder="e.g. 72 Le Thanh Ton, Ben Nghe, District 1, HCMC"
                      required
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">GPS Latitude</label>
                    <input
                      className={fieldClass}
                      type="number"
                      step="any"
                      value={form.lat}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, lat: e.target.value }))
                      }
                      placeholder="10.7769"
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">GPS Longitude</label>
                    <input
                      className={fieldClass}
                      type="number"
                      step="any"
                      value={form.lng}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, lng: e.target.value }))
                      }
                      placeholder="106.7009"
                      disabled={isSubmitting}
                    />
                  </div>
                </div>
              </div>

              {/* Group 2: Structure & Capacity */}
              <div className="border-t border-[var(--border)] pt-4">
                <h3 className="mb-2 text-xs font-bold uppercase tracking-wider text-[var(--muted)]">
                  2. Structure & Capacity
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="label">Total Parking Slots *</label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="1"
                      max="2000"
                      value={form.totalSlots}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, totalSlots: e.target.value }))
                      }
                      placeholder="e.g. 100"
                      required
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">Floor Levels *</label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="1"
                      max="20"
                      value={form.floors}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, floors: e.target.value }))
                      }
                      disabled={form.type === "outdoor" || isSubmitting}
                      required
                    />
                    {form.type === "outdoor" && (
                      <span className="text-[11px] text-[var(--muted)]">
                        Outdoor lots are restricted to 1 ground level.
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {/* Group 3: Pricing & Policies */}
              <div className="border-t border-[var(--border)] pt-4">
                <h3 className="mb-2 text-xs font-bold uppercase tracking-wider text-[var(--muted)]">
                  3. Pricing & Reservation Policy
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="label">Hourly Rate (₫)</label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="0"
                      step="1000"
                      value={form.hourlyRate}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, hourlyRate: e.target.value }))
                      }
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">Overnight / Night Rate (₫)</label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="0"
                      step="1000"
                      value={form.nightRate}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, nightRate: e.target.value }))
                      }
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">Daily Rate (₫)</label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="0"
                      step="5000"
                      value={form.dailyRate}
                      onChange={(e) =>
                        setForm((c) => ({ ...c, dailyRate: e.target.value }))
                      }
                      disabled={isSubmitting}
                    />
                  </div>

                  <div>
                    <label className="label">
                      Arrival Grace Period (Minutes)
                    </label>
                    <input
                      className={fieldClass}
                      type="number"
                      min="5"
                      max="60"
                      value={form.gracePeriodMinutes}
                      onChange={(e) =>
                        setForm((c) => ({
                          ...c,
                          gracePeriodMinutes: e.target.value,
                        }))
                      }
                      disabled={isSubmitting}
                    />
                  </div>
                </div>
              </div>

              {/* Modal Actions */}
              <div className="flex items-center justify-end gap-3 border-t border-[var(--border)] pt-4">
                <button
                  type="button"
                  className="btn-outline text-sm"
                  onClick={closeModal}
                  disabled={isSubmitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-primary text-sm"
                  disabled={isSubmitting}
                >
                  {isSubmitting
                    ? "Saving…"
                    : editingSite
                      ? "Save Site Changes"
                      : "Create Site"}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
    </section>
  )
}
