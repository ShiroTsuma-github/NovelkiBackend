import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/inter/index.css'
import '@fontsource-variable/ibm-plex-sans/index.css'
import './index.css'
import { App } from './app/App'
import { getProductionHttpsRedirectUrl } from './httpsRedirect'

const httpsRedirectUrl = getProductionHttpsRedirectUrl(
  window.location,
  import.meta.env.PROD,
  import.meta.env.VITE_PUBLIC_ORIGIN,
)

if (httpsRedirectUrl) {
  window.location.replace(httpsRedirectUrl)
} else {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}
