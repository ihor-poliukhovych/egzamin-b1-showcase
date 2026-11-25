import { createContext, useContext } from 'react'

export interface ConfirmOptions {
  /** The whole question as in the translations ("Delete …? This cannot be undone."); the first sentence becomes the title. */
  message: string
  confirmLabel?: string
  /** Irreversible action: red confirm button. */
  danger?: boolean
}

export type Ask = (options: ConfirmOptions) => Promise<boolean>

export const ConfirmContext = createContext<Ask>(async (o) => window.confirm(o.message))

/** Replaces the system window.confirm: a dialog in the site's style, focused on the button and closable with Esc. */
export function useConfirm(): Ask {
  return useContext(ConfirmContext)
}
