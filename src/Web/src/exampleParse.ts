// Parsing of a task's PRZYKŁAD line. Pure: node:test loads this file directly.

const LETTER_OPTIONS = /\b([abc])\)\s*/g
const TRAILING_LETTER = /\n?\s*[–-]\s*([abc])\)\s*$/
// "… – TAK", "… – osoba 3", "Samodzielne planowanie – F": the example's answer at the end of the line
export const TRAILING_VALUE = /^([\s\S]+?)\s+[–-]\s+(TAK|NIE|osoba\s+\d+|[A-H])\s*$/

export interface Choice { letter: string; text: string }

/** "Pytanie a) … b) … c) … – b)" → the question, its options and the correct letter; null unless it is one question with options a–c. */
export function parseChoiceExample(example: string): { prompt: string; choices: Choice[]; answer: string | null } | null {
  const answer = TRAILING_LETTER.exec(example)?.[1] ?? null
  const body = example.replace(TRAILING_LETTER, '')
  const marks = [...body.matchAll(LETTER_OPTIONS)]
  if (marks.length !== 3 || marks.map((m) => m[1]).join('') !== 'abc') return null
  const prompt = body.slice(0, marks[0].index).trim()
  const choices = marks.map((m, i) => ({
    letter: m[1],
    text: body.slice(m.index + m[0].length, i + 1 < marks.length ? marks[i + 1].index : undefined).trim(),
  }))
  return prompt && choices.every((c) => c.text) ? { prompt, choices, answer } : null
}
