import { LANGS, LANG_LABEL, LANG_NAME } from '../i18n/core'
import { useI18n } from '../i18n'

/** Language switch: short "UA | RU | PL | EN" on the sign-in page, full names in the profile. */
export default function LangSwitch({ className = '', full = false }: { className?: string; full?: boolean }) {
  const { lang, setLang, t } = useI18n()
  return (
    <div className={`langswitch ${className}`} role="group" aria-label={t('lang.label')}>
      {LANGS.map((l) => (
        <button key={l} type="button" lang={l} title={LANG_NAME[l]} aria-pressed={lang === l}
          className={`langswitch__btn ${lang === l ? 'langswitch__btn--on' : ''}`} onClick={() => setLang(l)}>
          {full ? LANG_NAME[l] : LANG_LABEL[l]}
        </button>
      ))}
    </div>
  )
}
