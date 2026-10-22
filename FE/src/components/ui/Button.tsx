import type { ButtonHTMLAttributes } from "react"

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "outline" | "accent"
}

export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  return <button className={`btn-${variant} ${className}`.trim()} {...props} />
}
