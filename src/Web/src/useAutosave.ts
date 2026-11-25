import { useCallback, useEffect, useRef, useState } from 'react'

export type SaveState = 'idle' | 'pending' | 'saving' | 'saved' | 'error'

interface Options<T> {
  /** Sends one payload. `keepalive` is true when the page may be going away (pagehide, unmount). */
  save: (payload: T, keepalive: boolean) => Promise<unknown>
  /** Combines an older unsaved payload with a newer one (e.g. merge answer maps, or just take the newer value). */
  merge: (older: T, newer: T) => T
  delayMs?: number
}

export interface Autosave<T> {
  /** Queues a change; it is sent after the debounce delay, or right away when `immediately` is set. */
  schedule: (patch: T, immediately?: boolean) => void
  /** Sends whatever is pending and waits for every queued save. Resolves to false if the last save failed. */
  flush: () => Promise<boolean>
  state: SaveState
  error: unknown
}

/**
 * Debounced autosave with one in-flight request at a time.
 * Saves are chained on a single promise so they reach the server in order; only the latest save may update
 * the visible state (older responses are ignored). A failed payload is merged back so the next flush retries it.
 * Pending changes are flushed on unmount and on `pagehide` (with fetch keepalive).
 */
export function useAutosave<T>({ save, merge, delayMs = 600 }: Options<T>): Autosave<T> {
  const [state, setState] = useState<SaveState>('idle')
  const [error, setError] = useState<unknown>(null)
  const saveRef = useRef(save)
  const mergeRef = useRef(merge)
  const pending = useRef<T | null>(null)
  const inFlight = useRef<T | null>(null)
  const chain = useRef<Promise<boolean>>(Promise.resolve(true))
  const seq = useRef(0)
  const timer = useRef<number | null>(null)
  const alive = useRef(true)

  useEffect(() => {
    saveRef.current = save
    mergeRef.current = merge
  }, [save, merge])

  const clearTimer = () => {
    if (timer.current !== null) window.clearTimeout(timer.current)
    timer.current = null
  }

  const flushWith = useCallback((keepalive: boolean): Promise<boolean> => {
    clearTimer()
    const payload = pending.current
    if (payload === null) return chain.current
    pending.current = null
    const mine = ++seq.current
    chain.current = chain.current.then(async () => {
      inFlight.current = payload
      if (alive.current) setState('saving')
      try {
        await saveRef.current(payload, keepalive)
        if (alive.current && mine === seq.current) {
          setError(null)
          setState(pending.current === null ? 'saved' : 'pending')
        }
        return true
      } catch (e) {
        // keep the failed change so the next flush (or submit) sends it again
        pending.current = pending.current === null ? payload : mergeRef.current(payload, pending.current)
        if (alive.current && mine === seq.current) {
          setError(e)
          setState('error')
        }
        return false
      } finally {
        inFlight.current = null
      }
    })
    return chain.current
  }, [])

  const flush = useCallback(() => flushWith(false), [flushWith])

  const schedule = useCallback((patch: T, immediately = false) => {
    pending.current = pending.current === null ? patch : mergeRef.current(pending.current, patch)
    setState('pending')
    clearTimer()
    if (immediately) void flushWith(false)
    else timer.current = window.setTimeout(() => { void flushWith(false) }, delayMs)
  }, [delayMs, flushWith])

  useEffect(() => {
    alive.current = true
    // The page is going away: send the newest known state directly with keepalive instead of waiting for the queue.
    const onPageHide = () => {
      const payload = pending.current
      if (payload === null) return
      clearTimer()
      const all = inFlight.current === null ? payload : mergeRef.current(inFlight.current, payload)
      pending.current = null
      void saveRef.current(all, true).catch(() => { /* nothing more can be done while the page unloads */ })
    }
    window.addEventListener('pagehide', onPageHide)
    return () => {
      window.removeEventListener('pagehide', onPageHide)
      alive.current = false
      void flushWith(true)
    }
  }, [flushWith])

  return { schedule, flush, state, error }
}
