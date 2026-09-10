import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import 'leaflet/dist/leaflet.css'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './contexts/AuthContext.jsx'
import { AppUiProvider } from './contexts/AppUiContext.jsx'
import { AgencyProvider } from './contexts/AgencyContext.jsx'
import { PermissionProvider } from './contexts/PermissionContext.jsx'
import { TenantProvider } from './contexts/TenantContext.jsx'
import { CelebrationProvider } from './components/CelebrationOverlay.jsx';
import { EpilepsySafeModeProvider } from './components/EpilepsySafeMode.jsx';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AuthProvider>
      <AppUiProvider>
        <TenantProvider>
          <PermissionProvider>
            <AgencyProvider>
              <EpilepsySafeModeProvider>
                <CelebrationProvider>
                  <App />
                </CelebrationProvider>
              </EpilepsySafeModeProvider>
            </AgencyProvider>
          </PermissionProvider>
        </TenantProvider>
      </AppUiProvider>
    </AuthProvider>
  </StrictMode>,
)
