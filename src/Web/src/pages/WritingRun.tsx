import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { api, type ExamAnswer, type ExamAttempt, type ExamItem, type ReviewError, type WritingReview } from '../api'
import { formatPoints } from '../format'
import { links } from '../router'
import { Timer } from '../components/Timer'
import { BackLink, BackRow } from '../components/BackLink'
import { useConfirm } from '../components/confirm'
import { Loader } from '../components/Loader'
import { useI18n } from '../i18n'
import { itemLabel, readWritingReview, REVIEW_KEYS } from '../review'
import { useAutosave } from '../useAutosave'

// Official criteria and text-form names of the exam: Polish exam content, shown as in the arkusz
const CRITERIA = ['Wykonanie zadania', 'Poprawność gramatyczna', 'Słownictwo', 'Styl', 'Ortografia i interpunkcja']
const FORM_LABEL: Record<string, string> = {
  zyczenia: 'życzenia', pozdrowienia: 'pozdrowienia', ogloszenie: 'ogłoszenie', zaproszenie: 'zaproszenie', sms: 'SMS',
  sprawozdanie: 'sprawozdanie', charakterystyka: 'charakterystyka', opowiadanie: 'opowiadanie', esej: 'esej',
  list: 'list / e-mail', opinia: 'opinia', opis: 'opis',
}

type TextMap = Record<string, string>
const mergeTexts = (older: TextMap, newer: TextMap): TextMap => ({ ...older, ...newer })
const toAnswers = (map: TextMap): ExamAnswer[] => Object.entries(map).map(([itemId, value]) => ({ itemId, value }))

