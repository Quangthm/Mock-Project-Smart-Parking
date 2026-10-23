import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

/**
 * Merges multiple class names safely using clsx and resolves Tailwind CSS conflicts with twMerge.
 * Document Ref: tempfe/ai-ui-rules.md Section 3.2
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
