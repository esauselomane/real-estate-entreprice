import React, { useState } from 'react';
import { Building2, Users, Shield, Plus, Key, ToggleLeft, ToggleRight, CheckCircle, Mail, MapPin } from 'lucide-react';

export default function SuperAdminBoard() {
  // Navigation tabs state tracking
  const [activeTab, setActiveTab] = useState('agencies'); // Options: 'agencies', 'users'

  // --- 🏢 SECTION 1: AGENCY ONBOARDING DATABASE PROFILE STORE ---
  const [agencies, setAgencies] = useState([
    { id: "TEN-001", name: "Apex Luxury Realty", initials: "ALR", contact: "admin@apex.com", address: "105 Rivonia Rd, Sandton", status: "Active" },
    { id: "TEN-002", name: "Coastal Living Properties", initials: "CLP", contact: "info@coastal.co.za", address: "42 Beach Rd, Green Point", status: "Active" }
  ]);

  // Form states for creating a new tenant agency profile
  const [newAgencyName, setNewAgencyName] = useState('');
  const [newAgencyInitials, setNewAgencyInitials] = useState('');
  const [newAgencyContact, setNewAgencyContact] = useState('');
  const [newAgencyAddress, setNewAgencyAddress] = useState('');

  const handleOnboardAgency = (e) => {
    e.preventDefault();
    if (!newAgencyName || !newAgencyContact) return;

    const newTenant = {
      id: `TEN-00${agencies.length + 1}`,
      name: newAgencyName,
      initials: newAgencyInitials.toUpperCase() || newAgencyName.substring(0, 3).toUpperCase(),
      contact: newAgencyContact,
      address: newAgencyAddress || "Not Provided",
      status: "Active"
    };

    setAgencies([...agencies, newTenant]);
    // Clear dynamic input fields
    setNewAgencyName('');
    setNewAgencyInitials('');
    setNewAgencyContact('');
    setNewAgencyAddress('');
    alert(`Multi-tenant provisioning engine triggered! New agency infrastructure isolation container successfully allocated for: ${newTenant.name}`);
  };
  // --- 🔐 SECTION 2: IDENTITY PROFILE & SECURITY CLAIMS DATA STORE ---
  const [systemUsers, setSystemUsers] = useState([
    { 
      id: "USR-881", 
      name: "Sarah Jenkins", 
      email: "sarah@apex.com", 
      agencyId: "TEN-001", 
      role: "Agent", 
      status: "Active",
      avatar: "https://unsplash.com",
      claims: { canDelete: false, canExportReports: true, canBypassValuation: false }
    },
    { 
      id: "USR-882", 
      name: "Johnathan Ross", 
      email: "john@coastal.co.za", 
      agencyId: "TEN-002", 
      role: "Manager", 
      status: "Active",
      avatar: "https://unsplash.com",
      claims: { canDelete: true, canExportReports: true, canBypassValuation: true }
    }
  ]);

  // Form inputs tracking for provisioning a new real estate professional user
  const [newUserName, setNewUserName] = useState('');
  const [newUserEmail, setNewUserEmail] = useState('');
  const [newUserAgency, setNewUserAgency] = useState('TEN-001');
  const [newUserRole, setNewUserRole] = useState('Agent');
  const [newUserAvatar, setNewUserAvatar] = useState('');

  const handleRegisterUser = (e) => {
    e.preventDefault();
    if (!newUserName || !newUserEmail) return;

    const newUserRecord = {
      id: `USR-${Math.floor(100 + Math.random() * 900)}`,
      name: newUserName,
      email: newUserEmail,
      agencyId: newUserAgency,
      role: newUserRole,
      status: "Active",
      avatar: newUserAvatar || "https://unsplash.com", // Safe default
      claims: { canDelete: newUserRole === 'Manager', canExportReports: true, canBypassValuation: false }
    };

    setSystemUsers([...systemUsers, newUserRecord]);
    setNewUserName('');
    setNewUserEmail('');
    setNewUserAvatar('');
    alert(`Identity core registration processed! User ${newUserRecord.name} bound to Agency ${newUserRecord.agencyId}. Temporary encrypted login credentials generated.`);
  };

  const toggleUserStatus = (userId) => {
    setSystemUsers(systemUsers.map(u => {
      if (u.id === userId) {
        const nextStatus = u.status === 'Active' ? 'De-registered' : 'Active';
        alert(`User account access vectors altered! Status updated to ${nextStatus}.`);
        return { ...u, status: nextStatus };
      }
      return u;
    }));
  };

  const toggleClaimPermission = (userId, claimKey) => {
    setSystemUsers(systemUsers.map(u => {
      if (u.id === userId) {
        const activeClaims = { ...u.claims, [claimKey]: !u.claims[claimKey] };
        return { ...u, claims: activeClaims };
      }
      return u;
    }));
  };

  const resetUserPassword = (userEmail) => {
    alert(`Outbound transactional email broadcast dispatched to [${userEmail}] containing unique automated self-service recovery key.`);
  };
  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 🎛️ ADMINISTRATIVE TOOLBAR MANAGEMENT TABS */}
      <div className="flex bg-slate-900 border border-slate-800 p-1.5 rounded-2xl gap-2 max-w-sm shadow-xl">
        <button
          onClick={() => setActiveTab('agencies')}
          className={`flex-1 flex items-center justify-center gap-2 text-xs font-bold py-2.5 rounded-xl transition-all cursor-pointer ${
            activeTab === 'agencies' ? 'bg-blue-600 text-white shadow-md' : 'text-slate-400 hover:text-white'
          }`}
        >
          <Building2 size={14} /> Agency Onboarding
        </button>
        <button
          onClick={() => setActiveTab('users')}
          className={`flex-1 flex items-center justify-center gap-2 text-xs font-bold py-2.5 rounded-xl transition-all cursor-pointer ${
            activeTab === 'users' ? 'bg-blue-600 text-white shadow-md' : 'text-slate-400 hover:text-white'
          }`}
        >
          <Users size={14} /> User Governance
        </button>
      </div>

      {/* 🏢 VIEW CONTAINER A: AGENCY ONBOARDING WORKFLOW */}
      {activeTab === 'agencies' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* Dynamic Registration Entry Form */}
          <form onSubmit={handleOnboardAgency} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl h-fit space-y-4">
            <h3 className="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2 border-b border-slate-800 pb-3">
              <Plus size={16} className="text-blue-500" /> Register Corporate Agency
            </h3>
            
            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Trading Corporate Name</label>
              <input type="text" required value={newAgencyName} onChange={(e) => setNewAgencyName(e.target.value)} placeholder="Elite Real Estate" className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Logo Initials</label>
                <input type="text" maxLength={4} value={newAgencyInitials} onChange={(e) => setNewAgencyInitials(e.target.value)} placeholder="ERE" className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
              </div>
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Primary Email Contact</label>
                <input type="email" required value={newAgencyContact} onChange={(e) => setNewAgencyContact(e.target.value)} placeholder="ops@elite.com" className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Headquarters Address</label>
              <input type="text" value={newAgencyAddress} onChange={(e) => setNewAgencyAddress(e.target.value)} placeholder="Cape Town HQ office..." className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
            </div>

            <button type="submit" className="w-full py-2.5 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-xl text-xs shadow-lg transition-colors cursor-pointer mt-2">
              Provision Tenant Workspace
            </button>
          </form>

          {/* Provisioned Tenant Output List Matrix */}
          <div className="lg:col-span-2 space-y-3">
            {agencies.map((agency) => (
              <div key={agency.id} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between gap-4">
                <div className="flex items-center gap-4">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-600 flex items-center justify-center font-black text-white text-xs shadow-md">
                    {agency.initials}
                  </div>
                  <div>
                    <h4 className="text-white font-bold text-base tracking-tight">{agency.name}</h4>
                    <div className="flex items-center gap-4 text-slate-500 text-xs mt-1 font-medium">
                      <span className="flex items-center gap-1"><Mail size={12} /> {agency.contact}</span>
                      <span className="flex items-center gap-1"><MapPin size={12} /> {agency.address}</span>
                    </div>
                  </div>
                </div>
                <div className="text-right">
                  <span className="text-[10px] font-mono text-slate-500 block">{agency.id}</span>
                  <span className="inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider bg-emerald-950 text-emerald-400 border border-emerald-900 px-2 py-0.5 rounded-md mt-1">
                    <CheckCircle size={10} /> {agency.status}
                  </span>
                </div>
              </div>
            ))}
          </div>

        </div>
      )}
      {/* 🔐 VIEW CONTAINER B: USER GOVERNANCE & SECURITY CLAIMS ACCESS SETS */}
      {activeTab === 'users' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* User Provisioning Form with Profile Pic Handler */}
          <form onSubmit={handleRegisterUser} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl h-fit space-y-4">
            <h3 className="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2 border-b border-slate-800 pb-3">
              <Plus size={16} className="text-blue-500" /> Provision Real Estate User
            </h3>

            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Full Legal Name</label>
              <input type="text" required value={newUserName} onChange={(e) => setNewUserName(e.target.value)} placeholder="Michael Scott" className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
            </div>

            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Corporate Email Pointer</label>
              <input type="email" required value={newUserEmail} onChange={(e) => setNewUserEmail(e.target.value)} placeholder="michael@apex.com" className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
            </div>

            <div className="space-y-1">
              <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Profile Picture URL</label>
              <input type="text" value={newUserAvatar} onChange={(e) => setNewUserAvatar(e.target.value)} placeholder="Paste portrait unsplash URL..." className="w-full px-3 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Bind to Agency Tenant</label>
                <select value={newUserAgency} onChange={(e) => setNewUserAgency(e.target.value)} className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none">
                  {agencies.map(a => <option key={a.id} value={a.id}>{a.initials}</option>)}
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Primary System Role</label>
                <select value={newUserRole} onChange={(e) => setNewUserRole(e.target.value)} className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none">
                  <option value="Agent">Agent Scope</option>
                  <option value="Manager">Manager Priv</option>
                </select>
              </div>
            </div>

            <button type="submit" className="w-full py-2.5 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-xl text-xs shadow-lg transition-colors cursor-pointer mt-2">
              Commit Identity Profile Records
            </button>
          </form>
          {/* User Claims Grid Listing & Management Options Panel */}
          <div className="lg:col-span-2 space-y-4">
            {systemUsers.map((user) => (
              <div key={user.id} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
                
                {/* Profile Header Details Layout Area with Professional Rounded Portrait */}
                <div className="flex items-center justify-between gap-4 border-b border-slate-800/80 pb-3">
                  <div className="flex items-center gap-3">
                    <img src={user.avatar} alt="User structural thumbnail representation" className="w-11 h-11 rounded-full border-2 border-slate-700 object-cover shadow-inner bg-slate-950" />
                    <div>
                      <h4 className="text-white font-bold text-base tracking-tight">{user.name}</h4>
                      <p className="text-xs text-slate-500 font-medium">{user.email} • Bound: <span className="text-slate-300 font-semibold">{user.agencyId}</span></p>
                    </div>
                  </div>
                  
                  <div className="flex items-center gap-2">
                    <button 
                      onClick={() => resetUserPassword(user.email)}
                      title="Self-service email recovery link command trigger"
                      type="button"
                      className="p-2 bg-slate-950 text-slate-400 hover:text-blue-400 border border-slate-800 hover:border-slate-700 rounded-xl cursor-pointer transition-colors"
                    >
                      <Key size={14} />
                    </button>
                    <button 
                      onClick={() => toggleUserStatus(user.id)}
                      type="button"
                      className={`text-[10px] font-bold uppercase px-2.5 py-1.5 rounded-xl border cursor-pointer transition-all ${
                        user.status === 'Active' 
                          ? 'bg-slate-950 hover:bg-red-950/20 text-emerald-400 border-slate-800 hover:border-red-900/60' 
                          : 'bg-red-950/30 text-red-400 border-red-900'
                      }`}
                    >
                      {user.status === 'Active' ? 'De-register User' : 'Restore Access'}
                    </button>
                  </div>
                </div>

                {/* 🔒 Dynamic System Claims Security Block Checklist */}
                <div className="bg-slate-950/50 p-3 rounded-xl border border-slate-800/60 space-y-2">
                  <span className="text-[10px] text-slate-500 font-bold uppercase tracking-widest flex items-center gap-1 mb-1">
                    <Shield size={10} className="text-blue-500" /> RBAC Specific Granular Claims Permissions
                  </span>
                  
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-1">
                    {Object.keys(user.claims).map((claimKey) => (
                      <div 
                        key={claimKey}
                        onClick={() => toggleClaimPermission(user.id, claimKey)}
                        className="flex items-center justify-between p-2 bg-slate-900 border border-slate-800/80 rounded-lg cursor-pointer hover:border-slate-700 transition-colors select-none"
                      >
                        <span className="text-[11px] font-mono text-slate-400 truncate uppercase">
                          {claimKey.replace('can', '')}
                        </span>
                        <button type="button" className="text-slate-500 transition-colors">
                          {user.claims[claimKey] 
                            ? <ToggleRight size={20} className="text-blue-500" /> 
                            : <ToggleLeft size={20} className="text-slate-600" />
                          }
                        </button>
                      </div>
                    ))}
                  </div>
                </div>

              </div>
            ))}
          </div>

        </div>
      )}

    </div>
  );
}
