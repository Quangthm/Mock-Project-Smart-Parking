import { useEffect, useRef, type HTMLAttributes, type ReactNode } from "react"

export function Modal({ title, children, onClose, className = "", ...props }: Omit<HTMLAttributes<HTMLDivElement>, "title"> & {
  title: ReactNode
  onClose: () => void
}) {
  const closeButton = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    closeButton.current?.focus()
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose()
    }
    document.addEventListener("keydown", handleKeyDown)
    return () => document.removeEventListener("keydown", handleKeyDown)
  }, [onClose])
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <div role="dialog" aria-modal="true" aria-label={typeof title === "string" ? title : undefined} className={`card max-h-[90vh] w-full max-w-lg overflow-y-auto overscroll-contain p-5 ${className}`.trim()} {...props}>
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 className="text-lg font-bold text-[var(--fg)]">{title}</h2>
          <button ref={closeButton} type="button" onClick={onClose} aria-label="Close dialog" className="grid size-11 place-items-center rounded text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">×</button>
        </div>
        {children}
      </div>
    </div>
  )
}
