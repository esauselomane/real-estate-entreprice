import { createContext, useContext, useState, useEffect, useCallback, useMemo } from 'react';
import { crmService } from '../services/crmService.js';
import { useAuth } from './AuthContext.jsx';

const TenantContext = createContext(null);

const STORAGE_KEY = 'active_tenant_override';

function readPersistedOverride() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function persistOverride(value) {
  try {
    if (value == null) localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  } catch {
    // localStorage unavailable — silently ignore
  }
}

export function TenantProvider({ children }) {
  const { activeProfile, userRole } = useAuth();
  const isSuperAdmin = userRole === 'PlatformAdmin';
  const isAgencyAdmin = userRole === 'AgencyAdmin';
  const canSwitch = isSuperAdmin || isAgencyAdmin;

  const [agencies, setAgencies] = useState([]);
  const [agenciesLoading, setAgenciesLoading] = useState(false);
  const [agenciesError, setAgenciesError] = useState('');
  const [overrideTenantId, setOverrideState] = useState(readPersistedOverride);

  // 🔄 Always load the visible agency list for the dropdown (cached for 60s).
  useEffect(() => {
    if (!canSwitch || !activeProfile?.tenantId) return undefined;
    let cancelled = false;
    setAgenciesLoading(true);
    setAgenciesError('');
    crmService
      .getAgencies()
      .then((rows) => {
        if (cancelled) return;
        setAgencies(Array.isArray(rows) ? rows : []);
      })
      .catch((err) => {
        if (cancelled) return;
        setAgenciesError(err?.message || 'Unable to resolve tenant roster.');
      })
      .finally(() => {
        if (!cancelled) setAgenciesLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [canSwitch, activeProfile]);

  // 🎯 Compute the tenant the caller is actually viewing right now.
  // SuperAdmin: override is allowed across all agencies; default = all (no filter).
  // AgencyAdmin: override is limited to the agencies returned by getAgencies()
  //   (which is already filtered server-side to the subtree); default = own tenant.
  const visibleAgencies = useMemo(() => {
    if (!canSwitch) return [];
    if (isSuperAdmin) return agencies;
    // AgencyAdmin: agencies list is already subtree-filtered by the backend
    return agencies;
  }, [agencies, canSwitch, isSuperAdmin]);

  const defaultTenantId = useMemo(() => {
    if (isSuperAdmin) return '__all__';
    return activeProfile?.tenantId || null;
  }, [isSuperAdmin, activeProfile]);

  const activeTenantId = useMemo(() => {
    if (!canSwitch) return activeProfile?.tenantId || null;
    if (!overrideTenantId) return defaultTenantId;
    // For SuperAdmin: '__all__' means "show every tenant".
    if (overrideTenantId === '__all__') return '__all__';
    // Make sure the override is still within the visible set; if not, fall back.
    const exists = visibleAgencies.some((a) => a.agencyId === overrideTenantId);
    if (!exists) return defaultTenantId;
    return overrideTenantId;
  }, [canSwitch, overrideTenantId, defaultTenantId, visibleAgencies]);

  const setActiveTenantId = useCallback(
    (tenantId) => {
      if (!canSwitch) return;
      if (tenantId == null || tenantId === defaultTenantId) {
        setOverrideState(null);
        persistOverride(null);
        return;
      }
      setOverrideState(tenantId);
      persistOverride(tenantId);
    },
    [canSwitch, defaultTenantId]
  );

  const clearActiveTenant = useCallback(() => {
    setOverrideState(null);
    persistOverride(null);
  }, []);

  const activeAgency = useMemo(() => {
    if (activeTenantId === '__all__') {
      return { agencyId: '__all__', name: 'All Tenants', tradingName: 'All Tenants' };
    }
    return visibleAgencies.find((a) => a.agencyId === activeTenantId) || null;
  }, [activeTenantId, visibleAgencies]);

  return (
    <TenantContext.Provider
      value={{
        canSwitch,
        agencies: visibleAgencies,
        agenciesLoading,
        agenciesError,
        activeTenantId,
        activeAgency,
        defaultTenantId,
        setActiveTenantId,
        clearActiveTenant,
        isAllTenants: activeTenantId === '__all__',
      }}
    >
      {children}
    </TenantContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export const useTenant = () => {
  const context = useContext(TenantContext);
  if (!context) {
    throw new Error('useTenant must be used within a TenantProvider');
  }
  return context;
};

export default TenantContext;
