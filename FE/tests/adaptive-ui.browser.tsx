import React, { useState } from "react"
import { createRoot } from "react-dom/client"
import { MemoryRouter } from "react-router-dom"
import { AppProvider } from "../src/context/AppContext"
import { Modal } from "../src/components/ui/Modal"
import { RoundedSelect } from "../src/components/ui/RoundedSelect"
import { ParkingDateField } from "../src/components/forms/ParkingDateField"
import { useConfirmation } from "../src/components/ui/useConfirmation"
import { UsersTable } from "../src/roles/admin/users/UsersTable"
import type { UsersViewModel } from "../src/roles/admin/users/useUsers"
import "../src/index.css"
import "../src/theme.css"
import "../src/workspace-layout.css"
import "../src/ui-refinement.css"
import "../src/motion.css"
import "../src/adaptive-ui.css"

// Dev-only fixtures: no fake user/token, no backend writes, no production route.
function Fixture() {
  const [open, setOpen] = useState(false)
  const [date, setDate] = useState("")
  const [lot, setLot] = useState("all")
  const [selected, setSelected] = useState<string | null>(null)
  const [removed, setRemoved] = useState(false)
  const { confirm, confirmation } = useConfirmation()
  const model = { selectedId: selected, setSelectedId: setSelected, loading: false, saving: false,
    totalItems: 1, currentPage: 1, totalPages: 1, startIdx: 1, endIdx: 1, setPageNumber: () => {},
    page: { items: [{ id: "fixture-account", fullName: "Nguyễn Thị Minh Anh — Long account name for responsive testing", email: "long.email.address.for.responsive.testing@example.com", phone: "0901234567", roles: ["owner", "driver"], status: "active" }] }
  } as unknown as UsersViewModel
  return <main className="dashboard-theme" style={{ padding: 16, minHeight: "100dvh" }}>
    <h1 className="mb-4 text-xl font-bold">Adaptive UI test fixtures</h1>
    <p className="mb-4">Component tests only — not authenticated E2E.</p>
    <div className="mb-5 grid items-start gap-4 sm:grid-cols-3">
      <label className="label">Parking lot<RoundedSelect value={lot} onChange={e => setLot(e.target.value)}><option value="all">All parking lots</option><option value="long">A long parking facility name that must fit small screens</option></RoundedSelect></label>
      <ParkingDateField label="From date" value={date} onChange={setDate} />
      <button className="btn-outline" onClick={() => setOpen(true)}>Open form</button>
    </div>
    <UsersTable model={model} />
    <p role="status">{selected ? "Account selected" : "No selection"} · {removed ? "Confirmed" : "Not confirmed"}</p>
    {open && <Modal title="Edit parking site" onClose={() => setOpen(false)}>
      <label className="label">Site name<input className="input my-3 w-full" defaultValue="Draft survives nested dialog" /></label>
      <ParkingDateField label="Maintenance date" value={date} onChange={setDate} />
      <label className="label mt-4">Vehicle<RoundedSelect value={lot} onChange={e => setLot(e.target.value)}><option value="all">All vehicles</option><option value="car">Car</option></RoundedSelect></label>
      <button className="btn-outline mt-5" onClick={async () => { if (await confirm({ title: "Remove item?", description: "A fixture confirmation. No data is deleted.", confirmLabel: "Remove", danger: true })) setRemoved(true) }}>Remove item</button>
    </Modal>}
    {confirmation}
  </main>
}
createRoot(document.getElementById("root")!).render(<React.StrictMode><MemoryRouter><AppProvider><Fixture /></AppProvider></MemoryRouter></React.StrictMode>)
