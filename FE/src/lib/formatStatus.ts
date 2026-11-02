/** Formats API status codes for display without changing their underlying values. */
export function formatStatus(value: string | null | undefined): string {
  if (!value) return "Unknown"
  return value
    .replace(/([a-z\d])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim()
    .toLowerCase()
    .replace(/^\p{L}/u, character => character.toUpperCase())
}
