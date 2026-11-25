import type { Lang } from '../i18n/core'

/** Knowledge-base articles in display order. Text is Markdown in knowledge/<language>/<slug>.md. */
export const ARTICLES = ['egzamin', 'bledy', 'pisanie', 'mowienie', 'gramatyka', 'pisownia'] as const
export type ArticleSlug = (typeof ARTICLES)[number]

export function isArticle(slug: string): slug is ArticleSlug {
  return (ARTICLES as readonly string[]).includes(slug)
}

// each article is a separate chunk: the texts do not end up in the home page bundle
const files = import.meta.glob('./*/*.md', { query: '?raw', import: 'default' }) as Record<string, () => Promise<string>>

/** Fallback language order while a translation is missing: the article source is Russian. */
const FALLBACK: Record<Lang, Lang[]> = { ru: ['ru', 'uk'], uk: ['uk', 'ru'], pl: ['pl', 'en', 'ru'], en: ['en', 'pl', 'ru'] }

export async function loadArticle(lang: Lang, slug: ArticleSlug): Promise<{ text: string; lang: Lang } | null> {
  for (const l of FALLBACK[lang]) {
    const load = files[`./${l}/${slug}.md`]
    if (load) return { text: await load(), lang: l }
  }
  return null
}

/** Article title and subtitle: the first "# …" line and the first "> …" quote. */
export function articleMeta(text: string): { title: string; summary: string } {
  const lines = text.replace(/\r\n/g, '\n').split('\n')
  const title = lines.find((l) => l.startsWith('# '))?.slice(2).trim() ?? ''
  const summary = lines.find((l) => l.startsWith('> '))?.slice(2).trim() ?? ''
  return { title: stripInline(title), summary: stripInline(summary) }
}

function stripInline(s: string): string {
  return s.replace(/\*\*(.+?)\*\*/g, '$1').replace(/\*(.+?)\*/g, '$1').replace(/`(.+?)`/g, '$1')
}
