import * as React from "react"
import { AlertTriangle, Bot, Navigation } from "lucide-react"
import { MorphButton } from "@/components/ui/button"

export interface SpatialDockProps {
  onFindNearest: () => void
  onOpenAiAssistant: () => void
  onOpenEmergencySos: () => void
}

const iconButtonWidth = "min-w-11"

export function SpatialDock({
  onFindNearest,
  onOpenAiAssistant,
  onOpenEmergencySos,
}: SpatialDockProps) {
  return (
    <nav aria-label="Thao tác nhanh trên bản đồ" className="flex w-fit flex-col items-end gap-2 select-none">
      <MorphButton
        intent="primary"
        size="md"
        mode="icon-to-text"
        text="Bãi gần nhất"
        icon={<Navigation size={19} strokeWidth={1.75} />}
        minWidth={iconButtonWidth}
        onClick={onFindNearest}
        title="Chọn bãi xe gần nhất"
        className="h-11 w-11 rounded-full border border-[var(--border)] px-0 shadow-sm transition-[width,border-radius] duration-200 hover:w-36 hover:rounded-xl hover:px-3 focus-visible:w-36 focus-visible:rounded-xl focus-visible:px-3 motion-reduce:transition-none"
      />
      <MorphButton
        intent="secondary"
        size="md"
        mode="icon-to-text"
        text="Trợ lý"
        icon={<Bot size={19} strokeWidth={1.75} />}
        minWidth={iconButtonWidth}
        onClick={onOpenAiAssistant}
        title="Mở trợ lý đỗ xe"
        className="h-11 w-11 rounded-full border border-[var(--border)] px-0 shadow-sm transition-[width,border-radius] duration-200 hover:w-36 hover:rounded-xl hover:px-3 focus-visible:w-36 focus-visible:rounded-xl focus-visible:px-3 motion-reduce:transition-none"
      />
      <MorphButton
        intent="outline"
        size="md"
        mode="icon-to-text"
        text="Báo sự cố"
        icon={<AlertTriangle size={19} strokeWidth={1.75} />}
        minWidth={iconButtonWidth}
        onClick={onOpenEmergencySos}
        title="Báo cáo sự cố"
        className="h-11 w-11 rounded-full border border-[var(--border)] px-0 shadow-sm transition-[width,border-radius] duration-200 hover:w-36 hover:rounded-xl hover:px-3 focus-visible:w-36 focus-visible:rounded-xl focus-visible:px-3 motion-reduce:transition-none"
      />
    </nav>
  )
}
