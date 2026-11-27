import { useEffect, useRef, useState } from "react"
import { useNavigate } from "react-router-dom"
import { BrandLogo, BrandWordmark } from "../../../components/brand/BrandLogo"
import { useApp } from "../../../context/AppContext"
import { getSessionGeneration } from "../../../lib/authApi"
import { formatStatus } from "../../../lib/formatStatus"
import {
  classifyPaymentStatus,
  isPaymentOrderId,
  paymentApi,
  type ReservationCheckoutState,
  type PaymentStatusResponse,
} from "../../../lib/paymentApi"
import { reservationsApi } from "../../../lib/reservationsApi"
import { useReservationProgress } from "../../../lib/reservationProgress"
import "./payment-checkout.css"

type ResultState =
  | "checking"
  | "success"
  | "failed"
  | "pending"
  | "unavailable"

type ReservationLookup = Pick<ReservationCheckoutState, "reservationId" | "state"> & {
  orderId: string
  requestId: string | null
}

export function PaymentResultPage() {
  const { user, authReady } = useApp()
  if (!authReady) return <main aria-busy="true">Loading payment details…</main>
  return <AccountPaymentResult key={`${getSessionGeneration()}:${user?.id ?? ""}:${window.location.search}`} />
}

const closedReservations = new Set(["CANCELLED", "EXPIRED", "UNFULFILLABLE", "INVALIDATED", "NO_SHOW", "REJECTED"])

