import { useId, useRef, useState } from "react"

export const quarterHourTimes = Array.from({ length: 96 }, (_, index) =>
  `${String(Math.floor(index / 4)).padStart(2, "0")}:${String((index % 4) * 15).padStart(2, "0")}`)
const weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
const monthLabel = new Intl.DateTimeFormat("en-VN", { month: "long", year: "numeric" })
const dayLabel = new Intl.DateTimeFormat("en-VN", { dateStyle: "full" })
const selectedLabel = new Intl.DateTimeFormat("en-VN", { dateStyle: "medium" })

export function localDate(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`
}

export function ParkingCalendar({ month, onMonthChange, selected, today, min, max, onSelect, isDateDisabled }: {
  month: Date
  onMonthChange: (month: Date) => void
  selected: string
  today: string
  min?: string
  max?: string
  onSelect: (date: string) => void
  isDateDisabled?: (date: string) => boolean
}) {
  const year = month.getFullYear()
  const monthIndex = month.getMonth()
  const offset = (month.getDay() + 6) % 7
  const dayCount = new Date(year, monthIndex + 1, 0).getDate()
  const monthKey = localDate(month).slice(0, 7)
  return <div className="booking-calendar">
    <div className="booking-calendar-heading">
      <strong aria-live="polite">{monthLabel.format(month)}</strong>
      <div>
        <button type="button" aria-label="Previous month" disabled={!!min && monthKey <= min.slice(0, 7)}
          onClick={() => onMonthChange(new Date(year, monthIndex - 1, 1))}>‹</button>
        <button type="button" aria-label="Next month" disabled={!!max && monthKey >= max.slice(0, 7)}
          onClick={() => onMonthChange(new Date(year, monthIndex + 1, 1))}>›</button>
      </div>
    </div>
    <div className="booking-calendar-grid">
      {weekdays.map(day => <span key={day} className="booking-calendar-weekday">{day}</span>)}
      {Array.from({ length: offset }, (_, index) => <span key={`blank-${index}`} />)}
      {Array.from({ length: dayCount }, (_, index) => {
        const day = index + 1
        const current = new Date(year, monthIndex, day)
        const date = localDate(current)
        return <button key={date} type="button" aria-label={dayLabel.format(current)}
          disabled={Boolean((min && date < min.slice(0, 10)) || (max && date > max.slice(0, 10)) || isDateDisabled?.(date))}
          aria-pressed={selected === date} aria-current={date === today ? "date" : undefined}
          className={`${selected === date ? "is-selected " : ""}${date === today ? "is-today" : ""}`}
          onClick={() => onSelect(date)}>{day}</button>
      })}
    </div>
  </div>
}

export function ParkingDateField({ label, value, onChange, min, max, withTime = false, disabled = false }: {
  label: string
  value: string
  onChange: (value: string) => void
  min?: string
  max?: string
  withTime?: boolean
  disabled?: boolean
}) {
  const id = useId()
  const trigger = useRef<HTMLButtonElement>(null)
  const [open, setOpen] = useState(false)
  const [month, setMonth] = useState(() => new Date())
  const [draftTime, setDraftTime] = useState("")
  const [draftDate, setDraftDate] = useState("")
  const selected = value.slice(0, 10)
  const date = new Date(`${selected}T00:00`)
  const summary = selected && Number.isFinite(date.getTime()) ? selectedLabel.format(date) : "Choose a date"

  function toggle() {
    if (!open) {
      const now = localDate(new Date())
      const reference = selected || (min && now < min.slice(0, 10) ? min.slice(0, 10) :
        max && now > max.slice(0, 10) ? max.slice(0, 10) : now)
      const first = new Date(`${reference.slice(0, 7)}-01T00:00`)
      setMonth(first)
      setDraftDate(selected)
      setDraftTime(value.slice(11, 16))
    }
    setOpen(!open)
  }

  const combined = `${draftDate}T${draftTime}`
  const canApply = draftDate && draftTime && (!min || combined >= min) && (!max || combined <= max)
  function finish(next: string) {
    onChange(next)
    setOpen(false)
    trigger.current?.focus()
  }

  return <div className="min-w-0 space-y-2">
    <span id={`${id}-label`} className="block text-sm font-medium text-[var(--fg)]">{label}</span>
    <div className="booking-datetime-picker" onKeyDown={event => {
      if (!open || event.key !== "Escape") return
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
      trigger.current?.focus()
    }}>
      <button ref={trigger} type="button" className="booking-datetime-field w-full text-left" aria-expanded={open && !disabled}
        aria-controls={id} aria-labelledby={`${id}-label ${id}-value`} disabled={disabled} onClick={toggle}>
        <strong id={`${id}-value`}>{summary}{withTime && selected && value.slice(11, 16) ? ` · ${value.slice(11, 16)}` : ""}</strong>
      </button>
      <div id={id} hidden={!open || disabled}>
        {open && <ParkingCalendar month={month} onMonthChange={setMonth} selected={withTime ? draftDate : selected}
          today={localDate(new Date())} min={min} max={max} onSelect={day => {
            if (withTime) setDraftDate(day)
            else finish(day)
          }} />}
        {withTime ? <div className="flex flex-wrap items-end gap-2 px-3 pb-3">
          <label className="label min-w-0 flex-1">
            Time (local)
            <input type="time" className="input mt-1 w-full" value={draftTime}
              onChange={event => setDraftTime(event.target.value)} />
          </label>
          <button type="button" className="btn-primary" disabled={!canApply}
            onClick={() => { if (canApply) finish(combined) }}>Apply</button>
        </div> : value && <div className="px-3 pb-3">
          <button type="button" className="btn-ghost" onClick={() => finish("")}>Clear date</button>
        </div>}
      </div>
    </div>
  </div>
}
