import { StructureApiError, type StructureCommitment } from "../../../lib/parkingApi"

export function structureErrorMessage(cause: unknown, fallback: string): string {
  if (cause instanceof Error && cause.message) return cause.message
  if (typeof cause === "string" && cause.trim()) return cause
  return fallback
}

function formatInstant(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function CommitmentList({ commitments }: { commitments: StructureCommitment[] }) {
  return (
    <ul className="mt-2 space-y-1 text-xs">
      {commitments.map((commitment) => (
        <li key={commitment.id} className="rounded-md border border-[var(--border)] bg-[var(--card)] px-2 py-1">
          <strong className="font-semibold">{commitment.type.replace(/_/g, " ")}</strong>
          {" · "}
          {formatInstant(commitment.start)}
          {" → "}
          {commitment.end ? formatInstant(commitment.end) : "open session"}
        </li>
      ))}
    </ul>
  )
}

/**
 * Shared error/notice block for the structure panels. For a `STRUCTURE_IN_USE`
 * rejection it shows the reasons the backend returned (message plus the `commitments`
 * list when present) instead of replacing them with a generic failure.
 */
export function StructureFeedback({
  error,
  notice,
  fallback = "The parking structure change failed.",
}: {
  error?: unknown
  notice?: string
  fallback?: string
}) {
  if (!error && !notice) return null
  const structureError = error instanceof StructureApiError ? error : null
  return (
    <>
      {notice && (
        <p
          role="status"
          className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-xs text-emerald-700 dark:text-emerald-300"
        >
          {notice}
        </p>
      )}
      {error && (
        <div
          role="alert"
          className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-300"
        >
          <p className="font-medium">{structureErrorMessage(error, fallback)}</p>
          {structureError?.code && (
            <p className="mt-1 font-mono text-[10px] uppercase tracking-wide opacity-80">
              {structureError.code}
            </p>
          )}
          {structureError?.commitments && structureError.commitments.length > 0 && (
            <CommitmentList commitments={structureError.commitments} />
          )}
          {structureError?.items && structureError.items.length > 0 && (
            <ul className="mt-2 space-y-1">
              {structureError.items.map((item) => (
                <li key={`${item.resourceId}-${item.code}`} className="rounded-md border border-[var(--border)] bg-[var(--card)] px-2 py-1">
                  <strong className="font-mono text-[10px]">{item.code}</strong> {item.message}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </>
  )
}
