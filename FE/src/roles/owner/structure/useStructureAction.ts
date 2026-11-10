import { useCallback, useRef, useState } from "react"

/**
 * Small action runner shared by the Owner structure panels. It keeps one in-flight
 * mutation at a time (the server also serialises on the site gate), keeps the raw error
 * so contract members survive, and refreshes the read after a committed change.
 */
export function useStructureAction(onChanged?: () => void) {
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const [error, setError] = useState<unknown>(null)
  const [notice, setNotice] = useState("")
  const changedRef = useRef(onChanged)
  changedRef.current = onChanged

  const run = useCallback(async (action: () => Promise<unknown>, success?: string) => {
    if (busyRef.current) return false
    busyRef.current = true
    setBusy(true)
    setError(null)
    setNotice("")
    try {
      await action()
      if (success) setNotice(success)
      changedRef.current?.()
      return true
    } catch (cause) {
      setError(cause)
      return false
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }, [])

  return {
    busy,
    error,
    notice,
    run,
    setError,
    setNotice,
    clearFeedback: () => {
      setError(null)
      setNotice("")
    },
  }
}
