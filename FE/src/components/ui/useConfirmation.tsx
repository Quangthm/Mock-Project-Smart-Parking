import { useEffect, useRef, useState } from "react"
import { Modal } from "./Modal"

type Prompt = { title: string; description: string; confirmLabel: string; danger?: boolean }

/** UI-only replacement for blocking browser confirmations. */
export function useConfirmation() {
  const [prompt, setPrompt] = useState<Prompt | null>(null)
  const resolve = useRef<((confirmed: boolean) => void) | null>(null)
  useEffect(() => () => { resolve.current?.(false); resolve.current = null }, [])
  function finish(confirmed: boolean) {
    resolve.current?.(confirmed)
    resolve.current = null
    setPrompt(null)
  }
  function confirm(next: Prompt): Promise<boolean> {
    resolve.current?.(false)
    setPrompt(next)
    return new Promise(done => { resolve.current = done })
  }
  const confirmation = prompt && <Modal title={prompt.title} onClose={() => finish(false)}>
    <p className="text-sm leading-relaxed break-words text-[var(--muted)]">{prompt.description}</p>
    <div className="mt-6 flex flex-wrap justify-end gap-3">
      <button type="button" className="btn-outline" onClick={() => finish(false)}>Cancel</button>
      <button type="button" className={prompt.danger ? "btn-outline structure-action-danger" : "btn-primary"} onClick={() => finish(true)}>{prompt.confirmLabel}</button>
    </div>
  </Modal>
  return { confirm, confirmation }
}
