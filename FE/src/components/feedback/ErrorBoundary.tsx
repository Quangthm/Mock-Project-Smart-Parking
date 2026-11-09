import React, { Component, type ErrorInfo, type ReactNode } from "react"

interface ErrorBoundaryProps {
  children: ReactNode
  fallback?: ReactNode | ((error: Error, reset: () => void) => ReactNode)
  onCatch?: (error: Error, errorInfo: ErrorInfo) => void
  name?: string
}

interface ErrorBoundaryState {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error(`[ErrorBoundary${this.props.name ? `:${this.props.name}` : ""}]`, error, errorInfo)
    this.props.onCatch?.(error, errorInfo)
  }

  reset = () => {
    this.setState({ hasError: false, error: null })
  }

  render() {
    if (this.state.hasError) {
      if (typeof this.props.fallback === "function") {
        return this.props.fallback(this.state.error || new Error("Unknown error"), this.reset)
      }
      if (this.props.fallback) {
        return this.props.fallback
      }
      return (
        <div
          role="alert"
          className="my-4 rounded-xl border border-red-500/30 bg-red-500/10 p-5 text-sm text-[var(--fg)] shadow-sm"
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-red-600 dark:text-red-400">
                {this.props.name ? `Error in ${this.props.name}` : "Something went wrong"}
              </h3>
              <p className="mt-1 text-xs text-[var(--muted)]">
                {this.state.error?.message || "An unexpected rendering error occurred."}
              </p>
            </div>
            <button
              type="button"
              className="btn-outline px-3 py-1.5 text-xs cursor-pointer"
              onClick={this.reset}
            >
              Retry
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
