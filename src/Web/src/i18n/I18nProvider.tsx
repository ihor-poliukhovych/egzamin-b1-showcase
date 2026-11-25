import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { setFormatLocale } from '../format'
import { initialLang, LOCALE, storeLang, translate, translatePlural, type Lang } from './core'
import { errorMessage } from './errors'
import { I18nContext, type I18n } from './index'

export default function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(initialLang)
  // errorText must stay stable (it is a dependency of data-loading effects, and a language switch must not refetch),
  // so it reads the language from a ref that is only written in the setLang event handler.
  const langRef = useRef(lang)
  const errorText = useCallback((error: unknown) => {
    const { key, params } = errorMessage(error)
    return translate(langRef.current, key, params)
  }, [])

  // date and number formatting reads the current locale; set it before the children render
  setFormatLocale(LOCALE[lang])
  useEffect(() => { document.documentElement.lang = lang }, [lang])

  const setLang = useCallback((next: Lang) => {
    langRef.current = next
    storeLang(next)
    setLangState(next)
  }, [])

  const value = useMemo<I18n>(() => ({
    lang,
    setLang,
    t: (key, params) => translate(lang, key, params),
    tp: (base, count, params) => translatePlural(lang, base, count, params),
    errorText,
  }), [lang, setLang, errorText])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}
