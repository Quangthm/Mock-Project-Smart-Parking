import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { useEffect, useState, type FormEvent } from "react"

import { useApp } from "../../../context/AppContext"
import { PageHeader } from "../../../components/ui/PageHeader"

import { driverData as store } from "../data/data"

import type {
  Booking,
  ParkingLot,
  ParkingSlot,
  PaymentMethod,
} from "../../../lib/types"

import { PAYMENT_OPTIONS } from "../data/paymentOptions"

import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { BookingDateTimePicker } from "./BookingDateTimePicker"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"
import {
  formatVietnamesePlate,
  formatVietnamesePlateValue,
  isSupportedVietnamesePlate,
  vehiclesApi,
  type RegisteredVehicle,
} from "../../../lib/vehiclesApi"

type Step = "slot" | "details" | "payment" | "qr"

type VehicleForm = {
  type: "motorcycle" | "car"
  plate: string
  imageReference: string
}

const slotTakenMessage =
  "This slot is currently being booked by someone else. Please try again."

function isAvailable(slot: ParkingSlot) {
  return (
    slot.status === "available" ||
    (slot.status === "reserved" &&
      !!slot.reservedUntil &&
      new Date(slot.reservedUntil).getTime() <= Date.now())
  )
}

function formatDuration(hours: number) {
  const totalMinutes = Math.round(hours * 60)
  const wholeHours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return [wholeHours ? `${wholeHours} hr${wholeHours === 1 ? "" : "s"}` : "", minutes ? `${minutes} min` : ""]
    .filter(Boolean).join(" ")
}

