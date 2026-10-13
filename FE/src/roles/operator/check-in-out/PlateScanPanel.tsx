import { useEffect, useRef, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

const aiServer = (
  import.meta.env.VITE_AI_API_BASE_URL ?? "http://localhost:5001"
).replace(/\/$/, "")

interface RecognitionResult {
  success: boolean
  plate?: string
  confidence?: number
  vehicle_type_name?: string
  message?: string
}

export function PlateScanPanel({
  onPlateRecognized,
}: {
  onPlateRecognized: (plate: string, confidence?: number) => void
}) {
  const videoRef = useRef<HTMLVideoElement | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const [cameraReady, setCameraReady] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [recognized, setRecognized] = useState<RecognitionResult | null>(null)
  const [preview, setPreview] = useState("")

  useEffect(() => () => {
    streamRef.current?.getTracks().forEach((track) => track.stop())
  }, [])

  useEffect(() => {
    if (!cameraReady || !videoRef.current || !streamRef.current) return
    videoRef.current.srcObject = streamRef.current
    void videoRef.current.play()
  }, [cameraReady])

  function stopCamera() {
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
    setCameraReady(false)
  }

  async function startCamera() {
    setError("")
    if (!navigator.mediaDevices?.getUserMedia) {
      setError("Camera access is unavailable. Use image upload or enter the plate manually.")
      return
    }

    try {
      stopCamera()
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: { ideal: "environment" } },
      })
      streamRef.current = stream
      setCameraReady(true)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not open the device camera.")
    }
  }

  async function recognizeImage(image: string) {
    setBusy(true)
    setError("")
    setRecognized(null)
    setPreview(image)

    try {
      const response = await fetch(`${aiServer}/api/v1/stream/recognize_uploaded_image`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ image }),
      })
      const result = await response.json() as RecognitionResult
      if (!response.ok || !result.success || !result.plate) {
        throw new Error(result.message || `Plate recognition failed (${response.status}).`)
      }

      setRecognized(result)
      onPlateRecognized(result.plate, result.confidence)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not reach the AI recognition service.")
    } finally {
      setBusy(false)
    }
  }

  function captureFrame() {
    const video = videoRef.current
    if (!video || video.videoWidth === 0 || video.videoHeight === 0) {
      setError("The camera has not produced a frame yet.")
      return
    }

    const canvas = document.createElement("canvas")
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    const context = canvas.getContext("2d")
    if (!context) {
      setError("Could not capture a frame from the camera.")
      return
    }
    context.drawImage(video, 0, 0)
    void recognizeImage(canvas.toDataURL("image/jpeg", 0.9))
  }

  function uploadImage(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ""
    if (!file) return
    if (!file.type.startsWith("image/")) {
      setError("Choose an image file.")
      return
    }
    if (file.size > 10 * 1024 * 1024) {
      setError("Image must be 10 MB or smaller.")
      return
    }

    const reader = new FileReader()
    reader.onload = () => {
      if (typeof reader.result === "string") void recognizeImage(reader.result)
      else setError("Could not read the selected image.")
    }
    reader.onerror = () => setError(reader.error?.message || "Could not read the selected image.")
    reader.readAsDataURL(file)
  }

  return (
    <section className="mt-5 rounded-xl border border-[var(--border)] bg-[var(--bg)] p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-[var(--fg)]">AI Plate Scanner</h3>
          <p className="mt-1 text-xs text-[var(--muted)]">
            Plates recognized with at least 90% confidence are checked in automatically. Lower confidence requires operator review.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {!cameraReady ? (
            <button type="button" className="btn-outline text-xs" onClick={() => void startCamera()} disabled={busy}>
              <UntitledIcon name="camera" size={14} /> Open camera
            </button>
          ) : (
            <>
              <button type="button" className="btn-primary text-xs" onClick={captureFrame} disabled={busy}>
                <UntitledIcon name="scan" size={14} /> Capture & recognize
              </button>
              <button type="button" className="btn-outline text-xs" onClick={stopCamera} disabled={busy}>
                Close camera
              </button>
            </>
          )}
          <label className={`btn-outline cursor-pointer text-xs ${busy ? "pointer-events-none opacity-50" : ""}`}>
            <UntitledIcon name="upload" size={14} /> Upload image
            <input
              type="file"
              accept="image/*"
              className="sr-only"
              onChange={uploadImage}
              disabled={busy}
            />
          </label>
        </div>
      </div>

      {cameraReady && (
        <video
          ref={videoRef}
          autoPlay
          muted
          playsInline
          className="mt-4 max-h-72 w-full rounded-lg bg-black object-contain"
        />
      )}
      {!cameraReady && preview && (
        <img src={preview} alt="Captured vehicle for plate recognition" className="mt-4 max-h-72 w-full rounded-lg bg-black object-contain" />
      )}

      {busy && (
        <p role="status" className="mt-3 text-xs font-medium text-[var(--muted)]">
          Analyzing image with the AI service…
        </p>
      )}
      {error && (
        <p role="alert" className="mt-3 text-xs font-medium text-red-600">
          {error}
        </p>
      )}
      {recognized?.plate && (
        <div role="status" className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border border-green-500/30 bg-green-500/10 p-3 text-sm">
          <span className="font-mono text-lg font-bold tracking-wider text-[var(--fg)]">{recognized.plate}</span>
          {typeof recognized.confidence === "number" && (
            <span className="text-xs text-[var(--muted)]">
              Confidence: {Math.round(Math.max(0, Math.min(1, recognized.confidence)) * 100)}%
            </span>
          )}
          {recognized.vehicle_type_name && (
            <span className="text-xs text-[var(--muted)]">{recognized.vehicle_type_name}</span>
          )}
          <span className="text-xs text-green-700">
            {typeof recognized.confidence === "number" && recognized.confidence >= 0.9
              ? "High-confidence plate sent for automatic check-in."
              : "Review this plate and complete check-in manually."}
          </span>
        </div>
      )}
    </section>
  )
}
