import { useEffect, useState } from 'react';
import LeftSidebar from './components/LeftSidebar.jsx';
import PropertySearch from './components/PropertySearch.jsx';
import PropertyDetails from './components/PropertyDetails.jsx';
import ClientDetails from './components/ClientDetails.jsx';
import CRMWorkspace from './components/CRMWorkspace.jsx';
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
import { isTrialUser, getTrialExpiry } from './services/authService.js';
import './index.css';

export default function App() {
  const {
    isLoggedIn,
    userRole,
    isRegistering,
    errorMessage,
    isConnecting,
    login,
    register,
    setRegistering,
  } = useAuth();

  const {
    currentView,
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

  const activationToken = !isLoggedIn ? new URLSearchParams(window.location.search).get('token') : null;

  const handlePasswordSet = () => {
    window.location.href = window.location.pathname;
  };

  // --- Login form local bindings (form-specific, not shared app state) ---
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  // --- Register form local bindings ---
  const [registerName, setRegisterName] = useState('');
  const [registerEmail, setRegisterEmail] = useState('');
  const [registerPassword, setRegisterPassword] = useState('');
  const [registerRole, setRegisterRole] = useState('Broker');

  const handleLoginSubmit = async (e) => {
    e.preventDefault();
    try {
      await login(email, password);
    } catch { /* error handled in AuthContext */ }
  };

  const handleRegisterSubmit = async (e) => {
    e.preventDefault();
    try {
      await register(registerName, registerEmail, registerPassword, registerRole);
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
  if (!isLoggedIn && activationToken) {
    return <SetPassword activationToken={activationToken} onPasswordSet={handlePasswordSet} />;
  }

  // 1. GATEWAY SCREEN VIEW: Render security login card layout if unauthenticated
  if (!isLoggedIn) {
    return (
      <div className="min-h-screen w-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-zinc-950 p-6 overflow-hidden">
        
        {/* Luxury Abstract Background Glow Patterns */}
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl pointer-events-none"></div>
        <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-emerald-500/5 rounded-full blur-3xl pointer-events-none"></div>

        {/* Premium Animated Rotating Card Container */}
        <div className="card w-full max-w-md">
          
          {/* Logo and Greeting Header */}
          <div className="text-center mb-8">
            <div className="inline-flex items-center justify-center w-14 h-14 rounded-xl bg-blue-600 shadow-lg shadow-blue-500/30 mb-4">
              <svg className="w-7 h-7 text-white" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5m0 0V11m0 0h4m-4 0h5m-5 10V11m12 4a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-white tracking-tight">{isRegistering ? 'Create Account' : 'Welcome Back'}</h2>
            <p className="text-slate-400 text-sm mt-1">{isRegistering ? 'Register a new team member for the agency workspace' : 'Access your multi-tenant real estate ecosystem'}</p>
          </div>

          <div className="flex rounded-xl border border-slate-700 bg-slate-950/50 p-1 mb-6">
            <button
              type="button"
              onClick={() => setRegistering(false)}
              className={`flex-1 py-2 rounded-lg text-sm font-semibold transition btn-primary`}
            >
              Sign In
            </button>
            <button
              type="button"
              onClick={() => setRegistering(true)}
              className={`flex-1 py-2 rounded-lg text-sm font-semibold transition text-slate-300 hover:text-white hidden`}
            >
              Register
            </button>
          </div>

          {/* eslint-disable no-constant-condition, no-constant-binary-expression */}
          {false && isRegistering ? (
            <form onSubmit={handleRegisterSubmit} className="space-y-5">
              {errorMessage && (
                <div className={`p-3 rounded-xl text-xs font-medium ${errorMessage.includes('successful') ? 'bg-emerald-950/40 border border-emerald-900 text-emerald-400' : 'bg-red-950/40 border border-red-900 text-red-400'} animate-pulse`}>
                  {errorMessage}
                </div>
              )}

              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Full Name</label>
                <input type="text" required value={registerName} onChange={(e) => setRegisterName(e.target.value)} disabled={isConnecting} placeholder="Jane Smith" className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200" />
              </div>

              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Email Address</label>
                <input type="email" required value={registerEmail} onChange={(e) => setRegisterEmail(e.target.value)} disabled={isConnecting} placeholder="agent@agency.com" className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200" />
              </div>

              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Password</label>
                <input type="password" required value={registerPassword} onChange={(e) => setRegisterPassword(e.target.value)} disabled={isConnecting} placeholder="••••••••" className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200" />
              </div>

              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Role</label>
                <select value={registerRole} onChange={(e) => setRegisterRole(e.target.value)} className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200">
                  <option value="Broker">Broker</option>
                  <option value="Owner">Owner</option>
                  <option value="Tenant">Tenant</option>
                </select>
              </div>

               <button type="submit" disabled={isConnecting} className="btn-success">
                {isConnecting ? 'Creating Account...' : 'Create Account'}
               </button>
            </form>
          ) : (
            <form onSubmit={handleLoginSubmit} className="space-y-5">
              {errorMessage && (
                <div className="p-3 bg-red-950/40 border border-red-900 text-red-400 rounded-xl text-xs font-medium animate-pulse">
                  {errorMessage}
                </div>
              )}

              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Email Address</label>
                <input 
                  type="email" required value={email} onChange={(e) => setEmail(e.target.value)} disabled={isConnecting} placeholder="agent@agency.com" 
                  className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200" 
                />
              </div>
              <div>
                <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">Password</label>
                <input 
                  type="password" required value={password} onChange={(e) => setPassword(e.target.value)} disabled={isConnecting} placeholder="••••••••" 
                  className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200" 
                />
              </div>

              <button
                type="submit" disabled={isConnecting}
                className="btn-primary"
              >
                {isConnecting ? "Validating Tokens..." : "Sign In to Dashboard"}
              </button>
            </form>
          )}

          {/* Footer Tenant/Agency Watermark */}
          <div className="mt-8 pt-6 border-t border-slate-800/80 text-center">
            <p className="text-xs text-slate-500">Secure Multi-Tenant Cloud Architecture</p>
          </div>

        </div>
      </div>
     );
  }

  // 2. MAIN SYSTEM DASHBOARD VIEW: Render workspace layers after successful authentication
  return (
    <div className="flex bg-slate-950 min-h-screen text-slate-100">

      {/* 🧭 Collapsible Side Navigation Drawer Shell */}
      <LeftSidebar />

      {/* 📊 Main Dynamic Workspace Content Window Canvas */}
      <div className="flex-1 p-8 overflow-y-auto max-h-screen">
        <header className="flex justify-between items-center mb-6 pb-4 border-b border-slate-800">
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
              {currentView === 'map' && "Property Map"}
              {currentView === 'admin' && "SuperAdmin Control Board"}
              {currentView === 'agency-glance' && "Agency at a Glance"}
              {currentView === 'roles' && "Role & Permission Management"}
            </h2>
            <p className="text-slate-400 text-sm">
              {isAllTenants
                ? 'Multi-tenant spatial intelligence portal — viewing all tenants'
                : `Multi-tenant spatial intelligence portal for ${currentAgency.name}`}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <NotificationsBell />
            <BrochureCartBell />
            <TenantSwitcher />
          </div>
        </header>

        {/* 🎯 TRIAL BANNER — prominently shown at the top for trial accounts */}
        {isTrialUser() && (() => {
          const expiry = getTrialExpiry();
          const isExpired = expiry && new Date(expiry) < new Date();
          const formatted = expiry ? new Date(expiry).toLocaleDateString('en-ZA', { year: 'numeric', month: 'short', day: 'numeric' }) : 'soon';
          return (
            <div className={`mb-6 p-4 rounded-2xl border flex items-center gap-3 ${
              isExpired
                ? 'bg-red-950/40 border-red-800 text-red-200'
                : 'bg-amber-950/40 border-amber-700 text-amber-200'
            }`}>
              <div className={`text-2xl ${isExpired ? 'text-red-400' : 'text-amber-400'}`}>⚠️</div>
              <div className="flex-1">
                <p className="text-sm font-bold uppercase tracking-wider">
                  {isExpired ? 'Trial Expired' : 'Trial Period Active'}
                </p>
                <p className="text-xs opacity-90">
                  {isExpired
                    ? 'Your trial has ended. Please contact support to upgrade.'
                    : `Your 14-day free trial expires on ${formatted}.`}
                </p>
              </div>
            </div>
          );
        })()}

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
          <CRMWorkspace />
        ) : currentView === 'leads' ? (
          <LeadsPage />
        ) : currentView === 'deals' ? (
          <DealsPage />
        ) : null}

        {currentView === 'owners-tenants' && (
          <OwnersTenantsWorkspace />
        )}

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

        {currentView === 'agency-glance' && hasPermission('billing:view') && (
          <AgencyGlance />
        )}
        {currentView === 'admin' && hasPermission('agencies:view') && (
          <SystemAdminWorkspace />
        )}
        {currentView === 'roles' && hasPermission('system:roles:manage') && (
          <RolePermissionsPage />
        )}
      </div>

    </div>
  );
}
