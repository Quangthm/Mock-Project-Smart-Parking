import { useEffect, useRef, useState } from "react"
import { uploadVehicleImage, validateVehicleImage, vehicleImageTypes } from "../../lib/vehiclesApi"

type UploadState =
  | { status: "idle" | "uploading" }
  | { status: "ready"; file: File }
  | { status: "failed"; message: string; retry?: { file: File; key: string } }

export function VehicleImageUpload({ onReady, editing = false, disabled = false }: {
  onReady: (assetId: string | null) => void
  editing?: boolean
  disabled?: boolean
}) {
  const [upload, setUpload] = useState<UploadState>({ status: "idle" })
  const [preview, setPreview] = useState("")
  const generation = useRef(0)
  const readyFile = upload.status === "ready" ? upload.file : undefined

  useEffect(() => () => { generation.current++ }, [])
  useEffect(() => {
    if (!readyFile) {
      setPreview("")
      return
    }
    const url = URL.createObjectURL(readyFile)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [readyFile])

  async function start(file: File, key: string) {
    const attempt = ++generation.current
    onReady(null)
    const error = validateVehicleImage(file)
    if (error) {
      setUpload({ status: "failed", message: error })
      return
    }
    setUpload({ status: "uploading" })
    try {
      const result = await uploadVehicleImage(file, key)
      if (attempt !== generation.current) return
      setUpload({ status: "ready", file })
      onReady(result.assetId)
    } catch (error) {
      if (attempt !== generation.current) return
      setUpload({
        status: "failed",
        message: error instanceof Error ? error.message : "Image upload failed.",
        retry: { file, key },
      })
    }
  }

  return <div className="col-span-full min-w-0 space-y-2">
    <label className="label">
      {editing ? "Replace vehicle photo (optional)" : "Vehicle photo"}
      <input className="input mt-1 w-full min-w-0" type="file" accept={vehicleImageTypes}
        disabled={disabled} onChange={event => {
          const file = event.currentTarget.files?.[0]
          event.currentTarget.value = ""
          if (file) void start(file, crypto.randomUUID())
        }} />
    </label>
    <p className="text-sm text-[var(--muted)]">JPEG, PNG or WebP · Up to 5 MiB</p>
    {upload.status === "uploading" && <p role="status"><span aria-hidden="true" className="mr-2 inline-block size-4 animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none" />Uploading photo…</p>}
    {upload.status === "ready" && <div role="status">
      Photo ready
      {preview && <img className="mt-2 max-h-40 max-w-full rounded-lg object-contain" src={preview} alt="Selected vehicle photo" />}
    </div>}
    {upload.status === "failed" && <div className="space-y-2">
      <p role="alert">{upload.message}</p>
      {upload.retry && <button type="button" className="btn-outline" onClick={() => {
        const retry = upload.retry
        if (retry) void start(retry.file, retry.key)
      }}>Retry upload</button>}
    </div>}
  </div>
}
