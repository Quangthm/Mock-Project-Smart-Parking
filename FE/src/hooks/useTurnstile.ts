import { useEffect, useRef, useState, useCallback, type RefObject } from "react"

const SCRIPT_ID = "cf-turnstile-script"
const SCRIPT_URL = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit"
const CLOUDFLARE_TEST_SITE_KEY = "1x00000000000000000000AA" // Cloudflare always-pass dummy site key

function loadTurnstileScript(): Promise<void> {
  if (typeof window === "undefined") return Promise.resolve()
  if (window.turnstile) return Promise.resolve()

  const existing = document.getElementById(SCRIPT_ID) as HTMLScriptElement | null
  if (existing) {
    return new Promise((resolve, reject) => {
      const checkReady = () => {
        if (window.turnstile) {
          resolve()
        } else {
          setTimeout(checkReady, 50)
        }
      }
      existing.addEventListener("load", checkReady, { once: true })
      existing.addEventListener("error", (e) => reject(e), { once: true })
      checkReady()
    })
  }

  return new Promise((resolve, reject) => {
    const script = document.createElement("script")
    script.id = SCRIPT_ID
    script.src = SCRIPT_URL
    script.async = true
    script.defer = true
    script.onload = () => {
      const checkReady = () => {
        if (window.turnstile) {
          resolve()
        } else {
          setTimeout(checkReady, 50)
        }
      }
      checkReady()
    }
    script.onerror = (e) => reject(e)
    document.head.appendChild(script)
  })
}

export interface UseTurnstileOptions {
  action: string
  siteKey?: string
  cdata?: string
  theme?: "light" | "dark" | "auto"
  onTokenChange?: (token: string) => void
  onExpire?: () => void
  onError?: (error: unknown) => void
}

export function useTurnstile(
  containerRef: RefObject<HTMLDivElement | null>,
  options: UseTurnstileOptions,
) {
  const { action, siteKey, cdata, theme = "auto", onTokenChange, onExpire, onError } = options

  const [token, setToken] = useState<string | null>(null)
  const [isLoaded, setIsLoaded] = useState(false)
  const widgetIdRef = useRef<string | null>(null)

  const effectiveSiteKey = siteKey || import.meta.env.VITE_TURNSTILE_SITE_KEY || CLOUDFLARE_TEST_SITE_KEY

  const reset = useCallback(() => {
    setToken(null)
    onTokenChange?.("")
    if (widgetIdRef.current && window.turnstile) {
      try {
        window.turnstile.reset(widgetIdRef.current)
      } catch (err) {
        console.warn("Turnstile reset error:", err)
      }
    }
  }, [onTokenChange])

  useEffect(() => {
    let cancelled = false
    let currentWidgetId: string | null = null

    // Reset token state when action/sitekey changes
    setToken(null)
    onTokenChange?.("")

    loadTurnstileScript()
      .then(() => {
        if (cancelled) return
        setIsLoaded(true)

        if (!containerRef.current || !window.turnstile) return

        try {
          currentWidgetId = window.turnstile.render(containerRef.current, {
            sitekey: effectiveSiteKey,
            action,
            cdata,
            theme,
            callback: (newToken: string) => {
              if (cancelled) return
              setToken(newToken)
              onTokenChange?.(newToken)
            },
            "expired-callback": () => {
              if (cancelled) return
              setToken(null)
              onTokenChange?.("")
              onExpire?.()
            },
            "error-callback": (err: unknown) => {
              if (cancelled) return
              setToken(null)
              onTokenChange?.("")
              onError?.(err)
            },
          })
          widgetIdRef.current = currentWidgetId
        } catch (err) {
          console.error("Turnstile render failed:", err)
          onError?.(err)
        }
      })
      .catch((err) => {
        if (!cancelled) {
          console.error("Failed to load Turnstile script:", err)
          onError?.(err)
        }
      })

    return () => {
      cancelled = true
      if (currentWidgetId && window.turnstile) {
        try {
          window.turnstile.remove(currentWidgetId)
        } catch {
          // ignore cleanup errors during unmount
        }
        if (widgetIdRef.current === currentWidgetId) {
          widgetIdRef.current = null
        }
      }
    }
  }, [action, effectiveSiteKey, theme, cdata, containerRef, onTokenChange, onExpire, onError])

  return {
    token,
    isLoaded,
    reset,
  }
}
