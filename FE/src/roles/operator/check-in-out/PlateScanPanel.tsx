import { useEffect, useRef, useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"

const aiServer = (
  import.meta.env.VITE_AI_API_BASE_URL ?? "http://localhost:5001"
).replace(/\/$/, "")
const liveAiServer = (
  import.meta.env.VITE_AI_LIVE_API_BASE_URL ?? "http://localhost:5002"
).replace(/\/$/, "")

const scanIntervalMs = 750
const framesBeforePlateIsRearmed = 5

interface RecognitionResult {
  success: boolean
  plate?: string
  confidence?: number
  vehicle_type_name?: string
  message?: string
}

interface LiveVehicleResult {
  plate?: string | null
  ocr_confidence?: number
  vehicle_type?: string
  vehicle_bbox?: number[]
}

interface LiveRecognitionResult {
  success: boolean
  message?: string
  vehicle_count?: number
  vehicles?: LiveVehicleResult[]
}

interface RecognizedPlate {
  plate: string
  confidence?: number
  vehicleType?: string
}

export function PlateScanPanel({
  onPlateRecognized,
}: {
  onPlateRecognized: (plate: string, confidence?: number) => void | Promise<void>
}) {
  const videoRef = useRef<HTMLVideoElement | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const onPlateRecognizedRef = useRef(onPlateRecognized)
  const activePlatesRef = useRef(new Map<string, number>())
  const callbackQueueRef = useRef(Promise.resolve())
  const [cameraReady, setCameraReady] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [recognized, setRecognized] = useState<RecognitionResult | null>(null)
  const [livePlates, setLivePlates] = useState<RecognizedPlate[]>([])
  const [vehiclesInFrame, setVehiclesInFrame] = useState(0)

  useEffect(() => {
    onPlateRecognizedRef.current = onPlateRecognized
  }, [onPlateRecognized])

  useEffect(() => () => {
    streamRef.current?.getTracks().forEach((track) => track.stop())
  }, [])

  useEffect(() => {
    if (!cameraReady) return

    const video = videoRef.current
    const stream = streamRef.current
    if (!video || !stream) return

    const videoElement = video
    let stopped = false
    const controller = new AbortController()
    activePlatesRef.current.clear()
    videoElement.srcObject = stream

    async function scanContinuously() {
      try {
        await videoElement.play()
        while (!stopped) {
          if (videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
            const scale = Math.min(1, 1280 / videoElement.videoWidth)
            const canvas = document.createElement("canvas")
            canvas.width = Math.round(videoElement.videoWidth * scale)
            canvas.height = Math.round(videoElement.videoHeight * scale)
            const context = canvas.getContext("2d")
            if (!context) throw new Error("Could not capture a frame from the device camera.")
            context.drawImage(videoElement, 0, 0, canvas.width, canvas.height)

            try {
              const response = await fetch(`${liveAiServer}/api/v1/stream/recognize_live`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ image: canvas.toDataURL("image/jpeg", 0.85) }),
                signal: controller.signal,
              })
              const result = await response.json() as LiveRecognitionResult
              if (!response.ok || !result.success || !Array.isArray(result.vehicles)) {
                throw new Error(result.message || `Live plate scan failed (${response.status}).`)
              }

              const currentFramePlates = new Map<string, RecognizedPlate>()
              for (const vehicle of result.vehicles) {
                const plate = vehicle.plate?.trim()
                if (!plate) continue
                const key = plate.replace(/[^a-z0-9]/gi, "").toUpperCase()
                if (!key) continue
                currentFramePlates.set(key, {
                  plate,
                  confidence: vehicle.ocr_confidence,
                  vehicleType: vehicle.vehicle_type,
                })
              }

              for (const [key, missedFrames] of activePlatesRef.current) {
                if (currentFramePlates.has(key)) {
                  activePlatesRef.current.set(key, 0)
                } else if (missedFrames + 1 >= framesBeforePlateIsRearmed) {
                  activePlatesRef.current.delete(key)
                } else {
                  activePlatesRef.current.set(key, missedFrames + 1)
                }
              }

              const newPlates: RecognizedPlate[] = []
              for (const [key, plateResult] of currentFramePlates) {
                if (!activePlatesRef.current.has(key)) {
                  activePlatesRef.current.set(key, 0)
                  newPlates.push(plateResult)
                }
              }
              setLivePlates([...currentFramePlates.values()])
              setVehiclesInFrame(result.vehicle_count ?? result.vehicles.length)
              setError("")

              for (const plateResult of newPlates) {
                callbackQueueRef.current = callbackQueueRef.current
                  .then(() => onPlateRecognizedRef.current(plateResult.plate, plateResult.confidence))
                  .catch((cause: unknown) => {
                    if (!stopped)
                      setError(cause instanceof Error ? cause.message : "Could not process the recognized plate.")
                  })
              }
            } catch (cause) {
              if (!stopped && !(cause instanceof DOMException && cause.name === "AbortError"))
                setError(cause instanceof Error ? cause.message : "Could not reach the AI recognition service.")
            }
          }

          await new Promise<void>((resolve) => window.setTimeout(resolve, scanIntervalMs))
        }
      } catch (cause) {
        if (!stopped)
          setError(cause instanceof Error ? cause.message : "Could not start continuous camera scanning.")
      }
    }

    void scanContinuously()
    return () => {
      stopped = true
      controller.abort()
      videoElement.pause()
      videoElement.srcObject = null
    }
  }, [cameraReady])

  function stopCamera() {
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
    setCameraReady(false)
    setLivePlates([])
    setVehiclesInFrame(0)
    activePlatesRef.current.clear()
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
      await onPlateRecognizedRef.current(result.plate, result.confidence)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Could not reach the AI recognition service.")
    } finally {
      setBusy(false)
    }
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
            The scanner is ready. Open the camera to continuously detect plates; new plates with at least 90% confidence are checked in automatically.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {!cameraReady ? (
            <button type="button" className="btn-outline text-xs" onClick={() => void startCamera()} disabled={busy}>
              <UntitledIcon name="camera" size={14} /> Open camera
            </button>
          ) : (
            <button type="button" className="btn-outline text-xs" onClick={stopCamera}>
              Close camera
            </button>
          )}
          <label className={`btn-outline cursor-pointer text-xs ${busy || cameraReady ? "pointer-events-none opacity-50" : ""}`}>
            <UntitledIcon name="upload" size={14} /> Upload image
            <input
              type="file"
              accept="image/*"
              className="sr-only"
              onChange={uploadImage}
              disabled={busy || cameraReady}
            />
          </label>
        </div>
      </div>

      {cameraReady ? (
        <video
          ref={videoRef}
          autoPlay
          muted
          playsInline
          className="mt-4 max-h-72 w-full rounded-lg bg-black object-contain"
        />
      ) : (
        <div className="mt-4 flex min-h-40 flex-col items-center justify-center rounded-lg border border-dashed border-[var(--border)] bg-[var(--card)] p-6 text-center">
          <UntitledIcon name="camera" size={24} />
          <p className="mt-2 text-sm font-semibold text-[var(--fg)]">Camera is off</p>
          <p className="mt-1 text-xs text-[var(--muted)]">The check-in/out screen stays ready here. Press Open camera to start live scanning.</p>
        </div>
      )}

      {cameraReady && (
        <p role="status" className="mt-3 text-xs font-medium text-[var(--muted)]">
          Continuously scanning · {vehiclesInFrame} vehicle{vehiclesInFrame === 1 ? "" : "s"} in frame
        </p>
      )}
      {livePlates.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          {livePlates.map((item) => (
            <span key={`${item.plate}-${item.vehicleType ?? ""}`} className="rounded-lg border border-[var(--border)] px-3 py-1.5 text-xs">
              <span className="font-mono font-bold text-[var(--fg)]">{item.plate}</span>
              {typeof item.confidence === "number" && (
                <span className="ml-2 text-[var(--muted)]">{Math.round(item.confidence * 100)}%</span>
              )}
              {item.vehicleType && <span className="ml-2 text-[var(--muted)]">{item.vehicleType}</span>}
            </span>
          ))}
        </div>
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
