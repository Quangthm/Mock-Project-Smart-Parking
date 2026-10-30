export interface ContractDraftRecord {
  ownerName: string
  ownerEmail?: string
  businessName?: string
  lotName?: string
  ownerAcknowledgedAt?: string
  adminName?: string
  adminAcknowledgedAt?: string
  modelFileName?: string
  setupFingerprint?: string
}

const key = (applicationId: string) => `sp_contract_draft_${applicationId}`

export function loadContractDraft(applicationId: string): ContractDraftRecord | null {
  try {
    const value = JSON.parse(localStorage.getItem(key(applicationId)) ?? "null")
    return value && typeof value.ownerName === "string" ? value as ContractDraftRecord : null
  } catch { return null }
}

export function saveContractDraft(applicationId: string, record: ContractDraftRecord): void {
  localStorage.setItem(key(applicationId), JSON.stringify(record))
}

export function listOwnerContractCopies(email: string): { applicationId: string; record: ContractDraftRecord }[] {
  if (!email.trim()) return []
  const normalizedEmail = email.trim().toLowerCase()
  const copies: { applicationId: string; record: ContractDraftRecord }[] = []
  try {
    for (let index = 0; index < localStorage.length; index++) {
      const storageKey = localStorage.key(index)
      if (!storageKey?.startsWith("sp_contract_draft_")) continue
      const applicationId = storageKey.slice("sp_contract_draft_".length)
      const record = loadContractDraft(applicationId)
      if (record?.ownerEmail?.trim().toLowerCase() === normalizedEmail && record.ownerAcknowledgedAt && record.adminAcknowledgedAt) {
        copies.push({ applicationId, record })
      }
    }
  } catch { return [] }
  return copies.sort((a, b) => (b.record.adminAcknowledgedAt ?? "").localeCompare(a.record.adminAcknowledgedAt ?? ""))
}
