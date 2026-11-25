// Parsers for AI review JSON stored on an attempt. The data comes from a language model, so every field is checked
// and malformed entries are dropped instead of crashing the page. Pure: node:test loads this file directly.
import type { ReviewError, SpeakingReview, WritingReview } from './api'

/** Score keys of the five writing criteria, in the order the examiners list them. */
export const REVIEW_KEYS = ['wykonanie', 'gramatyka', 'slownictwo', 'styl', 'ortografia'] as const

type Obj = Record<string, unknown>

const isObj = (v: unknown): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v)
const str = (v: unknown): string | null => (typeof v === 'string' ? v : null)
const strings = (v: unknown): string[] => (Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : [])

/** Task letter for an item number: 1 → "a", 2 → "b". */
export const itemLabel = (n: number) => String.fromCharCode(96 + n)

function readError(raw: unknown, zadanie?: string): ReviewError | null {
  if (!isObj(raw)) return null
  const fragment = str(raw.fragment)
  const poprawnie = str(raw.poprawnie)
  if (fragment === null || poprawnie === null) return null
  return {
    fragment,
    poprawnie,
    typ: str(raw.typ) ?? '',
    regula: str(raw.regula) ?? '',
    kontrast: str(raw.kontrast),
    zadanie: zadanie ?? str(raw.zadanie),
  }
}

function readScores(raw: unknown): Record<string, number> {
  if (!isObj(raw)) return {}
  return Object.fromEntries(Object.entries(raw).filter((e): e is [string, number] => typeof e[1] === 'number' && Number.isFinite(e[1])))
}

function readErrors(raw: unknown, zadanie?: string): ReviewError[] {
  return Array.isArray(raw) ? raw.map((e) => readError(e, zadanie)).filter((e): e is ReviewError => e !== null) : []
}

/**
 * Current format: one review for the whole zestaw ({ scores, errors, komentarz, werdykt }).
 * Legacy format (before calibration): one review per task ({ "1": {...}, "2": {...} }), merged into one here.
 */
export function readWritingReview(raw: unknown): WritingReview | null {
  if (!isObj(raw)) return null
  if ('scores' in raw) {
    return { scores: readScores(raw.scores), errors: readErrors(raw.errors), komentarz: str(raw.komentarz) ?? '', werdykt: str(raw.werdykt) }
  }
  const parts = Object.entries(raw).filter((e): e is [string, Obj] => /^\d+$/.test(e[0]) && isObj(e[1]))
  if (parts.length === 0) return null
  const perPart = parts.map(([n, r]) => ({ label: itemLabel(Number(n)), scores: readScores(r.scores), errors: r.errors, komentarz: str(r.komentarz) ?? '' }))
  const scores = Object.fromEntries(REVIEW_KEYS.map((k) => [k, perPart.reduce((sum, p) => sum + (p.scores[k] ?? 0), 0) / perPart.length]))
  return {
    scores,
    errors: perPart.flatMap((p) => readErrors(p.errors, p.label)),
    komentarz: perPart.map((p) => `${p.label}) ${p.komentarz}`).join(' '),
    werdykt: null,
  }
}

export function readSpeakingReview(raw: unknown): SpeakingReview | null {
  if (!isObj(raw)) return null
  const tasks = Array.isArray(raw.tasks)
    ? raw.tasks.flatMap((x) => (isObj(x) && typeof x.task === 'number'
      ? [{ task: x.task, covered: strings(x.covered), missing: strings(x.missing), komentarz: str(x.komentarz) ?? '' }]
      : []))
    : []
  return { tasks, komentarz: str(raw.komentarz) ?? '' }
}
