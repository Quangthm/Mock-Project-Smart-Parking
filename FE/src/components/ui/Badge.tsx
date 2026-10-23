import type { HTMLAttributes } from "react"

export type BadgeTone = "primary" | "success" | "danger" | "warning" | "muted" | "purple"

const tones: Record<BadgeTone, string> = {
  primary: "border-[var(--primary)]/25 bg-[var(--primary)]/10 text-[var(--badge-primary-fg)]",
  success: "border-[var(--available)]/25 bg-[var(--available)]/10 text-[var(--badge-success-fg)]",
  danger: "border-[var(--occupied)]/25 bg-[var(--occupied)]/10 text-[var(--occupied)]",
  warning: "border-[var(--reserved)]/25 bg-[var(--reserved)]/10 text-[var(--badge-warning-fg)]",
  muted: "border-[var(--muted)]/25 bg-[var(--muted)]/10 text-[var(--muted)]",
  purple: "border-[var(--backup)]/25 bg-[var(--backup)]/10 text-[var(--badge-purple-fg)]",
}

export function Badge({ tone = "muted", className = "", ...props }: HTMLAttributes<HTMLSpanElement> & { tone?: BadgeTone }) {
  return <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold ${tones[tone]} ${className}`.trim()} {...props} />
}
