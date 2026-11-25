import { useState } from 'react'
import { compressImage } from '../imageCompress'
import { api, type FeedbackTopic } from '../api'
import { useI18n } from '../i18n'
import type { Key } from '../i18n/core'

const TOPICS: { id: FeedbackTopic; label: Key }[] = [
  { id: 'question', label: 'feedback.topic.question' },
  { id: 'bug', label: 'feedback.topic.bug' },
  { id: 'content', label: 'feedback.topic.content' },
  { id: 'idea', label: 'feedback.topic.idea' },
  { id: 'data', label: 'feedback.topic.data' },
  { id: 'other', label: 'feedback.topic.other' },
]

const MAX = 4000
const MAX_IMAGES = 3

/** "Contact" form: the message is saved in the database, the reply goes to the given e-mail or the account address. */
export function FeedbackForm() {
  const { t, lang, errorText } = useI18n()
  const [topic, setTopic] = useState<FeedbackTopic>('question')
  const [message, setMessage] = useState('')
  const [email, setEmail] = useState('')
  const [website, setWebsite] = useState('')
  const [state, setState] = useState<'idle' | 'sending' | 'sent'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [images, setImages] = useState<string[]>([])

  // images: from the file picker or by pasting a screenshot (Ctrl+V) into the message field
  async function addImages(files: Iterable<File>) {
    const picked = [...files].filter((f) => f.type.startsWith('image/')).slice(0, MAX_IMAGES - images.length)
    if (picked.length === 0) return
    setError(null)
    try {
      const added = await Promise.all(picked.map((f) => compressImage(f)))
      setImages((cur) => [...cur, ...added].slice(0, MAX_IMAGES))
    } catch {
      setError(t('feedback.imageError'))
    }
  }

  async function send(e: React.FormEvent) {
    e.preventDefault()
    if (message.trim().length < 5) { setError(t('feedback.tooShort')); return }
    setState('sending')
    setError(null)
    try {
      await api.sendFeedback({ topic, message: message.trim(), email: email.trim() || null, language: lang, website, images })
      setState('sent')
    } catch (err) {
      setError(errorText(err))
      setState('idle')
    }
  }

  if (state === 'sent') {
    return (
      <div className="feedback feedback--sent" role="status">
        <strong>{t('feedback.sentTitle')}</strong>
        <p className="muted">{t('feedback.sentText')}</p>
        <button type="button" className="btn btn--sm" onClick={() => { setMessage(''); setImages([]); setState('idle') }}>{t('feedback.another')}</button>
      </div>
    )
  }

  return (
    <form className="feedback" onSubmit={(e) => void send(e)} noValidate>
      <label className="feedback__row">
        <span className="feedback__label">{t('feedback.topic')}</span>
        <select className="field" value={topic} onChange={(e) => setTopic(e.target.value as FeedbackTopic)}>
          {TOPICS.map((tp) => <option key={tp.id} value={tp.id}>{t(tp.label)}</option>)}
        </select>
      </label>
      <label className="feedback__row">
        <span className="feedback__label">{t('feedback.message')}</span>
        <textarea className="field feedback__text" rows={6} maxLength={MAX} value={message} onChange={(e) => setMessage(e.target.value)} placeholder={t('feedback.messageHint')} required
          onPaste={(e) => { const files = [...e.clipboardData.files]; if (files.some((f) => f.type.startsWith('image/'))) { e.preventDefault(); void addImages(files) } }} />
        <span className="muted small feedback__count">{message.length} / {MAX}</span>
      </label>
      <div className="feedback__row">
        <span className="feedback__label">{t('feedback.images', { max: MAX_IMAGES })}</span>
        <div className="feedback__images">
          {images.map((src, i) => (
            <figure key={i} className="feedback__thumb">
              <img src={src} alt="" />
              <button type="button" className="feedback__remove" aria-label={t('feedback.removeImage')} title={t('feedback.removeImage')}
                onClick={() => setImages((cur) => cur.filter((_, j) => j !== i))}>×</button>
            </figure>
          ))}
          {images.length < MAX_IMAGES && (
            <label className="feedback__add">
              <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(e) => { if (e.target.files) void addImages(e.target.files); e.target.value = '' }} />
              <span aria-hidden="true">+</span>{t('feedback.addImage')}
            </label>
          )}
        </div>
        <span className="muted small">{t('feedback.imagesHint')}</span>
      </div>
      <label className="feedback__row">
        <span className="feedback__label">{t('feedback.email')}</span>
        <input className="field" type="email" autoComplete="email" maxLength={256} value={email} onChange={(e) => setEmail(e.target.value)} placeholder="name@example.com" />
        <span className="muted small">{t('feedback.emailHint')}</span>
      </label>
      {/* bot trap: invisible to people */}
      <input className="feedback__trap" tabIndex={-1} autoComplete="off" aria-hidden="true" value={website} onChange={(e) => setWebsite(e.target.value)} name="website" />
      {error && <p className="error small">{error}</p>}
      <p className="muted small">{t('feedback.privacy')}</p>
      <div><button type="submit" className="btn btn--primary" disabled={state === 'sending'}>{state === 'sending' ? t('feedback.sending') : t('feedback.send')}</button></div>
    </form>
  )
}
