import { useCallback, useEffect, useRef, useState } from 'react'
import { api, type DialogLine, type ExamAttempt, type ExamItem, type SpeakingReview } from '../api'
import { formatPoints } from '../format'
import { links } from '../router'
import { Timer } from '../components/Timer'
import { useI18n } from '../i18n'
import type { Key } from '../i18n/core'
import { SpeechRecorder } from '../components/SpeechRecorder'
import { DialogPanel } from '../components/DialogPanel'
import { dialogTranscript } from '../dialogTranscript'
import { HintPopover } from '../components/HintPopover'
import { BackLink, BackRow } from '../components/BackLink'
import { useConfirm } from '../components/confirm'
import { Loader } from '../components/Loader'
import { MediaImage } from '../components/MediaImage'
import { photoFor, withoutPhotoPlaceholder } from '../mowieniePhotos'
import { readSpeakingReview } from '../review'
import { useAutosave } from '../useAutosave'

const ANSWER_MINUTES = 15
const KINDS = ['opis', 'monolog', 'sytuacja'] as const
type Kind = (typeof KINDS)[number]
const isKind = (hint: string | null): hint is Kind => (KINDS as readonly (string | null)[]).includes(hint)

interface Assessment {
  spoken: Record<string, string>
  // task 3: the dialog lines; spoken[n] holds the same lines as text for the AI review
  dialog?: Record<string, DialogLine[]>
  answerStartedAt?: string
}

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v)

/** The stored assessment is free-form JSON: keep only well-formed fields. */
function readAssessment(raw: unknown): Assessment {
  const a = isRecord(raw) ? raw : {}
  const spoken = isRecord(a.spoken)
    ? Object.fromEntries(Object.entries(a.spoken).filter((e): e is [string, string] => typeof e[1] === 'string'))
    : {}
  const dialog = isRecord(a.dialog)
    ? Object.fromEntries(Object.entries(a.dialog).filter((e): e is [string, DialogLine[]] => Array.isArray(e[1])))
    : {}
  return { spoken, dialog, answerStartedAt: typeof a.answerStartedAt === 'string' ? a.answerStartedAt : undefined }
}

const latestWins = (_older: Assessment, newer: Assessment) => newer

