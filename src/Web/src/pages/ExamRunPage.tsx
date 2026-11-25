import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, type ExamAnswer, type ExamAttempt, type ExamItem, type ExamTask } from '../api'
import { TaskText } from '../components/TaskText'
import { ExampleBlock } from '../components/ExampleBlock'
import { formatDateTime, formatPoints } from '../format'
import { PART_LABEL } from '../parts'
import { useI18n } from '../i18n'
import SpeakingRun from './SpeakingRun'
import WritingRun from './WritingRun'
import ListeningPlayer from '../components/ListeningPlayer'
import { stopAudio } from '../audioController'
import { useConfirm } from '../components/confirm'
import { Loader } from '../components/Loader'
import { MediaImage } from '../components/MediaImage'
import { mediaUrl } from '../media'
import { Timer } from '../components/Timer'
import { links, navigate } from '../router'
import { useAutosave } from '../useAutosave'

type AnswerMap = Record<string, string>

const mergeAnswers = (older: AnswerMap, newer: AnswerMap): AnswerMap => ({ ...older, ...newer })
const toAnswers = (map: AnswerMap): ExamAnswer[] => Object.entries(map).map(([itemId, value]) => ({ itemId, value }))
const isExpired = (a: ExamAttempt) => new Date(a.startedAt).getTime() + a.arkusz.timeLimitMinutes * 60_000 <= Date.now()

/** Loads the attempt and hands it to the runner for its part. */
export default function ExamRunPage({ attemptId }: { attemptId: string }) {
  const { t, errorText } = useI18n()
  const [loaded, setLoaded] = useState<{ attempt: ExamAttempt; expiredOnOpen: boolean } | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.examAttempt(attemptId)
      .then((a) => {
        if (a.submittedAt && a.arkusz.part !== 'pisanie' && a.arkusz.part !== 'mowienie') { navigate(links.examResult(a.id)); return }
        // captured once on open: a part whose time ran out while the tab was closed is not auto-submitted
        setLoaded({ attempt: a, expiredOnOpen: isExpired(a) })
      })
      .catch((e: unknown) => setError(errorText(e)))
  }, [attemptId, errorText])

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!loaded) return <Loader label={t('run.loading')} />
  const { attempt } = loaded
  return <ClosedRun attempt={attempt} expiredOnOpen={loaded.expiredOnOpen} />
}

