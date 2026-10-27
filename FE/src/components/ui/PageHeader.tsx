import type { ReactNode } from "react"

export function PageHeader({ id, title, description, eyebrow, badge, actions, variant = "default", align = "start", divider = false }: {
  id?: string
  title: ReactNode
  description?: ReactNode
  eyebrow?: ReactNode
  badge?: ReactNode
  actions?: ReactNode
  variant?: "default" | "compact" | "hero" | "promo" | "panel"
  align?: "start" | "center"
  divider?: boolean
}) {
  const centered = align === "center"
  const titleStyle = {
    default: "text-2xl font-semibold",
    compact: "text-xl font-semibold",
    hero: "text-4xl font-semibold leading-[1.12] sm:text-5xl",
    promo: "text-[1.75rem] font-semibold",
    panel: "text-lg font-semibold leading-tight sm:text-[1.35rem]",
  }[variant]
  const inverse = variant === "promo"
  return (
    <header className={`flex flex-wrap items-center justify-between gap-3 ${centered ? "flex-col text-center" : ""} ${divider ? "border-b border-[var(--border)] pb-4" : ""}`}>
      <div className={centered ? "mx-auto" : ""}>
        {eyebrow && <p className={`mb-1 text-sm font-medium ${inverse ? "inline-flex items-center gap-1.5 rounded-full border border-blue-300/40 bg-blue-500/25 px-2.5 py-1 text-blue-200" : "text-[var(--primary)]"}`}>{eyebrow}</p>}
        <div className={`flex items-center gap-2 ${centered ? "justify-center" : ""}`}>
          <h1 id={id} className={`tracking-tight ${inverse ? "text-white" : "text-[var(--fg)]"} ${titleStyle}`}>{title}</h1>
          {badge}
        </div>
        {description && <p className={`${variant === "hero" ? "mt-6 max-w-xl text-base leading-7 sm:text-lg sm:leading-8" : `mt-1 ${variant === "panel" ? "hidden text-base sm:block" : "text-base"}`} ${inverse ? "text-slate-200" : "text-[var(--muted)]"} ${centered ? "mx-auto max-w-2xl" : ""}`}>{description}</p>}
      </div>
      {actions}
    </header>
  )
}
