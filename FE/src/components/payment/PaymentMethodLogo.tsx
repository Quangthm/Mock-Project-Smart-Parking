import type { CSSProperties } from "react"

import { getPaymentMethodInfo } from "../../roles/driver/data/paymentOptions"

interface PaymentMethodLogoProps {
  method: string

  size?: "xs" | "sm" | "md" | "lg"

  showName?: boolean

  className?: string

  style?: CSSProperties
}

export function PaymentMethodLogo({
  method,

  size = "md",

  showName = false,

  className = "",

  style,
}: PaymentMethodLogoProps) {
  const info = getPaymentMethodInfo(method)

  const sizeMap = {
    xs: {
      height: 16,

      maxWidth: 36,

      fontSize: "0.75rem",

      gap: "0.35rem",
    },

    sm: {
      height: 20,

      maxWidth: 46,

      fontSize: "0.8rem",

      gap: "0.4rem",
    },

    md: {
      height: 28,

      maxWidth: 60,

      fontSize: "0.85rem",

      gap: "0.5rem",
    },

    lg: {
      height: 36,

      maxWidth: 80,

      fontSize: "0.9rem",

      gap: "0.6rem",
    },
  }

  const currentSize = sizeMap[size]

  const logoImg = (
    <img
      src={info.logo}
      alt={info.name}
      title={info.name}
      style={{
        height: currentSize.height,

        maxWidth: currentSize.maxWidth,

        objectFit: "contain",

        display: "inline-block",

        verticalAlign: "middle",

        flexShrink: 0,
      }}
      loading="lazy"
    />
  )

  if (!showName) {
    return (
      <span
        className={className}
        style={{ display: "inline-flex", alignItems: "center", ...style }}
      >
        {logoImg}
      </span>
    )
  }

  return (
    <span
      className={className}
      style={{
        display: "inline-flex",

        alignItems: "center",

        gap: currentSize.gap,

        fontSize: currentSize.fontSize,

        fontWeight: 600,

        color: "var(--fg)",

        ...style,
      }}
    >
      {logoImg}
      <span>{info.name}</span>
    </span>
  )
}
