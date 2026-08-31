import { useState, useEffect, useRef } from 'react';
import { Briefcase, DollarSign, TrendingUp, UserPlus, Filter, User, CheckCircle, Loader2, Layers, Sparkles, Building2, Edit2, Search } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';

export default function CRMWorkspace() {
  const { userRole: currentUserRole } = useAuth();
  const { setBrochureDealTarget: onOpenBrochureBuilder } = useAppUi();
  // Navigation active control dashboard sub-tabs
  const [activeTab, setActiveTab] = useState('leads'); // leads, deals, companies, legalEntities, contacts

  // Data array storage grids
  const [unverifiedLeads, setUnverifiedLeads] = useState([]);
  const [verifiedDeals, setVerifiedDeals] = useState([]);
  const [companies, setCompanies] = useState([]);
  const [legalEntities, setLegalEntities] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [brokers, setBrokers] = useState([]);

  // Broker brochure cards currently in flight — drives the notification badge deck.
  const [activeCards, setActiveCards] = useState([]);
  const activeCardDealIds = new Set(activeCards.map((c) => c.dealId));

  const [summaryReport, setSummaryReport] = useState({
    activeLeadsCount: 0, valuationsInProcessCount: 0, conveyancingAtDeedsCount: 0, finalizedClosedCount: 0, totalPipelineEstimatedValue: 0
  });

  // Dynamic Edit state anchors mapping selection objects
  const [editingCompany, setEditingCompany] = useState(null);
  const [editingLegalEntity, setEditingLegalEntity] = useState(null);
  const [editingContact, setEditingContact] = useState(null);
  const [selectedLeadForEdit, setSelectedLeadForEdit] = useState(null);
  const [selectedDealForEdit, setSelectedDealForEdit] = useState(null);

  // 💰 Create-Lead form bindings (commission % / amount toggle lives here)
  const [isLeadFormOpen, setIsLeadFormOpen] = useState(false);
  const [leadForm, setLeadForm] = useState({
    clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential',
    preferredArea: '', estimatedBudget: '', commissionType: 'Percent', commissionValue: '',
    allocatedToAgent: ''
  });

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
        const [boardsRes, brokersRes, cardsRes] = await Promise.all([
          crmService.getCRMBoards(),
          crmService.getBrokers(),
          crmService.getActiveBrochureCards().catch(() => [])
        ]);
        const boards = boardsRes || {};
        setUnverifiedLeads(boards.unverifiedLeads || []);
        setVerifiedDeals(boards.verifiedDeals || []);
        setBrokers(brokersRes || []);
        setActiveCards(cardsRes || []);
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

  // --- 🔍 CLIENT-SIDE SEARCH + PAGINATED INFINITE SCROLL (20 per page, fetches more on scroll) ---
  const PAGE_SIZE = 20;
  const [searchTerm, setSearchTerm] = useState('');
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  // Reset paging + search whenever the active tab changes.
  const handleSetTab = (id) => {
    setActiveTab(id);
    setSearchTerm('');
    setVisibleCount(PAGE_SIZE);
  };

  const sentinelRef = useRef(null);
  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;
    const observer = new IntersectionObserver((entries) => {
      if (entries[0].isIntersecting) setVisibleCount((c) => c + PAGE_SIZE);
    }, { threshold: 1 });
    observer.observe(el);
    return () => observer.disconnect();
  }, [sentinelRef]);

  // Case-insensitive search across the supplied field selectors for the active tab.
  const filterBySearch = (items, fieldFns) => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return items;
    return items.filter((item) =>
      fieldFns.some((fn) => String(fn(item) ?? '').toLowerCase().includes(term))
    );
  };
  const firstVisible = (items, fieldFns) => filterBySearch(items, fieldFns).slice(0, visibleCount);
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
      // 🎯 Presentation stage is the trigger point for the brochure card journey: hand the
      // deal straight to the builder so the broker can curate the client deck.
      if (nextStageValue === 'Presentation' && typeof onOpenBrochureBuilder === 'function') {
        onOpenBrochureBuilder(selectedDealForEdit);
      }
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

  const handleCreateLead = async (e) => {
    e.preventDefault();
    try {
      await crmService.createLead({
        ...leadForm,
        estimatedBudget: parseFloat(leadForm.estimatedBudget) || 0,
        commissionValue: parseFloat(leadForm.commissionValue) || 0
      });
      setLeadForm({ clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential', preferredArea: '', estimatedBudget: '', commissionType: 'Percent', commissionValue: '', allocatedToAgent: '' });
      setIsLeadFormOpen(false);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to create lead record.');
    }
  };

  // 🚀 Promote a lead to a deal with automatic property matching (rent or sale).
  const handlePromoteLead = async (lead, isRent) => {
    try {
      const res = await crmService.promoteLeadToDeal(lead.leadId, {
        isRent,
        propertyTypeOverride: lead.propertyInterestClass,
        preferredAreaOverride: lead.preferredArea
      });
      alert(`Lead promoted to deal DL-${res.dealId}` +
        (res.linkedPropertyId ? ` (matched property ${res.linkedPropertyId})` : ' — no matching property found.'));
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Deal auto-promotion engine failed.');
    }
  };

  // ❌ Delete a deal — only AgencyAdmin + PlatformAdmin at the UI layer (server also enforces).
  const canManageDeals = currentUserRole === 'PlatformAdmin' || currentUserRole === 'AgencyAdmin';
  const handleDeleteDeal = async (deal) => {
    if (!window.confirm(`Delete deal DL-${deal.dealId}? This cannot be undone.`)) return;
    try {
      await crmService.deleteDeal(deal.dealId);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Deal deletion permission denied or failed.');
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
            onClick={() => handleSetTab(tab.id)}
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

      {/* 🔍 TAB-SCOPED SEARCH — filters the active pipeline list client-side */}
      {!isLoading && (
        <div className="relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            type="text"
            placeholder="Search this list..."
            value={searchTerm}
            onChange={(e) => { setSearchTerm(e.target.value); setVisibleCount(PAGE_SIZE); }}
            className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
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

          {/* ➕ CREATE LEAD PANEL (with commission % / amount toggle) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
            <button
              type="button"
              onClick={() => setIsLeadFormOpen(!isLeadFormOpen)}
              className="flex items-center gap-2 text-xs font-bold text-slate-300 hover:text-white cursor-pointer"
            >
              <UserPlus size={14} /> New Lead {isLeadFormOpen ? '▲' : '▼'}
            </button>
            {isLeadFormOpen && (
              <form onSubmit={handleCreateLead} className="mt-4 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 animate-fade-in">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Client Name & Surname</label><input required type="text" value={leadForm.clientName} onChange={(e) => setLeadForm({ ...leadForm, clientName: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Email Address</label><input required type="email" value={leadForm.contactEmail} onChange={(e) => setLeadForm({ ...leadForm, contactEmail: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Phone Number</label><input type="text" value={leadForm.contactPhone} onChange={(e) => setLeadForm({ ...leadForm, contactPhone: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Property Interest</label><select value={leadForm.propertyInterestClass} onChange={(e) => setLeadForm({ ...leadForm, propertyInterestClass: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"><option>Residential</option><option>Commercial</option><option>Retail</option><option>Industrial</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Preferred Area / Suburb</label><input type="text" placeholder="Cape Town" value={leadForm.preferredArea} onChange={(e) => setLeadForm({ ...leadForm, preferredArea: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Estimated Budget (R)</label><input required type="number" value={leadForm.estimatedBudget} onChange={(e) => setLeadForm({ ...leadForm, estimatedBudget: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                {/* 💰 Commission toggle: percentage OR fixed amount */}
                <div className="space-y-1 md:col-span-2 lg:col-span-1">
                  <label className="text-[10px] text-slate-400 uppercase">Commission</label>
                  <div className="grid grid-cols-2 gap-1 h-10">
                    {[{ v: 'Percent', label: '% of Deal' }, { v: 'Amount', label: 'Fixed R' }].map((o) => (
                      <button key={o.v} type="button" onClick={() => setLeadForm({ ...leadForm, commissionType: o.v })} className={`text-xs font-bold rounded-lg border transition-all cursor-pointer ${leadForm.commissionType === o.v ? 'bg-blue-600 border-blue-600 text-white' : 'bg-slate-950 border-slate-700 text-slate-400 hover:text-white'}`}>{o.label}</button>
                    ))}
                  </div>
                </div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">{leadForm.commissionType === 'Percent' ? '% Commission' : 'Amount (R)'}</label><input required type="number" value={leadForm.commissionValue} onChange={(e) => setLeadForm({ ...leadForm, commissionValue: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 lg:col-span-3"><label className="text-[10px] text-slate-400 uppercase">Allocated To Agent</label><input list="broker-suggestions" type="text" placeholder="Leave blank for Unassigned Pool" value={leadForm.allocatedToAgent} onChange={(e) => setLeadForm({ ...leadForm, allocatedToAgent: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
                <datalist id="broker-suggestions">{brokers.map((b) => <option key={b.brokerId || b.fullName} value={b.fullName || b.emailAddress} />)}</datalist>
                {currentUserRole !== 'Broker' && <p className="text-[10px] text-slate-500">Type or select an existing broker from your agency. Brokers always self-allocate to themselves.</p>}</div>
                <div className="lg:col-span-3 flex gap-3 pt-1"><button type="submit" className="py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer">Create Lead Record</button><button type="button" onClick={() => setIsLeadFormOpen(false)} className="py-2 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">Cancel</button></div>
              </form>
            )}
          </div>

          {activeTab === 'leads' && firstVisible(unverifiedLeads, [
            (l) => l.clientName, (l) => l.contactEmail, (l) => l.contactPhone, (l) => l.leadStage
          ]).map((lead) => (
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
                  <p className="text-[10px] text-slate-500 mt-1">
                    💰 Budget: R {Number(lead.estimatedBudget || 0).toLocaleString()} •
                    Commission: {lead.commissionType === 'Percent' ? `${Number(lead.commissionValue || 0).toLocaleString()}%` : `R ${Number(lead.commissionValue || 0).toLocaleString()}`} •
                    Agent: <span className="text-blue-400 font-semibold">{lead.allocatedToAgent || 'Unassigned'}</span>
                  </p>
                </div>
              </div>
                <div className="flex items-center gap-2 flex-wrap">
                  <button onClick={() => { setSelectedLeadForEdit(lead); setNextStageValue(lead.leadStage); }} className="bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Update Stage</button>
                  <button onClick={() => handlePromoteLead(lead, true)} className="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Promote → Rent Deal 🚀</button>
                  <button onClick={() => handlePromoteLead(lead, false)} className="bg-purple-600 hover:bg-purple-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Promote → Sale Deal 🚀</button>
                </div>

                </div>

          ))}

          {/* 💼 RENDERING PIPELINE VIEW 2: VERIFIED CORPORATE DEALS */}
          {activeTab === 'deals' && firstVisible(verifiedDeals, [
            (d) => d.buyerClientId, (d) => d.sellerClientId, (d) => d.dealStage
          ]).map((deal) => (
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
                   {deal.dealStage === 'Presentation' && activeCardDealIds.has(deal.dealId) && (
                     <span title="Brochure card in progress" className="inline-flex items-center gap-1 text-[10px] font-bold font-mono text-blue-300 bg-blue-950/40 px-2 py-0.5 rounded border border-blue-900 animate-pulse">
                       <span className="w-1.5 h-1.5 rounded-full bg-blue-400 inline-block"></span> Card Active
                     </span>
                   )}
                 </div>
                <p className="text-xs text-slate-400 font-medium">Buyer: <span className="text-blue-400 font-semibold">{deal.buyerClientId}</span> • Seller: <span className="text-purple-400 font-semibold">{deal.sellerClientId}</span></p>
                {deal.commissionValue > 0 && (
                  <p className="text-[10px] text-slate-500 mt-1">
                    💰 Commission: {deal.commissionType === 'Percent' ? `${deal.commissionValue}%` : `R ${deal.commissionValue.toLocaleString()}`}
                    {deal.dealStage === 'ClosedWon' && deal.realisedCommission !== null && deal.realisedCommission !== undefined && ` → realised R ${Number(deal.realisedCommission).toLocaleString()}`}
                  </p>
                )}
              </div>
              <div className="flex items-center gap-2">
                <button onClick={() => { setSelectedDealForEdit(deal); setNextStageValue(deal.dealStage); }} className="bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Progress Deal</button>
                {deal.dealStage === 'Presentation' && (
                  <button
                    onClick={() => onOpenBrochureBuilder && onOpenBrochureBuilder(deal)}
                    className="p-2 bg-indigo-950/40 hover:bg-indigo-800 text-indigo-400 rounded-xl border border-indigo-900 cursor-pointer"
                    title="Open brochure card (add/remove units & client details)"
                  >
                    <Layers size={14} />
                  </button>
                )}
                {canManageDeals ? (
                  <button onClick={() => handleDeleteDeal(deal)} className="p-2 bg-red-950/40 hover:bg-red-900/40 text-red-400 rounded-xl border border-red-900 cursor-pointer" title="Delete deal (AgencyAdmin/PlatformAdmin only)">✕</button>
                ) : (
                  <span title="Deal deletion restricted to AgencyAdmin / PlatformAdmin" className="p-2 text-slate-600 cursor-not-allowed">🚫</span>
                )}
              </div>
            </div>
          ))}

          {/* 🏢 RENDERING PIPELINE VIEW 3: COMPANIES REGISTRY DIRECTORY */}
          {activeTab === 'companies' && firstVisible(companies, [
            (c) => c.businessName, (c) => c.registrationNumber, (c) => c.vatNumber, (c) => c.industryVertical
          ]).map((comp) => (
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
          {activeTab === 'legalEntities' && firstVisible(legalEntities, [
            (e) => e.registeredLegalName, (e) => e.entityTypeClassifier, (e) => e.taxRegistrationIndex, (e) => e.deedsOfficeIdentifier
          ]).map((ent) => (
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
          {activeTab === 'contacts' && firstVisible(contacts, [
            (c) => c.fullName, (c) => c.contactRoleType, (c) => c.positionJobTitle, (c) => c.emailAddress, (c) => c.phoneNumber
          ]).map((con) => (
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

          {((activeTab === 'leads' && filterBySearch(unverifiedLeads, []).length === 0) ||
            (activeTab === 'deals' && filterBySearch(verifiedDeals, []).length === 0) ||
            (activeTab === 'companies' && filterBySearch(companies, []).length === 0) ||
            (activeTab === 'legalEntities' && filterBySearch(legalEntities, []).length === 0) ||
            (activeTab === 'contacts' && filterBySearch(contacts, []).length === 0)) && !errorMessage && (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
              {searchTerm.trim()
                ? 'No records match your search. Try a different term.'
                : 'No active records found matching this pipeline layout screen state.'}
            </div>
          )}

          {/* 🔁 INFINITE-SCROLL SENTINEL: fetches the next PAGE_SIZE as the list is scrolled */}
          <div ref={sentinelRef} className="h-1" />
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
              {selectedLeadForEdit.leadNotes && (
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Lead Note History</label>
                  <div className="max-h-36 overflow-y-auto bg-slate-950 border border-slate-700 rounded-xl p-2 text-[10px] text-slate-400 space-y-1">
                    {selectedLeadForEdit.leadNotes.split(' | ').filter(Boolean).map((n, i) => <p key={i} className="leading-snug">• {n.trim()}</p>)}
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
              {selectedDealForEdit.dealNotes && (
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">Deal Note History</label>
                  <div className="max-h-36 overflow-y-auto bg-slate-950 border border-slate-700 rounded-xl p-2 text-[10px] text-slate-400 space-y-1">
                    {selectedDealForEdit.dealNotes.split(' | ').filter(Boolean).map((n, i) => <p key={i} className="leading-snug">• {n.trim()}</p>)}
                  </div>
                </div>
              )}
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
