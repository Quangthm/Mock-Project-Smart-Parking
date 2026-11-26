import { useEffect, useRef, useState } from "react"
import { motion, useReducedMotion } from "motion/react"
import { AuthApiError, request } from "../lib/authApi"
import { prepareVehiclePhoto, vehiclePhotoTypes } from "../lib/vehiclePhoto"
import { vehiclesApi } from "../lib/vehiclesApi"
import { UntitledIcon } from "./icon/UntitledIcon"

type UploadResult = { assetId: string; state: string; recoveryCode?: string }
type UploadIntent = { controller: AbortController; key: string; vehicleId?: string; draft: string; uploading: boolean }

function uploadError(error: unknown) {
  if (error instanceof AuthApiError) {
    if (error.status === 413) return "The image exceeds the permitted upload size."
    if (error.code === "MEDIA_INVALID_IMAGE") return "The selected image is invalid or unsupported."
    if (error.code === "MEDIA_DISABLED" || error.code === "MEDIA_UNAVAILABLE") return "Image storage is temporarily unavailable."
    return error.message
  }
  return "Could not upload the vehicle photo. Check your connection and retry."
}

export function VehicleImageInput({ vehicleId, onUploaded, disabled = false, showEmptyPreview = false }: {
  vehicleId?: string
  onUploaded: (assetId: string) => void
  disabled?: boolean
  showEmptyPreview?: boolean
}) {
  const reduceMotion = useReducedMotion()
  const [file, setFile] = useState<File | null>(null)
  const [phase, setPhase] = useState<"idle" | "preparing" | "uploading" | "pending" | "ready">("idle")
  const busy = phase === "preparing" || phase === "uploading"
  const [message, setMessage] = useState("")
  const [preview, setPreview] = useState("")
  const [failed, setFailed] = useState(false)
  const intent = useRef<UploadIntent | null>(null)
  useEffect(() => () => intent.current?.controller.abort(), [])
  useEffect(() => {
    if (!file) return
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  async function select(selected: File) {
    intent.current?.controller.abort()
    const controller = new AbortController()
    intent.current = { controller, key: crypto.randomUUID(), draft: crypto.randomUUID(), vehicleId, uploading: false }
    onUploaded("")
    setFile(null)
    setPreview("")
    setFailed(false)
    setPhase("preparing")
    setMessage(`Preparing ${selected.name} for upload…`)
    try {
      const prepared = await prepareVehiclePhoto(selected, controller.signal)
      if (controller.signal.aborted) return
      setFile(prepared)
      setMessage("Photo selected and ready to upload.")
    } catch (error) {
      if (controller.signal.aborted) return
      setFailed(true)
      setMessage(error instanceof Error && error.name === "Error" ? error.message : "This photo could not be optimized. Choose another image.")
    } finally {
      if (!controller.signal.aborted) setPhase("idle")
    }
  }

  async function upload() {
    const current = intent.current
    if (!current || !file || current.uploading || busy || phase === "ready" || disabled) return
    current.uploading = true
    setPhase("uploading")
    setMessage("Uploading vehicle photo…")
    setFailed(false)
    try {
      const body = new FormData()
      body.append("file", file)
      body.append(current.vehicleId ? "vehicleId" : "draftIntentId", current.vehicleId ?? current.draft)
      const response = await request<{ success: boolean; data: UploadResult }>(
        "/images", "POST", body, "/api/vehicles", undefined,
        { "Idempotency-Key": current.key }, { signal: current.controller.signal },
      )
      if (current.controller.signal.aborted) return
      if (!response.success || !response.data?.assetId || !["ready", "pending"].includes(response.data.state)) {
        setPhase("idle")
        setFailed(true)
        setMessage("The server did not confirm the upload. Retry this file.")
      } else if (response.data.state === "pending") {
        setPhase("pending")
        setMessage("The upload is still being checked. Wait a few minutes, then retry this file.")
      } else {
        onUploaded(response.data.assetId)
        setPhase("ready")
        setMessage("Image uploaded and ready to save.")
      }
    } catch (error) {
      if (current.controller.signal.aborted) return
      setPhase("idle")
      setFailed(true)
      setMessage(uploadError(error))
    } finally {
      current.uploading = false
    }
  }

  return <div className="min-w-0 space-y-3">
    {(showEmptyPreview || (file && preview)) && <figure>
      <div className="grid aspect-video max-h-56 place-items-center overflow-hidden rounded-xl bg-[var(--bg)]">
        {file && preview
          ? <motion.img key={preview} width={640} height={360}
              initial={reduceMotion ? false : { opacity: 0 }} animate={{ opacity: 1 }}
              transition={{ duration: reduceMotion ? 0 : 0.2, ease: [0.23, 1, 0.32, 1] }}
              className="h-full w-full min-h-0 object-contain" src={preview} alt="Selected vehicle photo" />
          : <p className="px-4 text-center text-sm text-[var(--muted)]">Your photo preview will appear here.</p>}
      </div>
      {file && preview && <figcaption className="mt-1 break-all text-sm text-[var(--muted)]">Preview · {file.name}</figcaption>}
    </figure>}
    <label className="label">
      {vehicleId ? "Choose a replacement photo" : "Choose a vehicle photo"}
      <input className="input mt-1 w-full min-w-0" name="vehiclePhoto" type="file" accept={vehiclePhotoTypes.join(",")} disabled={disabled}
        onChange={event => {
          const selected = event.currentTarget.files?.[0]
          event.currentTarget.value = ""
          if (selected) void select(selected)
      }} />
    </label>
    <p className="text-sm text-[var(--muted)]">JPEG, PNG or WebP · Still photos up to 5 MiB · Automatically optimized to 1 MiB or less before upload</p>
    <button type="button" className="btn-outline min-h-11" disabled={!file || busy || phase === "ready" || disabled} onClick={upload}>
      {phase === "preparing" ? "Optimizing…" : phase === "uploading" ? "Uploading…" : phase === "ready" ? "Photo uploaded" : (failed && file || phase === "pending") ? "Retry upload" : "Upload photo"}
    </button>
    {message && <p className={`text-sm ${failed ? "text-[var(--danger,#b42318)]" : "text-[var(--muted)]"}`} role={failed ? "alert" : "status"}>{message}</p>}
  </div>
}

export function SavedVehicleImage({ assetId, plate, compact = false }: { assetId?: string | null; plate: string; compact?: boolean }) {
  const [url, setUrl] = useState("")
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    let active = true
    let objectUrl = ""
    setUrl("")
    setFailed(false)
    if (assetId) vehiclesApi.image(assetId).then(blob => {
      if (!active) return
      objectUrl = URL.createObjectURL(blob)
      setUrl(objectUrl)
    }).catch(() => { if (active) setFailed(true) })
    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [assetId])
  return <div className="grid aspect-video place-items-center overflow-hidden rounded-xl bg-[var(--bg)]">
    {url && !failed
      ? <img src={url} width={320} height={180} alt={`Vehicle ${plate}`} className="h-full w-full object-contain" onError={() => setFailed(true)} />
      : <span className="flex items-center gap-2 p-2 text-center text-xs text-[var(--muted)]">
        {!compact && <span aria-hidden="true"><UntitledIcon name="car" size={18} /></span>}
        {!assetId ? "No saved photo" : failed ? "Photo unavailable" : "Loading photo…"}
      </span>}
  </div>
}
