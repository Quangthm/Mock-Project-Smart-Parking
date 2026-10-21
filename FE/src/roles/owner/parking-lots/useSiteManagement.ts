import { useEffect, useState, useMemo } from "react"
import { useApp } from "../../../context/AppContext"
import { parkingApi, operatorsApi, type Site, type Operator } from "../../../lib/parkingApi"

export function useSiteManagement(onSiteCreated: (site: Site, created: boolean) => void) {
  const { user } = useApp()
  const [sites, setSites] = useState<Site[]>([])
  const [operators, setOperators] = useState<Operator[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [reload, setReload] = useState(0)

  // Modals
  const [editingSite, setEditingSite] = useState<Site | undefined>()
  const [showFormModal, setShowFormModal] = useState(false)
  const [inspectSite, setInspectSite] = useState<Site | null>(null)

  // Operator Reassignment Modal (Requirement C & F)
  const [assignModal, setAssignModal] = useState<{
    targetSite: Site
    selectedOpId: string
  } | null>(null)
  const [assignBusy, setAssignBusy] = useState(false)
  const [assignError, setAssignError] = useState("")

  // Search & Filter
  const [searchTerm, setSearchTerm] = useState("")
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all")

  useEffect(() => {
    let current = true
    setLoading(true)
    setError("")
    setShowFormModal(false)

    Promise.allSettled([parkingApi.list(), operatorsApi.list()])
      .then(([siteResult, operatorResult]) => {
        if (!current) return
        if (siteResult.status === "fulfilled") setSites(siteResult.value)
        else setError(siteResult.reason instanceof Error ? siteResult.reason.message : "Cannot load sites.")
        if (operatorResult.status === "fulfilled") setOperators(operatorResult.value)
        else if (siteResult.status === "fulfilled")
          setError(operatorResult.reason instanceof Error ? operatorResult.reason.message : "Cannot load operators.")
      })
      .finally(() => {
        if (current) setLoading(false)
      })

    return () => {
      current = false
    }
  }, [user?.id, reload])

  function handleSavedSite(site: Site) {
    const created = !editingSite
    setSites((current) =>
      [...current.filter((s) => s.id !== site.id), site].sort((a, b) =>
        a.name.localeCompare(b.name),
      ),
    )
    setShowFormModal(false)
    setEditingSite(undefined)
    setMessage(`Successfully saved parking facility: ${site.name}`)
    setReload((value) => value + 1)
    onSiteCreated(site, created)
  }

  function handleOpenAssignModal(site: Site) {
    // Find first operator not already assigned to this site, or default to first operator
    const unassigned = operators.find((op) => !op.siteIds.includes(site.id))
    setAssignModal({
      targetSite: site,
      selectedOpId: unassigned?.id ?? operators[0]?.id ?? "",
    })
    setAssignError("")
  }

  async function handleConfirmAssign() {
    if (!assignModal || !assignModal.selectedOpId) return
    const op = operators.find((o) => o.id === assignModal.selectedOpId)
    if (!op) return

    setAssignBusy(true)
    setAssignError("")

    // Add target site to operator's site list if not present
    const updatedSiteIds = Array.from(
      new Set([...op.siteIds, assignModal.targetSite.id]),
    )

    try {
      const updated = await operatorsApi.reassignSite(op.id, updatedSiteIds)
      setOperators((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      setMessage(
        `Assigned operator ${op.fullName} to site ${assignModal.targetSite.name}.`,
      )
      setAssignModal(null)
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : "Site assignment update failed. Backend endpoint may not be available yet."
      setAssignError(
        `${msg} (Previous operator assignments were kept unchanged).`,
      )
    } finally {
      setAssignBusy(false)
    }
  }

  function handleNavigateToStructure(siteId: string) {
    window.dispatchEvent(
      new CustomEvent("sp:dashboard-nav", { detail: "structure" }),
    )
    window.dispatchEvent(
      new CustomEvent("sp:structure-select-site", { detail: siteId }),
    )
  }

  const filteredSites = useMemo(() => {
    return sites.filter((site) => {
      const matchStatus =
        statusFilter === "all" ||
        (statusFilter === "active" ? site.isActive : !site.isActive)
      const matchSearch =
        !searchTerm.trim() ||
        site.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        site.address.toLowerCase().includes(searchTerm.toLowerCase())
      return matchStatus && matchSearch
    })
  }, [sites, statusFilter, searchTerm])

  return {
    sites, operators, loading, error, message, reload, setReload,
    editingSite, setEditingSite, showFormModal, setShowFormModal,
    inspectSite, setInspectSite, assignModal, setAssignModal,
    assignBusy, assignError, searchTerm, setSearchTerm, statusFilter, setStatusFilter,
    filteredSites, handleSavedSite, handleOpenAssignModal, handleConfirmAssign,
    handleNavigateToStructure,
  }
}

export type SiteManagementModel = ReturnType<typeof useSiteManagement>
