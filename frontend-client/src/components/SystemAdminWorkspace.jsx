import React, { useEffect, useState } from 'react';
import { Building2, UserPlus, Users, Plus, Pencil, Trash2, ShieldCheck, Tag, ChevronRight, X, Settings, Save, CircleX } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { systemRolesService } from '../services/systemRolesService.js';
import { authService } from '../services/authService.js';

export default function SystemAdminWorkspace({ currentUserRole }) {
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

  useEffect(() => { loadData(); }, []);
  const loadData = async () => { await Promise.all([loadAgencies(), loadBrokers(), loadUsers(), isSuperAdmin && loadRoles()]); };

  const isSuperAdmin = currentUserRole === 'PlatformAdmin';
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
    </div>
  );
}
