import { useContext, createContext, useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext.jsx';
import { systemRolesService } from '../services/systemRolesService.js';

export const PermissionContext = createContext({
  permissions: [],
  rolePermissions: {},
  loading: true,
  hasPermission: () => false,
  refreshPermissions: async () => {}
});

export function PermissionProvider({ children }) {
  const { activeProfile, isLoggedIn } = useAuth();
  const [permissions, setPermissions] = useState([]);
  const [rolePermissions, setRolePermissions] = useState({});
  const [loading, setLoading] = useState(true);

  const fetchPermissions = async () => {
    if (!isLoggedIn || !activeProfile) {
      setPermissions([]);
      setRolePermissions({});
      setLoading(false);
      return;
    }

    try {
      const roles = await systemRolesService.getSystemRoles();
      const allPermissions = new Set();
      const roleMap = {};

      for (const role of roles) {
        const rolePerms = role.permissions || [];
        roleMap[role.name] = rolePerms;
        for (const perm of rolePerms) {
          allPermissions.add(perm.key);
        }
      }

      setPermissions(Array.from(allPermissions));
      setRolePermissions(roleMap);
    } catch (err) {
      console.error('Failed to fetch permissions:', err);
      setPermissions([]);
      setRolePermissions({});
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPermissions();
  }, [isLoggedIn, activeProfile?.role]);

  const hasPermission = (permissionKey) => {
    if (!activeProfile) return false;
    const role = activeProfile.role;
    if (role === 'PlatformAdmin') return true;
    const rolePerms = rolePermissions[role] || [];
    return rolePerms.some(p => p.key === permissionKey || p === permissionKey);
  };

  return (
    <PermissionContext.Provider value={{ permissions, rolePermissions, loading, hasPermission, refreshPermissions: fetchPermissions }}>
      {children}
    </PermissionContext.Provider>
  );
}

export function usePermission() {
  const context = useContext(PermissionContext);
  if (!context) {
    throw new Error('usePermission must be used within a PermissionProvider');
  }
  return context;
}
