import { useState, type FormEvent, type ReactNode } from 'react'
import { authUrls } from '../api'
import { useI18n } from '../i18n'
import { useSite } from '../site'
import type { Key } from '../i18n/core'

const icon = (d: string): ReactNode => (
  <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true">
    <path d={d} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
)

const FEATURES: { icon: ReactNode; title: Key; text: Key }[] = [
  { icon: icon('M12 7v5l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z'), title: 'login.f1Title', text: 'login.f1Text' },
  { icon: icon('M4 20h4L19 9l-4-4L4 16v4ZM13.5 6.5l4 4'), title: 'login.f2Title', text: 'login.f2Text' },
  { icon: icon('M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM12 12h.01'), title: 'login.f3Title', text: 'login.f3Text' },
  { icon: icon('M4 5a2 2 0 0 1 2-2h12v16H6a2 2 0 0 0-2 2V5ZM4 19a2 2 0 0 1 2-2h12M9 7h5'), title: 'login.f4Title', text: 'login.f4Text' },
]

/** Google logo per the brand guidelines: the colored "G" on a light button. */
function GoogleLogo() {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
      <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
      <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
      <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
    </svg>
  )
}

const DEMO_SITE = 'https://egzamin-b1.pl'
const DEV_DEFAULT_EMAIL = 'student@example.test'

export default function LoginPage() {
  const { t } = useI18n()
  const site = useSite()
  const returnUrl = `/${window.location.hash}`

  return (
    <section className="landing">
      <div className="landing__top">
        <div className="landing__hero">
          <p className="landing__eyebrow">{t('login.eyebrow')}</p>
          <h1 className="landing__title">{t('login.headline')}</h1>
          <p className="landing__lead">{t('login.lead')}</p>
          <div className="landing__cta">
            <a className="btn btn--google" href={authUrls.google(returnUrl)}>
              <GoogleLogo />{t('login.google')}
            </a>
            <a className="landing__demo" href={DEMO_SITE} target="_blank" rel="noopener noreferrer">{t('login.demoLink')}</a>
          </div>
        </div>
        {site.devLogin && <DevLogin returnUrl={returnUrl} />}
      </div>

      <ul className="landing__features">
        {FEATURES.map((f) => (
          <li key={f.title} className="landing__feature">
            <span className="landing__icon">{f.icon}</span>
            <strong>{t(f.title)}</strong>
            <span className="muted">{t(f.text)}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** Development-only sign-in: shown when the API reports devLogin, it sends the browser to /auth/dev-login. */
function DevLogin({ returnUrl }: { returnUrl: string }) {
  const { t } = useI18n()
  const [email, setEmail] = useState(DEV_DEFAULT_EMAIL)

  function submit(e: FormEvent) {
    e.preventDefault()
    const value = email.trim()
    if (value) window.location.assign(authUrls.devLogin(value, returnUrl))
  }

  return (
    <form className="dev-login" onSubmit={submit}>
      <p className="dev-login__title">{t('login.devTitle')}</p>
      <label className="dev-login__field">
        <span>{t('login.devEmail')}</span>
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="off" required />
      </label>
      <button type="submit" className="btn">{t('login.devSubmit')}</button>
    </form>
  )
}
