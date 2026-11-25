import { useEffect, useRef, useState } from 'react'
import { useI18n } from '../i18n'

// The Web Speech API is not in lib.dom, so only the parts we use are declared here
interface RecognitionResult { isFinal: boolean; 0: { transcript: string } }
interface RecognitionEvent { resultIndex: number; results: ArrayLike<RecognitionResult> }
interface Recognition {
  lang: string; continuous: boolean; interimResults: boolean
  onresult: ((e: RecognitionEvent) => void) | null
  onerror: ((e: { error: string }) => void) | null
  onend: (() => void) | null
  start(): void; stop(): void
}
type RecognitionCtor = new () => Recognition

const FATAL_ERRORS = new Set(['not-allowed', 'service-not-allowed', 'network', 'audio-capture', 'language-not-supported'])
const ERROR_KEY: Record<string, 'speak.micDenied' | 'speak.speechNetwork' | 'speak.micBusy'> = {
  'not-allowed': 'speak.micDenied', 'service-not-allowed': 'speak.speechNetwork', network: 'speak.speechNetwork',
  'language-not-supported': 'speak.speechNetwork', 'audio-capture': 'speak.micBusy',
}

function recognitionCtor(): RecognitionCtor | null {
  const w = window as unknown as { SpeechRecognition?: RecognitionCtor; webkitSpeechRecognition?: RecognitionCtor }
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null
}

/** inline: a "Record" button with a status line; big: a large centered microphone (tasks 1–2); round: a small round one (dialog line field). */
export type RecorderVariant = 'inline' | 'big' | 'round'

