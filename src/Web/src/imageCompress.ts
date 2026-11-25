/** In-browser image compression before upload: long side <= 1600 px, JPEG; ~150–400 KB instead of megabytes of screenshot. */
const MAX_SIDE = 1600
const MAX_BYTES = 1_400_000

export async function compressImage(file: Blob): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const k = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * k)
  canvas.height = Math.round(bitmap.height * k)
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('canvas')
  // transparent PNG → white background, otherwise it turns black in JPEG
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  for (const quality of [0.85, 0.7, 0.55]) {
    const url = canvas.toDataURL('image/jpeg', quality)
    if (url.length * 0.75 <= MAX_BYTES) return url
  }
  throw new Error('too large')
}
