import { useState } from 'react'
import { useI18n } from '../i18n'

/** Image served by the API under /media; if it fails to load, a small neutral note replaces the broken-image icon. */
export function MediaImage({ src, alt, className }: { src: string; alt: string; className?: string }) {
  const { t } = useI18n()
  const [failed, setFailed] = useState(false)
  if (failed) return <span className="media-missing" role="img" aria-label={alt}>{t('run.imageMissing')}</span>
  return <img className={className} src={src} alt={alt} loading="lazy" onError={() => setFailed(true)} />
}
