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
  const menuRequested = useRef(false)
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
    if (!menuRequested.current) return
    menuRequested.current = false
    window.clearTimeout(closeTimer.current)
    window.cancelAnimationFrame(openFrame.current ?? 0)
    setOpen(false)
    closeTimer.current = window.setTimeout(() => setPresent(false), 180)
    if (returnFocus) triggerRef.current?.focus({ preventScroll: true })
  }

  function openMenu(focusIndex: "first" | "last" = "first") {
    if (menuRequested.current || disabled || !options.length) return
    window.clearTimeout(closeTimer.current)
    const trigger = triggerRef.current
    if (!trigger) return
    menuRequested.current = true
    const rect = trigger.getBoundingClientRect()
    const width = Math.min(Math.max(rect.width, 188), window.innerWidth - 24)
    const left = Math.max(12, Math.min(rect.left, window.innerWidth - width - 12))
    const viewport = window.visualViewport
    const viewportTop = viewport?.offsetTop ?? 0
    const viewportBottom = viewportTop + (viewport?.height ?? window.innerHeight)
    const belowSpace = viewportBottom - rect.bottom - 16
    const aboveSpace = rect.top - viewportTop - 16
    const expectedHeight = Math.min(options.length * 44 + 16, 320)
    const above = belowSpace < expectedHeight && aboveSpace > belowSpace
    const maxHeight = Math.min(320, Math.max(44, (above ? aboveSpace : belowSpace) - 8))
    const height = Math.min(expectedHeight, maxHeight)
    const computed = getComputedStyle(trigger)
    const colors = Object.fromEntries(["card", "fg", "muted", "border", "raised", "primary", "focus"].map((name) => [`--sp-select-${name}`, computed.getPropertyValue(`--${name}`)]))
    setPlacement(above ? "above" : "below")
    setMenuStyle({ ...colors, position: "fixed", top: above ? rect.top - height - 8 : rect.bottom + 8, left, width, maxHeight } as CSSProperties)
    setPresent(true)
    openFrame.current = window.requestAnimationFrame(() => {
      setOpen(true)
      openFrame.current = window.requestAnimationFrame(() => {
        const choices = menuRef.current?.querySelectorAll<HTMLButtonElement>('[role="option"]:not(:disabled)')
        if (choices?.length) choices[focusIndex === "first" ? 0 : choices.length - 1]?.focus({ preventScroll: true })
      })
    })
  }

  useEffect(() => {
    if (!present) return
    const dismiss = (event: PointerEvent) => {
      if (!triggerRef.current?.contains(event.target as Node) && !menuRef.current?.contains(event.target as Node)) closeMenu()
    }
    const scroll = (event: Event) => {
      if (!menuRef.current?.contains(event.target as Node)) closeMenu()
    }
    document.addEventListener("pointerdown", dismiss)
    document.addEventListener("scroll", scroll, true)
    window.addEventListener("resize", closeOnResize)
    window.visualViewport?.addEventListener("resize", closeOnResize)
    function closeOnResize() { closeMenu() }
    return () => {
      document.removeEventListener("pointerdown", dismiss)
      document.removeEventListener("scroll", scroll, true)
      window.removeEventListener("resize", closeOnResize)
      window.visualViewport?.removeEventListener("resize", closeOnResize)
    }
  }, [present])

  useEffect(() => () => {
    window.clearTimeout(closeTimer.current)
    window.cancelAnimationFrame(openFrame.current ?? 0)
  }, [])

  return (
    <span className={`sp-rounded-select ${className}`} style={style} onKeyDown={(event) => {
      if (event.key !== "Escape" || !menuRequested.current) return
      event.preventDefault()
      event.stopPropagation()
      closeMenu(true)
    }}>
      <select {...nativeProps} id={id} value={value} disabled={disabled} required={required} className="sp-rounded-select-native" tabIndex={-1} aria-hidden="true" onFocus={() => triggerRef.current?.focus()} onInvalid={(event) => { event.preventDefault(); triggerRef.current?.focus() }} onChange={(event) => onChange?.({ target: { value: event.target.value } })}>{children}</select>
      <button ref={triggerRef} type="button" className="sp-rounded-select-trigger" title={title} aria-label={nativeProps["aria-label"] ?? (labelText ? `${labelText}: ${selected?.label ?? ""}` : undefined)} aria-haspopup="listbox" aria-expanded={open} aria-controls={present ? menuId : undefined} aria-required={required || undefined} disabled={disabled}
        onPointerDown={(event) => { if (event.button === 0 && menuRequested.current) event.preventDefault() }}
        onClick={() => menuRequested.current ? closeMenu(true) : openMenu()} onKeyDown={(event) => {
        if (event.key === "ArrowDown" || event.key === "ArrowUp") {
          event.preventDefault()
          openMenu(event.key === "ArrowDown" ? "first" : "last")
        }
      }}>
        <span className="sp-rounded-select-value">{selected?.label ?? options[0]?.label ?? "Select"}</span>
        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none"><path d="m6 9 6 6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </button>
      {present && createPortal(
        <div ref={menuRef} id={menuId} role="listbox" aria-label={nativeProps["aria-label"] || labelText || title || "Choose an option"} className="sp-rounded-select-menu" data-open={open} data-placement={placement} style={menuStyle} inert={!open} onBlur={(event) => {
          // A second press moves focus to the trigger before its click toggles the menu.
          if (event.relatedTarget !== triggerRef.current && !event.currentTarget.contains(event.relatedTarget)) closeMenu()
        }} onKeyDown={(event) => {
          if (event.key === "Tab") { closeMenu(true); return }
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
