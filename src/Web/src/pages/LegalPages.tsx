import type { ReactNode } from 'react'
import { useI18n } from '../i18n'
import { MARK } from '../i18n/core'
import { LEGAL_SLUGS, links, type LegalSlug } from '../router'
import { CERTIFICATE_SITE, useSite } from '../site'
import { LEGAL, type Block } from './legalContent'
import { FeedbackForm } from '../components/FeedbackForm'

const INLINE = /\*\*(.+?)\*\*|`(.+?)`|\[(.+?)\]\((.+?)\)/g

/** Tiny inline markup: **bold**, `code`, [text](#slug | https://… | mailto:…). Any other link target is rendered as plain text. */
function renderInline(text: string, fill: (s: string) => string): ReactNode[] {
  const out: ReactNode[] = []
  let last = 0
  let key = 0
  for (const m of text.matchAll(INLINE)) {
    if (m.index > last) out.push(fill(text.slice(last, m.index)))
    if (m[1] !== undefined) out.push(<strong key={key++}>{fill(m[1])}</strong>)
    else if (m[2] !== undefined) out.push(<code key={key++}>{fill(m[2])}</code>)
    else {
      const href = fill(m[4])
      const label = fill(m[3])
      const slug = href.startsWith('#') ? LEGAL_SLUGS.find((x) => x === href.slice(1)) : undefined
      if (slug) out.push(<a key={key++} href={links.legal(slug)}>{label}</a>)
      else if (href.startsWith('https:')) out.push(<a key={key++} href={href} target="_blank" rel="noopener noreferrer">{label}</a>)
      else if (href.startsWith('mailto:')) out.push(<a key={key++} href={href}>{label}</a>)
      else out.push(label)
    }
    last = m.index + m[0].length
  }
  if (last < text.length) out.push(fill(text.slice(last)))
  return out
}

export default function LegalPage({ slug }: { slug: LegalSlug }) {
  const { lang, t } = useI18n()
  // disclaimer about independence from the exam board, under "O projekcie" and "Źródła materiałów", not in the footer
  const [discBefore, discAfter] = t('footer.disclaimer', { site: MARK }).split(MARK)
  const site = useSite()
  const doc = LEGAL[lang][slug]
  const fill = (s: string) => s.replaceAll('{owner}', site.ownerName).replaceAll('{email}', site.contactEmail).replaceAll('{name}', site.name)

  const render = (block: Block, i: number) => {
    if ('h' in block) return <h2 key={i}>{block.h}</h2>
    if ('p' in block) return <p key={i}>{renderInline(block.p, fill)}</p>
    if ('ul' in block) return <ul key={i}>{block.ul.map((li, j) => <li key={j}>{renderInline(li, fill)}</li>)}</ul>
    if ('muted' in block) return <p key={i} className="muted small">{block.muted}</p>
    if ('form' in block) return <FeedbackForm key={i} />
    return <p key={i} className="legal__mail"><a href={`mailto:${site.contactEmail}`}>{site.contactEmail}</a></p>
  }

  return (
    <article className="legal">
      <header className="header"><h1>{doc.title}</h1></header>
      {doc.blocks.map(render)}
      {(slug === 'zrodla' || slug === 'o-projekcie') && (
        <p className="muted small legal__disclaimer">
          {discBefore}<a href={CERTIFICATE_SITE} target="_blank" rel="noopener noreferrer">certyfikatpolski.pl</a>{discAfter}
        </p>
      )}
    </article>
  )
}
