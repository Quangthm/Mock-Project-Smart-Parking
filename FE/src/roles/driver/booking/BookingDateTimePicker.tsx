import { useEffect, useState } from "react"
import { localDate, ParkingCalendar, quarterHourTimes } from "../../../components/forms/ParkingDateField"

type Field = "start" | "end"

function readableDateTime(value: string) {
  if (!value) return "Choose date and time"
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? "Choose date and time" : new Intl.DateTimeFormat("en-VN", {
    day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false,
  }).format(date)
}

export function BookingDateTimePicker({ start, end, onStartChange, onEndChange }: {
  start: string
  end: string
  onStartChange: (value: string) => void
  onEndChange: (value: string) => void
}) {
  const [field, setField] = useState<Field>("start")
  const [now, setNow] = useState(() => new Date())
  const [month, setMonth] = useState(() => {
    const date = start ? new Date(start) : new Date()
    return new Date(date.getFullYear(), date.getMonth(), 1)
  })
  const [draftDate, setDraftDate] = useState(() => start.slice(0, 10))
  const [draftTime, setDraftTime] = useState(() => start.slice(11, 16))

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  function chooseField(next: Field) {
    if (next === "end" && !start) return
    setField(next)
    const value = next === "start" ? start : end
    const reference = value || (next === "end" ? start : "")
    setDraftDate(value.slice(0, 10))
    setDraftTime(value.slice(11, 16))
    if (reference) {
      const date = new Date(reference)
      setMonth(new Date(date.getFullYear(), date.getMonth(), 1))
    }
  }

  const min = field === "end" && start ? new Date(start) : now
  const today = localDate(now)
  function validChoice(date: string, time: string) {
    const candidate = new Date(`${date}T${time}`)
    return !Number.isNaN(candidate.getTime()) && candidate.getTime() > min.getTime()
  }

  function commit(date: string, time: string) {
    if (!date || !time || !validChoice(date, time)) return
    const value = `${date}T${time}`
    if (field === "start") {
      onStartChange(value)
      if (end && new Date(end).getTime() <= new Date(value).getTime()) onEndChange("")
    } else {
      onEndChange(value)
    }
  }

  function chooseDay(date: string) {
    setDraftDate(date)
    if (draftTime && validChoice(date, draftTime)) commit(date, draftTime)
    else setDraftTime("")
  }

  function chooseTime(time: string) {
    setDraftTime(time)
    commit(draftDate, time)
    if (field === "start" && draftDate && validChoice(draftDate, time)) {
      setField("end")
      setDraftTime("")
    }
  }

  return (
    <div className="booking-datetime-picker">
      <div className="booking-datetime-fields">
        <button type="button" className={`booking-datetime-field${field === "start" ? " is-active" : ""}`} onClick={() => chooseField("start")} aria-pressed={field === "start"}>
          <span>Arrival</span><strong>{readableDateTime(start)}</strong>
        </button>
        <button type="button" className={`booking-datetime-field${field === "end" ? " is-active" : ""}`} onClick={() => chooseField("end")} aria-pressed={field === "end"} disabled={!start}>
          <span>Departure</span><strong>{readableDateTime(end)}</strong>
        </button>
      </div>
      <div className="booking-datetime-body">
        <ParkingCalendar month={month} onMonthChange={setMonth} selected={draftDate} today={today}
          min={today} isDateDisabled={date => !quarterHourTimes.some(time => validChoice(date, time))}
          onSelect={chooseDay} />
        <div className="booking-time-list">
          <strong>Time <small>(local)</small></strong>
          <div role="group" aria-label={`${field === "start" ? "Arrival" : "Departure"} time`}>
            {draftDate ? quarterHourTimes.map((time) => <button key={time} type="button" disabled={!validChoice(draftDate, time)} className={time === draftTime ? "is-selected" : ""} onClick={() => chooseTime(time)} aria-pressed={time === draftTime}>{time}</button>) : <p>Choose a date first.</p>}
          </div>
        </div>
      </div>
      <p className="booking-datetime-hint">Choose an arrival after the current time, then a departure after arrival. Times are 15 minutes apart in your device’s local timezone.</p>
    </div>
  )
}
