import type { ReactNode } from 'react'
import { MARK } from '../i18n/core'

/** Inserts a node (usually a link) in place of the marker in a localized phrase. */
export function WithSlot({ text, children }: { text: string; children: ReactNode }) {
  const [before, after = ''] = text.split(MARK)
  return <>{before}{children}{after}</>
}
