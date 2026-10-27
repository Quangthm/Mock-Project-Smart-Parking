import { PageHeader } from "../../../components/ui/PageHeader"
import { Badge } from "../../../components/ui/Badge"
import { Button } from "../../../components/ui/Button"
import { useUsers } from "./useUsers"
import { UsersToolbar } from "./UsersToolbar"
import { UsersTable } from "./UsersTable"
import { UserDetailPanel } from "./UserDetailPanel"

export function Users() {
  const model = useUsers()
  if (model.admin?.role !== "admin") {
    return <div role="alert" className="p-6 text-sm text-[var(--fg)]">Administrator access required.</div>
  }

  return (
    <section className="w-full space-y-5" aria-label="User Management">
      <PageHeader title="User Management" badge={<Badge tone="primary" className="border-0">{model.totalItems} accounts</Badge>} description="Search accounts, view user details, and lock or unlock sign-in access." />
      <UsersToolbar model={model} />
      {model.error && (
        <div role="alert" className="flex items-center justify-between rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-600">
          <span>{model.error}</span>
          <Button variant="outline" className="text-xs" onClick={model.retryList}>Retry</Button>
        </div>
      )}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <UsersTable model={model} />
        <UserDetailPanel model={model} />
      </div>
    </section>
  )
}
