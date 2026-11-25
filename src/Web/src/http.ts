// Pure HTTP helpers shared by the API client. No DOM or React imports, so node:test can load this file directly.

export type QueryValue = string | number | boolean | null | undefined

/** Builds an API path: every segment and every query value is URI-encoded; null/undefined query values are skipped. */
export function apiPath(segments: readonly (string | number)[], query?: Record<string, QueryValue>): string {
  const path = '/' + segments.map((s) => encodeURIComponent(String(s))).join('/')
  if (!query) return path
  const pairs = Object.entries(query)
    .filter((entry): entry is [string, string | number | boolean] => entry[1] !== null && entry[1] !== undefined && entry[1] !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
  return pairs.length > 0 ? `${path}?${pairs.join('&')}` : path
}

/** Machine-readable error codes the backend puts into the ProblemDetails `code` extension. */
export const ERROR_CODES = ['review-no-text', 'review-limit', 'review-failed', 'conflict', 'validation', 'rate-limited', 'payload-too-large', 'full-exam-unavailable', 'part-submitted', 'next-part-blocked'] as const
export type ErrorCode = (typeof ERROR_CODES)[number]

export function isErrorCode(value: unknown): value is ErrorCode {
  return typeof value === 'string' && (ERROR_CODES as readonly string[]).includes(value)
}

/** API error: the client builds the localized message itself from the status and the ProblemDetails code. */
export class ApiError extends Error {
  readonly status: number
  readonly code: ErrorCode | null
  /** Seconds from the Retry-After header (429), if the server sent one. */
  readonly retryAfter: number | null
  /** Parsed JSON error body, for endpoints that return a domain result with a non-2xx status (e.g. join outcomes). */
  readonly body: unknown

  constructor(status: number, code: ErrorCode | null = null, retryAfter: number | null = null, body: unknown = null) {
    super(`HTTP ${status}${code ? ` (${code})` : ''}`)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.retryAfter = retryAfter
    this.body = body
  }
}

/** Reads the `code` extension from a parsed ProblemDetails body; anything else yields null. */
export function problemCode(body: unknown): ErrorCode | null {
  if (!body || typeof body !== 'object') return null
  const code = (body as { code?: unknown }).code
  return isErrorCode(code) ? code : null
}

/** Retry-After is either delta-seconds or an HTTP date; returns whole seconds (>= 0) or null. */
export function parseRetryAfter(header: string | null, now: number = Date.now()): number | null {
  if (!header) return null
  const trimmed = header.trim()
  if (/^\d+$/.test(trimmed)) return Number(trimmed)
  const at = Date.parse(trimmed)
  return Number.isNaN(at) ? null : Math.max(0, Math.ceil((at - now) / 1000))
}
