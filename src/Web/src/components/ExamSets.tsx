import { useEffect, useRef } from 'react'
import type { PartKind } from '../api'
import { useI18n } from '../i18n'
import type { Key } from '../i18n/core'
import { PART_SHORT, setTitle, type PartInfo, type SetInfo } from '../examSets'
import { formatPercent, formatPoints } from '../format'
import { PART_LABEL, RUNNABLE_PARTS } from '../parts'
import { links } from '../router'

const STATE_KEY: Record<PartInfo['state'], Key> = {
  missing: 'part.state.missing',
  todo: 'part.state.todo',
  active: 'part.state.active',
  passed: 'part.state.passed',
  failed: 'part.state.failed',
}
const STATE_MARK: Record<PartInfo['state'], string> = { missing: '–', todo: '', active: '…', passed: '✓', failed: '!' }
const STATUS_KEY: Record<SetInfo['status'], Key> = { new: 'set.status.new', progress: 'set.status.progress', passed: 'set.status.passed', failed: 'set.status.failed' }

/** Five "stamps" of arkusz parts: unavailable, available, passed, not passed. */
export function PartStamps({ parts }: { parts: PartInfo[] }) {
  const { t } = useI18n()
  const title = (p: PartInfo) => p.percent !== null
    ? t('part.stampTitlePct', { part: PART_LABEL[p.kind], state: t(STATE_KEY[p.state]), percent: Math.round(p.percent) })
    : t('part.stampTitle', { part: PART_LABEL[p.kind], state: t(STATE_KEY[p.state]) })
  return (
    <span className="stamps" role="list">
      {parts.map((p) => (
        <span key={p.kind} role="listitem" className={`stamp stamp--${p.state}`} title={title(p)} aria-label={title(p)}>
          <span className="stamp__name">{PART_SHORT[p.kind].replace('.', '')}</span>
          {p.state === 'failed' && p.percent !== null
            ? <span className="stamp__mark stamp__mark--pct" aria-hidden="true">{Math.round(p.percent)}%</span>
            : <span className="stamp__mark" aria-hidden="true">{STATE_MARK[p.state]}</span>}
        </span>
      ))}
    </span>
  )
}

function useStatusLine() {
  const { t } = useI18n()
  return (s: SetInfo) => {
    if (s.status === 'failed') return t('set.note.failed', { parts: s.failedParts.map((f) => `${PART_SHORT[f.kind].replace('.', '')} ${Math.round(f.percent)} %`).join(', ') })
    if (s.status === 'progress') return t('set.note.progress', { done: s.done, total: s.available })
    if (s.status === 'passed') return s.arkusz.fullExam ? t('set.note.passedFull') : t('set.note.passedPartial', { available: s.available })
    return s.arkusz.fullExam ? t('set.note.newFull') : t('set.note.newPartial', { available: s.available })
  }
}

export function SetCard({ set, onOpen }: { set: SetInfo; onOpen: () => void }) {
  const { t } = useI18n()
  const statusLine = useStatusLine()
  return (
    <li>
      <button type="button" className={`set set--${set.status}`} onClick={onOpen}>
        <span className="set__top">
          <span className="set__title">{setTitle(set, t)}</span>
          <span className={`set__status set__status--${set.status}`}>{set.status === 'passed' ? '✓ ' : ''}{t(STATUS_KEY[set.status])}</span>
        </span>
        <span className="set__sub">
          {set.sample && <span className="tag">{t('set.sample')}</span>}
          <span className="tag tag--quiet">{set.arkusz.fullExam ? t('set.tag.full') : t('set.tag.parts')}</span>
        </span>
        <PartStamps parts={set.parts} />
        <span className="set__note">{statusLine(set)}</span>
      </button>
    </li>
  )
}

interface DialogProps {
  set: SetInfo | null
  starting: string | null
  onClose: () => void
  onStartSession: (code: string) => void
  onStartPart: (code: string, part: PartKind) => void
}

