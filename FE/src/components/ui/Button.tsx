import type { ButtonHTMLAttributes, ReactNode } from "react"

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost" | "danger" | "outline" | "accent"
  loading?: boolean
  leadingIcon?: ReactNode
  trailingIcon?: ReactNode
}

export function Button({ variant = "primary", className = "", loading = false, disabled, leadingIcon, trailingIcon, children, ...props }: ButtonProps) {
  return (
    <button
      className={`btn-${variant} ${loading ? "btn-loading" : ""} ${className}`.trim()}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...props}
    >
      {leadingIcon && <span className="btn-icon" aria-hidden="true">{leadingIcon}</span>}
      <span>{children}</span>
      {trailingIcon && <span className="btn-icon" aria-hidden="true">{trailingIcon}</span>}
    </button>
  )
}
