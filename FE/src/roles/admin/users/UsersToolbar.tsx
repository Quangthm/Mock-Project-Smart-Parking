import { RoundedSelect } from "../../../components/ui/RoundedSelect"
import { Button } from "../../../components/ui/Button"
import { Input } from "../../../components/ui/Input"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { UsersViewModel } from "./useUsers"

export function UsersToolbar({ model }: { model: UsersViewModel }) {
  const { search, setSearch, query, saving, handleSearchSubmit, handleResetFilters, setFilter } = model
  return (
      <form
        className="card flex flex-wrap items-center gap-3 p-4"
        onSubmit={handleSearchSubmit}
      >
        {/* Search Field */}
        <div className="relative min-w-[240px] flex-1">
          <span className="pointer-events-none absolute left-3 top-1/2 flex -translate-y-1/2 items-center text-[var(--muted)]">
            <UntitledIcon name="search" size={16} />
          </span>
          <Input
            className="sp-search-input w-full !pl-11 !pr-9"
            aria-label="Search users by name, email, or phone number"
            placeholder="Search by name, email, or phone number…"
            maxLength={200}
            disabled={saving}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          {search && (
            <button
              type="button"
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[var(--muted)] hover:text-[var(--fg)]"
              onClick={() => setSearch("")}
              title="Clear search"
            >
              <UntitledIcon name="x" size={14} />
            </button>
          )}
        </div>

        {/* Role Filter */}
        <div className="w-auto min-w-[140px]">
          <RoundedSelect
            className="input w-full py-2 text-sm"
            disabled={saving}
            value={query.role}
            onChange={(event) => {
              setFilter("role", event.target.value)
            }}
          >
            <option value="">All Roles</option>
            <option value="driver">Driver</option>
            <option value="owner">Owner</option>
            <option value="operator">Operator</option>
            <option value="admin">Admin</option>
          </RoundedSelect>
        </div>

        {/* Status Filter */}
        <div className="w-auto min-w-[150px]">
          <RoundedSelect
            className="input w-full py-2 text-sm"
            disabled={saving}
            value={query.status}
            onChange={(event) => {
              setFilter("status", event.target.value)
            }}
          >
            <option value="">All Statuses</option>
            <option value="active">Active</option>
            <option value="locked">Locked</option>
            <option value="pendingVerification">Pending Verification</option>
            <option value="pendingApproval">Pending Approval</option>
            <option value="rejected">Rejected</option>
          </RoundedSelect>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-2">
          <Button type="submit" disabled={saving}>
            Search
          </Button>
          {(query.search || query.role || query.status || search) && (
            <Button
              variant="outline" className="text-xs"
              type="button"
              onClick={handleResetFilters}
              disabled={saving}
            >
              Reset
            </Button>
          )}
        </div>
      </form>

  )
}
