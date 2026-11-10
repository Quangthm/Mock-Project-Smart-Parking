import { useState } from "react"
import { Modal } from "../../../components/ui/Modal"
import type { ReleasedIdentifier } from "../../../lib/parkingApi"
import { structureErrorMessage } from "./StructureFeedback"

type ResourceKind = "unit" | "slot" | "path"

const KIND_LABEL: Record<ResourceKind, string> = {
  unit: "unit name",
  slot: "slot code",
  path: "path code",
}

const KIND_CONSEQUENCE: Record<ResourceKind, string> = {
  unit: "Restoring reuses the original unit row, including its unit type and parent.",
  slot: "Restoring reuses the original slot row, including its unit, code and categories.",
  path: "Restoring reuses the original path row; both endpoints must still be live.",
}

/**
 * A create that names a released identifier is a decision, never a retry. Registration
 * never restores by itself because a delayed retry of an old create must not undo a
 * deliberate removal (F06). Units, slots and paths share this dialog.
 */
export function ReleasedIdentifierDialog({
  kind,
  released,
  onChooseAnother,
  onRestore,
  onClose,
}: {
  kind: ResourceKind
  released: ReleasedIdentifier
  onChooseAnother: () => void
  onRestore: () => Promise<unknown> | unknown
  onClose: () => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const label = KIND_LABEL[kind]
  const releasedAt = released.deletedAt
    ? new Date(released.deletedAt).toLocaleString()
    : "an earlier time"

  async function restore() {
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      await onRestore()
      setBusy(false)
      onClose()
    } catch (cause) {
      setBusy(false)
      setError(cause)
    }
  }

  return (
    <Modal title={`Released ${label}`} onClose={busy ? () => {} : onClose}>
      <div className="space-y-4 text-sm text-[var(--fg)]">
        <p className="break-words">
          <strong className="font-semibold">{released.code}</strong> belongs to a{" "}
          {kind} that was released on {releasedAt}. Nothing has been created.
        </p>
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-xs text-amber-800 dark:text-amber-200">
          <p className="font-medium">Choose explicitly: use a different {label}, or restore the released {kind}.</p>
          <p className="mt-1">{KIND_CONSEQUENCE[kind]}</p>
          <p className="mt-1">
            A create is never restored automatically, so an old retry cannot undo a deliberate removal.
          </p>
        </div>
        {error ? (
          <p role="alert" className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-300">
            {structureErrorMessage(error, `The released ${kind} could not be restored.`)}
          </p>
        ) : null}
        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" className="btn-outline" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button type="button" className="btn-outline" onClick={onChooseAnother} disabled={busy}>
            Use a different {label}
          </button>
          <button type="button" className="btn-primary" onClick={restore} disabled={busy} aria-busy={busy || undefined}>
            {busy ? "Restoring…" : `Restore released ${kind}`}
          </button>
        </div>
      </div>
    </Modal>
  )
}
