import { useEffect, useId, useRef, useState, type ComponentProps } from "react"
import { createPortal } from "react-dom"
import { motion, useReducedMotion } from "motion/react"
import { useApp } from "../../../context/AppContext"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

const menuWidth = 220
export function LotActionMenu({ name, actions, onMore }: {
  name: string
  actions: readonly { label: string; icon: ComponentProps<typeof UntitledIcon>["name"]; run: () => void }[]
  onMore?: () => void
}) {
  const id = useId()
  const { accentColor } = useApp()
  const reduceMotion = useReducedMotion()
  const trigger = useRef<HTMLButtonElement>(null)
  const menu = useRef<HTMLDivElement>(null)
  const [position, setPosition] = useState<{ right: number; edge: number; above: boolean; maxHeight: number } | null>(null)

  function close(restoreFocus = false) {
    setPosition(null)
    if (restoreFocus) trigger.current?.focus({ preventScroll: true })
  }

  useEffect(() => {
    if (!position) return
    menu.current?.querySelector("button")?.focus({ preventScroll: true })
    const onOutside = (event: PointerEvent) => {
      if (event.target instanceof Node && !menu.current?.contains(event.target) && !trigger.current?.contains(event.target)) close()
    }
    const onScroll = (event: Event) => {
      if (event.target instanceof Node && menu.current?.contains(event.target)) return
      close()
    }
    const onResize = () => close()
    document.addEventListener("pointerdown", onOutside)
    window.addEventListener("scroll", onScroll, true)
    window.addEventListener("resize", onResize)
    return () => {
      document.removeEventListener("pointerdown", onOutside)
      window.removeEventListener("scroll", onScroll, true)
      window.removeEventListener("resize", onResize)
    }
  }, [position])

  const menuHeight = 18 + actions.length * 44 + (onMore ? 68 : 0)

  return <>
    <button ref={trigger} type="button" aria-label={`Actions for ${name}`} aria-expanded={!!position}
      aria-controls={position ? id : undefined}
      title="Lot actions"
      className="inline-grid size-11 place-items-center rounded-md text-[var(--muted)] transition-colors hover:text-[var(--primary)] focus-visible:outline-2 focus-visible:outline-[var(--focus)] aria-expanded:text-[var(--primary)] motion-reduce:transition-none"
      onClick={event => {
        if (position) { close(); return }
        const rect = event.currentTarget.getBoundingClientRect()
        const above = window.innerHeight - rect.bottom < menuHeight && rect.top > window.innerHeight - rect.bottom
        const edge = above ? window.innerHeight - rect.top + 6 : rect.bottom + 6
        setPosition({
          right: Math.min(Math.max(12, window.innerWidth - rect.right), Math.max(12, window.innerWidth - menuWidth - 12)),
          edge,
          above,
          maxHeight: Math.max(44, window.innerHeight - edge - 8),
        })
      }}>
      <span aria-hidden="true"><UntitledIcon name="menu" size={19} /></span>
    </button>
    {position && createPortal(
      <motion.div id={id} ref={menu} role="group" aria-label={`Actions for ${name}`} data-accent={accentColor}
        className="dashboard-theme fixed z-[900] max-w-[calc(100vw-24px)] overflow-y-auto overscroll-contain rounded-2xl border border-[var(--border)] bg-[var(--card)] p-2 text-[var(--fg)] shadow-xl"
        style={{
          right: position.right,
          width: menuWidth,
          maxHeight: position.maxHeight,
          [position.above ? "bottom" : "top"]: position.edge,
          transformOrigin: position.above ? "bottom right" : "top right",
        }}
        initial={reduceMotion ? false : { opacity: 0, scale: 0.9 }} animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: reduceMotion ? 0 : 0.22, ease: [0.215, 0.61, 0.355, 1] }}
        onBlur={event => {
          if (event.relatedTarget && !event.currentTarget.contains(event.relatedTarget) && event.relatedTarget !== trigger.current) close()
        }}
        onKeyDown={event => {
          if (event.key === "Escape") { event.preventDefault(); close(true); return }
          if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return
          const buttons = [...event.currentTarget.querySelectorAll("button")]
          const index = buttons.indexOf(document.activeElement as HTMLButtonElement)
          event.preventDefault()
          buttons[(index + (event.key === "ArrowDown" ? 1 : -1) + buttons.length) % buttons.length]?.focus()
        }}>
        {actions.map(({ label, icon, run }) => (
          <button key={label} type="button"
            className="flex min-h-11 w-full items-center gap-3 rounded-xl px-2.5 text-left text-sm font-medium transition-colors hover:bg-[var(--raised)] focus-visible:bg-[var(--raised)] focus-visible:outline-2 focus-visible:outline-[var(--focus)] motion-reduce:transition-none"
            onClick={() => { close(true); run() }}>
            <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-lg bg-[var(--bg)] text-[var(--muted)]">
              <UntitledIcon name={icon} size={17} />
            </span>
            {label}
          </button>
        ))}
        {onMore && <div className="mt-1.5 border-t border-[var(--border)] pt-1.5">
          <button type="button"
            className="flex min-h-11 w-full items-center gap-3 rounded-xl px-2.5 py-1.5 text-left text-sm font-medium transition-colors hover:bg-[var(--raised)] focus-visible:bg-[var(--raised)] focus-visible:outline-2 focus-visible:outline-[var(--focus)] motion-reduce:transition-none"
            onClick={() => { close(true); onMore() }}>
            <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-lg bg-[var(--bg)] text-[var(--muted)]">
              <UntitledIcon name="sliders" size={17} />
            </span>
            <span className="min-w-0 flex-1">More<span className="block text-xs font-normal text-[var(--muted)]">Table columns</span></span>
            <span aria-hidden="true" className="text-[var(--muted)]"><UntitledIcon name="arrow-right" size={14} /></span>
          </button>
        </div>}
      </motion.div>, document.body)}
  </>
}
