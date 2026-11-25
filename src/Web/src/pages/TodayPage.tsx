import { useEffect, useState } from 'react'
import { api, type Today, type TodayItem } from '../api'
import { formatDate } from '../format'
import { useConfirm } from '../components/confirm'
import { Loader } from '../components/Loader'
import { TrashIcon } from '../components/TrashIcon'
import { useI18n } from '../i18n'

export default function TodayPage() {
  const { t, lang, errorText } = useI18n()
  const confirm = useConfirm()
  const [today, setToday] = useState<Today | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.today(lang).then(setToday).catch((e: unknown) => setError(errorText(e)))
  }, [lang, errorText])

  async function remove(patternId: string) {
    if (!(await confirm({ message: t('today.confirmDelete'), confirmLabel: t('today.delete'), danger: true }))) return
    try {
      await api.deletePattern(patternId)
      setToday((cur) => cur && { ...cur, items: cur.items.filter((i) => i.pattern.id !== patternId) })
    } catch (e) {
      setError(errorText(e))
    }
  }

  if (error) return <p className="error">{t('common.errorPrefix', { message: error })}</p>
  if (!today) return <Loader label={t('today.loading')} showLabel />

  return (
    <>
      <header className="header">
        <h1>{t('today.title')}</h1>
        <p className="muted">{t('today.summary', { date: formatDate(today.date), total: today.items.length })}</p>
      </header>

      {today.items.length === 0 && <p className="muted">{t('today.empty')}</p>}

      {groupByType(today.items).map((group) => (
        <section key={group.name} className="blitz-group">
          <h2 className="blitz-group__title">
            {group.name}
            <span className="count-badge">{t('today.groupCount', { total: group.items.length })}</span>
          </h2>
          <ol className="mistakes">
            {group.items.map((item) => (
              <MistakeRow key={item.pattern.id} item={item} onDelete={remove} />
            ))}
          </ol>
        </section>
      ))}
    </>
  )
}

/** Top mistakes in blocks: mistakes of one type (AI group, otherwise category) go in a row; types with the most failures first. */
function groupByType(items: TodayItem[]): { name: string; items: TodayItem[] }[] {
  const groups = new Map<string, TodayItem[]>()
  for (const item of items) {
    const name = item.pattern.groupName ?? item.pattern.category
    groups.set(name, [...(groups.get(name) ?? []), item])
  }
  const falls = (list: TodayItem[]) => list.reduce((sum, i) => sum + i.pattern.fallsCount, 0)
  return [...groups]
    .map(([name, list]) => ({ name, items: [...list].sort((a, b) => b.pattern.fallsCount - a.pattern.fallsCount) }))
    .sort((a, b) => falls(b.items) - falls(a.items))
}

/** "Gramatyka 2024-06 · błąd gramatyczny: ukochaną" → "Gramatyka 2024-06": where the mistake comes from. */
function sourceOf(title: string): string {
  return title.split(' · ')[0]
}

// "Zadanie I: „…”" — context from a Gramatyka task; a bare "Zadanie I:" (old records) is not shown
const CONTEXT_LINE = /^Zadanie [IVXLC]+:?\s*(.*)$/

/** Mistake rule: explanation lines and, separately, the task sentence where the gap is filled with the correct form. */
function RuleText({ rule, correct }: { rule: string; correct: string }) {
  const lines = rule.split('\n').map((l) => l.trim()).filter(Boolean)
  return (
    <>
      {lines.map((line, i) => {
        const context = CONTEXT_LINE.exec(line)
        if (!context) return <p key={i} className="mistake__rule">{line}</p>
        const sentence = context[1].replace(/^„|”$/g, '')
        if (!sentence) return null
        const parts = sentence.split('___')
        return (
          <p key={i} className="mistake__context">
            {parts.map((part, j) => <span key={j}>{j > 0 && <mark className="mistake__gap">{correct}</mark>}{part}</span>)}
          </p>
        )
      })}
    </>
  )
}

/** A mistake in one line: was → correct, rule and context below; delete is a quiet icon. */
function MistakeRow({ item, onDelete }: { item: TodayItem; onDelete: (id: string) => void }) {
  const { t } = useI18n()
  const { pattern } = item

  return (
    <li className="mistake">
      <div className="mistake__main">
        <p className="mistake__pair">
          <span className="wrong">{pattern.wrongExample || '—'}</span>
          <span className="mistake__arrow" aria-hidden="true">→</span>
          <span className="correct">{pattern.correctForm}</span>
        </p>
        <RuleText rule={pattern.rule} correct={pattern.correctForm} />
        {pattern.contrastRuUa && <p className="mistake__rule muted">{t('today.contrast')}: {pattern.contrastRuUa}</p>}
        <p className="mistake__meta muted small">
          {pattern.code && <span className="tag">{pattern.code}</span>}
          <span>{sourceOf(pattern.title)}</span>
          {pattern.fallsCount > 1 && <span>{t('today.counters', { falls: pattern.fallsCount })}</span>}
        </p>
      </div>
      <button type="button" className="btn btn--icon btn--sm btn--quiet" onClick={() => onDelete(pattern.id)} aria-label={t('today.delete')} title={t('today.delete')}>
        <TrashIcon />
      </button>
    </li>
  )
}
