import { useEffect, useRef, useState } from "react"
import { request } from "../lib/authApi"

type UploadResult = { assetId: string; state: string; recoveryCode?: string }

export function VehicleImageInput({ vehicleId, onUploaded }: {
  vehicleId?: string
  onUploaded: (assetId: string) => void
}) {
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const intent = useRef({ key: crypto.randomUUID(), draft: crypto.randomUUID() })
  const generation = useRef(0)
  useEffect(() => () => { generation.current++ }, [])

  async function upload() {
    if (!file || busy) return
    const current = generation.current
    setBusy(true)
    setMessage("")
    try {
      if (file.size === 0 || file.size > 5 * 1024 * 1024) throw new Error("Choose an image up to 5 MiB.")
      const body = new FormData()
      body.append("file", file)
      body.append(vehicleId ? "vehicleId" : "draftIntentId", vehicleId ?? intent.current.draft)
      const response = await request<{ success: boolean; data: UploadResult }>(
        "/images", "POST", body, "/api/vehicles", undefined,
        { "Idempotency-Key": intent.current.key },
      )
      if (generation.current !== current) return
      if (response.data.state !== "ready") {
        setMessage("The upload is still being checked. Wait a few minutes, then retry this file.")
        return
      }
      onUploaded(response.data.assetId)
      setMessage("Image uploaded and ready to save.")
    } catch (error) {
      if (generation.current === current) setMessage(error instanceof Error ? error.message : "Image upload failed.")
    } finally {
      if (generation.current === current) setBusy(false)
    }
  }

  return <div>
    <input className="input" type="file" accept="image/jpeg,image/png,image/webp" disabled={busy}
      aria-label="Vehicle image" onChange={event => {
        generation.current++
        setFile(event.target.files?.[0] ?? null)
        intent.current = { key: crypto.randomUUID(), draft: crypto.randomUUID() }
        onUploaded("")
        setMessage("")
      }} />
    <p>JPEG, PNG or WebP, up to 5 MiB. No animated images.</p>
    <button type="button" className="btn-outline" disabled={!file || busy} onClick={upload}>
      {busy ? "Uploading…" : "Upload image"}
    </button>
    <p role="status">{message}</p>
  </div>
}
