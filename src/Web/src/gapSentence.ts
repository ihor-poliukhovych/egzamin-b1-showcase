const GAP = '___'
const MAX = 220

const isSentenceEnd = (text: string, i: number) =>
  text[i] === '\n' || ('.!?'.includes(text[i]) && (i + 1 >= text.length || /\s/.test(text[i + 1])))

/**
 * Task-text sentence with gap no. n ("…w zamkniętym ___."), like GapSentence on the server:
 * other gaps become "…", solved examples are shown as a plain word, a long sentence is trimmed around the gap.
 */
export function gapSentence(text: string | null, itemNumber: number): string | null {
  if (!text) return null
  const marker = `{${itemNumber}}`
  const at = text.indexOf(marker)
  if (at < 0) return null

  let start = at
  while (start > 0 && !isSentenceEnd(text, start - 1)) start--
  let end = at + marker.length
  while (end < text.length && !isSentenceEnd(text, end)) end++
  if (end < text.length) end++

  const s = text.slice(start, end).replace(marker, GAP)
    .replace(/\{\d+\}/g, '…')
    .replace(/\[\[([^\]]+)\]\]/g, '$1')
    .replace('[przykład]', '')
    .replace(/\s+/g, ' ')
    .trim()
  if (s.length <= MAX) return s

  const gap = s.indexOf(GAP)
  const from = Math.max(0, gap - MAX / 2)
  const to = Math.min(s.length, from + MAX)
  return (from > 0 ? '…' : '') + s.slice(from, to).trim() + (to < s.length ? '…' : '')
}
