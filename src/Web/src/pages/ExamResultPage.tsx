import { useEffect, useState } from 'react'
import { api, type AnswerVerdict, type ExamAttempt } from '../api'
import { useI18n } from '../i18n'
import type { Key } from '../i18n/core'
import { TaskText } from '../components/TaskText'
import { gapSentence } from '../gapSentence'
import { formatDateTime, formatPercent, formatPoints } from '../format'
import { BackLink, BackRow } from '../components/BackLink'
import { Loader } from '../components/Loader'
import { links, navigate } from '../router'

const VERDICT_KEY: Record<AnswerVerdict, Key> = {
  none: 'verdict.none',
  correct: 'verdict.correct',
  wrong: 'verdict.wrong',
  needsReview: 'verdict.needsReview',
  accepted: 'verdict.accepted',
}

const isError = (v: AnswerVerdict) => v === 'wrong' || v === 'needsReview'

export default function ExamResultPage({ attemptId }: { attemptId: string }) {
  const { t, errorText } = useI18n()
  const [attempt, setAttempt] = useState<ExamAttempt | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  // by default only mistakes (if any): correct answers do not stretch the review
  const [showErrors, setShowErrors] = useState<boolean | null>(null)

  useEffect(() => {
    api.examAttempt(attemptId)
      .then((a) => (a.arkusz.part === 'pisanie' || a.arkusz.part === 'mowienie' ? navigate(links.examRun(a.id)) : setAttempt(a)))
      .catch((e: unknown) => setError(errorText(e)))
  }, [attemptId, errorText])

  async function accept(itemId: string) {
    setBusy(itemId)
    try {
      setAttempt(await api.acceptAnswer(attemptId, itemId))
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(null)
    }
  }

  if (error && !attempt) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!attempt) return <Loader label={t('result.loading')} />
  if (!attempt.result) return <p className="muted">{t('result.notSubmitted')} <a href={links.examRun(attempt.id)}>{t('result.continueExam')}</a></p>

  const { arkusz, result } = attempt
  const needsReview = result.items.filter((i) => i.verdict === 'needsReview').length
  const errorCount = result.items.filter((i) => isError(i.verdict)).length
  const wrongCount = result.items.filter((i) => i.verdict === 'wrong' && i.value.trim() !== '').length
  const onlyErrors = showErrors ?? errorCount > 0
  const setOnlyErrors = (v: boolean) => setShowErrors(v)

  return (
    <>
      <header className="header">
        <h1>{t('result.title', { code: arkusz.code })}</h1>
        <p className="muted small">{t('result.submittedAt', { title: arkusz.title, date: formatDateTime(attempt.submittedAt!) })}</p>
      </header>

      {error && <p className="error">{t('common.errorPrefix', { message: error })}</p>}

      <section className={`score ${result.isPassed ? 'score--pass' : 'score--fail'}`}>
        <div className="score__main">
          <span className="score__points">{formatPoints(result.points)} / {formatPoints(result.maxPoints)}</span>
          <span className="score__percent">{formatPercent(result.percent)}</span>
        </div>
        <div className="score__verdict">
          {result.isPassed ? t('result.passed', { threshold: formatPercent(result.passThresholdPercent) }) : t('result.failed', { threshold: formatPercent(result.passThresholdPercent) })}
          {needsReview > 0 && <span className="muted small">{t('result.needsReview', { count: needsReview })}</span>}
        </div>
        <table className="score-table">
          <thead>
            <tr>{result.tasks.map((ts) => <th key={ts.number}>{ts.romanNumber}</th>)}<th>{t('result.total')}</th></tr>
          </thead>
          <tbody>
            <tr>
              {result.tasks.map((ts) => (
                <td key={ts.number} className={ts.points === ts.maxPoints ? 'cell--full' : ts.points === 0 ? 'cell--zero' : ''}>
                  {formatPoints(ts.points)}<span className="muted">/{formatPoints(ts.maxPoints)}</span>
                </td>
              ))}
              <td><strong>{formatPoints(result.points)}</strong><span className="muted">/{formatPoints(result.maxPoints)}</span></td>
            </tr>
          </tbody>
        </table>
      </section>

      {wrongCount > 0 && arkusz.part === 'gramatyka' && (
        <p className="result-note">{t('result.bankedAll', { count: wrongCount })} <a href={links.today}>{t('result.toMistakes')}</a></p>
      )}

      <div className="segmented result-filter" role="group" aria-label={t('result.filter')}>
        <button type="button" aria-pressed={onlyErrors} className={`segmented__btn ${onlyErrors ? 'segmented__btn--on' : ''}`} onClick={() => setOnlyErrors(true)}>
          {t('result.filterErrors', { count: errorCount })}
        </button>
        <button type="button" aria-pressed={!onlyErrors} className={`segmented__btn ${!onlyErrors ? 'segmented__btn--on' : ''}`} onClick={() => setOnlyErrors(false)}>
          {t('result.filterAll', { count: result.items.length })}
        </button>
      </div>
      {onlyErrors && errorCount === 0 && <p className="muted">{t('result.noErrors')}</p>}

      {arkusz.tasks.map((task) => {
        const items = result.items.filter((i) => i.taskNumber === task.number && (!onlyErrors || isError(i.verdict)))
        if (items.length === 0) return null
        const score = result.tasks.find((ts) => ts.number === task.number)!
        return (
          <section key={task.id} className="result-task">
            <h2 className="result-task__title">
              <span>{task.romanNumber}. {task.instruction}</span>
              <span className="result-task__points">{formatPoints(score.points)} / {formatPoints(score.maxPoints)} p.</span>
            </h2>
            <ol className="result-rows">
              {items.map((item) => {
                const context = gapSentence(task.text, item.number)
                const error = isError(item.verdict)
                return (
                  <li key={item.itemId} className={`result-row ${error ? 'result-row--error' : ''}`}>
                    <span className={`result-row__mark result-row__mark--${item.verdict}`} title={t(VERDICT_KEY[item.verdict])} aria-label={t(VERDICT_KEY[item.verdict])}>
                      {item.verdict === 'wrong' ? '✗' : item.verdict === 'needsReview' ? '?' : item.verdict === 'none' ? '–' : '✓'}
                    </span>
                    <div className="result-row__main">
                      {item.prompt && !context && <div className="result-row__prompt"><span className="result-row__n">{item.number}.</span> <TaskText text={item.prompt} />{item.hint && <> <em className="muted">{item.hint}</em></>}</div>}
                      {context && (
                        <p className="result-row__context">
                          <span className="result-row__n">{item.number}.</span>{' '}
                          {context.split('___').map((part, j) => <span key={j}>{j > 0 && <span className="result-row__gap">{item.value || '___'}</span>}{part}</span>)}
                          {item.prompt && <span className="muted small"> ({item.prompt})</span>}
                        </p>
                      )}
                      <p className="result-row__answer">
                        {!item.prompt && !context && <span className="result-row__n">{item.number}.</span>}
                        {error
                          ? <><span className="wrong">{item.value || '—'}</span><span className="muted" aria-hidden="true">→</span><span className="correct">{item.answerKey.join(' / ')}</span></>
                          : <span className="correct">{item.value || '—'}</span>}
                        {item.verdict === 'accepted' && <span className="tag tag--quiet">{t('verdict.accepted')}</span>}
                      </p>
                      {error && item.explanation && <p className="muted small">{t('result.commissionScoring')}: {item.explanation}</p>}
                      {item.verdict === 'needsReview' && (
                        <div><button className="btn btn--pass btn--sm" disabled={busy === item.itemId} onClick={() => accept(item.itemId)}>{t('result.accept')}</button></div>
                      )}
                    </div>
                    <span className="result-row__pts muted small">{formatPoints(item.points)}/{formatPoints(item.maxPoints)}</span>
                  </li>
                )
              })}
            </ol>
          </section>
        )
      })}

      {result.transcripts.length > 0 && (
        <section className="task">
          <h2 className="task__title">{t('result.transcripts')}</h2>
          {result.transcripts.map((tr) => (
            <details key={tr.taskNumber}><summary>Zadanie {tr.taskRomanNumber}</summary><p className="audio__text">{tr.text}</p></details>
          ))}
        </section>
      )}

      <BackRow>
        {attempt.sessionId && <BackLink href={links.session(attempt.sessionId)}>{t('result.toSession')}</BackLink>}
        <BackLink href={links.examList}>{t('result.toList')}</BackLink>
      </BackRow>
    </>
  )
}
