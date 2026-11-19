import { useEffect, useRef, useState, type DragEvent } from "react"
import { useApp } from "../../../context/AppContext"
import { assistantAdminApi as api, assistantAdminConfigured, type AiManagement, type AiProvider } from "../../../lib/assistantAdminApi"
import { ConfigurationEditor, type Editor } from "./ConfigurationEditor"
import { ConnectionTest } from "./ConnectionTest"
import "./assistant-settings.css"

const name = (id: string) => id === "gemini" ? "Gemini" : "OpenAI"
const byPriority = (a: { priority: number; id: string }, b: { priority: number; id: string }) =>
  a.priority - b.priority || a.id.localeCompare(b.id)
function failure(cause: unknown) {
  const code = (cause as { code?: string })?.code
  if (code === "MODEL_NOT_AVAILABLE") return "This model is unavailable for this Gemini key or incompatible with SmartPark."
  if (code === "MODEL_CATALOG_UNAVAILABLE") return "Could not verify the Gemini model. Try loading models again before saving."
  switch ((cause as { status?: number })?.status) {
    case 400: return "The service rejected this change. Check the fields and enabled configurations."
    case 401: return "The service could not verify your sign-in."
    case 403: return "This account cannot manage AI settings."
    case 429: return "Too many requests. Try again later."
    default: return "Could not confirm the current settings. Reload before editing."
  }
}
function Switch({ label, checked, change }: { label: string; checked: boolean; change: () => void }) {
  return <label className="assistant-settings-check"><input type="checkbox" role="switch" checked={checked}
    aria-label={label} onChange={change} />{checked ? "Enabled" : "Disabled"}</label>
}
export function AssistantSettings() {
  const { user } = useApp()
  return user?.role === "admin" ? <Management key={user.id} /> : null
}
function Management() {
  const [view, setView] = useState<AiManagement | null>(null)
  const [busy, setBusy] = useState(false)
  const [blocked, setBlocked] = useState(false)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const [maintenance, setMaintenance] = useState("")
  const [editor, setEditor] = useState<Editor | null>(null)
  const version = useRef(0)
  const latestRevision = useRef<number | null>(null)
  const pendingConflict = useRef(false)
  const locked = useRef(false)
  const drag = useRef<{ scope: string; id: string } | null>(null)
  function accept(next: AiManagement) {
    latestRevision.current = next.settings.revision
    setView(next); setMaintenance(next.settings.maintenanceMessage); setBlocked(false)
  }
  async function load(conflict = false) {
    pendingConflict.current = false
    const token = ++version.current
    setEditor(null); setBusy(true); locked.current = true; setError(""); setMessage("")
    try {
      const next = await api.read()
      if (version.current !== token) return
      accept(next)
      setMessage(conflict ? "Settings changed elsewhere. Review the reloaded values before making another change." : "")
    } catch (cause) {
      if (version.current === token) { setBlocked(true); setError(failure(cause)) }
    } finally {
      if (version.current === token) { setBusy(false); locked.current = false }
    }
    if (version.current === token && pendingConflict.current) await load(true)
  }
  useEffect(() => {
    if (assistantAdminConfigured) void load()
    return () => { version.current++ }
  }, [])
  async function save(input: Record<string, unknown>) {
    if (!view || locked.current || blocked) return
    locked.current = true
    const token = ++version.current
    setBusy(true); setError(""); setMessage("")
    try {
      const next = await api.change({ ...input, mutationId: crypto.randomUUID(), expectedRevision: view.settings.revision })
      if (version.current !== token) return
      accept(next); setEditor(null)
      setMessage(input.action === "add-configuration" ? "Configuration saved disabled. Test the connection before enabling it." : "Settings saved.")
    } catch (cause) {
      if (version.current !== token) return
      const status = (cause as { status?: number }).status
      if (status === 409) { await load(true); return }
      setError(failure(cause))
      if (status !== 400 && status !== 429) { setEditor(null); setBlocked(true) }
    } finally {
      if (version.current === token) { locked.current = false; setBusy(false) }
    }
    if (version.current === token && pendingConflict.current) await load(true)
  }
  const settings = view?.settings
  const providers = settings ? [...settings.providers].sort(byPriority) : []
  function saveProviders(rows: AiProvider[], text: string) {
    void save({ action: "save-config", providers: rows, enabled: settings!.enabled, maintenanceMessage: text })
  }
  function move(scope: string, ids: string[], source: string, target: string) {
    const from = ids.indexOf(source), to = ids.indexOf(target)
    if (from < 0 || to < 0 || from === to || blocked || locked.current) return
    const ordered = [...ids]
    ordered.splice(from, 1); ordered.splice(to, 0, source)
    void save(scope === "providers" ? { action: "reorder-providers", providerIds: ordered }
      : { action: "reorder-configurations", provider: scope, configurationIds: ordered })
  }
  function ordering(scope: string, ids: string[], id: string, label: string) {
    const index = ids.indexOf(id)
    return <div className="assistant-order">
      <span aria-hidden="true" title="Drag to reorder" draggable={!blocked && !busy}
        onDragStart={e => { drag.current = { scope, id }; e.dataTransfer.setData("text/plain", id); e.dataTransfer.effectAllowed = "move" }}
        onDragEnd={() => { drag.current = null }}>⠿</span>
      {([-1, 1] as const).map(offset => <button key={offset} type="button" className="btn-secondary"
        aria-label={`Move ${label} ${offset < 0 ? "up" : "down"}`} disabled={!ids[index + offset]}
        onClick={() => move(scope, ids, id, ids[index + offset])}>{offset < 0 ? "↑" : "↓"}</button>)}
    </div>
  }
  function drop(scope: string, ids: string[], id: string) {
    return { onDragOver(e: DragEvent) { if (drag.current?.scope === scope) { e.preventDefault(); e.stopPropagation() } },
      onDrop(e: DragEvent) {
        if (drag.current?.scope !== scope) return
        e.preventDefault(); e.stopPropagation(); move(scope, ids, drag.current.id, id); drag.current = null
      } }
  }
  function open(next: Editor) { setError(""); setEditor(next) }
  const providerIds = providers.map(p => p.id)
  return <section className="assistant-settings" aria-labelledby="assistant-settings-title">
    <header className="assistant-settings-row"><div><h3 id="assistant-settings-title">AI Chatbot</h3>
      <p>Manage availability and the order used for provider fallback.</p></div>
      {assistantAdminConfigured && <button className="btn-secondary" disabled={busy} onClick={() => void load()}>Reload settings</button>}
    </header>
    {!assistantAdminConfigured && <p role="status">Management service is not connected.</p>}
    {error && !editor && <p className="assistant-settings-error" role="alert">{error}</p>}
    <div role="status">{busy ? "Updating settings…" : message}</div>
    {view && settings && <fieldset className="assistant-settings-fields" disabled={busy || blocked}>
      <div className="assistant-settings-row"><div><h4>System availability</h4>
        <p>Revision {settings.revision} · Runtime connection {view.runtimeReleaseApproved ? "configured" : "not configured"}</p></div>
        <Switch label="System chatbot" checked={settings.enabled} change={() => void save({ action: "set-enabled", enabled: !settings.enabled })} /></div>
      {!view.secretsConfigured && <p role="status">Credential store unavailable.</p>}
      <details><summary>Maintenance message</summary><form onSubmit={e => { e.preventDefault(); saveProviders(providers, maintenance) }}>
        <label>Message<input className="input" required maxLength={500} value={maintenance} onChange={e => setMaintenance(e.target.value)} /></label>
        <button className="btn-secondary" disabled={!maintenance.trim() || maintenance === settings.maintenanceMessage}>Save message</button>
      </form></details>
      {providers.map(provider => {
        const rows = settings.configurations.filter(r => r.provider === provider.id)
          .sort((a, b) => Number(!!a.revokedAt) - Number(!!b.revokedAt) || byPriority(a, b))
        const ids = rows.filter(r => !r.revokedAt).map(r => r.id)
        return <section key={provider.id} className="assistant-settings-provider" aria-label={name(provider.id)}
          {...drop("providers", providerIds, provider.id)}>
          <header className="assistant-settings-row"><div><h4>{name(provider.id)}</h4><p>Provider priority {provider.priority}</p></div>
            <div className="assistant-settings-actions">
              <Switch label={name(provider.id)} checked={provider.enabled} change={() => saveProviders(
                providers.map(p => p.id === provider.id ? { ...p, enabled: !p.enabled } : p), settings.maintenanceMessage)} />
              {ordering("providers", providerIds, provider.id, name(provider.id))}
            </div></header>
          {!rows.length && <p>No configurations for {name(provider.id)}.</p>}
          {rows.map(row => <article className="assistant-settings-key" key={row.id} aria-label={row.label}
            {...(!row.revokedAt && drop(provider.id, ids, row.id))}>
            <div className="assistant-settings-row"><div><strong>{row.label}</strong><p>{row.model} · Priority {row.priority} · {row.revokedAt ? "Revoked" : row.enabled ? "Enabled" : "Disabled"}</p></div>
              {!row.revokedAt && ordering(provider.id, ids, row.id, row.label)}</div>
            {!row.revokedAt && <div className="assistant-settings-actions">
              <button className="btn-secondary" onClick={() => void save({ action: "set-configuration", keyId: row.id,
                label: row.label, model: row.model, enabled: !row.enabled, priority: ids.indexOf(row.id) + 1 })}>{row.enabled ? "Disable" : "Enable"}</button>
              <button className="btn-secondary" onClick={() => open({ mode: "edit", row })}>Edit</button>
              <button className="btn-secondary" disabled={!view.secretsConfigured} onClick={() => open({ mode: "rotate", row })}>Replace key</button>
              <button className="btn-secondary" onClick={() => { if (window.confirm(`Revoke “${row.label}”? New requests will no longer use it.`))
                void save({ action: "revoke-key", keyId: row.id }) }}>Revoke</button>
            </div>}
            <ConnectionTest row={row} revision={settings.revision} disabled={busy || blocked || !view.secretsConfigured} conflict={() => {
              if (latestRevision.current !== settings.revision) return
              if (locked.current) pendingConflict.current = true
              else void load(true)
            }} />
          </article>)}
          <button className="btn-secondary" disabled={!view.secretsConfigured} onClick={() => open({ mode: "add", provider: provider.id })}>Add configuration</button>
        </section>
      })}
    </fieldset>}
    {editor && settings && <ConfigurationEditor editor={editor} settings={settings} busy={busy} error={error} close={() => setEditor(null)}
      save={save} conflict={() => void load(true)} />}
  </section>
}
