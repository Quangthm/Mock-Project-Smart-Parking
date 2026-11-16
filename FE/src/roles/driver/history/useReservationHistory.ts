import { useEffect, useRef, useState } from "react"
import { getSessionGeneration, request } from "../../../lib/authApi"
import { reservationsApi, type OwnReservation, type ReservationHistoryCursor } from "../../../lib/reservationsApi"
import type { OperationalLayoutProjection } from "../../../lib/operationalLayout"

const facilityServer = (import.meta.env.VITE_PARKING_API_BASE_URL ?? "http://localhost:5045").replace(/\/$/, "")
export const HISTORY_PAGE_SIZE = 8
export interface HistorySite { name: string; slots: Record<string, string> }

export function useReservationHistory(scope: string) {
  const [navigation, setNavigation] = useState<{ scope: string; cursors: (ReservationHistoryCursor | undefined)[]; index: number }>({ scope, cursors: [undefined], index: 0 })
  const [revision, setRevision] = useState(0)
  const [page, setPage] = useState<{ scope: string; items: OwnReservation[]; hasNext: boolean; loading: boolean; error: string }>({ scope: "", items: [], hasNext: false, loading: true, error: "" })
  const [sites, setSites] = useState<{ scope: string; data: Record<string, HistorySite | null> }>({ scope: "", data: {} })
  const [selected, setSelected] = useState<{ scope: string; id: string } | null>(null)
  const [detailRevision, setDetailRevision] = useState(0)
  const [detail, setDetail] = useState<{ scope: string; id: string; data: OwnReservation | null; loading: boolean; error: string }>({ scope: "", id: "", data: null, loading: false, error: "" })
  const cache = useRef({ scope, sites: new Map<string, Promise<HistorySite | null>>(), pages: new Map<string, Promise<OwnReservation[]>>() })
  if (cache.current.scope !== scope) cache.current = { scope, sites: new Map(), pages: new Map() }
  const pageIndex = navigation.scope === scope ? navigation.index : 0
  const cursor = navigation.scope === scope ? navigation.cursors[pageIndex] : undefined
  const selectedId = selected?.scope === scope ? selected.id : null

  useEffect(() => {
    if (!scope) return
    let active = true
    const generation = getSessionGeneration()
    const current = () => active && generation === getSessionGeneration()
    const resources = cache.current
    setPage({ scope, items: [], hasNext: false, loading: true, error: "" })
    const key = JSON.stringify(cursor ?? null)
    let pending = resources.pages.get(key)
    if (!pending) {
      pending = reservationsApi.historyPage(HISTORY_PAGE_SIZE + 1, cursor)
      resources.pages.set(key, pending)
      const read = pending
      void read.finally(() => { if (resources.pages.get(key) === read) resources.pages.delete(key) }).catch(() => {})
    }
    pending.then(async items => {
      if (!current()) return
      const visible = items.slice(0, HISTORY_PAGE_SIZE)
      setPage({ scope, items: visible, hasNext: items.length > HISTORY_PAGE_SIZE, loading: false, error: "" })
      const ids = [...new Set(visible.map(item => item.siteId))]
      // Display enrichment only; resolve visible sites once, never all parking lots.
      const result: Record<string, HistorySite | null> = {}
      for (const id of ids) {
        if (!current()) return
        let site = resources.sites.get(id)
        if (!site) {
          site = request<{ success: boolean; data: OperationalLayoutProjection }>(
            `/${encodeURIComponent(id)}/operational-layout`, "GET", undefined, "/api/parking-lots", facilityServer,
          ).then(response => {
            const data = response.data
            if (!response.success || data?.siteId !== id || !Array.isArray(data.slots)) return null
            return { name: data.siteName, slots: Object.fromEntries(data.slots.map(slot => [slot.slotId, slot.slotCode])) }
          }).catch(() => null)
          resources.sites.set(id, site)
        }
        result[id] = await site
        if (current()) setSites(previous => ({ scope, data: { ...(previous.scope === scope ? previous.data : {}), ...result } }))
      }
    }).catch(error => {
      if (current()) setPage({ scope, items: [], hasNext: false, loading: false, error: error instanceof Error ? error.message : "Could not load reservation history. Try again." })
    })
    return () => { active = false }
  }, [scope, cursor?.before, cursor?.beforeId, revision])

  useEffect(() => {
    if (!scope || !selectedId) return
    let active = true
    const generation = getSessionGeneration()
    setDetail({ scope, id: selectedId, data: null, loading: true, error: "" })
    reservationsApi.booking(selectedId).then(data => {
      if (data.reservationId !== selectedId) throw new Error("The server returned a different reservation. Try again.")
      if (active && generation === getSessionGeneration()) setDetail({ scope, id: selectedId, data, loading: false, error: "" })
    }).catch(error => {
      if (active && generation === getSessionGeneration()) setDetail({ scope, id: selectedId, data: null, loading: false, error: error instanceof Error ? error.message : "Could not load reservation details." })
    })
    return () => { active = false }
  }, [scope, selectedId, detailRevision])

  const currentPage = page.scope === scope ? page : { items: [], hasNext: false, loading: !!scope, error: "" }
  return {
    ...currentPage,
    pageNumber: pageIndex + 1,
    sites: sites.scope === scope ? sites.data : {},
    selectedId,
    detail: detail.scope === scope && detail.id === selectedId ? detail : { data: null, loading: true, error: "" },
    openDetails: (id: string) => {
      setDetail({ scope, id, data: null, loading: true, error: "" })
      setSelected({ scope, id })
    },
    closeDetails: () => setSelected(null),
    retryDetails: () => setDetailRevision(value => value + 1),
    refresh: () => {
      cache.current.sites.clear()
      setSites({ scope, data: {} })
      setSelected(null)
      setNavigation({ scope, cursors: [undefined], index: 0 })
      setRevision(value => value + 1)
    },
    previous: () => { setSelected(null); setNavigation(value => value.scope === scope ? { ...value, index: Math.max(0, value.index - 1) } : { scope, cursors: [undefined], index: 0 }) },
    next: () => {
      const last = currentPage.items.at(-1)
      if (currentPage.loading || !currentPage.hasNext || !last) return
      const nextCursor = { before: last.createdAt, beforeId: last.reservationId }
      setSelected(null)
      setNavigation(value => ({ scope, cursors: [...(value.scope === scope ? value.cursors.slice(0, pageIndex + 1) : [undefined]), nextCursor], index: pageIndex + 1 }))
    },
  }
}
