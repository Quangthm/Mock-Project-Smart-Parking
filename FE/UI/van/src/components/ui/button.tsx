import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * Button Component (Shadcn/UI Architecture - Carbon Standard)
 * Minimum font size: 14px (text-sm)
 * Destructive / Cancellation: Subtle Neutral Outline / Ghost (No aggressive red)
 * ============================================================================
 */
export const buttonVariants = cva(
  "inline-flex items-center justify-center font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-40 select-none cursor-pointer text-sm",
  {
    variants: {
      intent: {
        primary:
          "bg-[var(--primary)] text-[var(--primary-fg)] hover:opacity-90 shadow-xs active:scale-[0.99]",
        secondary:
          "bg-[var(--card)] text-[var(--fg)] border border-[var(--border)] hover:bg-[var(--muted)]/10 shadow-xs active:scale-[0.99]",
        outline:
          "border border-[var(--border)] bg-transparent hover:bg-[var(--muted)]/10 text-[var(--fg)] active:scale-[0.99]",
        destructive:
          "border border-[var(--border)] bg-transparent hover:border-neutral-400 hover:bg-[var(--muted)]/5 text-[var(--fg)] active:scale-[0.99]",
        ghost:
          "hover:bg-[var(--muted)]/10 text-[var(--fg)]",
        link:
          "text-[var(--primary)] underline-offset-4 hover:underline p-0 h-auto font-normal",
      },
      size: {
        sm: "h-9 px-3.5 text-sm rounded-[var(--radius-md)] gap-2",
        md: "h-11 px-5 text-sm rounded-[var(--radius-lg)] gap-2.5",
        lg: "h-12 px-6 text-base rounded-[var(--radius-lg)] gap-3",
        icon: "size-10 rounded-[var(--radius-md)] justify-center",
      },
    },
    defaultVariants: {
      intent: "primary",
      size: "md",
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, intent, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button"
    return (
      <Comp
        className={cn(buttonVariants({ intent, size, className }))}
        ref={ref}
        {...props}
      />
    )
  }
)
Button.displayName = "Button"

export { MorphButton, type MorphButtonProps } from "./morph-button"

