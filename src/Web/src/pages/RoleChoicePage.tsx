import { useState } from 'react'
import { api, type Me } from '../api'
import { todayIso } from '../format'
import { useI18n } from '../i18n'
import { LANGS, LANG_NAME } from '../i18n/core'
import LevelPicker from '../components/LevelPicker'

type Step = 'language' | 'role' | 'goal'

/**
 * First sign-in: language → role → (student) goal, i.e. level and exam date. All of it can be changed later in the profile and dashboard.
 * People arriving through an invite link do not see this screen.
 */
export default function RoleChoicePage({ me, onMe, onDone }: { me: Me; onMe: (m: Me) => void; onDone: () => void }) {
  const { t, errorText, lang, setLang } = useI18n()
  const [step, setStep] = useState<Step>('language')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [examDate, setExamDate] = useState('')
  const [today] = useState(todayIso)
  const stepNumber = step === 'language' ? 1 : step === 'role' ? 2 : 3

  async function choose(role: 'Student' | 'Teacher') {
    setBusy(true)
    setError(null)
    try {
      await api.chooseRole(role)
      onMe({ ...me, role, roleChosen: true })
      if (role === 'Teacher') onDone()
      else setStep('goal')
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  async function finish() {
    setBusy(true)
    setError(null)
    try {
      if (examDate) {
        await api.setExamDate(examDate)
        onMe({ ...me, examDate })
      }
      onDone()
    } catch (e) {
      setError(errorText(e))
      setBusy(false)
    }
  }

  return (
    <>
      <p className="muted small onboarding__step">{t('onboarding.step', { n: stepNumber, total: 3 })}</p>

      {step === 'language' && (
        <>
          <header className="header">
            <h1>{t('onboarding.langTitle')}</h1>
            <p className="muted">{t('onboarding.langHint')}</p>
          </header>
          <div className="lang-choice">
            {LANGS.map((l) => (
              <button key={l} type="button" lang={l} className={`lang-card ${lang === l ? 'lang-card--on' : ''}`}
                onClick={() => { setLang(l); setStep('role') }}>
                {LANG_NAME[l]}
              </button>
            ))}
          </div>
        </>
      )}

      {step === 'role' && (
        <>
          <header className="header">
            <h1>{t('role.title')}</h1>
            <p className="muted">{t('role.subtitle')}</p>
          </header>
          <div className="role-choice">
            <button type="button" className="role-card" disabled={busy} onClick={() => void choose('Student')}>
              <strong className="role-card__title">{t('role.student')}</strong>
              <span className="muted">{t('role.studentHint')}</span>
            </button>
            <button type="button" className="role-card" disabled={busy} onClick={() => void choose('Teacher')}>
              <strong className="role-card__title">{t('role.teacher')}</strong>
              <span className="muted">{t('role.teacherHint')}</span>
            </button>
          </div>
          <button type="button" className="btn btn--ghost" onClick={() => setStep('language')}>{t('onboarding.back')}</button>
        </>
      )}

      {step === 'goal' && (
        <>
          <header className="header">
            <h1>{t('onboarding.goalTitle')}</h1>
            <p className="muted">{t('onboarding.goalHint')}</p>
          </header>
          <section className="task onboarding__goal">
            <span className="small-title">{t('onboarding.level')}</span>
            <LevelPicker me={me} onChange={onMe} />
            <label className="small-title" htmlFor="onboarding-date">{t('cabinet.examDate')}</label>
            <input id="onboarding-date" className="field" type="date" min={today} value={examDate} onChange={(e) => setExamDate(e.target.value)} />
            <p className="muted small">{t('onboarding.dateHint')}</p>
          </section>
          <div className="actions">
            <button type="button" className="btn btn--primary" disabled={busy} onClick={() => void finish()}>{t('onboarding.done')}</button>
            {!examDate && <button type="button" className="btn btn--ghost" disabled={busy} onClick={onDone}>{t('onboarding.skip')}</button>}
          </div>
        </>
      )}

      {error && <p className="error">{t('common.errorPrefix', { message: error })}</p>}
    </>
  )
}
