import { usePermission } from '../contexts/PermissionContext.jsx';

export default function Can({ permission, fallback = null, children }) {
  const { hasPermission, loading } = usePermission();

  if (loading) return fallback;
  if (!hasPermission(permission)) return fallback;
  return children;
}
