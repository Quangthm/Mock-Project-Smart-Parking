import { useEffect, useId, useRef, type HTMLAttributes, type ReactNode } from "react"
import { createPortal } from "react-dom"
import { motion, useReducedMotion } from "motion/react"
import { useApp } from "../../context/AppContext"
import { UntitledIcon } from "../icon/UntitledIcon"

// One stack coordinates nested dialogs; only the top surface owns Escape/Tab.
const modalStack: HTMLElement[] = []
let bodyOverflow = ""

export function Modal({ title, children, onClose, className = "", portalContainer = document.body, ...props }: Omit<HTMLAttributes<HTMLDivElement>, "title"> & {
  title: ReactNode
  onClose: () => void
  portalContainer?: Element
}) {
  const { accentColor } = useApp()
  const reduceMotion = useReducedMotion()
  const titleId = useId()
  const closeButton = useRef<HTMLButtonElement>(null)
  const overlay = useRef<HTMLDivElement>(null)
  const dialog = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose
  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const element = dialog.current!
    if (!modalStack.length) {
      bodyOverflow = document.body.style.overflow
      document.body.style.overflow = "hidden"
    }
    modalStack.push(element)
    closeButton.current?.focus({ preventScroll: true })
    function handleKeyDown(event: KeyboardEvent) {
      if (modalStack.at(-1) !== element || event.defaultPrevented) return
      if (event.key === "Escape") {
        event.preventDefault()
        event.stopImmediatePropagation()
        onCloseRef.current()
        return
      }
      if (event.key !== "Tab") return
      // Portalled selects manage their own Tab before returning to their trigger.
      if (event.target instanceof Element && event.target.closest('[role="listbox"]')) return
      const controls = [...element.querySelectorAll<HTMLElement>('button, input, select, textarea, a[href], [tabindex]')]
        .filter(control => control.tabIndex >= 0 && !control.matches(':disabled') && !control.closest('[hidden], [inert], [aria-hidden="true"]') && control.getClientRects().length > 0)
      if (!controls.length) { event.preventDefault(); element.focus(); return }
      const first = controls[0]
      const last = controls[controls.length - 1]
      if (!element.contains(document.activeElement) || (event.shiftKey && document.activeElement === first)) {
        event.preventDefault(); (event.shiftKey ? last : first).focus()
      } else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener("keydown", handleKeyDown)
    return () => {
      document.removeEventListener("keydown", handleKeyDown)
      const wasTop = modalStack.at(-1) === element
      const index = modalStack.indexOf(element)
      if (index >= 0) modalStack.splice(index, 1)
      if (!modalStack.length) document.body.style.overflow = bodyOverflow
      if (wasTop && previousFocus?.isConnected) previousFocus.focus({ preventScroll: true })
    }
  }, [])
  useEffect(() => {
    const viewport = window.visualViewport
    if (!viewport) return
    const update = () => {
      if (!overlay.current) return
      overlay.current.style.top = `${viewport.offsetTop}px`
      overlay.current.style.height = `${viewport.height}px`
    }
    update()
    viewport.addEventListener("resize", update)
    viewport.addEventListener("scroll", update)
    return () => { viewport.removeEventListener("resize", update); viewport.removeEventListener("scroll", update) }
  }, [])
  return createPortal(
    <motion.div ref={overlay} className="dashboard-theme sp-modal-overlay fixed inset-0 z-[1000] grid place-items-center bg-black/50 p-3 sm:p-4" data-accent={accentColor}
      initial={reduceMotion ? false : { opacity: 0 }} animate={{ opacity: 1 }}
      transition={{ duration: reduceMotion ? 0 : 0.2, ease: [0.23, 1, 0.32, 1] }}
      onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <div ref={dialog} role="dialog" tabIndex={-1} aria-modal="true" aria-labelledby={titleId}
        className={`sp-modal-panel flex max-h-[calc(100dvh-2rem)] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--card)] text-[var(--fg)] shadow-xl ${className}`.trim()} {...props}>
        <div className="flex shrink-0 items-start justify-between gap-3 px-4 pt-4 pb-3 sm:px-5 sm:pt-5">
          <h2 id={titleId} className="min-w-0 break-words text-lg font-bold text-[var(--fg)]">{title}</h2>
          <button ref={closeButton} type="button" onClick={onClose} aria-label="Close dialog"
            className="grid size-11 shrink-0 place-items-center rounded-full border border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] transition-colors hover:bg-[var(--raised)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--focus)] motion-reduce:transition-none">
            <UntitledIcon name="x" size={18} />
          </button>
        </div>
        <div className="sp-modal-body min-h-0 overflow-y-auto overscroll-contain px-4 pb-4 sm:px-5 sm:pb-5">
          {children}
        </div>
      </div>
    </motion.div>,
    portalContainer,
  )
}
