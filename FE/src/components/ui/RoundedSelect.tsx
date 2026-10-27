import { Children, isValidElement, useEffect, useId, useRef, useState } from "react"
import type { CSSProperties, ReactElement, ReactNode, SelectHTMLAttributes } from "react"
import { createPortal } from "react-dom"

type SelectOption = { value: string; label: string; disabled: boolean }
type RoundedSelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "children" | "onChange" | "multiple" | "size"> & {
  children: ReactNode
  onChange?: (event: { target: { value: string } }) => void
}

function optionText(content: ReactNode): string {
  if (typeof content === "string" || typeof content === "number") return String(content)
  return Children.toArray(content).map((child) => isValidElement<{ children?: ReactNode }>(child) ? optionText(child.props.children) : optionText(child)).join("")
}

export function RoundedSelect({ children, className = "", style, value, disabled, required, id, title, onChange, ...nativeProps }: RoundedSelectProps) {
  const options = Children.toArray(children)
    .filter((child): child is ReactElement<{ value?: string | number; disabled?: boolean; children?: ReactNode }> => isValidElement(child) && child.type === "option")
    .map((option): SelectOption => ({
      value: String(option.props.value ?? optionText(option.props.children)),
      label: optionText(option.props.children),
      disabled: Boolean(option.props.disabled),
    }))
  const selectedValue = String(value ?? "")
  const selected = options.find((option) => option.value === selectedValue)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const closeTimer = useRef<number | undefined>(undefined)
  const openFrame = useRef<number | undefined>(undefined)
  const [present, setPresent] = useState(false)
  const [open, setOpen] = useState(false)
  const [placement, setPlacement] = useState<"above" | "below">("below")
  const [menuStyle, setMenuStyle] = useState<CSSProperties>({})
  const [labelText, setLabelText] = useState("")
  const menuId = useId()

  useEffect(() => {
    const trigger = triggerRef.current
    const label = trigger?.closest("label") ?? [...document.querySelectorAll("label")].find((item) => item.htmlFor === id)
    if (!label) return
    const copy = label.cloneNode(true) as HTMLElement
    copy.querySelectorAll(".sp-rounded-select").forEach((item) => item.remove())
    setLabelText(copy.textContent?.replace(/\s+/g, " ").trim() ?? "")
  }, [id])

  function closeMenu(returnFocus = false) {
    window.clearTimeout(closeTimer.current)
    window.cancelAnimationFrame(openFrame.current ?? 0)
    setOpen(false)
    closeTimer.current = window.setTimeout(() => setPresent(false), 180)
    if (returnFocus) triggerRef.current?.focus()
  }

  function openMenu(focusIndex: "first" | "last" = "first") {
    if (disabled || !options.length) return
    window.clearTimeout(closeTimer.current)
    const trigger = triggerRef.current
    if (!trigger) return
    const rect = trigger.getBoundingClientRect()
    const width = Math.min(Math.max(rect.width, 188), window.innerWidth - 24)
    const left = Math.max(12, Math.min(rect.left, window.innerWidth - width - 12))
    const belowSpace = window.innerHeight - rect.bottom - 16
    const aboveSpace = rect.top - 16
    const expectedHeight = Math.min(options.length * 44 + 16, 320)
    const above = belowSpace < expectedHeight && aboveSpace > belowSpace
    const maxHeight = Math.min(320, Math.max(88, (above ? aboveSpace : belowSpace) - 8))
    const height = Math.min(expectedHeight, maxHeight)
    const computed = getComputedStyle(trigger)
    const colors = Object.fromEntries(["card", "fg", "muted", "border", "raised", "primary", "focus"].map((name) => [`--sp-select-${name}`, computed.getPropertyValue(`--${name}`)]))
    setPlacement(above ? "above" : "below")
    setMenuStyle({ ...colors, position: "fixed", top: above ? rect.top - height - 8 : rect.bottom + 8, left, width, maxHeight } as CSSProperties)
    setPresent(true)
    openFrame.current = window.requestAnimationFrame(() => {
      setOpen(true)
      window.requestAnimationFrame(() => {
        const choices = menuRef.current?.querySelectorAll<HTMLButtonElement>('[role="option"]:not(:disabled)')
        if (choices?.length) choices[focusIndex === "first" ? 0 : choices.length - 1]?.focus()
      })
    })
  }

  useEffect(() => {
    if (!present) return
    const dismiss = (event: PointerEvent) => {
      if (!triggerRef.current?.contains(event.target as Node) && !menuRef.current?.contains(event.target as Node)) closeMenu()
    }
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); closeMenu(true) }
    }
    const scroll = (event: Event) => {
      if (!menuRef.current?.contains(event.target as Node)) closeMenu()
    }
    document.addEventListener("pointerdown", dismiss)
    document.addEventListener("keydown", escape)
    document.addEventListener("scroll", scroll, true)
    window.addEventListener("resize", closeOnResize)
    function closeOnResize() { closeMenu() }
    return () => {
      document.removeEventListener("pointerdown", dismiss)
      document.removeEventListener("keydown", escape)
      document.removeEventListener("scroll", scroll, true)
      window.removeEventListener("resize", closeOnResize)
    }
  }, [present])

  useEffect(() => () => {
    window.clearTimeout(closeTimer.current)
    window.cancelAnimationFrame(openFrame.current ?? 0)
  }, [])

  return (
    <span className={`sp-rounded-select ${className}`} style={style}>
      <select {...nativeProps} id={id} value={value} disabled={disabled} required={required} className="sp-rounded-select-native" tabIndex={-1} aria-hidden="true" onFocus={() => triggerRef.current?.focus()} onInvalid={(event) => { event.preventDefault(); triggerRef.current?.focus() }} onChange={(event) => onChange?.({ target: { value: event.target.value } })}>{children}</select>
      <button ref={triggerRef} type="button" className="sp-rounded-select-trigger" title={title} aria-label={nativeProps["aria-label"] ?? (labelText ? `${labelText}: ${selected?.label ?? ""}` : undefined)} aria-haspopup="listbox" aria-expanded={open} aria-controls={menuId} aria-required={required || undefined} disabled={disabled} onClick={() => open ? closeMenu() : openMenu()} onKeyDown={(event) => {
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
          event.preventDefault()
          openMenu(event.key === "ArrowDown" ? "first" : "last")
        }
      }}>
        <span className="sp-rounded-select-value">{selected?.label ?? options[0]?.label ?? "Select"}</span>
        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="m6 9 6 6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </button>
      {present && createPortal(
        <div ref={menuRef} id={menuId} role="listbox" aria-label={nativeProps["aria-label"] ?? labelText ?? title ?? "Choose an option"} className="sp-rounded-select-menu" data-open={open} data-placement={placement} style={menuStyle} onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget as Node)) closeMenu() }} onKeyDown={(event) => {
          if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return
          event.preventDefault()
          const choices = [...event.currentTarget.querySelectorAll<HTMLButtonElement>('[role="option"]:not(:disabled)')]
          const current = choices.indexOf(document.activeElement as HTMLButtonElement)
          const next = event.key === "Home" ? 0 : event.key === "End" ? choices.length - 1 : (current + (event.key === "ArrowDown" ? 1 : -1) + choices.length) % choices.length
          choices[next]?.focus()
        }}>
          {options.map((option, index) => (
            <button key={`${option.value}-${index}`} type="button" role="option" aria-selected={option.value === selectedValue} disabled={option.disabled} onClick={() => { onChange?.({ target: { value: option.value } }); closeMenu(true) }}>
              <span>{option.label}</span><span className="sp-rounded-select-check" aria-hidden="true">{option.value === selectedValue ? "✓" : ""}</span>
            </button>
          ))}
        </div>, document.body,
      )}
    </span>
  )
}
