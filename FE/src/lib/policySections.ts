import type { LotPolicyPdf } from "./types"

export const ownerPolicySections = [
  { id: "operations", title: "Entry & parking rules", description: "Arrival, barriers, parking spaces, and site conduct." },
  { id: "pricing", title: "Pricing & payment", description: "Rates, deposits, payment, and receipts." },
  { id: "cancellations", title: "Cancellations & refunds", description: "Changes, cancellations, and refund terms." },
  { id: "safety", title: "Safety & responsibility", description: "Vehicle care, incidents, and contact procedures." },
] as const

export type OwnerPolicySectionId = (typeof ownerPolicySections)[number]["id"]
export type OwnerPolicySectionDocuments = Partial<Record<OwnerPolicySectionId, LotPolicyPdf>>
export type OwnerPolicySectionText = { content: string; updatedAt: string; lotId?: string }
export type OwnerPolicySectionTexts = Partial<Record<OwnerPolicySectionId, OwnerPolicySectionText>>

export const starterOwnerPolicyText: Record<OwnerPolicySectionId, string> = {
  operations: "1. Follow site signs and staff directions when entering or leaving.\n2. Park only in the space assigned to your booking.\n3. Keep your booking or entry reference available if staff request it.\n4. Contact the site operator if access or space allocation does not match your booking.",
  pricing: "1. Parking charges follow the rates displayed for this site before booking.\n2. Review the estimated total and payment details before confirming.\n3. Ask the site operator about any charge you do not recognize before completing payment.",
  cancellations: "1. Review the cancellation conditions shown with your booking before confirming.\n2. Use the booking page to request a cancellation or change.\n3. Any eligible refund follows the SmartParking Platform Payment & Refund policy.",
  safety: "1. Follow traffic signs and keep access lanes and emergency exits clear.\n2. Lock your vehicle and take valuables with you.\n3. Report damage, hazards, or incidents to on-site staff as soon as possible.",
}

function key(ownerId: string, scope: string) {
  return `sp_owner_policy_sections_${ownerId}_${scope}`
}

function textKey(ownerId: string, scope: string) {
  return `sp_owner_policy_texts_${ownerId}_${scope}`
}

function read(ownerId: string, scope: string): OwnerPolicySectionDocuments {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(key(ownerId, scope)) || "{}")
    return value && typeof value === "object" && !Array.isArray(value) ? value as OwnerPolicySectionDocuments : {}
  } catch {
    return {}
  }
}

function readTexts(ownerId: string, scope: string): OwnerPolicySectionTexts {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(textKey(ownerId, scope)) || "{}")
    return value && typeof value === "object" && !Array.isArray(value) ? value as OwnerPolicySectionTexts : {}
  } catch {
    return {}
  }
}

export function getOwnerPolicySectionTexts(ownerId: string, lotId: string): OwnerPolicySectionTexts {
  const defaults = readTexts(ownerId, "all")
  return lotId === "all" ? defaults : { ...defaults, ...readTexts(ownerId, lotId) }
}

export function saveOwnerPolicySectionText(ownerId: string, scope: string, section: OwnerPolicySectionId, content: string) {
  const entry: OwnerPolicySectionText = { content, updatedAt: new Date().toISOString(), lotId: scope === "all" ? undefined : scope }
  localStorage.setItem(textKey(ownerId, scope), JSON.stringify({ ...readTexts(ownerId, scope), [section]: entry }))
  window.dispatchEvent(new Event("sp-data-change"))
}

export function deleteOwnerPolicySectionText(ownerId: string, scope: string, section: OwnerPolicySectionId) {
  const texts = readTexts(ownerId, scope)
  delete texts[section]
  localStorage.setItem(textKey(ownerId, scope), JSON.stringify(texts))
  window.dispatchEvent(new Event("sp-data-change"))
}

export function getOwnerPolicySectionDocuments(ownerId: string, lotId: string): OwnerPolicySectionDocuments {
  const defaults = read(ownerId, "all")
  return lotId === "all" ? defaults : { ...defaults, ...read(ownerId, lotId) }
}

export function saveOwnerPolicySectionDocument(ownerId: string, scope: string, section: OwnerPolicySectionId, document: LotPolicyPdf) {
  localStorage.setItem(key(ownerId, scope), JSON.stringify({ ...read(ownerId, scope), [section]: document }))
  window.dispatchEvent(new Event("sp-data-change"))
}

export function deleteOwnerPolicySectionDocument(ownerId: string, scope: string, section: OwnerPolicySectionId) {
  const documents = read(ownerId, scope)
  delete documents[section]
  localStorage.setItem(key(ownerId, scope), JSON.stringify(documents))
  window.dispatchEvent(new Event("sp-data-change"))
}
