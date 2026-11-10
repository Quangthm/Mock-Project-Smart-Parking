import { createPortal } from "react-dom"
import { SiteForm } from "./SiteForm"
import type { LotManagementModel } from "./useLotManagement"
import { useDialogKeyboard } from "../../../components/ui/useDialogKeyboard"

export function LotEditModal({ model }: { model: LotManagementModel }) {
  const { editSite, setEditSite, saveSite } = model
  const dialogRef = useDialogKeyboard(editSite?.id ?? null, () => setEditSite(null))
  return (
    <>
{editSite &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
          >
            <div ref={dialogRef} role="dialog" aria-modal="true" aria-label={`Edit ${editSite.name}`} tabIndex={-1} className="relative flex max-h-[calc(100dvh-2rem)] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--card)] shadow-2xl">
              <SiteForm
                site={editSite}
                onSaved={saveSite}
                onCancel={() => setEditSite(null)}
              />
            </div>
          </div>,
          document.body,
        )}
    </>
  )
}
