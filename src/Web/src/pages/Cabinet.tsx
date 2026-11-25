import { useCallback, useEffect, useState } from 'react'
import { api, type ArkuszSummary, type DashboardData, type GroupSummary, type Me, type PartKind } from '../api'
import { Patterns, ProgressBoard } from '../components/Progress'
import LevelPicker from '../components/LevelPicker'
import { formatDate, formatDateTime, formatPercent, formatPoints, todayIso } from '../format'
import { PART_LABEL, patternShort } from '../parts'
import { PART_ORDER as PARTS, PART_SHORT as SHORT } from '../examSets'
import { useI18n } from '../i18n'
import { BackLink, BackRow } from '../components/BackLink'
import { useConfirm } from '../components/confirm'
import { ReadinessGauge } from '../components/ReadinessGauge'
import { Loader } from '../components/Loader'
import { articleMeta, loadArticle, type ArticleSlug } from '../knowledge'
import { links, navigate } from '../router'


/** Dictionary keys from the server are PascalCase (Sluchanie), so they are compared case-insensitively. */
function byKind<T>(dict: Record<string, T> | undefined, kind: PartKind): T | undefined {
  if (!dict) return undefined
  const hit = Object.keys(dict).find((k) => k.toLowerCase() === kind)
  return hit ? dict[hit] : undefined
}

/** Which knowledge-base article helps with a weak part. */
const ARTICLE_FOR: Record<PartKind, ArticleSlug> = { sluchanie: 'egzamin', czytanie: 'egzamin', gramatyka: 'gramatyka', pisanie: 'pisanie', mowienie: 'mowienie' }

type Readiness = DashboardData['readiness'][number]

/** The weakest part: below the threshold with the lowest % → not yet submitted → smallest margin. */
function weakestPart(readiness: Readiness[]): { r: Readiness; kind: 'below' | 'new' | 'margin' } | null {
  const known = readiness.filter((r) => r.last.percent !== null).sort((a, b) => a.last.percent! - b.last.percent!)
  const below = known.find((r) => r.last.isBelowThreshold)
  if (below) return { r: below, kind: 'below' }
  const fresh = readiness.find((r) => r.last.percent === null)
  if (fresh) return { r: fresh, kind: 'new' }
  return known[0] ? { r: known[0], kind: 'margin' } : null
}

