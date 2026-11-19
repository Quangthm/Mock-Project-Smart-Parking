const timeZone = "Asia/Ho_Chi_Minh"
const calendar = new Intl.DateTimeFormat("en-CA", {
  timeZone, year: "numeric", month: "2-digit", day: "2-digit",
})

type VehicleType = "CAR" | "MOTORCYCLE"
type TimeRange = { start: string; end: string }
type MissingField = "vehicleType" | "vehicle" | "site" | "date" | "timeRange"

// Local intake for review. Text fields are user input, with no owner IDs or quote.
export type BookingPreview = {
  vehicleType: VehicleType | null
  vehicleText: string | null
  siteText: string | null
  startAt: string | null
  endAt: string | null
  timeZone: typeof timeZone
  missingFields: MissingField[]
}

type BookingFields = Pick<BookingPreview, "vehicleType" | "vehicleText" | "siteText"> & {
  day: string | null
  times: TimeRange | null
}
export type BookingTurn = {
  fields: BookingFields
  preview: BookingPreview
  reply: string
}

const emptyFields: BookingFields = {
  vehicleType: null, vehicleText: null, siteText: null, day: null, times: null,
}
const vehicleCategory = /^(?:ô tô|oto|xe (?:ô tô|oto|hơi|máy))(?=\s|$)/iu
const alternative = /(?:^|\s)(?:hay|hoặc)(?:\s|$)/iu
const explicitBooking = /^(?:(?:tôi )?muốn )?đặt (?:cho (?:tôi|mình) (?:một )?chỗ|chỗ cho (?:tôi|mình)|chỗ)(?=\s|$)/iu
const edit = /^(?:đổi|sửa|chỉnh)(?: sang)?\s+/iu
const siteCue = /^(?:bãi(?:\s|$)|(?:tại|ở)\s)/iu
const plateReply = /^(?:xe )?(\d{2}[ .-]?[a-z]{1,2}\d?[ .-]?\d{3}\.?\d{2})\??$/iu
const prompt: Record<MissingField, string> = {
  vehicleType: "Bạn gửi ô tô hay xe máy?",
  vehicle: "Bạn muốn dùng xe nào? Ví dụ: “xe 51A-123.45”.",
  site: "Bạn muốn gửi ở bãi nào? Ví dụ: “bãi FPT”.",
  date: "Bạn muốn gửi ngày nào? Ví dụ: “ngày mai” hoặc nhập ngày theo mẫu YYYY-MM-DD.",
  timeRange: "Bạn cho mình giờ bắt đầu và giờ kết thúc trong cùng ngày nhé. Ví dụ: “14h đến 16h” hoặc “02:00 đến 04:00”.",
}

function enteredLabel(text: string): string | null {
  const value = text.trim()
  if (!value || value.length > 100 || alternative.test(value) || /[,;!?]|\s[+/]\s/u.test(value))
    return null
  return value
}

function vehicleFields(text: string): Pick<BookingFields, "vehicleType" | "vehicleText"> {
  const category = text.match(vehicleCategory)
  const vehicleType = category ? (/máy$/iu.test(category[0]) ? "MOTORCYCLE" : "CAR") : null
  return {
    vehicleType,
    vehicleText: enteredLabel(category ? text.slice(category[0].length) : text.replace(/^xe(?:\s+|$)/iu, "")),
  }
}

function dayFor(expression: string, today: string): string | null {
  const offset = /^mai$/iu.test(expression) ? 1 : /^kia$/iu.test(expression) ? 2 : 0
  const iso = /^\d{4}-\d{2}-\d{2}$/.test(expression)
  const date = new Date(`${iso ? expression : today}T00:00:00Z`)
  if (!Number.isFinite(date.getTime()) || (iso && date.toISOString().slice(0, 10) !== expression)) return null
  date.setUTCDate(date.getUTCDate() + offset)
  const day = date.toISOString().slice(0, 10)
  return day >= today ? day : null
}

const clockToken = "(\\d{1,2})(?::(\\d{2})|(?:h| giờ)(\\d{2})?)"
const rangePattern = new RegExp(`^(?:từ )?${clockToken}\\s*(?:đến|tới|[-–—])\\s*${clockToken}(?: (sáng|chiều|tối))?$`, "iu")
function timeRange(text: string): TimeRange | null {
  const range = text.match(rangePattern)
  if (!range) return null
  const [, first, firstColon, firstHourMinute, last, lastColon, lastHourMinute, period] = range
  const start = Number(first)
  const end = Number(last)
  const clock = (hour: number, minute: string) => {
    if (hour > 23 || Number(minute) > 59 || (period && (hour === 0 || hour > 12))) return null
    const adjusted = period ? (hour % 12) + (period.toLowerCase() === "sáng" ? 0 : 12) : hour
    return `${String(adjusted).padStart(2, "0")}:${minute}`
  }
  const firstMinute = firstColon ?? firstHourMinute ?? "00"
  const lastMinute = lastColon ?? lastHourMinute ?? "00"
  const firstTime = clock(start, firstMinute)
  const lastTime = clock(end, lastMinute)
  if (!firstTime || !lastTime || firstTime >= lastTime) return null

  const otherTime = (raw: string, colon: string | undefined, minute: string) => {
    const hour = Number(raw)
    if (period || hour < 1 || hour > 12 || raw.startsWith("0") || (colon && raw.length === 2)) return null
    return clock((hour + 12) % 24, minute)
  }
  const otherStart = otherTime(first, firstColon, firstMinute)
  const otherEnd = otherTime(last, lastColon, lastMinute)
  if ((otherStart && otherStart < lastTime) || (otherEnd && firstTime < otherEnd)) return null
  return { start: firstTime, end: lastTime }
}

