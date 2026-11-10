import * as THREE from "three"
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js"

interface CachedGltfEntry {
  scene: THREE.Group
}

const gltfMemoryCache = new Map<string, Promise<CachedGltfEntry>>()

/**
 * Loads a GLTF/GLB model from cache or fetches it if not present.
 * Returns an isolated clone of the parsed scene graph (Three.js scene clone)
 * with dedicated material clones so that each viewer component instance receives its own
 * scene hierarchy and materials without re-downloading or re-parsing the GLB asset,
 * and without material disposal in one viewer corrupting the shared cached scene.
 */
export async function loadGltfWithCache(url: string): Promise<THREE.Group> {
  let pending = gltfMemoryCache.get(url)

  if (!pending) {
    pending = new Promise<CachedGltfEntry>((resolve, reject) => {
      const loader = new GLTFLoader()
      loader.load(
        url,
        (gltf) => {
          resolve({ scene: gltf.scene })
        },
        undefined,
        (err) => {
          // Do not cache failed load promises
          gltfMemoryCache.delete(url)
          reject(err)
        },
      )
    })
    gltfMemoryCache.set(url, pending)
  }

  const { scene } = await pending
  const clone = scene.clone(true)
  clone.traverse((node) => {
    if (node instanceof THREE.Mesh && node.material) {
      node.material = Array.isArray(node.material)
        ? node.material.map((m) => m.clone())
        : node.material.clone()
    }
  })
  return clone
}

/**
 * Clears the in-memory GLTF cache.
 */
export function clearGltfCache(): void {
  gltfMemoryCache.clear()
}