export default function SpeakingRun({ initial }: { initial: ExamAttempt }) {
  const { t, errorText, lang } = useI18n()
  const confirm = useConfirm()
  const [attempt, setAttempt] = useState(initial)
  const [assessment, setAssessment] = useState(() => readAssessment(initial.assessment))
  const latest = useRef(assessment)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submitted = attempt.submittedAt !== null
  const task = attempt.arkusz.tasks.find((x) => x.number === attempt.chosenSet) ?? null
  const review = readSpeakingReview(attempt.review)
  const kindLabel = (hint: string | null) => (isKind(hint) ? t(`speak.kind.${hint}` as Key) : hint ?? '')
  const kindShort = (item: ExamItem) => (isKind(item.hint) ? t(`speak.short.${item.hint}` as Key) : String(item.number))

  const saveDetails = useCallback((a: Assessment, keepalive: boolean) => api.saveDetails(attempt.id, a, keepalive), [attempt.id])
  const { schedule, flush, error: saveFailure } = useAutosave<Assessment>({ save: saveDetails, merge: latestWins })
  const shownError = error ?? (saveFailure === null ? null : errorText(saveFailure))

  const update = useCallback((next: Assessment, immediately = false) => {
    latest.current = next
    setAssessment(next)
    schedule(next, immediately)
  }, [schedule])

  // A recognized fragment is appended to the newest transcript, not to the one captured at render time
  const append = useCallback((key: string, text: string) => {
    if (!text) return
    const cur = latest.current
    const before = cur.spoken[key] ?? ''
    update({ ...cur, spoken: { ...cur.spoken, [key]: before ? `${before} ${text}` : text } })
  }, [update])

  // Dialog lines are saved immediately: the partner's reply builds on what was already said
  const setDialog = useCallback((key: string, lines: DialogLine[]) => {
    const cur = latest.current
    update({ ...cur, dialog: { ...cur.dialog, [key]: lines }, spoken: { ...cur.spoken, [key]: dialogTranscript(lines) } }, true)
  }, [update])

  /** Saves the latest answer, then submits it for the AI review. Returns true once the attempt is submitted. */
  async function submitAnswer(): Promise<boolean> {
    setBusy(true)
    setError(null)
    try {
      schedule(latest.current)
      if (!(await flush())) { setError(t('run.saveFailed')); return false }
      setAttempt(await api.finish(attempt.id, lang))
      return true
    } catch (e) {
      setError(errorText(e))
      return false
    } finally {
      setBusy(false)
    }
  }

  // As in the exam: once submitted, the answer is graded by the examiner (AI against the structure checklist), no self-assessment
  async function finish() {
    if (!(await confirm({ message: t('speak.confirmFinish'), confirmLabel: t('speak.finish') }))) return
    await submitAnswer()
  }

  // Time is up: as in the exam, the answer is submitted without confirmation (marked done only after success)
  const autoFinished = useRef(false)
  async function autoFinish() {
    if (autoFinished.current || submitted || busy) return
    if (await submitAnswer()) autoFinished.current = true
  }

  // As in the exam: the zestaw is drawn at random right on opening, without a separate step
  const drawn = useRef(false)
  useEffect(() => {
    if (task !== null || submitted || drawn.current) return
    drawn.current = true
    // draw only from the official zestawy I–III; the custom one (IV) is never drawn
    const official = attempt.arkusz.tasks.filter((x) => x.number <= 3)
    const sets = official.length > 0 ? official : attempt.arkusz.tasks
    const picked = sets[Math.floor(Math.random() * sets.length)]
    api.chooseSet(attempt.id, picked.number).then(setAttempt).catch((e: unknown) => setError(errorText(e)))
  }, [task, submitted, attempt, errorText])

  const phase = assessment.answerStartedAt ? 'answer' : 'idle'
  const items = task ? [...task.items].sort((a, b) => a.number - b.number) : []
  const isDone = (item: ExamItem) => {
    const key = String(item.number)
    return item.dialog ? (assessment.dialog?.[key] ?? []).some((l) => l.who === 'candidate') : (assessment.spoken[key] ?? '').trim() !== ''
  }
  // one task on screen: by default the first unfinished one, afterwards wherever the candidate moved.
  // The step is chosen once (adjusting state during render) and does not jump when the current task becomes "done".
  const [stepPick, setStepPick] = useState<number | null>(null)
  const firstOpen = Math.max(0, items.findIndex((i) => !isDone(i)))
  if (stepPick === null && items.length > 0) setStepPick(firstOpen)
  const step = Math.min(stepPick ?? firstOpen, Math.max(0, items.length - 1))
  const goTo = (i: number) => { setStepPick(i); window.scrollTo({ top: 0, behavior: 'smooth' }) }

  // once a zestaw exists the answer (and its timer) starts right away, without separate preparation steps
  useEffect(() => {
    if (task !== null && !submitted && phase === 'idle') update({ ...latest.current, answerStartedAt: new Date().toISOString() }, true)
  }, [task, submitted, phase, update])

  return (
    <>
      <header className="header header--sticky">
        <div className="header__row">
          <div>
            <h1>Mówienie · {attempt.arkusz.code}{task && <> · Zestaw {task.number <= 3 ? task.romanNumber : `IV (${t('speak.own')})`}</>}</h1>
            <p className="muted small">{t('speak.meta', { answer: ANSWER_MINUTES })}</p>
          </div>
          {!submitted && assessment.answerStartedAt && <Timer startedAt={assessment.answerStartedAt} limitMinutes={ANSWER_MINUTES} onExpire={() => void autoFinish()} />}
        </div>
      </header>

      {shownError && <p className="error">{shownError}</p>}

      {task === null && !submitted && !shownError && <Loader label={t('common.loading')} />}

      {task !== null && !submitted && items.length > 0 && (
        <>
          <nav className="speak-steps" aria-label={t('speak.stepsLabel')}>
            {items.map((item, i) => (
              <button key={item.id} type="button" className={`speak-step ${i === step ? 'speak-step--on' : ''} ${isDone(item) ? 'speak-step--done' : ''}`}
                aria-current={i === step ? 'step' : undefined} onClick={() => goTo(i)}>
                <span className="speak-step__n" aria-hidden="true">{isDone(item) && i !== step ? '✓' : item.number}</span>
                <span className="speak-step__label">{kindShort(item)}</span>
              </button>
            ))}
          </nav>

          <StepTask key={items[step].id} attemptId={attempt.id} item={items[step]} title={kindLabel(items[step].hint)} assessment={assessment}
            photo={items[step].hint === 'opis' ? photoFor(attempt.arkusz.code, task.number) : null}
            onChange={update} onAppend={(text) => append(String(items[step].number), text)}
            onDialog={(lines) => setDialog(String(items[step].number), lines)} />

          <footer className="submit-bar speak-nav">
            {step > 0
              ? <button type="button" className="btn btn--plain" onClick={() => goTo(step - 1)}>← {kindShort(items[step - 1])}</button>
              : <span />}
            {busy
              ? <Loader inline showLabel label={t('speak.checking')} />
              : step < items.length - 1
                ? <button type="button" className="btn btn--primary" onClick={() => goTo(step + 1)}>{t('speak.next', { task: kindShort(items[step + 1]) })} →</button>
                : <button type="button" className="btn btn--primary" onClick={finish}>{t('speak.finish')}</button>}
          </footer>
        </>
      )}

      {task !== null && submitted && (
        <>
          {task.items.map((item) => (
            <SubmittedTask key={item.id} attemptId={attempt.id} item={item} title={kindLabel(item.hint)} assessment={assessment}
              photo={item.hint === 'opis' ? photoFor(attempt.arkusz.code, task.number) : null}
              review={review?.tasks.find((x) => x.task === item.number)} />
          ))}

          {review && <section className="review"><p><strong>{t('speak.aiComment')}</strong> {review.komentarz}</p></section>}

          <section className="score score--pass">
            <div className="score__main"><span className="score__points">{formatPoints(attempt.points)} / 40</span></div>
            <p className="muted">{t('speak.scoreNote')}</p>
            <BackRow>{attempt.sessionId ? <BackLink href={links.session(attempt.sessionId)}>{t('speak.toSession')}</BackLink> : <BackLink href={links.examList}>{t('speak.toList')}</BackLink>}</BackRow>
          </section>
        </>
      )}
    </>
  )
}

