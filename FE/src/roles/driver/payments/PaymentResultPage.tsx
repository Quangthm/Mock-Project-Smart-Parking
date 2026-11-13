
import { useEffect, useState } from "react"
import { useNavigate } from "react-router-dom"
import { BrandLogo, BrandWordmark } from "../../../components/brand/BrandLogo"
import { useApp } from "../../../context/AppContext"
import { getSessionGeneration } from "../../../lib/authApi"
import { formatStatus } from "../../../lib/formatStatus"
import {
  classifyPaymentStatus,
  isPaymentOrderId,
  paymentApi,
  type PaymentStatusResponse,
} from "../../../lib/paymentApi"
import {
  reservationsApi,
  type OwnReservation,
} from "../../../lib/reservationsApi"
import { useServerRefresh } from "../../../lib/serverRefresh"
import "./payment-checkout.css"

type ResultState =
  | "checking"
  | "success"
  | "failed"
  | "pending"
  | "unavailable"

export function PaymentResultPage() {
  const { user, setView } = useApp()
  const navigate = useNavigate()

  const params = new URLSearchParams(window.location.search)
  const orderId = params.get("orderId") ?? ""
  const transactionId = params.get("transactionId") ?? ""

  const [result, setResult] = useState<ResultState>("checking")
  const [detail, setDetail] = useState<PaymentStatusResponse | null>(null)
  const [error, setError] = useState("")
  const [refresh, setRefresh] = useState(0)

  const [reservation, setReservation] = useState<OwnReservation | null>(null)
  const [reservationError, setReservationError] = useState("")

  // Operations is the authority for reservation confirmation.
  async function refreshReservation(isCurrent: () => boolean) {
    if (user?.role !== "driver" || !isPaymentOrderId(orderId)) return

    const generation = getSessionGeneration()

    try {
      let matched: OwnReservation | null = null

      // Develop: use the saved reservation ID when available.
      try {
        const reservationId = sessionStorage.getItem(
          `sp_reservation_order:${orderId}`
        )

        if (reservationId) {
          const booking = await reservationsApi.booking(reservationId)

          if (booking.orderId?.toLowerCase() === orderId.toLowerCase()) {
            matched = booking
          }
        }
      } catch {
        // Missing/stale browser reference: fall back to server lookup.
      }

      // SPARK-273: find the reservation by its authoritative order ID.
      if (!matched) {
        const items = await reservationsApi.list()
        matched = items.find(
          item => item.orderId?.toLowerCase() === orderId.toLowerCase()
        ) ?? null
      }

      if (!isCurrent() || generation !== getSessionGeneration()) return

      setReservation(matched)
      setReservationError("")
    } catch {
      if (isCurrent() && generation === getSessionGeneration()) {
        setReservationError(
          "Could not refresh reservation confirmation. Check Current Booking."
        )
      }
    }
  }

  useEffect(() => {
    let active = true

    setReservation(null)
    setReservationError("")
    void refreshReservation(() => active)

    return () => {
      active = false
    }
  }, [orderId, user?.id, user?.role, refresh, result])

  // Keep checking Operations even after Payment succeeds.
  useServerRefresh(
    user?.role === "driver" &&
      isPaymentOrderId(orderId) &&
      reservation?.state !== "CONFIRMED"
      ? orderId
      : undefined,
    refreshReservation,
    5000
  )

  // Payment status is independent of reservation confirmation.
  useEffect(() => {
    let active = true
    let timeout: number | undefined

    const checkout = isPaymentOrderId(orderId)
      ? paymentApi.rememberedCheckout(orderId)
      : null

    const statusTransactionId = isPaymentOrderId(transactionId)
      ? transactionId
      : checkout?.paymentTransactionId ?? ""

    if (!isPaymentOrderId(statusTransactionId)) {
      setResult("unavailable")
      setError(
        "This browser has no matching checkout reference. Payment status cannot be confirmed here."
      )
      return
    }

    setResult("checking")
    setError("")

    let attempts = 0

    async function check() {
      try {
        const status = await paymentApi.status(statusTransactionId)

        if (!active) return

        if (
          isPaymentOrderId(orderId) &&
          status.orderId.toLowerCase() !== orderId.toLowerCase()
        ) {
          throw new Error("Payment Service returned a different order.")
        }

        setDetail(status)

        const outcome = classifyPaymentStatus(status)

        if (outcome === "success" || outcome === "failed") {
          setResult(outcome)
          return
        }

        if (++attempts < 12) {
          timeout = window.setTimeout(check, 5000)
          return
        }

        setResult("pending")
      } catch (failure) {
        if (!active) return

        setError(
          failure instanceof Error
            ? failure.message
            : "Payment status is unavailable."
        )
        setResult("unavailable")
      }
    }

    void check()

    return () => {
      active = false
      if (timeout !== undefined) window.clearTimeout(timeout)
    }
  }, [orderId, refresh, transactionId])

  const title =
    result === "success" ? "Payment successful"
    : result === "failed" ? "Payment not completed"
    : result === "checking" ? "Checking your payment"
    : result === "pending" ? "Payment is still processing"
    : "Payment status unavailable"

  const description =
    result === "success"
      ? "Payment Service confirmed your transaction with payOS."
    : result === "failed"
      ? "Payment Service reported that this transaction was cancelled or failed."
    : result === "checking"
      ? "We are asking Payment Service for the confirmed result. This may take a moment."
    : result === "pending"
      ? "The payment has not been confirmed yet. Please check again before trying to pay."
    : error

  return (
    <main className="payment-result-page">
      <section className="card payment-result-card" aria-live="polite">
        <div className="payment-result-brand">
          <BrandLogo height={38} />
          <BrandWordmark height={21} />
        </div>

        <span
          className={`payment-result-icon is-${result}`}
          aria-hidden="true"
        >
          {result === "success" ? "✓" : result === "failed" ? "×" : "·"}
        </span>

        <h1>{title}</h1>
        <p>{description}</p>

        {reservation && (
          <p role="status">
            {reservation.state === "CONFIRMED"
              ? "Reservation confirmed. You can view it in Current Booking."
              : `Reservation: ${formatStatus(reservation.state)}. Payment and booking confirmation are checked separately.`}
          </p>
        )}

        {reservationError && (
          <p role="status">{reservationError}</p>
        )}

        {detail && (
          <dl className="payment-result-facts">
            <div>
              <dt>Order code</dt>
              <dd>{detail.orderCode}</dd>
            </div>
            <div>
              <dt>Amount</dt>
              <dd>{detail.amount.toLocaleString("vi-VN")} VND</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd>{formatStatus(detail.status)}</dd>
            </div>
          </dl>
        )}

        {(orderId || transactionId) && (
          <p className="payment-result-reference">
            Payment reference: {orderId || transactionId}
          </p>
        )}

        <div className="payment-result-actions">
          {(result === "pending" ||
            (result === "unavailable" &&
              (!!paymentApi.rememberedCheckout(orderId) ||
                isPaymentOrderId(transactionId)))) && (
            <button
              type="button"
              className="btn-primary"
              onClick={() => setRefresh(value => value + 1)}
            >
              Check status again
            </button>
          )}

          <button
            type="button"
            className="btn-outline"
            onClick={() => {
              setView(user?.role === "driver" ? "driver" : "sign-in")
              navigate("/")
            }}
          >
            Back to SmartParking
          </button>
        </div>
      </section>
    </main>
  )
}