const MicIcon = ({ size }: { size: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true">
    <path d="M12 3a3 3 0 0 0-3 3v6a3 3 0 0 0 6 0V6a3 3 0 0 0-3-3ZM5 11a7 7 0 0 0 14 0M12 18v3M8 21h8" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
)
const StopIcon = ({ size }: { size: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="6" width="12" height="12" rx="2.5" fill="currentColor" /></svg>
)

const clock = (sec: number) => `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`

/**
 * Records an answer: the browser's speech recognition (pl-PL, free, no server involved) appends text to the transcript,
 * while audio is recorded locally only, so the candidate can listen back. The transcript can be corrected by hand later.
 */
export function SpeechRecorder({ onAppend, disabled, variant = 'inline' }: { onAppend: (text: string) => void; disabled?: boolean; variant?: RecorderVariant }) {
  const { t } = useI18n()
  const [recording, setRecording] = useState(false)
  const [interim, setInterim] = useState('')
  const [audioUrl, setAudioUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const recognition = useRef<Recognition | null>(null)
  const recorder = useRef<MediaRecorder | null>(null)
  const stream = useRef<MediaStream | null>(null)
  const active = useRef(false)
  const interimRef = useRef('')
  const appendRef = useRef(onAppend)
  const mounted = useRef(true)
  useEffect(() => { appendRef.current = onAppend }, [onAppend])
  const supported = recognitionCtor() !== null
  const [startedAt, setStartedAt] = useState<number | null>(null)
  const [elapsed, setElapsed] = useState(0)

  useEffect(() => {
    if (startedAt === null) return
    const id = window.setInterval(() => setElapsed(Math.round((Date.now() - startedAt) / 1000)), 500)
    return () => window.clearInterval(id)
  }, [startedAt])

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false; active.current = false; recognition.current?.stop(); stopMedia() }
  }, [])
  useEffect(() => () => { if (audioUrl) URL.revokeObjectURL(audioUrl) }, [audioUrl])

  function stopMedia() {
    if (recorder.current && recorder.current.state !== 'inactive') recorder.current.stop()
    stream.current?.getTracks().forEach((tr) => tr.stop())
    stream.current = null
  }

  function startRecognition() {
    const Ctor = recognitionCtor()
    if (!Ctor) return
    const r = new Ctor()
    r.lang = 'pl-PL'
    r.continuous = true
    r.interimResults = true
    r.onresult = (e) => {
      let pending = ''
      for (let i = e.resultIndex; i < e.results.length; i++) {
        const res = e.results[i]
        if (res.isFinal) appendRef.current(res[0].transcript.trim())
        else pending += res[0].transcript
      }
      interimRef.current = pending
      setInterim(pending)
    }
    r.onerror = (e) => {
      if (e.error === 'no-speech' || e.error === 'aborted') return
      // without permission or a recognition service restarting is pointless: stop the loop and explain
      if (FATAL_ERRORS.has(e.error)) active.current = false
      setError(t(ERROR_KEY[e.error] ?? 'speak.speechError'))
    }
    r.onend = () => {
      // on stop the browser sometimes does not finalize the last phrase in time; keep it instead of losing it
      if (!active.current && interimRef.current.trim()) appendRef.current(interimRef.current.trim())
      interimRef.current = ''
      setInterim('')
      // Chrome stops recognition by itself after a pause; restart it while recording is still on
      if (active.current) { try { r.start() } catch { /* already running */ } }
    }
    recognition.current = r
    r.start()
  }

  async function start() {
    setError(null)
    active.current = true
    setRecording(true)
    setElapsed(0)
    setStartedAt(Date.now())
    try { startRecognition() } catch (e) { setError(String(e)) }
    try {
      const media = await navigator.mediaDevices.getUserMedia({ audio: true })
      // unmounted or stopped while the permission prompt was open: release the microphone right away
      if (!mounted.current || !active.current) { media.getTracks().forEach((tr) => tr.stop()); return }
      stream.current = media
      const chunks: Blob[] = []
      const rec = new MediaRecorder(media)
      rec.ondataavailable = (ev) => { if (ev.data.size > 0) chunks.push(ev.data) }
      rec.onstop = () => { if (chunks.length > 0) setAudioUrl(URL.createObjectURL(new Blob(chunks, { type: rec.mimeType || 'audio/webm' }))) }
      recorder.current = rec
      rec.start()
    } catch {
      if (!supported) { setError(t('speak.micDenied')); active.current = false; setRecording(false) }
    }
  }

  function stop() {
    active.current = false
    setRecording(false)
    setStartedAt(null)
    recognition.current?.stop()
    stopMedia()
  }

  if (variant !== 'inline') {
    const big = variant === 'big'
    return (
      <div className={`mic mic--${variant}`}>
        <button type="button" className={`mic__btn ${recording ? 'mic__btn--live' : ''}`} disabled={disabled && !recording}
          aria-pressed={recording} aria-label={recording ? t('speak.stop') : t('speak.record')} title={recording ? t('speak.stop') : t('speak.record')}
          onClick={() => (recording ? stop() : void start())}>
          {recording ? <StopIcon size={big ? 30 : 18} /> : <MicIcon size={big ? 34 : 20} />}
        </button>
        {big && <span className={`mic__hint ${recording ? 'mic__hint--live' : ''}`}>{recording ? t('speak.recordingFor', { time: clock(elapsed) }) : t('speak.tapToTalk')}</span>}
        {big && interim && <p className="muted small recorder__interim">{interim}</p>}
        {big && audioUrl && !recording && <audio controls src={audioUrl} className="recorder__audio" />}
        {big && !supported && <p className="muted small">{t('speak.noSpeechApi')}</p>}
        {error && <p className="error small mic__error">{error}</p>}
      </div>
    )
  }

  return (
    <div className="recorder">
      <div className="recorder__bar">
        {recording
          ? <button type="button" className="btn btn--fail" onClick={stop}>■ {t('speak.stop')}</button>
          : <button type="button" className="btn btn--primary" disabled={disabled} onClick={() => void start()}>● {t('speak.record')}</button>}
        {recording && <span className="recorder__live">{t('speak.recording')}</span>}
        {audioUrl && !recording && <audio controls src={audioUrl} className="recorder__audio" />}
      </div>
      {interim && <p className="muted small recorder__interim">{interim}</p>}
      {!supported && <p className="muted small">{t('speak.noSpeechApi')}</p>}
      {error && <p className="error small">{error}</p>}
    </div>
  )
}
