import type { ComponentType, ReactNode } from 'react'
import { AlphabetSheet, LetterSheet } from './LetterSheet'

const WIDGETS: Record<string, ComponentType> = { alfabet: AlphabetSheet, litery: LetterSheet }

/**
 * A small Markdown renderer for knowledge-base articles (our own content from the repository):
 * headings #–###, paragraphs, lists - and 1., quotes >, tables |a|b|, ---, **bold**, *italic*, `code`.
 * HTML is deliberately unsupported: only React nodes, no dangerouslySetInnerHTML.
 */
export function Markdown({ text }: { text: string }) {
  return <div className="md">{renderBlocks(text.replace(/\r\n/g, '\n').split('\n'))}</div>
}

function renderBlocks(lines: string[]): ReactNode[] {
  const out: ReactNode[] = []
  let i = 0
  const key = () => `b${out.length}`
  while (i < lines.length) {
    const line = lines[i]
    if (!line.trim()) { i++; continue }
    // embedded illustrations: a line [[name]] becomes a component
    const widget = /^\[\[([a-z-]+)\]\]$/.exec(line.trim())
    if (widget) { const W = WIDGETS[widget[1]]; if (W) out.push(<W key={key()} />); i++; continue }
    const heading = /^(#{1,3})\s+(.*)$/.exec(line)
    if (heading) {
      const level = heading[1].length
      const content = inline(heading[2])
      out.push(level === 1 ? <h1 key={key()}>{content}</h1> : level === 2 ? <h2 key={key()}>{content}</h2> : <h3 key={key()}>{content}</h3>)
      i++
      continue
    }
    if (/^-{3,}\s*$/.test(line)) { out.push(<hr key={key()} />); i++; continue }
    if (line.startsWith('>')) {
      const quote: string[] = []
      while (i < lines.length && lines[i].startsWith('>')) { quote.push(lines[i].replace(/^>\s?/, '')); i++ }
      out.push(<blockquote key={key()}>{inline(quote.join(' '))}</blockquote>)
      continue
    }
    if (/^\s*[-*]\s+/.test(line)) {
      const items: string[] = []
      while (i < lines.length && /^\s*[-*]\s+/.test(lines[i])) { items.push(lines[i].replace(/^\s*[-*]\s+/, '')); i++ }
      out.push(<ul key={key()}>{items.map((it, n) => <li key={n}>{inline(it)}</li>)}</ul>)
      continue
    }
    if (/^\s*\d+[.)]\s+/.test(line)) {
      const items: string[] = []
      while (i < lines.length && /^\s*\d+[.)]\s+/.test(lines[i])) { items.push(lines[i].replace(/^\s*\d+[.)]\s+/, '')); i++ }
      out.push(<ol key={key()}>{items.map((it, n) => <li key={n}>{inline(it)}</li>)}</ol>)
      continue
    }
    if (line.trim().startsWith('|')) {
      const rows: string[][] = []
      while (i < lines.length && lines[i].trim().startsWith('|')) {
        const cells = lines[i].trim().replace(/^\||\|$/g, '').split('|').map((c) => c.trim())
        if (!cells.every((c) => /^:?-{2,}:?$/.test(c))) rows.push(cells)
        i++
      }
      const [head, ...body] = rows
      out.push(
        <div key={key()} className="md__table">
          <table>
            {head && <thead><tr>{head.map((c, n) => <th key={n}>{inline(c)}</th>)}</tr></thead>}
            <tbody>{body.map((r, n) => <tr key={n}>{r.map((c, m) => <td key={m}>{inline(c)}</td>)}</tr>)}</tbody>
          </table>
        </div>,
      )
      continue
    }
    const para: string[] = []
    while (i < lines.length && lines[i].trim() && !/^(#{1,3}\s|>|\s*[-*]\s+|\s*\d+[.)]\s+|\||-{3,}\s*$|\[\[)/.test(lines[i])) { para.push(lines[i].trim()); i++ }
    out.push(<p key={key()}>{inline(para.join(' '))}</p>)
  }
  return out
}

/** **bold**, *italic*, `code`; ✗ and ✓ get the wrong/right answer colors. */
function inline(text: string): ReactNode[] {
  const parts: ReactNode[] = []
  const re = /\*\*(.+?)\*\*|\*(?!\s)(.+?)\*|`(.+?)`|(✗|✓)/g
  let last = 0
  let m: RegExpExecArray | null
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) parts.push(text.slice(last, m.index))
    const k = `i${parts.length}`
    if (m[1] !== undefined) parts.push(<strong key={k}>{inline(m[1])}</strong>)
    else if (m[2] !== undefined) parts.push(<em key={k}>{m[2]}</em>)
    else if (m[3] !== undefined) parts.push(<code key={k}>{m[3]}</code>)
    else parts.push(<span key={k} className={m[4] === '✗' ? 'md__wrong' : 'md__right'}>{m[4]}</span>)
    last = re.lastIndex
  }
  if (last < text.length) parts.push(text.slice(last))
  return parts
}
