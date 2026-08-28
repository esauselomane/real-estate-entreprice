import React, { useState, useEffect } from 'react';
import { Briefcase, DollarSign, TrendingUp, UserPlus, Filter, User, CheckCircle, Loader2, Layers, Sparkles, Building2, Edit2 } from 'lucide-react';
import { crmService } from '../services/ crmService.js';

export default function CRMWorkspace({ currentUserRole }) {
  // Navigation active control dashboard sub-tabs
  const [activeTab, setActiveTab] = useState('leads'); // leads, deals, companies, legalEntities, contacts
  
  // Data array storage grids
  const [unverifiedLeads, setUnverifiedLeads] = useState([]);
  const [verifiedDeals, setVerifiedDeals] = useState([]);
  const [companies, setCompanies] = useState([]);
  const [legalEntities, setLegalEntities] = useState([]);
  const [contacts, setContacts] = useState([]);
  
  const [summaryReport, setSummaryReport] = useState({
    activeLeadsCount: 0, valuationsInProcessCount: 0, conveyancingAtDeedsCount: 0, finalizedClosedCount: 0, totalPipelineEstimatedValue: 0
  });

  // Dynamic Edit state anchors mapping selection objects
  const [editingCompany, setEditingCompany] = useState(null);
  const [editingLegalEntity, setEditingLegalEntity] = useState(null);
  const [editingContact, setEditingContact] = useState(null);
  const [selectedLeadForEdit, setSelectedLeadForEdit] = useState(null);
  const [selectedDealForEdit, setSelectedDealForEdit] = useState(null);

  // Common Modal form bindings
  const [nextStageValue, setNextStageValue] = useState('');
  const [notesUpdateText, setNotesUpdateText] = useState('');
  const [linkedPropertyInputId, setLinkedPropertyInputId] = useState('');
  const [sellerProfileInputName, setSellerProfileInputName] = useState('');

  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  // 🔄 REFRESH PIPELINE: Synchronizes elements matching selected tab view scopes
  const loadActiveWorkspaceData = async () => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const metrics = await crmService.getPipelineReportSummary();
      setSummaryReport(metrics);

      if (activeTab === 'leads' || activeTab === 'deals') {
        const boards = await crmService.getCRMBoards();
        setUnverifiedLeads(boards.unverifiedLeads || []);
        setVerifiedDeals(boards.verifiedDeals || []);
      } else if (activeTab === 'companies') {
        const res = await crmService.getCompanies();
        setCompanies(res || []);
      } else if (activeTab === 'legalEntities') {
        const res = await crmService.getLegalEntities();
        setLegalEntities(res || []);
      } else if (activeTab === 'contacts') {
        const res = await crmService.getContacts();
        setContacts(res || []);
      }
    } catch (err) {
      setErrorMessage(err || 'Failed to populate operational ledger streams.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadActiveWorkspaceData();
  }, [activeTab]);
  // --- 💾 RELATIONAL RECORD MODIFICATION SUBMISSION HANDLERS ---
  const handleUpdateCompanySubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateCompany(editingCompany);
      setEditingCompany(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply company updates.');
    }
  };

  const handleUpdateLegalEntitySubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateLegalEntity(editingLegalEntity);
      setEditingLegalEntity(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply legal entity updates.');
    }
  };

  const handleUpdateContactSubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateContact(editingContact);
      setEditingContact(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply contact profile updates.');
    }
  };

  const handleUpdateLeadAction = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateLeadStage(
        selectedLeadForEdit.leadId, nextStageValue, notesUpdateText, linkedPropertyInputId, sellerProfileInputName
      );
      setSelectedLeadForEdit(null);
      setNotesUpdateText('');
      setLinkedPropertyInputId('');
      setSellerProfileInputName('');
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply lead stage updates.');
    }
  };

  const handleUpdateDealAction = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateDealStage(selectedDealForEdit.dealId, nextStageValue, notesUpdateText);
      setSelectedDealForEdit(null);
      setNotesUpdateText('');
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply deal stage updates.');
    }
  };

  const handleReallocateAgent = async (targetId, isDeal, targetAgent) => {
    try {
      await crmService.reallocateTarget(targetId, isDeal, targetAgent);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Allocation update failed.');
    }
  };

  const formatEnumTitle = (enumString) => {
    if (!enumString) return '';
    return String(enumString || '').replace(/([A-Z])/g, " $1").replace(/^./, str => str.toUpperCase());
  };
  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 📊 TOP ANALYTICS DASHBOARD SUMMARY METRIC CARDS */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Unverified Leads</span>
            <span className="text-2xl font-black text-white mt-1 block">{summaryReport.activeLeadsCount} Active</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-blue-950 border border-blue-900 flex items-center justify-center text-blue-400">
            <UserPlus size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Active Deals</span>
            <span className="text-2xl font-black text-amber-400 mt-1 block">{summaryReport.valuationsInProcessCount} Open</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-amber-950/40 border border-amber-900 flex items-center justify-center text-amber-400">
            <TrendingUp size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">In Negotiation</span>
            <span className="text-2xl font-black text-purple-400 mt-1 block">{summaryReport.conveyancingAtDeedsCount} Reviewed</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-purple-950/40 border border-purple-900 flex items-center justify-center text-purple-400">
            <Briefcase size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Closed Won Value</span>
            <span className="text-2xl font-black text-emerald-400 mt-1 block">R {summaryReport.totalPipelineEstimatedValue.toLocaleString()}</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-emerald-950/40 border border-emerald-900 flex items-center justify-center text-emerald-400">
            <DollarSign size={18} />
          </div>
        </div>
      </div>

      {/* 🎛️ ENTERPRISE FIVE-WAY NAVIGATIONAL WORKSPACE SWITCHER */}
      <div className="flex bg-slate-900 border border-slate-800 p-1.5 rounded-2xl gap-1.5 max-w-4xl shadow-xl overflow-x-auto scrollbar-none">
        {[
          { id: 'leads', label: 'Unverified Leads', count: unverifiedLeads.length },
          { id: 'deals', label: 'Corporate Deals', count: verifiedDeals.length },
          { id: 'companies', label: 'Companies Directory', count: companies.length },
          { id: 'legalEntities', label: 'Legal Entities', count: legalEntities.length },
          { id: 'contacts', label: 'Human Contacts', count: contacts.length }
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer whitespace-nowrap ${
              activeTab === tab.id ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            {tab.label} {tab.count > 0 && `(${tab.count})`}
          </button>
        ))}
      </div>
      {errorMessage && (
        <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">
          {errorMessage}
        </div>
      )}

      {/* ⏳ ASYNC LOADING DATA SPINNER OVERLAY MASK */}
      {isLoading ? (
        <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
          <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
          <span className="text-xs font-medium tracking-wide">Syncing real-time corporate relationship tables...</span>
        </div>
      ) : (
        <div className="space-y-4">
          
          {/* 🎯 RENDERING PIPELINE VIEW 1: UNVERIFIED LEADS */}
          {activeTab === 'leads' && unverifiedLeads.map((lead) => (
            <div key={lead.leadId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col lg:flex-row justify-between lg:items-center gap-4 hover:border-slate-700 transition-colors">
              <div className="space-y-2 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border bg-blue-950 text-blue-400 border-blue-900">
                    Lead: {formatEnumTitle(lead.leadStage)}
                  </span>
                  <span className="text-xs text-slate-500 font-mono">Index: LD-{lead.leadId}</span>
                </div>
                <div>
                  <h4 className="text-white font-bold text-base tracking-tight">{lead.clientName}</h4>
                  <p className="text-xs text-slate-400 font-medium">Email: {lead.contactEmail} • Phone: {lead.contactPhone}</p>
                </div>
              </div>
              <button onClick={() => { setSelectedLeadForEdit(lead); setNextStageValue(lead.leadStage); }} className="bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Update Stage</button>
            </div>
          ))}

          {/* 💼 RENDERING PIPELINE VIEW 2: VERIFIED CORPORATE DEALS */}
          {activeTab === 'deals' && verifiedDeals.map((deal) => (
            <div key={deal.dealId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col lg:flex-row justify-between lg:items-center gap-4 hover:border-slate-700 transition-colors">
              <div className="space-y-2 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border ${
                    deal.dealStage === 'ClosedWon' ? 'bg-emerald-950 text-emerald-400 border-emerald-900' :
                    deal.dealStage === 'ClosedLost' ? 'bg-red-950 text-red-400 border-red-900' :
                    'bg-amber-950 text-amber-400 border-amber-900'
                  }`}>
                    Deal: {formatEnumTitle(deal.dealStage)}
                  </span>
                  <span className="text-xs text-slate-500 font-mono">Reference: DL-{deal.dealId}</span>
                </div>
                <p className="text-xs text-slate-400 font-medium">Buyer: <span className="text-blue-400 font-semibold">{deal.buyerClientId}</span> • Seller: <span className="text-purple-400 font-semibold">{deal.sellerClientId}</span></p>
              </div>
              <button onClick={() => { setSelectedDealForEdit(deal); setNextStageValue(deal.dealStage); }} className="bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Progress Deal</button>
            </div>
          ))}

          {/* 🏢 RENDERING PIPELINE VIEW 3: COMPANIES REGISTRY DIRECTORY */}
          {activeTab === 'companies' && companies.map((comp) => (
            <div key={comp.companyId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight flex items-center gap-2"><Building2 size={16} className="text-slate-500" /> {comp.businessName}</h4>
                <p className="text-xs text-slate-400 mt-1">Reg: {comp.registrationNumber || 'N/A'} • VAT: {comp.vatNumber || 'N/A'} • Credit Limit: R {comp.corporateCreditLimit.toLocaleString()}</p>
                <p className="text-[11px] text-slate-500 mt-0.5">HQ: {comp.headOfficeAddress || 'Not Provided'}</p>
              </div>
              <button onClick={() => setEditingCompany(comp)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
            </div>
          ))}
          {/* ⚖️ RENDERING PIPELINE VIEW 4: CONTRACTUAL LEGAL ENTITIES */}
          {activeTab === 'legalEntities' && legalEntities.map((ent) => (
            <div key={ent.legalEntityId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight">{ent.registeredLegalName}</h4>
                <p className="text-xs text-slate-400 mt-1">Structure: <span className="text-blue-400">{ent.entityTypeClassifier}</span> • Tax Ref: {ent.taxRegistrationIndex || 'N/A'} • Deeds Reg: {ent.deedsOfficeIdentifier || 'N/A'}</p>
                {ent.companyDetails && <p className="text-[11px] text-slate-500 mt-0.5">Belongs To Company: <span className="text-slate-400 font-semibold">{ent.companyDetails.businessName}</span></p>}
              </div>
              <button onClick={() => setEditingLegalEntity(ent)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
            </div>
          ))}

          {/* 👤 RENDERING PIPELINE VIEW 5: HUMAN CONTACTS DIRECTORY */}
          {activeTab === 'contacts' && contacts.map((con) => (
            <div key={con.contactId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight">{con.fullName}</h4>
                <p className="text-xs text-slate-400 mt-1">Role: <span className="text-purple-400 font-bold">{con.contactRoleType}</span> ({con.positionJobTitle}) • Email: {con.emailAddress} • Phone: {con.phoneNumber}</p>
                {(con.companyDetails || con.legalEntityDetails) && (
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    Affiliation: {con.companyDetails?.businessName || ''} {con.legalEntityDetails?.registeredLegalName ? `[Entity: ${con.legalEntityDetails.registeredLegalName}]` : ''}
                  </p>
                )}
              </div>
              <button onClick={() => setEditingContact(con)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
            </div>
          ))}

          {((activeTab === 'leads' && unverifiedLeads.length === 0) || 
            (activeTab === 'deals' && verifiedDeals.length === 0) ||
            (activeTab === 'companies' && companies.length === 0) ||
            (activeTab === 'legalEntities' && legalEntities.length === 0) ||
            (activeTab === 'contacts' && contacts.length === 0)) && !errorMessage && (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
              No active records found matching this pipeline layout screen state.
            </div>
          )}
        </div>
      )}

      {/* 🏢 MODAL CONTAINER A: EDIT COMPANY DETAILS */}
      {editingCompany && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-md w-full space-y-4 shadow-2xl">
            <h3 className="text-base font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-2 flex items-center gap-1.5">
              <Building2 size={16} className="text-blue-500" /> Modify Corporate Company Profile
            </h3>
            <form onSubmit={handleUpdateCompanySubmit} className="space-y-3">
              <div>
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Business Operating Name</label>
                <input 
                  type="text" required value={editingCompany.businessName || ''} 
                  onChange={(e) => setEditingCompany({ ...editingCompany, businessName: e.target.value })} 
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Registration No</label>
                  <input 
                    type="text" value={editingCompany.registrationNumber || ''} 
                    onChange={(e) => setEditingCompany({ ...editingCompany, registrationNumber: e.target.value })} 
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                  />
                </div>
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">VAT Number</label>
                  <input 
                    type="text" value={editingCompany.vatNumber || ''} 
                    onChange={(e) => setEditingCompany({ ...editingCompany, vatNumber: e.target.value })} 
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                  />
                </div>
              </div>
              <div>
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Head Office Address Location</label>
                <input 
                  type="text" value={editingCompany.headOfficeAddress || ''} 
                  onChange={(e) => setEditingCompany({ ...editingCompany, headOfficeAddress: e.target.value })} 
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Credit Limit Boundary</label>
                  <input 
                    type="number" value={editingCompany.corporateCreditLimit || 0} 
                    onChange={(e) => setEditingCompany({ ...editingCompany, corporateCreditLimit: parseFloat(e.target.value) || 0 })} 
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                  />
                </div>
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Industry Vertical</label>
                  <input 
                    type="text" value={editingCompany.industryVertical || ''} 
                    onChange={(e) => setEditingCompany({ ...editingCompany, industryVertical: e.target.value })} 
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors" 
                  />
                </div>
              </div>
              <div className="flex gap-3 pt-3">
                <button type="button" onClick={() => setEditingCompany(null)} className="flex-1 py-2 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">Cancel</button>
                <button type="submit" className="flex-1 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-xl cursor-pointer">Save Changes</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 🎯 MODAL CONTAINER D: UPDATE LEAD STAGE & AUTO-PROMOTION SYSTEM DIALOG */}
      {selectedLeadForEdit && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-md w-full space-y-4 shadow-2xl">
            <h3 className="text-base font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-2 flex items-center gap-1.5">
              <Sparkles size={16} className="text-blue-500" /> Update Lead Workflow Stage
            </h3>
            <form onSubmit={handleUpdateLeadAction} className="space-y-4">
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Target Phase Milestone</label>
                <select 
                  value={nextStageValue} 
                  onChange={(e) => setNextStageValue(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl p-2.5 text-xs text-white focus:outline-none cursor-pointer"
                >
                  <option value="Contacted">Contacted</option>
                  <option value="AttemptedContact">Attempted Contact</option>
                  <option value="Nurturing">Nurturing Case</option>
                  <option value="Disqualified">Disqualified / Lost</option>
                  <option value="Qualified">Qualified (Promote to Formal Deal) 🚀</option>
                </select>
              </div>

              {nextStageValue === 'Qualified' && (
                <div className="p-3 bg-blue-950/20 border border-blue-900/60 rounded-xl space-y-3 animate-fade-in">
                  <span className="text-[10px] text-blue-400 font-bold uppercase tracking-wider block">Auto-Promotion Pipeline Engine Fields</span>
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Link Property ID (Optional)</label>
                    <input type="number" placeholder="e.g. 1" value={linkedPropertyInputId} onChange={(e) => setLinkedPropertyInputId(e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none" />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Seller / Property Owner Legal Name</label>
                    <input type="text" placeholder="e.g. Arthur Pendelton" required value={sellerProfileInputName} onChange={(e) => setSellerProfileInputName(e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-lg text-xs text-white focus:outline-none" />
                  </div>
                </div>
              )}
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Append Action Progress Note</label>
                <textarea rows={3} placeholder="Provide latest discussion logs..." value={notesUpdateText} onChange={(e) => setNotesUpdateText(e.target.value)} className="w-full p-3 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
              </div>

              <div className="flex gap-3 pt-2">
                <button type="button" onClick={() => setSelectedLeadForEdit(null)} className="flex-1 py-2.5 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 transition-colors cursor-pointer">Cancel</button>
                <button type="submit" className="flex-1 py-2.5 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-xl shadow-md transition-colors cursor-pointer">Commit Status Changes</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 💼 MODAL CONTAINER E: PROGRESS VERIFIED CORPORATE DEAL LIFE CYCLE */}
      {selectedDealForEdit && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-md w-full space-y-4 shadow-2xl">
            <h3 className="text-base font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-2 flex items-center gap-1.5">
              <Briefcase size={16} className="text-amber-500" /> Progress Enterprise Sales Pipeline
            </h3>
            <form onSubmit={handleUpdateDealAction} className="space-y-4">
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Target Corporate Stage</label>
                <select 
                  value={nextStageValue} 
                  onChange={(e) => setNextStageValue(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-700 rounded-xl p-2.5 text-xs text-white focus:outline-none cursor-pointer"
                >
                  <option value="DiscoveryNeedsAnalysis">Discovery / Needs Analysis</option>
                  <option value="Presentation">Presentation Phase</option>
                  <option value="ProposalQuoteSent">Proposal / Quote Sent</option>
                  <option value="Negotiation">Negotiation Desk</option>
                  <option value="ClosedWon">Closed Won (Success) 🎉</option>
                  <option value="ClosedLost">Closed Lost (Dropped) ❌</option>
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Append Stage Progress Notes</label>
                <textarea rows={3} placeholder="Provide agreement changes or objective adjustments log entry..." value={notesUpdateText} onChange={(e) => setNotesUpdateText(e.target.value)} className="w-full p-3 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-amber-500" />
              </div>

              <div className="flex gap-3 pt-2">
                <button type="button" onClick={() => setSelectedDealForEdit(null)} className="flex-1 py-2.5 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 transition-colors cursor-pointer">Cancel</button>
                <button type="submit" className="flex-1 py-2.5 bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold rounded-xl shadow-md transition-colors cursor-pointer">Update Deal Stage</button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
}
