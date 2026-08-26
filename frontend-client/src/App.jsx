import React, { useState, useEffect } from 'react';
import LeftSidebar from './components/ LeftSidebar.jsx';
import PropertySearch from './components/ PropertySearch.jsx';
import PropertyDetails from './components/ PropertyDetails.jsx';
import ClientDetails from './components/ClientDetails.jsx';
import CRMWorkspace from './components/CRMWorkspace.jsx';
import PropertyMapWorkspace from './components/ PropertyMapWorkspace.jsx';
import OwnersTenantsWorkspace from './components/OwnersTenantsWorkspace.jsx';
import ReportsWorkspace from './components/ReportsWorkspace.jsx';
import SystemAdminWorkspace from './components/SystemAdminWorkspace.jsx';
import BrochureBuilderWorkspace from './components/ BrochureBuilderWorkspace.jsx';

// 🔌 Import our clean encapsulated API microservices client wrappers
import { authService } from './services/authService.js';

export default function App() {
  // Authentication session parameters
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [userRole, setUserRole] = useState('Agent'); 
  const [activeProfile, setActiveProfile] = useState(null);
  const [isRegistering, setIsRegistering] = useState(false);
  
  // Login input states
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [registerName, setRegisterName] = useState('');
  const [registerEmail, setRegisterEmail] = useState('');
  const [registerPassword, setRegisterPassword] = useState('');
  const [registerRole, setRegisterRole] = useState('Agent');
  const [errorMessage, setErrorMessage] = useState('');
  const [isConnecting, setIsConnecting] = useState(false);

  // 🧭 System central sub-view workspace routing state parameters
  const [currentView, setCurrentView] = useState('search'); 
  const [selectedPropertyId, setSelectedPropertyId] = useState(null);
  const [selectedClientData, setSelectedClientData] = useState({ id: null, type: '', name: '' });
  const [selectedWorkspace, setSelectedWorkspace] = useState('properties');

  // Multi-tenant profile configurations synchronized from response envelopes
  const [currentAgency, setCurrentAgency] = useState({
    name: "Resolving Workspace...",
    logoInitials: ".."
  });

  const [activeBrochureDealTarget, setActiveBrochureDealTarget] = useState(null);


  // 🔄 LIFECYCLE RECOVERY: Restores user login session records upon browser tab reloads
  useEffect(() => {
    const rawSavedProfile = localStorage.getItem('user_profile_data');
    const storedToken = localStorage.getItem('user_session_jwt');
    
    if (rawSavedProfile && storedToken) {
      const parsedProfile = JSON.parse(rawSavedProfile);
      setActiveProfile(parsedProfile);
      setUserRole(parsedProfile.role);
      setCurrentAgency({
        name: parsedProfile.tenantId === 'TEN-001' ? 'Apex Luxury Realty' : 'Coastal Living Properties',
        logoInitials: parsedProfile.tenantId === 'TEN-001' ? 'ALR' : 'CLP'
      });
      setIsLoggedIn(true);
    }
  }, []);

  const handleSelectProperty = (propertyId) => {
    setSelectedPropertyId(propertyId);
    setCurrentView('details');
  };

  const handleNavigateToClient = (clientOrId, type, name) => {
    if (clientOrId && typeof clientOrId === 'object') {
      setSelectedClientData({
        id: clientOrId.id || '',
        type: clientOrId.type || 'Client',
        name: clientOrId.name || 'Client Profile'
      });
    } else {
      setSelectedClientData({
        id: clientOrId || '',
        type: type || 'Client',
        name: name || 'Client Profile'
      });
    }
    setCurrentView('client');
  };

  const userRoleProfile = userRole;

  const handleLoginSubmit = async (e) => {
    e.preventDefault();
    setErrorMessage('');
    setIsConnecting(true);

    try {
      const loginPayload = await authService.login(email, password);
      setActiveProfile(loginPayload.profile);
      setUserRole(loginPayload.profile.role);
      
      setCurrentAgency({
        name: loginPayload.profile.tenantId === 'TEN-001' ? 'Apex Luxury Realty' : 'Coastal Living Properties',
        logoInitials: loginPayload.profile.tenantId === 'TEN-001' ? 'ALR' : 'CLP'
      });
      
      setIsLoggedIn(true);
    } catch (err) {
      setErrorMessage(err || 'Connection timed out. Ensure your Ocelot gateway service container is active.');
    } finally {
      setIsConnecting(false);
    }
  };

  const handleRegisterSubmit = async (e) => {
    e.preventDefault();
    setErrorMessage('');
    setIsConnecting(true);

    try {
      await authService.registerUser({
        fullName: registerName,
        email: registerEmail,
        passwordHash: registerPassword,
        systemRole: registerRole,
        tenantId: 'TEN-001'
      });

      setIsRegistering(false);
      setEmail(registerEmail);
      setPassword(registerPassword);
      setErrorMessage('Registration successful. You can now sign in.');
      setRegisterName('');
      setRegisterEmail('');
      setRegisterPassword('');
      setRegisterRole('Agent');
    } catch (err) {
      setErrorMessage(err || 'Registration failed. Please try again.');
    } finally {
      setIsConnecting(false);
    }
  };
  // 1. GATEWAY SCREEN VIEW: Render security login card layout if unauthenticated
  if (!isLoggedIn) {
    return (
      <div className="min-h-screen w-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-zinc-950 p-6 overflow-hidden">
        
        {/* Luxury Abstract Background Glow Patterns */}
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl pointer-events-none"></div>
        <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-emerald-500/5 rounded-full blur-3xl pointer-events-none"></div>

        {/* Premium Animated Rotating Card Container */}
        <div className="w-full max-w-md bg-slate-900/60 backdrop-blur-xl border border-slate-700/50 p-8 rounded-2xl shadow-2xl shadow-black/50 animate-spin-twice">
          
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
              onClick={() => setIsRegistering(false)}
              className={`flex-1 py-2 rounded-lg text-sm font-semibold transition ${!isRegistering ? 'bg-blue-600 text-white' : 'text-slate-300'}`}
            >
              Sign In
            </button>
            <button
              type="button"
              onClick={() => setIsRegistering(true)}
              className={`flex-1 py-2 rounded-lg text-sm font-semibold transition ${isRegistering ? 'bg-blue-600 text-white' : 'text-slate-300'}`}
            >
              Register
            </button>
          </div>

          {isRegistering ? (
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
                  <option value="Agent">Agent</option>
                  <option value="Manager">Manager</option>
                  <option value="SuperAdmin">SuperAdmin</option>
                </select>
              </div>

              <button type="submit" disabled={isConnecting} className="w-full py-3 mt-2 bg-emerald-600 hover:bg-emerald-500 active:bg-emerald-700 disabled:bg-emerald-800 disabled:text-slate-400 text-white font-semibold rounded-xl shadow-lg shadow-emerald-600/20 hover:shadow-emerald-500/30 transition-all duration-200 cursor-pointer">
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
                className="w-full py-3 mt-2 bg-blue-600 hover:bg-blue-500 active:bg-blue-700 disabled:bg-blue-800 disabled:text-slate-400 text-white font-semibold rounded-xl shadow-lg shadow-blue-600/20 hover:shadow-blue-500/30 transition-all duration-200 cursor-pointer"
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
      <LeftSidebar userRole={userRole} currentAgency={currentAgency} />

      {/* 📊 Main Dynamic Workspace Content Window Canvas */}
      <div className="flex-1 p-8 overflow-y-auto max-h-screen">
        <header className="flex justify-between items-center mb-6 pb-4 border-b border-slate-800">
          <div>
            <h2 className="text-2xl font-bold text-white tracking-tight">
              {currentView === 'search' && "Property Asset Registry"}
              {currentView === 'details' && "Asset Portfolio Insights"}
              {currentView === 'client' && "Legal Entity Dossier"}
              {currentView === 'crm' && "Corporate CRM Pipeline"}
              {currentView === 'owners-tenants' && "Owner & Tenant Directory"}
              {currentView === 'reports' && "Performance Reports"}
              {currentView === 'map' && "Property Map"}
              {currentView === 'admin' && "SuperAdmin Control Board"}
            </h2>
            <p className="text-slate-400 text-sm">Multi-tenant spatial intelligence portal for {currentAgency.name}</p>
          </div>
          
          {/* Quick Action Navigation Testing Hub & Active Module Selector Bar */}
          <div className="flex items-center gap-3">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-1.5 flex gap-1 shadow-md flex-wrap">
              <button 
                onClick={() => setCurrentView('search')}
                type="button"
                className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'search' || currentView === 'details' || currentView === 'client' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                Properties
              </button>
              <button 
                onClick={() => setCurrentView('map')}
                type="button"
                className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'map' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                Property Map
              </button>
              <button 
                onClick={() => setCurrentView('crm')}
                type="button"
                className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'crm' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                Leads & CRM
              </button>
              <button 
                onClick={() => setCurrentView('owners-tenants')}
                type="button"
                className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'owners-tenants' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                Owners/Tenants
              </button>
              <button 
                onClick={() => setCurrentView('reports')}
                type="button"
                className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'reports' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-slate-200'}`}
              >
                Reports
              </button>
              {userRole === 'SuperAdmin' && (
                <button 
                  onClick={() => setCurrentView('admin')}
                  type="button"
                  className={`text-xs font-semibold px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${currentView === 'admin' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-white'}`}
                >
                  System Admin
                </button>
              )}
            </div>

            {/* Profile Action Indicator with Dynamic Sign Out Handle Trigger */}
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-2 flex items-center gap-3 shadow-md">
              <div className="flex flex-col text-right">
                <span className="text-xs text-white font-bold">{activeProfile?.name || "Agent Session"}</span>
                <span className="text-[10px] text-slate-500 font-medium uppercase tracking-wider">{userRole} Scope</span>
              </div>
              <button
                onClick={() => authService.logout()}
                type="button"
                className="text-xs bg-slate-950 border border-slate-700 hover:bg-red-950/20 hover:text-red-400 font-semibold px-2.5 py-1.5 rounded-lg cursor-pointer transition-colors"
              >
                Sign Out
              </button>
            </div>
          </div>
        </header>

        {/* ⚙️ Dynamic UI view routing execution conditional dispatcher matrices */}
        {currentView === 'search' && (
          <PropertySearch onSelectProperty={handleSelectProperty} />
        )}

        {currentView === 'crm' && activeBrochureDealTarget ? (
          <BrochureBuilderWorkspace 
            dealData={activeBrochureDealTarget} 
            onBack={() => setActiveBrochureDealTarget(null)} 
          />
        ) : currentView === 'crm' ? (
          <CRMWorkspace 
            currentUserRole={userRoleProfile}
            onOpenBrochureBuilder={(deal) => setActiveBrochureDealTarget(deal)} 
          />
        ) : null}

        {currentView === 'owners-tenants' && (
          <OwnersTenantsWorkspace />
        )}

        {currentView === 'reports' && (
          <ReportsWorkspace />
        )}

        {currentView === 'map' && (
          <PropertyMapWorkspace />
        )}

        {currentView === 'details' && (
          <PropertyDetails 
            propertyId={selectedPropertyId} 
            onBack={() => setCurrentView('search')} 
            onNavigateToClient={handleNavigateToClient}
          />
        )}

        {currentView === 'client' && (
          <ClientDetails 
            clientId={selectedClientData.id}
            clientType={selectedClientData.type}
            clientName={selectedClientData.name}
            onBack={() => setCurrentView('details')}
          />
        )}

        {currentView === 'admin' && userRole === 'SuperAdmin' && (
          <SystemAdminWorkspace />
        )}
      </div>

    </div>
  );
}
