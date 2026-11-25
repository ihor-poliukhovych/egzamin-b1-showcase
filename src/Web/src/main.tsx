import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './ui.css'
import App from './App.tsx'
import I18nProvider from './i18n/I18nProvider'
import { ConfirmProvider } from './components/ConfirmDialog'
import { applyTheme, readTheme } from './theme'

// theme before first paint — no flash of a light background
applyTheme(readTheme())
// after a page refresh always start at the top: on iOS, scroll restoration prevents the sticky menu from painting until you scroll
if ('scrollRestoration' in history) history.scrollRestoration = 'manual'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <ConfirmProvider>
        <App />
      </ConfirmProvider>
    </I18nProvider>
  </StrictMode>,
)
