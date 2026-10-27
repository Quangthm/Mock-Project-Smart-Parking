import * as React from "react"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * Input Component (Accessible & Touch-friendly Form Primitive)
 * Document Ref: tempfe/design-system.md Section 6
 * ============================================================================
 */
export interface InputProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: boolean
  startIcon?: React.ReactNode
  endIcon?: React.ReactNode
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, error, startIcon, endIcon, ...props }, ref) => {
    return (
      <div className="relative flex items-center w-full">
        {startIcon && (
          <div className="absolute left-3 flex items-center pointer-events-none text-[var(--muted)]">
            {startIcon}
          </div>
        )}
        <input
          type={type}
          className={cn(
            "flex h-10 w-full rounded-[var(--radius-md)] border bg-[var(--card)] px-3 py-2 text-sm text-[var(--fg)] placeholder:text-[var(--muted)]/70 transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:border-[var(--primary)] disabled:cursor-not-allowed disabled:opacity-50",
            error ? "border-[var(--destructive)] focus-visible:ring-[var(--destructive)]" : "border-[var(--border)]",
            startIcon && "pl-9",
            endIcon && "pr-9",
            className
          )}
          ref={ref}
          {...props}
        />
        {endIcon && (
          <div className="absolute right-3 flex items-center text-[var(--muted)]">
            {endIcon}
          </div>
        )}
      </div>
    )
  }
)
Input.displayName = "Input"
