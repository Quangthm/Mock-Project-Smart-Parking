import { useEffect, useState } from "react"
import {
  getStaticModelMetadata,
  type ModelFloorMetadata,
} from "./parking3DConfig"

export interface ModelStructureAnalysis {
  totalCapacity: number
  floors: ModelFloorMetadata[]
}

const modelCapacityCache = new Map<string, number>()
const modelStructureCache = new Map<string, ModelStructureAnalysis>()

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

export function carBayKey(name: string): string | null {
  const clean = name.trim().toUpperCase()
  if (!clean) return null
  return BAY_NAME_PATTERNS.some((pattern) => pattern.test(clean)) ? clean : null
}

const FLOOR_PATTERNS = [
  /^Floor[_-]?([A-Z0-9]+)$/i,
  /^Level[_-]?([A-Z0-9]+)$/i,
  /^(B\d+)$/i,
  /^(Outdoor(?:Parking)?)$/i,
  /^(?:Tang[_-]?([A-Z0-9]+))$/i,
]

export function parseFloorKey(name: string): string | null {
  for (const pat of FLOOR_PATTERNS) {
    const m = (name || "").match(pat)
    if (m) return m[1] || m[0]
  }
  return null
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

/**
 * Analyzes floor groupings and counts car bays per floor from a GLTF scene graph.
 */
export function analyzeModelFloors(nodes: ModelNode[]): ModelFloorMetadata[] {
  const floorNodes: Array<{ index: number; name: string; key: string }> = []
  nodes.forEach((n, idx) => {
    const key = parseFloorKey(n.name ?? "")
    if (
      key &&
      !/slab|wall|door|sign|curb|fixture|arrow/i.test(n.name ?? "") &&
      ((n.children && n.children.length > 0) || !carBayKey(n.name ?? ""))
    ) {
      floorNodes.push({ index: idx, name: n.name ?? "", key: key.toUpperCase() })
    }
  })

  if (floorNodes.length === 0) {
    const total = countCarBays(nodes)
    return [
      {
        floorKey: "Ground",
        displayName: "Khu vực ngoài trời",
        capacity: total,
        nodeName: "OutdoorParking",
      },
    ]
  }

  return floorNodes.map((fn) => {
    const bays = new Set<string>()
    const queue = [fn.index]
    const visited = new Set<number>()
    while (queue.length > 0) {
      const curr = queue.shift()!
      if (visited.has(curr)) continue
      visited.add(curr)
      const node = nodes[curr]
      if (!node) continue
      const bay = carBayKey(node.name ?? "")
      if (bay) {
        bays.add(bay)
      }
      for (const child of node.children ?? []) {
        queue.push(child)
      }
    }

    let displayName = `Tầng ${fn.key}`
    if (/^outdoor/i.test(fn.key) || fn.key === "GROUND") {
      displayName = "Khu vực ngoài trời"
    }
    return {
      floorKey: fn.key,
      displayName,
      capacity: bays.size,
      nodeName: fn.name,
    }
  })
}

/**
 * Reads GLTF JSON header by streaming only the required header bytes instead
 * of downloading the whole multi-megabyte binary geometry chunk.
 */
async function fetchGltfJsonHeader(url: string): Promise<{ nodes?: ModelNode[] }> {
  // 1. Try reading via Range request if network URL
  if (url.startsWith("http://") || url.startsWith("https://")) {
    try {
      const rangeRes = await fetch(url, { headers: { Range: "bytes=0-65535" } })
      if (rangeRes.status === 206) {
        const chunk = await rangeRes.arrayBuffer()
        const view = new DataView(chunk)
        if (
          chunk.byteLength >= 20 &&
          view.getUint32(0, true) === 0x46546c67 &&
          view.getUint32(16, true) === 0x4e4f534a
        ) {
          const jsonLength = view.getUint32(12, true)
          if (20 + jsonLength <= chunk.byteLength) {
            return JSON.parse(
              new TextDecoder().decode(new Uint8Array(chunk, 20, jsonLength)),
            )
          } else {
            const fullJsonRes = await fetch(url, {
              headers: { Range: `bytes=0-${20 + jsonLength - 1}` },
            })
            if (fullJsonRes.status === 206) {
              const fullChunk = await fullJsonRes.arrayBuffer()
              return JSON.parse(
                new TextDecoder().decode(new Uint8Array(fullChunk, 20, jsonLength)),
              )
            }
          }
        }
      }
    } catch {
      // Fall through to streaming / normal fetch
    }
  }

  // 2. Fetch with streaming reader to cancel download immediately after JSON chunk
  const response = await fetch(url)
  if (!response.ok) throw new Error("The 3D model could not be loaded.")

  if (response.body && typeof response.body.getReader === "function") {
    const reader = response.body.getReader()
    const chunks: Uint8Array[] = []
    let receivedBytes = 0
    let jsonLength: number | null = null

    try {
      while (true) {
        const { done, value } = await reader.read()
        if (value) {
          chunks.push(value)
          receivedBytes += value.byteLength
        }

        if (jsonLength === null && receivedBytes >= 20) {
          const headerBuf = new Uint8Array(20)
          let pos = 0
          for (const c of chunks) {
            const take = Math.min(c.byteLength, 20 - pos)
            headerBuf.set(c.subarray(0, take), pos)
            pos += take
            if (pos >= 20) break
          }
          const view = new DataView(headerBuf.buffer)
          if (
            view.getUint32(0, true) !== 0x46546c67 ||
            view.getUint32(16, true) !== 0x4e4f534a
          ) {
            reader.cancel().catch(() => {})
            throw new Error("Select a valid GLB parking model.")
          }
          jsonLength = view.getUint32(12, true)
        }

        if (jsonLength !== null && receivedBytes >= 20 + jsonLength) {
          reader.cancel().catch(() => {})
          break
        }

        if (done) break
      }
    } catch (streamError) {
      reader.cancel().catch(() => {})
      throw streamError
    }

    const combined = new Uint8Array(receivedBytes)
    let offset = 0
    for (const c of chunks) {
      combined.set(c, offset)
      offset += c.byteLength
    }

    if (jsonLength === null || 20 + jsonLength > combined.byteLength) {
      throw new Error("The 3D model is incomplete.")
    }

    return JSON.parse(
      new TextDecoder().decode(combined.subarray(20, 20 + jsonLength)),
    )
  }

  // 3. Fallback for environments without stream reader
  const bytes = await response.arrayBuffer()
  const view = new DataView(bytes)
  if (
    bytes.byteLength < 20 ||
    view.getUint32(0, true) !== 0x46546c67 ||
    view.getUint32(16, true) !== 0x4e4f534a
  ) {
    throw new Error("Select a valid GLB parking model.")
  }
  const jsonLength = view.getUint32(12, true)
  if (20 + jsonLength > bytes.byteLength) {
    throw new Error("The 3D model is incomplete.")
  }
  return JSON.parse(
    new TextDecoder().decode(new Uint8Array(bytes, 20, jsonLength)),
  ) as { nodes?: ModelNode[] }
}

/**
 * Returns structural analysis of a 3D parking model (floors and capacities).
 * Static system models return in 0ms without network roundtrips.
 */
export async function getOrAnalyzeModelStructure(
  url: string,
): Promise<ModelStructureAnalysis> {
  const staticMeta = getStaticModelMetadata(url)
  if (staticMeta) {
    return {
      totalCapacity: staticMeta.totalCapacity,
      floors: staticMeta.floors,
    }
  }

  const cached = modelStructureCache.get(url)
  if (cached) return cached

  const gltf = await fetchGltfJsonHeader(url)
  const nodes = gltf.nodes ?? []
  const floors = analyzeModelFloors(nodes)
  const totalCapacity = floors.reduce((sum, f) => sum + f.capacity, 0) || countCarBays(nodes)

  if (totalCapacity === 0) {
    throw new Error(
      "No named car bays were found. Name slot meshes Slot_001, CarParkingSpace_001, or use the supplied model format.",
    )
  }

  const result: ModelStructureAnalysis = { totalCapacity, floors }
  modelStructureCache.set(url, result)
  modelCapacityCache.set(url, totalCapacity)
  return result
}

export async function readModelCarCapacity(url: string): Promise<number> {
  // 0ms fast-path for system models
  const staticMeta = getStaticModelMetadata(url)
  if (staticMeta) return staticMeta.totalCapacity

  const cached = modelCapacityCache.get(url)
  if (cached !== undefined) return cached

  const structure = await getOrAnalyzeModelStructure(url)
  return structure.totalCapacity
}

export function useModelCarCapacity(url: string) {
  const [result, setResult] = useState<{
    url: string
    capacity: number | null
    error: string
  }>(() => {
    const staticMeta = getStaticModelMetadata(url)
    if (staticMeta) {
      return { url, capacity: staticMeta.totalCapacity, error: "" }
    }
    const cached = modelCapacityCache.get(url)
    if (cached !== undefined) {
      return { url, capacity: cached, error: "" }
    }
    return { url, capacity: null, error: "" }
  })

  useEffect(() => {
    if (!url) return
    const staticMeta = getStaticModelMetadata(url)
    if (staticMeta) {
      setResult({ url, capacity: staticMeta.totalCapacity, error: "" })
      return
    }

    let active = true
    readModelCarCapacity(url)
      .then((capacity) => {
        if (active) setResult({ url, capacity, error: "" })
      })
      .catch((error) => {
        if (active)
          setResult({
            url,
            capacity: null,
            error:
              error instanceof Error
                ? error.message
                : "Could not inspect the 3D model.",
          })
      })
    return () => {
      active = false
    }
  }, [url])

  return result.url === url ? result : { url, capacity: null, error: "" }
}

export function useModelStructure(url: string) {
  const [result, setResult] = useState<{
    url: string
    analysis: ModelStructureAnalysis | null
    loading: boolean
    error: string
  }>(() => {
    const staticMeta = getStaticModelMetadata(url)
    if (staticMeta) {
      return {
        url,
        analysis: {
          totalCapacity: staticMeta.totalCapacity,
          floors: staticMeta.floors,
        },
        loading: false,
        error: "",
      }
    }
    const cached = modelStructureCache.get(url)
    if (cached) {
      return { url, analysis: cached, loading: false, error: "" }
    }
    return { url, analysis: null, loading: Boolean(url), error: "" }
  })

  useEffect(() => {
    if (!url) {
      setResult({ url: "", analysis: null, loading: false, error: "" })
      return
    }

    const staticMeta = getStaticModelMetadata(url)
    if (staticMeta) {
      setResult({
        url,
        analysis: {
          totalCapacity: staticMeta.totalCapacity,
          floors: staticMeta.floors,
        },
        loading: false,
        error: "",
      })
      return
    }

    let active = true
    getOrAnalyzeModelStructure(url)
      .then((analysis) => {
        if (active) setResult({ url, analysis, loading: false, error: "" })
      })
      .catch((err) => {
        if (active)
          setResult({
            url,
            analysis: null,
            loading: false,
            error:
              err instanceof Error
                ? err.message
                : "Could not inspect 3D model structure.",
          })
      })
    return () => {
      active = false
    }
  }, [url])

  return result.url === url
    ? result
    : { url, analysis: null, loading: Boolean(url), error: "" }
}
