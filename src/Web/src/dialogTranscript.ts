import type { DialogLine } from './api'

/** The dialog as text for the review: "Egzaminator: …" / "Zdający: …" (same as Dialogs.Transcript on the server). */
export function dialogTranscript(lines: DialogLine[]): string {
  return lines.map((l) => `${l.who === 'examiner' ? 'Egzaminator' : 'Zdający'}: ${l.text}`).join('\n')
}
