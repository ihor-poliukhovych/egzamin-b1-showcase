import uk from './uk.json'
import pl from './pl.json'
import en from './en.json'
import ru from './ru.json'

export const LANGS = ['uk', 'ru', 'pl', 'en'] as const
export type Lang = (typeof LANGS)[number]
export const LANG_LABEL: Record<Lang, string> = { uk: 'UA', ru: 'RU', pl: 'PL', en: 'EN' }
export const LANG_NAME: Record<Lang, string> = { uk: 'Українська', ru: 'Русский', pl: 'Polski', en: 'English' }
export const LOCALE: Record<Lang, string> = { uk: 'uk-UA', ru: 'ru-RU', pl: 'pl-PL', en: 'en-GB' }

/** Keys come from the Ukrainian dictionary; a test checks that the others have exactly the same set. */
export type Key = keyof typeof uk
export type Params = Record<string, string | number>
/** Base key of a plural group: "exams.count" for "exams.count_one" / "…_other". */
export type PluralKey = Key extends infer K ? (K extends `${infer B}_other` ? B : never) : never

export const dictionaries: Record<Lang, Record<string, string>> = { uk, ru, pl, en }

const STORAGE_KEY = 'b1.lang'

export function isLang(value: unknown): value is Lang {
  return typeof value === 'string' && (LANGS as readonly string[]).includes(value)
}

/** Browser language: uk → UA, ru/be → RU, pl → PL, en → EN, anything else → UA. */
export function detectLang(navigatorLanguage: string | undefined): Lang {
  const base = (navigatorLanguage ?? '').toLowerCase().split('-')[0]
  return base === 'pl' ? 'pl' : base === 'en' ? 'en' : base === 'ru' || base === 'be' ? 'ru' : 'uk'
}

/** The user's saved choice on this device (before sign-in this is the only storage). */
export function storedLang(): Lang | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY)
    return isLang(value) ? value : null
  } catch {
    return null
  }
}

export function storeLang(lang: Lang) {
  try {
    window.localStorage.setItem(STORAGE_KEY, lang)
  } catch {
    // private mode: the language simply is not remembered
  }
}

export function initialLang(): Lang {
  return storedLang() ?? detectLang(typeof navigator === 'undefined' ? undefined : navigator.language)
}

function fill(template: string, params?: Params) {
  return params ? template.replace(/\{(\w+)\}/g, (m, name: string) => (name in params ? String(params[name]) : m)) : template
}

export function translate(lang: Lang, key: string, params?: Params): string {
  const template = dictionaries[lang][key] ?? dictionaries.uk[key] ?? key
  return fill(template, params)
}

/** Plural form by the language rules (uk/pl: one, few, many; en: one, other). */
export function translatePlural(lang: Lang, base: string, count: number, params?: Params): string {
  const category = new Intl.PluralRules(LOCALE[lang]).select(count)
  const dict = dictionaries[lang]
  const template = dict[`${base}_${category}`] ?? dict[`${base}_other`] ?? dictionaries.uk[`${base}_other`] ?? base
  return fill(template, { count, ...params })
}

/** Placeholder marking where a link goes inside a translated phrase: t('key', { link: MARK }) -> WithSlot. */
export const MARK = String.fromCharCode(0)
