import { useEffect, useRef, useState } from "react"
import { assistantAdminApi as api, type AiConfiguration, type TestResult } from "../../../lib/assistantAdminApi"

export function ConnectionTest({ row, revision, disabled, conflict }: {
  row: AiConfiguration; revision: number; disabled: boolean; conflict: () => void
}) {
  const [result, setResult] = useState<TestResult | null>(null)
  const [status, setStatus] = useState("")
  const [running, setRunning] = useState(false)
  const request = useRef<{ expectedRevision: number; testRequestId: string } | null>(null)
  const receiptDeadline = useRef(0)
  const generation = useRef(0)
  const lock = useRef(false)
  useEffect(() => () => { generation.current++ }, [])
  async function check() {
    if (lock.current || disabled || row.revokedAt) return
    if (Date.now() >= receiptDeadline.current) request.current = null
    if (!request.current) {
      if (!window.confirm("Test this saved configuration? The provider may charge for a short generation request.")) return
      receiptDeadline.current = Date.now() + 55 * 60_000
      request.current = { expectedRevision: revision, testRequestId: crypto.randomUUID() }
    }
    const payload = request.current
    const attempt = generation.current
    lock.current = true
    setResult(null); setRunning(true); setStatus("Testing connection…")
    try {
      for (let poll = 0; poll < 6; poll++) {
        if (attempt !== generation.current) return
        if (Date.now() >= receiptDeadline.current) {
          request.current = null; setStatus("Result lookup expired. Start a new test to check this configuration."); return
        }
        const response = await api.test(row.id, payload)
        if (attempt !== generation.current) return
        if (response.outcome !== "running") {
          request.current = null; setResult(response); setStatus(""); return
        }
        setStatus("Test in progress…")
        await new Promise(resolve => setTimeout(resolve, 2000))
      }
      if (attempt === generation.current) setStatus("Test is still in progress. Check again for the result.")
    } catch (cause) {
      if (attempt !== generation.current) return
      const status = (cause as { status?: number }).status
      if (status && status < 500) request.current = null
      setStatus(status === 429 ? "Test rate limit reached. Try again later." : "Could not confirm the test result.")
      if (status === 409) conflict()
    } finally {
      if (attempt === generation.current) { lock.current = false; setRunning(false) }
    }
  }
  return <div className="assistant-test">
    {!row.revokedAt && <button className="btn-secondary" type="button" disabled={disabled || running} onClick={() => void check()}>
      {running ? "Testing…" : request.current ? "Check test result" : "Test connection"}</button>}
    <span role="status">{status}{result && <>
      <strong>{result.outcome}</strong> · {result.model} · {result.latencyMs} ms · <time dateTime={result.checkedAt}>{new Date(result.checkedAt).toLocaleString()}</time>
      {result.code && <> · {result.code}</>}
      {result.code === "PROVIDER_TIMEOUT" && <> · The provider did not return a result in time. Starting a new test may use quota again.</>}
      {(!result.current || result.testedRevision !== revision) && <> · Previous configuration (revision {result.testedRevision})</>}
    </>}</span>
  </div>
}
