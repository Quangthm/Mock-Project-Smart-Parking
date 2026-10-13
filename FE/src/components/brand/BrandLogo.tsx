import type { CSSProperties } from "react"

import logo from "../../assets/Logo.png"

export function BrandLogo({
  height,
  style,
}: {
  height: number
  style?: CSSProperties
}) {
  return (
    <img
      src={logo}
      alt="Smart Parking"
      style={{
        display: "block",
        width: "auto",
        height,
        maxWidth: "100%",
        objectFit: "contain",
        ...style,
      }}
    />
  )
}
