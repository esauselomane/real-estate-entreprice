import { createContext, useContext, useState, useEffect } from 'react';
import { crmService } from '../services/crmService.js';
import { useAuth } from './AuthContext.jsx';
import { useTenant } from './TenantContext.jsx';

const AgencyContext = createContext(null);

export function AgencyProvider({ children }) {
  const { activeProfile } = useAuth();
  const { activeTenantId, activeAgency, isAllTenants } = useTenant();
  const [currentAgency, setCurrentAgency] = useState({
    name: 'Resolving Workspace...',
    logoInitials: '..',
  });

  // 🌐 The sidebar/header agency name now follows the top-right TenantSwitcher
  // override instead of the logged-in user's tenantId. The logged-in tenantId
  // remains untouched in the JWT and in `user_profile_data` — only the
  // displayed workspace and downstream data scope changes.
  useEffect(() => {
    // "All Tenants" mode for SuperAdmin — show a neutral label.
    if (isAllTenants) {
      setCurrentAgency({ name: 'All Tenants', logoInitials: 'ALL' });
      return;
    }
    if (activeAgency && (activeAgency.name || activeAgency.tradingName)) {
      const name = activeAgency.tradingName || activeAgency.name;
      setCurrentAgency({
        name,
        logoInitials: name
          .split(/\s+/)
          .map((word) => word[0])
          .join('')
          .slice(0, 3)
          .toUpperCase(),
      });
      return;
    }
    if (!activeTenantId) return;
    crmService
      .getAgencies()
      .then((agencies) => {
        const agency =
          (agencies || []).find((item) => item.agencyId === activeTenantId) ||
          agencies?.[0];
        if (agency) {
          setCurrentAgency({
            name: agency.name,
            logoInitials: agency.name
              .split(/\s+/)
              .map((word) => word[0])
              .join('')
              .slice(0, 3)
              .toUpperCase(),
          });
        }
      })
      .catch(() =>
        setCurrentAgency({
          name: activeTenantId,
          logoInitials: activeTenantId.slice(-3),
        })
      );
  }, [activeProfile, activeTenantId, activeAgency, isAllTenants]);

  return <AgencyContext.Provider value={{ currentAgency }}>{children}</AgencyContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAgency = () => {
  const context = useContext(AgencyContext);
  if (!context) {
    throw new Error('useAgency must be used within an AgencyProvider');
  }
  return context;
};

export default AgencyContext;
