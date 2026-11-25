import { useEffect, useId, useRef, useState } from 'react'

/** Hint icon: on click shows a list of questions in a popover; closes on outside click or Esc. */
export function HintPopover({ label, items }: { label: string; items: string[] }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLSpanElement>(null)
  const id = useId()

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey) }
  }, [open])

  return (
    <span className="hint" ref={ref}>
      <button type="button" className="hint__btn" aria-label={label} title={label} aria-expanded={open} aria-controls={id} onClick={() => setOpen((v) => !v)}>
        <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2V16h5v-.1c0-.8.4-1.5 1-2A6 6 0 0 0 12 3Z" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      {open && (
        <span className="hint__pop" id={id} role="dialog" aria-label={label}>
          <strong className="hint__title">{label}</strong>
          <ul className="hint__list">{items.map((q) => <li key={q}>{q}</li>)}</ul>
        </span>
      )}
    </span>
  )
}
