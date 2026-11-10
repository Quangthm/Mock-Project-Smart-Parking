import { useEffect, useRef } from "react"

const focusableSelector = 'a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'

export function useDialogKeyboard<T extends HTMLElement = HTMLDivElement>(activeDialog: string | null, onClose: () => void) {
  const dialogRef = useRef<T>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    if (!activeDialog) return
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const dialog = dialogRef.current
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = "hidden"
    const controls = () => [...(dialog?.querySelectorAll<HTMLElement>(focusableSelector) ?? [])].filter(element => element.getClientRects().length > 0)
    ;(controls()[0] ?? dialog)?.focus()

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault()
        onCloseRef.current()
        return
      }
      if (event.key !== "Tab") return
      const items = controls()
      if (!items.length) {
        event.preventDefault()
        dialog?.focus()
        return
      }
      const first = items[0]
      const last = items[items.length - 1]
      if (event.shiftKey && (document.activeElement === first || !dialog?.contains(document.activeElement))) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && (document.activeElement === last || !dialog?.contains(document.activeElement))) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener("keydown", onKeyDown)
    return () => {
      document.removeEventListener("keydown", onKeyDown)
      document.body.style.overflow = previousOverflow
      previousFocus?.focus()
    }
  }, [activeDialog])

  return dialogRef
}
