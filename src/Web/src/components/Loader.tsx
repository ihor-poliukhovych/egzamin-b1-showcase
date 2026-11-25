/**
 * Loading indicator: three pulsing dots. The default text is for screen readers only;
 * `showLabel` is for when the caption helps sighted users too (a long AI check).
 */
export function Loader({ label, inline = false, showLabel = false }: { label: string; inline?: boolean; showLabel?: boolean }) {
  return (
    <span className={`loader ${inline ? 'loader--inline' : ''}`} role="status" aria-live="polite">
      <span className="loader__dots" aria-hidden="true"><i /><i /><i /></span>
      <span className={showLabel ? 'loader__label' : 'visually-hidden'}>{label}</span>
    </span>
  )
}
