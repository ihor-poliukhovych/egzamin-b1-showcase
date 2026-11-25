import { useState } from 'react'
import { api, type Me } from '../api'
import { useI18n } from '../i18n'
import type { LevelStat } from '../examSets'
import { LEVELS, VARIANTS } from '../levels'


/** Saves the choice as "my target level" and reports the updated profile upwards. */
function useTarget(me: Me, onChange: (next: Me) => void) {
  const { t, errorText } = useI18n()
  const [error, setError] = useState<string | null>(null)
  async function change(level: string, variant: string) {
    if (level === me.targetLevel && variant === me.targetVariant) return
    setError(null)
    try {
      await api.updateSettings(level, variant, me.uiLanguage || undefined)
      onChange({ ...me, targetLevel: level, targetVariant: variant })
    } catch (e) {
      setError(t('picker.saveFailed', { message: errorText(e) }))
    }
  }
  return { change, error }
}

export function VariantSwitch({ me, onChange }: { me: Me; onChange: (next: Me) => void }) {
  const { t } = useI18n()
  const { change, error } = useTarget(me, onChange)
  return (
    <div>
      <div className="segmented" role="group" aria-label={t('variant.group')}>
        {VARIANTS.map((v) => (
          <button key={v.id} type="button" className={`segmented__btn ${me.targetVariant === v.id ? 'segmented__btn--on' : ''}`}
            aria-pressed={me.targetVariant === v.id} onClick={() => void change(me.targetLevel, v.id)}>{t(v.label)}</button>
        ))}
      </div>
      {error && <p className="error small">{error}</p>}
    </div>
  )
}

/** Large level tiles with the number of exams and progress. */
export function LevelTiles({ me, onChange, stats }: { me: Me; onChange: (next: Me) => void; stats: LevelStat[] }) {
  const { t, tp } = useI18n()
  const { change, error } = useTarget(me, onChange)
  return (
    <div>
      <div className="level-tiles" role="group" aria-label={t('picker.levelGroup')}>
        {stats.map((s) => {
          const on = me.targetLevel === s.level
          const empty = s.total === 0
          return (
            <button key={s.level} type="button" aria-pressed={on}
              className={`level-tile ${on ? 'level-tile--on' : ''} ${empty ? 'level-tile--empty' : ''}`}
              onClick={() => void change(s.level, me.targetVariant)}>
              <span className="level-tile__level">{s.level}</span>
              <span className="level-tile__count">{empty ? t('tiles.empty') : tp('tiles.exams', s.total)}</span>
              <span className="level-tile__bar" aria-hidden="true">
                <span style={{ width: s.total ? `${(s.passed / s.total) * 100}%` : '0%' }} />
              </span>
              <span className="level-tile__progress">{empty ? ' ' : t('tiles.progress', { passed: s.passed, total: s.total })}</span>
            </button>
          )
        })}
      </div>
      {error && <p className="error small">{error}</p>}
    </div>
  )
}

/** Compact picker for the dashboards: the same segmented level and variant switch. */
export default function LevelPicker({ me, onChange }: { me: Me; onChange: (next: Me) => void }) {
  const { t } = useI18n()
  const { change, error } = useTarget(me, onChange)
  return (
    <div className="level-picker">
      <div className="segmented" role="group" aria-label={t('picker.level')}>
        {LEVELS.map((l) => (
          <button key={l} type="button" className={`segmented__btn ${me.targetLevel === l ? 'segmented__btn--on' : ''}`}
            aria-pressed={me.targetLevel === l} onClick={() => void change(l, me.targetVariant)}>{l}</button>
        ))}
      </div>
      <VariantSwitch me={me} onChange={onChange} />
      {error && <p className="error small">{error}</p>}
    </div>
  )
}
