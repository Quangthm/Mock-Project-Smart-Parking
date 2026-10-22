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
    default: "text-2xl font-bold",
    compact: "text-xl font-bold",
    hero: "text-4xl font-extrabold sm:text-5xl",
    promo: "text-[1.75rem] font-extrabold",
    panel: "text-base font-bold leading-tight sm:text-[1.35rem]",
  }[variant]
  const inverse = variant === "promo"
  return (
    <header className={`flex flex-wrap items-center justify-between gap-3 ${centered ? "flex-col text-center" : ""} ${divider ? "border-b border-[var(--border)] pb-4" : ""}`}>
      <div className={centered ? "mx-auto" : ""}>
        {eyebrow && <p className={`mb-1 text-sm font-medium ${inverse ? "inline-flex items-center gap-1.5 rounded-full border border-blue-300/40 bg-blue-500/25 px-2.5 py-1 text-blue-200" : "text-[var(--primary)]"}`}>{eyebrow}</p>}
        <div className={`flex items-center gap-2 ${centered ? "justify-center" : ""}`}>
          <h1 id={id} className={`font-['Outfit'] tracking-tight ${inverse ? "text-white" : "text-[var(--fg)]"} ${titleStyle}`}>{title}</h1>
          {badge}
        </div>
        {description && <p className={`mt-1 ${variant === "panel" ? "hidden text-xs sm:block" : "text-sm"} ${inverse ? "text-slate-200" : "text-[var(--muted)]"} ${centered ? "mx-auto max-w-2xl" : ""}`}>{description}</p>}
      </div>
      {actions}
    </header>
  )
}
