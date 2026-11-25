import { lazy, Suspense, useCallback, useEffect, useState } from 'react'
import { api, ApiError, UNAUTHORIZED_EVENT, type Me } from './api'
import ExamListPage from './pages/ExamListPage'
import { StudentCabinet, StudentCardPage, TeacherCabinet } from './pages/Cabinet'
import { GroupPage, GroupsPage, JoinPage, ProfilePage } from './pages/GroupPages'
import LoginPage from './pages/LoginPage'
import ExamResultPage from './pages/ExamResultPage'
import ExamRunPage from './pages/ExamRunPage'
import SessionPage from './pages/SessionPage'
import TodayPage from './pages/TodayPage'
import { links, useRoute } from './router'
import Footer from './components/Footer'
import LangSwitch from './components/LangSwitch'
import { useI18n } from './i18n'
import { isLang, storedLang } from './i18n/core'
import { stopAudio } from './audioController'
import { Loader } from './components/Loader'
import { isArticle } from './knowledge'
import RoleChoicePage from './pages/RoleChoicePage'

// Legal texts (4 languages) and the feedback form live in their own chunk so the main bundle stays under 500 KB
const LegalPage = lazy(() => import('./pages/LegalPages'))
const KnowledgePage = lazy(() => import('./pages/KnowledgePage').then((m) => ({ default: m.KnowledgePage })))
const ArticlePage = lazy(() => import('./pages/KnowledgePage').then((m) => ({ default: m.ArticlePage })))

/** undefined = still loading, null = signed out, 'error' = the API could not be reached (network or 5xx). */
type Session = Me | null | undefined | 'error'

