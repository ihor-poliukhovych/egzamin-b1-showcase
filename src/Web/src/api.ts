import { ApiError, apiPath, parseRetryAfter, problemCode } from './http'

export { ApiError, apiPath } from './http'

export type PatternCategory = 'ortografia' | 'gramatyka' | 'leksyka' | 'rekcja' | 'skladnia' | 'interpunkcja'
export type PatternStatus = 'watch' | 'active' | 'archived'
export type AttemptResult = 'pass' | 'fail'

export interface Pattern {
  id: string
  code: string | null
  title: string
  wrongExample: string
  correctForm: string
  rule: string
  contrastRuUa: string | null
  category: PatternCategory
  groupName: string | null
  status: PatternStatus
  fallsCount: number
  cleanStreak: number
  nextDueAt: string | null
}

export type FeedbackTopic = 'question' | 'bug' | 'content' | 'idea' | 'data' | 'other'

export interface TodayItem {
  pattern: Pattern
  result: AttemptResult | null
  order: number
}

export interface Today {
  sessionId: string
  date: string
  isComplete: boolean
  answered: number
  total: number
  items: TodayItem[]
}

export interface AttemptResponse {
  attemptId: string
  pattern: Pattern
  today: Today
}

// ----- Exam (Poprawność gramatyczna) -----

export type ArkuszSource = 'official' | 'own'
export type ExamTaskType = 'chooseInText' | 'wordBank' | 'fillForm' | 'askQuestion' | 'transform' | 'multipleChoice' | 'trueFalse' | 'matching' | 'shortAnswer' | 'writing' | 'speaking'
export type AnswerVerdict = 'none' | 'correct' | 'wrong' | 'needsReview' | 'accepted'

export type PartKind = 'sluchanie' | 'czytanie' | 'gramatyka' | 'pisanie' | 'mowienie'

export interface PartSummary {
  kind: PartKind
  timeLimitMinutes: number
  taskCount: number
  maxPoints: number
}

export interface ArkuszSummary {
  id: string
  code: string
  title: string
  source: ArkuszSource
  maxPoints: number
  parts: PartSummary[]
  level: string
  variant: string
  fullExam: boolean
}

export interface ExamItem {
  id: string
  number: number
  prompt: string | null
  hint: string | null
  options: string[]
  targetWords: number
  dialog: DialogInfo | null
}

/** Mówienie task 3 is a role-play: who the examiner plays, who opens, and how many lines the examiner has. */
export interface DialogInfo {
  role: string
  examinerOpens: boolean
  turns: number
}

export interface DialogLine {
  who: 'examiner' | 'candidate'
  text: string
}

export interface DialogTurn {
  line: string | null
  done: boolean
  adapted: boolean
}

export interface ExamTask {
  id: string
  number: number
  romanNumber: string
  type: ExamTaskType
  instruction: string
  pointsPerItem: number
  maxPoints: number
  text: string | null
  basedOn: string | null
  example: string | null
  wordBank: string[]
  items: ExamItem[]
  /** Illustration from the arkusz, relative to /media/ (e.g. "zadania/2021-01-s5.jpg"), as in Słuchanie V "match the text to the picture". */
  image?: string | null
}

export interface Arkusz {
  id: string
  code: string
  title: string
  source: ArkuszSource
  part: PartKind
  timeLimitMinutes: number
  maxPoints: number
  tasks: ExamTask[]
  audioKey: string | null
}

export interface ExamAnswer {
  itemId: string
  value: string
}

export interface ItemResult {
  itemId: string
  taskNumber: number
  taskRomanNumber: string
  number: number
  prompt: string | null
  hint: string | null
  value: string
  answerKey: string[]
  explanation: string | null
  verdict: AnswerVerdict
  points: number
  maxPoints: number
}

export interface TaskScore {
  number: number
  romanNumber: string
  points: number
  maxPoints: number
}

export interface Transcript {
  taskNumber: number
  taskRomanNumber: string
  text: string
}

export interface ExamResult {
  transcripts: Transcript[]
  points: number
  maxPoints: number
  percent: number
  isPassed: boolean
  passThresholdPercent: number
  tasks: TaskScore[]
  items: ItemResult[]
}

