import { useEffect, useRef, type HTMLAttributes, type ReactNode } from "react"

export function Modal({ title, children, onClose, className = "", ...props }: Omit<HTMLAttributes<HTMLDivElement>, "title"> & {
  title: ReactNode
  onClose: () => void
}) {
  const closeButton = useRef<HTMLButtonElement>(null)
  const dialog = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    closeButton.current?.focus()
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onCloseRef.current()
      if (event.key !== "Tab" || !dialog.current) return
      const controls = [...dialog.current.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href]')]
      if (!controls.length) return
      if (event.shiftKey && document.activeElement === controls[0]) { event.preventDefault(); controls[controls.length - 1].focus() }
      else if (!event.shiftKey && document.activeElement === controls[controls.length - 1]) { event.preventDefault(); controls[0].focus() }
    }
    document.addEventListener("keydown", handleKeyDown)
    return () => { document.removeEventListener("keydown", handleKeyDown); document.body.style.overflow = previousOverflow; previousFocus?.focus() }
  }, [])
  return (
    <div className="fixed inset-0 z-[300] flex items-center justify-center bg-black/50 p-2 sm:p-4" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <div ref={dialog} role="dialog" aria-modal="true" aria-label={typeof title === "string" ? title : undefined} className={`card max-h-[calc(100dvh-1rem)] w-full max-w-lg overflow-y-auto overscroll-contain p-4 sm:p-5 ${className}`.trim()} {...props}>
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 className="text-lg font-bold text-[var(--fg)]">{title}</h2>
          <button ref={closeButton} type="button" onClick={onClose} aria-label="Close dialog" className="grid size-11 shrink-0 place-items-center rounded-full text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]">×</button>
        </div>
        {children}
      </div>
    </div>
  )
}
