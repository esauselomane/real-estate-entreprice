import React, { useState } from 'react';
import { 
  Home, Search, Users, Briefcase, BarChart3, 
  Settings, ChevronLeft, ChevronRight, LogOut, Shield 
} from 'lucide-react';

export default function LeftSidebar({ userRole, currentAgency }) {
  const [isCollapsed, setIsCollapsed] = useState(false);

  // Mock profile details to replicate a professional live system look
  const userProfile = {
    name: "Sarah Jenkins",
    email: "sarah@eliteproperties.com",
    avatar: "https://unsplash.com"
  };

  // Base structural links mapped directly to permissions
  const navItems = [
    { name: 'Dashboard', icon: Home, roles: ['Agent', 'Manager', 'SuperAdmin'] },
    { name: 'Property Search', icon: Search, roles: ['Agent', 'Manager', 'SuperAdmin'] },
    { name: 'Leads & CRM', icon: Briefcase, roles: ['Agent', 'Manager', 'SuperAdmin'] },
    { name: 'Clients (Tenants/Owners)', icon: Users, roles: ['Agent', 'Manager', 'SuperAdmin'] },
    { name: 'Performance Reports', icon: BarChart3, roles: ['Agent', 'Manager', 'SuperAdmin'] },
    { name: 'System Admin', icon: Shield, roles: ['Manager', 'SuperAdmin'] },
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
                className="w-full flex items-center gap-4 px-3 py-3 rounded-xl hover:bg-slate-800/60 hover:text-white transition-all duration-200 group cursor-pointer text-left"
              >
                <IconComponent size={20} className="text-slate-400 group-hover:text-blue-400 transition-colors" />
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
          <img 
            src={userProfile.avatar} 
            alt="Profile Avatar Target" 
            className="w-10 h-10 rounded-full border-2 border-slate-700 object-cover shadow-sm bg-slate-800 min-w-10"
          />
          {!isCollapsed && (
            <div className="flex flex-col min-w-0 flex-1">
              <span className="text-sm font-semibold text-white truncate">{userProfile.name}</span>
              <span className="text-xs text-slate-500 truncate">{userRole} Privilege</span>
            </div>
          )}
        </div>

        {/* Minimal logout row button container */}
        <button className="w-full mt-3 flex items-center gap-4 px-3 py-2 rounded-lg hover:bg-red-950/20 hover:text-red-400 text-slate-400 transition-colors cursor-pointer text-left text-xs font-medium">
          <LogOut size={16} />
          {!isCollapsed && <span>Sign Out</span>}
        </button>
      </div>
    </div>
  );
}
