import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import 'leaflet/dist/leaflet.css'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './contexts/AuthContext.jsx'
import { AppUiProvider } from './contexts/AppUiContext.jsx'
import { AgencyProvider } from './contexts/AgencyContext.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AuthProvider>
      <AppUiProvider>
        <AgencyProvider>
          <App />
        </AgencyProvider>
      </AppUiProvider>
    </AuthProvider>
  </StrictMode>,
)
