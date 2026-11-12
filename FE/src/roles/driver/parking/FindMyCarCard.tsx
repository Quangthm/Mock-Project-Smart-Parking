import { useCallback, useEffect, useId, useRef, useState } from "react"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import {
  allocationStatusLabel,
  getCurrentVehicleLocation,
  locationDetailLines,
  type LocationPrecision,
  type VehicleLocationData,
} from "../../../lib/driverVehicleLocationApi"
import "./find-my-car.css"

const recordedTimeFormatter = new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" })

const precisionBadges: Record<LocationPrecision, { className: string; label: string }> = {
  SLOT: { className: "badge is-slot", label: "Vị trí chính xác (Exact Slot)" },
  ZONE: { className: "badge is-zone", label: "Khu vực (Zone-only)" },
  UNKNOWN: { className: "badge is-unknown", label: "Đang cập nhật (Pending)" },
}

function formatTimestamp(value: string | null): string {
  if (!value) return "không rõ"
  const time = new Date(value)
  if (Number.isNaN(time.getTime())) return value
  return recordedTimeFormatter.format(time)
}

function DetailList({ lines }: { lines: string[] }) {
  return <dl className="find-my-car-details">
    {lines.map((line, index) => {
      const separator = line.indexOf(": ")
      const label = separator === -1 ? line : line.slice(0, separator)
      const value = separator === -1 ? "" : line.slice(separator + 2)
      return <div key={`${label}-${index}`}>
        <dt>{label}</dt>
        {value && <dd>{value}</dd>}
      </div>
    })}
  </dl>
}

export function FindMyCarCard() {
  const [data, setData] = useState<VehicleLocationData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const mountedRef = useRef(true)
  const requestRef = useRef(0)
  const titleId = useId()

  const reload = useCallback(() => {
    const requestId = ++requestRef.current
    setLoading(true)
    setError(null)
    getCurrentVehicleLocation()
      .then((result) => {
        if (!mountedRef.current || requestId !== requestRef.current) return
        setData(result)
        setLoading(false)
      })
      .catch(() => {
        if (!mountedRef.current || requestId !== requestRef.current) return
        setError("Không thể tải vị trí xe. Vui lòng thử lại.")
        setLoading(false)
      })
  }, [])

  useEffect(() => {
    mountedRef.current = true
    reload()
    return () => {
      mountedRef.current = false
      requestRef.current += 1
    }
  }, [reload])

  // No active session is a valid final result: the card renders nothing.
  if (!loading && !error && data === null) return null

  const precision = data?.recordedLocation.precision ?? null
  const badge = precision ? precisionBadges[precision] : null
  const recordedAt = data ? data.recordedAt ?? data.freshness.generatedAt : null

  return (
    <section className="find-my-car-card" aria-labelledby={titleId} aria-busy={loading}>
      <header className="find-my-car-header">
        <div className="find-my-car-heading">
          <span className="find-my-car-icon" aria-hidden="true"><UntitledIcon name="car" size={20} /></span>
          <div>
            <h2 id={titleId}>Find my car</h2>
            <p className="find-my-car-subtitle">Tìm xe của tôi</p>
          </div>
        </div>
        {badge && <span className={badge.className}>{badge.label}</span>}
      </header>

      {loading ? (
        <div className="find-my-car-state" role="status" aria-live="polite">
          <span className="find-my-car-spinner" aria-hidden="true" />
          <p>Đang tải vị trí xe...</p>
        </div>
      ) : error ? (
        <div className="find-my-car-state is-error" role="alert">
          <UntitledIcon name="alert" size={18} />
          <p>{error}</p>
          <button type="button" className="find-my-car-reload" onClick={reload}>Tải lại</button>
        </div>
      ) : data ? (
        <>
          {data.freshness.isStale && (
            <div className="find-my-car-stale" role="status" aria-live="polite">
              <UntitledIcon name="alert" size={18} />
              <p>Vị trí xe có thể chưa cập nhật do mất kết nối. Dữ liệu ghi nhận lúc {formatTimestamp(recordedAt)}.</p>
              <button type="button" className="find-my-car-reload" onClick={reload}>Tải lại</button>
            </div>
          )}

          {data.recordedLocation.precision === "SLOT" && (
            <div className="find-my-car-body">
              <DetailList lines={locationDetailLines(data.recordedLocation)} />
              {allocationStatusLabel(data.allocationStatus) && (
                <span className="find-my-car-allocation" data-status={data.allocationStatus ?? undefined}>
                  {allocationStatusLabel(data.allocationStatus)}
                </span>
              )}
              <p className="find-my-car-meta">Ghi nhận lúc <strong>{formatTimestamp(recordedAt)}</strong></p>
            </div>
          )}

          {data.recordedLocation.precision === "ZONE" && (
            <div className="find-my-car-body">
              <DetailList lines={locationDetailLines(data.recordedLocation)} />
              <p className="find-my-car-note">
                <UntitledIcon name="parking" size={16} />
                <span>Khu vực đỗ xe tự do/xe máy. Không phân bổ ô đỗ cụ thể.</span>
              </p>
            </div>
          )}

          {data.recordedLocation.precision === "UNKNOWN" && (
            <p className="find-my-car-pending">
              <UntitledIcon name="clock" size={18} />
              <span>Xe đã vào bãi. Vị trí cụ thể đang được hệ thống xác định.</span>
            </p>
          )}
        </>
      ) : null}
    </section>
  )
}
