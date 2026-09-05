import { useState, useEffect } from 'react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell } from 'recharts';
import { UserPlus, Search, Loader2, X } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { usePermission } from '../contexts/PermissionContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
import Can from '../components/Can.jsx';
import ContactPicker from '../components/ContactPicker.jsx';
import BrokerPicker from '../components/BrokerPicker.jsx';
import Pagination from '../components/Pagination.jsx';

const LEAD_STAGES = ['Contacted', 'Qualified', 'Negotiation', 'ProposalSent', 'ClosedWon', 'ClosedLost'];
const LEAD_PAGE_SIZE = 20;
const LEAD_STAGE_COLORS = { Contacted: '#3b82f6', Qualified: '#14b8a6', Negotiation: '#f59e0b', ProposalSent: '#a78bfa', ClosedWon: '#22c55e', ClosedLost: '#ef4444' };

const formatLeadStage = (value) => {
  if (typeof value === 'string') return value.replace(/([A-Z])/g, ' $1').trim();
  if (typeof value === 'number') return LEAD_STAGES[value]?.replace(/([A-Z])/g, ' $1').trim() || String(value);
  return String(value || '');
};

const getLeadStageValue = (lead) => {
  const stage = lead?.leadStage;
  if (typeof stage === 'string') return stage;
  if (typeof stage === 'number') return LEAD_STAGES[stage] || String(stage);
  return String(stage || '');
};