export function StudentCabinet({ me, onMe }: { me: Me; onMe: (m: Me) => void }) {
  const { t, errorText, lang } = useI18n()
  const [data, setData] = useState<DashboardData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [starting, setStarting] = useState(false)
  const [editing, setEditing] = useState(false)
  const [showAll, setShowAll] = useState(false)
  const [article, setArticle] = useState<{ slug: ArticleSlug; title: string; summary: string } | null>(null)
  const [today] = useState(todayIso)

  useEffect(() => {
    api.dashboard(me.targetLevel, me.targetVariant).then((d) => { setData(d); setCode(d.suggestedArkusz ?? '') }).catch((e: unknown) => setError(errorText(e)))
  }, [me.targetLevel, me.targetVariant, errorText])

  const weak = data ? weakestPart(data.readiness) : null
  const articleSlug: ArticleSlug = weak ? ARTICLE_FOR[weak.r.kind] : 'bledy'
  useEffect(() => {
    let alive = true
    void loadArticle(lang, articleSlug).then((a) => { if (alive && a) setArticle({ slug: articleSlug, ...articleMeta(a.text) }) })
    return () => { alive = false }
  }, [lang, articleSlug])

  async function startExam() {
    if (!code) return
    setStarting(true)
    try {
      const session = await api.startSession(code)
      navigate(links.session(session.id))
    } catch (e) {
      setError(errorText(e))
      setStarting(false)
    }
  }

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!data) return <Loader label={t('cabinet.loading')} />

  const passing = data.readiness.filter((r) => r.last.percent !== null && !r.last.isBelowThreshold).length
  const total = Math.max(1, data.readiness.length)
  // readiness level: average of the latest results per part, a part not yet submitted counts as 0 %
  const readiness = data.readiness.reduce((sum, r) => sum + (r.last.percent ?? 0), 0) / total
  const days = me.examDate ? Math.round((Date.parse(me.examDate) - Date.parse(today)) / 86400000) : null
  const variant = me.targetVariant === 'dim' ? ` · ${t('variant.youthLower')}` : ''
  const pct = (v: number) => `${Math.round(v)} %`
  const history = showAll ? data.history : data.history.slice(0, 3)

  return (
    <>
      <h1 className="visually-hidden">{t('cabinet.title', { level: me.targetLevel, variant })}</h1>
      <section className="cab-hero" aria-label={t('cabinet.goals')}>
        <div className="cab-hero__gauge">
          <ReadinessGauge value={readiness} caption={t('cabinet.readiness')} empty={data.readiness.every((r) => r.last.percent === null)} />
        </div>

        <div className="cab-hero__goal">
          <span className="small-title">{t('cabinet.goalLabel', { level: me.targetLevel, variant })}</span>
          {days !== null ? (
            <div className="goals__countdown">
              <strong>{days}</strong>
              <span>{days === 0 ? t('cabinet.examToday') : t('cabinet.daysToExam', { date: formatDate(me.examDate!) })}</span>
            </div>
          ) : (
            <p className="muted">{t('cabinet.examDateHint')}</p>
          )}
          <p className="cab-hero__parts">
            {t('cabinet.goalParts', { passing, total: data.readiness.length })}
            {weak && weak.kind === 'below' && <> · {t('cabinet.weakest', { part: PART_LABEL[weak.r.kind], percent: pct(weak.r.last.percent!) })}</>}
          </p>
          <button type="button" className="btn btn--ghost cab-hero__edit" aria-expanded={editing} onClick={() => setEditing(!editing)}>
            {editing ? t('cabinet.editGoalDone') : t('cabinet.editGoal')}
          </button>
        </div>

        <div className="cab-hero__cta">
          <select className="field" value={code} onChange={(e) => setCode(e.target.value)} aria-label={t('cabinet.arkuszLabel')}>
            {data.fullArkuszCodes.length === 0 && <option value="">{t('cabinet.mockNone')}</option>}
            {[...data.fullArkuszCodes].sort().reverse().map((c) => <option key={c} value={c}>{c === data.suggestedArkusz ? t('cabinet.notTaken', { code: c }) : c}</option>)}
          </select>
          <button className="btn btn--primary btn--block" disabled={!code || starting} onClick={startExam}>{t('cabinet.startMock')}</button>
          <span className="muted small">{t('cabinet.mockHint')}</span>
        </div>

        {editing && (
          <div className="cab-hero__editor">
            <LevelPicker me={me} onChange={onMe} />
            <ExamDateGoal me={me} onMe={onMe} />
          </div>
        )}
      </section>

      <section className="cab-section">
        <h2 className="cab-section__title">{t('cabinet.todo')}</h2>
        <div className="todo-grid">
          {weak && (
            <a className="todo-card" href={links.examList}>
              <span className={`todo-card__tag ${weak.kind === 'below' ? 'todo-card__tag--fail' : ''}`}>{weak.kind === 'new' ? t('cabinet.todoNewTag') : t('cabinet.todoWeakTag')}</span>
              <strong className="todo-card__title">{t('cabinet.todoWeakTitle', { part: PART_LABEL[weak.r.kind] })}</strong>
              <span className="muted small">
                {weak.kind === 'new' ? t('cabinet.todoNewText')
                  : weak.kind === 'below' ? t('cabinet.todoWeakText', { percent: pct(weak.r.last.percent!), gap: Math.round(50 - weak.r.last.percent!) })
                  : t('cabinet.todoMarginText', { percent: pct(weak.r.last.percent!) })}
              </span>
              <span className="todo-card__go">{t('cabinet.todoStart')} →</span>
            </a>
          )}
          <a className="todo-card" href={links.today}>
            <span className="todo-card__tag todo-card__tag--warn">{t('cabinet.todoMistakesTag')}</span>
            <strong className="todo-card__title">{t('cabinet.todoMistakesTitle')}</strong>
            <span className="muted small">{data.topPatterns.length > 0 ? t('cabinet.mistakesHint', { title: patternShort(data.topPatterns[0].title) }) : t('cabinet.mistakesEmpty')}</span>
            <span className="todo-card__go">{t('cabinet.todoOpen')} →</span>
          </a>
          {article && (
            <a className="todo-card" href={links.article(article.slug)}>
              <span className="todo-card__tag">{t('nav.knowledge')}</span>
              <strong className="todo-card__title">{article.title}</strong>
              <span className="muted small">{article.summary}</span>
              <span className="todo-card__go">{t('cabinet.todoRead')} →</span>
            </a>
          )}
        </div>
      </section>

      <section className="task cab-parts">
        <div className="cab-section__head">
          <h2 className="task__title">{t('cabinet.partsTitle')}</h2>
          <span className="muted small">{t('cabinet.partsHint')}</span>
        </div>
        <ul className="part-progress">
          {data.readiness.map((r) => {
            const v = r.last.percent
            return (
              <li key={r.kind}>
                <span className="part-progress__name">{PART_LABEL[r.kind]}</span>
                <div className="bar bar--big">
                  {v !== null && <span className={`bar__fill ${r.last.isBelowThreshold ? 'bar__fill--fail' : 'bar__fill--pass'}`} style={{ width: `${Math.min(100, v)}%` }} />}
                  <span className="bar__mark" />
                </div>
                <span className={`part-progress__value ${v === null ? 'muted' : r.last.isBelowThreshold ? 'wrong-text' : 'correct-text'}`}>{v === null ? t('cabinet.notTakenShort') : pct(v)}</span>
                <a className="part-progress__train" href={links.examList}>{t('cabinet.train')}</a>
              </li>
            )
          })}
        </ul>
      </section>

      <section className="task">
        <h2 className="task__title">{t('cabinet.recent')}</h2>
        {data.history.length === 0 ? <p className="muted">{t('cabinet.historyEmpty')}</p> : (
          <>
            <ul className="history">
              {history.map((h) => (
                <li key={h.attemptId}>
                  <span className="badge">{h.arkuszCode}</span> {PART_LABEL[h.kind]} · {formatPoints(h.points)}/{formatPoints(h.maxPoints)} ({formatPercent(h.percent)})
                  <span className="muted small"> · {formatDateTime(h.submittedAt)}{h.sessionId ? t('cabinet.mockSuffix') : ''}</span>
                  {' '}<a href={links.examResult(h.attemptId)}>{t('cabinet.review')}</a>
                </li>
              ))}
            </ul>
            {data.history.length > 3 && <button type="button" className="btn btn--ghost" onClick={() => setShowAll(!showAll)}>{showAll ? t('cabinet.showLess') : t('cabinet.allHistory', { count: data.history.length })}</button>}
          </>
        )}
        {data.groups.length > 0 && (
          <p className="muted small">{t('cabinet.myGroups')}: {data.groups.map((g) => g.name).join(', ')}</p>
        )}
      </section>
    </>
  )
}

