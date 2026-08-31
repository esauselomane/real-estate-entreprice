import { useState } from 'react';
import { Home, Search, Users, Briefcase, BarChart3, ChevronLeft, ChevronRight, LogOut, Shield, Map } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import { useAgency } from '../contexts/AgencyContext.jsx';

export default function LeftSidebar() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const { userRole, activeProfile, logout } = useAuth();
  const { currentView, navigateTo } = useAppUi();
  const { currentAgency } = useAgency();


  // Base structural links mapped directly to permissions
  const navItems = [
    { name: 'Dashboard', view: 'dashboard', icon: Home, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner', 'Tenant'] },
    { name: 'Property Search', view: 'search', icon: Search, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner'] },
    { name: 'Property Map', view: 'map', icon: Map, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner'] },
    { name: 'Leads & CRM', view: 'crm', icon: Briefcase, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker'] },
    { name: 'Clients (Tenants/Owners)', view: 'owners-tenants', icon: Users, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner'] },
    { name: 'Performance Reports', view: 'reports', icon: BarChart3, roles: ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner'] },
    { name: 'System Admin', view: 'admin', icon: Shield, roles: ['PlatformAdmin', 'AgencyAdmin'] },
  ];

  // Filters visible screens depending on the user's role criteria
  const visibleItems = navItems.filter(item => item.roles.includes(userRole));

  return (
    <div 
      className={`min-h-screen bg-slate-900 border-r border-slate-800 text-slate-300 flex flex-col justify-between relative transition-all duration-300 ease-in-out select-none ${
        isCollapsed ? 'w-20' : 'w-64'
      }`}
    >
      {/* Collapse/Expand Toggle Action Button */}
      <button 
        onClick={() => setIsCollapsed(!isCollapsed)}
        className="absolute -right-3 top-6 bg-blue-600 hover:bg-blue-500 text-white rounded-full p-1 border border-slate-900 cursor-pointer shadow-lg z-50 transition-colors"
      >
        {isCollapsed ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
      </button>

      <div>
        {/* 🏢 Agency Profile Header Area */}
        <div className="p-4 border-b border-slate-800 flex items-center gap-3 overflow-hidden h-16">
          <div className="min-w-8 h-8 rounded-lg bg-gradient-to-tr from-blue-600 to-indigo-600 flex items-center justify-center font-bold text-white shadow-md text-sm">
            {currentAgency.logoInitials}
          </div>
          {!isCollapsed && (
            <div className="flex flex-col animate-fade-in">
              <span className="text-white font-semibold text-sm truncate">{currentAgency.name}</span>
              <span className="text-xs text-slate-500 uppercase tracking-wider font-bold">Workspace</span>
            </div>
          )}
        </div>

        {/* 🔗 Dynamic Functional Navigation Menu Links */}
        <nav className="p-3 space-y-1">
          {visibleItems.map((item, index) => {
            const IconComponent = item.icon;
            return (
              <button
                key={index}
                type="button"
                onClick={() => navigateTo(item.view)}
                aria-current={currentView === item.view ? 'page' : undefined}
                className={`w-full flex items-center gap-4 px-3 py-3 rounded-xl transition-all duration-200 group cursor-pointer text-left ${currentView === item.view ? 'bg-blue-600/20 text-white border border-blue-900/70' : 'hover:bg-slate-800/60 hover:text-white border border-transparent'}`}
              >
                <IconComponent size={20} className={`transition-colors ${currentView === item.view ? 'text-blue-400' : 'text-slate-400 group-hover:text-blue-400'}`} />
                {!isCollapsed && (
                  <span className="text-sm font-medium tracking-wide whitespace-nowrap">{item.name}</span>
                )}
              </button>
            );
          })}
        </nav>
      </div>

      {/* 👤 Account Profile Session Footer Section */}
      <div className="p-3 border-t border-slate-800 bg-slate-950/40">
        <div className="flex items-center gap-3 p-1 overflow-hidden">
          {/* Professional Round Profile Image Layout Container */}
          <div className="w-10 h-10 rounded-full border-2 border-slate-700 bg-blue-600 flex items-center justify-center text-sm font-bold text-white min-w-10">
            {(activeProfile?.name || 'U').split(/\s+/).map((part) => part[0]).join('').slice(0, 2).toUpperCase()}
          </div>
          {!isCollapsed && (
            <div className="flex flex-col min-w-0 flex-1">
              <span className="text-sm font-semibold text-white truncate">{activeProfile?.name || 'User session'}</span>
              <span className="text-xs text-slate-500 truncate">{userRole} Privilege</span>
            </div>
          )}
        </div>

        {/* Minimal logout row button container */}
        <button onClick={logout} className="w-full mt-3 flex items-center gap-4 px-3 py-2 rounded-lg hover:bg-red-950/20 hover:text-red-400 text-slate-400 transition-colors cursor-pointer text-left text-xs font-medium">
          <LogOut size={16} />
          {!isCollapsed && <span>Sign Out</span>}
        </button>
      </div>
    </div>
  );
}
