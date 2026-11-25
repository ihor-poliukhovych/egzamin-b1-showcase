import { apiPath } from './http'

/**
 * The single <audio> element per app. primeAudio is called synchronously from the "Start" click handler,
 * so the browser allows autoplay; the part page only attaches to the already playing element.
 */
let element: HTMLAudioElement | null = null
let currentKey: string | null = null

export function primeAudio(key: string, startAt = 0): Promise<boolean> {
  if (element && currentKey === key && !element.paused) return Promise.resolve(true)
  if (element) element.pause()

  const el = new Audio(apiPath(['audio', key]))
  el.preload = 'auto'
  if (startAt > 0) el.addEventListener('loadedmetadata', () => { el.currentTime = startAt }, { once: true })
  element = el
  currentKey = key
  return el.play().then(() => true).catch(() => false)
}

export function getAudio(key: string): HTMLAudioElement | null {
  return element && currentKey === key ? element : null
}

export function stopAudio() {
  if (element) element.pause()
  element = null
  currentKey = null
}
