import { useEffect, useState } from 'react'

export const LEGAL_SLUGS = ['o-projekcie', 'kontakt', 'prywatnosc', 'regulamin', 'zrodla'] as const
export type LegalSlug = (typeof LEGAL_SLUGS)[number]

export type Route =
  | { page: 'home' }
  | { page: 'today' }
  | { page: 'student-card'; groupId: string; studentId: string }
  | { page: 'exam-list' }
  | { page: 'exam-run'; attemptId: string }
  | { page: 'exam-result'; attemptId: string }
  | { page: 'session'; sessionId: string }
  | { page: 'groups' }
  | { page: 'group'; groupId: string }
  | { page: 'join'; token: string }
  | { page: 'profile' }
  | { page: 'knowledge' }
  | { page: 'article'; slug: string }
  | { page: 'legal'; slug: LegalSlug }

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** Route ids come from the address bar, so only well-formed GUIDs are accepted. */
export const isGuid = (value: string | undefined): value is string => value !== undefined && GUID.test(value)

export function parseHash(hash: string): Route {
  const path = hash.replace(/^#/, '').replace(/^\/+/, '')
  const parts = path.split('/').filter(Boolean)
  const [head, a, b, c] = parts
  if (head === 'egzamin') {
    if (parts.length === 1) return { page: 'exam-list' }
    if (!isGuid(a)) return { page: 'home' }
    return b === 'wynik' ? { page: 'exam-result', attemptId: a } : { page: 'exam-run', attemptId: a }
  }
  if (head === 'proba') return isGuid(a) ? { page: 'session', sessionId: a } : { page: 'home' }
  if (head === 'grupy') {
    if (a === undefined) return { page: 'groups' }
    if (!isGuid(a)) return { page: 'home' }
    if (b === 'student') return isGuid(c) ? { page: 'student-card', groupId: a, studentId: c } : { page: 'home' }
    return { page: 'group', groupId: a }
  }
  if (head === 'blitz') return { page: 'today' }
  if (head === 'join' && a) return { page: 'join', token: a }
  if (head === 'profil') return { page: 'profile' }
  if (head === 'wiedza') return a ? { page: 'article', slug: a } : { page: 'knowledge' }
  const legal = LEGAL_SLUGS.find((slug) => slug === head)
  if (legal) return { page: 'legal', slug: legal }
  return { page: 'home' }
}

export function useRoute(): Route {
  const [route, setRoute] = useState<Route>(() => parseHash(window.location.hash))
  useEffect(() => {
    const onChange = () => setRoute(parseHash(window.location.hash))
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  return route
}

export function navigate(hash: string) {
  window.location.hash = hash
}

export const links = {
  home: '#/',
  today: '#/blitz',
  studentCard: (groupId: string, studentId: string) => `#/grupy/${groupId}/student/${studentId}`,
  examList: '#/egzamin',
  examRun: (id: string) => `#/egzamin/${id}`,
  examResult: (id: string) => `#/egzamin/${id}/wynik`,
  session: (id: string) => `#/proba/${id}`,
  groups: '#/grupy',
  group: (id: string) => `#/grupy/${id}`,
  profile: '#/profil',
  knowledge: '#/wiedza',
  article: (slug: string) => `#/wiedza/${slug}`,
  legal: (slug: LegalSlug) => `#/${slug}`,
}
