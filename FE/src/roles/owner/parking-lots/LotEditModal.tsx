import { createPortal } from "react-dom"
import { SiteForm } from "./SiteForm"
import type { LotManagementModel } from "./useLotManagement"

export function LotEditModal({ model }: { model: LotManagementModel }) {
  const { editSite, setEditSite, saveSite } = model
  return (
    <>
{editSite &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            role="dialog"
            aria-modal="true"
            className="fixed inset-0 z-[1000] flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs"
          >
            <div className="relative w-full max-w-2xl rounded-2xl border border-[var(--border)] bg-[var(--card)] p-6 shadow-2xl">
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