/** Exam-date goal: days left until the exam; the server rejects dates in the past. */
function ExamDateGoal({ me, onMe }: { me: Me; onMe: (m: Me) => void }) {
  const { t, errorText } = useI18n()
  const [error, setError] = useState<string | null>(null)
  const [today] = useState(todayIso)

  async function save(value: string) {
    setError(null)
    try {
      await api.setExamDate(value || null)
      onMe({ ...me, examDate: value || null })
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <div className="goals__date">
      <label className="small-title" htmlFor="exam-date">{t('cabinet.examDate')}</label>
      <div className="actions">
        <input id="exam-date" className="field" type="date" min={today} value={me.examDate ?? ''} onChange={(e) => void save(e.target.value)} />
        {me.examDate && <button type="button" className="btn btn--ghost" onClick={() => void save('')}>{t('cabinet.clearDate')}</button>}
      </div>
      {error && <p className="error small">{error}</p>}
    </div>
  )
}

export function DashboardView({ data, arkusze }: { data: DashboardData; arkusze: ArkuszSummary[] | null }) {
  const { t } = useI18n()
  return (
    <>
      <ProgressBoard data={data} arkusze={arkusze} />

      <section className="task">
        <h2 className="task__title">{t('cabinet.patterns')}</h2>
        <Patterns data={data} />
      </section>

      <section className="task">
        <h2 className="task__title">{t('cabinet.history')}</h2>
        {data.history.length === 0 ? <p className="muted">{t('cabinet.historyEmpty')}</p> : (
          <ul className="history">
            {data.history.map((h) => (
              <li key={h.attemptId}>
                <span className="badge">{h.arkuszCode}</span> {PART_LABEL[h.kind]} · {formatPoints(h.points)}/{formatPoints(h.maxPoints)} ({formatPercent(h.percent)})
                <span className="muted small"> · {formatDateTime(h.submittedAt)}{h.sessionId ? t('cabinet.mockSuffix') : ''}</span>
                {' '}<a href={links.examResult(h.attemptId)}>{t('cabinet.review')}</a>
              </li>
            ))}
          </ul>
        )}
      </section>

      {data.groups.length > 0 && (
        <section className="task">
          <h2 className="task__title">{t('cabinet.myGroups')}</h2>
          <ul>{data.groups.map((g) => <li key={g.groupId}>{g.name} <span className="muted small">{t('cabinet.teacher', { name: g.teacherName })}</span></li>)}</ul>
        </section>
      )}
    </>
  )
}

/** Part result bar: the fill is the latest %, the tick is the 50 % threshold. */
function PartBar({ percent }: { percent: number | null }) {
  if (percent === null) return <div className="bar bar--empty"><span className="bar__mark" /></div>
  return (
    <div className="bar" title={formatPercent(percent)}>
      <span className={`bar__fill ${percent < 50 ? 'bar__fill--fail' : 'bar__fill--pass'}`} style={{ width: `${Math.min(100, percent)}%` }} />
      <span className="bar__mark" />
    </div>
  )
}

export function TeacherCabinet({ me, onMe }: { me: Me; onMe: (m: Me) => void }) {
  const { t, errorText } = useI18n()
  const confirm = useConfirm()
  const [groups, setGroups] = useState<GroupSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    api.teacherSummary(me.targetLevel, me.targetVariant).then(setGroups).catch((e: unknown) => setError(errorText(e)))
  }, [me.targetLevel, me.targetVariant, errorText])
  useEffect(load, [load])

  async function create() {
    if (!name.trim()) return
    setBusy(true)
    try {
      const group = await api.createGroup(name.trim())
      navigate(links.group(group.id))
    } catch (e) {
      setError(errorText(e))
      setBusy(false)
    }
  }

  async function close(g: GroupSummary) {
    if (!(await confirm({ message: t('cabinet.confirmClose', { name: g.name }), confirmLabel: t('cabinet.closeGroup'), danger: true }))) return
    try {
      await api.archiveGroup(g.groupId)
      load()
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <>
      <header className="header"><h1>{t('cabinet.titleTeacher', { level: me.targetLevel, variant: me.targetVariant === 'dim' ? ` · ${t('variant.youthLower')}` : '' })}</h1></header>
      <LevelPicker me={me} onChange={onMe} />
      {error && <p className="error">{t('common.errorPrefix', { message: error })}</p>}

      <section className="task">
        <h2 className="task__title">{t('cabinet.newGroup')}</h2>
        <form className="actions" onSubmit={(e) => { e.preventDefault(); void create() }}>
          <input className="field" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('cabinet.groupNamePlaceholder')} maxLength={100} />
          <button className="btn btn--primary" disabled={busy || !name.trim()}>{t('cabinet.createGroup')}</button>
        </form>
      </section>

      {groups === null ? <Loader label={t('cabinet.summaryLoading')} /> : groups.length === 0 ? <p className="muted">{t('cabinet.noTeacherGroups')}</p> : groups.map((g) => (
        <section key={g.groupId} className="task group-card">
          <div className="group-card__head">
            <h2 className="task__title"><a href={links.group(g.groupId)}>{g.name}</a> <span className="muted small">{t('cabinet.studentsCount', { count: g.students })}</span></h2>
            <div className="actions">
              <a className="btn" href={links.group(g.groupId)}>{t('cabinet.manageGroup')}</a>
              <button type="button" className="btn btn--fail" onClick={() => void close(g)}>{t('cabinet.closeGroup')}</button>
            </div>
          </div>

          {g.students === 0 ? <p className="muted">{t('cabinet.noStudents')} <a href={links.group(g.groupId)}>{t('cabinet.inviteLink')}</a></p> : (
            <>
              <ul className="part-bars">
                {PARTS.map((k) => {
                  const passed = byKind(g.passedByPart, k) ?? 0
                  const below = byKind(g.belowByPart, k) ?? 0
                  return (
                    <li key={k}>
                      <span className="part-bars__label">{PART_LABEL[k]}</span>
                      <div className="bar bar--stack" role="img" aria-label={t('cabinet.passedOf', { passed, total: g.students })}>
                        <span className="bar__fill bar__fill--pass" style={{ width: `${(passed / g.students) * 100}%` }} />
                        <span className="bar__fill bar__fill--fail" style={{ width: `${(below / g.students) * 100}%` }} />
                      </div>
                      <span className="part-bars__value small">{t('cabinet.passedOf', { passed, total: g.students })}</span>
                    </li>
                  )
                })}
              </ul>
              <p className="muted small">{t('cabinet.barsLegend')}</p>

              <div className="matrix-wrap">
                <table className="matrix students-progress">
                  <thead><tr><th>{t('cabinet.colStudent')}</th>{PARTS.map((k) => <th key={k} title={PART_LABEL[k]}>{SHORT[k]}</th>)}<th>{t('cabinet.colActivity')}</th></tr></thead>
                  <tbody>
                    {g.members.map((m) => (
                      <tr key={m.studentId}>
                        <th><a href={links.studentCard(g.groupId, m.studentId)}>{m.displayName}</a></th>
                        {PARTS.map((k) => {
                          const v = byKind(m.percent, k) ?? null
                          return <td key={k}><PartBar percent={v} /><span className="muted small">{v === null ? '—' : `${Math.round(v)} %`}</span></td>
                        })}
                        <td className={`small ${m.isInactive ? 'wrong' : 'muted'}`}>{m.lastActivityAt ? formatDate(m.lastActivityAt) : '—'}{m.isInactive ? ` · ${t('cabinet.inactive')}` : ''}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </section>
      ))}

      <p className="muted small"><a href={links.groups}>{t('cabinet.closedGroups')}</a></p>
    </>
  )
}

export function StudentCardPage({ groupId, studentId, me, onMe }: { groupId: string; studentId: string; me: Me; onMe: (m: Me) => void }) {
  const { t, errorText } = useI18n()
  const [data, setData] = useState<DashboardData | null>(null)
  const [arkusze, setArkusze] = useState<ArkuszSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.arkusze().then(setArkusze).catch(() => setArkusze(null))
  }, [])

  useEffect(() => {
    api.studentCard(groupId, studentId, me.targetLevel, me.targetVariant).then(setData).catch((e: unknown) => setError(errorText(e)))
  }, [groupId, studentId, me.targetLevel, me.targetVariant, errorText])

  if (error) return <><BackRow><BackLink href={links.group(groupId)}>{t('cabinet.toGroup')}</BackLink></BackRow><p className="error">{t('cabinet.cardNoAccess')}</p></>
  if (!data) return <Loader label={t('cabinet.cardLoading')} />
  return (
    <>
      <BackRow><BackLink href={links.group(groupId)}>{t('cabinet.toGroup')}</BackLink></BackRow>
      <header className="header"><h1>{t('cabinet.card')}</h1><p className="muted small">{t('cabinet.readOnly')}</p></header>
      <LevelPicker me={me} onChange={onMe} />
      <DashboardView data={data} arkusze={arkusze} />
    </>
  )
}

