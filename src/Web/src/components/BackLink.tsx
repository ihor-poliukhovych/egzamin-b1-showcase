import type { ReactNode } from 'react'

/** Unified "back" button: the arrow is drawn here, it is not in the translation texts. */
export function BackLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a className="back-btn" href={href}>
      <svg className="back-btn__icon" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
        <path d="M10 3 5 8l5 5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <span>{children}</span>
    </a>
  )
}

/** A row of "back" buttons (for example, "To session" and "To list"). */
export function BackRow({ children }: { children: ReactNode }) {
  return <nav className="back-row">{children}</nav>
}
