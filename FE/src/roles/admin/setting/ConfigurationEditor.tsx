import { useEffect, useRef, useState } from "react"
import { assistantAdminApi as api, type AiConfiguration, type AiProvider, type AiSettings, type ModelPage } from "../../../lib/assistantAdminApi"

export type Editor = { mode: "add"; provider: AiProvider["id"] } | { mode: "edit" | "rotate"; row: AiConfiguration }
type Props = { editor: Editor; settings: AiSettings; busy: boolean; error: string; close: () => void
  save: (input: Record<string, unknown>) => Promise<void>; conflict: () => void }

export function ConfigurationEditor({ editor, settings, busy, error, close, save, conflict }: Props) {
  const row = editor.mode === "add" ? null : editor.row
  const [provider, setProvider] = useState(editor.mode === "add" ? editor.provider : editor.row.provider)
  const [label, setLabel] = useState(row?.label ?? "")
  const [secret, setSecret] = useState("")
  const [model, setModel] = useState(row?.model ?? "")
  const [manualModel, setManualModel] = useState(false)
  const [items, setItems] = useState<ModelPage["items"]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [phase, setPhase] = useState<"idle" | "loading" | "loaded" | "error" | "expired">("idle")
  const dialog = useRef<HTMLDialogElement>(null)
  const sequence = useRef(0)
  useEffect(() => {
    const element = dialog.current!
    element.showModal()
    return () => { sequence.current++; element.close() }
  }, [])

  function reset() {
    sequence.current++
    setItems([]); setModel(""); setCursor(null); setPhase("idle")
  }
  async function discover(next: string | null) {
    const attempt = ++sequence.current
    setPhase("loading")
    try {
      const page = await api.discover(row ? { configurationId: row.id, expectedRevision: settings.revision, cursor: next }
        : { provider, secret, cursor: next })
      if (sequence.current !== attempt) return
      setItems(previous => next ? [...previous, ...page.items] : page.items)
      setCursor(page.nextCursor); setPhase("loaded")
      if (!manualModel && !next && !page.items.some(item => item.id === model) && model !== row?.model) setModel("")
    } catch (cause) {
      if (sequence.current !== attempt) return
      const e = cause as { status?: number; code?: string }
      if (e.status === 409 && e.code !== "CURSOR_CONFLICT" && row) { conflict(); return }
      if (e.status === 409) {
        setItems([]); setCursor(null); setPhase("expired")
        if (!manualModel) setModel(row?.model ?? "")
      }
      else setPhase("error")
    }
  }
  const active = settings.configurations.filter(config => config.provider === provider && !config.revokedAt)
    .sort((a, b) => a.priority - b.priority || a.id.localeCompare(b.id))
  const validSecret = secret.length >= 8 && !/\s/.test(secret)
  const rotating = editor.mode === "rotate"
  return <dialog className="assistant-settings assistant-editor" ref={dialog} aria-labelledby="config-title"
    onKeyDown={event => {
      if (event.key === "Escape") event.stopPropagation()
      if (event.key !== "Tab") return
      event.stopPropagation()
      const controls = Array.from(dialog.current!.querySelectorAll<HTMLElement>(
        "button:not(:disabled), input:not(:disabled), select:not(:disabled)"))
      const first = controls[0], last = controls[controls.length - 1]
      if (!first) { event.preventDefault(); return }
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }}
    onCancel={event => { event.preventDefault(); if (!busy) close() }}>
    <form onSubmit={event => {
      event.preventDefault()
      if (rotating && row) void save({ action: "rotate-key", keyId: row.id, secret })
      else if (row) void save({ action: "set-configuration", keyId: row.id, label, model, enabled: row.enabled,
        priority: active.findIndex(config => config.id === row.id) + 1 })
      else void save({ action: "add-configuration", provider, label, model, secret, enabled: false, priority: active.length + 1 })
    }}>
      <h3 id="config-title">{rotating ? "Replace key" : row ? "Edit configuration" : "Add configuration"}</h3>
      {error && <p role="alert">{error}</p>}
      <fieldset className="assistant-settings-fields" disabled={busy}>
        {row ? <p>{provider === "gemini" ? "Gemini" : "OpenAI"} · {row.label}</p> :
          <label>Provider<select className="input" value={provider} onChange={e => {
            reset(); setManualModel(false); setSecret(""); setProvider(e.target.value as AiProvider["id"])
          }}><option value="gemini">Gemini</option><option value="openai">OpenAI</option></select></label>}
        {!rotating && <label>Configuration name<input className="input" required maxLength={80} value={label}
          onChange={e => setLabel(e.target.value)} /></label>}
        {(!row || rotating) && <label>New API key<input className="input" type="password" required autoComplete="new-password"
          minLength={8} maxLength={1024} spellCheck={false} value={secret} onChange={e => { reset(); setSecret(e.target.value) }} /></label>}
        {!rotating && <>
          <button type="button" className="btn-secondary" disabled={phase === "loading" || (!row && (!validSecret || !label.trim()))}
            onClick={() => void discover(null)}>{phase === "idle" ? "Load models" : "Refresh models"}</button>
          <div aria-live="polite">
            {phase === "loading" && "Loading models…"}
            {phase === "expired" && "Model list expired. Refresh models to start again."}
            {phase === "error" && "Could not load models. Check the credential or try again."}
            {phase === "loaded" && !items.length && "No compatible models found."}
          </div>
          {cursor && <button type="button" className="btn-secondary" disabled={phase === "loading"} onClick={() => void discover(cursor)}>Load more models</button>}
          {provider === "gemini" && <button type="button" className="btn-secondary" disabled={phase === "loading"}
            onClick={() => {
              if (manualModel && model !== row?.model && !items.some(item => item.id === model)) setModel("")
              setManualModel(!manualModel)
            }}>{manualModel ? "Choose from list" : "Enter model ID"}</button>}
          {provider === "gemini" && manualModel ? <label>Model ID
            <input className="input" required maxLength={128} pattern="[A-Za-z0-9][A-Za-z0-9._\-]*" value={model}
              autoComplete="off" spellCheck={false} onChange={e => setModel(e.target.value)} />
            <small>SmartPark checks this ID against the Gemini catalog when you save.</small>
          </label> : phase === "error" && provider === "openai" && !row && !items.length ? <label>Known model ID (saved disabled)
            <input className="input" required maxLength={128} pattern="[A-Za-z0-9][A-Za-z0-9._\-]*" value={model}
              onChange={e => setModel(e.target.value)} /></label> : <label>Model<select aria-label="Model" className="input" required value={model} onChange={e => setModel(e.target.value)}>
            <option value="">Select a model</option>
            {row && !items.some(item => item.id === row.model) && <option value={row.model}>{row.model}</option>}
            {items.map(item => <option key={item.id} value={item.id}>{item.displayName} ({item.id})</option>)}
          </select></label>}
        </>}
        <div className="assistant-settings-actions">
          <button className="btn-primary" disabled={phase === "loading" || (rotating ? !validSecret : !model || !label.trim())}>
            {rotating ? "Replace key" : row ? "Save changes" : "Save disabled configuration"}</button>
          <button className="btn-secondary" type="button" onClick={close}>Cancel</button>
        </div>
      </fieldset>
    </form>
  </dialog>
}
