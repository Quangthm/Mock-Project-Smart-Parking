import type { Site } from "./parkingApi"
import { store } from "./store"
import { copyOwnerModel } from "./3d/ownerModelStorage"

const normalize = (value: string) => value.trim().replace(/\s+/g, " ").toLocaleLowerCase()

// Registration drafts use an application ID; approved sites use a different ID.
export async function reconcileOwnerLotSetups(sites: Site[], ownerId: string): Promise<void> {
  if (!ownerId) return
  const lots = store.getLots()
  for (const site of sites) {
    const exact = lots.find(lot => lot.id === site.id)
    const matches = exact ? [exact] : lots.filter(lot =>
      lot.ownerId === ownerId && normalize(lot.name) === normalize(site.name) && normalize(lot.address) === normalize(site.address))
    if (matches.length !== 1) continue
    const source = matches[0]
    if (source.ownerId !== ownerId) continue
    const modelUrl = source.id === site.id ? null : await copyOwnerModel(source.id, site.id).catch(() => null)
    const updated = { ...source, id: site.id, ownerId,
      name: site.name, address: site.address,
      status: site.isActive ? "active" as const : "inactive" as const,
      lat: site.latitude ?? source.lat, lng: site.longitude ?? source.lng,
      modelUrl: modelUrl ?? source.modelUrl }
    if (source.id !== updated.id || source.name !== updated.name || source.address !== updated.address ||
      source.status !== updated.status || source.lat !== updated.lat || source.lng !== updated.lng ||
      source.modelUrl !== updated.modelUrl) store.saveLot(updated)
  }
}
