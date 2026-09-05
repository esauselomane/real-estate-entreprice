import { useState, useRef, useEffect } from 'react';
import { ChevronDown, Building2, Globe, Loader2, AlertCircle, X } from 'lucide-react';
import { useTenant } from '../contexts/TenantContext.jsx';

// 🌐 TOP-RIGHT TENANT SWITCHER
// PlatformAdmin → may scope reports to any single tenant or pick "All Tenants"
// AgencyAdmin  → may scope reports to any agency in their visible subtree
// Broker/Owner/Tenant → not rendered (useTenant.canSwitch is false)
export default function TenantSwitcher() {
  const {
    canSwitch,
    agencies,
    agenciesLoading,
    agenciesError,
    activeTenantId,
    activeAgency,
    isAllTenants,
    setActiveTenantId,
    clearActiveTenant,
  } = useTenant();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const containerRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    const handleClick = (e) => {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  if (!canSwitch) return null;

  const filtered = (agencies || []).filter((a) => {
    if (!search.trim()) return true;
    const term = search.toLowerCase();
    return (
      (a.name || '').toLowerCase().includes(term) ||
      (a.tradingName || '').toLowerCase().includes(term) ||
      (a.agencyId || '').toLowerCase().includes(term)
    );
  });

  const buttonLabel = isAllTenants
    ? 'All Tenants'
    : activeAgency
      ? (activeAgency.tradingName || activeAgency.name)
      : 'Select tenant';
  const subtitle = isAllTenants
    ? 'Showing every agency'
    : activeAgency
      ? `${activeAgency.agencyId}${activeAgency.isTrial ? ' • TRIAL' : ''}`
      : 'No tenant selected';

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        className={`flex items-center gap-2 pl-3 pr-2.5 py-2 rounded-xl border transition-colors ${
          isAllTenants
            ? 'border-blue-900 bg-blue-950/40 text-blue-200 hover:bg-blue-950/60'
            : 'border-slate-700 bg-slate-900 text-slate-100 hover:border-blue-700 hover:bg-slate-800'
        }`}
        title="Switch active tenant for reports and admin views"
      >
        {isAllTenants ? <Globe size={14} className="text-blue-300" /> : <Building2 size={14} className="text-blue-300" />}
        <div className="flex flex-col items-start leading-tight min-w-[140px] max-w-[220px]">
          <span className="text-xs font-bold truncate w-full text-left">{buttonLabel}</span>
          <span className="text-[10px] text-slate-400 truncate w-full text-left">{subtitle}</span>
        </div>
        <ChevronDown size={14} className={`text-slate-400 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 z-50 bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl shadow-black/50 overflow-hidden">
          <div className="p-3 border-b border-slate-800 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Active Tenant</span>
              {activeTenantId && (
                <button
                  type="button"
                  onClick={() => { clearActiveTenant(); setOpen(false); }}
                  className="text-[10px] text-slate-400 hover:text-white flex items-center gap-1"
                >
                  <X size={10} /> Reset
                </button>
              )}
            </div>
            <input
              autoFocus
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search agencies…"
              className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white placeholder-slate-500"
            />
          </div>

          <div className="max-h-72 overflow-y-auto">
            <button
              type="button"
              onClick={() => { setActiveTenantId('__all__'); setOpen(false); }}
              className={`w-full text-left px-3 py-2 flex items-center gap-2 text-xs hover:bg-slate-800 ${
                isAllTenants ? 'bg-blue-950/40 text-blue-200' : 'text-slate-200'
              }`}
            >
              <Globe size={12} className="text-blue-300" />
              <div className="flex-1">
                <div className="font-semibold">All Tenants</div>
                <div className="text-[10px] text-slate-500">Aggregate across every visible agency</div>
              </div>
            </button>

            <div className="border-t border-slate-800 my-1" />

            {agenciesLoading && (
              <div className="px-3 py-3 text-[11px] text-slate-400 flex items-center gap-2">
                <Loader2 size={12} className="animate-spin" /> Loading tenant roster…
              </div>
            )}

            {agenciesError && !agenciesLoading && (
              <div className="px-3 py-3 text-[11px] text-red-300 flex items-center gap-2">
                <AlertCircle size={12} /> {agenciesError}
              </div>
            )}

            {!agenciesLoading && !agenciesError && filtered.length === 0 && (
              <div className="px-3 py-3 text-[11px] text-slate-500">
                {search ? 'No matches.' : 'No agencies available in your visible subtree.'}
              </div>
            )}

            {filtered.map((agency) => {
              const isActive = activeTenantId === agency.agencyId;
              return (
                <button
                  key={agency.agencyId}
                  type="button"
                  onClick={() => { setActiveTenantId(agency.agencyId); setOpen(false); }}
                  className={`w-full text-left px-3 py-2 flex items-center gap-2 text-xs hover:bg-slate-800 ${
                    isActive ? 'bg-blue-950/40 text-blue-200' : 'text-slate-200'
                  }`}
                >
                  <Building2 size={12} className="text-slate-400" />
                  <div className="flex-1 min-w-0">
                    <div className="font-semibold truncate">{agency.tradingName || agency.name}</div>
                    <div className="text-[10px] text-slate-500 truncate">
                      {agency.agencyId}
                      {agency.isTrial && <span className="ml-1 text-amber-300">TRIAL</span>}
                      {agency.parentAgencyId && <span className="ml-1 text-indigo-300">↳ #{agency.parentAgencyId}</span>}
                    </div>
                  </div>
                  {isActive && <span className="text-[10px] font-bold text-blue-300">ACTIVE</span>}
                </button>
              );
            })}
          </div>

          <div className="px-3 py-2 border-t border-slate-800 text-[10px] text-slate-500 bg-slate-950/60">
            Reports, leaderboards, and CRM filters scope to the selected tenant.
          </div>
        </div>
      )}
    </div>
  );
}