function AccountPaymentResult() {
  const { user, setView } = useApp()
  const navigate = useNavigate()

  const params = new URLSearchParams(window.location.search)
  const orderId = params.get("orderId") ?? ""
  const transactionId = params.get("transactionId") ?? ""
  const hint = params.get("requestId")

  const [lastResult, setResult] = useState<ResultState>("checking")
  const [detail, setDetail] = useState<PaymentStatusResponse | null>(null)
  const [error, setError] = useState("")
  const [refresh, setRefresh] = useState(0)

  const [lookup, setLookup] = useState<ReservationLookup | null>(null)
  const [reservationError, setReservationError] = useState("")

  const boundOrder = orderId || detail?.orderId || ""
  const boundLookup = lookup?.orderId === boundOrder ? lookup : null
  const requestId = boundLookup?.requestId ?? null
  const progress = useReservationProgress(requestId, Boolean(requestId && boundLookup?.state !== "HISTORICAL"))
  const checkout = progress.checkout
  const matchingCheckout = checkout && checkout !== "UNAVAILABLE" &&
    checkout.orderId?.toLowerCase() === boundOrder.toLowerCase() ? checkout : null
  const result = matchingCheckout?.state === "PAID" ? "success" : lastResult
  const snapshot = progress.snapshot
  const reservation = requestId && snapshot?.requestId === requestId &&
    snapshot.reservationId?.toLowerCase() === boundLookup?.reservationId?.toLowerCase() &&
    (!snapshot.orderId || snapshot.orderId.toLowerCase() === boundOrder.toLowerCase()) ? snapshot : null
  const reservationState = reservation?.reservationState
  useEffect(() => {
    if (user?.role !== "driver" || !isPaymentOrderId(boundOrder)) return
    let active = true
    const generation = getSessionGeneration()
    const current = () => active && generation === getSessionGeneration()
    void paymentApi.orderReservation(boundOrder).then(async ({ state, reservationId, orderId: returnedOrderId }) => {
      if (!current()) return
      // Payment omits the order ID for a closed provisioning fence.
      const closedFence = state === "CLOSED" && returnedOrderId === null
      if (!closedFence && returnedOrderId?.toLowerCase() !== boundOrder.toLowerCase()) {
        throw new Error("Payment Service returned a different order.")
      }
      let bookingRequestId: string | null = null
      if (state !== "STANDALONE" && !closedFence) {
        if (!reservationId || !isPaymentOrderId(reservationId)) throw new Error("Invalid reservation reference.")
        const candidate = state === "HISTORICAL"
          ? (await reservationsApi.requestForOrder(boundOrder)).requestId : reservationId
        if (!candidate || !isPaymentOrderId(candidate)) throw new Error("Invalid booking request reference.")
        bookingRequestId = candidate
      }
      if (current()) {
        setLookup({ orderId: boundOrder, requestId: bookingRequestId, reservationId, state })
        setReservationError("")
      }
    }).catch(() => {
      if (current()) {
        setLookup(null)
        setReservationError("Could not load the reservation confirmation. Check Current Booking.")
      }
    })
    return () => { active = false }
  }, [boundOrder, refresh, user?.role])

  // Payment status is independent of reservation confirmation.
  useEffect(() => {
    let active = true
    const generation = getSessionGeneration()
    const current = () => active && generation === getSessionGeneration()

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

    async function check() {
      try {
        const status = await paymentApi.status(statusTransactionId)

        if (!current()) return

        if (
          isPaymentOrderId(orderId) &&
          status.orderId.toLowerCase() !== orderId.toLowerCase()
        ) {
          throw new Error("Payment Service returned a different order.")
        }

        setDetail(status)

        const outcome = classifyPaymentStatus(status)

        setResult(outcome)
      } catch (failure) {
        if (!current()) return

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
    }
  }, [orderId, refresh, transactionId])

  // Only standalone Payment workflows keep their bounded transaction-status retries.
  const standalone = !hint && (user?.role !== "driver" ||
    boundLookup?.state === "STANDALONE")
  const retries = useRef(0)
  useEffect(() => {
    if (!standalone || result !== "pending" || retries.current >= 11) return
    const timeout = window.setTimeout(() => {
      retries.current++
      setRefresh(value => value + 1)
    }, 5000)
    return () => window.clearTimeout(timeout)
  }, [standalone, result, detail])

  const paymentTitle =
    result === "success" ? "Payment successful"
    : result === "failed" ? "Payment not completed"
    : result === "checking" ? "Checking your payment"
    : result === "pending" ? "Payment is still processing"
    : "Payment status unavailable"

  const paymentDescription =
    result === "success"
      ? "Payment Service confirmed your transaction with payOS."
    : result === "failed"
      ? "Payment Service reported that this transaction was cancelled or failed."
    : result === "checking"
      ? "We are asking Payment Service for the confirmed result. This may take a moment."
    : result === "pending"
      ? "The payment has not been confirmed yet. Please check again before trying to pay."
    : error

  const orderState = matchingCheckout?.state ?? boundLookup?.state
  const confirmed = reservationState === "CONFIRMED" && lastResult !== "failed" && orderState !== "FAILED" && orderState !== "CLOSED"
  const closed = reservationState && closedReservations.has(reservationState)
  const orderClosed = orderState === "CLOSED"
  const title = confirmed ? "Reservation confirmed" : closed ? `Reservation ${formatStatus(reservationState).toLowerCase()}`
    : orderClosed ? "Reservation payment order closed"
    : requestId && result === "success" ? "Payment received. Awaiting reservation confirmation." : paymentTitle
  const description = confirmed ? "Operations confirmed your booking. You can view it in Current Booking."
    : closed || orderClosed ? "Check Current Booking for the reservation details." : paymentDescription
  const iconState = confirmed ? "success" : closed || orderClosed ? "failed" : result

  return (
    <main className="payment-result-page">
      <section className="card payment-result-card" aria-live="polite">
        <div className="payment-result-brand">
          <BrandLogo height={38} />
          <BrandWordmark height={21} />
        </div>

        <span
          className={`payment-result-icon is-${iconState}`}
          aria-hidden="true"
        >
          {iconState === "success" ? "✓" : iconState === "failed" ? "×" : "·"}
        </span>

        <h1>{title}</h1>
        <p>{description}</p>

        {reservationState && reservationState !== "CONFIRMED" && !closed && (
          <p role="status">
            {formatStatus(reservationState)}
          </p>
        )}
        {(confirmed || closed || orderClosed) && <p role="status">Last payment check: {paymentTitle}.</p>}
        {requestId && !progress.connected && <p role="status">Waiting for live reservation updates.
          {" "}<button type="button" className="btn-outline" onClick={progress.reconnect}>Reconnect</button>
        </p>}

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
              <dt>Last reported payment status</dt>
              <dd>{formatStatus(result === "success" ? "SUCCESS" : detail.status)}</dd>
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
              onClick={() => { retries.current = 0; setRefresh(value => value + 1) }}
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