/** Słuchanie, Czytanie and Gramatyka: closed tasks whose answers are autosaved per item. */
function ClosedRun({ attempt, expiredOnOpen }: { attempt: ExamAttempt; expiredOnOpen: boolean }) {
  const { t, errorText } = useI18n()
  const confirm = useConfirm()
  const [answers, setAnswers] = useState<AnswerMap>(() => Object.fromEntries(attempt.answers.map((x) => [x.itemId, x.value])))
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const saveAnswers = useCallback((map: AnswerMap, keepalive: boolean) => api.saveAnswers(attempt.id, toAnswers(map), keepalive), [attempt.id])
  const { schedule, flush, state: saveState, error: saveFailure } = useAutosave<AnswerMap>({ save: saveAnswers, merge: mergeAnswers })
  const shownError = error ?? (saveFailure === null ? null : errorText(saveFailure))

  const setAnswer = useCallback((itemId: string, value: string) => {
    setAnswers((prev) => ({ ...prev, [itemId]: value }))
    schedule({ [itemId]: value })
  }, [schedule])

  // time ran out while working: as in the real exam, answers are saved and submitted without asking
  async function autoSubmit() {
    if (submitting || expiredOnOpen) return
    await finishAttempt()
  }

  async function finishAttempt() {
    setSubmitting(true)
    setError(null)
    try {
      if (!(await flush())) {
        setError(t('run.saveFailed'))
        setSubmitting(false)
        return
      }
      stopAudio()
      await api.submitExam(attempt.id)
      navigate(attempt.sessionId ? links.session(attempt.sessionId) : links.examResult(attempt.id))
    } catch (e) {
      setError(errorText(e))
      setSubmitting(false)
    }
  }

  // an abandoned attempt can be discarded and the part restarted from scratch (outside a mock exam)
  async function restart() {
    setSubmitting(true)
    try {
      await api.discardAttempt(attempt.id)
      const fresh = await api.startExam(attempt.arkusz.code, attempt.arkusz.part)
      navigate(links.examRun(fresh.id))
    } catch (e) {
      setError(errorText(e))
      setSubmitting(false)
    }
  }

  async function submit() {
    const total = attempt.arkusz.tasks.reduce((n, task) => n + task.items.length, 0)
    const answered = Object.values(answers).filter((v) => v.trim() !== '').length
    const confirmText = answered < total
      ? t('run.confirmMissing', { missing: total - answered, total })
      : t('run.confirmSubmit')
    if (!(await confirm({ message: confirmText }))) return
    await finishAttempt()
  }

  if (attempt.arkusz.part === 'pisanie') return <WritingRun initial={attempt} />
  if (attempt.arkusz.part === 'mowienie') return <SpeakingRun initial={attempt} />

  const { arkusz } = attempt
  const total = arkusz.tasks.reduce((n, t) => n + t.items.length, 0)
  const answered = Object.values(answers).filter((v) => v.trim() !== '').length

  if (expiredOnOpen) {
    return (
      <section className="task expired">
        <h1 className="expired__title">{PART_LABEL[arkusz.part]} · {arkusz.code}</h1>
        <p className="expired__lead">{t('run.expiredTitle')}</p>
        <p className="muted">{t('run.expiredText', { date: formatDateTime(attempt.startedAt), answered, total })}</p>
        {shownError && <p className="error">{t('common.errorPrefix', { message: shownError })}</p>}
        <div className="actions">
          {!attempt.sessionId && <button className="btn btn--primary" disabled={submitting} onClick={() => void restart()}>{t('run.restart')}</button>}
          <button className={`btn ${attempt.sessionId ? 'btn--primary' : ''}`} disabled={submitting} onClick={() => void finishAttempt()}>{t('run.submitAsIs')}</button>
        </div>
      </section>
    )
  }

  return (
    <>
      <header className="header header--sticky">
        <div className="header__row">
          <div>
            <h1>{PART_LABEL[arkusz.part]} · {arkusz.code}</h1>
            <p className="muted small">{t('run.meta', { title: arkusz.title, tasks: arkusz.tasks.length, points: formatPoints(arkusz.maxPoints) })}</p>
          </div>
          <Timer startedAt={attempt.startedAt} limitMinutes={arkusz.timeLimitMinutes} onExpire={() => void autoSubmit()} />
        </div>
        <nav className="anchors">
          {arkusz.tasks.map((t) => {
            const done = t.items.filter((i) => (answers[i.id] ?? '').trim() !== '').length
            return (
              <a key={t.id} href={`#/egzamin/${attempt.id}#zadanie-${t.number}`} onClick={(e) => { e.preventDefault(); document.getElementById(`zadanie-${t.number}`)?.scrollIntoView({ behavior: 'smooth' }) }}
                className={`anchor ${done === t.items.length ? 'anchor--done' : ''}`}>
                {t.romanNumber} <span className="muted">{done}/{t.items.length}</span>
              </a>
            )
          })}
          <span className="save-state muted small">
            {saveState === 'pending' && t('run.save.pending')}
            {saveState === 'saving' && t('run.save.saving')}
            {saveState === 'saved' && t('run.save.saved')}
            {saveState === 'error' && <span className="error">{t('run.save.error')}</span>}
          </span>
        </nav>
      </header>

      {shownError && <p className="error">{t('common.errorPrefix', { message: shownError })}</p>}

      {arkusz.audioKey && <ListeningPlayer attemptId={attempt.id} audioKey={arkusz.audioKey} savedPosition={attempt.audioPositionSec} />}

      {arkusz.tasks.map((task) => (
        <TaskBlock key={task.id} task={task} answers={answers} setAnswer={setAnswer} />
      ))}

      <footer className="submit-bar">
        <span className="muted">{t('run.answered', { answered, total })}</span>
        <button className="btn btn--primary" disabled={submitting} onClick={submit}>
          {submitting ? t('run.submitting') : t('run.submit')}
        </button>
      </footer>
    </>
  )
}