export function BookingFlow({
  lot,
  onDone,
  resumeBooking,
}: {
  lot: ParkingLot
  onDone: () => void
  resumeBooking?: Booking
}) {
  const { user } = useApp()

  const [step, setStep] = useState<Step>(resumeBooking ? "payment" : "slot")

  const [viewMode, setViewMode] = useState<"3d" | "grid">("3d")

  const [lotSnapshot, setLotSnapshot] = useState(
    () => store.getLots().find((item) => item.id === lot.id) ?? lot,
  )

  const [selectedSlot, setSelectedSlot] = useState<ParkingSlot | null>(() =>
    resumeBooking
      ? (lot.slots.find((slot) => slot.id === resumeBooking.slotId) ?? null)
      : null,
  )

  const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([])
  const [selectedVehicleId, setSelectedVehicleId] = useState("")
  const [addVehicleOpen, setAddVehicleOpen] = useState(false)
  const [vehicleLoading, setVehicleLoading] = useState(false)
  const [vehicleSaving, setVehicleSaving] = useState(false)
  const [vehicleError, setVehicleError] = useState("")
  const [vehicleReload, setVehicleReload] = useState(0)

  const [vehicleForm, setVehicleForm] = useState<VehicleForm>({
    type: "motorcycle",
    plate: "",
    imageReference: "",
  })

  const [startTime, setStartTime] = useState(
    resumeBooking?.startTime.slice(0, 16) ?? "",
  )

  const [endTime, setEndTime] = useState(
    resumeBooking?.endTime.slice(0, 16) ?? "",
  )

  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod | null>(
    resumeBooking?.paymentMethod ?? null,
  )

  const bookingId = resumeBooking?.id ?? ""

  const [message, setMessage] = useState("")
  const [showPolicyModal, setShowPolicyModal] = useState(false)

  useEffect(() => {
    if (!user || resumeBooking) return
    let active = true
    setVehicleLoading(true)
    setVehicleError("")
    vehiclesApi.list()
      .then((items) => {
        if (!active) return
        setVehicles(items)
        setSelectedVehicleId((current) =>
          items.some((item) => item.id === current) ? current : items[0]?.id ?? "",
        )
        setAddVehicleOpen(items.length === 0)
      })
      .catch((error) => {
        if (active)
          setVehicleError(error instanceof Error ? error.message : "Cannot load vehicles.")
      })
      .finally(() => {
        if (active) setVehicleLoading(false)
      })
    return () => { active = false }
  }, [user?.id, resumeBooking?.id, vehicleReload])

  useEffect(() => {
    function refreshLots(event: StorageEvent) {
      if (!event.key || event.key === "sp_lots") {
        setLotSnapshot(
          store.getLots().find((item) => item.id === lot.id) ?? lot,
        )
      }
    }

    window.addEventListener("storage", refreshLots)

    return () => window.removeEventListener("storage", refreshLots)
  }, [lot])

  const hours =
    startTime && endTime
      ? Math.max(
          0,
          (new Date(endTime).getTime() - new Date(startTime).getTime()) /
            3600000,
        )
      : 0

  const startTimestamp = startTime ? new Date(startTime).getTime() : NaN
  const endTimestamp = endTime ? new Date(endTime).getTime() : NaN
  const validTimeWindow = Number.isFinite(startTimestamp) && Number.isFinite(endTimestamp) &&
    startTimestamp > Date.now() && endTimestamp > startTimestamp

  const amount = Math.ceil(hours) * lot.hourlyRate

  const selectedVehicle = vehicles.find(
    (vehicle) => vehicle.id === selectedVehicleId,
  )

  function chooseSlot(slot: ParkingSlot) {
    const latest =
      store.getLots().find((item) => item.id === lot.id) ?? lotSnapshot

    const latestSlot = latest.slots.find((item) => item.id === slot.id)

    if (!latestSlot || !isAvailable(latestSlot)) {
      setLotSnapshot(latest)

      setSelectedSlot(null)

      setMessage(
        latestSlot?.status === "occupied"
          ? "This slot is currently occupied. Please choose another slot."
          : slotTakenMessage,
      )

      return
    }

    setLotSnapshot(latest)

    setSelectedSlot(latestSlot)

    setMessage("")
  }

  function continueToDetails() {
    if (!selectedSlot) return

    const latest =
      store.getLots().find((item) => item.id === lot.id) ?? lotSnapshot

    const latestSlot = latest.slots.find((item) => item.id === selectedSlot.id)

    if (!latestSlot || !isAvailable(latestSlot)) {
      setLotSnapshot(latest)

      setSelectedSlot(null)

      setMessage(slotTakenMessage)

      return
    }

    setLotSnapshot(latest)

    setStep("details")
  }

  async function addVehicle(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (vehicleSaving || !user) return
    const vehicleType = vehicleForm.type === "car" ? "CAR" : "MOTORCYCLE"
    if (!isSupportedVietnamesePlate(vehicleForm.plate, vehicleType)) {
      setVehicleError("Use a supported Vietnamese plate: 51A-123.45 for cars or 59-B1 123.45 for motorcycles.")
      return
    }
    setVehicleSaving(true)
    setVehicleError("")
    try {
      const created = await vehiclesApi.save({
        plate: vehicleForm.plate.trim().toUpperCase(),
        vehicleType,
        imageReference: vehicleForm.imageReference,
      })
      setVehicles((current) => [...current, created])
      setSelectedVehicleId(created.id)
      setVehicleForm((current) => ({ ...current, plate: "", imageReference: "" }))
      setMessage("")
      setAddVehicleOpen(false)
    } catch (error) {
      setVehicleError(error instanceof Error ? error.message : "Cannot save vehicle.")
    } finally {
      setVehicleSaving(false)
    }
  }

  function holdSlotAndContinue() {
    setMessage("Booking is unavailable until the Driver booking service is connected.")
  }

  function cancelHold() {
    setStep("details")
    setPaymentMethod(null)
    setMessage("")
  }

  function continueToPayment() {
    if (!paymentMethod) return

    if (paymentMethod === "qr") {
      setStep("qr")

      return
    }

    setMessage(
      `Live ${PAYMENT_OPTIONS.find((option) => option.id === paymentMethod)?.name} checkout is not configured. A payment gateway endpoint is required to continue.`,
    )
  }

  return (
    <div className="driver-booking-flow" style={{ maxWidth: 1440, margin: "0 auto" }}>
      <div className="booking-flow-header">
      <PageHeader title={<>Book a Spot at {lotSnapshot.name}</>} variant="compact" actions={<div className="booking-flow-header-actions">
          <button
            type="button"
            className="btn-outline"
            title="Review owner and platform policies"
            onClick={() => setShowPolicyModal(true)}
            style={{
              padding: "0.4rem 0.75rem",
              fontSize: "0.78rem",
              display: "inline-flex",
              alignItems: "center",
              gap: "0.35rem",
            }}
          >
            <UntitledIcon name="file" size={13} />
            Owner & platform policies
          </button>
          <button
            type="button"
            className="btn-outline"
            onClick={() => {
              if (!bookingId) cancelHold()
              onDone()
            }}
            style={{ padding: "0.4rem 0.7rem", fontSize: "0.78rem" }}
          >
            ← Back to Parking
          </button>
        </div>} />
      </div>
      <div className="booking-flow-steps" aria-label="Booking progress">
        {["Select Slot", "Time & Vehicle", "Payment"].map((label, index) => {
          const currentIndex = step === "slot" ? 0 : step === "details" ? 1 : 2

          return (
            <div key={label} className="booking-flow-step">
              <div
                style={{
                  height: 3,
                  borderRadius: 4,
                  background:
                    index <= currentIndex ? "var(--primary)" : "var(--border)",
                }}
              />
              <span
                style={{
                  display: "block",
                  marginTop: 5,
                  fontSize: "0.72rem",
                  color:
                    index === currentIndex ? "var(--primary)" : "var(--muted)",
                }}
              >
                {label}
              </span>
            </div>
          )
        })}
      </div>

      <div className={`booking-flow-stage-grid${step === "slot" ? "" : " is-form-stage"}`}>
      {step === "slot" && <section className="booking-slot-layout">
          <div className="card booking-model-panel">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: "0.85rem",
              flexWrap: "wrap",
              gap: "0.5rem",
            }}
          >
            <div>
              <h3
                style={{
                  margin: 0,
                  fontSize: "1.05rem",
                  fontWeight: 700,
                  color: "var(--fg)",
                  fontFamily: "Be Vietnam Pro",
                }}
              >
                Select a Parking Slot
              </h3>
              <span style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                {viewMode === "3d"
                  ? "Click a parking slot in the 3D view to select it"
                  : "Click a numbered slot below to select it"}
              </span>
            </div>

            {/* View mode toggle */}
            <div
              style={{
                display: "flex",
                gap: "0.25rem",
                padding: "0.2rem",
                background: "var(--bg)",
                borderRadius: "var(--radius)",
                border: "1px solid var(--border)",
              }}
            >
              <button
                type="button"
                onClick={() => setViewMode("3d")}
                style={{
                  padding: "0.35rem 0.75rem",

                  fontSize: "0.78rem",

                  fontWeight: 600,

                  borderRadius: "calc(var(--radius) - 2px)",

                  border: "none",

                  cursor: "pointer",

                  background:
                    viewMode === "3d" ? "var(--primary)" : "transparent",

                  color: viewMode === "3d" ? "var(--primary-fg)" : "var(--muted)",

                  transition: "background-color 0.2s, color 0.2s",
                }}
              >
                <UntitledIcon name="parking" size={16} /> 3D View
              </button>
              <button
                type="button"
                onClick={() => setViewMode("grid")}
                style={{
                  padding: "0.35rem 0.75rem",

                  fontSize: "0.78rem",

                  fontWeight: 600,

                  borderRadius: "calc(var(--radius) - 2px)",

                  border: "none",

                  cursor: "pointer",

                  background:
                    viewMode === "grid" ? "var(--primary)" : "transparent",

                  color: viewMode === "grid" ? "var(--primary-fg)" : "var(--muted)",

                  transition: "background-color 0.2s, color 0.2s",
                }}
              >
                <UntitledIcon name="grid" size={16} /> Grid View
              </button>
            </div>
          </div>

          {/* 3D Mode */}
          {viewMode === "3d" && (
            <div style={{ marginBottom: "1rem" }}>
              <Parking3DViewer
                lotType={lotSnapshot.type}
                modelUrl={lotSnapshot.modelUrl}
                slots={lotSnapshot.slots}
                selectedSlotId={selectedSlot?.id}
                onSelectSlot={step === "slot" ? chooseSlot : undefined}
                height="min(70vh, 700px)"
                interactive={step === "slot"}
              />
            </div>
          )}

          {/* 2D Grid Mode */}
          {viewMode === "grid" && (
            <div>
              <div
                style={{
                  display: "flex",
                  gap: "1rem",
                  marginBottom: "0.8rem",
                  flexWrap: "wrap",
                  color: "var(--muted)",
                  fontSize: "0.78rem",
                }}
              >
                <span>
                  <i
                    aria-hidden="true"
                    style={{
                      display: "inline-block",
                      width: 8,
                      height: 8,
                      marginRight: 5,
                      borderRadius: "50%",
                      background: "#22c55e",
                    }}
                  />
                  Available
                </span>
                <span>
                  <i
                    aria-hidden="true"
                    style={{
                      display: "inline-block",
                      width: 8,
                      height: 8,
                      marginRight: 5,
                      borderRadius: "50%",
                      background: "#ef4444",
                    }}
                  />
                  Occupied
                </span>
                <span>
                  <i
                    aria-hidden="true"
                    style={{
                      display: "inline-block",
                      width: 8,
                      height: 8,
                      marginRight: 5,
                      borderRadius: "50%",
                      background: "#f59e0b",
                    }}
                  />
                  Reserved
                </span>
              </div>
              {Array.from(
                { length: Math.max(1, lotSnapshot.floors) },
                (_, index) => index + 1,
              ).map((floor) => {
                const floorSlots = lotSnapshot.slots.filter(
                  (slot) => lotSnapshot.floors === 1 || slot.floor === floor,
                )

                if (!floorSlots.length) return null

                return (
                  <div key={floor} style={{ marginBottom: "0.85rem" }}>
                    {lotSnapshot.floors > 1 && (
                      <div
                        style={{
                          marginBottom: "0.4rem",
                          color: "var(--muted)",
                          fontSize: "0.78rem",
                          fontWeight: 600,
                        }}
                      >
                        Floor {floor}
                      </div>
                    )}
                    <div className="booking-slot-grid">
                      {floorSlots.map((slot) => {
                        const available = isAvailable(slot)

                        const locked = slot.status === "reserved" && !available

                        const color = available
                          ? "#22c55e"
                          : slot.status === "occupied"
                            ? "#ef4444"
                            : "#f59e0b"

                        return (
                          <button
                            key={slot.id}
                            type="button"
                            aria-label={`Slot ${slot.number}${
                              locked ? ", reserved" : ""
                            }`}
                            aria-pressed={selectedSlot?.id === slot.id}
                            disabled={step !== "slot"}
                            onClick={() => chooseSlot(slot)}
                            style={{
                              minHeight: 44,
                              borderRadius: 4,
                              border: `1.5px solid ${
                                selectedSlot?.id === slot.id
                                  ? "var(--primary)"
                                  : color
                              }`,
                              background:
                                selectedSlot?.id === slot.id
                                  ? "var(--primary)"
                                  : `${color}18`,
                              color:
                                selectedSlot?.id === slot.id ? "white" : color,
                              fontSize: "0.68rem",
                              fontWeight: 600,
                              cursor: "pointer",
                              opacity: available ? 1 : 0.65,
                            }}
                          >
                            {slot.number}
                          </button>
                        )
                      })}
                    </div>
                  </div>
                )
              })}
            </div>
          )}

          </div>
          {step === "slot" && <aside className="card booking-summary-panel" aria-label="Selected slot and booking information">
            <span className="find-parking-eyebrow">Your booking</span>
            <h3>{lotSnapshot.name}</h3>
            <p className="booking-summary-address">{lotSnapshot.address}</p>
            {selectedSlot && (
              <div className="booking-selected-slot" aria-label={`Selected slot ${selectedSlot.number}, floor ${selectedSlot.floor}, available`}>
                <span className="booking-selected-slot-name"><strong>Slot {selectedSlot.number}</strong><small>Floor {selectedSlot.floor}</small></span>
                <span className="booking-selected-slot-status"><span aria-hidden="true" />Available</span>
              </div>
            )}
            <dl className="booking-summary-list">
              <div><dt>Vehicle</dt><dd>{selectedVehicle ? `${selectedVehicle.vehicleType === "CAR" ? "Car" : "Motorcycle"} · ${formatVietnamesePlate(selectedVehicle)}` : vehicleLoading ? "Loading vehicles…" : "Choose in the next step"}</dd></div>
              <div><dt>Arrival</dt><dd>{startTime ? new Date(startTime).toLocaleString("vi-VN") : "Choose in the next step"}</dd></div>
              <div><dt>Duration</dt><dd>{hours > 0 ? formatDuration(hours) : "Choose in the next step"}</dd></div>
              <div><dt>Estimated price</dt><dd>{hours > 0 ? `${amount.toLocaleString("vi-VN")} VND` : "Available after choosing time"}</dd></div>
            </dl>
            {!selectedSlot && <p className="booking-summary-empty">Select an available space in the model or grid to continue.</p>}
          {message && (
            <p
              role="alert"
              style={{
                margin: "0.5rem 0",
                color: "#dc2626",
                fontSize: "0.82rem",
              }}
            >
              {message}
            </p>
          )}
          <button
            type="button"
            className="btn-primary"
            disabled={!selectedSlot}
            onClick={continueToDetails}
            style={{
              justifyContent: "center",
              width: "100%",
              marginTop: "0.5rem",
            }}
          >
            Continue with Slot {selectedSlot?.number || ""}
          </button>
          </aside>}
        </section>}

      {step === "details" && (
        <section className="card booking-stage-right booking-time-vehicle-stage">
          <h3
            style={{
              margin: "0 0 0.9rem",
              color: "var(--fg)",
              fontFamily: "Be Vietnam Pro",
            }}
          >
            Slot {selectedSlot?.number} · Choose time and vehicle
          </h3>
          <div className="booking-time-vehicle-grid">
            <div className="booking-time-card">
              <h4>Parking time</h4>
              <BookingDateTimePicker start={startTime} end={endTime} onStartChange={setStartTime} onEndChange={setEndTime} />
            </div>
            <div className="booking-vehicle-card">
              <h4>Your vehicle</h4>
            <label className="label">
              Vehicle
              {vehicleLoading && <span className="text-sm text-[var(--muted)]">Loading vehicles…</span>}
              {!vehicleLoading && vehicles.length > 0 && (
                <RoundedSelect
                  className="input"
                  value={selectedVehicleId}
                  onChange={(event) => setSelectedVehicleId(event.target.value)}
                >
                  {vehicles.map((vehicle) => (
                    <option key={vehicle.id} value={vehicle.id}>
                      {vehicle.vehicleType === "CAR" ? "Car" : "Motorcycle"} ·{" "}
                      {formatVietnamesePlate(vehicle)}
                    </option>
                  ))}
                </RoundedSelect>
              )}
            </label>
          {vehicleError && (
            <div role="alert" className="mb-3 text-sm text-red-600">
              {vehicleError}{" "}
              {!vehicles.length && !vehicleLoading && !addVehicleOpen && (
                <button type="button" className="underline" onClick={() => setVehicleReload((n) => n + 1)}>
                  Retry
                </button>
              )}
            </div>
          )}
          {!vehicleLoading && !vehicleError && vehicles.length === 0 && (
            <p
              style={{
                margin: "0 0 0.75rem",
                color: "var(--muted)",
                fontSize: "0.82rem",
              }}
            >
              You have not saved a vehicle yet. Add one to continue.
            </p>
          )}
          {!vehicleLoading && (
            <button
              className="btn-outline"
              type="button"
              onClick={() => {
                setAddVehicleOpen((open) => !open)
                setMessage("")
              }}
              style={{
                padding: "0.45rem 0.7rem",
                fontSize: "0.78rem",
                marginBottom: "0.75rem",
              }}
            >
              {addVehicleOpen ? "Cancel Add Vehicle" : "+ Add New Vehicle"}
            </button>
          )}
          {addVehicleOpen && !vehicleLoading && (
            <form
              onSubmit={addVehicle}
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                gap: "0.7rem",
                padding: "0.85rem",
                marginBottom: "0.85rem",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius)",
              }}
            >
              <label className="label">
                Type
                <RoundedSelect
                  className="input"
                  value={vehicleForm.type}
                  onChange={(event) => {
                    setVehicleError("")
                    setVehicleForm((current) => ({
                      ...current,
                      type: event.target.value as VehicleForm["type"],
                    }))
                  }}
                >
                  <option value="motorcycle">Motorcycle</option>
                  <option value="car">Car</option>
                </RoundedSelect>
              </label>
              <label className="label" style={{ gridColumn: "1 / -1" }}>
                Vietnamese license plate
                <input
                  className="input"
                  value={vehicleForm.plate}
                  onChange={(event) => {
                    setVehicleError("")
                    setVehicleForm((current) => ({
                      ...current,
                      plate: event.target.value,
                    }))
                  }}
                  onBlur={() => setVehicleForm((current) => ({
                    ...current,
                    plate: formatVietnamesePlateValue(
                      current.plate,
                      current.type === "car" ? "CAR" : "MOTORCYCLE",
                    ),
                  }))}
                  placeholder={vehicleForm.type === "car" ? "e.g. 51A-123.45" : "e.g. 59-B1 123.45"}
                  required
                  maxLength={30}
                />
              </label>
              <label className="label" style={{ gridColumn: "1 / -1" }}>
                Vehicle image (HTTPS URL)
                <input
                  className="input"
                  type="url"
                  required
                  maxLength={2048}
                  value={vehicleForm.imageReference}
                  onChange={(event) => {
                    setVehicleError("")
                    setVehicleForm((current) => ({ ...current, imageReference: event.target.value }))
                  }}
                  placeholder="https://example.com/vehicle.jpg"
                />
              </label>
              <button
                type="submit"
                className="btn-primary"
                disabled={vehicleSaving}
                style={{ gridColumn: "1 / -1", justifyContent: "center" }}
              >
                {vehicleSaving ? "Saving…" : "Save Vehicle"}
              </button>
            </form>
          )}
            </div>
          </div>
          {startTime && endTime && !validTimeWindow && <p className="booking-time-error" role="alert">Departure must be after arrival, and arrival must be in the future.</p>}
          {hours > 0 && (
            <p
              style={{
                margin: "0 0 0.75rem",
                color: "var(--muted)",
                fontSize: "0.82rem",
              }}
            >
              Estimated total:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {amount.toLocaleString("vi-VN")} VND
              </strong>
            </p>
          )}
          {message && (
            <p role="alert" style={{ color: "#dc2626", fontSize: "0.82rem" }}>
              {message}
            </p>
          )}
          <div style={{ display: "flex", gap: "0.6rem" }}>
            <button
              type="button"
              className="btn-outline"
              onClick={() => setStep("slot")}
            >
              Back
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={
                vehicleLoading ||
                vehicleSaving ||
                !selectedVehicle ||
                !validTimeWindow
              }
              onClick={holdSlotAndContinue}
              style={{ flex: 1, justifyContent: "center" }}
            >
              Continue
            </button>
          </div>
        </section>
      )}

      {step === "payment" && (
        <section className="card booking-stage-right">
          <h3
            style={{
              margin: "0 0 0.9rem",
              fontFamily: "Be Vietnam Pro",
              color: "var(--fg)",
            }}
          >
            Choose a payment method
          </h3>
          <p
            style={{
              margin: "0 0 0.9rem",
              color: "var(--muted)",
              fontSize: "0.82rem",
            }}
          >
            Booking {bookingId || "pending"} is saved with Payment Pending.
            Choose a card, QR code, or another payment provider. You can return
            to Booking to continue later.
          </p>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              margin: "0 0 0.9rem",
              flexWrap: "wrap",
              gap: "0.5rem",
            }}
          >
            <p
              style={{
                margin: 0,
                color: "var(--muted)",
                fontSize: "0.82rem",
              }}
            >
              Total due:{" "}
              <strong style={{ color: "var(--fg)" }}>
                {(resumeBooking?.amount ?? amount).toLocaleString("vi-VN")} VND
              </strong>
            </p>
            <button
              type="button"
              className="btn-outline"
              title="Review owner and platform policies"
              onClick={() => setShowPolicyModal(true)}
              style={{
                fontSize: "0.75rem",
                padding: "0.25rem 0.55rem",
                display: "inline-flex",
                alignItems: "center",
                gap: "0.3rem",
              }}
            >
              <UntitledIcon name="file" size={12} />
              Owner & platform policies
            </button>
          </div>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))",
              gap: "0.75rem",
              marginBottom: "1rem",
            }}
          >
            {PAYMENT_OPTIONS.map((option) => (
              <button
                key={option.id}
                type="button"
                onClick={() => {
                  setPaymentMethod(option.id)
                  setMessage("")
                }}
                aria-pressed={paymentMethod === option.id}
                style={{
                  display: "flex",

                  flexDirection: "column",

                  alignItems: "center",

                  justifyContent: "center",

                  gap: "0.45rem",

                  padding: "0.85rem 0.5rem",

                  borderRadius: "var(--radius)",

                  border: `2px solid ${
                    paymentMethod === option.id
                      ? "var(--primary)"
                      : "var(--border)"
                  }`,

                  background:
                    paymentMethod === option.id
                      ? "color-mix(in srgb, var(--primary) 10%, var(--card))"
                      : "var(--card)",

                  cursor: "pointer",

                  transition: "all 0.15s ease",
                }}
              >
                <img
                  src={option.logo}
                  alt={option.name}
                  style={{ height: 28, maxWidth: 64, objectFit: "contain" }}
                />
                <span
                  style={{
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    color: "var(--fg)",
                    textAlign: "center",
                  }}
                >
                  {option.name}
                </span>
              </button>
            ))}
          </div>
          {message && (
            <p role="alert" style={{ color: "#b45309", fontSize: "0.82rem" }}>
              {message}
            </p>
          )}
          <div style={{ display: "flex", gap: "0.6rem" }}>
            <button type="button" className="btn-outline" onClick={cancelHold}>
              Back
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={!paymentMethod}
              onClick={continueToPayment}
              style={{ flex: 1, justifyContent: "center" }}
            >
              Continue
            </button>
          </div>
        </section>
      )}

      {step === "qr" && (
        <section className="card booking-stage-right" style={{ textAlign: "center" }}>
          <h3
            style={{
              margin: "0 0 0.5rem",
              fontFamily: "Be Vietnam Pro",
              color: "var(--fg)",
            }}
          >
            QR payment
          </h3>
          <p
            style={{
              margin: "0 0 1rem",
              color: "var(--muted)",
              fontSize: "0.82rem",
            }}
          >
            A real payment QR must be generated by the configured payment
            gateway. No gateway is connected yet, so this booking has not been
            charged.
          </p>
          <div
            style={{ display: "flex", justifyContent: "center", gap: "0.6rem" }}
          >
            <button
              type="button"
              className="btn-outline"
              onClick={() => setStep("payment")}
            >
              Back to payment methods
            </button>
            <button type="button" className="btn-outline" onClick={cancelHold}>
              Cancel hold
            </button>
          </div>
        </section>
      )}

      </div>

      {showPolicyModal && (
        <LotPolicyModal
          lot={lotSnapshot}
          onClose={() => setShowPolicyModal(false)}
        />
      )}
    </div>
  )
}
