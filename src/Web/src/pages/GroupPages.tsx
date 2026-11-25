import { useCallback, useEffect, useState } from 'react'
import { api, type Group, type Me, type Invite, type MyGroup, type StudentStatus } from '../api'
import { formatDateTime, formatPercent, formatPoints } from '../format'
import { PART_LABEL, patternShort } from '../parts'
import { PART_SHORT } from '../examSets'
import { links, navigate } from '../router'
import { useI18n } from '../i18n'
import { BackLink, BackRow } from '../components/BackLink'
import { useConfirm } from '../components/confirm'
import LangSwitch from '../components/LangSwitch'
import { Loader } from '../components/Loader'
import { TrashIcon } from '../components/TrashIcon'
import { THEMES, useTheme, type Theme } from '../theme'
import type { Key } from '../i18n/core'


export function GroupsPage() {
  const { t, errorText } = useI18n()
  const [groups, setGroups] = useState<Group[] | null>(null)
  const [name, setName] = useState('')
  const [archived, setArchived] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    api.groups(archived).then(setGroups).catch((e: unknown) => setError(errorText(e)))
  }, [archived, errorText])
  useEffect(load, [load])

  async function create() {
    if (!name.trim()) return
    try {
      const group = await api.createGroup(name.trim())
      navigate(links.group(group.id))
    } catch (e) {
      setError(errorText(e))
    }
  }

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  return (
    <>
      <header className="header"><h1>{t('groups.title')}</h1></header>
      <div className="actions">
        <input className="free-item__input" placeholder={t('groups.newPlaceholder')} value={name} onChange={(e) => setName(e.target.value)} />
        <button className="btn btn--primary" onClick={create}>{t('groups.create')}</button>
      </div>
      <label className="small"><input type="checkbox" checked={archived} onChange={(e) => setArchived(e.target.checked)} /> {t('groups.showArchived')}</label>
      {groups === null ? <Loader label={t('common.loading')} /> : groups.length === 0 ? <p className="muted">{t('groups.none')}</p> : (
        <ul className="list">
          {groups.map((g) => (
            <li key={g.id} className="card card--row">
              <div><strong>{g.name}</strong>{g.archivedAt && <span className="badge badge--muted">{t('groups.archiveBadge')}</span>}</div>
              <a className="btn" href={links.group(g.id)}>{t('groups.open')}</a>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

export function GroupPage({ groupId }: { groupId: string }) {
  const { t, errorText } = useI18n()
  const confirm = useConfirm()
  const [group, setGroup] = useState<Group | null>(null)
  const [students, setStudents] = useState<StudentStatus[]>([])
  const [invites, setInvites] = useState<Invite[]>([])
  const [name, setName] = useState('')
  const [maxUses, setMaxUses] = useState('')
  const [days, setDays] = useState('14')
  const [copied, setCopied] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() =>
    Promise.all([api.group(groupId), api.groupStudents(groupId), api.groupInvites(groupId)])
      .then(([g, s, i]) => { setGroup(g); setName(g.name); setStudents(s); setInvites(i) })
      .catch((e: unknown) => setError(errorText(e))), [groupId, errorText])
  useEffect(() => { void load() }, [load])

  const act = (fn: () => Promise<unknown>) => async () => {
    try { await fn(); await load() } catch (e) { setError(errorText(e)) }
  }
  const inviteBody = () => ({ days: Number(days) || 14, maxUses: maxUses ? Number(maxUses) : null })
  const link = (token: string) => `${window.location.origin}/#/join/${token}`

  async function copy(token: string) {
    await navigator.clipboard.writeText(link(token))
    setCopied(token)
  }

  if (error) return <><BackRow><BackLink href={links.groups}>{t('groups.toGroups')}</BackLink></BackRow><p className="error">{t('common.errorPrefix', { message: error })}</p></>
  if (!group) return <Loader label={t('common.loading')} />
  const active = invites.find((i) => i.state === 'ok')

  return (
    <>
      <BackRow><BackLink href={links.groups}>{t('groups.back')}</BackLink></BackRow>
      <header className="header">
        <h1>{group.name}{group.archivedAt && t('groups.archivedSuffix')}</h1>
      </header>

      <section className="task">
        <h2 className="task__title">{t('groups.inviteTitle')}</h2>
        {active ? (
          <>
            <p className="invite-link">{link(active.token)}</p>
            <p className="muted small">{t('groups.inviteInfo', { date: formatDateTime(active.expiresAt), uses: active.uses, limit: active.maxUses ? t('groups.inviteLimit', { max: active.maxUses }) : '' })}</p>
            <div className="actions">
              <button className="btn btn--primary" onClick={() => copy(active.token)}>{copied === active.token ? t('groups.copied') : t('groups.copy')}</button>
              <button className="btn" onClick={act(() => api.reissueInvite(groupId, inviteBody()))}>{t('groups.reissue')}</button>
              <button className="btn btn--fail" onClick={act(() => api.revokeInvite(groupId, active.id))}>{t('groups.revoke')}</button>
            </div>
          </>
        ) : !group.archivedAt && (
          <>
            <p className="muted">{t('groups.noInvite')}</p>
            <div className="actions">
              <label className="small">{t('groups.days')} <input className="free-item__input narrow" value={days} onChange={(e) => setDays(e.target.value)} /></label>
              <label className="small">{t('groups.maxUses')} <input className="free-item__input narrow" value={maxUses} placeholder={t('groups.noLimit')} onChange={(e) => setMaxUses(e.target.value)} /></label>
              <button className="btn btn--primary" onClick={act(() => api.issueInvite(groupId, inviteBody()))}>{t('groups.issue')}</button>
            </div>
          </>
        )}
      </section>

      <section className="task">
        <h2 className="task__title">{t('groups.students', { count: students.length })}</h2>
        {students.length === 0 ? <p className="muted">{t('groups.noStudents')}</p> : (
          <ul className="students">
            {students.map((s) => (
              <li key={s.studentId} className="student">
                <div className="student__head">
                  <div className="student__who">
                    <strong>{s.displayName}</strong>
                    <span className="muted small">{s.email} · {s.lastActivityAt ? t('groups.lastActivity', { date: formatDateTime(s.lastActivityAt) }) : t('groups.noActivity')}</span>
                  </div>
                  <div className="student__actions">
                    <a className="btn btn--sm" href={links.studentCard(groupId, s.studentId)}>{t('groups.card')}</a>
                    <button className="btn btn--icon btn--sm btn--quiet" aria-label={t('groups.remove')} title={t('groups.remove')}
                      onClick={async () => { if (await confirm({ message: t('groups.confirmRemove', { name: s.displayName }), confirmLabel: t('groups.remove'), danger: true })) void act(() => api.removeStudent(groupId, s.studentId))() }}>
                      <TrashIcon />
                    </button>
                  </div>
                </div>
                <div className="mini-parts">
                  {s.parts.map((p) => (
                    <div key={p.kind} className={`mini-part ${p.isBelowThreshold ? 'mini-part--fail' : p.percent !== null ? 'mini-part--pass' : ''}`}
                      title={p.points === null ? PART_LABEL[p.kind] : `${PART_LABEL[p.kind]}: ${formatPoints(p.points)} / ${formatPoints(p.maxPoints)}`}>
                      <span className="mini-part__name">{PART_SHORT[p.kind]}</span>
                      <span className="mini-part__bar"><i style={{ width: `${Math.min(100, p.percent ?? 0)}%` }} /></span>
                      <span className="mini-part__val">{p.percent === null ? '—' : formatPercent(Math.round(p.percent))}</span>
                    </div>
                  ))}
                </div>
                {s.topPatterns.length > 0 && (
                  <div className="student__patterns">
                    <span className="muted small">{t('groups.colPatterns')}</span>
                    {s.topPatterns.slice(0, 3).map((p) => <span key={p.title} className="tag tag--quiet">{patternShort(p.title)} ×{p.fallsCount}</span>)}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="task">
        <h2 className="task__title">{t('groups.manage')}</h2>
        <div className="actions">
          <input className="free-item__input" value={name} onChange={(e) => setName(e.target.value)} />
          <button className="btn" onClick={act(() => api.renameGroup(groupId, name))}>{t('groups.rename')}</button>
          {!group.archivedAt && (
            <button className="btn btn--fail" onClick={async () => { if (await confirm({ message: t('groups.confirmArchive'), confirmLabel: t('groups.archive'), danger: true })) void act(() => api.archiveGroup(groupId))() }}>{t('groups.archive')}</button>
          )}
        </div>
      </section>
    </>
  )
}

export function JoinPage({ token }: { token: string }) {
  const { t, errorText } = useI18n()
  const [preview, setPreview] = useState<{ state: string; groupName: string | null; teacherName: string | null } | null>(null)
  const [consent, setConsent] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  useEffect(() => {
    api.joinPreview(token).then(setPreview).catch(() => setPreview({ state: 'NotFound', groupName: null, teacherName: null }))
  }, [token])

  async function join() {
    try {
      const outcome = await api.join(token, consent)
      if (outcome === 'Joined' || outcome === 'AlreadyMember') setDone(true)
      else setMessage(t(joinKey(outcome)))
    } catch (e) {
      setMessage(errorText(e))
    }
  }

  if (!preview) return <Loader label={t('join.loading')} />
  if (preview.state !== 'Joined' && preview.state !== 'AlreadyMember') return <p className="error">{t(joinKey(preview.state))}</p>
  if (done) return <section className="score score--pass"><p>{t('join.joined', { group: preview.groupName ?? '' })} <a href={links.profile}>{t('join.profile')}</a> · <a href={links.today}>{t('join.toPractice')}</a></p></section>

  return (
    <section className="task">
      <h1>{t('join.title', { group: preview.groupName ?? '' })}</h1>
      <p>{t('join.teacher')} <strong>{preview.teacherName}</strong></p>
      <label className="choice choice--block">
        <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} /> {t('join.consent')}
      </label>
      {message && <p className="error">{message}</p>}
      <button className="btn btn--primary" disabled={!consent} onClick={() => { void join() }}>{t('join.submit')}</button>
    </section>
  )
}

const JOIN_KEYS: Record<string, Key> = {
  NotFound: 'join.NotFound', Revoked: 'join.Revoked', Expired: 'join.Expired', Exhausted: 'join.Exhausted',
  Archived: 'join.Archived', OwnGroup: 'join.OwnGroup', ConsentRequired: 'join.ConsentRequired',
}

function joinKey(outcome: string): Key {
  return JOIN_KEYS[outcome] ?? 'join.invalid'
}

const THEME_KEY: Record<Theme, 'profile.themeSystem' | 'profile.themeLight' | 'profile.themeDark'> = { system: 'profile.themeSystem', light: 'profile.themeLight', dark: 'profile.themeDark' }

export function ProfilePage({ name, email, role, onLogout, onMe }: { name: string; email: string; role: string; onLogout: () => void; onMe: (m: Me) => void }) {
  const { t, errorText } = useI18n()
  const [theme, setTheme] = useTheme()
  const confirm = useConfirm()
  const [roleError, setRoleError] = useState<string | null>(null)

  async function switchRole(next: 'Student' | 'Teacher') {
    if (next === role) return
    if (!(await confirm({ message: next === 'Student' ? t('profile.confirmToStudent') : t('profile.confirmToTeacher') }))) return
    setRoleError(null)
    try {
      await api.changeRole(next)
      onMe(await api.me())
    } catch (e) {
      setRoleError(errorText(e))
    }
  }
  const [groups, setGroups] = useState<MyGroup[] | null>(null)
  const load = useCallback(() => { api.myGroups().then(setGroups).catch(() => setGroups([])) }, [])
  useEffect(load, [load])

  return (
    <>
      <header className="header"><h1>{t('profile.title')}</h1><p className="muted">{name} · {email} · {role === 'Teacher' ? t('profile.roleTeacher') : t('profile.roleStudent')}</p></header>
      <section className="task">
        <h2 className="task__title">{t('profile.language')}</h2>
        <LangSwitch full className="langswitch--profile" />
        <p className="muted small">{t('profile.languageHint')}</p>
      </section>
      <section className="task">
        <h2 className="task__title">{t('profile.theme')}</h2>
        <div className="segmented" role="group" aria-label={t('profile.theme')}>
          {THEMES.map((th) => (
            <button key={th} type="button" aria-pressed={theme === th} className={`segmented__btn ${theme === th ? 'segmented__btn--on' : ''}`} onClick={() => setTheme(th)}>
              {t(THEME_KEY[th])}
            </button>
          ))}
        </div>
      </section>
      <section className="task">
        <h2 className="task__title">{t('profile.role')}</h2>
        <div className="segmented" role="group" aria-label={t('profile.role')}>
          {(['Student', 'Teacher'] as const).map((r) => (
            <button key={r} type="button" className={`segmented__btn ${role === r ? 'segmented__btn--on' : ''}`} onClick={() => void switchRole(r)}>
              {r === 'Teacher' ? t('profile.roleTeacher') : t('profile.roleStudent')}
            </button>
          ))}
        </div>
        <p className="muted small">{t('profile.roleHint')}</p>
        {roleError && <p className="error small">{roleError}</p>}
      </section>
      <section className="task">
        <h2 className="task__title">{t('groups.title')}</h2>
        {groups === null ? <Loader label={t('common.loading')} /> : groups.length === 0 ? <p className="muted">{t('cabinet.noGroups')}</p> : (
          <ul className="list">
            {groups.map((g) => (
              <li key={g.groupId} className="card card--row">
                <div><strong>{g.name}</strong>{g.isArchived && t('groups.archivedSuffix')}<div className="muted small">{t('profile.groupMeta', { teacher: g.teacherName, date: formatDateTime(g.joinedAt) })}</div></div>
                <button className="btn" onClick={async () => { if (await confirm({ message: t('profile.confirmLeave', { name: g.name }), confirmLabel: t('profile.leaveGroup'), danger: true })) void api.leaveGroup(g.groupId).then(load) }}>{t('profile.leaveGroup')}</button>
              </li>
            ))}
          </ul>
        )}
      </section>
      <button className="btn" onClick={onLogout}>{t('profile.logout')}</button>
      <DeleteAccount email={email} />
    </>
  )
}

/** Account deletion (GDPR): confirmed by typing a word, then all data is erased on the server. */
function DeleteAccount({ email }: { email: string }) {
  const { t, errorText } = useI18n()
  const [open, setOpen] = useState(false)
  const [word, setWord] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const confirmWord = t('account.confirmWord')
  const ready = word.trim().toLocaleUpperCase() === confirmWord

  async function remove() {
    setBusy(true)
    setError(null)
    try {
      await api.deleteAccount()
      window.location.assign('/')
    } catch (e) {
      setError(errorText(e))
      setBusy(false)
    }
  }

  return (
    <section className="danger-zone">
      <h2 className="task__title">{t('account.title')}</h2>
      <p className="muted small">{t('account.warning', { email })} <a href={links.legal('prywatnosc')}>{t('footer.privacy')}</a></p>
      {!open ? <button className="btn btn--fail" onClick={() => setOpen(true)}>{t('account.open')}</button> : (
        <div className="danger-zone__confirm">
          <label className="small">{t('account.typeToConfirm', { word: confirmWord })}
            <input className="free-item__input" value={word} onChange={(e) => setWord(e.target.value)} autoComplete="off" />
          </label>
          <div className="actions">
            <button className="btn btn--fail" disabled={!ready || busy} onClick={() => void remove()}>{busy ? t('account.deleting') : t('account.confirm')}</button>
            <button className="btn" disabled={busy} onClick={() => { setOpen(false); setWord('') }}>{t('common.cancel')}</button>
          </div>
          {error && <p className="error small">{error}</p>}
        </div>
      )}
    </section>
  )
}
