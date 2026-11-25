// Maps any thrown value to a dictionary key (+ params). Pure: imports only types and the http helpers, so node:test can load it.
import { ApiError } from '../http.ts'
import type { Key, Params } from './core'

export interface ErrorMessage {
  key: Key
  params?: Params
}

export function errorMessage(error: unknown): ErrorMessage {
  if (!(error instanceof ApiError)) return { key: error instanceof TypeError ? 'err.network' : 'err.unknown' }
  switch (error.code) {
    case 'review-no-text': return { key: 'err.reviewNoText' }
    case 'review-limit': return { key: 'err.reviewLimit' }
    case 'review-failed': return { key: 'err.reviewFailed' }
    case 'rate-limited': return rateLimited(error.retryAfter)
    case 'payload-too-large': return { key: 'err.payloadTooLarge' }
    case 'validation': return { key: 'err.validation' }
    case 'conflict': return { key: 'err.conflict' }
    case 'full-exam-unavailable': return { key: 'err.fullExamUnavailable' }
    case 'part-submitted': return { key: 'err.partSubmitted' }
    case 'next-part-blocked': return { key: 'err.nextPart' }
    case null: break
  }
  switch (error.status) {
    case 401: return { key: 'err.unauthorized' }
    case 403: return { key: 'err.forbidden' }
    case 404: return { key: 'err.notFound' }
    case 400:
    case 422: return { key: 'err.badRequest' }
    case 409: return { key: 'err.conflict' }
    case 413: return { key: 'err.payloadTooLarge' }
    case 429: return rateLimited(error.retryAfter)
    case 501: return { key: 'err.googleNotConfigured' }
    default: return { key: error.status >= 500 ? 'err.server' : 'err.unknown' }
  }
}

function rateLimited(retryAfter: number | null): ErrorMessage {
  return retryAfter !== null && retryAfter > 0 ? { key: 'err.rateLimited', params: { seconds: retryAfter } } : { key: 'err.rateLimitedSoon' }
}
