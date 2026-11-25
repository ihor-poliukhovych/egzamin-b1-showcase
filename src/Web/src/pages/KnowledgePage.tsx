import { useEffect, useState } from 'react'
import { useI18n } from '../i18n'
import { LANG_NAME } from '../i18n/core'
import { ARTICLES, articleMeta, loadArticle, type ArticleSlug } from '../knowledge'
import { Markdown } from '../components/Markdown'
import { BackLink, BackRow } from '../components/BackLink'
import { Loader } from '../components/Loader'
import { links } from '../router'

const ICON: Record<ArticleSlug, string> = {
  egzamin: 'M5 4h14v16H5zM9 8h6M9 12h6M9 16h3',
  bledy: 'M12 4v10M12 18v2',
  pisanie: 'M4 20l4-1 10-10-3-3L5 16zM14 7l3 3',
  mowienie: 'M4 5h16v11H9l-5 4z',
  gramatyka: 'M6 5h12M6 10h12M6 15h7M6 20h4',
  pisownia: 'M5 19L10 5l5 14M7 14h6M17 9v10',
}

/** Knowledge base: short articles about the exam, typical mistakes, writing and the oral part. */
export function KnowledgePage() {
  const { t, lang } = useI18n()
  const [items, setItems] = useState<{ slug: ArticleSlug; title: string; summary: string }[] | null>(null)

  useEffect(() => {
    let alive = true
    void Promise.all(ARTICLES.map(async (slug) => {
      const article = await loadArticle(lang, slug)
      return article ? { slug, ...articleMeta(article.text) } : null
    })).then((list) => { if (alive) setItems(list.filter((x): x is NonNullable<typeof x> => x !== null)) })
    return () => { alive = false }
  }, [lang])

  return (
    <>
      <header className="header">
        <h1>{t('knowledge.title')}</h1>
        <p className="muted">{t('knowledge.subtitle')}</p>
      </header>
      {items === null ? <Loader label={t('knowledge.loading')} /> : (
        <ul className="kb-list">
          {items.map((a) => (
            <li key={a.slug}>
              <a className="quick-link kb-card" href={links.article(a.slug)}>
                <span className="quick-link__icon" aria-hidden="true">
                  <svg width="22" height="22" viewBox="0 0 24 24"><path d={ICON[a.slug]} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>
                </span>
                <span className="quick-link__text"><strong>{a.title}</strong><span className="muted small kb-card__summary">{a.summary}</span></span>
              </a>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

export function ArticlePage({ slug }: { slug: ArticleSlug }) {
  const { t, lang } = useI18n()
  const key = `${lang}/${slug}`
  const [loaded, setLoaded] = useState<{ key: string; article: { text: string; lang: string } | null } | null>(null)
  // undefined while the article for the current language and slug is still loading
  const article = loaded?.key === key ? loaded.article : undefined

  useEffect(() => {
    let alive = true
    void loadArticle(lang, slug).then((a) => { if (alive) setLoaded({ key: `${lang}/${slug}`, article: a }) })
    return () => { alive = false }
  }, [lang, slug])

  return (
    <>
      <BackRow><BackLink href={links.knowledge}>{t('knowledge.back')}</BackLink></BackRow>
      {article === undefined ? <Loader label={t('knowledge.loading')} />
        : article === null ? <p className="muted">{t('knowledge.notFound')}</p>
        : (
          <article className="task article">
            {article.lang !== lang && <p className="muted small article__fallback">{t('knowledge.fallback', { language: LANG_NAME[article.lang as keyof typeof LANG_NAME] })}</p>}
            <Markdown text={article.text} />
          </article>
        )}
    </>
  )
}
