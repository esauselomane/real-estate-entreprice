import { useEffect, useState, useMemo } from 'react';
import { Settings, Save, RotateCcw, ShieldCheck, ChevronRight } from 'lucide-react';
import { systemRolesService } from '../services/systemRolesService.js';
import { permissionsService } from '../services/permissionsService.js';
import { useAuth } from '../contexts/AuthContext.jsx';

const MODULE_LABELS = {
  Properties: 'Property Management',
  Units: 'Unit Management',
  Agencies: 'Agency Management',
  Users: 'User Management',
  Billing: 'Billing & Reports',
  Valuations: 'Valuations'
};

const MODULE_ICONS = {
  Properties: '🏠',
  Units: '📦',
  Agencies: '🏢',
  Users: '👥',
  Billing: '💰',
  Valuations: '📊'
};

export default function RolePermissionsPage() {
  const { userRole } = useAuth();
  const isSuperAdmin = userRole === 'PlatformAdmin';
  const [roles, setRoles] = useState([]);
  const [allPermissions, setAllPermissions] = useState([]);
  const [selectedRoleId, setSelectedRoleId] = useState(null);
  const [selectedPermissions, setSelectedPermissions] = useState(new Set());
  const [originalPermissions, setOriginalPermissions] = useState(new Set());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const selectedRole = useMemo(() => roles.find(r => r.roleDefId === selectedRoleId), [roles, selectedRoleId]);

  const permissionsByModule = useMemo(() => {
    const grouped = {};
    for (const perm of allPermissions) {
      const module = perm.module || 'Other';
      if (!grouped[module]) grouped[module] = [];
      grouped[module].push(perm);
    }
    return grouped;
  }, [allPermissions]);

  const moduleOrder = useMemo(() => {
    const order = ['Properties', 'Units', 'Agencies', 'Users', 'Billing', 'Valuations', 'Other'];
    return order.filter(m => permissionsByModule[m]);
  }, [permissionsByModule]);

  const loadData = async () => {
    setLoading(true);
    setError('');
    try {
      const [rolesData, permsData] = await Promise.all([
        systemRolesService.getSystemRoles(),
        permissionsService.getPermissions()
      ]);
      setRoles(rolesData || []);
      setAllPermissions(permsData || []);
      if (rolesData?.length > 0 && !selectedRoleId) {
        setSelectedRoleId(rolesData[0].roleDefId);
      }
    } catch (err) {
      setError(err || 'Failed to load roles and permissions.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadData(); }, []);

  useEffect(() => {
    if (selectedRole) {
      const perms = new Set((selectedRole.permissions || []).map(p => p.permissionId));
      setSelectedPermissions(perms);
      setOriginalPermissions(perms);
    }
  }, [selectedRole]);

  const togglePermission = (permissionId) => {
    setSelectedPermissions(prev => {
      const next = new Set(prev);
      if (next.has(permissionId)) next.delete(permissionId);
      else next.add(permissionId);
      return next;
    });
  };

  const toggleModule = (module, selectAll) => {
    setSelectedPermissions(prev => {
      const next = new Set(prev);
      const modulePerms = permissionsByModule[module] || [];
      for (const perm of modulePerms) {
        if (selectAll) next.add(perm.permissionId);
        else next.delete(perm.permissionId);
      }
      return next;
    });
  };

  const toggleAll = (selectAll) => {
    setSelectedPermissions(selectAll ? new Set(allPermissions.map(p => p.permissionId)) : new Set());
  };

  const isModuleFullySelected = (module) => {
    const modulePerms = permissionsByModule[module] || [];
    return modulePerms.length > 0 && modulePerms.every(p => selectedPermissions.has(p.permissionId));
  };

  const isModulePartiallySelected = (module) => {
    const modulePerms = permissionsByModule[module] || [];
    const count = modulePerms.filter(p => selectedPermissions.has(p.permissionId)).length;
    return count > 0 && count < modulePerms.length;
  };

  const hasChanges = useMemo(() => {
    if (selectedPermissions.size !== originalPermissions.size) return true;
    for (const id of selectedPermissions) {
      if (!originalPermissions.has(id)) return true;
    }
    return false;
  }, [selectedPermissions, originalPermissions]);

  const handleSave = async () => {
    if (!selectedRoleId || !hasChanges) return;
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      const payload = {
        name: selectedRole?.name,
        description: selectedRole?.description,
        isPlatformScoped: selectedRole?.isPlatformScoped,
        isAgencyScoped: selectedRole?.isAgencyScoped,
        isActive: selectedRole?.isActive,
        permissionIds: Array.from(selectedPermissions)
      };
      await systemRolesService.updateSystemRole(selectedRoleId, payload);
      setOriginalPermissions(new Set(selectedPermissions));
      setSuccess('Permissions saved successfully.');
      await loadData();
    } catch (err) {
      setError(err || 'Failed to save permissions.');
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    setSelectedPermissions(new Set(originalPermissions));
    setError('');
    setSuccess('');
  };

  if (!isSuperAdmin) {
    return (
      <div className="flex items-center justify-center h-96 text-slate-400 text-sm">
        <ShieldCheck size={16} className="mr-2" /> Access restricted to Platform Administrators.
      </div>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96 text-slate-400 text-sm">
        Loading roles and permissions...
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {error && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{error}</div>}
      {success && <div className="p-3 rounded-xl border border-emerald-900 bg-emerald-950/40 text-emerald-300 text-xs">{success}</div>}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Role Selection Sidebar */}
        <div className="lg:col-span-1">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
              <Settings size={14} className="text-violet-500" /> System Roles
            </div>
            <div className="space-y-1.5">
              {roles.map(role => (
                <button
                  key={role.roleDefId}
                  type="button"
                  onClick={() => setSelectedRoleId(role.roleDefId)}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded-xl text-left transition-all cursor-pointer ${
                    selectedRoleId === role.roleDefId
                      ? 'bg-violet-600 text-white shadow-lg'
                      : 'bg-slate-950 text-slate-300 hover:text-white hover:bg-slate-800/60 border border-slate-800'
                  }`}
                >
                  <div className="flex flex-col min-w-0">
                    <span className="text-xs font-bold truncate">{role.name}</span>
                    <span className={`text-[10px] truncate ${selectedRoleId === role.roleDefId ? 'text-violet-200' : 'text-slate-500'}`}>
                      {role.isPlatformScoped && 'Platform '}{role.isAgencyScoped && 'Agency '}{!role.isPlatformScoped && !role.isAgencyScoped && 'Scoped'}
                    </span>
                  </div>
                  <ChevronRight size={14} className={selectedRoleId === role.roleDefId ? 'text-white' : 'text-slate-600'} />
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Permission Matrix */}
        <div className="lg:col-span-2">
          {selectedRole ? (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-5">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-white font-bold text-sm">{selectedRole.name} Permissions</h3>
                  <p className="text-[11px] text-slate-400 mt-0.5">{selectedRole.description || 'Configure accessible features for this role.'}</p>
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => toggleAll(true)}
                    className="text-[10px] px-3 py-1.5 rounded-lg bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition-colors cursor-pointer"
                  >
                    Select All
                  </button>
                  <button
                    type="button"
                    onClick={() => toggleAll(false)}
                    className="text-[10px] px-3 py-1.5 rounded-lg bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition-colors cursor-pointer"
                  >
                    Deselect All
                  </button>
                </div>
              </div>

              <div className="grid gap-4">
                {moduleOrder.map(module => {
                  const modulePerms = permissionsByModule[module] || [];
                  const fullySelected = isModuleFullySelected(module);
                  const partiallySelected = isModulePartiallySelected(module);
                  return (
                    <div key={module} className="bg-slate-950 border border-slate-800 rounded-xl p-4">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                          <span className="text-sm">{MODULE_ICONS[module] || '📁'}</span>
                          <h4 className="text-white font-bold text-xs uppercase tracking-wider">{MODULE_LABELS[module] || module}</h4>
                        </div>
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => toggleModule(module, true)}
                            className="text-[10px] px-2 py-1 rounded-md bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition-colors cursor-pointer"
                          >
                            Select All
                          </button>
                          <button
                            type="button"
                            onClick={() => toggleModule(module, false)}
                            className="text-[10px] px-2 py-1 rounded-md bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition-colors cursor-pointer"
                          >
                            Deselect All
                          </button>
                        </div>
                      </div>
                      <div className="grid grid-cols-2 md:grid-cols-3 gap-2.5">
                        {modulePerms.map(perm => {
                          const checked = selectedPermissions.has(perm.permissionId);
                          return (
                            <label
                              key={perm.permissionId}
                              className={`flex items-center gap-2 px-3 py-2 rounded-lg border cursor-pointer transition-all ${
                                checked
                                  ? 'bg-violet-950/40 border-violet-900 text-white'
                                  : 'bg-slate-900 border-slate-800 text-slate-400 hover:border-slate-700'
                              }`}
                            >
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => togglePermission(perm.permissionId)}
                                className="rounded border-slate-600 bg-slate-800 text-violet-600 focus:ring-violet-500 focus:ring-offset-slate-900"
                              />
                              <span className="text-[11px] truncate">{perm.description || perm.key}</span>
                            </label>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="flex items-center justify-between pt-4 border-t border-slate-800">
                <div className="text-[11px] text-slate-400">
                  {selectedPermissions.size} of {allPermissions.length} permissions selected
                  {hasChanges && <span className="text-amber-400 ml-2">(unsaved changes)</span>}
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={handleReset}
                    disabled={!hasChanges || saving}
                    className="flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-bold bg-slate-800 text-slate-300 hover:text-white border border-slate-700 transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <RotateCcw size={12} /> Reset
                  </button>
                  <button
                    type="button"
                    onClick={handleSave}
                    disabled={!hasChanges || saving}
                    className="flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-bold bg-violet-600 text-white hover:bg-violet-500 shadow-lg transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {saving ? (
                      <>Saving...</>
                    ) : (
                      <>
                        <Save size={12} /> Save Changes
                      </>
                    )}
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-8 text-center text-slate-400 text-sm">
              Select a role from the sidebar to configure its permissions.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
