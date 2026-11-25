import { useEffect, useState } from 'react'
import { api, type SiteInfo } from './api'

const FALLBACK: SiteInfo = { name: 'Polski B1', ownerName: '[imię i nazwisko administratora]', contactEmail: 'kontakt@example.com', devLogin: false }
let cached: Promise<SiteInfo> | null = null

/** Service owner details from the API configuration (Site:*). Loaded once; placeholders are shown on failure. */
export function useSite(): SiteInfo {
  const [site, setSite] = useState<SiteInfo>(FALLBACK)
  useEffect(() => {
    cached ??= api.site().catch(() => FALLBACK)
    let alive = true
    void cached.then((s) => { if (alive) setSite(s) })
    return () => { alive = false }
  }, [])
  return site
}

export const BUILD: string = typeof __BUILD__ === 'string' ? __BUILD__ : 'dev'
export const CERTIFICATE_SITE = 'https://certyfikatpolski.pl'
