import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * Badge Component (Solid Capsule Standard - No Border, No Pastel)
 * Only concise status labels (1-2 words): "Còn chỗ", "Sắp hết", "Hết chỗ", "Đang đỗ", "Bảo trì"
 * Strictly complies with Non-Negotiable Constraints #2 & #3:
 *   - Solid borderless capsule (border-0, rounded-full)
 *   - Typography >= 14px (text-sm)
 *   - No embedded progress bar
 *   - No washed-out pastel with heavy borders
 * ============================================================================
 */
export const badgeVariants = cva(
  "inline-flex items-center justify-center font-semibold transition-colors select-none rounded-full text-sm leading-none border-0 shadow-none",
  {
    variants: {
      variant: {
        available:
          "bg-[var(--badge-success-bg)] text-[var(--badge-success-fg)]",
        almostFull:
          "bg-[var(--badge-warning-bg)] text-[var(--badge-warning-fg)]",
        occupied:
          "bg-[var(--badge-danger-bg)] text-[var(--badge-danger-fg)]",
        primary:
          "bg-[var(--badge-primary-bg)] text-[var(--badge-primary-fg)]",
        neutral:
          "bg-[var(--badge-neutral-bg)] text-[var(--badge-neutral-fg)]",
      },
      size: {
        sm: "px-3 py-1.5 text-sm",
        md: "px-4 py-1.5 text-sm",
        lg: "px-5 py-2 text-base",
      },
    },
    defaultVariants: {
      variant: "neutral",
      size: "sm",
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({
  className,
  variant = "neutral",
  size,
  children,
  ...props
}: BadgeProps) {
  return (
    <div
      className={cn(badgeVariants({ variant, size }), className)}
      {...props}
    >
      {children}
    </div>
  )
}
