let locale = 'uk-UA'

/** Interface locale for dates and numbers; set by the language provider. */
export function setFormatLocale(next: string) {
  locale = next
}

export function formatDate(iso: string) {
  return new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(iso))
}

export function formatDateTime(iso: string) {
  return new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(iso))
}

/** "2 hours ago", "yesterday": how long ago an event happened, in the interface language. */
export function formatRelative(iso: string) {
  const seconds = (Date.parse(iso) - Date.now()) / 1000
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  const abs = Math.abs(seconds)
  if (abs < 60) return rtf.format(Math.round(seconds), 'second')
  if (abs < 3600) return rtf.format(Math.round(seconds / 60), 'minute')
  if (abs < 86400) return rtf.format(Math.round(seconds / 3600), 'hour')
  if (abs < 86400 * 30) return rtf.format(Math.round(seconds / 86400), 'day')
  return formatDate(iso)
}

export function formatPoints(value: number) {
  return new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value)
}

export function formatPercent(value: number) {
  return `${formatPoints(value)} %`
}

export function formatMonthYear(year: number, month: number) {
  const text = new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1))
  return text.charAt(0).toLocaleUpperCase(locale) + text.slice(1)
}

export function formatDuration(totalSeconds: number) {
  const sign = totalSeconds < 0 ? '−' : ''
  const abs = Math.abs(totalSeconds)
  const m = Math.floor(abs / 60)
  const s = abs % 60
  return `${sign}${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

/** Today's date as YYYY-MM-DD (UTC), e.g. for <input type="date" min>. Call it from a lazy state initializer, not during render. */
export const todayIso = () => new Date().toISOString().slice(0, 10)
