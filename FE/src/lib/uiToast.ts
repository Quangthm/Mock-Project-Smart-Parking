export type UiToastTone = "success" | "error" | "info"

export interface UiToastDetail {
  title: string
  message: string
  tone: UiToastTone
}

export const uiToastEvent = "sp:ui-toast"

export function showUiToast(title: string, message: string, tone: UiToastTone = "success") {
  window.dispatchEvent(new CustomEvent<UiToastDetail>(uiToastEvent, {
    detail: { title, message, tone },
  }))
}