/** Set window: the full mock exam (if the set is complete) and buttons for individual parts. */
export function SetDialog({ set, starting, onClose, onStartSession, onStartPart }: DialogProps) {
  const { t } = useI18n()
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const dlg = ref.current
    if (!dlg) return
    if (set && !dlg.open) dlg.showModal()
    if (!set && dlg.open) dlg.close()
  }, [set])

  return (
    <dialog ref={ref} className="set-dialog" onClose={onClose} onClick={(e) => { if (e.target === ref.current) onClose() }}>
      {set && (
        <div className="set-dialog__body">
          <header className="set-dialog__head">
            <div>
              <h2 className="set-dialog__title">{setTitle(set, t)}</h2>
              <p className="muted small">{set.arkusz.level} · {t(set.arkusz.variant === 'dim' ? 'variant.youthLower' : 'variant.adultsLower')}{set.sample ? ` · ${t('set.sample').toLowerCase()}` : ''} · <span className={`set__status set__status--${set.status}`}>{t(STATUS_KEY[set.status])}</span></p>
            </div>
            <button type="button" className="btn" onClick={onClose} aria-label={t('common.close')}>✕</button>
          </header>

          {set.arkusz.fullExam ? (
            <button type="button" className="btn btn--primary set-dialog__full" disabled={starting !== null} onClick={() => onStartSession(set.arkusz.code)}>
              {starting === `${set.arkusz.code}/session` ? t('dialog.creating') : t('dialog.fullExam')}
            </button>
          ) : (
            <p className="note">{t('dialog.notFull')}</p>
          )}

          <ul className="part-rows">
            {set.parts.map((p) => {
              const runnable = p.state !== 'missing' && RUNNABLE_PARTS.includes(p.kind)
              const key = `${set.arkusz.code}/${p.kind}`
              const meta = t('dialog.partMeta', { minutes: p.minutes, points: formatPoints(p.maxPoints) })
              // part already submitted: two actions in one row — "Review" and "Again" (an open retake continues silently)
              if (p.bestAttemptId && p.percent !== null) {
                const again = starting === key ? t('dialog.creating') : t('dialog.retry')
                return (
                  <li key={p.kind} className={`part-row part-row--${p.state}`}>
                    <div className="part-row__main part-row__main--static">
                      <span className="part-row__text">
                        <span className="part-row__name">{PART_LABEL[p.kind]}</span>
                        <span className="muted small">{meta}</span>
                      </span>
                      <span className={`part-row__score ${p.state === 'failed' ? 'part-row__score--fail' : 'part-row__score--pass'}`}>{formatPercent(Math.round(p.percent))}</span>
                      <span className="part-row__actions">
                        <a className="btn btn--sm btn--plain" href={links.examResult(p.bestAttemptId)}>{t('dialog.review')}</a>
                        {runnable && (p.activeAttemptId
                          ? <a className="btn btn--sm" href={links.examRun(p.activeAttemptId)}>{again}</a>
                          : <button type="button" className="btn btn--sm" disabled={starting !== null} onClick={() => onStartPart(set.arkusz.code, p.kind)}>{again}</button>)}
                      </span>
                    </div>
                  </li>
                )
              }
              // one row = one action: continue an open attempt, otherwise start (or retake)
              const action = starting === key ? t('dialog.creating') : p.activeAttemptId ? t('dialog.continue') : p.state === 'todo' ? t('dialog.start') : t('dialog.retry')
              const body = (
                <>
                  <span className="part-row__text">
                    <span className="part-row__name">{PART_LABEL[p.kind]}</span>
                    <span className="muted small">{p.state === 'missing' ? t('part.state.missing') : meta}</span>
                  </span>
                  {p.percent !== null && (
                    <span className={`part-row__score ${p.state === 'failed' ? 'part-row__score--fail' : 'part-row__score--pass'}`}>{formatPercent(Math.round(p.percent))}</span>
                  )}
                  {runnable && <span className="part-row__go">{action} ›</span>}
                </>
              )
              return (
                <li key={p.kind} className={`part-row part-row--${p.state}`}>
                  {!runnable ? <div className="part-row__main part-row__main--static">{body}</div>
                    : p.activeAttemptId ? <a className="part-row__main" href={links.examRun(p.activeAttemptId)}>{body}</a>
                    : <button type="button" className="part-row__main" disabled={starting !== null} onClick={() => onStartPart(set.arkusz.code, p.kind)}>{body}</button>}
                </li>
              )
            })}
          </ul>
        </div>
      )}
    </dialog>
  )
}
