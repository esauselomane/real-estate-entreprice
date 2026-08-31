import { useEffect, useState, useRef } from 'react';
import { Building2, UserPlus, Users, Pencil, Trash2, ShieldCheck, Tag, ChevronRight, X, Settings, CircleX, Mail, RefreshCw, AlertCircle, BarChart3, TrendingUp, DollarSign, Trophy } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line, Legend } from 'recharts';
import { crmService } from '../services/crmService.js';
import { systemRolesService } from '../services/systemRolesService.js';
import { authService } from '../services/authService.js';
import { leadCaptureService } from '../services/leadCaptureService.js';
import { useAuth } from '../contexts/AuthContext.jsx';

export default function SystemAdminWorkspace() {
  const { userRole: currentUserRole } = useAuth();
  const [activeTab, setActiveTab] = useState('agencies');
  const [agencies, setAgencies] = useState([]);
  const [brokers, setBrokers] = useState([]);
  const [systemUsers, setSystemUsers] = useState([]);
  const [agencyContacts, setAgencyContacts] = useState({});
  const [roles, setRoles] = useState([]);
  const [roleForm, setRoleForm] = useState({ name: '', description: '', isPlatformScoped: false, isAgencyScoped: false, permissionsJson: '[]', isActive: true });
  const [editingRole, setEditingRole] = useState(null);
  const [agencyForm, setAgencyForm] = useState({
    name: '', tradingName: '', registrationNumber: '', companyLogoUrl: '',
    contactEmail: '', headquartersAddress: '',
    adminFullName: '', adminEmail: '', adminPassword: ''
  });
  const [brokerForm, setBrokerForm] = useState({ fullName: '', email: '', phone: '', agencyId: '', role: 'Broker' });
  const [contactForm, setContactForm] = useState({ fullName: '', email: '', phone: '', position: '', role: 'Owner', isAgencyAdmin: false });
  const [error, setError] = useState('');
  const [editingAgency, setEditingAgency] = useState(null);
  const [editingBroker, setEditingBroker] = useState(null);
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

  // 📊 REPORTS & ANALYTICS — tenant-scoped pipeline / commission / broker metrics
  const [pipelineSummary, setPipelineSummary] = useState(null);
  const [reportOverview, setReportOverview] = useState(null);
  const [commissionByStage, setCommissionByStage] = useState([]);
  const [brokerLeaderboard, setBrokerLeaderboard] = useState([]);
  const [reportsLoading, setReportsLoading] = useState(false);
  const [reportsError, setReportsError] = useState('');

  const loadAgencies = async () => {
    try {
      const rows = await crmService.getAgencies();
      setAgencies(rows || []);
      if (!agencyForm.agencyId && (rows || []).length) {
        setBrokerForm((prev) => ({ ...prev, agencyId: rows[0].agencyId || rows[0].tenantId || '' }));
      }
    } catch (err) { setError(err || 'Unable to load agency records.'); }
  };

  const loadBrokers = async () => {
    try { setBrokers(await crmService.getBrokers() || []); }
    catch (err) { setError(err || 'Unable to load broker roster.'); }
  };

  const loadContacts = async (agencyId) => {
    try {
      const contacts = await crmService.getAgencyContacts(agencyId);
      setAgencyContacts((prev) => ({ ...prev, [agencyId]: contacts || [] }));
    } catch (err) {
      setAgencyContacts((prev) => ({ ...prev, [agencyId]: [] }));
    }
  };

  const loadRoles = async () => {
    try { setRoles(await systemRolesService.getSystemRoles() || []); }
    catch (err) { setError(err || 'Unable to load system roles.'); }
  };

  const loadUsers = async () => {
    try { setSystemUsers(await authService.getUsers() || []); }
    catch (err) { setError(err || 'Unable to load user directory.'); }
  };

  const isSuperAdmin = currentUserRole === 'PlatformAdmin';

  useEffect(() => { loadData(); }, []);

  const loadData = async () => { await Promise.all([loadAgencies(), loadBrokers(), loadUsers(), isSuperAdmin && loadRoles()]); };

  // Roles an admin may assign: PlatformAdmin gets the full palette; AgencyAdmin only agency-scoped roles.
  const assignableRoles = isSuperAdmin ? ['PlatformAdmin', 'AgencyAdmin', 'Broker', 'Owner', 'Tenant'] : ['Broker', 'Owner', 'Tenant'];

  const handleRoleSubmit = async (e) => {
    e.preventDefault();
    if (!isSuperAdmin) return;
    try {
      if (editingRole) await systemRolesService.updateSystemRole(editingRole.roleDefId, roleForm);
      else await systemRolesService.createSystemRole(roleForm);
      setRoleForm({ name: '', description: '', isPlatformScoped: false, isAgencyScoped: false, permissionsJson: '[]', isActive: true });
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
      permissionsJson: role.permissionsJson || '[]',
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
      if (editingAgency) await crmService.updateAgency(editingAgency.agencyId, { ...editingAgency, ...agencyForm });
      else {
        await crmService.createAgency(agencyForm);
        // 🚀 On agency creation, automatically provision an AgencyAdmin user bound to the
        // same tenant, so the new agency has an accountable administrator login.
        if (!editingAgency && agencyForm.adminEmail) {
          try {
            await authService.registerUser({
              fullName: agencyForm.adminFullName || agencyForm.contactEmail,
              email: agencyForm.adminEmail,
              systemRole: 'AgencyAdmin',
              passwordHash: agencyForm.adminPassword || 'ChangeMeNow!'
            });
          } catch (err) {
            alert(`Agency created, but the linked admin user could not be provisioned: ${err || 'registration failed'}`);
          }
        }
      }
      setAgencyForm({ name: '', tradingName: '', registrationNumber: '', companyLogoUrl: '', contactEmail: '', headquartersAddress: '', adminFullName: '', adminEmail: '', adminPassword: '' });
      setEditingAgency(null);
      await loadAgencies();
    } catch (err) { alert(err || 'Agency creation failed.'); }
  };

  const handleBrokerSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingBroker) await crmService.updateBroker(editingBroker.brokerId, { ...editingBroker, ...brokerForm });
      else await crmService.createBroker(brokerForm);
      setBrokerForm((prev) => ({ ...prev, fullName: '', email: '', phone: '', role: 'Broker' }));
      setEditingBroker(null);
      await loadBrokers();
    } catch (err) { alert(err || 'Broker onboarding failed.'); }
  };

  const handleContactSubmit = async (e) => {
    e.preventDefault();
    const targetAgency = editingContact ? editingContact.agencyId : expandedAgency;
    try {
      if (editingContact) await crmService.updateContactPerson(editingContact.contactPersonId, contactForm);
      else await crmService.createContactPerson(targetAgency, contactForm);
      setContactForm({ fullName: '', email: '', phone: '', position: '', role: 'Owner', isAgencyAdmin: false });
      setEditingContact(null);
      await loadContacts(targetAgency);
    } catch (err) { alert(err || 'Contact person save failed.'); }
  };

  const resetAgency = (agency) => {
    setEditingAgency(agency);
    setAgencyForm({
      name: agency.name || '', tradingName: agency.tradingName || '', registrationNumber: agency.registrationNumber || '',
      companyLogoUrl: agency.companyLogoUrl || '', contactEmail: agency.contactEmail || '', headquartersAddress: agency.headquartersAddress || '',
      adminFullName: '', adminEmail: '', adminPassword: ''
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
      setLeadsError(err || 'Unable to load captured leads.');
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

  useEffect(() => {
    if (activeTab !== 'captured-leads' || !isSuperAdmin || leads.length > 0 || leadsLoading) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLeads(1, true);
  }, [activeTab, isSuperAdmin, leads.length, leadsLoading]);

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

  // 📊 Load all report datasets when the reports tab is activated (PlatformAdmin only)
  const loadReports = async () => {
    if (!isSuperAdmin) return;
    setReportsLoading(true);
    setReportsError('');
    try {
      const [summary, overview, commission, leaderboard] = await Promise.all([
        crmService.getPipelineReportSummary().catch(() => null),
        crmService.getCRMReportOverview().catch(() => null),
        crmService.getCommissionByStage().catch(() => []),
        crmService.getBrokerLeaderboard().catch(() => [])
      ]);
      setPipelineSummary(summary);
      setReportOverview(overview);
      setCommissionByStage(commission || []);
      setBrokerLeaderboard(leaderboard || []);
      if (!summary && !overview) setReportsError('Some report data could not be loaded.');
    } catch {
      setReportsError('Unable to load report data.');
    } finally {
      setReportsLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'reports') {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      loadReports();
    }
  }, [activeTab, isSuperAdmin]);


  return (
    <div className="space-y-6 animate-fade-in">
      {error && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{error}</div>}

      {/* 🎛️ DRILL-NAVIGATION MENU */}
      <div className="flex gap-2 bg-slate-900 border border-slate-800 p-1.5 rounded-2xl shadow-xl overflow-x-auto">
        <button
          onClick={() => setActiveTab('agencies')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'agencies' ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
          <Building2 size={12} className="inline mr-1.5" /> Agency Admin
        </button>
        <button
          onClick={() => setActiveTab('users')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'users' ? 'bg-violet-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
          <Users size={12} className="inline mr-1.5" /> User Admin
        </button>
        <button
          onClick={() => setActiveTab('access')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'access' ? 'bg-violet-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
          <ShieldCheck size={12} className="inline mr-1.5" /> User Access
        </button>
        <button
          onClick={() => setActiveTab('roles')}
          type="button"
          className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
            activeTab === 'roles' ? 'bg-violet-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
          }`}
        >
           <Settings size={12} className="inline mr-1.5" /> System Roles
         </button>
         {isSuperAdmin && (
          <button
            onClick={() => setActiveTab('reports')}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
              activeTab === 'reports' ? 'bg-teal-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            <BarChart3 size={12} className="inline mr-1.5" /> Reports & Analytics
          </button>
        )}
        {isSuperAdmin && (
          <button
            onClick={() => setActiveTab('captured-leads')}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer ${
              activeTab === 'captured-leads' ? 'bg-emerald-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            <Mail size={12} className="inline mr-1.5" /> Captured Leads
          </button>
        )}
      </div>

      {activeTab === 'agencies' && (
        <>
          <div className="grid gap-6 xl:grid-cols-2">
            {/* 🏢 AGENCY FORM — visible to PlatformAdmin only. AgencyAdmin cannot add agencies. */}
            {isSuperAdmin ? (
              <form onSubmit={handleAgencySubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
                  <Building2 size={14} className="text-blue-500" /> {editingAgency ? 'Edit Agency' : 'Add Agency'}
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <input value={agencyForm.name} onChange={(e) => setAgencyForm({ ...agencyForm, name: e.target.value })} placeholder="Agency name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                  <input value={agencyForm.tradingName} onChange={(e) => setAgencyForm({ ...agencyForm, tradingName: e.target.value })} placeholder="Trading name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={agencyForm.registrationNumber} onChange={(e) => setAgencyForm({ ...agencyForm, registrationNumber: e.target.value })} placeholder="Registration number" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

                  {/* 🖼️ LOGO: either paste a URL or upload a file (stored as a data-URI on the agency record) */}
                  <div className="flex items-center gap-1">
                    <label className="text-[10px] text-slate-400">Logo</label>
                    <label className={`text-[10px] px-2 py-1 rounded-md border ${agencyForm.companyLogoUrl ? 'bg-slate-950 text-blue-400 border-blue-900' : 'bg-slate-950 text-slate-500 border-slate-700 cursor-pointer hover:text-white'}`}>
                      {agencyForm.companyLogoUrl ? <span>Change</span> : <span>Upload</span>}
                      <input type="file" accept="image/*" hidden onChange={handleLogoUpload} />
                    </label>
                  </div>
                  <input value={agencyForm.companyLogoUrl} onChange={(e) => setAgencyForm({ ...agencyForm, companyLogoUrl: e.target.value })} placeholder="Logo URL" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

                  <input value={agencyForm.contactEmail} onChange={(e) => setAgencyForm({ ...agencyForm, contactEmail: e.target.value })} type="email" placeholder="Contact email" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                  <input value={agencyForm.headquartersAddress} onChange={(e) => setAgencyForm({ ...agencyForm, headquartersAddress: e.target.value })} placeholder="Headquarters address" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />

                  {/* 👤 Auto-provisioned AgencyAdmin login (only required when creating) */}
                  {!editingAgency && (
                    <>
                      <input value={agencyForm.adminFullName} onChange={(e) => setAgencyForm({ ...agencyForm, adminFullName: e.target.value })} placeholder="Admin full name" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                      <input value={agencyForm.adminEmail} onChange={(e) => setAgencyForm({ ...agencyForm, adminEmail: e.target.value })} type="email" placeholder="Admin login email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                      <input value={agencyForm.adminPassword} onChange={(e) => setAgencyForm({ ...agencyForm, adminPassword: e.target.value })} type="password" placeholder="Admin initial password" autoComplete="new-password" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                    </>
                  )}
                </div>
                <button type="submit" className="w-full py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">{editingAgency ? 'Save agency' : 'Provision agency + admin user'}</button>
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
                  <button type="button" onClick={() => { setExpandedAgency(null); setEditingContact(null); setContactForm({ fullName: '', email: '', phone: '', position: '', role: 'Owner', isAgencyAdmin: false }); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <input value={contactForm.fullName} onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })} placeholder="Full name" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
                  <input value={contactForm.email} onChange={(e) => setContactForm({ ...contactForm, email: e.target.value })} type="email" placeholder="Email" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={contactForm.phone} onChange={(e) => setContactForm({ ...contactForm, phone: e.target.value })} placeholder="Phone" className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <input value={contactForm.position} onChange={(e) => setContactForm({ ...contactForm, position: e.target.value })} placeholder="Position" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
                  <select value={contactForm.role} onChange={(e) => setContactForm({ ...contactForm, role: e.target.value })} className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white"><option>Owner</option><option>Tenant</option><option>Manager</option></select>
                  <label className="col-span-2 flex items-center gap-2 text-[10px] text-slate-400"><input type="checkbox" checked={contactForm.isAgencyAdmin} onChange={(e) => setContactForm({ ...contactForm, isAgencyAdmin: e.target.checked })} /> Promote to AgencyAdmin on save</label>
                </div>
                <button type="submit" className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold">{editingContact ? 'Update contact' : 'Create contact' }</button>
              </form>
            )}
          </div>

          {/* 🏢 AGENCY ROSTER (click → contacts) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Building2 size={14} className="text-blue-500" /> Agency roster</div>
            <div className="space-y-3">
              {agencies.map((agency) => (
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
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] text-slate-500">{agency.registrationNumber || 'Reg: —'}</span>
                      <button type="button" onClick={(e) => { e.stopPropagation(); resetAgency(agency); }} className="text-slate-400 hover:text-white"><Pencil size={13} /></button>
                    </div>
                  </div>

                  {/* Expanded contact persons */}
                  {expandedAgency === agency.agencyId && (
                    <div className="px-4 pb-3 border-t border-slate-800 pt-2 space-y-2">
                      <div className="text-[10px] text-slate-400 font-bold uppercase">Contact persons — promote to broker/AgencyAdmin</div>
                      {(agencyContacts[agency.agencyId] || []).map((c) => (
                        <div key={c.contactPersonId} className="flex justify-between items-center rounded-lg border border-slate-800 bg-slate-950/40 p-2.5">
                          <div className="flex-1 truncate">
                            <div className="text-slate-200 text-xs font-semibold">{c.fullName}</div>
                            <div className="text-[10px] text-slate-500">{c.email} • {c.position || '—'}{c.isAgencyAdmin && <Tag size={10} className="ml-1 text-violet-400" />}</div>
                          </div>
                          <div className="flex gap-1.5">
                            <button type="button" onClick={async () => {
                              if (window.confirm(`Promote ${c.fullName} to a broker profile?`)) {
                                try { await crmService.promoteContactToBroker(c.contactPersonId); await loadBrokers(); await loadAgencies(); await loadContacts(agency.agencyId); }
                                catch (err) { alert(err || 'Promotion failed.'); }
                              }
                            }} className="p-1 rounded bg-violet-950 text-violet-300 border border-violet-900 hover:text-white" title="Promote to broker">
                              <ShieldCheck size={11} />
                            </button>
                            <button type="button" onClick={async () => {
                              if (window.confirm('Remove this contact person?')) {
                                try { await crmService.deleteContactPerson(c.contactPersonId); await loadContacts(agency.agencyId); await loadAgencies(); }
                                catch (err) { alert(err || 'Removal failed.'); }
                              }
                            }} className="p-1 rounded bg-red-950/30 text-red-300 border border-red-900 hover:text-white" title="Delete">
                              <Trash2 size={11} />
                            </button>
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
          {/* 👤 BROKER FORM */}
          <form onSubmit={handleBrokerSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><UserPlus size={14} className="text-emerald-500" /> {editingBroker ? 'Edit Broker' : 'Add Broker'}</div>
            <input value={brokerForm.fullName} onChange={(e) => setBrokerForm({ ...brokerForm, fullName: e.target.value })} placeholder="Broker full name" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
            <div className="grid grid-cols-2 gap-3">
              <input value={brokerForm.email} onChange={(e) => setBrokerForm({ ...brokerForm, email: e.target.value })} type="email" placeholder="Email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
              <input value={brokerForm.phone} onChange={(e) => setBrokerForm({ ...brokerForm, phone: e.target.value })} placeholder="Phone" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            </div>
            <select value={brokerForm.agencyId} onChange={(e) => setBrokerForm({ ...brokerForm, agencyId: e.target.value })} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
              {agencies.map((agency) => <option key={agency.agencyId || agency.tenantId} value={agency.agencyId || agency.tenantId}>{agency.tradingName || agency.name}</option>)}
            </select>
            <select value={brokerForm.role} onChange={(e) => setBrokerForm({ ...brokerForm, role: e.target.value })} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
              <option value="Broker">Broker</option>
              <option value="Manager">AgencyAdmin</option>
              <option value="Agent">Agent</option>
            </select>
            <button type="submit" className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold">{editingBroker ? 'Save broker' : 'Create broker profile'}</button>
          </form>

          {/* 🕵️ BROKER ROSTER */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Users size={14} className="text-violet-500" /> Broker roster ({currentUserRole === 'PlatformAdmin' ? 'All agencies' : 'Your agency'})</div>
            <div className="space-y-3">
              {brokers.map((broker) => (
                <div key={broker.brokerId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-3 flex justify-between items-center">
                  <div>
                    <h4 className="text-white font-bold text-sm">{broker.fullName}</h4>
                    <p className="text-xs text-slate-400">{broker.email} • {broker.phone || 'No phone'}</p>
                    <p className="text-[11px] text-slate-500">Agency: {broker.agencyId}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-[10px] px-2 py-1 rounded-md bg-violet-950 text-violet-300 border border-violet-900">{broker.role}</span>
                    <button type="button" onClick={() => { setEditingBroker(broker); setBrokerForm({ fullName: broker.fullName || '', email: broker.email || '', phone: broker.phone || '', agencyId: broker.agencyId || '', role: broker.role || 'Broker' }); }} className="p-2 text-slate-400 hover:text-white"><Pencil size={13} /></button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {activeTab === 'access' && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
            <ShieldCheck size={14} className="text-violet-500" /> User Access ({isSuperAdmin ? 'All tenants' : 'Your tenant'})
          </div>
          {systemUsers.length === 0 ? (
            <p className="text-xs text-slate-500">No user accounts match your scope.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-[11px]">
                <thead>
                  <tr className="text-slate-500 uppercase text-[9px]">
                    <th className="text-left py-1">User</th>
                    <th className="text-left py-1">Email</th>
                    <th className="text-left py-1">Tenant</th>
                    <th className="text-left py-1">Role</th>
                  </tr>
                </thead>
                <tbody>
                  {systemUsers.map((u) => (
                    <tr key={u.userId} className="border-t border-slate-800">
                      <td className="py-1 text-white">{u.fullName}</td>
                      <td className="py-1 text-slate-400">{u.email}</td>
                      <td className="py-1 text-slate-500 font-mono">{u.tenantId}</td>
                      <td className="py-1">
                        <select
                          value={u.systemRole}
                          onChange={async (e) => {
                            const newRole = e.target.value;
                            try {
                              await authService.updateUserRole(u.userId, newRole);
                              setSystemUsers((prev) => prev.map((x) => (x.userId === u.userId ? { ...x, systemRole: newRole } : x)));
                            } catch (err) { alert(err || 'Role reassignment failed.'); }
                          }}
                          className="bg-slate-950 border border-slate-700 rounded-md px-1.5 py-0.5 text-xs text-white focus:outline-none focus:border-blue-500"
                        >
                          {assignableRoles.map((r) => <option key={r} value={r}>{r}</option>)}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="text-[10px] text-slate-500 mt-3">Assign or remove a role by selecting it above. Agency admins may only assign agency-scoped roles (Broker/Owner/Tenant) and only within their own tenant.</p>
        </div>
      )}

      {activeTab === 'roles' && (
        <div className="grid gap-6 xl:grid-cols-2">
          {/* 🛠️ ROLE DEFINITIONS CRUD FORM (PlatformAdmin only) */}
          <form onSubmit={handleRoleSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]">
              <Settings size={14} className="text-violet-500" /> {editingRole ? 'Edit Role Definition' : 'Add Role Definition'}
            </div>
            <input value={roleForm.name} onChange={(e) => setRoleForm({ ...roleForm, name: e.target.value })} placeholder="Role name (e.g. ResellerPartner)" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
            <textarea value={roleForm.description} onChange={(e) => setRoleForm({ ...roleForm, description: e.target.value })} placeholder="Description" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white h-20" />
            <div className="grid grid-cols-2 gap-3">
              <label className="col-span-2 flex items-center gap-2 text-[10px] text-slate-300"><input type="checkbox" checked={roleForm.isPlatformScoped} onChange={(e) => setRoleForm({ ...roleForm, isPlatformScoped: e.target.checked })} disabled={!isSuperAdmin} /> Platform-scoped (assignable by PlatformAdmin)</label>
              <label className="col-span-2 flex items-center gap-2 text-[10px] text-slate-300"><input type="checkbox" checked={roleForm.isAgencyScoped} onChange={(e) => setRoleForm({ ...roleForm, isAgencyScoped: e.target.checked })} disabled={!isSuperAdmin} /> Agency-scoped (assignable by AgencyAdmin)</label>
              <label className="col-span-2 flex items-center gap-2 text-[10px] text-slate-300"><input type="checkbox" checked={roleForm.isActive} onChange={(e) => setRoleForm({ ...roleForm, isActive: e.target.checked })} /> Active</label>
              <input value={roleForm.permissionsJson} onChange={(e) => setRoleForm({ ...roleForm, permissionsJson: e.target.value })} placeholder='Permissions JSON array, e.g. ["CanDeleteProperties"]' className="col-span-2 bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
            </div>
            <div className="flex gap-2">
              <button type="submit" disabled={!isSuperAdmin} className={`py-2.5 rounded-xl text-xs font-bold ${isSuperAdmin ? 'bg-violet-600 text-white' : 'bg-slate-700 text-slate-400 cursor-not-allowed'}`}>{editingRole ? 'Save role' : 'Create role definition'}</button>
              {editingRole && <button type="button" onClick={() => { setEditingRole(null); setRoleForm({ name: '', description: '', isPlatformScoped: false, isAgencyScoped: false, permissionsJson: '[]', isActive: true }); }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300"><CircleX size={12} className="inline mr-1" /> Cancel</button>}
            </div>
          </form>

          {/* 🛠️ ROLE DEFINITIONS TABLE */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Settings size={14} className="text-violet-500" /> Role definitions</div>
            <div className="space-y-3">
              {roles.map((role) => (
                <div key={role.roleDefId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-3 flex justify-between items-center">
                  <div className="flex-1 min-w-0">
                    <h4 className="text-white font-bold text-sm">{role.name}</h4>
                    <p className="text-xs text-slate-400 truncate">{role.description || 'No description'}</p>
                    <div className="flex items-center gap-2 mt-1">
                      {role.isPlatformScoped && <span className="text-[10px] px-2 py-0.5 rounded-md bg-blue-950 text-blue-300 border border-blue-900">Platform admin</span>}
                      {role.isAgencyScoped && <span className="text-[10px] px-2 py-0.5 rounded-md bg-emerald-950 text-emerald-300 border border-emerald-900">Agency admin</span>}
                      <span className={`text-[10px] px-2 py-0.5 rounded-md border ${role.isActive ? 'border-emerald-900 bg-emerald-950/40 text-emerald-300' : 'border-slate-700 bg-slate-800 text-slate-400'}`}>{role.isActive ? 'Active' : 'Inactive'}</span>
                    </div>
                  </div>
                  <div className="flex gap-1.5">
                    <button type="button" onClick={() => resetRole(role)} disabled={!isSuperAdmin} className={`p-2 rounded ${isSuperAdmin ? 'bg-slate-800 text-slate-300 hover:text-white' : 'bg-slate-900 text-slate-600 cursor-not-allowed'}`} title="Edit"><Pencil size={13} /></button>
                    <button type="button" onClick={() => handleRoleDelete(role)} disabled={!isSuperAdmin} className={`p-2 rounded ${isSuperAdmin ? 'bg-red-950/30 text-red-300 border border-red-900 hover:text-white' : 'bg-slate-900 text-slate-600 cursor-not-allowed'}`} title="Deactivate"><Trash2 size={13} /></button>
                  </div>
                </div>
              ))}
              {roles.length === 0 && <div className="text-[11px] text-slate-500">No role definitions loaded.</div>}
            </div>
          </div>
        </div>
      )}

      {activeTab === 'reports' && isSuperAdmin && (
        <div className="space-y-6">
          {reportsError && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{reportsError}</div>}

          {reportsLoading && (
            <div className="text-center py-8 text-slate-500 text-xs">
              <RefreshCw size={16} className="animate-spin inline mb-1" />
              <p>Loading report data…</p>
            </div>
          )}

          {/* 📊 PIPELINE SUMMARY CARDS */}
          {pipelineSummary && (
            <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-blue-400">{pipelineSummary.activeLeadsCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Active Leads</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-amber-400">{pipelineSummary.valuationsInProcessCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Valuations In Process</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-violet-400">{pipelineSummary.conveyancingAtDeedsCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Conveyancing</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-emerald-400">{pipelineSummary.finalizedClosedCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Closed Won</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-sm font-bold text-white">${Number(pipelineSummary.totalPipelineEstimatedValue || 0).toLocaleString()}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Pipeline Value</div>
              </div>
            </div>
          )}

          {/* 📈 MONTHLY TRENDS + COMMISSION BY STAGE (side by side on desktop) */}
          <div className="grid gap-6 lg:grid-cols-2">
            {reportOverview && (
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                  <TrendingUp size={14} className="text-blue-400" /> Monthly Trends
                </div>
                <div className="grid grid-cols-3 gap-3 mb-4 text-center">
                  <div><div className="text-xl font-bold text-white">{reportOverview.totalLeads}</div><div className="text-[10px] text-slate-500">Leads</div></div>
                  <div><div className="text-xl font-bold text-white">{reportOverview.totalDeals}</div><div className="text-[10px] text-slate-500">Deals</div></div>
                  <div><div className="text-sm font-bold text-emerald-400">${Number(reportOverview.totalRevenue || 0).toLocaleString()}</div><div className="text-[10px] text-slate-500">Revenue</div></div>
                </div>
                <ResponsiveContainer width="100%" height={200}>
                  <LineChart data={reportOverview.monthlyBreakdown || []}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis dataKey="period" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3a8' }} />
                    <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3a8' }} />
                    <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', borderRadius: '8px', fontSize: '11px' }} />
                    <Legend wrapperStyle={{ fontSize: '10px', color: '#94a3a8' }} />
                    <Line type="monotone" dataKey="leads" stroke="#38bdf8" strokeWidth={2} name="Leads" />
                    <Line type="monotone" dataKey="deals" stroke="#a78bfa" strokeWidth={2} name="Deals" />
                    <Line type="monotone" dataKey="revenue" stroke="#4ade80" strokeWidth={2} name="Revenue" />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}

            {commissionByStage.length > 0 && (
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                  <DollarSign size={14} className="text-emerald-400" /> Commission by Stage
                </div>
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={commissionByStage} layout="vertical" margin={{ top: 5, right: 20, left: 80, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis type="number" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3a8' }} />
                    <YAxis type="category" dataKey="period" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3a8' }} />
                    <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', borderRadius: '8px', fontSize: '11px' }} />
                    <Bar dataKey="revenue" fill="#4ade80" name="Commission" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </div>

          {/* 🏆 BROKER LEADERBOARD */}
          {brokerLeaderboard.length > 0 && (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
              <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                <Trophy size={14} className="text-amber-400" /> Broker Leaderboard
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-[11px]">
                  <thead>
                    <tr className="text-slate-500 uppercase text-[9px]">
                      <th className="text-left py-1">#</th>
                      <th className="text-left py-1">Broker</th>
                      <th className="text-right py-1">Deals</th>
                      <th className="text-right py-1">Pipeline Value</th>
                      <th className="text-right py-1">Commission</th>
                      <th className="text-right py-1">Won</th>
                    </tr>
                  </thead>
                  <tbody>
                    {brokerLeaderboard.map((b, idx) => (
                      <tr key={b.agent || idx} className="border-t border-slate-800">
                        <td className="py-1 text-slate-400">{idx + 1}</td>
                        <td className="py-1 text-white">{b.agent}</td>
                        <td className="py-1 text-right text-slate-300">{b.deals}</td>
                        <td className="py-1 text-right text-slate-300">${Number(b.totalValue || 0).toLocaleString()}</td>
                        <td className="py-1 text-right text-emerald-400">${Number(b.totalCommission || 0).toLocaleString()}</td>
                        <td className="py-1 text-right text-slate-300">{b.won}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
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

              <button
                type="submit"
                className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold hover:bg-emerald-500 transition-colors"
              >
                {editingLead ? 'Update Lead' : 'Create Lead'}
              </button>
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
                      <button
                        type="button"
                        onClick={() => resetLead(lead)}
                        className="p-1.5 rounded-lg bg-slate-800 text-slate-400 hover:text-white"
                        title="Edit lead"
                      >
                        <Pencil size={12} />
                      </button>
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
    </div>
  );
}