function TaskBlock({ task, answers, setAnswer }: {
  task: ExamTask
  answers: Record<string, string>
  setAnswer: (itemId: string, value: string) => void
}) {
  const { t } = useI18n()
  const byNumber = useMemo(() => new Map(task.items.map((i) => [i.number, i])), [task.items])

  const renderSlot = (n: number) => {
    const item = byNumber.get(n)
    if (!item) return `{${n}}`
    const value = answers[item.id] ?? ''
    switch (task.type) {
      case 'chooseInText':
        return <ChoiceSlot item={item} value={value} onChange={(v) => setAnswer(item.id, v)} />
      case 'wordBank':
      case 'matching':
        return (
          <span className="slot">
            <select className="slot__select" value={value} onChange={(e) => setAnswer(item.id, e.target.value)}>
              <option value="">…</option>
              {task.wordBank.map((w) => <option key={w} value={w}>{w}</option>)}
            </select>
            <sup className="slot__n">{n}</sup>
          </span>
        )
      case 'fillForm':
        return (
          <span className="slot">
            <input className="slot__input" value={value} placeholder="……" size={Math.max(10, value.length + 2)}
              onChange={(e) => setAnswer(item.id, e.target.value)} autoComplete="off" spellCheck={false} />
            <sup className="slot__n">{n}</sup>
            {item.prompt && <span className="slot__prompt"> ({item.prompt})</span>}
          </span>
        )
      default:
        return `{${n}}`
    }
  }

  return (
    <section className="task" id={`zadanie-${task.number}`}>
      <h2 className="task__title">
        {task.romanNumber}. {task.instruction}
        <span className="task__points muted">{t('run.taskPoints', { items: task.items.length, points: formatPoints(task.maxPoints) })}</span>
      </h2>

      {task.image && <MediaImage className="task-image" src={mediaUrl(task.image)} alt={t('run.imageAlt')} />}

      {task.type === 'matching' && task.wordBank.length > 0 && !task.image && (
        <ul className="word-bank-list">{task.wordBank.map((w) => <li key={w}>{w}</li>)}</ul>
      )}
      {task.type !== 'matching' && task.wordBank.length > 0 && (
        <div className="word-bank">{task.wordBank.map((w) => <span key={w} className="word-bank__word">{w}</span>)}</div>
      )}

      {task.example && <ExampleBlock example={task.example} />}

      {task.text && <TaskText text={task.text} renderSlot={renderSlot} />}

      {task.type === 'shortAnswer' && (
        <ol className="free-items">
          {task.items.map((item) => (
            <li key={item.id} className="free-item">
              <div className="free-item__prompt"><TaskText text={item.prompt ?? ''} /></div>
              <input className="free-item__input" value={answers[item.id] ?? ''} placeholder={t('run.answerPlaceholder')} onChange={(e) => setAnswer(item.id, e.target.value)} autoComplete="off" spellCheck={false} />
            </li>
          ))}
        </ol>
      )}

      {(task.type === 'multipleChoice' || task.type === 'trueFalse' || (task.type === 'matching' && !(task.text ?? '').includes('{1}'))) && (
        <ol className={`free-items ${task.image ? 'free-items--grid' : ''}`}>
          {task.items.map((item) => (
            <li key={item.id} className="free-item">
              {item.prompt && <div className={`free-item__prompt ${task.type === 'multipleChoice' && item.prompt.length > 120 ? 'free-item__prompt--reading' : ''}`}><TaskText text={item.prompt} /></div>}
              {item.hint && <div className="free-item__hint">{item.hint}</div>}
              {task.type === 'matching' ? (
                <select className="slot__select" value={answers[item.id] ?? ''} onChange={(e) => setAnswer(item.id, e.target.value)}>
                  <option value="">…</option>
                  {task.wordBank.map((w) => <option key={w} value={w}>{w.slice(0, 80)}</option>)}
                </select>
              ) : (
                <div className="choices">
                  {item.options.map((opt) => (
                    <label key={opt} className={`choice choice--block ${answers[item.id] === opt ? 'choice--on' : ''}`}>
                      <input type="radio" name={`item-${item.id}`} value={opt} checked={answers[item.id] === opt} onChange={() => setAnswer(item.id, opt)} />
                      <OptionLabel text={opt} />
                    </label>
                  ))}
                </div>
              )}
            </li>
          ))}
        </ol>
      )}

      {(task.type === 'askQuestion' || task.type === 'transform') && (
        <ol className="free-items">
          {task.items.map((item) => (
            <li key={item.id} className="free-item">
              <div className="free-item__prompt">
                <TaskText text={item.prompt ?? ''} />
                {task.type === 'transform' && item.hint && <span className="free-item__hint">({item.hint})</span>}
              </div>
              {task.type === 'askQuestion' && item.hint && (
                <div className="free-item__frame">
                  <TaskText text={item.hint} />
                </div>
              )}
              <input className="free-item__input" value={answers[item.id] ?? ''}
                placeholder={task.type === 'askQuestion' ? t('run.questionPlaceholder') : t('run.transformPlaceholder')}
                onChange={(e) => setAnswer(item.id, e.target.value)} autoComplete="off" spellCheck={false} />
            </li>
          ))}
        </ol>
      )}

      {task.basedOn && <p className="muted small based-on">{task.basedOn}</p>}
    </section>
  )
}

function ChoiceSlot({ item, value, onChange }: { item: ExamItem; value: string; onChange: (v: string) => void }) {
  const name = `item-${item.id}`
  return (
    <span className="slot slot--choice">
      (
      {item.options.map((opt, i) => (
        <label key={opt} className={`choice ${value === opt ? 'choice--on' : ''}`}>
          <input type="radio" name={name} value={opt} checked={value === opt} onChange={() => onChange(opt)} />
          {opt}{i < item.options.length - 1 ? ',' : ''}
        </label>
      ))}
      )<sup className="slot__n">{item.number}</sup>
    </span>
  )
}

/** "a) text" from the arkusz becomes a letter badge plus the text; options without a letter are shown as they are. */
function OptionLabel({ text }: { text: string }) {
  const m = /^\s*([a-zA-Z])\)\s*(.*)$/s.exec(text)
  if (!m) return <span>{text}</span>
  return <><span className="choice__letter" aria-hidden="true">{m[1].toLowerCase()}</span><span>{m[2]}</span></>
}