export default function LeadsPage() {
  const { hasPermission } = usePermission();
  const { activeTenantId } = useTenant();
  const [leads, setLeads] = useState([]);
  const [leadTotalCount, setLeadTotalCount] = useState(0);
  const [leadPage, setLeadPage] = useState(1);
  const [stageAggregates, setStageAggregates] = useState({});
  const [brokers, setBrokers] = useState([]);
  const [summaryReport, setSummaryReport] = useState({ activeLeadsCount: 0, valuationsInProcessCount: 0, conveyancingAtDeedsCount: 0, finalizedClosedCount: 0, totalPipelineEstimatedValue: 0 });
  const [isLoading, setIsLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [isLeadFormOpen, setIsLeadFormOpen] = useState(false);
  const [contactSearch, setContactSearch] = useState('');
  const [contactMatches, setContactMatches] = useState([]);
  const [selectedContact, setSelectedContact] = useState(null);
  const [showContactDropdown, setShowContactDropdown] = useState(false);
  const [editingLead, setEditingLead] = useState(null);
  const [nextStage, setNextStage] = useState('');
  const [notes, setNotes] = useState('');
  const [editForm, setEditForm] = useState({ clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential', preferredArea: '', leadType: 'ForSale', estimatedBudget: '', commissionType: 'Percent', commissionValue: '', allocatedBrokerIds: [], linkedContactId: null });
  const [editContactSearch, setEditContactSearch] = useState('');
  const [editContactMatches, setEditContactMatches] = useState([]);
  const [editSelectedContact, setEditSelectedContact] = useState(null);
  const [showEditContactDropdown, setShowEditContactDropdown] = useState(false);
  const [editSelectedBrokers, setEditSelectedBrokers] = useState([]);
  const [errorMessage, setErrorMessage] = useState('');

  const toErrorString = (err, fallback) => {
    if (!err) return fallback;
    if (typeof err === 'string') return err;
    if (err.message) return err.message;
    try { return JSON.stringify(err); } catch { return fallback; }
  };

  const loadData = async (pageToLoad = 1) => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const [summary, boards, brokersRes] = await Promise.all([
        crmService.getPipelineReportSummary(),
        crmService.getCRMBoards(pageToLoad),
        crmService.getBrokers()
      ]);
      setSummaryReport(summary);
      setLeads(boards.unverifiedLeads || []);
      setLeadTotalCount(boards.leadCount || 0);
      setStageAggregates(boards.aggregates?.leadStage || {});
      setLeadPage(pageToLoad);
      setBrokers(brokersRes || []);
    } catch (err) {
      setErrorMessage(toErrorString(err, 'Failed to load leads.'));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { loadData(1); }, [activeTenantId]);

  const handleLeadPageChange = (newPage) => {
    if (newPage < 1) return;
    const maxPage = Math.max(1, Math.ceil(leadTotalCount / LEAD_PAGE_SIZE));
    if (newPage > maxPage) return;
    loadData(newPage);
  };

  // 🧑‍💼 Resolve a comma-separated AllocatedBrokerIds string to broker names
  // using the in-component `brokers` roster. Falls back to the raw id for
  // any id that hasn't loaded yet (so cards never crash if brokers are
  // still being fetched).
  const resolveBrokerNames = (allocatedBrokerIds) => {
    if (!allocatedBrokerIds) return '';
    return allocatedBrokerIds
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean)
      .map((id) => {
        const b = brokers.find((x) => x.brokerId === id);
        return b ? b.fullName : id;
      })
      .join(', ');
  };

  const handleContactSearch = async (value) => {
    setContactSearch(value);
    setSelectedContact(null);
    if (value.trim().length < 2) { setContactMatches([]); setShowContactDropdown(false); return; }
    try {
      const results = await crmService.searchContacts(value);
      setContactMatches(results || []);
      setShowContactDropdown(true);
    } catch { setContactMatches([]); }
  };

  const selectContact = (contact) => {
    setSelectedContact(contact);
    setContactSearch(contact.fullName || '');
    setShowContactDropdown(false);
  };

  const clearContact = () => {
    setSelectedContact(null);
    setContactSearch('');
    setContactMatches([]);
    setShowContactDropdown(false);
  };

  const handleEditContactSearch = async (value) => {
    setEditContactSearch(value);
    setEditSelectedContact(null);
    if (value.trim().length < 2) { setEditContactMatches([]); setShowEditContactDropdown(false); return; }
    try {
      const results = await crmService.searchContacts(value);
      setEditContactMatches(results || []);
      setShowEditContactDropdown(true);
    } catch { setEditContactMatches([]); }
  };

  const selectEditContact = (contact) => {
    setEditSelectedContact(contact);
    setEditContactSearch(contact.fullName || '');
    setShowEditContactDropdown(false);
    setEditForm((prev) => ({ ...prev, linkedContactId: contact.contactId }));
  };

  const clearEditContact = () => {
    setEditSelectedContact(null);
    setEditContactSearch('');
    setEditContactMatches([]);
    setShowEditContactDropdown(false);
    setEditForm((prev) => ({ ...prev, linkedContactId: null }));
  };

  const handleCreateLead = async (e) => {
    e.preventDefault();
    try {
      const payload = {
        clientName: selectedContact ? selectedContact.fullName : leadForm.clientName,
        contactEmail: selectedContact ? selectedContact.emailAddress : leadForm.contactEmail,
        contactPhone: selectedContact ? selectedContact.phoneNumber : leadForm.contactPhone,
        propertyInterestClass: leadForm.propertyInterestClass,
        preferredArea: leadForm.preferredArea,
        leadType: leadForm.leadType,
        estimatedBudget: parseFloat(leadForm.estimatedBudget) || 0,
        commissionType: leadForm.commissionType,
        commissionValue: parseFloat(leadForm.commissionValue) || 0,
        allocatedBrokerIds: (selectedBrokers || []).map((b) => b.brokerId).join(','),
        linkedContactId: selectedContact ? selectedContact.contactId : null
      };
      await crmService.createLead(payload);
      setLeadForm({ clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential', preferredArea: '', estimatedBudget: '', commissionType: 'Percent', commissionValue: '', allocatedBrokerIds: [] });
      setSelectedBrokers([]);
      clearContact();
      setIsLeadFormOpen(false);
      await loadData();
    } catch (err) {
      alert(toErrorString(err, 'Failed to create lead.'));
    }
  };

  const handleUpdateLead = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateLeadStage(editingLead.leadId, nextStage, notes, '', '');
      setEditingLead(null);
      setNotes('');
      setNextStage('');
      await loadData();
    } catch (err) {
      alert(toErrorString(err, 'Failed to update lead stage.'));
    }
  };

  const handlePromoteLead = async (lead, isRent) => {
    try {
      const res = await crmService.promoteLeadToDeal(lead.leadId, { isRent, propertyTypeOverride: lead.propertyInterestClass, preferredAreaOverride: lead.preferredArea });
      alert(`Lead promoted to deal DL-${res.dealId}`);
      await loadData();
    } catch (err) {
      alert(toErrorString(err, 'Promotion failed.'));
    }
  };

  const handleUpdateLeadFull = async (e) => {
    e.preventDefault();
    if (!editingLead) return;
    try {
      await crmService.updateLead(editingLead.leadId, {
        ...editForm,
        estimatedBudget: parseFloat(editForm.estimatedBudget) || 0,
        commissionValue: parseFloat(editForm.commissionValue) || 0,
        // The picker keeps its own array; the form's `allocatedBrokerIds`
        // is just the ids array (kept in sync on hydrate).
        allocatedBrokerIds: (editSelectedBrokers || []).map((b) => b.brokerId).join(',')
      });
      setEditingLead(null);
      setNotes('');
      setEditSelectedBrokers([]);
      clearEditContact();
      await loadData();
    } catch (err) {
      alert(toErrorString(err, 'Failed to update lead.'));
    }
  };

  const [leadForm, setLeadForm] = useState({ clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential', preferredArea: '', leadType: 'ForSale', estimatedBudget: '', commissionType: 'Percent', commissionValue: '', allocatedBrokerIds: [] });
  const [selectedBrokers, setSelectedBrokers] = useState([]);

  const filteredLeads = leads.filter((l) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (l.clientName || '').toLowerCase().includes(term) || (l.contactEmail || '').toLowerCase().includes(term) || (l.leadStage || '').toLowerCase().includes(term);
  });

  const stageCounts = LEAD_STAGES.reduce((acc, stage) => { acc[stage] = Number(stageAggregates[stage] || 0); return acc; }, {});
  const chartData = LEAD_STAGES.map((stage) => ({ name: stage.replace(/([A-Z])/g, ' $1').trim(), value: stageCounts[stage], color: LEAD_STAGE_COLORS[stage] })).filter((d) => d.value > 0);

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-white">Leads Pipeline</h2>
          <p className="text-xs text-slate-400 mt-1">Tenant-scoped leads • {leadTotalCount} total</p>
        </div>
        <Can permission="leads:create">
          <button type="button" onClick={() => { setIsLeadFormOpen(!isLeadFormOpen); if (!isLeadFormOpen) { clearContact(); setLeadForm({ clientName: '', contactEmail: '', contactPhone: '', propertyInterestClass: 'Residential', preferredArea: '', estimatedBudget: '', commissionType: 'Percent', commissionValue: '', allocatedBrokerIds: [] }); setSelectedBrokers([]); } }} className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold px-4 py-2.5 rounded-xl transition-all">
            <UserPlus size={14} /> New Lead
          </button>
        </Can>
      </div>

      {/* 📊 Summary Metrics */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
          <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Active Leads</span>
          <span className="text-xl font-black text-white">{summaryReport.activeLeadsCount}</span>
        </div>
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
          <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Valuations</span>
          <span className="text-xl font-black text-amber-400">{summaryReport.valuationsInProcessCount}</span>
        </div>
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
          <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Conveyancing</span>
          <span className="text-xl font-black text-purple-400">{summaryReport.conveyancingAtDeedsCount}</span>
        </div>
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
          <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Pipeline Value</span>
          <span className="text-xl font-black text-emerald-400">R {Number(summaryReport.totalPipelineEstimatedValue || 0).toLocaleString()}</span>
        </div>
      </div>

      {/* 📊 Stage Dashboard */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl">
        <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.12em] text-[10px] mb-4">
          <UserPlus size={12} className="text-blue-500" /> Lead Stage Distribution
        </div>
        {chartData.length === 0 ? (
          <p className="text-xs text-slate-500 text-center py-8">No leads yet. Create your first lead above.</p>
        ) : (
          <ResponsiveContainer width="100%" height={250}>
            <BarChart data={chartData} layout="vertical" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
              <XAxis type="number" hide />
              <YAxis dataKey="name" type="category" tick={{ fontSize: 11, fill: '#94a3b8' }} width={140} />
              <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#cbd5e1', fontSize: 11 }} cursor={{ fill: 'rgba(255,255,255,0.05)' }} />
              <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                {chartData.map((entry, index) => (<Cell key={index} fill={entry.color} />))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>

      {/* ➕ Create Lead Form */}
      {isLeadFormOpen && (
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl animate-fade-in">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-bold text-white uppercase tracking-wider">New Lead</h3>
            <button type="button" onClick={() => { setIsLeadFormOpen(false); clearContact(); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
          </div>
          <form onSubmit={handleCreateLead} className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div className="space-y-1 lg:col-span-3">
              <label className="text-[10px] text-slate-400 uppercase">Link Existing Contact (optional)</label>
              <ContactPicker
                value={selectedContact}
                onChange={(c) => { setSelectedContact(c); setContactSearch(c?.fullName || ''); }}
                allowCreate={false}
                placeholder="Search your tenant contacts by name, email, or phone..."
              />
            </div>
            {!selectedContact && (
              <>
              <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Client Name</label><input required type="text" value={leadForm.clientName} onChange={(e) => setLeadForm({ ...leadForm, clientName: e.target.value })} placeholder="Full name" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
              <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Email</label><input required type="email" value={leadForm.contactEmail} onChange={(e) => setLeadForm({ ...leadForm, contactEmail: e.target.value })} placeholder="email@example.com" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
              <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Phone</label><input type="text" value={leadForm.contactPhone} onChange={(e) => setLeadForm({ ...leadForm, contactPhone: e.target.value })} placeholder="+27 82 000 0000" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
              </>
            )}
            <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Property Interest</label><select value={leadForm.propertyInterestClass} onChange={(e) => setLeadForm({ ...leadForm, propertyInterestClass: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Residential</option><option>Commercial</option><option>Retail</option><option>Industrial</option></select></div>
            <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Lead Type</label><select value={leadForm.leadType} onChange={(e) => setLeadForm({ ...leadForm, leadType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option value="ForSale">For Sale</option><option value="ToRent">To Rent</option></select></div>
            <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Preferred Area</label><input type="text" value={leadForm.preferredArea} onChange={(e) => setLeadForm({ ...leadForm, preferredArea: e.target.value })} placeholder="Cape Town" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
            <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Budget (R)</label><input required type="number" value={leadForm.estimatedBudget} onChange={(e) => setLeadForm({ ...leadForm, estimatedBudget: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
            <div className="space-y-1 lg:col-span-3">
              <label className="text-[10px] text-slate-400 uppercase">Commission</label>
              <div className="grid grid-cols-2 gap-1 h-10">
                {[{ v: 'Percent', label: '% of Deal' }, { v: 'Amount', label: 'Fixed R' }].map((o) => (
                  <button key={o.v} type="button" onClick={() => setLeadForm({ ...leadForm, commissionType: o.v })} className={`text-xs font-bold rounded-lg border transition-all cursor-pointer ${leadForm.commissionType === o.v ? 'bg-blue-600 border-blue-600 text-white' : 'bg-slate-950 border-slate-700 text-slate-400 hover:text-white'}`}>{o.label}</button>
                ))}
              </div>
            </div>
            <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">{leadForm.commissionType === 'Percent' ? '% Commission' : 'Amount (R)'}</label><input required type="number" value={leadForm.commissionValue} onChange={(e) => setLeadForm({ ...leadForm, commissionValue: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
            <div className="space-y-1 lg:col-span-3">
              <label className="text-[10px] text-slate-400 uppercase">Allocated Brokers</label>
              <BrokerPicker
                multi
                value={selectedBrokers}
                onChange={setSelectedBrokers}
                placeholder="Search brokers by name or email..."
              />
            </div>
            <div className="lg:col-span-3 flex gap-3 pt-1">
              <Can permission="leads:create">
                <button type="submit" className="py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer">Create Lead</button>
              </Can>
              <button type="button" onClick={() => { setIsLeadFormOpen(false); clearContact(); }} className="py-2.5 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">Cancel</button>
            </div>
          </form>
        </div>
      )}

      {errorMessage && <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">{errorMessage}</div>}

      {isLoading ? (
        <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
          <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
          <span className="text-xs font-medium tracking-wide">Loading leads...</span>
        </div>
      ) : (
        <div className="space-y-4">
          {/* 🔍 Search */}
          {!isLoading && (
            <div className="relative">
              <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
              <input type="text" placeholder="Search leads..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500" />
            </div>
          )}

          {filteredLeads.length === 0 && !errorMessage ? (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
              {searchTerm.trim() ? 'No leads match your search.' : 'No leads found.'}
            </div>
          ) : (
            filteredLeads.map((lead) => (
              <div key={lead.leadId} onClick={() => {
                setEditingLead(lead);
                setNextStage(getLeadStageValue(lead));
                setNotes(lead.leadNotes || '');
                // 🧑‍💼 Resolve broker ids (csv) → broker objects for the picker.
                // The picker's display is live-resolved from crmService.getBrokers()
                // so renaming a broker automatically updates every lead card.
                const ids = (lead.allocatedBrokerIds || '').split(',').map((s) => s.trim()).filter(Boolean);
                const hydratedBrokers = ids
                  .map((id) => brokers.find((b) => b.brokerId === id))
                  .filter(Boolean);
                setEditSelectedBrokers(hydratedBrokers);
                setEditForm({
                  clientName: lead.clientName || '',
                  contactEmail: lead.contactEmail || '',
                  contactPhone: lead.contactPhone || '',
                  propertyInterestClass: lead.propertyInterestClass || 'Residential',
                  preferredArea: lead.preferredArea || '',
                  leadType: lead.leadType || 'ForSale',
                  estimatedBudget: String(lead.estimatedBudget || ''),
                  commissionType: lead.commissionType || 'Percent',
                  commissionValue: String(lead.commissionValue || ''),
                  allocatedBrokerIds: ids,
                  linkedContactId: lead.linkedContactId || null
                });
                setEditContactSearch('');
                setEditContactMatches([]);
                setEditSelectedContact(null);
                setShowEditContactDropdown(false);
              }} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col lg:flex-row justify-between lg:items-center gap-4 hover:border-blue-700 transition-colors cursor-pointer">
                <div className="space-y-2 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border bg-blue-950 text-blue-400 border-blue-900">Lead: {formatLeadStage(lead.leadStage)}</span>
                    <span className="text-xs text-slate-500 font-mono">LD-{lead.leadId}</span>
                  </div>
                  <div>
                    <h4 className="text-white font-bold text-base tracking-tight">{lead.clientName}</h4>
                    <p className="text-xs text-slate-400 font-medium">Email: {lead.contactEmail} • Phone: {lead.contactPhone}</p>
                    <p className="text-[10px] text-slate-500 mt-1">
                      Budget: R {Number(lead.estimatedBudget || 0).toLocaleString()} • Commission: {lead.commissionType === 'Percent' ? `${Number(lead.commissionValue || 0).toLocaleString()}%` : `R ${Number(lead.commissionValue || 0).toLocaleString()}`} • Brokers: <span className="text-blue-400 font-semibold">{resolveBrokerNames(lead.allocatedBrokerIds, brokers) || 'Unassigned'}</span>
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2 flex-wrap" onClick={(e) => e.stopPropagation()}>
                  <Can permission="leads:edit">
                    <button onClick={() => { setEditingLead(lead); setNextStage(getLeadStageValue(lead)); setNotes(''); }} className="bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Update Stage</button>
                  </Can>
                  <Can permission="deals:create">
                    {/* 🚀 Promote buttons are gated by lead.leadType:
                        ForSale leads → Sale Deal only; ToRent leads → Rent Deal only.
                        Backend PromoteLeadToDeal picks listing type from the request.isRent flag. */}
                    {lead.leadType === 'ToRent' && (
                      <button onClick={(e) => { e.stopPropagation(); handlePromoteLead(lead, true); }} className="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Promote → Rent Deal</button>
                    )}
                    {lead.leadType === 'ForSale' && (
                      <button onClick={(e) => { e.stopPropagation(); handlePromoteLead(lead, false); }} className="bg-purple-600 hover:bg-purple-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Promote → Sale Deal</button>
                    )}
                  </Can>
                </div>
              </div>
            ))
          )}

          <Pagination
            page={leadPage}
            pageSize={LEAD_PAGE_SIZE}
            totalCount={leadTotalCount}
            onPageChange={handleLeadPageChange}
            className="pt-2"
          />
        </div>
      )}

      {/* Edit Lead Modal */}
      {editingLead && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-lg w-full space-y-4 shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-white uppercase tracking-wider">Edit Lead</h3>
              <button type="button" onClick={() => { setEditingLead(null); setNotes(''); clearEditContact(); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
            </div>
            <form onSubmit={handleUpdateLeadFull} className="space-y-3">
              <div className="space-y-1 lg:col-span-3">
                <label className="text-[10px] text-slate-400 uppercase">Link Existing Contact (optional)</label>
                <ContactPicker
                  value={editSelectedContact}
                  onChange={(c) => { setEditSelectedContact(c); setEditForm((prev) => ({ ...prev, linkedContactId: c?.contactId || null })); }}
                  allowCreate={false}
                  placeholder="Search tenant contacts by name, email, or phone..."
                />
              </div>
              {!editSelectedContact && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Client Name</label><input required type="text" value={editForm.clientName} onChange={(e) => setEditForm({ ...editForm, clientName: e.target.value })} placeholder="Full name" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                  <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Email</label><input required type="email" value={editForm.contactEmail} onChange={(e) => setEditForm({ ...editForm, contactEmail: e.target.value })} placeholder="email@example.com" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                   <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Phone</label><input type="text" value={editForm.contactPhone} onChange={(e) => setEditForm({ ...editForm, contactPhone: e.target.value })} placeholder="+27 82 000 0000" className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                 </div>
                )}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Property Interest</label><select value={editForm.propertyInterestClass} onChange={(e) => setEditForm({ ...editForm, propertyInterestClass: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option>Residential</option><option>Commercial</option><option>Retail</option><option>Industrial</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Lead Type</label><select value={editForm.leadType} onChange={(e) => setEditForm({ ...editForm, leadType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"><option value="ForSale">For Sale</option><option value="ToRent">To Rent</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Preferred Area</label><input type="text" value={editForm.preferredArea} onChange={(e) => setEditForm({ ...editForm, preferredArea: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Budget (R)</label><input required type="number" value={editForm.estimatedBudget} onChange={(e) => setEditForm({ ...editForm, estimatedBudget: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Commission</label>
                  <div className="grid grid-cols-2 gap-1 h-10">
                    {[{ v: 'Percent', label: '% of Deal' }, { v: 'Amount', label: 'Fixed R' }].map((o) => (
                      <button key={o.v} type="button" onClick={() => setEditForm({ ...editForm, commissionType: o.v })} className={`text-xs font-bold rounded-lg border transition-all cursor-pointer ${editForm.commissionType === o.v ? 'bg-blue-600 border-blue-600 text-white' : 'bg-slate-950 border-slate-700 text-slate-400 hover:text-white'}`}>{o.label}</button>
                    ))}
                  </div>
                </div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">{editForm.commissionType === 'Percent' ? '% Commission' : 'Amount (R)'}</label><input required type="number" value={editForm.commissionValue} onChange={(e) => setEditForm({ ...editForm, commissionValue: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Allocated Brokers</label>
                  <BrokerPicker
                    multi
                    value={editSelectedBrokers}
                    onChange={setEditSelectedBrokers}
                    placeholder="Search brokers by name or email..."
                  />
                </div>
                <div className="md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Stage</label>
                  <select value={nextStage} onChange={(e) => setNextStage(e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500">
                    {LEAD_STAGES.map((s) => <option key={s} value={s}>{s.replace(/([A-Z])/g, ' $1').trim()}</option>)}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Notes</label>
                  <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" placeholder="Optional notes..." />
                </div>
              </div>
              <div className="flex gap-2">
                <Can permission="leads:edit">
                  <button type="submit" className="flex-1 py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">Save Changes</button>
                </Can>
                <button type="button" onClick={() => { setEditingLead(null); setNotes(''); clearEditContact(); }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300">Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
