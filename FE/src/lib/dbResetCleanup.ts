const lastResetKey = "sp_last_db_reset_id"
const lotsKey = "sp_lots"
const structuresKey = "sp_parking_structures"
const sampleLotIds = new Set(["lot-001", "lot-002", "lot-003"])

export async function cleanLotsAfterDbReset() {
  try {
    const response = await fetch("/__smartpark/db-reset-id", {
      cache: "no-store",
      signal: AbortSignal.timeout(2000),
    })
    if (!response.ok) return

    const resetId = (await response.text()).trim()
    if (!/^[0-9a-f]{32}$/.test(resetId) || localStorage.getItem(lastResetKey) === resetId) return

    const lots = JSON.parse(localStorage.getItem(lotsKey) ?? "[]")
    const structures = JSON.parse(localStorage.getItem(structuresKey) ?? "{}")
    if (!Array.isArray(lots) || lots.some((lot) => typeof lot?.id !== "string") ||
        !structures || typeof structures !== "object" || Array.isArray(structures)) return

    const sampleLots = lots.filter((lot) => sampleLotIds.has(lot.id))
    if (sampleLots.length !== lots.length) {
      for (const lot of lots) {
        if (!sampleLotIds.has(lot.id)) delete structures[lot.id]
      }
      // Keep the lot IDs available for a retry if saving fails partway.
      localStorage.setItem(structuresKey, JSON.stringify(structures))
      localStorage.setItem(lotsKey, JSON.stringify(sampleLots))
    }
    localStorage.setItem(lastResetKey, resetId)
  } catch {
    // Startup can continue and the same reset can be retried on reload.
  }
}