const dayToken = /(?:^|\s)(?:ngày )?((?:mai|kia)|hôm nay|\d{4}-\d{2}-\d{2})(?=\s|$)/giu
const dateCue = /^(?:ngày(?:\s|$)|hôm nay|mai(?:\s|$)|thứ\s|chủ nhật|\d{4}-|\d{1,2}\/)/iu
const timeCue = /^(?:từ\s|sáng(?:\s|$)|chiều(?:\s|$)|tối(?:\s|$)|\d{1,2}(?:h|:|\s|$))/iu
const timingStart = /\s+(?=(?:ngày\s|mai(?:\s|$)|kia(?:\s|$)|hôm nay(?:\s|$)|từ\s+\d|\d{1,2}(?:h|:)))/iu

function updateTime(fields: BookingFields, text: string, today: string): BookingFields {
  const days = Array.from(text.matchAll(dayToken))
  const hours = text.replace(dayToken, " ").trim().replace(/\s+/gu, " ")
  const unsupportedDate = /(?:^|\s)(?:ngày\s|thứ\s|chủ nhật|\d{1,2}\/\d)/iu.test(hours)
  let day = fields.day
  if (days.length > 0 || dateCue.test(text) || unsupportedDate)
    day = days.length === 1 && !unsupportedDate ? dayFor(days[0][1], today) : null
  const times = days.length === 1 && !hours ? fields.times : timeRange(hours)
  return { ...fields, day, times }
}

function siteText(text: string): string | null {
  const value = text.replace(/^bãi(?:\s+|$)/iu, "")
  return /^(?:thứ\s|đầu tiên$|cuối cùng$|đó$|này$|trước$|\d+$)/iu.test(value) ? null : enteredLabel(value)
}

function review(fields: BookingFields, now: Date, today: string, requestedReview: boolean): BookingTurn {
  if (fields.day && fields.day < today) fields = { ...fields, day: null }
  if (fields.day && fields.times && Date.parse(`${fields.day}T${fields.times.start}:00+07:00`) <= now.getTime())
    fields = { ...fields, times: null }
  const { day, times, vehicleType, vehicleText, siteText } = fields
  const missingFields: MissingField[] = []
  if (!day) missingFields.push("date")
  if (!times) missingFields.push("timeRange")
  if (!vehicleType) missingFields.push("vehicleType")
  if (!vehicleText) missingFields.push("vehicle")
  if (!siteText) missingFields.push("site")
  const preview: BookingPreview = {
    vehicleType, vehicleText, siteText,
    startAt: day && times ? `${day}T${times.start}:00+07:00` : null,
    endAt: day && times ? `${day}T${times.end}:00+07:00` : null,
    timeZone, missingFields,
  }
  const reply = missingFields.length
    ? (missingFields.includes("vehicle") && missingFields.includes("site") && missingFields[0] === "vehicle"
      ? `${prompt.vehicle} ${prompt.site}` : prompt[missingFields[0]])
    : requestedReview
      ? "Bạn có thể xem lại thông tin bên dưới. Việc xác nhận sẽ thực hiện trên form đặt chỗ khi form được kết nối."
      : "Bạn xem lại bản nháp bên dưới. Có thể nhắn “đổi xe sang xe máy”, “đổi bãi sang bãi FPT” hoặc “đổi giờ sang 15h đến 17h”."
  return { fields, preview, reply }
}

