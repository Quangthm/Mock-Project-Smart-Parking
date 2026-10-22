import * as React from "react"
import { buttonVariants, type ButtonProps } from "./button"
import { cn } from "@/lib/utils"

/**
 * ============================================================================
 * MorphButton Component (Carbon Standard - Micro-Interaction)
 * Hover interaction that smoothly swaps back and forth between Text and Icon
 * using a mechanical ticker roll transition (cubic-bezier easing).
 * ============================================================================
 */
export interface MorphButtonProps extends Omit<ButtonProps, "children"> {
  text: string
  icon: React.ReactNode
  /**
   * "text-to-icon": Displays text by default; morphs to icon on hover (default)
   * "icon-to-text": Displays icon by default; morphs to text on hover
   */
  mode?: "text-to-icon" | "icon-to-text"
  minWidth?: string
}

export const MorphButton = React.forwardRef<HTMLButtonElement, MorphButtonProps>(
  (
    {
      className,
      intent = "primary",
      size = "md",
      text,
      icon,
      mode = "text-to-icon",
      minWidth,
      disabled,
      ...props
    },
    ref
  ) => {
    const isTextToIcon = mode === "text-to-icon"
    const isIconToText = mode === "icon-to-text"
    const defaultMinWidth = size === "sm" ? "min-w-[100px]" : "min-w-[124px]"
    const resolvedMinWidth = minWidth ?? defaultMinWidth

    return (
      <button
        ref={ref}
        type="button"
        disabled={disabled}
        aria-label={text}
        className={cn(
          buttonVariants({ intent, size }),
          "group relative overflow-hidden select-none transition-colors",
          isIconToText && "morph-button-icon-to-text",
          resolvedMinWidth,
          className
        )}
        {...props}
      >
        {/* Layer 1: Text */}
        <span
          className={cn(
            "morph-button-label inline-flex items-center justify-center whitespace-nowrap font-medium transition-all duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] motion-reduce:transition-none",
            disabled
              ? "translate-y-0 opacity-100"
              : isTextToIcon
                ? "translate-y-0 opacity-100 group-hover:-translate-y-8 group-hover:opacity-0 group-focus-visible:-translate-y-8 group-focus-visible:opacity-0"
                : "-translate-y-8 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 group-focus-visible:translate-y-0 group-focus-visible:opacity-100"
          )}
        >
          {text}
        </span>

        {/* Layer 2: Icon (Centered overlay) */}
        <span
          aria-hidden="true"
          className={cn(
            "morph-button-icon absolute inset-0 flex items-center justify-center transition-all duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] pointer-events-none motion-reduce:transition-none",
            disabled
              ? "translate-y-8 opacity-0"
              : isTextToIcon
                ? "translate-y-8 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 group-hover:scale-110 group-focus-visible:translate-y-0 group-focus-visible:opacity-100 group-focus-visible:scale-110"
                : "translate-y-0 opacity-100 group-hover:translate-y-8 group-hover:opacity-0 group-focus-visible:translate-y-8 group-focus-visible:opacity-0"
          )}
        >
          {icon}
        </span>
      </button>
    )
  }
)

MorphButton.displayName = "MorphButton"
