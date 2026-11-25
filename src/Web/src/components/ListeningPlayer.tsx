import { useEffect, useRef, useState } from 'react'
import { useI18n } from '../i18n'
import { api } from '../api'
import { formatDuration } from '../format'
import { getAudio, primeAudio, stopAudio } from '../audioController'

const SAVE_EVERY_MS = 5000

/** Player as in the exam: no pause or seeking, position stored in the attempt. */
export default function ListeningPlayer({ attemptId, audioKey, savedPosition }: { attemptId: string; audioKey: string; savedPosition: number }) {
  const { t } = useI18n()
  const [time, setTime] = useState(0)
  const [duration, setDuration] = useState(0)
  const [blocked, setBlocked] = useState(false)
  const [ended, setEnded] = useState(false)
  const elRef = useRef<HTMLAudioElement | null>(null)

  useEffect(() => {
    let cancelled = false
    const attach = (el: HTMLAudioElement) => {
      elRef.current = el
      if (savedPosition > el.currentTime + 2) el.currentTime = savedPosition
      const onTime = () => setTime(el.currentTime)
      const onMeta = () => setDuration(el.duration)
      const onEnd = () => setEnded(true)
      // There is no pause in the exam: a manual pause is undone. But after stopAudio() (part submitted, exit) the element is no longer current —
      // so we do not restart it, otherwise the recording plays after submission.
      const onPause = () => { if (!el.ended && !cancelled && getAudio(audioKey) === el) void el.play().catch(() => setBlocked(true)) }
      el.addEventListener('timeupdate', onTime)
      el.addEventListener('loadedmetadata', onMeta)
      el.addEventListener('ended', onEnd)
      el.addEventListener('pause', onPause)
      if (el.duration) setDuration(el.duration)
      if (el.paused && !el.ended) setBlocked(true)
      return () => {
        el.removeEventListener('timeupdate', onTime)
        el.removeEventListener('loadedmetadata', onMeta)
        el.removeEventListener('ended', onEnd)
        el.removeEventListener('pause', onPause)
      }
    }

    let detach: (() => void) | null = null
    const existing = getAudio(audioKey)
    if (existing) {
      detach = attach(existing)
    } else {
      void primeAudio(audioKey, savedPosition).then((ok) => {
        const el = getAudio(audioKey)
        if (el && !cancelled) { detach = attach(el); if (!ok) setBlocked(true) }
      })
    }

    const timer = window.setInterval(() => {
      const el = elRef.current
      if (el && !el.ended) void api.saveAudioPosition(attemptId, el.currentTime).catch(() => undefined)
    }, SAVE_EVERY_MS)

    return () => {
      cancelled = true
      window.clearInterval(timer)
      const el = elRef.current
      if (el) void api.saveAudioPosition(attemptId, el.currentTime).catch(() => undefined)
      detach?.()
      // Left the part page — the recording must go silent; the position is already saved in the attempt.
      stopAudio()
    }
  }, [attemptId, audioKey, savedPosition])

  function enableSound() {
    void primeAudio(audioKey, Math.max(savedPosition, elRef.current?.currentTime ?? 0)).then((ok) => {
      const el = getAudio(audioKey)
      if (el) elRef.current = el
      setBlocked(!ok)
    })
  }

  const percent = duration > 0 ? Math.min(100, (time / duration) * 100) : 0
  return (
    <div className="audio">
      <div className="audio__bar">
        <span>🔊 {t('player.title')}</span>
        <span className="muted small">{ended ? t('player.ended') : `${formatDuration(Math.floor(time))} / ${duration ? formatDuration(Math.floor(duration)) : '--:--'}`}</span>
        {blocked && !ended && <button className="btn btn--primary" onClick={enableSound}>{t('player.enableSound')}</button>}
      </div>
      <div className="audio__progress" aria-hidden="true"><div className="audio__progress-fill" style={{ width: `${percent}%` }} /></div>
      <p className="muted small">{t('player.note')}</p>
    </div>
  )
}
