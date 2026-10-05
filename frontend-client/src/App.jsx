import { Menu } from 'lucide-react';
import './WorkspaceMobile.css';
import RealEstateLanding from './components/RealEstateLanding.jsx';
import ReportsAnalytics from './components/ReportsAnalytics.jsx';
import { useEffect, useState } from 'react';
import LeftSidebar from './components/LeftSidebar.jsx';
import PropertySearch from './components/PropertySearch.jsx';
import PropertyDetails from './components/PropertyDetails.jsx';
import ClientDetails from './components/ClientDetails.jsx';
import CRMWorkspace from './components/CRMWorkspace.jsx';
import ErrorBoundary from './components/ErrorBoundary.jsx';
import PropertyMapWorkspace from './components/PropertyMapWorkspace.jsx';
import OwnersTenantsWorkspace from './components/OwnersTenantsWorkspace.jsx';
import ReportsWorkspace from './components/ReportsWorkspace.jsx';
import SystemAdminWorkspace from './components/SystemAdminWorkspace.jsx';
import AgencyGlance from './components/AgencyGlance.jsx';
import BrochureBuilderWorkspace from './components/BrochureBuilderWorkspace.jsx';
import RoleDashboard from './components/RoleDashboard.jsx';
import RolePermissionsPage from './components/RolePermissionsPage.jsx';
import SetPassword from './components/SetPassword.jsx';
import LeadsPage from './components/LeadsPage.jsx';
import DealsPage from './components/DealsPage.jsx';
import TenantSwitcher from './components/TenantSwitcher.jsx';
import BrochureCartBell from './components/BrochureCartBell.jsx';
import BrokerDashboard from './components/BrokerDashboard.jsx';
import NotificationsBell from './components/NotificationsBell.jsx';

import { useAuth } from './contexts/AuthContext.jsx';
import { useAppUi } from './contexts/AppUiContext.jsx';
import { useAgency } from './contexts/AgencyContext.jsx';
import { usePermission } from './contexts/PermissionContext.jsx';
import { useTenant } from './contexts/TenantContext.jsx';
import './index.css';
import SubscriptionGate from './components/SubscriptionGate.jsx';

