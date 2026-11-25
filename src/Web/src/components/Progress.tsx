import { useMemo } from 'react'
import type { ArkuszSummary, DashboardData } from '../api'
import { activity, buildSets, lookupFromMatrix, PASS_PERCENT } from '../examSets'
import { useI18n } from '../i18n'
import { formatPoints } from '../format'
import { PART_LABEL } from '../parts'

const TREND_ARROW = { up: '↗', flat: '→', down: '↘', none: '' } as const
const WARN_MARGIN = 15

type Tone = 'ok' | 'warn' | 'low' | 'none'
function tone(percent: number | null): Tone {
  if (percent === null) return 'none'
  const margin = percent - PASS_PERCENT
  return margin < 0 ? 'low' : margin < WARN_MARGIN ? 'warn' : 'ok'
}

/** Small chart of the latest results of a part with a dashed 50 % threshold line. */
export function Sparkline({ values, label }: { values: number[]; label: string }) {
  const w = 120
  const h = 44
  const pad = 4
  const y = (v: number) => pad + (1 - Math.max(0, Math.min(100, v)) / 100) * (h - pad * 2)
  const x = (i: number) => (values.length === 1 ? w / 2 : pad + (i / (values.length - 1)) * (w - pad * 2))
  const points = values.map((v, i) => `${x(i).toFixed(1)},${y(v).toFixed(1)}`).join(' ')
  const last = values[values.length - 1]
  return (
    <svg className={`spark spark--${tone(last)}`} viewBox={`0 0 ${w} ${h}`} role="img" aria-label={label} preserveAspectRatio="none">
      <line className="spark__threshold" x1="0" x2={w} y1={y(PASS_PERCENT)} y2={y(PASS_PERCENT)} />
      {values.length > 1 && <polyline className="spark__line" points={points} fill="none" vectorEffect="non-scaling-stroke" />}
      {values.map((v, i) => <circle key={i} className={i === values.length - 1 ? 'spark__dot spark__dot--last' : 'spark__dot'} cx={x(i)} cy={y(v)} r={i === values.length - 1 ? 3.2 : 2} />)}
    </svg>
  )
}

function Readiness({ data }: { data: DashboardData }) {
  const { t } = useI18n()
  return (
    <div className="readiness">
      {data.readiness.map((r) => {
        const percent = r.last.points === null ? null : r.last.percent ?? 0
        const toneName = tone(percent)
        const margin = percent === null ? null : Math.round(percent - PASS_PERCENT)
        const trend = r.trend.map((v) => Math.round(v))
        return (
          <div key={r.kind} className={`rd rd--${toneName}`}>
            <div className="rd__name">{PART_LABEL[r.kind]}</div>
            {percent === null ? <div className="rd__value rd__value--none">{t('progress.noData')}</div> : (
              <>
                <div className="rd__value">{Math.round(percent)} % <span className="rd__arrow" aria-hidden="true">{TREND_ARROW[r.direction]}</span></div>
                <div className="rd__bar" role="img" aria-label={t('progress.barLabel', { percent: Math.round(percent), pass: PASS_PERCENT })}>
                  <span className="rd__fill" style={{ width: `${Math.min(100, percent)}%` }} />
                  <span className="rd__tick" />
                </div>
                <div className="rd__margin">
                  {margin! >= 0 ? t('progress.marginAbove', { margin: margin! }) : t('progress.marginBelow', { margin: -margin! })}
                  <span className="muted"> · {formatPoints(r.last.points!)}/{formatPoints(r.last.maxPoints)} p.</span>
                </div>
                {trend.length > 0 && <Sparkline values={trend} label={t('progress.sparkLabel', { part: PART_LABEL[r.kind], values: trend.join(', ') })} />}
              </>
            )}
          </div>
        )
      })}
    </div>
  )
}

function SetsProgress({ data, arkusze }: { data: DashboardData; arkusze: ArkuszSummary[] }) {
  const { t, tp } = useI18n()
  const sets = useMemo(() => buildSets(arkusze.filter((a) => data.arkuszCodes.includes(a.code)), lookupFromMatrix(data.matrix)), [arkusze, data])
  const passed = sets.filter((s) => s.status === 'passed').length
  const act = useMemo(() => activity(data.history.map((h) => h.submittedAt)), [data.history])
  const total = sets.length
  return (
    <div className="stats">
      <div className="stat">
        <div className="stat__value">{passed}<span className="stat__of">{t('progress.ofTotal', { total })}</span></div>
        <div className="stat__label">{tp('progress.setsPassed', passed)}</div>
        <div className="stat__bar" aria-hidden="true"><span style={{ width: total ? `${(passed / total) * 100}%` : '0%' }} /></div>
      </div>
      <div className="stat">
        <div className="stat__value">{act.streak}<span className="stat__of">{tp('progress.days', act.streak)}</span></div>
        <div className="stat__label">{t('progress.streak')}</div>
        <div className="days" role="img" aria-label={t('progress.activityLabel', { values: act.days.map((d) => d.count).join(', ') })}>
          {act.days.map((d) => <span key={d.date} title={`${d.date}: ${d.count}`} className={`days__cell ${d.count > 0 ? (d.count > 1 ? 'days__cell--2' : 'days__cell--1') : ''}`} />)}
        </div>
      </div>
    </div>
  )
}

function Patterns({ data }: { data: DashboardData }) {
  const { t } = useI18n()
  const max = Math.max(1, ...data.topPatterns.map((p) => p.fallsCount))
  if (data.topPatterns.length === 0) return <p className="muted">{t('progress.noPatterns')}</p>
  return (
    <ul className="bars">
      {data.topPatterns.map((p) => (
        <li key={p.title} className="bars__row">
          <span className="bars__label">{p.title}</span>
          <span className="bars__track" aria-hidden="true"><span style={{ width: `${(p.fallsCount / max) * 100}%` }} /></span>
          <span className="bars__n">{p.fallsCount}</span>
        </li>
      ))}
    </ul>
  )
}

export function ProgressBoard({ data, arkusze }: { data: DashboardData; arkusze: ArkuszSummary[] | null }) {
  const { t } = useI18n()
  return (
    <>
      <section className="task">
        <h2 className="task__title">{t('progress.readiness')}</h2>
        <p className="muted small">{t('progress.readinessHint')}</p>
        <Readiness data={data} />
      </section>
      {arkusze && (
        <section className="task">
          <h2 className="task__title">{t('progress.levelProgress')}</h2>
          <SetsProgress data={data} arkusze={arkusze} />
        </section>
      )}
    </>
  )
}

export { Patterns }
