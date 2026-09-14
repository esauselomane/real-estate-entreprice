import { useState, useRef, useEffect } from 'react';
import { Home, Building, TrendingUp, User, PieChart, Key, ChevronDown, ChevronRight, LayoutDashboard, Activity, Search, Map, UserPlus, Briefcase, Users, UserCheck, BarChart3, Shield, Settings, Building2, ChevronLeft, LogOut } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import { useAgency } from '../contexts/AgencyContext.jsx';
import { usePermission } from '../contexts/PermissionContext.jsx';
import UserSelfAdminPopout from './UserSelfAdminPopout.jsx';
import AgencyGlance from './AgencyGlance.jsx';

export default function LeftSidebar() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [popoutOpen, setPopoutOpen] = useState(false);
  const [expandedCategories, setExpandedCategories] = useState(new Set(['workspace']));
  const profilePicRef = useRef(null);
  const { userRole, activeProfile, logout } = useAuth();
  const { currentView, navigateTo } = useAppUi();
  const { currentAgency } = useAgency();
  const { hasPermission } = usePermission();

  const toggleCategory = (id) => {
    setExpandedCategories(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  // Hierarchical navigation: 6 parent categories with expandable submenus
  const navCategories = [
    {
      id: 'workspace',
      label: 'Workspace',
      icon: Home,
      items: [
        { name: 'Dashboard', view: 'dashboard', icon: LayoutDashboard, permission: 'properties:view' },
        { name: 'Broker Dashboard', view: 'broker-dashboard', icon: Activity, permission: 'deals:view' },
      ],
    },
    {
      id: 'properties',
      label: 'Properties',
      icon: Building,
      items: [
        { name: 'Property Search', view: 'search', icon: Search, permission: 'properties:view' },
        { name: 'Property Map', view: 'map', icon: Map, permission: 'properties:view' },
      ],
    },
    {
      id: 'business',
      label: 'Business',
      icon: TrendingUp,
      items: [
        { name: 'Leads', view: 'leads', icon: UserPlus, permission: 'leads:view' },
        { name: 'Deals', view: 'deals', icon: Briefcase, permission: 'deals:view' },
      ],
    },
    {
      id: 'people',
      label: 'People',
      icon: User,
      items: [
        { name: 'CRM Directory', view: 'crm', icon: Users, permission: 'users:view' },
        { name: 'Clients (Tenants/Owners)', view: 'owners-tenants', icon: UserCheck, permission: 'users:view' },
      ],
    },
    {
      id: 'reports',
      label: 'Reports',
      icon: PieChart,
      items: [
        { name: 'Performance Reports', view: 'reports', icon: BarChart3, permission: 'billing:view' },
        { name: 'Agency at a Glance', view: 'agency-glance', icon: Building2, permission: 'billing:view' },
      ],
    },
    {
      id: 'administration',
      label: 'Administration',
      icon: Key,
      items: [
        { name: 'System Admin', view: 'admin', icon: Shield, permission: 'agencies:view' },
        { name: 'Roles & Permissions', view: 'roles', icon: Settings, permission: 'system:roles:manage' },
      ],
    },
  ];

  // Auto-expand the category containing the current view
  useEffect(() => {
    const cat = navCategories.find(c => c.items.some(item => item.view === currentView));
    if (cat && !expandedCategories.has(cat.id)) {
      setExpandedCategories(prev => new Set([...prev, cat.id]));
    }
  }, [currentView]);

  const isCategoryActive = (catId) => {
    const cat = navCategories.find(c => c.id === catId);
    return cat ? cat.items.some(item => item.view === currentView) : false;
  };

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

        {/* 🔗 Hierarchical Navigation */}
        <nav className="p-3 space-y-1">
          {navCategories.map(category => {
            const CategoryIcon = category.icon;
            const isExpanded = expandedCategories.has(category.id);
            const isActive = isCategoryActive(category.id);
            const categoryItems = category.items.filter(item => hasPermission(item.permission));

            return (
              <div key={category.id}>
                <button
                  type="button"
                  onClick={() => {
                    if (isCollapsed) {
                      navigateTo(categoryItems[0]?.view || 'dashboard');
                    } else {
                      toggleCategory(category.id);
                    }
                  }}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all duration-200 group cursor-pointer text-left ${
                    isActive
                      ? 'bg-blue-600/20 text-white border border-blue-900/70'
                      : 'hover:bg-slate-800/60 hover:text-white border border-transparent'
                  }`}
                >
                  <CategoryIcon size={20} className={`transition-colors ${isActive ? 'text-blue-400' : 'text-slate-400 group-hover:text-blue-400'}`} />
                  {!isCollapsed && (
                    <>
                      <span className="text-sm font-semibold tracking-wide flex-1 whitespace-nowrap">{category.label}</span>
                      {isExpanded
                        ? <ChevronDown size={14} className="text-slate-500" />
                        : <ChevronRight size={14} className="text-slate-500" />
                      }
                    </>
                  )}
                </button>
                {!isCollapsed && isExpanded && (
                  <div className="ml-7 mt-1 space-y-1 border-l border-slate-800 pl-2">
                    {categoryItems.map((item, idx) => {
                      const ItemIcon = item.icon;
                      return (
                        <button
                          key={idx}
                          type="button"
                          onClick={() => navigateTo(item.view)}
                          aria-current={currentView === item.view ? 'page' : undefined}
                          className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all duration-200 group cursor-pointer text-left ${currentView === item.view ? 'bg-blue-600/20 text-white border border-blue-900/70' : 'hover:bg-slate-800/60 hover:text-white border border-transparent'}`}
                        >
                          <ItemIcon size={16} className={`transition-colors ${currentView === item.view ? 'text-blue-400' : 'text-slate-500 group-hover:text-blue-400'}`} />
                          <span className="text-xs font-medium tracking-wide whitespace-nowrap">{item.name}</span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </nav>
      </div>

          {/* 👤 Account Profile Session Footer Section */}
      <div className="p-3 border-t border-slate-800 bg-slate-950/40">
        <div
          ref={profilePicRef}
          className="flex items-center gap-3 p-1 overflow-hidden cursor-pointer"
          onClick={() => setPopoutOpen(true)}
        >
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

      <UserSelfAdminPopout
        isOpen={popoutOpen}
        onClose={() => setPopoutOpen(false)}
        anchorRect={profilePicRef.current?.getBoundingClientRect() || null}
      />
    </div>
  );
}
