import { useState } from "react"
import { Modal } from "../../../components/ui/Modal"

type Confirmation = {
  title: string
  description: string
  label: "Delete" | "Remove"
  confirm: () => void
}

export function useStructureConfirmation() {
  const [pending, ask] = useState<Confirmation | null>(null)
  const dialog = pending && (
    <Modal title={pending.title} onClose={() => ask(null)}>
      <p className="text-sm leading-relaxed text-[var(--muted)]">{pending.description}</p>
      <div className="mt-5 flex flex-wrap justify-end gap-3">
        <button type="button" className="btn-outline structure-action" onClick={() => ask(null)}>Cancel</button>
        <button type="button" className="btn-outline structure-action structure-action-danger" onClick={() => {
          ask(null)
          pending.confirm()
        }}>{pending.label}</button>
      </div>
    </Modal>
  )
  return { ask, dialog }
}
