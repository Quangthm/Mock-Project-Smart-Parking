import { useState, type FormEvent } from "react"
import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import {
  StructureApiError,
  parkingApi,
  type AccessPath,
  type ReleasedIdentifier,
  type Unit,
} from "../../../lib/parkingApi"
import { MAX_SLOT_CODE_LENGTH } from "../../../lib/structureRules"
import { ReleasedIdentifierDialog } from "./ReleasedIdentifierDialog"
import { StructureFeedback } from "./StructureFeedback"
import { useStructureAction } from "./useStructureAction"

const inputClass = "input w-full text-sm"
const labelClass = "space-y-1 text-xs font-medium text-[var(--muted)]"

export function AccessPathsPanel({
  siteId,
  units,
  paths,
  onChanged,
}: {
  siteId: string
  units: Unit[]
  paths: AccessPath[]
  onChanged: () => void
}) {
  const [code, setCode] = useState("")
  const [from, setFrom] = useState("")
  const [to, setTo] = useState("")
  const [creating, setCreating] = useState(false)
  const [formError, setFormError] = useState("")
  const [released, setReleased] = useState<ReleasedIdentifier | null>(null)
  const [formNotice, setFormNotice] = useState("")
  const { busy, error, notice, run } = useStructureAction(onChanged)

  const unitName = (id: string | null) =>
    (id && units.find((unit) => unit.id === id)?.name) || "—"

  async function createPath(event: FormEvent) {
    event.preventDefault()
    setFormError("")
    setFormNotice("")
    const trimmed = code.trim()
    if (!trimmed) {
      setFormError("Enter a path code.")
      return
    }
    if (trimmed.length > MAX_SLOT_CODE_LENGTH) {
      setFormError(`Path codes can contain at most ${MAX_SLOT_CODE_LENGTH} characters.`)
      return
    }
    if (!from || !to) {
      setFormError("Choose both endpoint units.")
      return
    }
    if (from === to) {
      setFormError("Access path endpoints must differ.")
      return
    }
    setCreating(true)
    try {
      await parkingApi.path(siteId, { code: trimmed, from, to })
      setCreating(false)
      setCode("")
      setFormNotice("Access path created.")
      onChanged()
    } catch (cause) {
      setCreating(false)
      if (
        cause instanceof StructureApiError &&
        cause.code === "IDENTIFIER_RELEASED" &&
        cause.released
      ) {
        setReleased(cause.released)
        return
      }
      setFormError(cause instanceof Error ? cause.message : "The access path could not be created.")
    }
  }

  function removePath(path: AccessPath) {
    if (!confirm(`Delete access path ${path.code}? The path row is released and can only be restored explicitly.`)) return
    void run(
      () => parkingApi.edit(siteId, { action: "removePath", resourceId: path.id }),
      `Access path ${path.code} released.`,
    )
  }

  return (
    <section className="space-y-3 rounded-2xl border border-[var(--border)] bg-[var(--card)] p-4" aria-label="Access paths">
      <div>
        <h2 className="text-base font-semibold">Access paths</h2>
        <p className="mt-1 text-xs text-[var(--muted)]">
          Internal topology between two live units. Paths are not geographic routing and never change slot capacity.
        </p>
      </div>

      <StructureFeedback error={error} notice={notice} fallback="The access path change failed." />
      <StructureFeedback error={formError || undefined} notice={formNotice} fallback="The access path could not be created." />

      <form onSubmit={createPath} className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto] sm:items-end">
        <label className={labelClass}>
          <span>Path code</span>
          <input
            className={inputClass}
            value={code}
            onChange={(event) => {
              setCode(event.target.value)
              setFormError("")
            }}
            maxLength={MAX_SLOT_CODE_LENGTH}
            placeholder="P1"
            required
          />
        </label>
        <label className={labelClass}>
          <span>From unit</span>
          <RoundedSelect className={inputClass} value={from} onChange={(event) => setFrom(event.target.value)} required>
            <option value="">Select a unit</option>
            {units.map((unit) => (
              <option key={unit.id} value={unit.id}>
                {unit.name} ({unit.type})
              </option>
            ))}
          </RoundedSelect>
        </label>
        <label className={labelClass}>
          <span>To unit</span>
          <RoundedSelect className={inputClass} value={to} onChange={(event) => setTo(event.target.value)} required>
            <option value="">Select a unit</option>
            {units.map((unit) => (
              <option key={unit.id} value={unit.id}>
                {unit.name} ({unit.type})
              </option>
            ))}
          </RoundedSelect>
        </label>
        <button type="submit" className="btn-primary px-3 py-2 text-xs" disabled={creating || busy || units.length < 2}>
          {creating ? "Creating…" : "Add path"}
        </button>
      </form>

      {paths.length === 0 ? (
        <p className="rounded-xl border border-dashed border-[var(--border)] p-4 text-xs text-[var(--muted)]">
          No access paths are configured for this site.
        </p>
      ) : (
        <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)]">
          {paths.map((path) => (
            <li key={path.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
              <div className="min-w-0 break-words">
                <span className="font-mono text-xs font-semibold text-[var(--fg)]">{path.code}</span>
                <span className="ml-2 text-xs text-[var(--muted)]">
                  {unitName(path.fromUnitId)} → {unitName(path.toUnitId)}
                </span>
              </div>
              <button
                type="button"
                className="btn-ghost px-2 py-1 text-[11px] text-[var(--occupied)]"
                onClick={() => removePath(path)}
                disabled={busy}
                aria-label={`Delete access path ${path.code}`}
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}

      {released && (
        <ReleasedIdentifierDialog
          kind="path"
          released={released}
          onClose={() => setReleased(null)}
          onChooseAnother={() => {
            setReleased(null)
            setCode("")
            setFormNotice("Enter a different path code, then add the path again.")
          }}
          onRestore={async () => {
            await parkingApi.edit(siteId, {
              action: "restorePath",
              resourceId: released.id,
            })
            setCode("")
            setFormNotice("Released access path restored. It keeps its original endpoints.")
            onChanged()
          }}
        />
      )}
    </section>
  )
}
