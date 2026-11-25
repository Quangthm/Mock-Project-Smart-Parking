import { paymentApi, type ReservationCheckoutState } from "./paymentApi"
import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr"
import { useEffect, useState } from "react"
import { ensureAccessToken, getSessionGeneration } from "./authApi"
import { reservationServer, reservationsApi, type ReservationProgress } from "./reservationsApi"

export function watchReservationProgress(
  requestId: string,
  onSnapshot: (snapshot: ReservationProgress) => void,
  onAvailable: (available: boolean) => void,
  onCheckout?: (value: ReservationCheckoutState | "UNAVAILABLE" | null) => void,
) {
  const generation = getSessionGeneration()
  let stopped = false
  let epoch = 0
  let latest = -1
  const active = () => !stopped && generation === getSessionGeneration()
  const connection = new HubConnectionBuilder()
    .withUrl(`${reservationServer}/hubs/reservation-progress`, {
      accessTokenFactory: async () => {
        if (!active()) throw new Error("Your sign-in changed.")
        const token = await ensureAccessToken()
        if (!active() || !token) throw new Error("Sign in to view reservation progress.")
        return token
      },
      withCredentials: false,
      transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()

  function receive(snapshot: ReservationProgress) {
    if (!active() || snapshot?.requestId !== requestId || !Number.isSafeInteger(snapshot.version) ||
        snapshot.version < 0 || !snapshot.checks) return false
    if (snapshot.version > latest) {
      latest = snapshot.version
      onSnapshot(snapshot)
    }
    return true
  }
  let checkoutRead = 0
  async function refreshCheckout() {
    if (!onCheckout || !active()) return
    const read = ++checkoutRead
    const connectionEpoch = epoch
    try {
      const value = await paymentApi.reservationCheckout(requestId)
      if (active() && read === checkoutRead && epoch === connectionEpoch) {
        if (value.reservationId && value.reservationId !== requestId) throw new Error("Mismatched order.")
        onCheckout(value)
      }
    } catch {
      if (active() && read === checkoutRead && epoch === connectionEpoch) onCheckout("UNAVAILABLE")
    }
  }
  const hint = (reservationId: string) => {
    if (reservationId === requestId) void refreshCheckout()
  }
  async function recover() {
    const currentEpoch = ++epoch
    try {
      if (!active()) return
      await connection.invoke("Subscribe", requestId)
      if (!active() || currentEpoch !== epoch) return
      // Join before the read, so a newer notification racing the read wins by revision.
      void refreshCheckout()
      const snapshot = await reservationsApi.progress(requestId)
      if (active() && currentEpoch === epoch) {
        onAvailable(receive(snapshot))
      }
    } catch {
      if (active() && currentEpoch === epoch) onAvailable(false)
    }
  }
  function disconnected() {
    ++epoch
    if (active()) { onAvailable(false); onCheckout?.(null) }
  }
  connection.on("ReservationCheckoutChanged", hint)
  connection.on("ReservationProgress", receive)
  connection.onreconnecting(disconnected)
  connection.onreconnected(recover)
  connection.onclose(disconnected)
  void connection.start().then(recover, disconnected)
  return () => {
    stopped = true
    connection.off("ReservationCheckoutChanged", hint)
    connection.off("ReservationProgress", receive)
    void connection.stop()
  }
}

export function useReservationProgress(requestId?: string | null, checkoutEnabled = false) {
  const generation = getSessionGeneration()
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{
    requestId: string; generation: number; snapshot: ReservationProgress
  } | null>(null)
  const [financial, setFinancial] = useState<{ requestId: string; generation: number; value: ReservationCheckoutState | "UNAVAILABLE" | null } | null>(null)
  const [connected, setConnected] = useState(false)
  useEffect(() => {
    setConnected(false)
    setFinancial(null)
    if (!requestId) return
    const stop = watchReservationProgress(requestId, snapshot => {
      setState(prior => prior?.generation === generation && prior.requestId === requestId &&
        prior.snapshot.version >= snapshot.version ? prior : { generation, requestId, snapshot })
    }, setConnected, checkoutEnabled ? value => setFinancial({ requestId, generation, value }) : undefined)
    const restore = (event: PageTransitionEvent) => {
      if (event.persisted) setAttempt(value => value + 1)
    }
    window.addEventListener("pageshow", restore)
    return () => {
      window.removeEventListener("pageshow", restore)
      stop()
    }
  }, [requestId, generation, attempt, checkoutEnabled])
  return {
    checkout: financial?.generation === generation && financial.requestId === requestId ? financial.value : null,
    snapshot: state?.generation === generation && state.requestId === requestId ? state.snapshot : null,
    connected,
    reconnect: () => setAttempt(value => value + 1),
  }
}
