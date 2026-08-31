import { createContext, useContext, useState, useEffect } from 'react';
import { crmService } from '../services/crmService.js';
import { useAuth } from './AuthContext.jsx';

const AgencyContext = createContext(null);

export function AgencyProvider({ children }) {
  const { activeProfile } = useAuth();
  const [currentAgency, setCurrentAgency] = useState({
    name: 'Resolving Workspace...',
    logoInitials: '..',
  });

  useEffect(() => {
    if (!activeProfile?.tenantId) return;
    crmService
      .getAgencies()
      .then((agencies) => {
        const agency =
          (agencies || []).find((item) => item.agencyId === activeProfile.tenantId) ||
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
          name: activeProfile.tenantId,
          logoInitials: activeProfile.tenantId.slice(-3),
        })
      );
  }, [activeProfile]);

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
