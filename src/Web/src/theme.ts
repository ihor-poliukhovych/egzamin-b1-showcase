import { useCallback, useState } from 'react'

/** Interface theme: "as in system" (default), light or dark. The choice is stored only in this browser. */
export type Theme = 'system' | 'light' | 'dark'
export const THEMES: Theme[] = ['system', 'light', 'dark']
const STORAGE_KEY = 'b1.theme'

export function readTheme(): Theme {
  try {
    const v = window.localStorage.getItem(STORAGE_KEY)
    return v === 'light' || v === 'dark' ? v : 'system'
  } catch {
    return 'system'
  }
}

/** data-theme on <html> switches the tokens in index.css; without the attribute prefers-color-scheme applies. */
export function applyTheme(theme: Theme) {
  const root = document.documentElement
  if (theme === 'system') delete root.dataset.theme
  else root.dataset.theme = theme
}

export function useTheme(): [Theme, (t: Theme) => void] {
  const [theme, setThemeState] = useState<Theme>(readTheme)
  const setTheme = useCallback((next: Theme) => {
    setThemeState(next)
    applyTheme(next)
    try {
      if (next === 'system') window.localStorage.removeItem(STORAGE_KEY)
      else window.localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // private mode: the theme simply will not be remembered
    }
  }, [])
  return [theme, setTheme]
}