export interface ReviewError {
  fragment: string
  poprawnie: string
  /** Error type from the examiners' legend: "błąd gramatyczny" … "usterka stylistyczna". */
  typ: string
  /** Review step 3: the rule, why it is an error, and a self-check technique. */
  regula: string
  kontrast?: string | null
  /** Which task it belongs to: a or b. */
  zadanie?: string | null
}

/** One review for the whole zestaw (a + b), as the examiners do it. */
export interface WritingReview {
  scores: Record<string, number>
  errors: ReviewError[]
  komentarz: string
  werdykt?: string | null
}

export interface SpeakingReview {
  tasks: { task: number; covered: string[]; missing: string[]; komentarz: string }[]
  komentarz: string
}

export type PartStatus = 'none' | 'notStarted' | 'inProgress' | 'submitted' | 'notAvailable'
export type SessionVerdict = 'none' | 'inProgress' | 'incomplete' | 'passed' | 'failed'

export interface PartResult {
  kind: PartKind
  status: PartStatus
  points: number | null
  maxPoints: number
  percent: number | null
  isBelowThreshold: boolean
  attemptId: string | null
  submittedAt: string | null
  timeLimitMinutes: number
}

export interface ExamSessionInfo {
  id: string
  arkuszCode: string
  arkuszTitle: string
  startedAt: string
  verdict: SessionVerdict
  nextPart: PartKind | null
  passThresholdPercent: number
  parts: PartResult[]
}

export interface ExamAttempt {
  id: string
  sessionId: string | null
  startedAt: string
  submittedAt: string | null
  arkusz: Arkusz
  answers: ExamAnswer[]
  result: ExamResult | null
  audioPositionSec: number
  chosenSet: number | null
  assessment: unknown
  review: WritingReview | null
  points: number
}

export interface ExamAttemptSummary {
  id: string
  part: PartKind
  arkuszCode: string
  arkuszTitle: string
  startedAt: string
  submittedAt: string | null
  points: number
  maxPoints: number
  answered: number
  total: number
}

export interface Me {
  id: string
  email: string
  displayName: string
  role: 'Student' | 'Teacher'
  targetLevel: string
  targetVariant: string
  uiLanguage: string
  roleChosen: boolean
  examDate: string | null
}

/** An already submitted standalone attempt that can be counted towards a mock exam. */
export interface ReusablePart {
  part: PartKind
  attemptId: string
  submittedAt: string
  points: number
  maxPoints: number
}

export interface Group {
  id: string
  name: string
  createdAt: string
  archivedAt: string | null
}

export interface Invite {
  id: string
  token: string
  expiresAt: string
  maxUses: number | null
  uses: number
  revokedAt: string | null
  state: 'ok' | 'revoked' | 'expired' | 'exhausted'
}

export interface StudentStatus {
  studentId: string
  displayName: string
  email: string
  joinedAt: string
  parts: { kind: PartKind; status: PartStatus; points: number | null; maxPoints: number; percent: number | null; isBelowThreshold: boolean; submittedAt: string | null }[]
  topPatterns: { title: string; fallsCount: number }[]
  lastActivityAt: string | null
}

export interface MyGroup {
  groupId: string
  name: string
  teacherName: string
  joinedAt: string
  isArchived: boolean
}

export interface DashboardData {
  readiness: { kind: PartKind; last: { kind: PartKind; status: PartStatus; points: number | null; maxPoints: number; percent: number | null; isBelowThreshold: boolean }; trend: number[]; direction: 'none' | 'up' | 'flat' | 'down' }[]
  history: { attemptId: string; sessionId: string | null; arkuszCode: string; kind: PartKind; points: number; maxPoints: number; submittedAt: string; percent: number }[]
  matrix: Record<string, Record<string, number>>
  arkuszCodes: string[]
  fullArkuszCodes: string[]
  coveredPairs: number
  totalPairs: number
  topPatterns: { title: string; fallsCount: number }[]
  groups: MyGroup[]
  suggestedArkusz: string | null
}

export interface GroupSummary {
  groupId: string
  name: string
  students: number
  belowByPart: Record<string, number>
  inactiveStudents: string[]
  passedByPart: Record<string, number>
  members: { studentId: string; displayName: string; lastActivityAt: string | null; isInactive: boolean; percent: Record<string, number | null> }[]
}

export interface SiteInfo {
  name: string
  ownerName: string
  contactEmail: string
  /** True only in development: the API exposes GET /auth/dev-login. */
  devLogin: boolean
}


