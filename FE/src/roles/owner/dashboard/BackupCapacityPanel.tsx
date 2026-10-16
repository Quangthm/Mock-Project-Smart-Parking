import { useState } from "react"

import { parkingApi, type Structure } from "../../../lib/parkingApi"

export function BackupCapacityPanel({
  structure,
  onChanged,
}: {
  structure: Structure
  onChanged: () => void
}) {
  const [form, setForm] = useState({
    unitId: "",
    vehicleType: "CAR",
    capacity: 0,
  })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  async function save(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError("")
    try {
      await parkingApi.edit(structure.site.id, {
        action: "backup",
        ...form,
        unitId: form.unitId || "00000000-0000-0000-0000-000000000000",
      })
      onChanged()
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cannot update backup.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card">
      <h3>Capacity and backup</h3>
      <p>
        Configured backup and marked backup share inventory. Their overlap is
        counted once.
      </p>
      <div style={{ overflowX: "auto" }}>
        <table>
          <thead>
            <tr>
              {[
                "Scope",
                "Category",
                "Total",
                "Occupied",
                "Protected",
                "Pending payment",
                "Backup",
                "Unavailable",
                "Available",
              ].map((label) => (
                <th key={label}>{label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {structure.capacityViews
              .filter((v) => v.total > 0)
              .map((v) => (
                <tr key={`${v.unitId}-${v.vehicleType}`}>
                  <td>
                    {structure.units.find((u) => u.id === v.unitId)?.name ??
                      "Site"}
                  </td>
                  <td>{v.vehicleType}</td>
                  <td>{v.total}</td>
                  <td>{v.occupied}</td>
                  <td>{v.protected}</td>
                  <td>{v.pendingPayment}</td>
                  <td>{v.effectiveBackup}</td>
                  <td>{v.unavailable}</td>
                  <td>
                    {v.inheritedBackup ? "Configured at ancestor" : v.available}
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>
      <form onSubmit={save}>
        <label className="label">
          Scope
          <select
            className="input"
            value={form.unitId}
            onChange={(e) => setForm({ ...form, unitId: e.target.value })}
          >
            <option value="">Whole site</option>
            {structure.units.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
          </select>
        </label>
        <label className="label">
          Vehicle category
          <select
            className="input"
            value={form.vehicleType}
            onChange={(e) => setForm({ ...form, vehicleType: e.target.value })}
          >
            {["CAR", "MOTORCYCLE"].map((t) => (
              <option key={t}>{t}</option>
            ))}
          </select>
        </label>
        <label className="label">
          Configured backup
          <input
            className="input"
            type="number"
            min={0}
            step={1}
            required
            value={form.capacity}
            onChange={(e) =>
              setForm({ ...form, capacity: Number(e.target.value) })
            }
          />
        </label>
        <button className="btn-primary" disabled={busy}>
          Save backup configuration
        </button>
      </form>
      <p role="alert">{error}</p>
    </section>
  )
}
