import { useEffect, useMemo, useState } from 'react'
import { LevelTiles, VariantSwitch } from '../components/LevelPicker'
import { LEVELS } from '../levels'
import { SetCard, SetDialog } from '../components/ExamSets'
import { api, type Me, type ArkuszSummary, type ExamAttemptSummary, type ExamSessionInfo, type PartKind } from '../api'
import { buildSets, levelStats, lookupFromAttempts } from '../examSets'
import { formatDateTime, formatPoints } from '../format'
import { links, navigate } from '../router'
import { PART_LABEL, VERDICT_KEY } from '../parts'
import { useI18n } from '../i18n'
import type { Key } from '../i18n/core'
import { Loader } from '../components/Loader'
import { primeAudio } from '../audioController'

type SetFilter = 'all' | 'started' | 'new'
const FILTERS: { id: SetFilter; label: Key }[] = [
  { id: 'all', label: 'examList.filterAll' },
  { id: 'started', label: 'examList.filterStarted' },
  { id: 'new', label: 'examList.filterNew' },
]

export default function ExamListPage({ me, onMe }: { me: Me; onMe: (m: Me) => void }) {
  const { t, errorText } = useI18n()
  const [arkusze, setArkusze] = useState<ArkuszSummary[] | null>(null)
  const [attempts, setAttempts] = useState<ExamAttemptSummary[] | null>(null)
  const [sessions, setSessions] = useState<ExamSessionInfo[]>([])
  const [error, setError] = useState<string | null>(null)
  const [starting, setStarting] = useState<string | null>(null)
  const [openCode, setOpenCode] = useState<string | null>(null)
  const [filter, setFilter] = useState<SetFilter>('all')

  useEffect(() => {
    Promise.all([api.arkusze(), api.examAttempts(), api.sessions()])
      .then(([a, t, s]) => { setArkusze(a); setAttempts(t); setSessions(s) })
      .catch((e: unknown) => setError(errorText(e)))
  }, [errorText])

  const allSets = useMemo(() => (arkusze && attempts ? buildSets(arkusze, lookupFromAttempts(attempts)) : []), [arkusze, attempts])

  async function start(code: string, part: PartKind) {
    if (part === 'sluchanie') void primeAudio(code)
    setStarting(`${code}/${part}`)
    try {
      const attempt = await api.startExam(code, part)
      navigate(links.examRun(attempt.id))
    } catch (e) {
      setError(errorText(e))
      setStarting(null)
    }
  }

  async function startSession(code: string) {
    setStarting(`${code}/session`)
    try {
      const session = await api.startSession(code)
      navigate(links.session(session.id))
    } catch (e) {
      setError(errorText(e))
      setStarting(null)
    }
  }

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!arkusze || !attempts) return <Loader label={t('examList.loading')} />

  const stats = levelStats(allSets, me.targetVariant, LEVELS)
  const ofLevel = allSets.filter((s) => s.arkusz.level === me.targetLevel && s.arkusz.variant === me.targetVariant)
  const startedCount = ofLevel.filter((s) => s.status !== 'new').length
  // the filter is only needed when there are both started and untouched sets
  const withFilter = startedCount > 0 && startedCount < ofLevel.length
  const shown = !withFilter || filter === 'all' ? ofLevel : ofLevel.filter((s) => (s.status !== 'new') === (filter === 'started'))
  const openSet = allSets.find((s) => s.arkusz.code === openCode) ?? null
  const inProgress = attempts.filter((a) => !a.submittedAt)
  const finished = attempts.filter((a) => a.submittedAt)
  const variantName = t(me.targetVariant === 'dim' ? 'variant.youthLower' : 'variant.adultsLower')

  return (
    <>
      <header className="header">
        <h1>{t('examList.title')}</h1>
        <p className="muted">{t('examList.lead')}</p>
      </header>

      <section className="picker" aria-label={t('examList.pickerLabel')}>
        <LevelTiles me={me} onChange={onMe} stats={stats} />
        <VariantSwitch me={me} onChange={onMe} />
      </section>

      <section className="section">
        <h2>{me.targetLevel} · {variantName}</h2>
        {arkusze.length === 0 && <p className="muted">{t('examList.seedHint')} <code>dotnet run --project src/Api --launch-profile sqlite -- seed-arkusze</code></p>}
        {withFilter && (
          <div className="segmented set-filter" role="group" aria-label={t('examList.filter')}>
            {FILTERS.map((f) => (
              <button key={f.id} type="button" aria-pressed={filter === f.id} className={`segmented__btn ${filter === f.id ? 'segmented__btn--on' : ''}`} onClick={() => setFilter(f.id)}>
                {t(f.label, { count: f.id === 'all' ? ofLevel.length : f.id === 'started' ? startedCount : ofLevel.length - startedCount })}
              </button>
            ))}
          </div>
        )}
        {arkusze.length > 0 && ofLevel.length === 0 && (
          <p className="note">{t('examList.noneForLevel', { level: me.targetLevel, variant: variantName })}</p>
        )}
        <ul className="set-grid">
          {shown.map((s) => <SetCard key={s.arkusz.id} set={s} onOpen={() => setOpenCode(s.arkusz.code)} />)}
        </ul>
      </section>

      <SetDialog set={openSet} starting={starting} onClose={() => setOpenCode(null)} onStartSession={startSession} onStartPart={start} />

      {sessions.length > 0 && (
        <section className="section">
          <h2>{t('examList.sessions')}</h2>
          <ul className="list">
            {sessions.map((s) => (
              <li key={s.id} className="card card--row">
                <div>
                  <span className="badge">{s.arkuszCode}</span> {s.arkuszTitle}
                  <p className="muted small">{t('examList.startedVerdict', { date: formatDateTime(s.startedAt), verdict: t(VERDICT_KEY[s.verdict]) })}</p>
                </div>
                <a className="btn" href={links.session(s.id)}>{t('examList.open')}</a>
              </li>
            ))}
          </ul>
        </section>
      )}

      {inProgress.length > 0 && (
        <section className="section">
          <h2>{t('examList.unfinished')}</h2>
          <ul className="list">
            {inProgress.map((a) => (
              <li key={a.id} className="card card--row">
                <div>
                  <span className="badge">{a.arkuszCode}</span> {a.arkuszTitle} · {PART_LABEL[a.part]}
                  <p className="muted small">{t('examList.startedAnswered', { date: formatDateTime(a.startedAt), answered: a.answered, total: a.total })}</p>
                </div>
                <a className="btn" href={links.examRun(a.id)}>{t('dialog.continue')}</a>
              </li>
            ))}
          </ul>
        </section>
      )}

      {finished.length > 0 && (
        <section className="section">
          <h2>{t('examList.finished')}</h2>
          <ul className="list">
            {finished.map((a) => (
              <li key={a.id} className={`card card--row ${a.points / a.maxPoints >= 0.5 ? 'card--pass' : 'card--fail'}`}>
                <div>
                  <span className="badge">{a.arkuszCode}</span> {a.arkuszTitle} · {PART_LABEL[a.part]}
                  <p className="muted small">{t('examList.submittedScore', { date: formatDateTime(a.submittedAt!), points: formatPoints(a.points), max: formatPoints(a.maxPoints) })}</p>
                </div>
                <a className="btn" href={links.examResult(a.id)}>{t('dialog.review')}</a>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  )
}
