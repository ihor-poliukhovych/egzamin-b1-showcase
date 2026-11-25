// Photos for "Opis ilustracji". The exam board does not publish the photos of past Mówienie sessions, so photos from
// its B1 task collection (Mówienie) are used. They are served by the API under /media/zdjecia (not bundled) and are
// spread over zestawy deterministically: the three zestawy of one arkusz get three different photos.
const PHOTOS = ['/media/zdjecia/m1.jpg', '/media/zdjecia/m2.jpg', '/media/zdjecia/m3.jpg', '/media/zdjecia/m4.jpg', '/media/zdjecia/m5.jpg', '/media/zdjecia/m6.jpg', '/media/zdjecia/m7.jpg']

export function photoFor(arkuszCode: string, setNumber: number): string {
  const base = [...arkuszCode].reduce((sum, ch) => sum + ch.charCodeAt(0), 0)
  return PHOTOS[(base + setNumber) % PHOTOS.length]
}

/** Strips the "[Zdjęcie niedostępne…]" placeholder from the task text: the photo is shown by the app itself. */
export function withoutPhotoPlaceholder(prompt: string): string {
  return prompt.replace(/^\s*\[Zdj[^\]]*\]\s*/, '')
}
