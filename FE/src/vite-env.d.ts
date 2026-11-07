/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_PARKING_API_BASE_URL?: string
  readonly VITE_RESERVATION_API_BASE_URL?: string
  readonly VITE_PAYMENT_API_BASE_URL?: string
  readonly VITE_AI_API_BASE_URL?: string
  readonly VITE_AI_LIVE_API_BASE_URL?: string
  readonly VITE_MAPTILER_API_KEY?: string
  readonly VITE_OPERATIONAL_LAYOUT_FIXTURE?: string
  readonly VITE_TURNSTILE_SITE_KEY?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

interface TurnstileRenderOptions {
  sitekey: string
  action?: string
  cdata?: string
  theme?: "light" | "dark" | "auto"
  callback?: (token: string) => void
  "expired-callback"?: () => void
  "error-callback"?: (error: unknown) => void
}

interface Window {
  turnstile?: {
    render: (container: string | HTMLElement, options: TurnstileRenderOptions) => string
    reset: (widgetId?: string) => void
    remove: (widgetId?: string) => void
    getResponse: (widgetId?: string) => string | undefined
  }
}
