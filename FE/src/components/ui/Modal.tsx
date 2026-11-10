import { useEffect, useRef, type HTMLAttributes, type ReactNode } from "react"
import { createPortal } from "react-dom"
import { motion, useReducedMotion } from "motion/react"
import { useApp } from "../../context/AppContext"
import { UntitledIcon } from "../icon/UntitledIcon"

export function Modal({ title, children, onClose, className = "", ...props }: Omit<HTMLAttributes<HTMLDivElement>, "title"> & {
  title: ReactNode
  onClose: () => void
}) {
  const { accentColor } = useApp()
  const reduceMotion = useReducedMotion()
  const closeButton = useRef<HTMLButtonElement>(null)
  const dialog = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    closeButton.current?.focus({ preventScroll: true })
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape" && !event.defaultPrevented) onCloseRef.current()
      if (event.key !== "Tab" || !dialog.current) return
      const controls = [...dialog.current.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href]')]
      if (!controls.length) return
      if (event.shiftKey && document.activeElement === controls[0]) { event.preventDefault(); controls[controls.length - 1].focus() }
      else if (!event.shiftKey && document.activeElement === controls[controls.length - 1]) { event.preventDefault(); controls[0].focus() }
    }
    document.addEventListener("keydown", handleKeyDown)
    return () => { document.removeEventListener("keydown", handleKeyDown); document.body.style.overflow = previousOverflow; previousFocus?.focus({ preventScroll: true }) }
  }, [])
  return createPortal(
    <motion.div className="dashboard-theme fixed inset-0 z-[1000] grid place-items-center bg-black/50 p-3 sm:p-4" data-accent={accentColor}
      initial={reduceMotion ? false : { opacity: 0 }} animate={{ opacity: 1 }}
      transition={{ duration: reduceMotion ? 0 : 0.2, ease: [0.23, 1, 0.32, 1] }}
      onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <div ref={dialog} role="dialog" aria-modal="true" aria-label={typeof title === "string" ? title : undefined}
        className={`max-h-[calc(100dvh-2rem)] w-full max-w-lg overflow-y-auto overscroll-contain rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4 text-[var(--fg)] shadow-xl sm:p-5 ${className}`.trim()} {...props}>
        <div className="mb-4 flex items-start justify-between gap-3">
          <h2 className="text-lg font-bold text-[var(--fg)]">{title}</h2>
          <button ref={closeButton} type="button" onClick={onClose} aria-label="Close dialog"
            className="grid size-11 shrink-0 place-items-center rounded-full border border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] transition-colors hover:bg-[var(--raised)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--focus)] motion-reduce:transition-none">
            <UntitledIcon name="x" size={18} />
          </button>
        </div>
        {children}
      </div>
    </motion.div>,
    document.body,
  )
}
