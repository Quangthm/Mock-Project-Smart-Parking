export const vehiclePhotoTypes = ["image/jpeg", "image/png", "image/webp"]
const maximumOriginal = 5 * 1024 * 1024
const maximumUpload = 1024 * 1024
const invalid = "The selected image is invalid or unsupported."
const optimizationFailure = "This photo could not be optimized. Choose another image."

// Browsers can decode an animated container as a single frame. Check the source
// before allowing canvas to turn that frame into an apparently valid still.
function checkContainer(bytes: Uint8Array, mime: string) {
  const view = new DataView(bytes.buffer)
  const tag = (offset: number) => String.fromCharCode(...bytes.subarray(offset, offset + 4))
  if (mime === "image/jpeg") {
    if (bytes[0] !== 0xff || bytes[1] !== 0xd8 || bytes[2] !== 0xff) throw new Error(invalid)
    return
  }
  const png = mime === "image/png"
  if (bytes.length < 12 || (png
    ? view.getUint32(0) !== 0x89504e47 || view.getUint32(4) !== 0x0d0a1a0a
    : tag(0) !== "RIFF" || tag(8) !== "WEBP")) throw new Error(invalid)

  const overhead = png ? 12 : 8
  for (let offset = png ? 8 : 12; offset + overhead <= bytes.length;) {
    const type = tag(offset + (png ? 4 : 0))
    const length = view.getUint32(offset + (png ? 0 : 4), !png)
    if (offset + overhead + length > bytes.length) throw new Error(invalid)
    if (["acTL", "fcTL", "fdAT", "ANIM", "ANMF"].includes(type) ||
        (type === "VP8X" && length > 0 && (bytes[offset + 8] & 2) !== 0)) {
      throw new Error("Choose a still JPEG, PNG or WebP image.")
    }
    if (png && type === "IEND") break
    offset += overhead + length + (png ? 0 : length % 2)
  }
}

export async function prepareVehiclePhoto(file: File, signal: AbortSignal): Promise<File> {
  if (!file.size) throw new Error("Choose a nonempty image.")
  if (!vehiclePhotoTypes.includes(file.type)) throw new Error("Choose a JPEG, PNG or WebP image.")
  if (file.size > maximumOriginal) throw new Error("Choose an image up to 5 MiB.")
  const bytes = new Uint8Array(await file.arrayBuffer())
  signal.throwIfAborted()
  checkContainer(bytes, file.type)

  const image = new Image()
  const url = URL.createObjectURL(file)
  try {
    // Check the browser's oriented intrinsic dimensions before requesting full
    // pixel decoding or allocating a canvas.
    await new Promise<void>((resolve, reject) => {
      image.onload = () => resolve()
      image.onerror = () => reject(new Error(invalid))
      image.src = url
    })
    signal.throwIfAborted()
    const { naturalWidth: width, naturalHeight: height } = image
    if (!width || !height || Math.max(width, height) > 8192 || width * height > 25_000_000)
      throw new Error(invalid)
    await image.decode().catch(() => { throw new Error(invalid) })
    signal.throwIfAborted()
    if (file.size <= maximumUpload) return file

    const canvas = document.createElement("canvas")
    try {
      const context = canvas.getContext("2d")
      if (!context) throw new Error(optimizationFailure)
      for (const [longSide, quality] of [[1920, 0.88], [1600, 0.84], [1280, 0.8], [1024, 0.76]]) {
        const scale = Math.min(1, longSide / Math.max(width, height))
        canvas.width = Math.max(1, Math.round(width * scale))
        canvas.height = Math.max(1, Math.round(height * scale))
        context.fillStyle = "#fff"
        context.fillRect(0, 0, canvas.width, canvas.height)
        context.drawImage(image, 0, 0, canvas.width, canvas.height)
        const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, "image/jpeg", quality))
        signal.throwIfAborted()
        if (!blob?.size || blob.type !== "image/jpeg") throw new Error(optimizationFailure)
        if (blob.size <= maximumUpload)
          return new File([blob], file.name.replace(/\.[^.]*$/, "") + ".jpg", { type: blob.type })
      }
    } finally {
      canvas.width = canvas.height = 0
    }
    throw new Error(optimizationFailure)
  } finally {
    URL.revokeObjectURL(url)
    image.removeAttribute("src")
  }
}
