import { createContext, useContext } from 'react'
import type { Key, Lang, Params, PluralKey } from './core'

export interface I18n {
  lang: Lang
  /** Changes the UI language and remembers the choice on this device. */
  setLang: (lang: Lang) => void
  t: (key: Key, params?: Params) => string
  /** Plural form chosen by the language rules: tp('exams.count', 5). */
  tp: (base: PluralKey, count: number, params?: Params) => string
  /** Localized error message: the client builds it from the API status and ProblemDetails code. */
  errorText: (error: unknown) => string
}

export const I18nContext = createContext<I18n | null>(null)

export function useI18n(): I18n {
  const ctx = useContext(I18nContext)
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider')
  return ctx
}
