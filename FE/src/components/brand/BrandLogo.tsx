import type { CSSProperties } from "react"
import iconBase from "../../assets/smartpark-icon-base.svg"
import iconAccent from "../../assets/smartpark-icon-accent.svg"

export function BrandLogo({
  height,
  style,
}: {
  height: number
  style?: CSSProperties
}) {
  const size = height >= 60 ? 42 : Math.min(height, 36)
  return (
    <span className="brand-logo" role="img" aria-label="SmartParking" style={{ display: "block", position: "relative", width: size * 124 / 142, height: size, flex: "none", ...style }}>
      <span aria-hidden="true" className="brand-logo-layer brand-logo-base" style={{ maskImage: `url("${iconBase}")`, WebkitMaskImage: `url("${iconBase}")` }} />
      <span aria-hidden="true" className="brand-logo-layer brand-logo-accent" style={{ maskImage: `url("${iconAccent}")`, WebkitMaskImage: `url("${iconAccent}")` }} />
    </span>
  )
}

export function BrandWordmark({ height = 23 }: { height?: number }) {
  return <span className="brand-wordmark-text" aria-hidden="true" style={{ fontSize: height, flex: "none" }}>
    <span>Smart</span><span>Parking</span>
  </span>
}
