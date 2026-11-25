import type { ArkuszSummary, ExamAttemptSummary, PartKind } from './api'
import { formatMonthYear } from './format'

export const PART_ORDER: PartKind[] = ['sluchanie', 'czytanie', 'gramatyka', 'pisanie', 'mowienie']
export const PART_SHORT: Record<PartKind, string> = { sluchanie: 'Słuch.', czytanie: 'Czyt.', gramatyka: 'Gram.', pisanie: 'Pisan.', mowienie: 'Mówien.' }
export const PASS_PERCENT = 50

/** Parses a set code: year, month (0 for sample tests) and whether it is a sample test. setTitle builds the label. */
export function parseSetCode(code: string): { sample: boolean; year: number; month: number; sort: number } {
  const sample = code.includes('przyklad')
  const m = code.match(/(\d{4})(?:-(\d{2}))?$/)
  const year = m ? Number(m[1]) : 0
  const month = m && m[2] ? Number(m[2]) : 0
  return { sample, year, month, sort: sample ? year : year * 100 + month }
}

/** Set title in the UI language, e.g. "February 2024" or "Sample test 2019". */
export function setTitle(set: { sample: boolean; year: number; month: number; arkusz: { code: string } }, t: (key: 'set.sample') => string) {
  if (set.sample) return `${t('set.sample')} ${set.year || ''}`.trim()
  return set.month >= 1 && set.month <= 12 ? formatMonthYear(set.year, set.month) : set.arkusz.code
}

export type PartState = 'missing' | 'todo' | 'active' | 'passed' | 'failed'
export type SetStatus = 'new' | 'progress' | 'passed' | 'failed'

export interface PartInfo {
  kind: PartKind
  state: PartState
  percent: number | null
  minutes: number
  maxPoints: number
  /** An open (not submitted) attempt, to offer "Continue". */
  activeAttemptId: string | null
  /** The best submitted attempt, to open its review. */
  bestAttemptId: string | null
}

export interface SetInfo {
  arkusz: ArkuszSummary
  sample: boolean
  year: number
  month: number
  sort: number
  parts: PartInfo[]
  status: SetStatus
  failedParts: { kind: PartKind; percent: number }[]
  available: number
  done: number
}

export interface PartResultLookup {
  /** Best submitted result for the part in % (undefined if the part was never submitted). */
  best(code: string, part: PartKind): { percent: number; attemptId: string | null } | undefined
  /** The open attempt for the part. */
  active(code: string, part: PartKind): string | undefined
}

export function lookupFromAttempts(attempts: ExamAttemptSummary[]): PartResultLookup {
  return {
    best(code, part) {
      let hit: { percent: number; attemptId: string | null } | undefined
      for (const a of attempts) {
        if (a.arkuszCode !== code || a.part !== part || !a.submittedAt || a.maxPoints <= 0) continue
        const percent = (a.points / a.maxPoints) * 100
        if (!hit || percent > hit.percent) hit = { percent, attemptId: a.id }
      }
      return hit
    },
    active(code, part) {
      return attempts.find((a) => a.arkuszCode === code && a.part === part && !a.submittedAt)?.id
    },
  }
}

/** Dictionary keys from the server are PascalCase (Sluchanie), so they are compared case-insensitively. */
export function lookupFromMatrix(matrix: Record<string, Record<string, number>>): PartResultLookup {
  return {
    best(code, part) {
      const row = matrix[code]
      if (!row) return undefined
      const key = Object.keys(row).find((k) => k.toLowerCase() === part)
      return key === undefined ? undefined : { percent: row[key], attemptId: null }
    },
    active: () => undefined,
  }
}

export function buildSets(arkusze: ArkuszSummary[], results: PartResultLookup): SetInfo[] {
  const sets = arkusze.map((arkusz): SetInfo => {
    const { sample, year, month, sort } = parseSetCode(arkusz.code)
    const parts = PART_ORDER.map((kind): PartInfo => {
      const published = arkusz.parts.find((p) => p.kind === kind)
      if (!published) return { kind, state: 'missing', percent: null, minutes: 0, maxPoints: 0, activeAttemptId: null, bestAttemptId: null }
      const best = results.best(arkusz.code, kind)
      const active = results.active(arkusz.code, kind) ?? null
      const state: PartState = best ? (best.percent >= PASS_PERCENT ? 'passed' : 'failed') : active ? 'active' : 'todo'
      return { kind, state, percent: best?.percent ?? null, minutes: published.timeLimitMinutes, maxPoints: published.maxPoints, activeAttemptId: active, bestAttemptId: best?.attemptId ?? null }
    })
    const present = parts.filter((p) => p.state !== 'missing')
    const failedParts = present.filter((p) => p.state === 'failed').map((p) => ({ kind: p.kind, percent: p.percent ?? 0 }))
    const done = present.filter((p) => p.state === 'passed' || p.state === 'failed').length
    const touched = present.some((p) => p.state !== 'todo')
    const status: SetStatus = failedParts.length > 0 ? 'failed'
      : present.length > 0 && present.every((p) => p.state === 'passed') ? 'passed'
      : touched ? 'progress' : 'new'
    return { arkusz, sample, year, month, sort, parts, status, failedParts, available: present.length, done }
  })
  // newest dates first, sample tests last
  return sets.sort((a, b) => Number(a.sample) - Number(b.sample) || b.sort - a.sort)
}

export interface LevelStat { level: string; total: number; passed: number }

export function levelStats(sets: SetInfo[], variant: string, levels: readonly string[]): LevelStat[] {
  return levels.map((level) => {
    const mine = sets.filter((s) => s.arkusz.level === level && s.arkusz.variant === variant)
    return { level, total: mine.length, passed: mine.filter((s) => s.status === 'passed').length }
  })
}

/** Streak of consecutive days (ending today or yesterday) with at least one submitted part, plus a 14-day activity strip. */
export function activity(submittedAt: string[], now = new Date()): { streak: number; days: { date: string; count: number }[] } {
  const key = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  const counts = new Map<string, number>()
  for (const iso of submittedAt) counts.set(key(new Date(iso)), (counts.get(key(new Date(iso))) ?? 0) + 1)
  const days: { date: string; count: number }[] = []
  for (let i = 13; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - i)
    days.push({ date: key(d), count: counts.get(key(d)) ?? 0 })
  }
  let streak = 0
  let offset = counts.has(key(now)) ? 0 : 1
  for (;;) {
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - offset)
    if (!counts.has(key(d))) break
    streak++
    offset++
  }
  return { streak, days }
}
