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

export const contractDraftStorageKey = (applicationId: string) => `sp_contract_draft_${applicationId}`

export interface OwnerContractHandoff {
  applicationId: string
  ownerName: string
  ownerEmail: string
  businessName: string
  lotName: string
  setupFingerprint: string
  modelFileName?: string
}

const ownerHandoffKey = (applicationId: string) => `sp_owner_contract_handoff_${applicationId}`

export function saveOwnerContractHandoff(handoff: OwnerContractHandoff): void {
  localStorage.setItem(ownerHandoffKey(handoff.applicationId), JSON.stringify(handoff))
}

export function loadOwnerContractHandoff(applicationId: string): OwnerContractHandoff | null {
  try {
    const value = JSON.parse(localStorage.getItem(ownerHandoffKey(applicationId)) ?? "null") as OwnerContractHandoff | null
    return value?.applicationId === applicationId && typeof value.setupFingerprint === "string" &&
      typeof value.ownerEmail === "string" ? value : null
  } catch { return null }
}

export function loadContractDraft(applicationId: string): ContractDraftRecord | null {
  try {
    const value = JSON.parse(localStorage.getItem(contractDraftStorageKey(applicationId)) ?? "null")
    return value && typeof value.ownerName === "string" ? value as ContractDraftRecord : null
  } catch { return null }
}

export function saveContractDraft(applicationId: string, record: ContractDraftRecord): void {
  localStorage.setItem(contractDraftStorageKey(applicationId), JSON.stringify(record))
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
