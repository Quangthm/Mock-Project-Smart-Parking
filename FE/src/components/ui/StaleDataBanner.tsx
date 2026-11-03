import { UntitledIcon } from "../icon/UntitledIcon"

/**
 * A 30% dim leaves 70% opacity. The snapshot is still readable and every slot
 * remains interactive, but the screen clearly reads as "not live" (A5).
 */
export const STALE_DIM_OPACITY = 0.7

export function formatLayoutAge(seconds: number | null): string {
  if (seconds == null || !Number.isFinite(seconds) || seconds < 0) return "unknown"
  if (seconds < 60) return `${Math.round(seconds)}s`

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes} min`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h`

  const days = Math.floor(hours / 24)
  return `${days} d`
}

export function staleBannerMessage(ageSeconds: number | null): string {
  if (ageSeconds == null) {
    return "Live view is stale — the snapshot age is unknown"
  }
  return `Live view is stale — last update ${formatLayoutAge(ageSeconds)} ago`
}

export function StaleDataBanner({
  ageSeconds,
  className = "",
}: {
  ageSeconds: number | null
  className?: string
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-atomic="true"
      className={`flex items-start gap-2.5 rounded-lg border border-[var(--reserved)]/40 bg-[var(--status-reserved-bg)] px-3 py-2 text-[var(--reserved)] ${className}`.trim()}
    >
      <UntitledIcon name="alert" size={15} />
      <div className="space-y-0.5">
        <p className="text-xs font-semibold leading-snug">
          {staleBannerMessage(ageSeconds)}
        </p>
        <p className="text-[11px] leading-snug opacity-80">
          Showing the last received snapshot. It refreshes when the connection returns.
        </p>
      </div>
    </div>
  )
}
