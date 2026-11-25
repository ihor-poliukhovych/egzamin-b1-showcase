// Unit tests for pure modules. Node strips TypeScript types natively, so the .ts sources are imported as they are.
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { parseHash } from '../src/router.ts'
import { ApiError, apiPath, parseRetryAfter, problemCode } from '../src/http.ts'
import { errorMessage } from '../src/i18n/errors.ts'
import { readSpeakingReview, readWritingReview } from '../src/review.ts'
import { parseChoiceExample } from '../src/exampleParse.ts'

const ID = '3f2b8c1e-9a4d-4e2f-8b7a-0c1d2e3f4a5b'
const ID2 = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'

test('parseHash: known pages and GUID routes', () => {
  assert.deepEqual(parseHash(''), { page: 'home' })
  assert.deepEqual(parseHash('#/egzamin'), { page: 'exam-list' })
  assert.deepEqual(parseHash(`#/egzamin/${ID}`), { page: 'exam-run', attemptId: ID })
  assert.deepEqual(parseHash(`#/egzamin/${ID}/wynik`), { page: 'exam-result', attemptId: ID })
  assert.deepEqual(parseHash(`#/proba/${ID}`), { page: 'session', sessionId: ID })
  assert.deepEqual(parseHash('#/grupy'), { page: 'groups' })
  assert.deepEqual(parseHash(`#/grupy/${ID}`), { page: 'group', groupId: ID })
  assert.deepEqual(parseHash(`#/grupy/${ID}/student/${ID2}`), { page: 'student-card', groupId: ID, studentId: ID2 })
  assert.deepEqual(parseHash('#/join/abc-token'), { page: 'join', token: 'abc-token' })
  assert.deepEqual(parseHash('#/regulamin'), { page: 'legal', slug: 'regulamin' })
  assert.deepEqual(parseHash('#/wiedza/bledy'), { page: 'article', slug: 'bledy' })
})

test('parseHash: malformed ids and removed pages fall back to home', () => {
  assert.deepEqual(parseHash('#/egzamin/not-a-guid'), { page: 'home' })
  assert.deepEqual(parseHash('#/egzamin/..%2Fadmin/wynik'), { page: 'home' })
  assert.deepEqual(parseHash('#/proba/123'), { page: 'home' })
  assert.deepEqual(parseHash(`#/grupy/${ID}/student/x`), { page: 'home' })
  assert.deepEqual(parseHash('#/grupy/evil'), { page: 'home' })
  assert.deepEqual(parseHash('#/admin'), { page: 'home' })
})

test('apiPath encodes every segment and query value', () => {
  assert.equal(apiPath(['exam-attempts', ID, 'answers']), `/exam-attempts/${ID}/answers`)
  assert.equal(apiPath(['join', 'a/b?c#d']), '/join/a%2Fb%3Fc%23d')
  assert.equal(apiPath(['dashboard', 'me'], { level: 'B1', variant: 'dorosli' }), '/dashboard/me?level=B1&variant=dorosli')
  assert.equal(apiPath(['today'], { lang: undefined }), '/today')
  assert.equal(apiPath(['groups'], { includeArchived: false }), '/groups?includeArchived=false')
  assert.equal(apiPath(['auth', 'dev-login'], { email: 'a+b@example.test', returnUrl: '/#/egzamin' }),
    '/auth/dev-login?email=a%2Bb%40example.test&returnUrl=%2F%23%2Fegzamin')
})

test('ProblemDetails code and Retry-After parsing', () => {
  assert.equal(problemCode({ status: 429, code: 'rate-limited' }), 'rate-limited')
  assert.equal(problemCode({ code: 'something-else' }), null)
  assert.equal(problemCode('text'), null)
  assert.equal(parseRetryAfter('30'), 30)
  assert.equal(parseRetryAfter(null), null)
  assert.equal(parseRetryAfter('Wed, 21 Oct 2026 07:28:10 GMT', Date.parse('Wed, 21 Oct 2026 07:28:00 GMT')), 10)
  assert.equal(parseRetryAfter('soon'), null)
})

test('errorMessage maps codes before statuses', () => {
  assert.deepEqual(errorMessage(new ApiError(409, 'review-limit')), { key: 'err.reviewLimit' })
  assert.deepEqual(errorMessage(new ApiError(429, 'rate-limited', 12)), { key: 'err.rateLimited', params: { seconds: 12 } })
  assert.deepEqual(errorMessage(new ApiError(429)), { key: 'err.rateLimitedSoon' })
  assert.deepEqual(errorMessage(new ApiError(413, 'payload-too-large')), { key: 'err.payloadTooLarge' })
  assert.deepEqual(errorMessage(new ApiError(400, 'validation')), { key: 'err.validation' })
  assert.deepEqual(errorMessage(new ApiError(409)), { key: 'err.conflict' })
  assert.deepEqual(errorMessage(new ApiError(503)), { key: 'err.server' })
  assert.deepEqual(errorMessage(new TypeError('Failed to fetch')), { key: 'err.network' })
  assert.deepEqual(errorMessage('weird'), { key: 'err.unknown' })
})

test('readWritingReview keeps valid entries and drops malformed ones', () => {
  const review = readWritingReview({
    scores: { wykonanie: 3, gramatyka: 'x', styl: 2.5 },
    errors: [{ fragment: 'jest', poprawnie: 'są', typ: 'błąd gramatyczny', regula: 'r', zadanie: 'a' }, { fragment: 1 }, null, 'bad'],
    komentarz: 42,
  })
  assert.deepEqual(review.scores, { wykonanie: 3, styl: 2.5 })
  assert.equal(review.errors.length, 1)
  assert.equal(review.errors[0].zadanie, 'a')
  assert.equal(review.komentarz, '')
  assert.equal(readWritingReview(null), null)
  assert.equal(readWritingReview([1, 2]), null)
})

test('readWritingReview merges the legacy per-task format', () => {
  const review = readWritingReview({
    1: { scores: { wykonanie: 4 }, errors: [{ fragment: 'a', poprawnie: 'b' }], komentarz: 'ok' },
    2: { scores: { wykonanie: 2 }, errors: 'nope', komentarz: 'meh' },
  })
  assert.equal(review.scores.wykonanie, 3)
  assert.deepEqual(review.errors.map((e) => e.zadanie), ['a'])
  assert.equal(review.komentarz, 'a) ok b) meh')
})

test('readSpeakingReview drops malformed tasks', () => {
  const review = readSpeakingReview({ tasks: [{ task: 1, covered: ['x', 2], missing: null, komentarz: 'k' }, { task: 'two' }, 7], komentarz: 'all' })
  assert.deepEqual(review, { tasks: [{ task: 1, covered: ['x'], missing: [], komentarz: 'k' }], komentarz: 'all' })
  assert.deepEqual(readSpeakingReview({}), { tasks: [], komentarz: '' })
  assert.equal(readSpeakingReview(undefined), null)
})

test('parseChoiceExample splits a solved multiple-choice example', () => {
  const parsed = parseChoiceExample('Gdzie jest kot? a) w domu b) w ogrodzie c) w szkole – b)')
  assert.deepEqual(parsed, {
    prompt: 'Gdzie jest kot?',
    choices: [{ letter: 'a', text: 'w domu' }, { letter: 'b', text: 'w ogrodzie' }, { letter: 'c', text: 'w szkole' }],
    answer: 'b',
  })
  assert.equal(parseChoiceExample('Tylko zdanie bez wariantów'), null)
})