/** The current step: the task, a large microphone, and the transcript after speaking (editable); task 3 is a chat. */
function StepTask({ attemptId, item, title, assessment, photo, onChange, onAppend, onDialog }: {
  attemptId: string
  item: ExamItem
  title: string
  assessment: Assessment
  photo: string | null
  onChange: (a: Assessment) => void
  onAppend: (text: string) => void
  onDialog: (lines: DialogLine[]) => void
}) {
  const { t } = useI18n()
  const key = String(item.number)
  const text = assessment.spoken[key] ?? ''
  const [editing, setEditing] = useState(false)

  return (
    <section className="task speak-card">
      <h2 className="task__title task__title--row">
        <span>{item.number}. {title}</span>
        {item.options.length > 0 && <HintPopover label={t('speak.hints')} items={item.options} />}
      </h2>
      {photo && <MediaImage className="speak-photo" src={photo} alt={t('run.imageAlt')} />}
      <p className="speak-card__prompt">{withoutPhotoPlaceholder(item.prompt ?? '')}</p>

      {item.dialog ? (
        <DialogPanel attemptId={attemptId} item={item.number} info={item.dialog} lines={assessment.dialog?.[key] ?? []} readOnly={false} onLines={onDialog} />
      ) : (
        <>
          <SpeechRecorder variant="big" onAppend={onAppend} />
          {editing ? (
            <div className="speak-transcript speak-transcript--edit">
              <textarea className="writing-area" rows={5} value={text} autoFocus
                onChange={(e) => onChange({ ...assessment, spoken: { ...assessment.spoken, [key]: e.target.value } })} />
              <div className="speak-transcript__actions"><button type="button" className="btn btn--sm" onClick={() => setEditing(false)}>{t('speak.editDone')}</button></div>
            </div>
          ) : text.trim() !== '' ? (
            <div className="speak-transcript">
              <div className="speak-transcript__head">
                <span className="speak-transcript__label">{t('speak.transcript')} · {t('speak.words', { count: text.trim().split(/\s+/).length })}</span>
                <button type="button" className="btn btn--sm btn--plain" onClick={() => setEditing(true)}>{t('speak.edit')}</button>
              </div>
              <p className="speak-transcript__text">{text}</p>
            </div>
          ) : (
            <p className="speak-transcript__empty muted small">{t('speak.transcriptEmpty')} <button type="button" className="link-btn" onClick={() => setEditing(true)}>{t('speak.typeInstead')}</button></p>
          )}
        </>
      )}
    </section>
  )
}

/** A submitted task: read-only transcript or dialog, plus the AI review of its structure. */
function SubmittedTask({ attemptId, item, title, assessment, photo, review }: {
  attemptId: string
  item: ExamItem
  title: string
  assessment: Assessment
  photo: string | null
  review?: SpeakingReview['tasks'][number]
}) {
  const { t } = useI18n()
  const key = String(item.number)

  return (
    <section className="task">
      <h2 className="task__title task__title--row">
        <span>{item.number}. {title}</span>
        {item.options.length > 0 && <HintPopover label={t('speak.hints')} items={item.options} />}
      </h2>
      {photo && <MediaImage className="speak-photo" src={photo} alt={t('run.imageAlt')} />}
      <p>{withoutPhotoPlaceholder(item.prompt ?? '')}</p>
      {item.dialog ? (
        <DialogPanel attemptId={attemptId} item={item.number} info={item.dialog} lines={assessment.dialog?.[key] ?? []} readOnly onLines={() => {}} />
      ) : (
        <>
          <label className="small-title">{t('speak.transcript')}</label>
          <textarea className="writing-area" rows={5} value={assessment.spoken[key] ?? ''} readOnly />
        </>
      )}
      {review && (
        <div className="review">
          {review.covered.length > 0 && <p className="correct small">✓ {review.covered.join(' · ')}</p>}
          {review.missing.length > 0 && <p className="wrong small">✗ {review.missing.join(' · ')}</p>}
          <p className="muted small">{review.komentarz}</p>
        </div>
      )}
    </section>
  )
}
