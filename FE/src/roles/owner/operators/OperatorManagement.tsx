import { useEffect, useState } from "react"
import { createPortal } from "react-dom"
import { useApp } from "../../../context/AppContext"

import { ownerData as store } from "../data/data"

import { CreateOperatorForm } from "./CreateOperatorForm"

import type { OperatorAccessRole, User } from "../../../lib/types"

import type { ParkingLot } from "../../../lib/types"

import { UntitledIcon } from "../../../components/icon/UntitledIcon"

// Operator management

export function OperatorManagement({
  sites,
  selectedSiteId,
  onOperatorsChanged,
}: {
  sites: ParkingLot[]
  selectedSiteId: string
  onOperatorsChanged: () => void
}) {
  const { user } = useApp()

  const [operators, setOperators] = useState<User[]>(() =>
    getVisibleOperators(),
  )

  const [showCreate, setShowCreate] = useState(false)

  // Modal confirmation state
  const [confirmTransferModal, setConfirmTransferModal] = useState<{
    op: User
    targetSiteId: string
    targetSiteName: string
    currentSiteName: string
  } | null>(null)

  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null)

  useEffect(
    () => setOperators(getVisibleOperators()),
    [selectedSiteId, user?.id, sites],
  )

  function getVisibleOperators() {
    return store.getUsers().filter((operator) => {
      if (operator.role !== "operator" || operator.ownerId !== user?.id)
        return false

      return (
        selectedSiteId === "all" ||
        operator.operatorSiteId === selectedSiteId ||
        operator.operatorSiteId === "all"
      )
    })
  }

  function getSiteDisplayName(siteId?: string) {
    if (!siteId || siteId === "") return "Unassigned"
    if (siteId === "all") return "All Sites"
    const found = sites.find((s) => s.id === siteId)
    return found ? found.name : siteId
  }

  function refreshOperators() {
    setOperators(getVisibleOperators())

    setShowCreate(false)

    onOperatorsChanged()
  }

  function toggleLock(op: User) {
    const updated = {
      ...op,
      lockedUntil: op.lockedUntil
        ? undefined
        : new Date(Date.now() + 99999 * 60000).toISOString(),
    }

    store.saveUser(updated)

    setOperators(getVisibleOperators())

    onOperatorsChanged()
  }

  function updateOperatorRole(op: User, operatorRole: OperatorAccessRole) {
    store.saveUser({
      ...op,
      operatorRole,
      operatorSiteId:
        op.operatorSiteId === "all" && operatorRole !== "financial"
          ? sites[0]?.id
          : op.operatorSiteId,
    })

    setOperators(getVisibleOperators())

    onOperatorsChanged()
  }

  function handleRequestSiteChange(op: User, nextSiteId: string) {
    const currentSiteId = op.operatorSiteId ?? ""
    if (nextSiteId === currentSiteId) return

    setConfirmTransferModal({
      op,
      targetSiteId: nextSiteId,
      targetSiteName: getSiteDisplayName(nextSiteId),
      currentSiteName: getSiteDisplayName(currentSiteId),
    })
  }

  function handleExecuteTransfer() {
    if (!confirmTransferModal) return
    const { op, targetSiteId, targetSiteName, currentSiteName } =
      confirmTransferModal

    store.saveUser({ ...op, operatorSiteId: targetSiteId })

    if (user) {
      store.addAuditLog({
        userId: user.id,
        userName: user.name,
        userRole: "owner",
        action: "OPERATOR_SITE_TRANSFERRED",
        details: `Transferred operator ${op.name} (${op.email}) from "${currentSiteName}" to "${targetSiteName}"`,
      })
    }

    store.notifyUser(
      op.id,
      "SITE_REASSIGNED",
      "Facility Reassigned",
      `You have been reassigned to: ${targetSiteName}.`,
    )

    setConfirmTransferModal(null)
    setOperators(getVisibleOperators())
    onOperatorsChanged()

    setFeedbackMessage(
      `Successfully transferred operator ${op.name} to "${targetSiteName}".`,
    )
    setTimeout(() => setFeedbackMessage(null), 4000)
  }

  function deleteOperator(op: User) {
    store.deleteUser(op.id)

    setOperators(getVisibleOperators())

    onOperatorsChanged()
  }

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1.25rem",
        }}
      >
        <h3
          style={{
            fontFamily: "Outfit",
            fontWeight: 700,
            fontSize: "1.05rem",
            color: "var(--fg)",
            margin: 0,
          }}
        >
          Operator Accounts
        </h3>
        <button
          type="button"
          className="btn-primary"
          style={{ fontSize: "0.85rem" }}
          onClick={() => setShowCreate(true)}
        >
          <UntitledIcon name="plus" size={16} /> New Operator
        </button>
      </div>
      {showCreate && (
        <CreateOperatorForm
          sites={sites}
          onCreated={refreshOperators}
          onCancel={() => setShowCreate(false)}
        />
      )}
      {feedbackMessage && (
        <div
          role="status"
          className="animate-in"
          style={{
            marginBottom: "1rem",
            padding: "0.75rem 1rem",
            borderRadius: "0.5rem",
            background: "rgba(22, 163, 74, 0.12)",
            border: "1px solid rgba(22, 163, 74, 0.35)",
            color: "#16a34a",
            fontSize: "0.85rem",
            fontWeight: 500,
            display: "flex",
            alignItems: "center",
            gap: "0.5rem",
          }}
        >
          <UntitledIcon name="check" size={16} />
          <span>{feedbackMessage}</span>
        </div>
      )}
      <div
        style={{ display: "flex", flexDirection: "column", gap: "0.625rem" }}
      >
        {operators.map((op) => {
          const currentSiteId = op.operatorSiteId ?? ""

          return (
            <div
              key={op.id}
              className="card"
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                gap: "1rem",
                padding: "0.875rem 1rem",
              }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.75rem",
                }}
              >
                <div
                  style={{
                    width: 32,
                    height: 32,
                    background: "#a855f720",
                    borderRadius: "50%",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontWeight: 700,
                    color: "#a855f7",
                    fontSize: "0.9rem",
                  }}
                >
                  {op.name.charAt(0)}
                </div>
                <div>
                  <div
                    style={{
                      fontWeight: 600,
                      fontSize: "0.875rem",
                      color: "var(--fg)",
                    }}
                  >
                    {op.name}
                  </div>
                  <div style={{ fontSize: "0.77rem", color: "var(--muted)" }}>
                    {op.email} ·{" "}
                    {(op.operatorRole ?? "operation").replace(/^./, (role) =>
                      role.toUpperCase(),
                    )}
                  </div>
                </div>
              </div>
              <div
                style={{
                  display: "flex",
                  gap: "0.55rem",
                  alignItems: "center",
                  flexWrap: "wrap",
                }}
              >
                {/* Site selector */}
                <label className="sr-only" htmlFor={`operator-site-${op.id}`}>
                  Site for {op.name}
                </label>
                <select
                  id={`operator-site-${op.id}`}
                  className="input"
                  value={currentSiteId || ""}
                  onChange={(event) =>
                    handleRequestSiteChange(op, event.target.value)
                  }
                  style={{
                    width: "auto",
                    minWidth: "150px",
                    maxWidth: "200px",
                    padding: "0.38rem 0.6rem",
                    fontSize: "0.78rem",
                  }}
                >
                  <option value="all">All Sites</option>
                  {!currentSiteId && <option value="">Unassigned</option>}
                  {sites.map((site) => (
                    <option key={site.id} value={site.id}>
                      {site.name}
                    </option>
                  ))}
                </select>

                <span
                  style={{
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    padding: "0.25rem 0.65rem",
                    borderRadius: "999px",
                    background: "rgba(37, 99, 235, 0.12)",
                    color: "var(--primary)",
                    border: "1px solid rgba(37, 99, 235, 0.25)",
                    whiteSpace: "nowrap",
                  }}
                >
                  Operation
                </span>
                {op.lockedUntil && (
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 4,
                      fontSize: "0.72rem",
                      color: "#ef4444",
                      fontWeight: 600,
                    }}
                  >
                    <UntitledIcon name="lock" size={13} /> LOCKED
                  </span>
                )}
                <button
                  style={{
                    fontSize: "0.78rem",
                    padding: "0.3rem 0.625rem",
                    background: "var(--card)",
                    border: "1px solid var(--border)",
                    borderRadius: "var(--radius)",
                    cursor: "pointer",
                    color: "var(--muted)",
                  }}
                  onClick={() => toggleLock(op)}
                >
                  {op.lockedUntil ? "Unlock" : "Lock"}
                </button>
                <button
                  style={{
                    fontSize: "0.78rem",
                    padding: "0.3rem 0.625rem",
                    background: "#fee2e2",
                    border: "1px solid #fca5a5",
                    borderRadius: "var(--radius)",
                    cursor: "pointer",
                    color: "#dc2626",
                  }}
                  onClick={() => {
                    if (confirm(`Remove operator ${op.name}?`))
                      deleteOperator(op)
                  }}
                >
                  Remove
                </button>
              </div>
            </div>
          )
        })}
        {!operators.length && (
          <div
            style={{
              textAlign: "center",
              padding: "2rem",
              color: "var(--muted)",
              fontSize: "0.875rem",
            }}
          >
            No operators yet. Create an account for your staff.
          </div>
        )}
      </div>

      {/* Confirmation Modal for Site Transfer */}
      {confirmTransferModal &&
        createPortal(
          <div
            style={{
              position: "fixed",
              inset: 0,
              zIndex: 99999,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              background: "rgba(0, 0, 0, 0.75)",
              backdropFilter: "blur(6px)",
              WebkitBackdropFilter: "blur(6px)",
              padding: "1rem",
            }}
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-transfer-title"
            onMouseDown={(e) => {
              if (e.target === e.currentTarget) setConfirmTransferModal(null)
            }}
          >
            <div
              className="card animate-in"
              style={{
                width: "100%",
                maxWidth: "460px",
                display: "flex",
                flexDirection: "column",
                gap: "1.25rem",
                border: "1.5px solid var(--border)",
                boxShadow: "0 20px 48px rgba(0, 0, 0, 0.6)",
              }}
            >
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  borderBottom: "1px solid var(--border)",
                  paddingBottom: "0.75rem",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "0.6rem",
                  }}
                >
                  <div
                    style={{
                      width: 36,
                      height: 36,
                      borderRadius: "50%",
                      background: "rgba(37, 99, 235, 0.15)",
                      color: "#3b82f6",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    <UntitledIcon name="building" size={20} />
                  </div>
                  <div>
                    <h4
                      id="confirm-transfer-title"
                      style={{
                        fontFamily: "Outfit",
                        fontWeight: 700,
                        fontSize: "1.05rem",
                        margin: 0,
                        color: "var(--fg)",
                      }}
                    >
                      Confirm Facility Transfer
                    </h4>
                    <p
                      style={{
                        margin: "0.1rem 0 0",
                        fontSize: "0.76rem",
                        color: "var(--muted)",
                      }}
                    >
                      Confirm site re-assignment for operator
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setConfirmTransferModal(null)}
                  style={{
                    background: "none",
                    border: "none",
                    color: "var(--muted)",
                    cursor: "pointer",
                    padding: "4px",
                  }}
                >
                  <UntitledIcon name="x" size={18} />
                </button>
              </div>

              <div
                style={{
                  background: "var(--bg)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem 1rem",
                  border: "1px solid var(--border)",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.55rem",
                  fontSize: "0.84rem",
                }}
              >
                <div
                  style={{ display: "flex", justifyContent: "space-between" }}
                >
                  <span style={{ color: "var(--muted)" }}>Operator:</span>
                  <strong style={{ color: "var(--fg)" }}>
                    {confirmTransferModal.op.name}
                  </strong>
                </div>
                <div
                  style={{ display: "flex", justifyContent: "space-between" }}
                >
                  <span style={{ color: "var(--muted)" }}>Email:</span>
                  <span style={{ color: "var(--fg)" }}>
                    {confirmTransferModal.op.email}
                  </span>
                </div>
                <div
                  style={{
                    height: "1px",
                    background: "var(--border)",
                    margin: "0.2rem 0",
                  }}
                />
                <div
                  style={{ display: "flex", justifyContent: "space-between" }}
                >
                  <span style={{ color: "var(--muted)" }}>
                    Current Facility:
                  </span>
                  <span style={{ color: "#ef4444", fontWeight: 500 }}>
                    {confirmTransferModal.currentSiteName}
                  </span>
                </div>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                  }}
                >
                  <span style={{ color: "var(--muted)" }}>New Facility:</span>
                  <span style={{ color: "#16a34a", fontWeight: 700 }}>
                    ➔ {confirmTransferModal.targetSiteName}
                  </span>
                </div>
              </div>

              <p
                style={{
                  margin: 0,
                  fontSize: "0.78rem",
                  color: "var(--muted)",
                  lineHeight: 1.5,
                }}
              >
                Operator access permissions will update to the new facility
                immediately. This transfer will be recorded in the system Audit
                Log.
              </p>

              <div
                style={{
                  display: "flex",
                  justifyContent: "flex-end",
                  gap: "0.625rem",
                  paddingTop: "0.5rem",
                  borderTop: "1px solid var(--border)",
                }}
              >
                <button
                  type="button"
                  className="btn-outline"
                  onClick={() => setConfirmTransferModal(null)}
                  style={{ fontSize: "0.85rem" }}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={handleExecuteTransfer}
                  style={{
                    fontSize: "0.85rem",
                    background:
                      "linear-gradient(180deg, #16a34a 0%, #15803d 100%)",
                    borderColor: "#15803d",
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.35rem",
                  }}
                >
                  <UntitledIcon name="check" size={15} /> Confirm Transfer
                </button>
              </div>
            </div>
          </div>,
          document.body,
        )}
    </div>
  )
}
