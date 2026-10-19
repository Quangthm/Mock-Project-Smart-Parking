export function digitsOnly(value: string): string {
  return value.replace(/[^0-9]/g, "")
}

export function formatGroupedDigits(value: string): string {
  return digitsOnly(value).replace(/\B(?=(\d{3})+(?!\d))/g, ",")
}

export function caretAfterDigits(formatted: string, digitCount: number): number {
  if (digitCount <= 0) return 0

  let seen = 0
  for (let index = 0; index < formatted.length; index += 1) {
    if (formatted[index] >= "0" && formatted[index] <= "9") {
      seen += 1
      if (seen === digitCount) return index + 1
    }
  }

  return formatted.length
}
