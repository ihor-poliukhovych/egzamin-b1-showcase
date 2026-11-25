import { useI18n } from '../i18n'
import { BUILD, useSite } from '../site'
import { links, type LegalSlug } from '../router'
import type { Key } from '../i18n/core'

const LINKS: { slug: LegalSlug; label: Key }[] = [
  { slug: 'o-projekcie', label: 'footer.about' },
  { slug: 'kontakt', label: 'footer.contact' },
  { slug: 'prywatnosc', label: 'footer.privacy' },
  { slug: 'regulamin', label: 'footer.terms' },
  { slug: 'zrodla', label: 'footer.sources' },
]

export default function Footer() {
  const site = useSite()
  const { t } = useI18n()
  return (
    <footer className="footer">
      <div className="footer__inner">
        <div className="footer__top">
          <span className="footer__brand">{site.name}</span>
          <span className="footer__copy">© 2026 · <span className="footer__build">{t('footer.build', { build: BUILD })}</span></span>
          <nav className="footer__links" aria-label={t('footer.nav')}>
            {LINKS.map((l) => <a key={l.slug} href={links.legal(l.slug)}>{t(l.label)}</a>)}
          </nav>
        </div>
      </div>
    </footer>
  )
}
