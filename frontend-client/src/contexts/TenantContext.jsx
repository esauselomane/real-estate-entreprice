import { createContext, useContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { crmService } from '../services/crmService.js';
import { useAuth } from './AuthContext.jsx';

const TenantContext = createContext(null);

// 🌐 View-scope tenant override. The value here is **only** used as a request
// filter (?tenantId=...) for read endpoints and as the stamp for new records.
// It is NEVER written back to the JWT or to user_profile_data — the
// logged-in user's session tenantId remains the authoritative identity.
//
// The dropdown is keyed by AgencyId (unique), but every backend data table
// is partitioned by TenantId — so the override payload carries BOTH so the
// services can send the correct filter value to whichever microservice they
// are calling.
const STORAGE_KEY = 'active_tenant_override';
const STORAGE_MARKER = 'view-override';

function readPersistedOverride() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    // Reject anything that doesn't carry our marker, to make sure we never
    // accidentally pick up a value written by something else into the same key.
    if (!parsed || parsed.__source !== STORAGE_MARKER) return null;
    return parsed;
  } catch {
    return null;
  }
}

function persistOverride(value) {
  try {
    if (value == null) {
      localStorage.removeItem(STORAGE_KEY);
    } else {
      const tagged = { __source: STORAGE_MARKER, ...value };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(tagged));
    }
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
  // 🛰️ Persisted override: { agencyId, tenantId, name } | { __all__: true } | null
  const [override, setOverrideState] = useState(readPersistedOverride);
  // 🌳 Per-agency subtree expansion cache. Keyed by agencyId; the value is
  // the array of TenantIds the property service should see when that
  // agency is selected (the agency itself, plus all descendants when it
  // is a top-level parent, or just itself when it has a parent). The
  // property service doesn't know about the agency hierarchy, so the CRM
  // expands the subtree and the frontend hands the resulting TenantId
  // list to the property endpoint as a comma-separated query value.
  const [subtreeCache, setSubtreeCache] = useState({});
  // 📌 Latest-cache ref so the lazy-resolution callback in setActiveTenantId
  // can read the current cache without re-creating the callback (and thus
  // re-running the pre-warm effect) every time the cache updates.
  const subtreeCacheRef = useRef({});
  useEffect(() => { subtreeCacheRef.current = subtreeCache; }, [subtreeCache]);

  // 🔄 Always load the FULL visible agency roster for the dropdown (no
  // pagination — the switcher needs every agency the caller can pick so
  // the client-side search works). The new /agencies/roster endpoint
  // honours the same `?tenantId=` override as everything else, so we load
  // with no override first (full visibility) and cache the result.
  useEffect(() => {
    if (!canSwitch || !activeProfile?.tenantId) return undefined;
    let cancelled = false;
    setAgenciesLoading(true);
    setAgenciesError('');
    crmService
      .getAgenciesRoster()
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

  const visibleAgencies = useMemo(() => agencies, [agencies]);

  // The agencyId the user has currently selected. "__all__" means no narrowing.
  // Null means "use the default" (own tenant for AgencyAdmin; all for PlatformAdmin).
  const activeAgencyId = useMemo(() => {
    if (!canSwitch) return null;
    if (!override) {
      return isSuperAdmin ? '__all__' : null;
    }
    if (override.__all__) return '__all__';
    return override.agencyId || null;
  }, [canSwitch, override, isSuperAdmin]);

  // The TenantId services should send as the request filter. This is the
  // actual partition key every backend data table uses; the agencyId is
  // a display/identity key only.
  const activeTenantId = useMemo(() => {
    if (!canSwitch) return activeProfile?.tenantId || null;
    if (activeAgencyId === '__all__') return '__all__';
    if (activeAgencyId) {
      const found = visibleAgencies.find((a) => a.agencyId === activeAgencyId);
      if (found && found.tenantId) return found.tenantId;
    }
    return activeProfile?.tenantId || null;
  }, [canSwitch, activeAgencyId, visibleAgencies, activeProfile]);

  const activeAgency = useMemo(() => {
    if (activeAgencyId === '__all__') {
      return { agencyId: '__all__', name: 'All Tenants', tradingName: 'All Tenants' };
    }
    return visibleAgencies.find((a) => a.agencyId === activeAgencyId) || null;
  }, [activeAgencyId, visibleAgencies]);

  const setActiveTenantId = useCallback(
    (agencyId) => {
      if (!canSwitch) return;
      if (agencyId == null || agencyId === '__all__') {
        setOverrideState({ __all__: true });
        persistOverride({ __all__: true });
        return;
      }
      const found = visibleAgencies.find((a) => a.agencyId === agencyId);
      if (!found) return;
      const payload = {
        agencyId: found.agencyId,
        tenantId: found.tenantId,
        name: found.tradingName || found.name || found.agencyId,
      };
      setOverrideState(payload);
      persistOverride(payload);
      // 🌱 Lazy subtree resolution for child agencies: the pre-warm only
      // resolves top-level parents (cheap, O(top-level) calls). When the
      // user picks a child, resolve its subtree on-demand — for a child
      // agency the server returns just the one row, so it's a single
      // quick call.
      if (found.parentAgencyId && !subtreeCacheRef.current[found.agencyId]) {
        crmService
          .getAgenciesRoster(found.agencyId)
          .then((rows) => {
            const tenantIds = Array.isArray(rows)
              ? Array.from(new Set(rows.map((r) => r.tenantId).filter(Boolean)))
              : [found.tenantId].filter(Boolean);
            setSubtreeCache((prev) => ({ ...prev, [found.agencyId]: tenantIds }));
          })
          .catch(() => {
            setSubtreeCache((prev) => ({ ...prev, [found.agencyId]: [found.tenantId].filter(Boolean) }));
          });
      }
    },
    [canSwitch, visibleAgencies]
  );

  const clearActiveTenant = useCallback(() => {
    setOverrideState(null);
    persistOverride(null);
  }, []);

  const defaultTenantId = useMemo(() => {
    if (isSuperAdmin) return '__all__';
    return activeProfile?.tenantId || null;
  }, [isSuperAdmin, activeProfile]);

  const isAllTenants = activeAgencyId === '__all__';

  // 🌳 Resolve the full subtree (TenantId list) for the active agency. The
  // server applies the parent-vs-child rule:
  //   • top-level parent → itself + every descendant TenantId
  //   • child agency    → its own TenantId only
  // Cached per agencyId so the property service can be sent a
  // comma-separated ?tenantId= list on every dashboard fetch.
  const activePropertyTenantIds = useMemo(() => {
    if (isAllTenants) return null; // null = "no filter" (Property service unfiltered for PlatformAdmin)
    if (!activeAgencyId) return null;
    const cached = subtreeCache[activeAgencyId];
    if (Array.isArray(cached)) return cached;
    return null;
  }, [isAllTenants, activeAgencyId, subtreeCache]);

  // 🛰️ True when a tenant override is active but the subtree cache for
  // that agency hasn't been resolved yet. Consumers (PropertyMapWorkspace,
  // dashboard cards) use this to render a "Loading new tenant scope…"
  // indicator so the brief flicker between the user picking a tenant
  // and the cache becoming available doesn't look like the page is stuck.
  const isResolvingSubtree = useMemo(() => {
    if (isAllTenants) return false;
    if (!activeAgencyId) return false;
    return !Array.isArray(subtreeCache[activeAgencyId]);
  }, [isAllTenants, activeAgencyId, subtreeCache]);

  // Pre-warm the subtree cache for the agencies the user is actually
  // likely to pick — TOP-LEVEL parents only. With 1000+ seeded agencies
  // (most of them leaves), pre-warming every single one fired 1000 BFS
  // round-trips on the CRM service every render, drowning the network
  // panel. Child agencies' subtrees are just [their own tenantId] so
  // there's no value in resolving them through the server.
  useEffect(() => {
    if (!canSwitch || !agencies || agencies.length === 0) return undefined;
    let cancelled = false;
    const missing = agencies
      .filter((a) => !a.parentAgencyId)
      .map((a) => a.agencyId)
      .filter((id) => id && !subtreeCache[id]);
    if (missing.length === 0) return undefined;
    (async () => {
      const updates = {};
      for (const agencyId of missing) {
        try {
          // CRM's getAgenciesRoster with ?tenantId=AG-XXXX returns the
          // subtree (top-level) or single tenant (child).
          const subtree = await crmService.getAgenciesRoster(agencyId);
          const tenantIds = Array.isArray(subtree)
            ? Array.from(new Set(subtree.map((row) => row.tenantId).filter(Boolean)))
            : [];
          updates[agencyId] = tenantIds;
        } catch {
          // Failed resolution = empty subtree, fall back to the agency's own tenantId.
          const own = agencies.find((a) => a.agencyId === agencyId);
          updates[agencyId] = own?.tenantId ? [own.tenantId] : [];
        }
      }
      if (cancelled) return;
      setSubtreeCache((prev) => ({ ...prev, ...updates }));
    })();
    return () => {
      cancelled = true;
    };
    // We intentionally don't depend on subtreeCache to avoid an infinite
    // loop — the cache is updated via setSubtreeCache.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canSwitch, agencies]);

  // 💾 Mirror the subtree cache into localStorage so the property service
  // (which lives outside React and reads localStorage directly) can
  // resolve the comma-separated ?tenantId= list for the active agency.
  useEffect(() => {
    try {
      localStorage.setItem('property_subtree_cache', JSON.stringify(subtreeCache));
    } catch { /* ignore quota / unavailable storage */ }
  }, [subtreeCache]);

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
        isAllTenants,
        activePropertyTenantIds,
        isResolvingSubtree,
      }}
    >
      {children}
    </TenantContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export const useTenant = () => {
  const context = useContext(TenantContext);
  if (context) return context;
  // 🛟 Defensive fallback: if a consumer mounts above the provider, return a
  // safe no-op context so the rest of the app still renders. The original
  // throw was masking provider-ordering bugs as full-screen errors.
  return {
    canSwitch: false,
    agencies: [],
    agenciesLoading: false,
    agenciesError: '',
    activeTenantId: null,
    activeAgency: null,
    defaultTenantId: null,
    setActiveTenantId: () => {},
    clearActiveTenant: () => {},
    isAllTenants: false,
    activePropertyTenantIds: null,
    isResolvingSubtree: false,
  };
};

export default TenantContext;
