import React, { useEffect, useState } from 'react';
import { Building2, Users, Briefcase, Home, Bell, ShieldCheck } from 'lucide-react';
import { crmService } from '../services/ crmService.js';
import { propertyService } from '../services/ propertyService.js';

const roleLabels = { PlatformAdmin: 'Platform Administrator', AgencyAdmin: 'Agency Administrator', Broker: 'Broker', Owner: 'Property Owner', Tenant: 'Tenant' };

export default function RoleDashboard({ profile, onNavigate }) {
  const role = profile?.role || 'Broker';
  const [metrics, setMetrics] = useState([]);
  useEffect(() => {
    const load = async () => {
      try {
        if (role === 'PlatformAdmin') {
          const agencies = await crmService.getAgencies();
          setMetrics([{ label: 'Agencies', value: agencies.length, icon: Building2 }, { label: 'Platform controls', value: 'Active', icon: ShieldCheck }]);
        } else if (role === 'AgencyAdmin') {
          const [brokers, boards] = await Promise.all([crmService.getBrokers(), crmService.getCRMBoards()]);
          setMetrics([{ label: 'Brokers', value: brokers.length, icon: Users }, { label: 'Agency deals', value: boards.verifiedDeals?.length || 0, icon: Briefcase }]);
        } else if (role === 'Broker') {
          const [boards, properties] = await Promise.all([crmService.getCRMBoards(), propertyService.searchProperties([], 'All', null, null)]);
          setMetrics([{ label: 'My deals', value: boards.verifiedDeals?.length || 0, icon: Briefcase }, { label: 'Available properties', value: properties.length, icon: Home }]);
        } else if (role === 'Owner') {
          setMetrics([{ label: 'Owner workspace', value: 'Manage tenants', icon: Users }, { label: 'Property operations', value: 'Available', icon: Home }]);
        } else {
          setMetrics([{ label: 'Lease access', value: 'Active', icon: Home }, { label: 'Requests', value: 'Use Operations', icon: Bell }]);
        }
      } catch { setMetrics([]); }
    };
    load();
  }, [role]);
  const next = role === 'Tenant' ? 'operations' : role === 'Owner' ? 'owners-tenants' : role === 'Broker' ? 'crm' : role === 'AgencyAdmin' ? 'admin' : 'admin';
  return <div className="space-y-6"><div><p className="text-[10px] uppercase tracking-[.2em] text-slate-500">{roleLabels[role] || role}</p><h3 className="text-2xl font-bold text-white">Welcome, {profile?.name || 'User'}</h3><p className="text-sm text-slate-400 mt-1">Your workspace only surfaces information appropriate to your role.</p></div><div className="grid gap-4 md:grid-cols-2">{metrics.map(({ label, value, icon: Icon }) => <div key={label} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex items-center justify-between"><div><p className="text-xs uppercase tracking-wider text-slate-500">{label}</p><p className="mt-2 text-2xl font-black text-white">{value}</p></div><Icon className="text-blue-400" /></div>)}</div><button type="button" onClick={() => onNavigate(next)} className="px-4 py-2.5 bg-blue-600 rounded-xl text-xs font-bold text-white">Open my workspace</button></div>;
}
