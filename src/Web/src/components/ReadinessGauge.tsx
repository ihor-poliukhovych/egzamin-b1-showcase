import { useI18n } from '../i18n'

/** Scale zones: below the 50 % threshold, borderline up to 65 %, above — with a margin. */
const LOW = 50
const HIGH = 65
const CX = 120
const CY = 116
const R = 92

/** Point on the semicircle for a fraction 0…1 (0 is left, 1 is right). */
function point(fraction: number, radius = R): [number, number] {
  const angle = Math.PI * (1 - fraction)
  return [CX + radius * Math.cos(angle), CY - radius * Math.sin(angle)]
}

function arc(from: number, to: number): string {
  const [x1, y1] = point(from)
  const [x2, y2] = point(to)
  return `M ${x1.toFixed(1)} ${y1.toFixed(1)} A ${R} ${R} 0 0 1 ${x2.toFixed(1)} ${y2.toFixed(1)}`
}

/**
 * "Readiness level" — a speedometer with a needle: average of the latest results per part,
 * an unsubmitted part counts as 0 %. The zone color and caption say whether this is enough for the exam.
 */
export function ReadinessGauge({ value, caption, empty = false }: { value: number; caption: string; empty?: boolean }) {
  const { t } = useI18n()
  const v = Math.max(0, Math.min(100, Math.round(value)))
  const zone = v < LOW ? 'low' : v < HIGH ? 'mid' : 'high'
  const label = empty ? t('cabinet.readinessEmpty') : zone === 'low' ? t('cabinet.readinessLow') : zone === 'mid' ? t('cabinet.readinessMid') : t('cabinet.readinessHigh')
  const [nx, ny] = point(v / 100, R - 14)

  return (
    <figure className={`gauge gauge--${empty ? 'empty' : zone}`}>
      <svg viewBox="0 0 240 132" role="img" aria-label={`${t('cabinet.readiness')}: ${v} % — ${label}`}>
        <path d={arc(0, LOW / 100 - 0.005)} className="gauge__zone gauge__zone--low" />
        <path d={arc(LOW / 100 + 0.005, HIGH / 100 - 0.005)} className="gauge__zone gauge__zone--mid" />
        <path d={arc(HIGH / 100 + 0.005, 1)} className="gauge__zone gauge__zone--high" />
        {[0, 50, 100].map((tick) => {
          const [tx, ty] = point(tick / 100, R + 16)
          return <text key={tick} x={tx} y={ty + 4} className="gauge__tick" textAnchor="middle">{tick}</text>
        })}
        <line x1={CX} y1={CY} x2={nx} y2={ny} className="gauge__needle" />
        <circle cx={CX} cy={CY} r="9" className="gauge__hub" />
      </svg>
      <figcaption className="gauge__caption">
        <strong className="gauge__value">{empty ? '—' : `${v} %`}</strong>
        <span className="gauge__label">{label}</span>
        <span className="muted small">{caption}</span>
      </figcaption>
    </figure>
  )
}
