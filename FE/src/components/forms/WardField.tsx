import { useEffect, useState } from "react"
import { RoundedSelect } from "../ui/RoundedSelect"
import { loadCurrentWards } from "../../lib/vietnamAdministrativeUnits"

export function WardField({ id, provinceCode, value, onChange, required = false }: {
  id: string
  provinceCode: string
  value: string
  onChange: (ward: string) => void
  required?: boolean
}) {
  const [wards, setWards] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [unavailable, setUnavailable] = useState(false)
  const [manual, setManual] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    setWards([])
    setLoading(true)
    setUnavailable(false)
    setManual(false)
    loadCurrentWards(provinceCode, controller.signal)
      .then(result => { if (!controller.signal.aborted) setWards(result) })
      .catch(() => { if (!controller.signal.aborted) setUnavailable(true) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [provinceCode])

  const custom = manual || unavailable || (!loading && !!value && !wards.includes(value))
  return <div>
    <label className="label" htmlFor={id}>Ward / Commune / Special zone {required ? "*" : ""}</label>
    {!unavailable && <RoundedSelect id={id} className="input" value={custom ? "__manual__" : value}
      disabled={loading} required={required && !custom} onChange={event => {
        if (event.target.value === "__manual__") {
          setManual(true)
          onChange("")
        } else {
          setManual(false)
          onChange(event.target.value)
        }
      }}>
      <option value="" disabled>{loading ? "Loading wards…" : "Select a ward or commune"}</option>
      {wards.map(ward => <option key={ward} value={ward}>{ward}</option>)}
      <option value="__manual__">Enter ward or commune manually</option>
    </RoundedSelect>}
    {custom && <input className={`input ${unavailable ? "" : "mt-2"}`} aria-label="Ward, commune or special zone"
      placeholder="e.g. Phường Sài Gòn" value={value} maxLength={120} required={required}
      onChange={event => onChange(event.target.value)} />}
    {unavailable && <p role="status" className="mt-1 text-xs text-[var(--muted)]">Ward list unavailable. Enter the current ward or commune manually.</p>}
  </div>
}
