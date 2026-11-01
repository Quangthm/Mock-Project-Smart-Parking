import { useEffect, useState } from "react"

const modelCapacityCache = new Map<string, number>()

type ModelNode = { name?: string; children?: number[] }

// Physical car-bay meshes in the supplied assets and in Owner uploads. A bay name
// may carry a Blender copy marker (`.001`) or a space-separated copy index (` 2`)
// that belongs to the bay's identity: the Parking House exposes e.g.
// PARKING_G_CAR_008 and PARKING_G_CAR_008.001 as two separate physical bays, so
// the suffix must be preserved instead of collapsed into the base name.
const BAY_NAME_PATTERNS = [
  /^CARPARKINGSPACE_\d+(?:[.\s]\d+)*$/,
  /^PARKING_EV_CAR_\d+(?:[.\s]\d+)*$/,
  /^B\d+_(?:EV_)?CAR_\d+(?:[.\s]\d+)*$/,
  /^PARKING_(?:G|L\d+)_(?:(?:EV_)?CAR|DISABLED)_\d+(?:[.\s]\d+)*$/,
  /^(?:SLOT|PARKING_SLOT|CAR_BAY)[_-]?[A-Z0-9_.\s-]+$/,
]

function carBayKey(name: string) {
  const clean = name.trim().toUpperCase()
  if (!clean) return null
  return BAY_NAME_PATTERNS.some(pattern => pattern.test(clean)) ? clean : null
}

/**
 * Counts distinct physical car bays. Multi-storey models usually nest bay nodes
 * under a floor group (Floor_G / Floor_L1 / B1 ...) and may reuse the same bay
 * name on every level, so the ancestor chain is part of the identity key. Exact
 * duplicate nodes collapse; identical names on different floors stay distinct.
 */
function countCarBays(nodes: ModelNode[]) {
  const parent = new Map<number, number>()
  nodes.forEach((node, index) => {
    for (const child of node.children ?? []) {
      if (!parent.has(child)) parent.set(child, index)
    }
  })

  const bays = new Set<string>()
  nodes.forEach((node, index) => {
    const bay = carBayKey(node.name ?? "")
    if (!bay) return
    const lineage: string[] = []
    const visited = new Set<number>()
    let current = parent.get(index)
    while (current !== undefined && !visited.has(current)) {
      visited.add(current)
      lineage.push((nodes[current]?.name ?? "").trim().toUpperCase())
      current = parent.get(current)
    }
    bays.add(`${lineage.reverse().join("/")}|${bay}`)
  })
  return bays.size
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
  const gltf = JSON.parse(new TextDecoder().decode(new Uint8Array(bytes, 20, jsonLength))) as { nodes?: ModelNode[] }
  const bayCount = countCarBays(gltf.nodes ?? [])
  if (bayCount === 0) throw new Error("No named car bays were found. Name slot meshes Slot_001, CarParkingSpace_001, or use the supplied model format.")
  modelCapacityCache.set(url, bayCount)
  return bayCount
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