/** Single entry point for every API call: same-origin cookies, JSON in and out, uniform 401 and error handling. */
async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, keepalive } = options
  const res = await fetch(path, {
    method,
    credentials: 'same-origin',
    keepalive,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (res.status === 401) {
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    throw new ApiError(401)
  }
  if (!res.ok) {
    const problem: unknown = await res.json().catch(() => null)
    throw new ApiError(res.status, problemCode(problem), parseRetryAfter(res.headers.get('Retry-After')), problem)
  }
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const UNAUTHORIZED_EVENT = 'b1:unauthorized'

interface RequestOptions {
  method?: string
  body?: unknown
  /** Lets the request outlive the page (used when flushing autosave on pagehide). */
  keepalive?: boolean
}

const path = apiPath

type InviteBody = { days: number; maxUses: number | null }

export const api = {
  sendFeedback: (body: { topic: FeedbackTopic; message: string; email: string | null; language: string; website: string; images: string[] }) =>
    request<void>(path(['feedback']), { method: 'POST', body }),
  today: (lang?: string) => request<Today>(path(['today'], { lang })),
  attempt: (patternId: string, result: AttemptResult, evidence?: string) =>
    request<AttemptResponse>(path(['attempts']), {
      method: 'POST',
      body: { patternId, result, source: 'blitz', evidence: evidence ?? null, countsTowardStreak: true },
    }),
  createPattern: (body: { title: string; wrongExample: string; correctForm: string; rule: string; category: PatternCategory; contrastRuUa?: string | null }) =>
    request<Pattern>(path(['patterns']), { method: 'POST', body }),
  deletePattern: (id: string) => request<void>(path(['patterns', id]), { method: 'DELETE' }),

  arkusze: () => request<ArkuszSummary[]>(path(['arkusze'])),
  examAttempts: () => request<ExamAttemptSummary[]>(path(['exam-attempts'])),
  startExam: (arkuszCode: string, part: PartKind = 'gramatyka') =>
    request<ExamAttempt>(path(['exam-attempts']), { method: 'POST', body: { arkuszCode, part } }),
  examAttempt: (id: string) => request<ExamAttempt>(path(['exam-attempts', id])),
  saveAnswers: (id: string, answers: ExamAnswer[], keepalive = false) =>
    request<ExamAttempt>(path(['exam-attempts', id, 'answers']), { method: 'PUT', body: { answers }, keepalive }),
  submitExam: (id: string) => request<ExamAttempt>(path(['exam-attempts', id, 'submit']), { method: 'POST' }),
  discardAttempt: (id: string) => request<void>(path(['exam-attempts', id]), { method: 'DELETE' }),
  acceptAnswer: (id: string, itemId: string) =>
    request<ExamAttempt>(path(['exam-attempts', id, 'answers', itemId, 'accept']), { method: 'POST' }),
  sessions: () => request<ExamSessionInfo[]>(path(['exam-sessions'])),
  startSession: (arkuszCode: string) =>
    request<ExamSessionInfo>(path(['exam-sessions']), { method: 'POST', body: { arkuszCode } }),
  session: (id: string) => request<ExamSessionInfo>(path(['exam-sessions', id])),
  reusableParts: (id: string) => request<ReusablePart[]>(path(['exam-sessions', id, 'reusable'])),
  adoptPart: (id: string, part: PartKind, attemptId: string) =>
    request<void>(path(['exam-sessions', id, 'parts', part, 'adopt']), { method: 'POST', body: { attemptId } }),
  startSessionPart: (id: string, part: PartKind) =>
    request<ExamAttempt>(path(['exam-sessions', id, 'parts']), { method: 'POST', body: { part } }),
  checklists: () => request<{ writing: Record<string, string[]>; speaking: Record<string, string[]> }>(path(['checklists'])),
  wordCount: (text: string, target: number) =>
    request<{ count: number; min: number; max: number; status: string }>(path(['tools', 'word-count']), { method: 'POST', body: { text, target } }),
  chooseSet: (id: string, set: number) =>
    request<ExamAttempt>(path(['exam-attempts', id, 'choose-set']), { method: 'PUT', body: { set } }),
  saveDetails: (id: string, assessment: unknown, keepalive = false) =>
    request<ExamAttempt>(path(['exam-attempts', id, 'details']), { method: 'PUT', body: { assessment }, keepalive }),
  dialogTurn: (id: string, item: number, history: DialogLine[]) =>
    request<DialogTurn>(path(['exam-attempts', id, 'dialog', item]), { method: 'POST', body: { history } }),
  finish: (id: string, lang?: string) => request<ExamAttempt>(path(['exam-attempts', id, 'finish'], { lang }), { method: 'POST' }),
  saveAudioPosition: (id: string, seconds: number) =>
    request<void>(path(['exam-attempts', id, 'audio-position']), { method: 'PUT', body: { seconds } }),

  groups: (includeArchived: boolean) => request<Group[]>(path(['groups'], { includeArchived })),
  createGroup: (name: string) => request<Group>(path(['groups']), { method: 'POST', body: { name } }),
  group: (id: string) => request<Group>(path(['groups', id])),
  renameGroup: (id: string, name: string) => request<Group>(path(['groups', id]), { method: 'PUT', body: { name } }),
  archiveGroup: (id: string) => request<void>(path(['groups', id, 'archive']), { method: 'POST' }),
  groupInvites: (id: string) => request<Invite[]>(path(['groups', id, 'invites'])),
  issueInvite: (id: string, body: InviteBody) => request<Invite>(path(['groups', id, 'invites']), { method: 'POST', body }),
  reissueInvite: (id: string, body: InviteBody) => request<Invite>(path(['groups', id, 'invites', 'reissue']), { method: 'POST', body }),
  revokeInvite: (id: string, inviteId: string) => request<void>(path(['groups', id, 'invites', inviteId]), { method: 'DELETE' }),
  groupStudents: (id: string) => request<StudentStatus[]>(path(['groups', id, 'students'])),
  removeStudent: (id: string, studentId: string) => request<void>(path(['groups', id, 'students', studentId]), { method: 'DELETE' }),
  myGroups: () => request<MyGroup[]>(path(['groups', 'mine'])),
  leaveGroup: (id: string) => request<void>(path(['groups', id, 'membership']), { method: 'DELETE' }),
  joinPreview: (token: string) =>
    request<{ state: string; groupName: string | null; teacherName: string | null }>(path(['join', token])),
  /** Join outcomes other than success come back as 400/404/410 with { outcome }; they are returned, not thrown. */
  join: async (token: string, consent: boolean) => {
    try {
      return (await request<{ outcome: string }>(path(['join', token]), { method: 'POST', body: { consent } })).outcome
    } catch (e) {
      const outcome = e instanceof ApiError && typeof e.body === 'object' && e.body !== null ? (e.body as { outcome?: unknown }).outcome : undefined
      if (typeof outcome === 'string') return outcome
      throw e
    }
  },

  dashboard: (level: string, variant: string) => request<DashboardData>(path(['dashboard', 'me'], { level, variant })),
  teacherSummary: (level: string, variant: string) => request<GroupSummary[]>(path(['teacher', 'summary'], { level, variant })),
  studentCard: (groupId: string, studentId: string, level: string, variant: string) =>
    request<DashboardData>(path(['teacher', 'groups', groupId, 'students', studentId, 'dashboard'], { level, variant })),

  me: () => request<Me>(path(['auth', 'me'])),
  site: () => request<SiteInfo>(path(['site'])),
  updateSettings: (targetLevel: string, targetVariant: string, uiLanguage?: string) =>
    request<void>(path(['auth', 'settings']), { method: 'PUT', body: { targetLevel, targetVariant, uiLanguage } }),
  deleteAccount: () => request<void>(path(['auth', 'account']), { method: 'DELETE' }),
  chooseRole: (role: 'Student' | 'Teacher') => request<void>(path(['auth', 'role']), { method: 'POST', body: { role } }),
  setExamDate: (date: string | null) => request<void>(path(['auth', 'exam-date']), { method: 'PUT', body: { date } }),
  changeRole: (role: 'Student' | 'Teacher') => request<void>(path(['auth', 'role']), { method: 'PUT', body: { role } }),
  logout: () => request<void>(path(['auth', 'logout']), { method: 'POST' }),
}

/** Browser-navigation URLs (full page loads, not fetch): Google sign-in and the development-only login. */
export const authUrls = {
  google: (returnUrl: string) => apiPath(['auth', 'login'], { returnUrl }),
  devLogin: (email: string, returnUrl: string) => apiPath(['auth', 'dev-login'], { email, returnUrl }),
}
