import type { OwnerPolicySectionId } from "./policySections"

const sectionIds: OwnerPolicySectionId[] = ["operations", "pricing", "cancellations", "safety"]

function decodePdfValue(value: string) {
  if (value.startsWith("<")) {
    const hex = value.slice(1, -1).replace(/\s/g, "")
    const bytes = Uint8Array.from(hex.match(/.{1,2}/g) ?? [], pair => Number.parseInt(pair, 16))
    if (bytes[0] === 0xfe && bytes[1] === 0xff) {
      const codes: number[] = []
      for (let i = 2; i + 1 < bytes.length; i += 2) codes.push((bytes[i] << 8) | bytes[i + 1])
      return String.fromCharCode(...codes)
    }
    return new TextDecoder().decode(bytes)
  }
  return value.slice(1, -1).replace(/\\([0-7]{1,3}|[nrtbf\\()])/g, (_, code: string) => {
    if (/^[0-7]/.test(code)) return String.fromCharCode(Number.parseInt(code, 8))
    return ({ n: "\n", r: "\r", t: "\t", b: "\b", f: "\f" } as Record<string, string>)[code] ?? code
  })
}

// Reads only the four named AcroForm fields in the supplied SmartParking PDF.
// Other PDF layouts remain viewable, but cannot be imported without a PDF parser.
export function extractOwnerPolicyPdfSections(bytes: ArrayBuffer): Partial<Record<OwnerPolicySectionId, string>> {
  const source = new TextDecoder("latin1").decode(bytes)
  const result: Partial<Record<OwnerPolicySectionId, string>> = {}
  for (const id of sectionIds) {
    const field = new RegExp(`/T\\s*\\(${id}\\)`).exec(source)
    if (!field) continue
    const start = source.lastIndexOf("endobj", field.index)
    const end = source.indexOf("endobj", field.index)
    const object = source.slice(start < 0 ? 0 : start + 6, end < 0 ? field.index + 10000 : end)
    const value = /\/V\s*(\((?:\\[\s\S]|[^\\)])*\)|<[0-9a-fA-F\s]+>)/.exec(object)
    if (!value) continue
    const content = decodePdfValue(value[1]).trim()
    if (content) result[id] = content
  }
  return result
}