export default function App() {
  const { t, lang, setLang, errorText } = useI18n()
  const route = useRoute()
  const [me, setMe] = useState<Session>(undefined)
  const [bootError, setBootError] = useState<unknown>(null)
  const [onboarding, setOnboarding] = useState(false)

  const loadMe = useCallback(() => api.me().then(
    (m) => {
      if (!m.roleChosen) setOnboarding(true)
      // an explicit choice on this device beats the profile; on a new device the profile language is used
      if (isLang(m.uiLanguage) && !storedLang()) setLang(m.uiLanguage)
      setMe(m)
    },
    (e: unknown) => {
      // 401 simply means "not signed in"; anything else is a real failure and must not look like the login page
      if (e instanceof ApiError && e.status === 401) setMe(null)
      else { setBootError(e); setMe('error') }
    },
  ), [setLang])

  useEffect(() => {
    void loadMe()
    const onUnauthorized = () => { stopAudio(); setMe(null) }
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [loadMe])

  // once signed in, the language lives in the profile: persist every change
  const user = typeof me === 'object' ? me : null
  useEffect(() => {
    if (!user || user.uiLanguage === lang) return
    api.updateSettings(user.targetLevel, user.targetVariant, lang)
      .then(() => setMe((cur) => (cur && typeof cur === 'object' ? { ...cur, uiLanguage: lang } : cur)))
      .catch(() => { /* the profile keeps the old language; the device choice still applies */ })
  }, [user, lang])

  async function logout() {
    stopAudio()
    try {
      await api.logout()
    } finally {
      setMe(null)
    }
  }

  function retry() {
    setBootError(null)
    setMe(undefined)
    void loadMe()
  }

  return (
    <div className="site">
      {me === undefined ? <main className="page"><Loader label={t('common.loading')} /></main>
        : me === 'error' ? (
          <main className="page">
            <section className="task boot-error" role="alert">
              <h1>{t('boot.error')}</h1>
              <p className="error">{errorText(bootError)}</p>
              <div className="actions"><button type="button" className="btn btn--primary" onClick={retry}>{t('common.retry')}</button></div>
            </section>
          </main>
        )
        : me === null ? <PublicShell />
        : (onboarding || !me.roleChosen) && route.page !== 'join' && route.page !== 'legal'
          ? <main className="page"><RoleChoicePage me={me} onMe={setMe} onDone={() => { setOnboarding(false); void loadMe() }} /></main>
        : <Shell me={me} onMe={setMe} onLogout={logout} />}
      <Footer />
    </div>
  )
}

/** Signed-out visitors only get the login screen and the legal pages. */
function PublicShell() {
  const route = useRoute()
  const { t } = useI18n()
  if (route.page !== 'legal') {
    return (
      <main className="page">
        <div className="public-bar"><LangSwitch /></div>
        <LoginPage />
      </main>
    )
  }
  return (
    <main className="page">
      <nav className="topnav">
        <a href="#/" className="topnav__link">{t('nav.signIn')}</a>
        <LangSwitch className="topnav__lang" />
      </nav>
      <Suspense fallback={<Loader label={t('common.loading')} />}><LegalPage slug={route.slug} /></Suspense>
    </main>
  )
}

function Shell({ me, onMe, onLogout }: { me: Me; onMe: (m: Me) => void; onLogout: () => void }) {
  const route = useRoute()
  const { t } = useI18n()
  const current = route.page === 'home' || route.page === 'groups' || route.page === 'group' || route.page === 'student-card' ? 'home' : route.page === 'today' ? 'today' : route.page === 'knowledge' || route.page === 'article' ? 'knowledge' : route.page === 'exam-list' || route.page === 'exam-run' || route.page === 'exam-result' || route.page === 'session' ? 'exam' : 'other'
  const teacherOnly = <p className="error">{t('common.teachersOnly')}</p>

  return (
    <main className="page">
      <nav className={`topnav ${route.page === 'exam-run' ? 'topnav--focus' : ''}`}>
        <div className="topnav__links">
        <a href={links.home} className={current === 'home' ? 'topnav__link topnav__link--on' : 'topnav__link'} aria-current={current === 'home' ? 'page' : undefined}>
          <svg className="topnav__icon" width="22" height="22" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 11l8-6 8 6v8h-5v-5H9v5H4z" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" /></svg>
          <span>{t('nav.cabinet')}</span>
        </a>
        <a href={links.examList} className={current === 'exam' ? 'topnav__link topnav__link--on' : 'topnav__link'} aria-current={current === 'exam' ? 'page' : undefined}>
          <svg className="topnav__icon" width="22" height="22" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 4h12v16H6zM9 9h6M9 13h6" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" /></svg>
          <span>{t('nav.exam')}</span>
        </a>
        <a href={links.today} className={current === 'today' ? 'topnav__link topnav__link--on' : 'topnav__link'} aria-current={current === 'today' ? 'page' : undefined}>
          <svg className="topnav__icon" width="22" height="22" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4v10M12 18v2" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" /></svg>
          <span className="topnav__full">{t('nav.blitz')}</span><span className="topnav__short">{t('nav.blitzShort')}</span>
        </a>
        <a href={links.knowledge} className={current === 'knowledge' ? 'topnav__link topnav__link--on' : 'topnav__link'} aria-current={current === 'knowledge' ? 'page' : undefined}>
          <svg className="topnav__icon" width="22" height="22" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5.5C6.5 4 9.5 4 12 6c2.5-2 5.5-2 8-.5V19c-2.5-1.5-5.5-1.5-8 .5-2.5-2-5.5-2-8-.5zM12 6v13.5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" /></svg>
          <span className="topnav__full">{t('nav.knowledge')}</span><span className="topnav__short">{t('nav.knowledgeShort')}</span>
        </a>
        </div>
        <div className="topnav__side">
          <a href={links.profile} className="topnav__user muted small">{me.displayName}{me.role === 'Teacher' ? t('nav.teacherSuffix') : ''}</a>
          <button className="btn btn--icon" onClick={onLogout} aria-label={t('nav.logout')} title={t('nav.logout')}>
            <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true"><path d="M14 5h4a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1h-4M10 16l-4-4 4-4M6 12h9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
          </button>
        </div>
      </nav>
      {route.page === 'home' && (me.role === 'Teacher' ? <TeacherCabinet me={me} onMe={onMe} /> : <StudentCabinet me={me} onMe={onMe} />)}
      {route.page === 'student-card' && (me.role === 'Teacher' ? <StudentCardPage groupId={route.groupId} studentId={route.studentId} me={me} onMe={onMe} /> : teacherOnly)}
      {route.page === 'today' && <TodayPage />}
      {route.page === 'exam-list' && <ExamListPage me={me} onMe={onMe} />}
      {route.page === 'exam-run' && <ExamRunPage key={route.attemptId} attemptId={route.attemptId} />}
      {route.page === 'exam-result' && <ExamResultPage key={route.attemptId} attemptId={route.attemptId} />}
      {route.page === 'session' && <SessionPage key={route.sessionId} sessionId={route.sessionId} />}
      {route.page === 'groups' && (me.role === 'Teacher' ? <GroupsPage /> : teacherOnly)}
      {route.page === 'group' && (me.role === 'Teacher' ? <GroupPage key={route.groupId} groupId={route.groupId} /> : teacherOnly)}
      {route.page === 'join' && <JoinPage key={route.token} token={route.token} />}
      {(route.page === 'knowledge' || route.page === 'article') && (
        <Suspense fallback={<Loader label={t('knowledge.loading')} />}>
          {route.page === 'article' && isArticle(route.slug) ? <ArticlePage slug={route.slug} /> : <KnowledgePage />}
        </Suspense>
      )}
      {route.page === 'profile' && <ProfilePage name={me.displayName} email={me.email} role={me.role} onLogout={onLogout} onMe={onMe} />}
      {route.page === 'legal' && <Suspense fallback={<Loader label={t('common.loading')} />}><LegalPage slug={route.slug} /></Suspense>}
    </main>
  )
}
