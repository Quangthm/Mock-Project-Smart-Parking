import * as React from "react"
import { Compass, Clock, User, Sun, Moon, Sparkles, Menu } from "lucide-react"

export type SpatialNavTab = "explore" | "history" | "garage"

interface SpatialSidebarProps {
  activeTab: SpatialNavTab
  onSelectTab: (tab: SpatialNavTab) => void
  isMobileViewport: boolean
  isDark: boolean
  onToggleDark: () => void
  onOpenDesignSpec?: () => void
  onExpandedChange?: (expanded: boolean) => void
}

export function SpatialSidebar({
  activeTab,
  onSelectTab,
  isMobileViewport,
  isDark,
  onToggleDark,
  onOpenDesignSpec,
  onExpandedChange,
}: SpatialSidebarProps) {
  const [isExpanded, setIsExpanded] = React.useState(false)

  const toggleExpanded = () => {
    const expanded = !isExpanded
    setIsExpanded(expanded)
    onExpandedChange?.(expanded)
  }

  const navItems = [
    { id: "explore" as SpatialNavTab, label: "Bản đồ khám phá", icon: Compass },
    { id: "history" as SpatialNavTab, label: "Lịch sử Booking", icon: Clock },
    { id: "garage" as SpatialNavTab, label: "Hồ sơ & Garage", icon: User },
  ]

  return (
    <aside
      aria-label="Điều hướng chính"
      className={`fixed inset-y-0 left-0 z-30 flex flex-col overflow-hidden border-r border-[var(--border)] bg-[var(--card)] text-[var(--fg)] transition-[width] duration-200 ${isExpanded ? "w-64" : "w-16"}`}
    >
      <header className={`flex h-16 shrink-0 items-center gap-3 border-b border-[var(--border)] ${isExpanded ? "px-3" : "justify-center px-2"}`}>
        <button
          type="button"
          onClick={toggleExpanded}
          className="flex size-11 shrink-0 items-center justify-center rounded-xl text-[var(--muted)] transition-colors hover:bg-[var(--bg)] hover:text-[var(--fg)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]"
          aria-label={isExpanded ? "Thu gọn thanh điều hướng" : "Mở rộng thanh điều hướng"}
          aria-expanded={isExpanded}
        >
          <Menu size={21} strokeWidth={1.75} aria-hidden="true" />
        </button>
        {isExpanded && (
          <div className="min-w-0 animate-in fade-in duration-150">
            <p className="truncate text-base font-semibold tracking-tight">SmartPark</p>
            <p className="text-sm text-[var(--muted)]">Tài xế</p>
          </div>
        )}
      </header>

      <nav className="mt-4 w-full shrink-0 space-y-2 px-2" aria-label="Các trang tài xế">
        {navItems.map((item) => {
          const Icon = item.icon
          const isActive = activeTab === item.id
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => {
                onSelectTab(item.id)
                if (isMobileViewport && isExpanded) {
                  setIsExpanded(false)
                  onExpandedChange?.(false)
                }
              }}
              className={`flex h-12 w-full items-center gap-3 rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))] ${isExpanded ? "px-2" : "justify-center"} ${
                isActive
                  ? "bg-[var(--primary)] font-semibold text-[var(--primary-fg)]"
                  : "text-[var(--muted)] hover:bg-[var(--bg)] hover:text-[var(--fg)]"
              }`}
              title={!isExpanded ? item.label : undefined}
              aria-label={!isExpanded ? item.label : undefined}
              aria-current={isActive ? "page" : undefined}
            >
              <span className="flex size-11 shrink-0 items-center justify-center">
                <Icon size={20} strokeWidth={1.75} aria-hidden="true" />
              </span>
              {isExpanded && (
                <span className="flex min-w-0 flex-1 items-center justify-between gap-2 text-left text-sm">
                  <span className="truncate">{item.label}</span>
                </span>
              )}
            </button>
          )
        })}
      </nav>

      <div className="mt-auto w-full space-y-1 border-t border-[var(--border)] px-2 py-3">
        <button
          type="button"
          onClick={onToggleDark}
          className={`flex h-12 w-full items-center gap-3 rounded-xl text-[var(--muted)] transition-colors hover:bg-[var(--bg)] hover:text-[var(--fg)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))] ${isExpanded ? "px-2" : "justify-center"}`}
          title="Chuyển chế độ Sáng / Tối"
          aria-label={isDark ? "Bật giao diện sáng" : "Bật giao diện tối"}
        >
          <span className="flex size-11 shrink-0 items-center justify-center">{isDark ? <Sun size={19} /> : <Moon size={19} />}</span>
          {isExpanded && <span className="text-sm font-medium">{isDark ? "Giao diện sáng" : "Giao diện tối"}</span>}
        </button>

        {onOpenDesignSpec && (
          <button
            type="button"
            onClick={onOpenDesignSpec}
            className={`flex h-12 w-full items-center gap-3 rounded-xl text-[var(--muted)] transition-colors hover:bg-[var(--bg)] hover:text-[var(--fg)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))] ${isExpanded ? "px-2" : "justify-center"}`}
            title="Mở quy chuẩn thiết kế"
            aria-label="Mở quy chuẩn thiết kế"
          >
            <span className="flex size-11 shrink-0 items-center justify-center"><Sparkles size={19} /></span>
            {isExpanded && <span className="text-sm font-medium">Quy chuẩn thiết kế</span>}
          </button>
        )}

        <div className={`flex min-h-14 items-center gap-3 border-t border-[var(--border)] pt-3 ${isExpanded ? "px-2" : "justify-center"}`}>
          <div className="flex size-11 shrink-0 items-center justify-center rounded-full border border-[var(--border)] bg-[var(--bg)] text-sm font-semibold" aria-hidden="true">NV</div>
          {isExpanded && (
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold">Nguyễn Văn A</p>
              <p className="truncate text-sm text-[var(--muted)]">Dữ liệu mẫu</p>
            </div>
          )}
        </div>
      </div>
    </aside>
  )
}
