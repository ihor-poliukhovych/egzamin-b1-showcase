import type { PartKind, SessionVerdict } from './api'
import type { Key } from './i18n/core'

export const PART_LABEL: Record<PartKind, string> = {
  sluchanie: 'Rozumienie ze słuchu',
  czytanie: 'Rozumienie tekstów pisanych',
  gramatyka: 'Poprawność gramatyczna',
  pisanie: 'Pisanie',
  mowienie: 'Mówienie',
}

export const RUNNABLE_PARTS: PartKind[] = ['sluchanie', 'czytanie', 'gramatyka', 'pisanie', 'mowienie']

export const VERDICT_KEY: Record<SessionVerdict, Key> = {
  none: 'verdict.none',
  inProgress: 'verdict.inProgress',
  incomplete: 'verdict.incomplete',
  passed: 'verdict.passed',
  failed: 'verdict.failed',
}

/** "Gramatyka 2024-06 · błąd gramatyczny: czy" → "czy": cards and chips show only the mistake itself. */
export function patternShort(title: string): string {
  const tail = title.split(' · ').pop() ?? title
  const colon = tail.indexOf(': ')
  return colon >= 0 ? tail.slice(colon + 2) : tail
}
