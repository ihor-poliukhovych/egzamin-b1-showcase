import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { useI18n } from '../i18n'
import { ConfirmContext, type Ask, type ConfirmOptions } from './confirm'

/** "Delete the mistake? It cannot be undone." → title up to and including "?", the rest is the explanation. */
function split(message: string): { title: string; body: string } {
  const at = message.indexOf('?')
  if (at < 0 || at === message.length - 1) return { title: message, body: '' }
  return { title: message.slice(0, at + 1).trim(), body: message.slice(at + 1).trim() }
}

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const { t } = useI18n()
  const [request, setRequest] = useState<(ConfirmOptions & { resolve: (ok: boolean) => void }) | null>(null)
  const dialog = useRef<HTMLDialogElement>(null)
  const confirmButton = useRef<HTMLButtonElement>(null)

  const ask = useCallback<Ask>((options) => new Promise<boolean>((resolve) => setRequest({ ...options, resolve })), [])

  useEffect(() => {
    const el = dialog.current
    if (!request || !el) return
    if (!el.open) el.showModal()
    confirmButton.current?.focus()
  }, [request])

  function close(ok: boolean) {
    dialog.current?.close()
    request?.resolve(ok)
    setRequest(null)
  }

  const text = request ? split(request.message) : { title: '', body: '' }

  return (
    <ConfirmContext.Provider value={ask}>
      {children}
      <dialog ref={dialog} className={`confirm ${request?.danger ? 'confirm--danger' : ''}`} aria-labelledby="confirm-title"
        onCancel={(e) => { e.preventDefault(); close(false) }}
        onClick={(e) => { if (e.target === e.currentTarget) close(false) }}>
        {request && (
          <div className="confirm__body">
            <h2 id="confirm-title" className="confirm__title">{text.title}</h2>
            {text.body && <p className="confirm__text">{text.body}</p>}
            <div className="confirm__actions">
              <button type="button" className="btn btn--ghost" onClick={() => close(false)}>{t('confirm.cancel')}</button>
              <button type="button" ref={confirmButton} className={`btn ${request.danger ? 'btn--danger' : 'btn--primary'}`} onClick={() => close(true)}>
                {request.confirmLabel ?? t('confirm.ok')}
              </button>
            </div>
          </div>
        )}
      </dialog>
    </ConfirmContext.Provider>
  )
}
