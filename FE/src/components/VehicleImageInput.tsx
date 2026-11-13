import { useEffect, useRef, useState } from "react"
import { motion, useReducedMotion } from "motion/react"
import { request } from "../lib/authApi"
import { vehiclesApi } from "../lib/vehiclesApi"
import { UntitledIcon } from "./icon/UntitledIcon"

type UploadResult = { assetId: string; state: string; recoveryCode?: string }
const imageTypes = ["image/jpeg", "image/png", "image/webp"]

export function VehicleImageInput({ vehicleId, onUploaded, disabled = false, showEmptyPreview = false }: {
  vehicleId?: string
  onUploaded: (assetId: string) => void
  disabled?: boolean
  showEmptyPreview?: boolean
}) {
  const reduceMotion = useReducedMotion()
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState("")
  const [preview, setPreview] = useState("")
  const [ready, setReady] = useState(false)
  const [failed, setFailed] = useState(false)
  const intent = useRef({ key: crypto.randomUUID(), draft: crypto.randomUUID() })
  const generation = useRef(0)
  const uploading = useRef(false)
  useEffect(() => () => { generation.current++ }, [])
  useEffect(() => {
    if (!file) return
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  async function upload() {
    if (!file || uploading.current || busy || ready || disabled) return
    uploading.current = true
    const current = generation.current
    setBusy(true)
    setMessage("")
    setFailed(false)
    try {
      const body = new FormData()
      body.append("file", file)
      body.append(vehicleId ? "vehicleId" : "draftIntentId", vehicleId ?? intent.current.draft)
      const response = await request<{ success: boolean; data: UploadResult }>(
        "/images", "POST", body, "/api/vehicles", undefined,
        { "Idempotency-Key": intent.current.key },
      )
      if (generation.current !== current) return
      if (!response.success || !response.data?.assetId) throw new Error("The server did not confirm the upload.")
      if (response.data.state !== "ready") {
        setMessage("The upload is still being checked. Wait a few minutes, then retry this file.")
        return
      }
      onUploaded(response.data.assetId)
      setReady(true)
      setMessage("Image uploaded and ready to save.")
    } catch (error) {
      if (generation.current === current) {
        setFailed(true)
        setMessage(error instanceof Error ? error.message : "Image upload failed.")
      }
    } finally {
      uploading.current = false
      if (generation.current === current) setBusy(false)
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
      <input className="input mt-1 w-full min-w-0" name="vehiclePhoto" type="file" accept={imageTypes.join(",")} disabled={busy || disabled}
        onChange={event => {
        const selected = event.currentTarget.files?.[0]
        event.currentTarget.value = ""
        if (!selected) return
        generation.current++
        setPreview("")
        setReady(false)
        setFailed(false)
        setMessage("")
        intent.current = { key: crypto.randomUUID(), draft: crypto.randomUUID() }
        onUploaded("")
        const error = !imageTypes.includes(selected.type) ? "Choose a JPEG, PNG or WebP image."
          : selected.size === 0 || selected.size > 5_242_880 ? "Choose a non-empty image up to 5 MiB." : ""
        if (error) event.currentTarget.value = ""
        setFile(error ? null : selected)
        setFailed(Boolean(error))
        setMessage(error)
      }} />
    </label>
    <p className="text-sm text-[var(--muted)]">JPEG, PNG or WebP · Up to 5 MiB · Still images</p>
    <button type="button" className="btn-outline min-h-11" disabled={!file || busy || ready || disabled} onClick={upload}>
      {busy ? "Uploading…" : ready ? "Photo uploaded" : failed && file ? "Retry upload" : "Upload photo"}
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
