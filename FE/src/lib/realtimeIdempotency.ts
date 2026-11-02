// Real-time idempotency consumer for SPARK-244 (G5).
//
// Tracks processed eventIds and per-slot sequence watermarks so that
// duplicate / replayed SignalR deltas are applied at most once and never
// move state backward.
//
// Design: docs/03-design/api/3d-digital-twin-spark-244-delta-design.md §6

export interface RealtimeEventEnvelope {
  eventId: string
  eventType?: string
  occurredAt?: string
  sequenceNumber?: number
  siteId?: string
  payload?: unknown
  [key: string]: unknown
}

export interface SlotDelta {
  slotId: string
  sequenceNumber?: number
  [key: string]: unknown
}

export interface IdempotencyDecision {
  apply: boolean
  reason: 'new' | 'duplicate_event' | 'stale_sequence' | 'duplicate_sequence'
}

export class RealtimeIdempotencyGuard {
  private processedEventIds = new Set<string>()
  private slotWatermark = new Map<string, number>()

  /** Decide whether an event should be applied. Does not mutate state. */
  decide(event: RealtimeEventEnvelope, slotId?: string): IdempotencyDecision {
    if (this.processedEventIds.has(event.eventId)) {
      return { apply: false, reason: 'duplicate_event' }
    }
    if (slotId && typeof event.sequenceNumber === 'number') {
      const last = this.slotWatermark.get(slotId)
      if (typeof last === 'number') {
        if (event.sequenceNumber < last) {
          return { apply: false, reason: 'stale_sequence' }
        }
        if (event.sequenceNumber === last) {
          return { apply: false, reason: 'duplicate_sequence' }
        }
      }
    }
    return { apply: true, reason: 'new' }
  }

  /** Mark an event as applied; update watermark. Call only after successful apply. */
  commit(event: RealtimeEventEnvelope, slotId?: string): void {
    this.processedEventIds.add(event.eventId)
    if (slotId && typeof event.sequenceNumber === 'number') {
      const last = this.slotWatermark.get(slotId)
      if (typeof last !== 'number' || event.sequenceNumber > last) {
        this.slotWatermark.set(slotId, event.sequenceNumber)
      }
    }
  }

  /** Reset all state (e.g. on reconnect after full snapshot reload). */
  reset(): void {
    this.processedEventIds.clear()
    this.slotWatermark.clear()
  }

  /** Number of processed events retained (for diagnostics). */
  get processedCount(): number {
    return this.processedEventIds.size
  }
}

/** Singleton guard for the operator monitoring view. */
export const operatorRealtimeGuard = new RealtimeIdempotencyGuard()
