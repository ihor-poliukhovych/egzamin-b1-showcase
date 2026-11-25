import { useEffect, useRef, useState } from 'react'
import { api, type DialogInfo, type DialogLine } from '../api'
import { useI18n } from '../i18n'
import { Loader } from './Loader'
import { SpeechRecorder } from './SpeechRecorder'

/** Speaks a line with the browser's Polish voice (free); without such a voice only the text is shown. */
function speak(text: string) {
  const synth = window.speechSynthesis
  if (!synth) return
  synth.cancel()
  const u = new SpeechSynthesisUtterance(text)
  u.lang = 'pl-PL'
  const voice = synth.getVoices().find((v) => v.lang.toLowerCase().startsWith('pl'))
  if (voice) u.voice = voice
  u.rate = 0.95
  synth.speak(u)
}

/**
 * Mówienie task 3 as in the exam: the examiner plays a role, the candidate answers by voice (the transcript can be corrected),
 * and the partner follows the situation's script, adapting the lines to what was said.
 */
export function DialogPanel({ attemptId, item, info, lines, readOnly, onLines }: {
  attemptId: string
  item: number
  info: DialogInfo
  lines: DialogLine[]
  readOnly: boolean
  onLines: (lines: DialogLine[]) => void
}) {
  const { t, errorText } = useI18n()
  const [draft, setDraft] = useState('')
  const [editing, setEditing] = useState(false)
  const [waiting, setWaiting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const endRef = useRef<HTMLLIElement>(null)
  const examinerLines = lines.filter((l) => l.who === 'examiner').length
  // the examiner has said everything in the script; the candidate gets the last line to close the conversation
  const examinerFinished = examinerLines >= info.turns
  const done = examinerFinished && lines.at(-1)?.who === 'candidate'
  const canSpeak = typeof window !== 'undefined' && 'speechSynthesis' in window

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }) }, [lines.length, waiting])
  useEffect(() => () => window.speechSynthesis?.cancel(), [])

  async function ask(history: DialogLine[]) {
    setWaiting(true)
    setError(null)
    try {
      const turn = await api.dialogTurn(attemptId, item, history)
      if (turn.line) {
        onLines([...history, { who: 'examiner', text: turn.line }])
        speak(turn.line)
      }
    } catch (e) {
      setError(errorText(e))
    } finally {
      setWaiting(false)
    }
  }

  function send() {
    const text = draft.trim()
    if (!text || waiting) return
    const next: DialogLine[] = [...lines, { who: 'candidate', text }]
    onLines(next)
    setDraft('')
    setEditing(false)
    if (!examinerFinished) void ask(next)
  }

  const needsStart = lines.length === 0 && info.examinerOpens

  return (
    <div className="dialog">
      {(lines.length > 0 || waiting) && (
        <ol className="dialog__lines">
          {lines.map((l, i) => (
            <li key={i} className={`bubble bubble--${l.who}`}>
              <span className="bubble__who">{l.who === 'examiner' ? t('speak.dialog.examiner') : t('speak.dialog.you')}</span>
              <span className="bubble__text">{l.text}</span>
              {l.who === 'examiner' && canSpeak && (
                <button type="button" className="bubble__play" onClick={() => speak(l.text)} aria-label={t('speak.dialog.replay')} title={t('speak.dialog.replay')}>
                  <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true"><path d="M11 5 6 9H3v6h3l5 4V5ZM15.5 8.5a5 5 0 0 1 0 7M18.5 5.5a9 9 0 0 1 0 13" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
                </button>
              )}
            </li>
          ))}
          {waiting && <li className="bubble bubble--examiner bubble--typing"><Loader inline label={t('speak.dialog.thinking')} /></li>}
          <li ref={endRef} className="dialog__end" aria-hidden="true" />
        </ol>
      )}

      {error && <p className="error small">{error}</p>}

      {!readOnly && needsStart && (
        <div><button type="button" className="btn btn--primary" disabled={waiting} onClick={() => void ask([])}>{t('speak.dialog.start')}</button></div>
      )}

      {!readOnly && !needsStart && !done && (
        <div className="dialog__reply">
          {examinerFinished && <p className="dialog__hint small">{t('speak.dialog.closeHint')}</p>}
          {/* as in tasks 1–2: a large microphone; the line appears after speaking and can be corrected before sending */}
          <SpeechRecorder variant="big" onAppend={(text) => setDraft((d) => (d ? `${d} ${text}` : text))} disabled={waiting} />
          {editing || draft.trim() !== '' ? (
            <div className="speak-transcript">
              <div className="speak-transcript__head">
                <span className="speak-transcript__label">{t('speak.dialog.yourLine')}</span>
                {!editing && <button type="button" className="btn btn--sm btn--plain" onClick={() => setEditing(true)}>{t('speak.edit')}</button>}
              </div>
              {editing
                ? <textarea className="writing-area dialog__draft" rows={3} value={draft} placeholder={t('speak.dialog.draft')} autoFocus
                    onChange={(e) => setDraft(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) send() }} />
                : <p className="speak-transcript__text">{draft}</p>}
              <div className="speak-transcript__actions">
                <button type="button" className="btn btn--primary" disabled={!draft.trim() || waiting} onClick={send}>{t('speak.dialog.reply')}</button>
              </div>
            </div>
          ) : (
            <p className="speak-transcript__empty muted small">{lines.length === 0 && !info.examinerOpens ? t('speak.dialog.youStartHint') : t('speak.dialog.empty')} <button type="button" className="link-btn" disabled={waiting} onClick={() => setEditing(true)}>{t('speak.typeInstead')}</button></p>
          )}
        </div>
      )}

      {done && <p className="dialog__done">{t('speak.dialog.done')}</p>}

    </div>
  )
}
