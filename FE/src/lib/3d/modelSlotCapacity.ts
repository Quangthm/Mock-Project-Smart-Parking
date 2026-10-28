import { useEffect, useState } from "react"

const modelCapacityCache = new Map<string, number>()

// The supported GLB assets name each physical car bay. Blender duplicate suffixes
// identify copies of one bay mesh, so they must not increase site capacity.
function carBayKey(name: string) {
  const clean = name.replace(/(?:\.\d{3}| \d+)+$/, "").toUpperCase()
  if (/^CARPARKINGSPACE_\d+$/.test(clean)) return clean
  if (/^PARKING_EV_CAR_\d+$/.test(clean)) return clean
  if (/^B\d+_(?:EV_)?CAR_\d+$/.test(clean)) return clean
  if (/^PARKING_(?:G|L\d+)_(?:(?:EV_)?CAR|DISABLED)_\d+$/.test(clean)) return clean
  if (/^(?:SLOT|PARKING_SLOT|CAR_BAY)[_-]?[A-Z0-9_-]+$/.test(clean)) return clean
  return null
}

export async function readModelCarCapacity(url: string): Promise<number> {
  const cached = modelCapacityCache.get(url)
  if (cached !== undefined) return cached
  const response = await fetch(url)
  if (!response.ok) throw new Error("The 3D model could not be loaded.")
  const bytes = await response.arrayBuffer()
  const view = new DataView(bytes)
  if (bytes.byteLength < 20 || view.getUint32(0, true) !== 0x46546c67 || view.getUint32(16, true) !== 0x4e4f534a) {
    throw new Error("Select a valid GLB parking model.")
  }
  const jsonLength = view.getUint32(12, true)
  if (20 + jsonLength > bytes.byteLength) throw new Error("The 3D model is incomplete.")
  const gltf = JSON.parse(new TextDecoder().decode(new Uint8Array(bytes, 20, jsonLength))) as { nodes?: Array<{ name?: string }> }
  const bays = new Set((gltf.nodes ?? []).map(node => carBayKey(node.name ?? "")).filter((key): key is string => Boolean(key)))
  if (bays.size === 0) throw new Error("No named car bays were found. Name slot meshes Slot_001, CarParkingSpace_001, or use the supplied model format.")
  modelCapacityCache.set(url, bays.size)
  return bays.size
}

export function useModelCarCapacity(url: string) {
  const [result, setResult] = useState<{ url: string; capacity: number | null; error: string }>({ url: "", capacity: null, error: "" })
  useEffect(() => {
    let active = true
    readModelCarCapacity(url)
      .then(capacity => { if (active) setResult({ url, capacity, error: "" }) })
      .catch(error => { if (active) setResult({ url, capacity: null, error: error instanceof Error ? error.message : "Could not inspect the 3D model." }) })
    return () => { active = false }
  }, [url])
  return result.url === url ? result : { url, capacity: null, error: "" }
}
