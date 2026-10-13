import { useEffect, useState, type FormEvent } from "react"

import { useApp } from "../../../context/AppContext"

import { driverData as store } from "../data/data"

import type {
  Booking,
  ParkingLot,
  ParkingSlot,
  PaymentMethod,
} from "../../../lib/types"

import { PAYMENT_OPTIONS } from "../data/paymentOptions"

import { Parking3DViewer } from "../../../components/3d/Parking3DViewer"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import { LotPolicyModal } from "../../../components/modals/LotPolicyModal"

type Step = "slot" | "details" | "payment" | "qr"

type VehicleForm = {
  type: "motorcycle" | "car"
  plateType: "vn" | "foreign"
  plate: string
}

const HOLD_MINUTES = 10

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

  const [holdId] = useState(() => store.generateId())

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

  const [vehicles, setVehicles] = useState(() =>
    user ? store.getVehiclesByDriver(user.id) : [],
  )

  const [selectedVehicleId, setSelectedVehicleId] = useState(
    vehicles[0]?.id ?? "",
  )

  const [addVehicleOpen, setAddVehicleOpen] = useState(vehicles.length === 0)

  const [vehicleForm, setVehicleForm] = useState<VehicleForm>({
    type: "motorcycle",
    plateType: "vn",
    plate: "",
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

  const [bookingId, setBookingId] = useState(resumeBooking?.id ?? "")

  const [message, setMessage] = useState("")
  const [showPolicyModal, setShowPolicyModal] = useState(false)

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

  function addVehicle(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!user) return

    const plate = vehicleForm.plate.trim().toUpperCase().replace(/\s+/g, "")

    const valid =
      vehicleForm.plateType === "foreign"
        ? /^(?=.*[A-Z])(?=.*\d)[A-Z0-9-]{4,12}$/.test(plate)
        : vehicleForm.type === "car"
          ? /^\d{2}[A-Z]{1,2}-?\d{4,5}$/.test(plate)
          : /^(\d{2}-?[A-Z]\d?-?\d{4,5}|\d{2}V[A-Z]-?\d{5})$/.test(plate)

    if (!valid) {
      setMessage("Please enter a valid license plate.")

      return
    }

    const created = store.createVehicle({
      driverId: user.id,
      type: vehicleForm.type,
      plateType: vehicleForm.plateType,
      licensePlate: plate,
    })

    if (!created) {
      setMessage(
        vehicles.length >= 3
          ? "You can save up to 3 vehicles."
          : "This license plate is already saved.",
      )

      return
    }

    setVehicles((current) => [...current, created])

    setSelectedVehicleId(created.id)

    setVehicleForm((current) => ({ ...current, plate: "" }))

    setMessage("")

    setAddVehicleOpen(false)
  }

  function holdSlotAndContinue() {
    if (
      !user ||
      !selectedSlot ||
      !selectedVehicle ||
      !startTime ||
      !endTime ||
      new Date(endTime) <= new Date(startTime)
    )
      return

    const latest =
      store.getLots().find((item) => item.id === lot.id) ?? lotSnapshot

    const latestSlot = latest.slots.find((item) => item.id === selectedSlot.id)

    if (!latestSlot || !isAvailable(latestSlot)) {
      setLotSnapshot(latest)

      setSelectedSlot(null)

      setMessage(slotTakenMessage)

      setStep("slot")

      return
    }

    const reservedUntil = new Date(
      Date.now() + HOLD_MINUTES * 60_000,
    ).toISOString()

    const updatedLot = {
      ...latest,

      slots: latest.slots.map((slot) =>
        slot.id === selectedSlot.id
          ? {
              ...slot,
              status: "reserved" as const,
              reservedUntil,
              reservedBy: holdId,
            }
          : slot,
      ),
    }

    store.saveLot(updatedLot)

    setLotSnapshot(updatedLot)

    const booking = store.createBooking({
      driverId: user.id,

      lotId: latest.id,

      lotName: latest.name,

      slotId: selectedSlot.id,

      slotNumber: selectedSlot.number,

      licensePlate: selectedVehicle.licensePlate,

      plateType: selectedVehicle.plateType,

      startTime: new Date(startTime).toISOString(),

      endTime: new Date(endTime).toISOString(),

      status: "pending",

      paymentStatus: "pending",

      amount,

      depositAmount: 0,
    })

    setBookingId(booking.id)

    const linkedLot = {
      ...updatedLot,
      slots: updatedLot.slots.map((slot) =>
        slot.id === selectedSlot.id
          ? { ...slot, reservedBy: booking.id }
          : slot,
      ),
    }

    store.saveLot(linkedLot)

    setLotSnapshot(linkedLot)

    setStep("payment")
  }

  function cancelHold() {
    if (bookingId) {
      const existing = store
        .getBookingsByDriver(user?.id ?? "")
        .find((booking) => booking.id === bookingId)

      if (existing) store.saveBooking({ ...existing, status: "cancelled" })

      setBookingId("")
    }

    if (selectedSlot) {
      const latest =
        store.getLots().find((item) => item.id === lot.id) ?? lotSnapshot

      store.saveLot({
        ...latest,

        slots: latest.slots.map((slot) =>
          slot.id === selectedSlot.id &&
          (slot.reservedBy === holdId || slot.reservedBy === bookingId)
            ? {
                ...slot,
                status: "available" as const,
                reservedUntil: undefined,
                reservedBy: undefined,
              }
            : slot,
        ),
      })
    }

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
    <div style={{ maxWidth: 1040, margin: "0 auto" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "0.65rem",
        }}
      >
        <h2
          style={{
            margin: 0,
            fontSize: "1.25rem",
            fontFamily: "Outfit",
            fontWeight: 700,
            color: "var(--fg)",
          }}
        >
          Đặt Chỗ Tại {lotSnapshot.name}
        </h2>
        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          <button
            type="button"
            className="btn-outline"
            title="Xem quy định & chính sách bãi đỗ xe (PDF)"
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
            Chính sách bãi đỗ
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
            Back to parking
          </button>
        </div>
      </div>
      <div style={{ display: "flex", gap: "0.3rem", marginBottom: "1.5rem" }}>
        {["Select Slot", "Time & Vehicle", "Payment"].map((label, index) => {
          const currentIndex = step === "slot" ? 0 : step === "details" ? 1 : 2

          return (
            <div key={label} style={{ flex: 1, textAlign: "center" }}>
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

      {step === "slot" && (
        <section className="card">
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
                  fontFamily: "Outfit",
                }}
              >
                Chọn Vị Trí Đỗ Xe (Parking Slot)
              </h3>
              <span style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                {viewMode === "3d"
                  ? "Click trực tiếp vào ô đỗ trong không gian 3D để chọn vị trí mong muốn"
                  : "Bấm vào ô số bên dưới để chọn"}
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

                  color: viewMode === "3d" ? "white" : "var(--muted)",

                  transition: "all 0.2s",
                }}
              >
                🎮 Mô Hình 3D Lớn
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

                  color: viewMode === "grid" ? "white" : "var(--muted)",

                  transition: "all 0.2s",
                }}
              >
                ▦ Sơ Đồ Lưới
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
                onSelectSlot={chooseSlot}
                height={550}
                interactive={true}
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
                    <div
                      style={{
                        display: "grid",
                        gridTemplateColumns: "repeat(8, minmax(28px, 1fr))",
                        gap: "0.35rem",
                      }}
                    >
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
                            onClick={() => chooseSlot(slot)}
                            style={{
                              height: 36,
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

          {selectedSlot && (
            <div
              style={{
                margin: "0.6rem 0",
                padding: "0.6rem 0.85rem",
                background: "var(--primary)12",
                border: "1px solid var(--primary)30",
                borderRadius: "var(--radius)",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <span style={{ fontSize: "0.84rem", color: "var(--fg)" }}>
                Vị trí đang chọn:{" "}
                <strong
                  style={{ color: "var(--primary)", fontSize: "0.95rem" }}
                >
                  Slot {selectedSlot.number}
                </strong>{" "}
                (Tầng {selectedSlot.floor})
              </span>
              <span
                style={{
                  fontSize: "0.75rem",
                  color: "#22c55e",
                  fontWeight: 600,
                }}
              >
                ● Hợp lệ
              </span>
            </div>
          )}
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
        </section>
      )}

      {step === "details" && (
        <section className="card">
          <h3
            style={{
              margin: "0 0 0.9rem",
              color: "var(--fg)",
              fontFamily: "Outfit",
            }}
          >
            Slot {selectedSlot?.number} · Choose time and vehicle
          </h3>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
              gap: "0.75rem",
              marginBottom: "1rem",
            }}
          >
            <label className="label">
              Start Time
              <input
                className="input"
                type="datetime-local"
                value={startTime}
                min={new Date().toISOString().slice(0, 16)}
                onChange={(event) => setStartTime(event.target.value)}
              />
            </label>
            <label className="label">
              End Time
              <input
                className="input"
                type="datetime-local"
                value={endTime}
                min={startTime || new Date().toISOString().slice(0, 16)}
                onChange={(event) => setEndTime(event.target.value)}
              />
            </label>
            <label className="label" style={{ gridColumn: "1 / -1" }}>
              Vehicle
              {vehicles.length > 0 && (
                <select
                  className="input"
                  value={selectedVehicleId}
                  onChange={(event) => setSelectedVehicleId(event.target.value)}
                >
                  {vehicles.map((vehicle) => (
                    <option key={vehicle.id} value={vehicle.id}>
                      {vehicle.type === "car" ? "Car" : "Motorcycle"} ·{" "}
                      {vehicle.licensePlate} ·{" "}
                      {vehicle.plateType === "vn" ? "Vietnam" : "International"}
                    </option>
                  ))}
                </select>
              )}
            </label>
          </div>
          {vehicles.length === 0 && (
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
          {vehicles.length < 3 && (
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
          {addVehicleOpen && vehicles.length < 3 && (
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
                <select
                  className="input"
                  value={vehicleForm.type}
                  onChange={(event) =>
                    setVehicleForm((current) => ({
                      ...current,
                      type: event.target.value as VehicleForm["type"],
                    }))
                  }
                >
                  <option value="motorcycle">Motorcycle</option>
                  <option value="car">Car</option>
                </select>
              </label>
              <label className="label">
                Plate region
                <select
                  className="input"
                  value={vehicleForm.plateType}
                  onChange={(event) =>
                    setVehicleForm((current) => ({
                      ...current,
                      plateType: event.target.value as VehicleForm["plateType"],
                    }))
                  }
                >
                  <option value="vn">Vietnam</option>
                  <option value="foreign">International</option>
                </select>
              </label>
              <label className="label" style={{ gridColumn: "1 / -1" }}>
                License plate
                <input
                  className="input"
                  value={vehicleForm.plate}
                  onChange={(event) =>
                    setVehicleForm((current) => ({
                      ...current,
                      plate: event.target.value,
                    }))
                  }
                  placeholder={
                    vehicleForm.plateType === "vn"
                      ? "e.g. 51A-12345"
                      : "e.g. ABC-1234"
                  }
                />
              </label>
              <button
                type="submit"
                className="btn-primary"
                style={{ gridColumn: "1 / -1", justifyContent: "center" }}
              >
                Save Vehicle
              </button>
            </form>
          )}
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
                {amount.toLocaleString("vi-VN")}₫
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
                !selectedVehicleId ||
                !startTime ||
                !endTime ||
                new Date(endTime) <= new Date(startTime)
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
        <section className="card">
          <h3
            style={{
              margin: "0 0 0.9rem",
              fontFamily: "Outfit",
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
                {(resumeBooking?.amount ?? amount).toLocaleString("vi-VN")}₫
              </strong>
            </p>
            <button
              type="button"
              className="btn-outline"
              title="Xem chính sách hoàn hủy & quy định bãi xe"
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
              Chính sách bãi đỗ
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
        <section className="card" style={{ textAlign: "center" }}>
          <h3
            style={{
              margin: "0 0 0.5rem",
              fontFamily: "Outfit",
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

      {showPolicyModal && (
        <LotPolicyModal
          lot={lotSnapshot}
          onClose={() => setShowPolicyModal(false)}
        />
      )}
    </div>
  )
}