export default function App() {
  const {
    isLoggedIn,
    userRole,
    errorMessage,
    isConnecting,
    login,
  } = useAuth();

  const {
    currentView,
    mobileNavigationOpen,
    setMobileNavigationOpen,
    selectedPropertyId,
    selectedClientData,
    activeBrochureDealTarget,
    navigateTo,
    selectProperty,
    navigateToClient,
    clearBrochureDealTarget,
  } = useAppUi();

  const { currentAgency } = useAgency();
  const { hasPermission } = usePermission();
  const { isAllTenants } = useTenant();

  const recoveryToken = new URLSearchParams(window.location.search).get('recoveryToken');
  const activationToken = !isLoggedIn ? new URLSearchParams(window.location.search).get('token') : null;

  const handlePasswordSet = () => {
    window.location.href = window.location.pathname;
  };

  // --- Login form local bindings (form-specific, not shared app state) ---
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleLoginSubmit = async (e) => {
    e.preventDefault();
    try {
      await login(email, password);
    } catch { /* error handled in AuthContext */ }
  };

  // 🛡️ Brokers default to the Broker Dashboard on first login. The other
  // roles stay on the generic Role Dashboard. Declared ABOVE the early
  // returns so the hook order is stable across auth→app transitions (rules
  // of hooks). The effect is guarded inside the body — it only navigates
  // when the user is a Broker still sitting on the default dashboard.
  useEffect(() => {
    if (isLoggedIn && userRole === 'Broker' && currentView === 'dashboard') {
      navigateTo('broker-dashboard');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoggedIn]);

  // 0. ACTIVATION GATE: If not logged in but a token query param is present, show set-password
  if (recoveryToken) {
    return <SetPassword recoveryToken={recoveryToken} onPasswordSet={() => { window.location.href = "/"; }} />;
  }
  if (!isLoggedIn && activationToken) {
    return <SetPassword activationToken={activationToken} onPasswordSet={handlePasswordSet} />;
  }

  if (!isLoggedIn) {
    const companyUrl = import.meta.env.VITE_COMPANY_SITE_URL || "https://www.biscovetech.com";
    return <RealEstateLanding>
      <h2>Sign in</h2>
      <p>Continue to your agency workspace.</p>
      <form onSubmit={handleLoginSubmit}>
        {errorMessage && <p className="re-login-error" role="alert">{errorMessage}</p>}
        <label>Email address<input type="email" autoComplete="username" required value={email} onChange={event => setEmail(event.target.value)} disabled={isConnecting} /></label>
        <label>Password<input type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} disabled={isConnecting} /></label>
        <button type="submit" disabled={isConnecting}>{isConnecting ? "Signing in…" : "Sign in to workspace"}</button>
      </form>
      <p className="re-trial-link">New agency? <a href={`${companyUrl}/onboarding`}>Start a trial</a></p>
    </RealEstateLanding>;
  }

  // 2. MAIN SYSTEM DASHBOARD VIEW: Render workspace layers after successful authentication
  return (
    <SubscriptionGate><div className="real-estate-workspace flex bg-slate-950 min-h-screen text-slate-100">

      {/* 🧭 Collapsible Side Navigation Drawer Shell */}
      <LeftSidebar />
      {mobileNavigationOpen && <button className="mobile-navigation-backdrop" aria-label="Dismiss navigation" onClick={() => setMobileNavigationOpen(false)} />}


      {/* 📊 Main Dynamic Workspace Content Window Canvas */}
      <div className="workspace-content flex-1 p-4 md:p-8">
        <header className="workspace-header mb-6 pb-4 border-b border-slate-800">
          <button type="button" className="mobile-navigation-open" aria-label="Open navigation" aria-controls="workspace-navigation" aria-expanded={mobileNavigationOpen} onClick={() => setMobileNavigationOpen(true)}><Menu size={24} /></button>
          <div>
            <h2 className="text-2xl font-bold text-white tracking-tight">
              {currentView === 'search' && "Property Asset Registry"}
              {currentView === 'dashboard' && "Role Dashboard"}
              {currentView === 'broker-dashboard' && "Broker Dashboard"}
              {currentView === 'details' && "Asset Portfolio Insights"}
              {currentView === 'client' && "Legal Entity Dossier"}
              {currentView === 'crm' && "Corporate CRM Pipeline"}
              {currentView === 'owners-tenants' && "Owner & Tenant Directory"}
              {currentView === 'reports' && "Performance Reports"}
              {currentView === 'reports-analytics' && "Reports & Analytics"}
              {currentView === 'map' && "Property Map"}
              {currentView === 'admin' && "SuperAdmin Control Board"}
              {currentView === 'agency-glance' && "Agency Overview"}
              {currentView === 'roles' && "Role & Permission Management"}
            </h2>
            <p className="text-slate-400 text-sm">
              {isAllTenants
                ? 'Multi-tenant spatial intelligence portal — viewing all tenants'
                : `Multi-tenant spatial intelligence portal for ${currentAgency.name}`}
            </p>
          </div>
          <div className="workspace-header-actions flex items-center gap-3">
            <NotificationsBell />
            <BrochureCartBell />
            <TenantSwitcher />
          </div>
        </header>

        {/* ⚙️ Dynamic UI view routing execution conditional dispatcher matrices */}
        {currentView === 'broker-dashboard' && <BrokerDashboard />}
        {currentView === 'dashboard' && <RoleDashboard />}

        {currentView === 'search' && (
          <PropertySearch onSelectProperty={selectProperty} />
        )}

{currentView === 'crm' && activeBrochureDealTarget ? (
          <BrochureBuilderWorkspace
            dealData={activeBrochureDealTarget}
            onBack={clearBrochureDealTarget}
          />
        ) : currentView === 'crm' ? (
          // 🛡️ CRM Directory is wrapped in an Error Boundary so a null /
          // undefined data payload or a hook failure can never crash the
          // whole app — the rest of the workspace keeps working.
          <ErrorBoundary>
            <CRMWorkspace />
          </ErrorBoundary>
        ) : currentView === 'leads' ? (
          <LeadsPage />
        ) : currentView === 'deals' ? (
          <DealsPage />
        ) : null}

        {currentView === 'owners-tenants' && (
          <OwnersTenantsWorkspace />
        )}

        {currentView === 'reports-analytics' && ['AgencyAdmin', 'PlatformAdmin'].includes(userRole) && <ReportsAnalytics />}
        {currentView === 'reports' && (
          <ReportsWorkspace />
        )}

        {currentView === 'map' && (
          <PropertyMapWorkspace onSelectProperty={selectProperty} />
        )}

        {currentView === 'details' && (
          <PropertyDetails 
            propertyId={selectedPropertyId} 
            onBack={() => navigateTo('search')} 
            onNavigateToClient={navigateToClient}
          />
        )}

        {currentView === 'client' && (
          <ClientDetails 
            clientId={selectedClientData.id}
            clientType={selectedClientData.type}
            clientName={selectedClientData.name}
            onBack={() => navigateTo('details')}
          />
        )}

        {currentView === 'agency-glance' && ['AgencyAdmin', 'PlatformAdmin'].includes(userRole) && (
          <AgencyGlance />
        )}
        {currentView === 'admin' && hasPermission('agencies:view') && (
          <SystemAdminWorkspace />
        )}
        {currentView === 'roles' && hasPermission('system:roles:manage') && (
          <RolePermissionsPage />
        )}
      </div>

    </div></SubscriptionGate>
  );
}
