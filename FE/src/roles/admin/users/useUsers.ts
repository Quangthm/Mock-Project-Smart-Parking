import { useEffect, useState } from "react"
import { useApp } from "../../../context/AppContext"
import { usersApi, type UserDetail, type UsersPage } from "../../../lib/usersApi"

export function useUsers() {
  const { user: admin } = useApp()
  const [search, setSearch] = useState("")
  const [query, setQuery] = useState({ search: "", role: "", status: "", page: 1 })
  const [page, setPage] = useState<UsersPage | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<UserDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [detailError, setDetailError] = useState("")
  const [notice, setNotice] = useState("")
  const [reload, setReload] = useState(0)
  const [detailReload, setDetailReload] = useState(0)

  useEffect(() => {
    if (admin?.role !== "admin") return
    let current = true
    setLoading(true)
    setError("")
    usersApi.list(query)
      .then((result) => { if (current) setPage(result) })
      .catch((err) => { if (current) setError(err instanceof Error ? err.message : "Cannot load users.") })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [query, reload, admin?.id, admin?.role])

  useEffect(() => {
    setDetail(null)
    setDetailError("")
    setNotice("")
    setDetailLoading(false)
    if (!selectedId || admin?.role !== "admin") return
    let current = true
    setDetailLoading(true)
    usersApi.get(selectedId)
      .then((result) => { if (current) setDetail(result) })
      .catch((err) => { if (current) setDetailError(err instanceof Error ? err.message : "Cannot load user details.") })
      .finally(() => { if (current) setDetailLoading(false) })
    return () => { current = false }
  }, [selectedId, detailReload, admin?.id, admin?.role])

  async function changeStatus() {
    if (!detail || saving) return
    const status = detail.user.status === "locked" ? "active" : "locked"
    setSaving(true)
    setDetailError("")
    setNotice("")
    try {
      const updated = await usersApi.setStatus(detail.user.id, status)
      setDetail((current) => current?.user.id === updated.id ? {
        user: updated,
        loginActivity: current.loginActivity.map((activity) => status === "locked" ? { ...activity, isRevoked: true } : activity),
      } : current)
      setNotice(status === "locked"
        ? "Account locked. Existing sessions have been revoked."
        : "Account unlocked. The user can sign in again.")
      setReload((value) => value + 1)
    } catch (err) {
      setDetailError(err instanceof Error ? err.message : "Cannot update account status.")
    } finally {
      setSaving(false)
    }
  }

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (saving) return
    setSelectedId(null)
    setQuery((current) => ({ ...current, search: search.trim(), page: 1 }))
  }

  function handleResetFilters() {
    setSearch("")
    setSelectedId(null)
    setQuery({ search: "", role: "", status: "", page: 1 })
  }

  function setFilter(key: "role" | "status", value: string) {
    setSelectedId(null)
    setQuery((current) => ({ ...current, [key]: value, page: 1 }))
  }

  function setPageNumber(value: number) {
    setSelectedId(null)
    setQuery((current) => ({ ...current, page: value }))
  }

  const managed = detail?.user
  const canChange = !!managed && managed.id !== admin?.id && !managed.roles.includes("admin") && ["active", "locked"].includes(managed.status)
  const totalItems = page?.total ?? 0
  const pageSize = page?.pageSize ?? 10
  const currentPage = page?.page ?? query.page
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize))
  const startIdx = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1
  const endIdx = Math.min(totalItems, currentPage * pageSize)

  return {
    admin, search, setSearch, query, page, selectedId, setSelectedId,
    detail, loading, detailLoading, saving, error, detailError, notice,
    retryList: () => setReload((value) => value + 1),
    retryDetail: () => setDetailReload((value) => value + 1),
    changeStatus, handleSearchSubmit, handleResetFilters, setFilter, setPageNumber,
    managed, canChange, totalItems, currentPage, totalPages, startIdx, endIdx,
  }
}

export type UsersViewModel = ReturnType<typeof useUsers>
