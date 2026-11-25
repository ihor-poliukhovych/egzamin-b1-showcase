import { useEffect, useMemo, useRef, useState } from 'react'
import { formatDuration } from '../format'
import { useI18n } from '../i18n'

const secondsLeft = (deadline: number) => Math.round((deadline - Date.now()) / 1000)

/** Countdown to the end of a part: stops at zero (never negative) and calls onExpire exactly once. */
export function Timer({ startedAt, limitMinutes, onExpire }: { startedAt: string; limitMinutes: number; onExpire?: () => void }) {
  const { t } = useI18n()
  const deadline = useMemo(() => new Date(startedAt).getTime() + limitMinutes * 60_000, [startedAt, limitMinutes])
  const [left, setLeft] = useState(() => secondsLeft(deadline))
  const expireRef = useRef(onExpire)
  useEffect(() => { expireRef.current = onExpire }, [onExpire])
  useEffect(() => {
    let fired = false
    const tick = () => {
      const next = secondsLeft(deadline)
      setLeft(next)
      if (next <= 0 && !fired) { fired = true; expireRef.current?.() }
    }
    const id = window.setInterval(tick, 1000)
    return () => window.clearInterval(id)
  }, [deadline])
  const overtime = left <= 0
  return (
    <div className={`timer ${overtime ? 'timer--over' : left < 5 * 60 ? 'timer--warn' : ''}`} title={t('run.timerTitle')}>
      <span className="timer__value">{formatDuration(Math.max(0, left))}</span>
      <span className="muted small">{overtime ? t('run.timeUp') : t('run.timeOf', { minutes: limitMinutes })}</span>
    </div>
  )
}
