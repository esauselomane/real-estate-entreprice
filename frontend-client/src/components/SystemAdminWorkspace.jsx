import BrokerSyndicationFields from './BrokerSyndicationFields.jsx';
import { useEffect, useState, useRef } from 'react';
import { Building2, UserPlus, Users, Pencil, Trash2, ShieldCheck, Tag, Package, ChevronRight, X, Settings, CircleX, Mail, RefreshCw, AlertCircle, BarChart3, TrendingUp, Banknote, Trophy, Upload } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line, Legend } from 'recharts';
import { crmService } from '../services/crmService.js';
import { systemRolesService } from '../services/systemRolesService.js';
import { permissionsService } from '../services/permissionsService.js';
import { authService } from '../services/authService.js';
import { leadCaptureService } from '../services/leadCaptureService.js';
import apiClient from '../services/apiClient.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { usePermission } from '../contexts/PermissionContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
import Can from '../components/Can.jsx';
import Pagination from '../components/Pagination.jsx';
import SubscriptionPackagesAdmin from './subscriptions/SubscriptionPackagesAdmin.jsx';
import AmenityTypesWorkspace from './AmenityTypesWorkspace.jsx';

export default function SystemAdminWorkspace({ defaultTab }) {
  const { userRole: currentUserRole, activeProfile } = useAuth();
  const isSuperAdmin = currentUserRole === 'PlatformAdmin';
  const canManageSubscriptionPackages = isSuperAdmin;
  const { hasPermission } = usePermission();
  const { activeTenantId, activeAgency, isAllTenants } = useTenant();
  const canManageAgencies = isSuperAdmin || currentUserRole === 'AgencyAdmin';
  const [activeTab, setActiveTab] = useState(defaultTab || (isSuperAdmin ? 'agencies' : 'users'));

  const [agencies, setAgencies] = useState([]);
  const [agencySearch, setAgencySearch] = useState('');
  const filteredAgencies = agencies.filter(agency => [agency.name, agency.tradingName, agency.agencyId, agency.tenantId, agency.contactEmail].some(value => String(value || '').toLowerCase().includes(agencySearch.trim().toLowerCase())));
  const [systemUsers, setSystemUsers] = useState([]);
  const [usersPage, setUsersPage] = useState(1);
  const usersPageSize = 20;
  const [usersSearch, setUsersSearch] = useState('');
  const [usersTotal, setUsersTotal] = useState(0);
  const [agencyContacts, setAgencyContacts] = useState({});
  const [roles, setRoles] = useState([]);
  const [allPermissions, setAllPermissions] = useState([]);
  const [roleForm, setRoleForm] = useState({ name: '', description: '', isPlatformScoped: false, isAgencyScoped: false, permissionIds: [], isActive: true });
  const [editingRole, setEditingRole] = useState(null);
  const [agencyForm, setAgencyForm] = useState({
    name: '', tradingName: '', registrationNumber: '', companyLogoUrl: '',
    contactEmail: '', headquartersAddress: '',
    adminFullName: '', adminEmail: '', adminPassword: '',
    parentAgencyId: '', isTrial: false, trialStartDate: '', trialEndDate: '',
    brokerageLicense: ''
  });
  const [userForm, setUserForm] = useState({ fullName: '', email: '', password: '', systemRole: 'Broker', licenseNumber: '', monthlyTargetCommission: '' });
  const [editingUser, setEditingUser] = useState(null);
  const [newUserTenantId, setNewUserTenantId] = useState('');
  const [contactForm, setContactForm] = useState({ fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '', contactType: 'Staff', isAgencyAdmin: false });
  const [error, setError] = useState('');
  const [editingAgency, setEditingAgency] = useState(null);
  const [editingContact, setEditingContact] = useState(null);
  const [expandedAgency, setExpandedAgency] = useState(null);

  // 📋 CAPTURED LEADS (from LeadCaptureController — distinct from CRM leads)
  const [leads, setLeads] = useState([]);
  const [leadsTotal, setLeadsTotal] = useState(0);
  const [leadsPage, setLeadsPage] = useState(1);
  const [leadsHasMore, setLeadsHasMore] = useState(true);
  const [leadsLoading, setLeadsLoading] = useState(false);
  const [leadsError, setLeadsError] = useState('');
  const [editingLead, setEditingLead] = useState(null);
  const [leadForm, setLeadForm] = useState({
    fullName: '', email: '', phone: '', company: '',
    userType: 'Individual', package: '', demoRequested: false, message: '', source: 'website'
  });
  const leadsSentinelRef = useRef(null);

  // 📥 IMPORT DATA
  const [importFile, setImportFile] = useState(null);
  const [importJobId, setImportJobId] = useState(null);
  const [importStatus, setImportStatus] = useState(null);
  const [importPolling, setImportPolling] = useState(false);

  const loadAgencies = async () => {
    try {
      const rows = await crmService.getAgenciesRoster();
      setAgencies(rows || []);
    } catch (err) { setError((err && err.message) || 'Unable to load agency records.'); }
  };

  const loadBrokers = async () => {
    try { return await crmService.getBrokers() || []; }
    catch { return []; }
  };

  const loadContacts = async (agencyId) => {
    try {
      const contacts = await crmService.getAgencyContacts(agencyId);
      setAgencyContacts((prev) => ({ ...prev, [agencyId]: contacts || [] }));
    } catch {
      setAgencyContacts((prev) => ({ ...prev, [agencyId]: [] }));
    }
  };

  const loadRoles = async () => {
    try { setRoles(await systemRolesService.getSystemRoles() || []); }
    catch (err) { setError((err && err.message) || 'Unable to load system roles.'); }
  };

  const loadPermissions = async () => {
    try { setAllPermissions(await permissionsService.getPermissions() || []); }
    catch (err) { console.error('Unable to load permissions:', err); }
  };

  const loadUsers = async (page = usersPage, search = usersSearch) => {
    try {
      const res = await authService.getUsers({ page, pageSize: usersPageSize, search });
      setSystemUsers((res && res.items) || []);
      setUsersTotal(res?.totalCount || 0);
      setUsersPage(res?.page || page);
    } catch (err) { setError((err && err.message) || 'Unable to load user directory.'); }
  };

  const loadData = async () => { await Promise.all([loadAgencies(), loadBrokers(), loadUsers(1, ''), isSuperAdmin && loadRoles(), isSuperAdmin && loadPermissions()]); };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { loadData(); }, [activeTenantId]);

  // Roles an admin may assign: PlatformAdmin gets the full palette; AgencyAdmin only agency-scoped roles.
  const assignableRoles = isSuperAdmin ? ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner', 'Tenant'] : ['AgencyAdmin', 'Broker', 'Owner', 'Tenant'];

  const handleRoleSubmit = async (e) => {
    e.preventDefault();
    if (!isSuperAdmin) return;
    try {
      const payload = { ...roleForm };
      if (editingRole) await systemRolesService.updateSystemRole(editingRole.roleDefId, payload);
      else await systemRolesService.createSystemRole(payload);
      setRoleForm({ name: '', description: '', isPlatformScoped: false, isAgencyScoped: false, permissionIds: [], isActive: true });
      setEditingRole(null);
      await loadRoles();
    } catch (err) { alert(err || 'Role save failed.'); }
  };

  const resetRole = (role) => {
    setEditingRole(role);
    setRoleForm({
      name: role.name || '',
      description: role.description || '',
      isPlatformScoped: role.isPlatformScoped || false,
      isAgencyScoped: role.isAgencyScoped || false,
      permissionIds: (role.permissions || []).map(p => p.permissionId),
      isActive: role.isActive
    });
  };

  const handleRoleDelete = async (role) => {
    if (!isSuperAdmin) return;
    if (!window.confirm(`Deactivate role "${role.name}"? This is non-destructive.`)) return;
    try {
      await systemRolesService.deleteSystemRole(role.roleDefId);
      await loadRoles();
    } catch (err) { alert(err || 'Role deactivation failed.'); }
  };

  const handleAgencySubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingAgency) {
        await crmService.updateAgency(editingAgency.agencyId, { ...editingAgency, ...agencyForm });
      } else {
        const created = await crmService.createAgency(agencyForm);
        // 🚀 On agency creation, automatically provision an AgencyAdmin user bound to the
        // same tenant, so the new agency has an accountable administrator login.
        if (agencyForm.adminEmail) {
          try {
             await authService.registerUser({
              fullName: agencyForm.adminFullName || agencyForm.contactEmail,
              email: agencyForm.adminEmail,
              systemRole: 'AgencyAdmin',
              tenantId: created?.tenantId || created?.TenantId,
              passwordHash: agencyForm.adminPassword || 'ChangeMeNow!',
              licenseNumber: agencyForm.brokerageLicense || ''
            });
          } catch {
            alert(`Agency created, but the linked admin user could not be provisioned: registration failed`);
          }
        }
      }
      setAgencyForm({ name: '', tradingName: '', registrationNumber: '', companyLogoUrl: '', contactEmail: '', headquartersAddress: '', adminFullName: '', adminEmail: '', adminPassword: '', parentAgencyId: '', isTrial: false, trialStartDate: '', trialEndDate: '', brokerageLicense: '' });
      setEditingAgency(null);
      await loadAgencies();
    } catch (err) { alert(err || 'Agency creation failed.'); }
  };

  const handleContactSubmit = async (e) => {
    e.preventDefault();
    const targetAgency = editingContact ? editingContact.agencyId : expandedAgency;
    try {
       if (editingContact) await crmService.updateContact({ ...editingContact, ...contactForm, contactId: editingContact.contactId, agencyId: targetAgency });
      else await crmService.createContactPerson(targetAgency, contactForm);
      setContactForm({ fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '', contactType: 'Staff', isAgencyAdmin: false });
      setEditingContact(null);
      await loadContacts(targetAgency);
    } catch (err) { alert(err || 'Contact person save failed.'); }
  };

  const resetAgency = (agency) => {
    setEditingAgency(agency);
    setAgencyForm({
      name: agency.name || '', tradingName: agency.tradingName || '', registrationNumber: agency.registrationNumber || '',
      companyLogoUrl: agency.companyLogoUrl || '', contactEmail: agency.contactEmail || '', headquartersAddress: agency.headquartersAddress || '',
      adminFullName: '', adminEmail: '', adminPassword: '',
      parentAgencyId: agency.parentAgencyId || '',
      isTrial: !!agency.isTrial,
      trialStartDate: agency.trialStartDate || '',
      trialEndDate: agency.trialEndDate || '',
      brokerageLicense: agency.brokerageLicense || ''
    });
  };

  const handleLogoUpload = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => setAgencyForm((prev) => ({ ...prev, companyLogoUrl: reader.result }));
    reader.readAsDataURL(file);
  };

  // 📋 — Captured leads data loading + infinite scroll

  const LEAD_PAGE_SIZE = 20;

  const loadLeads = async (page = 1, reset = true) => {
    if (reset) {
      setLeads([]);
      setLeadsPage(1);
      setLeadsHasMore(true);
    }
    setLeadsLoading(true);
    setLeadsError('');
    try {
      const result = await leadCaptureService.getLeads(page, 'New');
      if (reset) {
        setLeads(result.data);
      } else {
        setLeads((prev) => [...prev, ...result.data]);
      }
      setLeadsTotal(result.totalCount);
      setLeadsHasMore(result.data.length === LEAD_PAGE_SIZE && (result.data.length + (reset ? 0 : (page - 1) * LEAD_PAGE_SIZE)) < result.totalCount);
    } catch (err) {
      setLeadsError((err && err.message) || 'Unable to load captured leads.');
    } finally {
      setLeadsLoading(false);
    }
  };

  const resetLead = (lead) => {
    setEditingLead(lead);
    setLeadForm({
      fullName: lead.fullName || '',
      email: lead.email || '',
      phone: lead.phone || '',
      company: lead.company || '',
      userType: lead.userType || 'Individual',
      package: lead.package || '',
      demoRequested: lead.demoRequested || false,
      message: lead.message || '',
      source: lead.source || 'website',
    });
  };

  const handleLeadSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingLead) {
        await leadCaptureService.updateLead(editingLead.id, leadForm);
      } else {
        await leadCaptureService.createLead(leadForm);
      }
      setLeadForm({ fullName: '', email: '', phone: '', company: '', userType: 'Individual', package: '', demoRequested: false, message: '', source: 'website' });
      setEditingLead(null);
      loadLeads(1, true);
    } catch (err) {
      alert(err || 'Lead save failed.');
    }
  };

  const handleLeadStatusChange = async (leadId, status) => {
    try {
      await leadCaptureService.updateLeadStatus(leadId, status);
      setLeads((prev) => prev.filter((l) => l.id !== leadId));
    } catch (err) {
      alert(err || 'Lead status update failed.');
    }
  };

  const LEAD_STATUSES = ['Contacted', 'QuoteSent', 'Paid', 'AgencyCreated'];

   // eslint-disable-next-line react-hooks/exhaustive-deps
   useEffect(() => {
    if (activeTab !== 'captured-leads' || !isSuperAdmin) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLeads(1, true);
  }, [activeTab, isSuperAdmin]);

  // 📥 Poll import job status
  useEffect(() => {
    if (!importJobId || !importPolling) return;
    const interval = setInterval(async () => {
       try {
         const res = await apiClient.get(`/properties/import/jobs/${importJobId}`);
         const data = res.data;
         setImportStatus(data);
        const pct = data.totalRows > 0 ? Math.round((data.processedRows / data.totalRows) * 100) : 0;
        const bar = document.getElementById('import-progress');
        const txt = document.getElementById('import-status-text');
        if (bar) bar.style.width = pct + '%';
        if (txt) txt.textContent = `${data.status} — ${data.processedRows}/${data.totalRows} rows (${data.successCount} success, ${data.errorCount} errors)`;
        if (data.status === 'completed' || data.status === 'failed' || data.status === 'completed_with_errors') {
          clearInterval(interval);
          setImportPolling(false);
        }
      } catch { /* non-fatal */ }
    }, 2000);
    return () => clearInterval(interval);
  }, [importJobId, importPolling]);

  useEffect(() => {
    const el = leadsSentinelRef.current;
    if (!el || activeTab !== 'captured-leads') return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && leadsHasMore && !leadsLoading) {
          const nextPage = leadsPage + 1;
          setLeadsPage(nextPage);
          loadLeads(nextPage, false);
        }
      },
      { threshold: 1.0 }
    );
    observer.observe(el);
    return () => observer.disconnect();
   }, [activeTab, leadsHasMore, leadsLoading, leadsPage]);

  return (
    <div className="space-y-6 animate-fade-in">
      {error && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{error}</div>}

      {/* 🎛️ DRILL-NAVIGATION MENU */}
      <div className="flex gap-2 bg-slate-900 border border-slate-800 p-1.5 rounded-2xl shadow-xl overflow-x-auto">
        {canManageSubscriptionPackages && (
          <button
            onClick={() => setActiveTab('subscription-packages')}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
              activeTab === 'subscription-packages' ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            <Package size={12} className="inline mr-1.5" /> Subscription packages
          </button>
        )}
        {canManageAgencies && (
        <button
          onClick={() => setActiveTab('agencies')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'agencies' ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
          <Building2 size={12} className="inline mr-1.5" /> Agency Admin
        </button>
        )}
        <button
          onClick={() => setActiveTab('users')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'users' ? 'bg-violet-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
          <Users size={12} className="inline mr-1.5" /> User Admin
        </button>
        {isSuperAdmin && <Can permission="amenities:view">
          <button
            onClick={() => setActiveTab('amenities')}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
              activeTab === 'amenities' ? 'bg-emerald-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            <Tag size={12} className="inline mr-1.5" /> Amenity Types
          </button>
        </Can>}
          {isSuperAdmin && <Can permission="leads:view">
            <button
              onClick={() => setActiveTab('captured-leads')}
              type="button"
              className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
                activeTab === 'captured-leads' ? 'bg-emerald-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
              }`}
            >
              <Mail size={12} className="inline mr-1.5" /> Captured Leads
            </button>
          </Can>}
          {isSuperAdmin && (
            <Can permission="agencies:create">
              <button
                onClick={() => setActiveTab('import')}
                type="button"
                className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
                  activeTab === 'import' ? 'bg-orange-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
                }`}
              >
                <Upload size={12} className="inline mr-1.5" /> Import Data
              </button>
            </Can>
          )}
      </div>

      {activeTab === 'subscription-packages' && canManageSubscriptionPackages && <SubscriptionPackagesAdmin />}

      {activeTenantId && (
        <div className="text-[11px] text-slate-300 bg-slate-900/60 border border-slate-800 rounded-xl px-3 py-2 flex items-center gap-2">
          <Building2 size={12} className="text-blue-400" />
          <span>
            Tenant scope: <strong className="text-white">{isAllTenants ? 'All Tenants' : (activeAgency?.tradingName || activeAgency?.name || activeTenantId)}</strong>
            {activeAgency?.agencyId && !isAllTenants && <span className="text-slate-500 ml-1">#{activeAgency.agencyId}</span>}
            {activeAgency?.isTrial && !isAllTenants && <span className="ml-2 text-amber-300 text-[10px]">TRIAL</span>}
          </span>
          {isAllTenants && <span className="text-[10px] text-slate-500">— reports aggregate across every visible agency</span>}
        </div>
      )}

      {activeTab === 'agencies' && (
        <>
          <div className="grid gap-6 xl:grid-cols-2">
            {/* 🏢 AGENCY FORM — visible to PlatformAdmin only. AgencyAdmin cannot add agencies. */}
            {canManageAgencies ? (
              <form onSubmit={handleAgencySubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
                  <Building2 size={14} className="text-blue-500" /> {editingAgency ? 'Edit Agency' : 'Add Agency'}
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <input value={agencyForm.name} onChange={(e) => setAgencyForm({ ...agencyForm, name: e.target.value })} placeholder="Agency name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                  <input value={agencyForm.tradingName} onChange={(e) => setAgencyForm({ ...agencyForm, tradingName: e.target.value })} placeholder="Trading name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={agencyForm.registrationNumber} onChange={(e) => setAgencyForm({ ...agencyForm, registrationNumber: e.target.value })} placeholder="Registration number" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

                  {/* 🪪 Brokerage License — required for agency operation */}
                  <input value={agencyForm.brokerageLicense} onChange={(e) => setAgencyForm({ ...agencyForm, brokerageLicense: e.target.value })} placeholder="Brokerage license #" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />

                  {/* 🖼️ LOGO: either paste a URL or upload a file (stored as a data-URI on the agency record) */}
                  <div className="flex items-center gap-1">
                    <label className="text-[10px] text-slate-400">Logo</label>
                    <label className={`text-[10px] px-2 py-1 rounded-md border ${agencyForm.companyLogoUrl ? 'bg-slate-950 text-blue-400 border-blue-900' : 'bg-slate-950 text-slate-500 border-slate-700 cursor-pointer hover:text-white'}`}>
                      {agencyForm.companyLogoUrl ? <span>Change</span> : <span>Upload</span>}
                      <input type="file" accept="image/*" hidden onChange={handleLogoUpload} />
                    </label>
                  </div>
                  <input value={agencyForm.companyLogoUrl} onChange={(e) => setAgencyForm({ ...agencyForm, companyLogoUrl: e.target.value })} placeholder="Logo URL" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

                   <input value={agencyForm.contactEmail} onChange={(e) => setAgencyForm({ ...agencyForm, contactEmail: e.target.value })} type="email" placeholder="Contact email" className={`col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white ${editingAgency ? 'hidden' : ''}`} required={editingAgency ? false : true} />
                   {editingAgency && (
                     <div className="col-span-2 text-xs text-slate-500">
                       Contact email is managed via the linked user record.
                     </div>
                   )}
                   <input value={agencyForm.headquartersAddress} onChange={(e) => setAgencyForm({ ...agencyForm, headquartersAddress: e.target.value })} placeholder="Headquarters address" className={`col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white ${editingAgency ? 'hidden' : ''}`} />
                   {editingAgency && (
                     <div className="col-span-2 text-xs text-slate-500">
                       Headquarters address is managed at the corporate entity level.
                     </div>
                   )}

                  {/* 🧬 PARENT AGENCY — PlatformAdmin may nest this agency under an existing parent to model a sub-brand / branch */}
                  <select disabled={!isSuperAdmin} value={agencyForm.parentAgencyId} onChange={(e) => setAgencyForm({ ...agencyForm, parentAgencyId: e.target.value })} className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
                    <option value="">{isSuperAdmin ? '(No parent — top-level agency)' : 'Parent: your agency (assigned automatically)'}</option>
                    {agencies.filter(a => !editingAgency || a.agencyId !== editingAgency.agencyId).map(a => (
                      <option key={a.agencyId} value={a.agencyId}>{a.name} ({a.agencyId})</option>
                    ))}
                  </select>

                  {/* 🎯 TRIAL ONBOARDING — flag this agency as a trial and pre-fill the trial window */}
                  <label className="col-span-2 flex items-center gap-2 text-[11px] text-slate-300">
                    <input type="checkbox" checked={agencyForm.isTrial} onChange={(e) => setAgencyForm({ ...agencyForm, isTrial: e.target.checked })} className="accent-amber-500" />
                    Onboard as trial agency (read-only access until trial ends)
                  </label>
                  {agencyForm.isTrial && (
                    <>
                      <input type="date" aria-label="Trial start date" value={agencyForm.trialStartDate ? agencyForm.trialStartDate.substring(0, 10) : ''} onChange={(e) => setAgencyForm({ ...agencyForm, trialStartDate: e.target.value })} className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                      <input type="date" aria-label="Trial end date" value={agencyForm.trialEndDate ? agencyForm.trialEndDate.substring(0, 10) : ''} onChange={(e) => setAgencyForm({ ...agencyForm, trialEndDate: e.target.value })} className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                    </>
                  )}

                  {/* 👤 Auto-provisioned AgencyAdmin login (only required when creating) */}
                  {!editingAgency && (
                    <>
                      <input value={agencyForm.adminFullName} onChange={(e) => setAgencyForm({ ...agencyForm, adminFullName: e.target.value })} placeholder="Admin full name" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                      <input value={agencyForm.adminEmail} onChange={(e) => setAgencyForm({ ...agencyForm, adminEmail: e.target.value })} type="email" placeholder="Admin login email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                      <input value={agencyForm.adminPassword} onChange={(e) => setAgencyForm({ ...agencyForm, adminPassword: e.target.value })} type="password" placeholder="Admin initial password" autoComplete="new-password" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                    </>
                  )}
                </div>
                <Can permission={editingAgency ? 'agencies:edit' : 'agencies:create'}>
                  <button type="submit" className="w-full py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">{editingAgency ? 'Save agency' : 'Provision agency + admin user'}</button>
                </Can>
              </form>
            ) : (
              <div className="bg-slate-950/40 border border-slate-800 rounded-2xl p-5 flex flex-col gap-3">
                <p className="text-xs font-bold text-slate-400 uppercase">Agency provisioning</p>
                <p className="text-xs text-slate-500">Only a Platform Administrator can create agencies. This action is restricted to the platform super-admin role.</p>
              </div>
            )}


            {/* 👤 CONTACT PERSON FORM (for the expanded agency) */}
            {expandedAgency && (
              <form onSubmit={handleContactSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
                    <UserPlus size={14} className="text-emerald-500" /> {editingContact ? 'Edit Contact' : 'Add Contact'}
                  </div>
                  <button type="button" onClick={() => { setExpandedAgency(null); setEditingContact(null); setContactForm({ fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '', contactType: 'Staff', isAgencyAdmin: false }); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <input value={contactForm.fullName} onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })} placeholder="Full name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                  <input value={contactForm.emailAddress} onChange={(e) => setContactForm({ ...contactForm, emailAddress: e.target.value })} type="email" placeholder="Email" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={contactForm.phoneNumber} onChange={(e) => setContactForm({ ...contactForm, phoneNumber: e.target.value })} placeholder="Phone" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={contactForm.positionJobTitle} onChange={(e) => setContactForm({ ...contactForm, positionJobTitle: e.target.value })} placeholder="Position" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <select value={contactForm.contactType} onChange={(e) => setContactForm({ ...contactForm, contactType: e.target.value })} className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white"><option>Owner</option><option>Tenant</option><option>Staff</option><option>Manager</option><option>Broker</option></select>
                  <label className="col-span-2 flex items-center gap-2 text-[10px] text-slate-400"><input type="checkbox" checked={contactForm.isAgencyAdmin} onChange={(e) => setContactForm({ ...contactForm, isAgencyAdmin: e.target.checked })} /> Promote to AgencyAdmin on save</label>
                </div>
                <Can permission={editingContact ? 'users:edit' : 'users:create'}>
                  <button type="submit" className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold">{editingContact ? 'Update contact' : 'Create contact' }</button>
                </Can>
              </form>
            )}
          </div>

          {/* 🏢 AGENCY ROSTER (click → contacts) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Building2 size={14} className="text-blue-500" /> Agency roster</div>
            <input aria-label="Filter agency roster" placeholder="Search agencies by name, ID or email…" value={agencySearch} onChange={event => setAgencySearch(event.target.value)} className="w-full mb-3 px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-sm text-white" />
            <p className="text-xs text-slate-400 mb-3">{filteredAgencies.length} of {agencies.length} agencies</p>
            {filteredAgencies.length === 0 && <p className="text-sm text-slate-400">No agencies match your search.</p>}
            <div className="space-y-3">
              {filteredAgencies.map((agency) => (
                <div key={agency.agencyId} className="rounded-xl border border-slate-800 bg-slate-950/60">
                  <div className="p-3 flex justify-between items-center gap-2 cursor-pointer" onClick={() => { setExpandedAgency(agency.agencyId); setEditingContact(null); if (!agencyContacts[agency.agencyId]) loadContacts(agency.agencyId); }}>
                    <div className="flex items-center gap-3">
                      <ChevronRight size={14} className="text-slate-500 transition-transform" />
                      <div className="flex items-center gap-2.5">
                        {agency.companyLogoUrl ? <img src={agency.companyLogoUrl} alt={agency.name} className="w-7 h-7 rounded object-cover" /> : <Building2 size={14} className="text-slate-500" />}
                        <span className="text-white font-bold">{agency.tradingName || agency.name}</span>
                      </div>
                      <span className="text-[10px] text-slate-500 font-mono">#{agency.agencyId}</span>
                      <span className={`text-[10px] px-2 py-1 rounded-md border ${agency.operationalStatus === 'Active' ? 'border-emerald-900 bg-emerald-950/40 text-emerald-300' : 'border-amber-900 bg-amber-950/40 text-amber-300'}`}>{agency.operationalStatus || 'Active'}</span>
                      {agency.isTrial && <span className="text-[10px] px-2 py-1 rounded-md border border-amber-900 bg-amber-950/40 text-amber-300">TRIAL</span>}
                      {agency.parentAgencyId && <span className="text-[10px] px-2 py-1 rounded-md border border-indigo-900 bg-indigo-950/40 text-indigo-300">↳ child of #{agency.parentAgencyId}</span>}
                      {(() => {
                        const childCount = agencies.filter(a => a.parentAgencyId === agency.agencyId).length;
                        return childCount > 0 ? <span className="text-[10px] px-2 py-1 rounded-md border border-blue-900 bg-blue-950/40 text-blue-300">{childCount} child{childCount === 1 ? '' : 'ren'}</span> : null;
                      })()}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] text-slate-500">{agency.registrationNumber || 'Reg: —'}</span>
                      <Can permission="agencies:edit">
                        <button type="button" onClick={(e) => { e.stopPropagation(); resetAgency(agency); }} className="text-slate-400 hover:text-white"><Pencil size={13} /></button>
                      </Can>
                    </div>
                  </div>

                  {/* Expanded contact persons */}
                  {expandedAgency === agency.agencyId && (
                    <div className="px-4 pb-3 border-t border-slate-800 pt-2 space-y-2">
                      <div className="text-[10px] text-slate-400 font-bold uppercase">Agency contacts — promote to broker</div>
                      {(agencyContacts[agency.agencyId] || []).map((c) => (
                        <div key={c.contactId} className="flex justify-between items-center rounded-lg border border-slate-800 bg-slate-950/40 p-2.5">
                          <div className="flex-1 truncate">
                            <div className="text-slate-200 text-xs font-semibold">{c.fullName}</div>
                            <div className="text-[10px] text-slate-500">{c.emailAddress} • {c.positionJobTitle || '—'}{c.isAgencyAdmin && <Tag size={10} className="ml-1 text-violet-400" />}</div>
                          </div>
                          <div className="flex gap-1.5">
                            <Can permission="users:edit">
                              <button type="button" onClick={async () => {
                                if (window.confirm(`Promote ${c.fullName} to a broker profile?`)) {
                                  try { await crmService.promoteContactToBroker(c.contactId); await loadBrokers(); await loadAgencies(); await loadContacts(agency.agencyId); }
                                  catch (err) { alert(err || 'Promotion failed.'); }
                                }
                              }} className="p-1 rounded bg-violet-950 text-violet-300 border border-violet-900 hover:text-white" title="Promote to broker">
                                <ShieldCheck size={11} />
                              </button>
                            </Can>
                            <Can permission="users:delete">
                              <button type="button" onClick={async () => {
                                if (window.confirm('Remove this contact?')) {
                                  try { await crmService.deleteContactPerson(c.contactId); await loadContacts(agency.agencyId); await loadAgencies(); }
                                  catch (err) { alert(err || 'Removal failed.'); }
                                }
                              }} className="p-1 rounded bg-red-950/30 text-red-300 border border-red-900 hover:text-white" title="Delete">
                                <Trash2 size={11} />
                              </button>
                            </Can>
                          </div>
                        </div>
                      ))}
                      {(!agencyContacts[agency.agencyId]) && <div className="text-[10px] text-slate-500">Loading contacts…</div>}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </>
      )}

      {activeTab === 'users' && (
        <div className="grid gap-6 xl:grid-cols-2">
          {/* 👤 USER FORM — SuperAdmin and AgencyAdmin can create/edit users with assignable roles */}
          {(isSuperAdmin || currentUserRole === 'AgencyAdmin') && (
          <form onSubmit={async (e) => {
             e.preventDefault();
             const portalCodes = Object.fromEntries([...new FormData(e.currentTarget).entries()]
               .filter(([name]) => name.startsWith('portalCode:')).map(([name, value]) => [name.slice(11), value]));
             try {
               let managedUserId = editingUser?.userId;
               const targetTenantId = editingUser?.tenantId || newUserTenantId || activeAgency?.tenantId || activeProfile?.tenantId;
               const targetAgency = agencies.find(agency => agency.tenantId === targetTenantId);
               if (!editingUser && userForm.systemRole === 'Broker' && !targetAgency) throw new Error('Select the agency for this broker.');
                if (editingUser && editingUser.userId) {
                  if (userForm.systemRole !== editingUser.systemRole) await authService.updateUserRole(editingUser.userId, userForm.systemRole);
                  if (userForm.password) {
                    await authService.adminResetPassword(editingUser.userId, userForm.password);
                  }
                } else {
                  const createdUser = await authService.registerUser({
                   tenantId: targetTenantId,
                   fullName: userForm.fullName,
                   email: userForm.email,
                   systemRole: userForm.systemRole,
                   passwordHash: userForm.password || 'ChangeMeNow!',
                   licenseNumber: userForm.licenseNumber || ''
                 });
                  managedUserId = createdUser.userId;
                  setEditingUser({ ...createdUser, userId: managedUserId, tenantId: targetTenantId });
                  if (userForm.systemRole === 'Broker') {
                    try {
                      await crmService.createBroker({ brokerId: managedUserId, tenantId: targetTenantId, agencyId: targetAgency.agencyId, fullName: userForm.fullName, email: userForm.email, role: 'Broker', status: 'Active' });
                    } catch {
                      await loadUsers();
                      throw new Error('The user account was created, but the broker profile could not be created. Do not submit a duplicate account.');
                    }
                  }
                }
                if (userForm.systemRole === 'Broker' && Object.keys(portalCodes).length) {
                  await apiClient.put(`/crm/brokers/${encodeURIComponent(managedUserId)}/syndication-codes`, { codes: portalCodes });
                }
                // 🎯 If this is a Broker, upsert the current month's target.
                if (userForm.systemRole === 'Broker' && userForm.monthlyTargetCommission) {
                  const now = new Date();
                  await crmService.setBrokerTarget({
                    brokerId: managedUserId,
                    year: now.getFullYear(),
                    month: now.getMonth() + 1,
                    targetCommission: Number(userForm.monthlyTargetCommission)
                  });
                }
                setUserForm({ fullName: '', email: '', password: '', systemRole: 'Broker', licenseNumber: '', monthlyTargetCommission: '' });
              setEditingUser(null);
              await loadUsers();
            } catch (err) { alert(err || 'User save failed.'); }
          }} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><UserPlus size={14} className="text-emerald-500" /> {editingUser ? 'Edit User' : 'Add User'}</div>
            <input value={userForm.fullName} onChange={(e) => setUserForm({ ...userForm, fullName: e.target.value })} placeholder="Full name" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
            <div className="grid grid-cols-2 gap-3">
              <input value={userForm.email} onChange={(e) => setUserForm({ ...userForm, email: e.target.value })} type="email" placeholder="Email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
              <input value={userForm.password} onChange={(e) => setUserForm({ ...userForm, password: e.target.value })} type="password" placeholder={editingUser ? 'Leave blank to keep' : 'Password'} autoComplete="new-password" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            </div>
                {!editingUser && <label className="block text-xs text-slate-400">Agency
                  <select value={newUserTenantId || activeAgency?.tenantId || activeProfile?.tenantId || ''} onChange={event => setNewUserTenantId(event.target.value)} className="mt-1 w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-white">
                    {agencies.map(agency => <option key={agency.agencyId} value={agency.tenantId}>{agency.name}</option>)}
                  </select>
                </label>}
                <select value={userForm.systemRole} onChange={(e) => setUserForm({ ...userForm, systemRole: e.target.value })} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
                  {assignableRoles.map((r) => <option key={r} value={r}>{r}</option>)}
                </select>
                {/* 🪪 License number — required for brokers, optional for AgencyAdmin (brokerage license) */}
                {(userForm.systemRole === 'Broker' || userForm.systemRole === 'AgencyAdmin') && (
                  <input value={userForm.licenseNumber} onChange={(e) => setUserForm({ ...userForm, licenseNumber: e.target.value })} placeholder={userForm.systemRole === 'Broker' ? 'Broker License #' : 'Brokerage License #'} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                )}
                {userForm.systemRole === 'Broker' && <BrokerSyndicationFields key={editingUser?.userId || newUserTenantId || activeAgency?.tenantId || 'new'} brokerId={editingUser?.userId} />}
                {/* 🎯 Monthly target commission — only for Brokers */}
                {userForm.systemRole === 'Broker' && (
                  <input value={userForm.monthlyTargetCommission} onChange={(e) => setUserForm({ ...userForm, monthlyTargetCommission: e.target.value })} type="number" placeholder="Monthly Target Commission (R)" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                )}
            <div className="flex gap-2">
              <Can permission={editingUser ? 'users:edit' : 'users:create'}>
                <button type="submit" className="flex-1 py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold">{editingUser ? 'Save user' : 'Create user'}</button>
              </Can>
              {editingUser && <button type="button" onClick={() => { setEditingUser(null); setUserForm({ fullName: '', email: '', password: '', systemRole: 'Broker', licenseNumber: '', monthlyTargetCommission: '' }); }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300"><CircleX size={12} className="inline mr-1" /> Cancel</button>}
            </div>
          </form>
          )}

          {/* 🕵️ USER ROSTER */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Users size={14} className="text-violet-500" /> User roster ({currentUserRole === 'PlatformAdmin' ? 'All tenants' : 'Your tenant'})</div>
            <input
              type="text"
              placeholder="Search users..."
              value={usersSearch}
              onChange={(e) => { const next = e.target.value; setUsersSearch(next); setUsersPage(1); loadUsers(1, next); }}
              className="w-full mb-3 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-violet-600"
            />
            <div className="space-y-3">
              {systemUsers.map((u) => (
                <div key={u.userId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-3 flex justify-between items-center">
                  <div>
                    <h4 className="text-white font-bold text-sm">{u.fullName}</h4>
                    <p className="text-xs text-slate-400">{u.email} • {u.phone || 'No phone'}</p>
                    <p className="text-[11px] text-slate-500">Tenant: {u.tenantId}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-[10px] px-2 py-1 rounded-md bg-violet-950 text-violet-300 border border-violet-900">{u.systemRole}</span>
                      {(isSuperAdmin || currentUserRole === 'AgencyAdmin') && u.systemRole !== 'PlatformAdmin' && (
                        <>
                           <Can permission="users:edit">
                             <button type="button" onClick={async () => {
                               setEditingUser(u);
                               setUserForm({ fullName: u.fullName || '', email: u.email || '', password: '', systemRole: u.systemRole || 'Broker', licenseNumber: u.licenseNumber || '', monthlyTargetCommission: '' });
                               if (u.systemRole === 'Broker') {
                                 try {
                                   const now = new Date();
                                   const targets = await crmService.getBrokerTargets(u.userId, now.getFullYear());
                                   const currentTarget = targets.find(t => t.month === now.getMonth() + 1 && t.year === now.getFullYear());
                                   if (currentTarget) {
                                     setUserForm((prev) => ({ ...prev, monthlyTargetCommission: currentTarget.targetCommission }));
                                   }
                                 } catch { /* non-fatal */ }
                               }
                             }} className="p-2 text-slate-400 hover:text-white"><Pencil size={13} /></button>
                           </Can>
                          {u.accountStatus !== 'Disabled' && (
                            <Can permission="users:edit">
                              <button type="button" onClick={async () => {
                                if (!confirm(`Deactivate user "${u.fullName}"? They will be unable to sign in.`)) return;
                                try {
                                  await authService.disableUser(u.userId);
                                  setSystemUsers((prev) => prev.map((x) => (x.userId === u.userId ? { ...x, accountStatus: 'Disabled' } : x)));
                                } catch (err) { alert(err || 'Deactivation failed.'); }
                              }} className="p-2 text-red-400 hover:text-red-300"><CircleX size={13} /></button>
                            </Can>
                          )}
                        </>
                      )}
                  </div>
                </div>
              ))}
            </div>
            <div className="mt-3">
              <Pagination
                page={usersPage}
                pageSize={usersPageSize}
                totalCount={usersTotal}
                onPageChange={(p) => { setUsersPage(p); loadUsers(p, usersSearch); }}
              />
            </div>
          </div>
        </div>
      )}

      {activeTab === 'amenities' && isSuperAdmin && (
        <AmenityTypesWorkspace />
      )}


      {activeTab === 'captured-leads' && isSuperAdmin && (
        <div className="grid gap-6 xl:grid-cols-3">
          {/* 📝 LEAD FORM (add / edit) */}
          <div className="xl:col-span-1">
            <form onSubmit={handleLeadSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4 sticky top-6 h-fit">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
                  <Mail size={14} className="text-emerald-400" />
                  {editingLead ? 'Edit Captured Lead' : 'Add Manual Lead'}
                </div>
                {editingLead && (
                  <button
                    type="button"
                    onClick={() => { setEditingLead(null); setLeadForm({ fullName: '', email: '', phone: '', company: '', userType: 'Individual', package: '', demoRequested: false, message: '', source: 'website' }); }}
                    className="text-slate-400 hover:text-white"
                    title="Cancel edit"
                  >
                    <X size={14} />
                  </button>
                )}
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Full Name *</label>
                <input
                  type="text" required
                  value={leadForm.fullName}
                  onChange={(e) => setLeadForm({ ...leadForm, fullName: e.target.value })}
                  placeholder="Jane Smith"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Email *</label>
                <input
                  type="email" required
                  value={leadForm.email}
                  onChange={(e) => setLeadForm({ ...leadForm, email: e.target.value })}
                  placeholder="jane@example.com"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Phone</label>
                <input
                  type="tel"
                  value={leadForm.phone}
                  onChange={(e) => setLeadForm({ ...leadForm, phone: e.target.value })}
                  placeholder="+27 82 123 4567"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Company</label>
                <input
                  type="text"
                  value={leadForm.company}
                  onChange={(e) => setLeadForm({ ...leadForm, company: e.target.value })}
                  placeholder="Company name"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">User Type</label>
                <select
                  value={leadForm.userType}
                  onChange={(e) => setLeadForm({ ...leadForm, userType: e.target.value })}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                >
                  <option value="Individual">Individual</option>
                  <option value="Agency">Agency</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Package</label>
                <select
                  value={leadForm.package}
                  onChange={(e) => setLeadForm({ ...leadForm, package: e.target.value })}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                >
                  <option value="">None</option>
                  <option value="SMEAgency">SME Agency</option>
                  <option value="Private">Private</option>
                  <option value="Enterprise">Enterprise</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Source</label>
                <input
                  type="text"
                  value={leadForm.source}
                  onChange={(e) => setLeadForm({ ...leadForm, source: e.target.value })}
                  placeholder="website"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label className="block text-slate-400 text-[10px] uppercase tracking-wider mb-1.5">Message</label>
                <textarea
                  rows={3}
                  value={leadForm.message}
                  onChange={(e) => setLeadForm({ ...leadForm, message: e.target.value })}
                  placeholder="Additional notes or requirements"
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white focus:outline-none focus:border-emerald-500 resize-none"
                />
              </div>

              <div className="flex items-center gap-2 text-[10px] text-slate-300">
                <input
                  type="checkbox"
                  id="demoRequested"
                  checked={leadForm.demoRequested}
                  onChange={(e) => setLeadForm({ ...leadForm, demoRequested: e.target.checked })}
                  className="rounded bg-slate-800 border-slate-600 text-emerald-500"
                />
                <label htmlFor="demoRequested" className="cursor-pointer">Demo requested</label>
              </div>

              <Can permission={editingLead ? 'leads:edit' : 'leads:create'}>
                <button
                  type="submit"
                  className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold hover:bg-emerald-500 transition-colors"
                >
                  {editingLead ? 'Update Lead' : 'Create Lead'}
                </button>
              </Can>
            </form>
          </div>

          {/* 📋 LEADS LIST with infinite scroll */}
          <div className="xl:col-span-2">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
                <Mail size={14} className="text-emerald-400" />
                New Lead Queue ({leadsTotal})
              </div>
              <button
                type="button"
                onClick={() => loadLeads(1, true)}
                disabled={leadsLoading}
                className="p-1.5 rounded-lg bg-slate-800 text-slate-400 hover:text-white disabled:opacity-50"
                title="Refresh"
              >
                <RefreshCw size={14} className={leadsLoading ? 'animate-spin' : ''} />
              </button>
            </div>

            {leadsError && (
              <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs mb-3">{leadsError}</div>
            )}

            <div className="space-y-2">
              {leads.map((lead) => (
                <div
                    key={lead.id}
                    className="rounded-xl border border-slate-800 bg-slate-950/40 p-3 flex justify-between items-start gap-3"
                  >
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-white font-semibold text-sm">{lead.fullName}</span>
                        {lead.demoRequested && <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-950 text-blue-300 border border-blue-900">Demo</span>}
                      </div>
                      <p className="text-xs text-slate-400">{lead.email}</p>
                      {lead.phone && <p className="text-xs text-slate-500">{lead.phone}</p>}
                      {lead.company && <p className="text-xs text-slate-500">{lead.company}</p>}
                      <div className="flex items-center gap-2 mt-1">
                        <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-slate-800 text-slate-400 border border-slate-700">{lead.userType}</span>
                        {lead.package && <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-violet-950 text-violet-300 border border-violet-900">{lead.package}</span>}
                        <span className="text-[10px] text-slate-500">via {lead.source || 'website'}</span>
                      </div>
                      {lead.message && <p className="text-[11px] text-slate-400 mt-1 line-clamp-2">{lead.message}</p>}
                      <p className="text-[10px] text-slate-500 mt-1">{new Date(lead.createdAt).toLocaleString()}</p>
                    </div>

                    <div className="flex flex-col gap-1 shrink-0">
                      <Can permission="leads:edit">
                        <button
                          type="button"
                          onClick={() => resetLead(lead)}
                          className="p-1.5 rounded-lg bg-slate-800 text-slate-400 hover:text-white"
                          title="Edit lead"
                        >
                          <Pencil size={12} />
                        </button>
                      </Can>
                      <Can permission="leads:edit">
                        <select
                          onChange={(e) => handleLeadStatusChange(lead.id, e.target.value)}
                          defaultValue=""
                          className="text-[10px] bg-slate-800 border border-slate-700 rounded-lg px-1.5 py-0.5 text-slate-300 focus:outline-none focus:border-emerald-500"
                          title="Advance lead status"
                        >
                          <option value="" disabled>Status →</option>
                          {LEAD_STATUSES.map((s) => (
                            <option key={s} value={s}>{s}</option>
                          ))}
                        </select>
                      </Can>
                    </div>
                  </div>
              ))}

              {leadsLoading && (
                <div className="text-center py-6 text-slate-500 text-xs">
                  <RefreshCw size={16} className="animate-spin inline mb-1" />
                  <p>Loading leads…</p>
                </div>
              )}

              {!leadsLoading && leads.length === 0 && (
                <div className="text-center py-10 text-slate-500 text-xs">
                  <AlertCircle size={16} className="inline mb-1" />
                  <p>No new leads in the queue.</p>
                </div>
              )}

               {leadsHasMore && !leadsLoading && (
                 <div ref={leadsSentinelRef} className="h-1" />
               )}
             </div>
           </div>
         </div>
       )}

       {activeTab === 'import' && isSuperAdmin && (
         <div className="space-y-6">
           <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
             <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
               <Upload size={14} className="text-orange-400" /> Import Data
             </div>
               <p className="text-xs text-slate-500 mb-4">Import CRM, Property and Identity seed data from a single Excel workbook. The template contains one sheet per table (Agencies, Brokers, Contacts, Companies, LegalEntities, Leads, Deals, Geography, Properties, Units, PropertyTypes, AmenityTypes, TenantAgencies, UserAccounts). Imports are processed asynchronously via the import queue and you will be notified when complete.</p>

              <div className="mb-4">
                <label className="block text-[10px] uppercase tracking-widest text-slate-500 mb-1 font-bold">Excel File</label>
                <input type="file" accept=".xlsx,.xls" onChange={(e) => setImportFile(e.target.files?.[0] || null)} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white file:mr-2 file:py-1 file:px-3 file:rounded-lg file:border-0 file:text-xs file:font-semibold file:bg-slate-800 file:text-slate-200 hover:file:bg-slate-700" />
              </div>

              <div className="flex gap-2 mb-4">
                <button type="button" onClick={async () => {
                  try {
                    const response = await apiClient.get('/properties/import/template', { responseType: 'blob' });
                    const blob = response.data;
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = 'system-data-template.xlsx';
                    a.click();
                    URL.revokeObjectURL(url);
                  } catch { alert('Failed to download template.'); }
                }} className="px-4 py-2 rounded-xl bg-slate-800 text-slate-200 text-xs font-bold hover:bg-slate-700 cursor-pointer">Download Template</button>

                <button type="button" disabled={!importFile} onClick={async () => {
                  if (!importFile) return;
                  setImportPolling(true);
                  setImportStatus(null);
                   try {
                     const buffer = await importFile.arrayBuffer();
                     const base64 = btoa(String.fromCharCode(...new Uint8Array(buffer)));
                     const response = await apiClient.post('/properties/import/upload', { fileName: importFile.name, base64Content: base64 });
                     setImportJobId(response.data.jobId);
                   } catch (err) { alert(err.message); setImportPolling(false); }
                }} className="px-4 py-2 rounded-xl bg-orange-600 text-white text-xs font-bold hover:bg-orange-500 disabled:opacity-40 cursor-pointer">Upload & Import</button>
              </div>

             {importJobId && (
               <div className="bg-slate-950 border border-slate-800 rounded-xl p-4">
                 <div className="text-xs font-bold text-white mb-2">Job {importJobId}</div>
                 <div className="h-2 rounded-full bg-slate-800 overflow-hidden">
                   <div id="import-progress" className="h-full rounded-full bg-orange-500 transition-all" style={{ width: '0%' }} />
                 </div>
                 <div id="import-status-text" className="text-[10px] text-slate-500 mt-1">Queued...</div>
               </div>
             )}
           </div>
         </div>
       )}
     </div>
   );
}
