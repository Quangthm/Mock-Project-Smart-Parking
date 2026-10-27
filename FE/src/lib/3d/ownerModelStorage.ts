const databaseName = "smartparking-owner-models"
const storeName = "site-models"
const objectUrls = new Map<string, string>()

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof indexedDB === "undefined") { reject(new Error("Local model storage is unavailable.")); return }
    const request = indexedDB.open(databaseName, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(storeName)
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error("Local model storage could not open."))
  })
}

export async function saveOwnerModel(siteId: string, file: File): Promise<string> {
  const db = await openDatabase()
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = db.transaction(storeName, "readwrite")
      transaction.objectStore(storeName).put(file, siteId)
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error ?? new Error("Could not save the 3D model."))
    })
  } finally { db.close() }
  const previous = objectUrls.get(siteId)
  if (previous) URL.revokeObjectURL(previous)
  const url = URL.createObjectURL(file)
  objectUrls.set(siteId, url)
  return url
}

export async function loadOwnerModelUrl(siteId: string): Promise<string | null> {
  if (!siteId) return null
  const cached = objectUrls.get(siteId)
  if (cached) return cached
  const db = await openDatabase()
  let file: Blob | undefined
  try {
    file = await new Promise<Blob | undefined>((resolve, reject) => {
      const request = db.transaction(storeName, "readonly").objectStore(storeName).get(siteId)
      request.onsuccess = () => resolve(request.result as Blob | undefined)
      request.onerror = () => reject(request.error ?? new Error("Could not load the 3D model."))
    })
  } finally { db.close() }
  if (!file) return null
  const url = URL.createObjectURL(file)
  objectUrls.set(siteId, url)
  return url
}

export async function deleteOwnerModel(siteId: string): Promise<void> {
  const db = await openDatabase()
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = db.transaction(storeName, "readwrite")
      transaction.objectStore(storeName).delete(siteId)
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error ?? new Error("Could not remove the previous 3D model."))
    })
  } finally { db.close() }
  const url = objectUrls.get(siteId)
  if (url) URL.revokeObjectURL(url)
  objectUrls.delete(siteId)
}
