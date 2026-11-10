import { forwardRef, useImperativeHandle, useRef, type CSSProperties } from "react"
import { useTurnstile } from "../../hooks/useTurnstile"

export interface TurnstileWidgetRef {
  reset: () => void
  getToken: () => string | null
}

export interface TurnstileWidgetProps {
  action: string
  siteKey?: string
  cdata?: string
  theme?: "light" | "dark" | "auto"
  onTokenChange?: (token: string) => void
  onExpire?: () => void
  onError?: (error: unknown) => void
  className?: string
  style?: CSSProperties
}

export const TurnstileWidget = forwardRef<TurnstileWidgetRef, TurnstileWidgetProps>(
  function TurnstileWidget(
    {
      action,
      siteKey,
      cdata,
      theme = "auto",
      onTokenChange,
      onExpire,
      onError,
      className,
      style,
    },
    ref,
  ) {
    const containerRef = useRef<HTMLDivElement>(null)
    const { token, reset, isLoaded } = useTurnstile(containerRef, {
      action,
      siteKey,
      cdata,
      theme,
      onTokenChange,
      onExpire,
      onError,
    })

    useImperativeHandle(
      ref,
      () => ({
        reset,
        getToken: () => token,
      }),
      [reset, token],
    )

    return (
      <div
        className={className}
        style={{
          minHeight: "65px",
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          margin: "0.5rem 0",
          ...style,
        }}
      >
        <div ref={containerRef} />
      </div>
    )
  },
)