export default function WritingRun({ initial }: { initial: ExamAttempt }) {
  const { t, errorText, lang } = useI18n()
  const confirm = useConfirm()
  const [attempt, setAttempt] = useState(initial)
  const [texts, setTexts] = useState<TextMap>(() => Object.fromEntries(initial.answers.map((a) => [a.itemId, a.value])))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submitted = attempt.submittedAt !== null
  const task = attempt.arkusz.tasks.find((x) => x.number === attempt.chosenSet) ?? null
  const review = readWritingReview(attempt.review)

  const saveTexts = useCallback((map: TextMap, keepalive: boolean) => api.saveAnswers(attempt.id, toAnswers(map), keepalive), [attempt.id])
  const { schedule, flush, error: saveFailure } = useAutosave<TextMap>({ save: saveTexts, merge: mergeTexts })
  const shownError = error ?? (saveFailure === null ? null : errorText(saveFailure))

  function setText(item: ExamItem, value: string) {
    setTexts((prev) => ({ ...prev, [item.id]: value }))
    schedule({ [item.id]: value })
  }

  async function chooseSet(set: number) {
    setBusy(true)
    setError(null)
    try {
      setAttempt(await api.chooseSet(attempt.id, set))
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  // As in the exam: once submitted, the AI grades the work on the examiners' 5 criteria and mistakes go to the bank
  async function finish() {
    if (!(await confirm({ message: t('write.confirmFinish'), confirmLabel: t('write.finish') }))) return
    setBusy(true)
    setError(null)
    try {
      if (task) schedule(Object.fromEntries(task.items.map((i) => [i.id, texts[i.id] ?? ''])))
      if (!(await flush())) { setError(t('run.saveFailed')); return }
      setAttempt(await api.finish(attempt.id, lang))
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const sets = attempt.arkusz.tasks

  return (
    <>
      <header className="header header--sticky">
        <div className="header__row">
          <div>
            <h1>Pisanie · {attempt.arkusz.code}</h1>
            <p className="muted small">{t('write.meta', { title: attempt.arkusz.title })}</p>
          </div>
          {!submitted && <Timer startedAt={attempt.startedAt} limitMinutes={attempt.arkusz.timeLimitMinutes} />}
        </div>
      </header>

      {shownError && <p className="error">{shownError}</p>}

      {task === null && !submitted && (
        <section>
          {/* the official arkusz instruction, kept in Polish as exam content */}
          <p className="muted">Uwaga! Proszę wybrać tylko jeden z zestawów i wykonać obydwa zadania (a i b) z tego samego zestawu.</p>
          {sets.map((s) => (
            <div key={s.id} className="task">
              <h2 className="task__title">Zestaw {s.romanNumber}</h2>
              {s.items.map((i) => (
                <p key={i.id}><strong>{itemLabel(i.number)}.</strong> {i.prompt} <span className="muted">({t('write.targetWords', { count: i.targetWords })})</span></p>
              ))}
              <button className="btn btn--primary" disabled={busy} onClick={() => void chooseSet(s.number)}>
                {t('write.chooseSet', { set: s.romanNumber })}
              </button>
            </div>
          ))}
        </section>
      )}

      {task !== null && (
        <>
          <p className="muted small">Zestaw {task.romanNumber}</p>
          {task.items.map((item) => (
            <WritingItem key={item.id} item={item} text={texts[item.id] ?? ''} readOnly={submitted}
              onText={(v) => setText(item, v)}
              errors={review?.errors.filter((e) => e.zadanie === itemLabel(item.number)) ?? null} />
          ))}

          {review && (
            <section className="task review">
              <h2 className="task__title">{t('write.aiReviewTitle')}{review.werdykt && <> · <strong>{review.werdykt}</strong></>}</h2>
              <ul className="criteria-scores">
                {CRITERIA.map((name, idx) => <li key={name}><span>{name}</span><strong>{formatReviewScore(review, REVIEW_KEYS[idx])} / 4</strong></li>)}
              </ul>
              <p><strong>{t('write.aiComment')}</strong> {review.komentarz}</p>
              {review.errors.length > 0 && <p className="muted small">{t('write.bankedAuto')}</p>}
            </section>
          )}

          {submitted ? (
            <section className="score score--pass">
              <div className="score__main"><span className="score__points">{formatPoints(attempt.points)} / 30</span></div>
              <p className="muted">{t('write.resultCounted')}</p>
              <BackRow>{attempt.sessionId ? <BackLink href={links.session(attempt.sessionId)}>{t('speak.toSession')}</BackLink> : <BackLink href={links.examList}>{t('speak.toList')}</BackLink>}</BackRow>
            </section>
          ) : (
            <footer className="submit-bar">
              {busy ? <Loader inline showLabel label={t('write.checking')} /> : <span />}
              <button className="btn btn--primary" disabled={busy} onClick={finish}>{t('write.finish')}</button>
            </footer>
          )}
        </>
      )}
    </>
  )
}

function formatReviewScore(review: WritingReview, key: string) {
  const value = review.scores[key]
  return typeof value === 'number' ? formatScore(value) : '—'
}

/** Criterion score in 0.25 steps: formatPoints rounds to tenths (3.25 → "3,3"), here hundredths are needed. */
const formatScore = (value: number) => String(Math.round(value * 100) / 100).replace('.', ',')

function WritingItem({ item, text, readOnly, onText, errors }: {
  item: ExamItem; text: string; readOnly: boolean; onText: (v: string) => void
  errors: ReviewError[] | null
}) {
  const { t } = useI18n()
  const [count, setCount] = useState<{ count: number; min: number; max: number; status: string } | null>(null)
  useEffect(() => {
    const id = window.setTimeout(() => { api.wordCount(text, item.targetWords).then(setCount).catch(() => setCount(null)) }, 400)
    return () => window.clearTimeout(id)
  }, [text, item.targetWords])

  return (
    <section className="task">
      <h2 className="task__title">{itemLabel(item.number)}. {item.prompt}</h2>
      <p className="muted small">{t('write.form', { form: FORM_LABEL[item.hint ?? ''] ?? item.hint ?? '', words: t('write.targetWords', { count: item.targetWords }) })}</p>
      {readOnly && errors && errors.length > 0
        ? <HighlightedText text={text} errors={errors} />
        : <textarea className="writing-area" rows={item.targetWords > 60 ? 12 : 5} value={text} readOnly={readOnly} onChange={(e) => onText(e.target.value)} spellCheck={false} />}
      {count && (
        <p className={`word-count word-count--${count.status}`}>
          {t('write.wordCount', { count: count.count, min: count.min, max: count.max })}{count.status === 'under' && t('write.under')}{count.status === 'over' && t('write.over')}
        </p>
      )}
      {errors && (
        <div className="review">
          <p><strong>{t('write.aiErrors', { count: errors.length })}</strong>{errors.length === 0 && t('write.aiErrorsNone')}</p>
          <ul className="review-errors">
            {errors.map((err, i) => (
              <li key={i}>
                <span className="wrong">{err.fragment}</span> → <span className="correct">{err.poprawnie}</span> <span className="muted small">({err.typ})</span>
                <div className="muted small">{err.regula}</div>
                {err.kontrast && <div className="muted small">RU/UA: {err.kontrast}</div>}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

/** The work with highlighted errors: each reviewed fragment is a <mark>; hovering shows the correct form. */
function HighlightedText({ text, errors }: { text: string; errors: ReviewError[] }) {
  const lower = text.toLowerCase()
  const ranges: { start: number; end: number; err: ReviewError }[] = []
  for (const err of errors) {
    const needle = err.fragment.trim().toLowerCase()
    if (!needle) continue
    let from = 0
    while (from <= lower.length) {
      const at = lower.indexOf(needle, from)
      if (at < 0) break
      const end = at + needle.length
      if (!ranges.some((r) => at < r.end && end > r.start)) { ranges.push({ start: at, end, err }); break }
      from = at + 1
    }
  }
  ranges.sort((a, b) => a.start - b.start)
  const parts: ReactNode[] = []
  let pos = 0
  ranges.forEach((r, i) => {
    if (r.start > pos) parts.push(text.slice(pos, r.start))
    parts.push(<mark key={i} className="err-mark" title={'→ ' + r.err.poprawnie + ' (' + r.err.typ + ')'}>{text.slice(r.start, r.end)}</mark>)
    pos = r.end
  })
  if (pos < text.length) parts.push(text.slice(pos))
  return <div className="writing-area writing-area--marked">{parts}</div>
}