export function interpretBookingTurn(
  previous: BookingTurn | null, message: string, now = new Date(),
): BookingTurn | null {
  const text = message.normalize("NFC").trim().replace(/\s+/gu, " ")
    .replace(/ (?:nhé|nha|ạ)[?!.]*$/iu, "").replace(/[.!]+$/u, "")
  const today = calendar.format(now)
  const booking = text.match(explicitBooking)
  const details = booking ? text.slice(booking[0].length).trim().replace(/^cho\s+/iu, "") : ""
  // Booking-policy questions still use the ordinary FAQ/read flow.
  const isQuestion = text.includes("?") || /(?:^|\s)(?:bao nhiêu|bao lâu|mấy giờ|thế nào|được không|có được)(?:\s|$)/iu.test(text)
  if (booking && !isQuestion && (!details || vehicleCategory.test(details) || /^xe\s/iu.test(details) ||
      timeCue.test(details) || dateCue.test(details) || siteCue.test(details))) {
    let [request, site] = details.split(/\s+(?:tại|ở)\s+/iu, 2)
    if (siteCue.test(request)) {
      site = request.replace(/^(?:tại|ở)\s+/iu, "")
      request = ""
    }
    const siteTiming = site?.match(timingStart)
    if (siteTiming && site) {
      const timing = site.slice(siteTiming.index! + siteTiming[0].length).trim()
      site = site.slice(0, siteTiming.index).trim()
      request = `${request} ${timing}`.trim()
    }
    const vehiclePrefix = request.match(vehicleCategory)?.[0] ?? request.match(/^xe(?=\s|$)/iu)?.[0]
    const afterVehicle = vehiclePrefix ? request.slice(vehiclePrefix.length).trim() : request
    const timeIndex = afterVehicle.search(/(?:^|\s)(?=từ\s|(?:ngày|mai|kia|sáng|chiều|tối)(?:\s|$)|thứ\s|chủ nhật|hôm nay|\d{1,2}(?:h|:|\/|\s+(?:đến|tới))|\d{4}-)/iu)
    const vehicleInput = vehiclePrefix && (timeIndex < 0 ? request : `${vehiclePrefix} ${afterVehicle.slice(0, timeIndex)}`).trim()
    let timing = request
    if (vehiclePrefix && timeIndex < 0) timing = ""
    else if (vehiclePrefix) timing = afterVehicle.slice(timeIndex).trim()
    const fields = {
      ...emptyFields,
      ...(vehicleInput && !alternative.test(vehicleInput) ? vehicleFields(vehicleInput) : {}),
      siteText: site ? siteText(site) : null,
    }
    return review(updateTime(fields, timing, today), now, today, false)
  }
  if (!previous) return null

  const fields = previous.fields
  const plate = text.match(plateReply)
  if (plate) return review({ ...fields, vehicleText: plate[1] }, now, today, false)
  if (isQuestion) return null

  const revised = text.replace(edit, "").replace(/^chọn\s+(?=bãi(?:\s|$))/iu, "")
  const changed = revised !== text
  if (!changed) {
    const readText = text.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase().replaceAll("đ", "d")
    if (/^(?:danh sach )?xe cua toi$/u.test(readText) ||
        /^(?:ngay mai|hom nay|mai) .*\blich dat\b/u.test(readText)) return null
  }
  if (changed && /\s(?:(?:đổi|sửa|chỉnh)(?: sang)?\s+(?:xe|bãi|giờ|ngày)(?=\s|$)|và (?:xe|bãi|giờ|ngày) sang\s)/iu.test(revised)) {
    const current = review(fields, now, today, false)
    return { ...current, reply: "Bạn gửi từng thay đổi trong một tin nhắn nhé. Ví dụ: “đổi bãi sang bãi FPT”, rồi “đổi giờ sang 15h đến 17h”." }
  }
  if (/^(?:đồng ý|đặt luôn|xác nhận|xem(?: lại)? bản nháp)$/iu.test(text))
    return review(fields, now, today, true)
  if (/^bãi(?:\s|$)/iu.test(revised)) {
    const value = revised.replace(/^bãi\s+sang\s+/iu, "")
    return review({ ...fields, siteText: siteText(value) }, now, today, false)
  }
  if ((vehicleCategory.test(revised) || /^xe(?:\s|$)/iu.test(revised)) &&
      (changed || !/^xe (?:của tôi|đã lưu|nào)(?:\s|$)/iu.test(revised))) {
    const value = revised.replace(/^xe\s+sang\s+/iu, "").replace(/\s+thay\s+(?:cho\s+)?(?:ô tô|oto|xe máy|xe hơi)$/iu, "")
    if (vehicleCategory.test(value) && alternative.test(value))
      return review({ ...fields, vehicleType: null, vehicleText: null }, now, today, false)
    const vehicle = vehicleFields(value)
    const keepVehicle = value.trim() === value.match(vehicleCategory)?.[0] && !changed &&
      (!fields.vehicleType || vehicle.vehicleType === fields.vehicleType)
    return review({
      ...fields,
      vehicleType: vehicle.vehicleType ?? fields.vehicleType,
      vehicleText: keepVehicle ? fields.vehicleText : vehicle.vehicleText,
    }, now, today, false)
  }
  const timing = revised.replace(/^giờ(?: sang)?(?:\s+|$)/iu, "")
  if (timeCue.test(revised) || dateCue.test(revised) || (changed && timing !== revised))
    return review(updateTime(fields, timing, today), now, today, false)
  return null
}
