import { useCallback, useEffect, useState } from 'react'
import { api, type ExamSessionInfo, type PartKind, type PartResult, type ReusablePart } from '../api'
import { formatDate, formatDateTime, formatPercent, formatPoints } from '../format'
import { PART_LABEL, VERDICT_KEY } from '../parts'
import { useI18n } from '../i18n'
import { links, navigate } from '../router'
import { BackLink, BackRow } from '../components/BackLink'
import { Loader } from '../components/Loader'
import { primeAudio } from '../audioController'

type StepState = 'done' | 'current' | 'todo' | 'missing'

export default function SessionPage({ sessionId }: { sessionId: string }) {
  const { t, errorText } = useI18n()
  const [session, setSession] = useState<ExamSessionInfo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const [reusable, setReusable] = useState<ReusablePart[]>([])

  const load = useCallback(() => {
    api.session(sessionId).then(setSession).catch((e: unknown) => setError(errorText(e)))
    api.reusableParts(sessionId).then(setReusable).catch(() => setReusable([]))
  }, [sessionId, errorText])
  useEffect(load, [load])

  // Count already submitted individual parts of this arkusz instead of retaking them
  async function adopt(items: ReusablePart[]) {
    setBusy(true)
    try {
      for (const r of items) await api.adoptPart(sessionId, r.part, r.attemptId)
      load()
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  async function startPart(kind: PartKind) {
    if (kind === 'sluchanie' && session) void primeAudio(session.arkuszCode)
    setBusy(true)
    try {
      const attempt = await api.startSessionPart(sessionId, kind)
      navigate(links.examRun(attempt.id))
    } catch (e) {
      setError(errorText(e))
      setBusy(false)
    }
  }

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!session) return <Loader label={t('session.loading')} />

  const next = session.nextPart
  const done = session.parts.filter((p) => p.status === 'submitted').length
  const finished = next === null
  const stateOf = (p: PartResult): StepState =>
    p.status === 'submitted' ? 'done' : p.kind === next ? 'current' : p.status === 'notAvailable' ? 'missing' : 'todo'

  return (
    <>
      <header className="header">
        <h1>{t('session.title', { code: session.arkuszCode })}</h1>
        <p className="muted small">{t('session.meta', { title: session.arkuszTitle, date: formatDateTime(session.startedAt) })}</p>
      </header>

      {finished
        ? <SessionTotal session={session} />
        : <p className="session-progress">{t('session.progress', { done, total: session.parts.length, threshold: formatPercent(session.passThresholdPercent) })}</p>}

      {!finished && reusable.length > 0 && (
        <section className="reuse-box">
          <div className="reuse-box__head">
            <strong>{t('session.reuseTitle')}</strong>
            {reusable.length > 1 && <button className="btn btn--sm" disabled={busy} onClick={() => void adopt(reusable)}>{t('session.reuseAll')}</button>}
          </div>
          <p className="muted small">{t('session.reuseHint')}</p>
          <ul className="reuse-box__list">
            {reusable.map((r) => (
              <li key={r.part}>
                <span>{t('session.reuseItem', { part: PART_LABEL[r.part], points: formatPoints(r.points), max: formatPoints(r.maxPoints), date: formatDate(r.submittedAt) })}</span>
                <button className="btn btn--sm btn--plain" disabled={busy} onClick={() => void adopt([r])}>{t('session.reuseAdopt')}</button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <ol className="exam-steps">
        {session.parts.map((p, i) => (
          <Step key={p.kind} part={p} n={i + 1} state={stateOf(p)} busy={busy} onStart={() => void startPart(p.kind)} />
        ))}
      </ol>

      <BackRow><BackLink href={links.examList}>{t('session.toList')}</BackLink></BackRow>
    </>
  )
}

function Step({ part, n, state, busy, onStart }: { part: PartResult; n: number; state: StepState; busy: boolean; onStart: () => void }) {
  const { t } = useI18n()
  const meta = part.timeLimitMinutes > 0
    ? t('session.stepMeta', { minutes: part.timeLimitMinutes, max: formatPoints(part.maxPoints) })
    : t('session.stepMax', { max: formatPoints(part.maxPoints) })
  return (
    <li className={`exam-step exam-step--${state}${part.isBelowThreshold ? ' exam-step--low' : ''}`} aria-current={state === 'current' ? 'step' : undefined}>
      <span className="exam-step__n" aria-hidden="true">{state === 'done' && !part.isBelowThreshold ? '✓' : n}</span>
      <div className="exam-step__body">
        <div className="exam-step__head">
          <span className="exam-step__name">{PART_LABEL[part.kind]}</span>
          {state === 'done' && part.percent !== null && (
            <span className={`part-row__score ${part.isBelowThreshold ? 'part-row__score--fail' : 'part-row__score--pass'}`}>{formatPercent(part.percent)}</span>
          )}
        </div>
        {state === 'done' && part.points !== null ? (
          <p className="exam-step__meta">
            {t('session.stepPoints', { points: formatPoints(part.points), max: formatPoints(part.maxPoints) })}
            {part.isBelowThreshold && <span className="exam-step__low"> · {t('session.belowThreshold')}</span>}
            {part.attemptId && <> · <a href={links.examResult(part.attemptId)}>{t('session.review')}</a></>}
          </p>
        ) : (
          <p className="exam-step__meta">
            {meta}
            {state === 'missing' && <> · {t('session.notAvailable')}</>}
            {state === 'todo' && part.status === 'inProgress' && <> · {t('session.inProgress')}</>}
          </p>
        )}
        {state === 'current' && (
          <button className="btn btn--primary exam-step__go" disabled={busy} onClick={onStart}>
            {part.status === 'inProgress' ? t('session.resume') : t('session.begin')}
          </button>
        )}
      </div>
    </li>
  )
}

/** Result after all parts are submitted: verdict, total score and which parts are below the threshold. */
function SessionTotal({ session }: { session: ExamSessionInfo }) {
  const { t } = useI18n()
  const points = session.parts.reduce((sum, p) => sum + (p.points ?? 0), 0)
  const max = session.parts.reduce((sum, p) => sum + p.maxPoints, 0)
  const percent = max > 0 ? Math.round((points / max) * 1000) / 10 : 0
  const low = session.parts.filter((p) => p.isBelowThreshold)
  const threshold = formatPercent(session.passThresholdPercent)
  const verdict = session.verdict
  return (
    <section className={`session-total session-total--${verdict}`}>
      <div className="session-total__verdict">
        {verdict === 'passed' ? t('session.passed') : verdict === 'failed' ? t('session.failed') : t(VERDICT_KEY[verdict])}
      </div>
      <div className="session-total__score">
        <span className="session-total__points">{formatPoints(points)}</span>
        <span className="session-total__of">{t('session.totalOf', { max: formatPoints(max) })}</span>
        <span className="session-total__percent">{formatPercent(percent)}</span>
      </div>
      {low.length > 0 ? (
        <p className="session-total__note">
          {t('session.lowParts', { threshold })}{' '}
          {low.map((p, i) => (
            <span key={p.kind}>{i > 0 && ', '}<strong>{PART_LABEL[p.kind]}</strong> <span className="nowrap">({formatPercent(p.percent ?? 0)})</span></span>
          ))}
        </p>
      ) : (
        <p className="session-total__note">{t(verdict === 'passed' ? 'session.allAbove' : 'session.thresholdRule', { threshold })}</p>
      )}
      <div className="actions">
        <a className="btn btn--primary" href={links.home}>{t('session.toCabinet')}</a>
        <a className="btn" href={links.examList}>{t('session.another')}</a>
      </div>
    </section>
  )
}
