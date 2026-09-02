import { useState, useEffect } from 'react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell } from 'recharts';
import { Briefcase, Search, Loader2, Layers, X, Trash2 } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import { usePermission } from '../contexts/PermissionContext.jsx';
import Can from '../components/Can.jsx';

const DEAL_STAGES = ['DiscoveryNeedsAnalysis', 'Presentation', 'ProposalQuoteSent', 'Negotiation', 'ClosedWon', 'ClosedLost'];
const DEAL_STAGE_COLORS = { DiscoveryNeedsAnalysis: '#0ea5e9', Presentation: '#3b82f6', ProposalQuoteSent: '#a78bfa', Negotiation: '#f59e0b', ClosedWon: '#22c55e', ClosedLost: '#ef4444' };

const formatDealStage = (value) => {
  if (typeof value === 'string') return value.replace(/([A-Z])/g, ' $1').trim();
  if (typeof value === 'number') return DEAL_STAGES[value]?.replace(/([A-Z])/g, ' $1').trim() || String(value);
  return String(value || '');
};

const getDealStageValue = (deal) => {
  const stage = deal?.dealStage;
  if (typeof stage === 'string') return stage;
  if (typeof stage === 'number') return DEAL_STAGES[stage] || String(stage);
  return String(stage || '');
};

export default function DealsPage() {
  const { userRole: currentUserRole } = useAuth();
  const { setBrochureDealTarget: onOpenBrochureBuilder } = useAppUi();
  const { hasPermission } = usePermission();
  const [deals, setDeals] = useState([]);
  const [dealTotalCount, setDealTotalCount] = useState(0);
  const [brokers, setBrokers] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [activeCards, setActiveCards] = useState([]);
  const [summaryReport, setSummaryReport] = useState({ activeLeadsCount: 0, valuationsInProcessCount: 0, conveyancingAtDeedsCount: 0, finalizedClosedCount: 0, totalPipelineEstimatedValue: 0 });
  const [isLoading, setIsLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [editingDeal, setEditingDeal] = useState(null);
  const [nextStage, setNextStage] = useState('');
  const [notes, setNotes] = useState('');
  const [editForm, setEditForm] = useState({ buyerClientId: '', sellerClientId: '', dealStage: 'DiscoveryNeedsAnalysis', transactionValue: '', commissionType: 'Percent', commissionValue: '', realisedCommission: '', linkedPropertyId: '', dealNotes: '', allocatedToAgent: '' });
  const [brokerSplits, setBrokerSplits] = useState([]);
  const [isLoadingSplits, setIsLoadingSplits] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const canManageDeals = currentUserRole === 'PlatformAdmin' || currentUserRole === 'AgencyAdmin';
  const canEditCommission = currentUserRole === 'PlatformAdmin' || currentUserRole === 'AgencyAdmin';

  const loadData = async () => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const [summary, boards, brokersRes, contactsRes, cardsRes] = await Promise.all([
        crmService.getPipelineReportSummary(),
        crmService.getCRMBoards(),
        crmService.getBrokers(),
        crmService.getContacts().catch(() => []),
        crmService.getActiveBrochureCards().catch(() => [])
      ]);
      setSummaryReport(summary);
      setDeals(boards.verifiedDeals || []);
      setDealTotalCount(boards.dealCount || 0);
      setBrokers(brokersRes || []);
      setContacts(contactsRes || []);
      setActiveCards(cardsRes || []);
    } catch (err) {
      setErrorMessage(err || 'Failed to load deals.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { loadData(); }, []);

  useEffect(() => {
    if (editingDeal) {
      loadBrokerSplits(editingDeal.dealId);
    }
  }, [editingDeal]);

  const activeCardDealIds = new Set(activeCards.map((c) => c.dealId));

  const handleDeleteDeal = async (deal) => {
    if (!window.confirm(`Delete deal DL-${deal.dealId}? This cannot be undone.`)) return;
    try {
      await crmService.deleteDeal(deal.dealId);
      await loadData();
    } catch (err) {
      alert(err || 'Deletion failed.');
    }
  };

  const handleUpdateDealFull = async (e) => {
    e.preventDefault();
    if (!editingDeal) return;
    try {
      await crmService.updateDeal(editingDeal.dealId, {
        ...editForm,
        transactionValue: parseFloat(editForm.transactionValue) || 0,
        commissionValue: parseFloat(editForm.commissionValue) || 0,
        realisedCommission: parseFloat(editForm.realisedCommission) || 0,
        linkedPropertyId: editForm.linkedPropertyId ? parseInt(editForm.linkedPropertyId, 10) : null
      });
      setEditingDeal(null);
      setNotes('');
      await loadData();
    } catch (err) {
      alert(err || 'Failed to update deal.');
    }
  };

  const loadBrokerSplits = async (dealId) => {
    setIsLoadingSplits(true);
    try {
      const splits = await crmService.getDealBrokerSplits(dealId);
      setBrokerSplits(splits || []);
    } catch {
      setBrokerSplits([]);
    } finally {
      setIsLoadingSplits(false);
    }
  };

  const handleUpsertBrokerSplits = async (e) => {
    e.preventDefault();
    if (!editingDeal) return;
    try {
      await crmService.upsertDealBrokerSplits(editingDeal.dealId, brokerSplits);
      alert('Commission split updated.');
      await loadBrokerSplits(editingDeal.dealId);
    } catch (err) {
      alert(err || 'Failed to update commission split.');
    }
  };

  const filteredDeals = deals.filter((d) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (d.buyerClientId || '').toLowerCase().includes(term) || (d.sellerClientId || '').toLowerCase().includes(term) || (d.dealStage || '').toLowerCase().includes(term);
  });

  const stageCounts = DEAL_STAGES.reduce((acc, stage) => { acc[stage] = deals.filter((d) => getDealStageValue(d) === stage).length; return acc; }, {});
  const chartData = DEAL_STAGES.map((stage) => ({ name: stage.replace(/([A-Z])/g, ' $1').trim(), value: stageCounts[stage], color: DEAL_STAGE_COLORS[stage] })).filter((d) => d.value > 0);

  const pipelineValue = deals.filter((d) => { const stage = getDealStageValue(d); return stage !== 'ClosedWon' && stage !== 'ClosedLost'; }).reduce((sum, d) => sum + (Number(d.transactionValue) || 0), 0);

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-white">Deals Pipeline</h2>
          <p className="text-xs text-slate-400 mt-1">Tenant-scoped deals • {dealTotalCount} total</p>
        </div>
      </div>

      {/* 📊 Stage Dashboard */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.12em] text-[10px] mb-4">
            <Briefcase size={12} className="text-amber-500" /> Deal Stage Distribution
          </div>
          {chartData.length === 0 ? (
            <p className="text-xs text-slate-500 text-center py-8">No deals yet.</p>
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

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-center">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.12em] text-[10px] mb-4">
            <Briefcase size={12} className="text-emerald-500" /> Pipeline Metrics
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-emerald-400">{summaryReport.finalizedClosedCount}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Closed Won</div>
            </div>
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-amber-400">{summaryReport.valuationsInProcessCount}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">In Process</div>
            </div>
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-purple-400">{summaryReport.conveyancingAtDeedsCount}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Conveyancing</div>
            </div>
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-sm font-black text-white">R {pipelineValue.toLocaleString()}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Pipeline Value</div>
            </div>
          </div>
        </div>
      </div>

      {errorMessage && <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">{errorMessage}</div>}

      {isLoading ? (
        <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
          <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
          <span className="text-xs font-medium tracking-wide">Loading deals...</span>
        </div>
      ) : (
        <div className="space-y-4">
          {!isLoading && (
            <div className="relative">
              <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
              <input type="text" placeholder="Search deals..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500" />
            </div>
          )}

          {filteredDeals.length === 0 && !errorMessage ? (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
              {searchTerm.trim() ? 'No deals match your search.' : 'No deals found.'}
            </div>
          ) : (
            filteredDeals.map((deal) => (
              <div key={deal.dealId} onClick={() => {
                setEditingDeal(deal);
                setNextStage(getDealStageValue(deal));
                setNotes(deal.dealNotes || '');
                setEditForm({
                  buyerClientId: deal.buyerClientId || '',
                  sellerClientId: deal.sellerClientId || '',
                  dealStage: getDealStageValue(deal),
                  transactionValue: String(deal.transactionValue || ''),
                  commissionType: deal.commissionType || 'Percent',
                  commissionValue: String(deal.commissionValue || ''),
                  realisedCommission: String(deal.realisedCommission || ''),
                  linkedPropertyId: String(deal.linkedPropertyId || ''),
                  dealNotes: deal.dealNotes || '',
                  allocatedToAgent: deal.allocatedToAgent || ''
                });
              }} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col lg:flex-row justify-between lg:items-center gap-4 hover:border-blue-700 transition-colors cursor-pointer">
                <div className="space-y-2 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border ${
                      getDealStageValue(deal) === 'ClosedWon' ? 'bg-emerald-950 text-emerald-400 border-emerald-900' :
                      getDealStageValue(deal) === 'ClosedLost' ? 'bg-red-950 text-red-400 border-red-900' :
                      'bg-amber-950 text-amber-400 border-amber-900'
                    }`}>
                      Deal: {formatDealStage(deal.dealStage)}
                    </span>
                    <span className="text-xs text-slate-500 font-mono">DL-{deal.dealId}</span>
                    {getDealStageValue(deal) === 'Presentation' && activeCardDealIds.has(deal.dealId) && (
                      <span className="inline-flex items-center gap-1 text-[10px] font-bold font-mono text-blue-300 bg-blue-950/40 px-2 py-0.5 rounded border border-blue-900 animate-pulse">
                        <span className="w-1.5 h-1.5 rounded-full bg-blue-400 inline-block"></span> Card Active
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-400 font-medium">Buyer: <span className="text-blue-400 font-semibold">{contacts.find((c) => c.contactId === deal.buyerClientId)?.fullName || deal.buyerClientId}</span> • Seller: <span className="text-purple-400 font-semibold">{contacts.find((c) => c.contactId === deal.sellerClientId)?.fullName || deal.sellerClientId}</span></p>
                  {deal.commissionValue > 0 && (
                    <p className="text-[10px] text-slate-500 mt-1">
                      💰 Commission: {deal.commissionType === 'Percent' ? `${deal.commissionValue}%` : `R ${deal.commissionValue.toLocaleString()}`}
                       {getDealStageValue(deal) === 'ClosedWon' && deal.realisedCommission != null && ` → realised R ${Number(deal.realisedCommission).toLocaleString()}`}
                    </p>
                  )}
                  {deal.brokerSplits && deal.brokerSplits.length > 0 && (
                    <p className="text-[10px] text-slate-500 mt-1">
                      🤝 Split: {deal.brokerSplits.map(s => `${s.brokerName} (${s.splitPercentage}%)`).join(', ')}
                    </p>
                  )}
                </div>
                <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                  <Can permission="deals:edit">
                    <button onClick={() => { setEditingDeal(deal); setNextStage(getDealStageValue(deal)); setNotes(''); }} className="bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold py-2 px-3 rounded-xl cursor-pointer">Progress Deal</button>
                  </Can>
                  {getDealStageValue(deal) === 'Presentation' && (
                    <Can permission="billing:export">
                      <button onClick={() => onOpenBrochureBuilder && onOpenBrochureBuilder(deal)} className="p-2 bg-indigo-950/40 hover:bg-indigo-800 text-indigo-400 rounded-xl border border-indigo-900 cursor-pointer" title="Open brochure card"><Layers size={14} /></button>
                    </Can>
                  )}
                  <Can permission="deals:delete">
                    <button onClick={() => handleDeleteDeal(deal)} className="p-2 bg-red-950/40 hover:bg-red-900/40 text-red-400 rounded-xl border border-red-900 cursor-pointer" title="Delete deal"><Trash2 size={14} /></button>
                  </Can>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* Edit Deal Modal */}
      {editingDeal && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-lg w-full space-y-4 shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-white uppercase tracking-wider">Edit Deal</h3>
              <button type="button" onClick={() => { setEditingDeal(null); setNotes(''); }} className="text-slate-400 hover:text-white"><X size={14} /></button>
            </div>
            <form onSubmit={handleUpdateDealFull} className="space-y-3">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Buyer Client ID</label>
                  <select value={editForm.buyerClientId} onChange={(e) => setEditForm({ ...editForm, buyerClientId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500">
                    <option value="">Select buyer...</option>
                    {contacts.map((c) => <option key={c.contactId} value={c.contactId}>{c.fullName} ({c.emailAddress})</option>)}
                  </select>
                </div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Seller Client ID</label>
                  <select value={editForm.sellerClientId} onChange={(e) => setEditForm({ ...editForm, sellerClientId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500">
                    <option value="">Select seller...</option>
                    {contacts.map((c) => <option key={c.contactId} value={c.contactId}>{c.fullName} ({c.emailAddress})</option>)}
                  </select>
                </div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Transaction Value (R)</label><input required type="number" value={editForm.transactionValue} onChange={(e) => setEditForm({ ...editForm, transactionValue: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Linked Property ID</label><input type="number" value={editForm.linkedPropertyId} onChange={(e) => setEditForm({ ...editForm, linkedPropertyId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Commission</label>
                  <div className="grid grid-cols-2 gap-1 h-10">
                    {[{ v: 'Percent', label: '% of Deal' }, { v: 'Amount', label: 'Fixed R' }].map((o) => (
                      <button key={o.v} type="button" onClick={() => setEditForm({ ...editForm, commissionType: o.v })} className={`text-xs font-bold rounded-lg border transition-all cursor-pointer ${editForm.commissionType === o.v ? 'bg-blue-600 border-blue-600 text-white' : 'bg-slate-950 border-slate-700 text-slate-400 hover:text-white'}`}>{o.label}</button>
                    ))}
                  </div>
                </div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">{editForm.commissionType === 'Percent' ? '% Commission' : 'Amount (R)'}</label><input required type="number" value={editForm.commissionValue} onChange={(e) => setEditForm({ ...editForm, commissionValue: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Realised Commission (R)</label><input type="number" value={editForm.realisedCommission} onChange={(e) => setEditForm({ ...editForm, realisedCommission: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1 md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Allocated To Agent</label>
                  <select value={editForm.allocatedToAgent} onChange={(e) => setEditForm({ ...editForm, allocatedToAgent: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500">
                    <option value="">Unassigned Pool</option>
                    {brokers.map((b) => <option key={b.brokerId || b.emailAddress} value={b.fullName || b.emailAddress}>{b.fullName} ({b.emailAddress})</option>)}
                  </select>
                </div>

                {/* 🤝 CO-BROKERAGE COMMISSION SPLIT (AgencyAdmin only) */}
                <div className="md:col-span-2 p-3 bg-slate-950/40 border border-slate-800 rounded-xl space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-[10px] text-slate-400 uppercase font-bold">Commission Split</span>
                    {!canEditCommission && <span className="text-[10px] text-slate-500">Read-only</span>}
                  </div>
                  {isLoadingSplits ? (
                    <p className="text-xs text-slate-500">Loading splits...</p>
                  ) : brokerSplits.length > 0 ? (
                    <div className="space-y-1">
                      {brokerSplits.map((split) => (
                        <div key={split.splitId} className="flex items-center justify-between text-xs bg-slate-900/60 p-2 rounded-lg">
                          <span className="text-slate-300">{split.brokerName}</span>
                          <span className="text-slate-400 font-mono">{split.splitPercentage}% — R {split.splitAmount.toLocaleString()}</span>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-xs text-slate-500">No broker splits configured. Commission goes to the primary allocated agent.</p>
                  )}
                  {canEditCommission && (
                    <div className="flex gap-2 pt-1">
                      <Can permission="billing:edit">
                        <button type="button" onClick={async () => {
                          const evenPct = 100 / (brokers.length || 1);
                          const commissionVal = parseFloat(editForm.commissionValue) || 0;
                          const newSplits = brokers.map(b => ({
                            brokerId: b.brokerId,
                            brokerName: b.fullName,
                            splitPercentage: Math.Round(evenPct, 2),
                            splitAmount: Math.Round(commissionVal * evenPct / 100, 2)
                          }));
                          setBrokerSplits(newSplits);
                        }} className="text-[10px] font-bold text-blue-400 hover:text-blue-300 border border-blue-900 px-2 py-1 rounded-lg">Auto-Split Evenly</button>
                      </Can>
                      <Can permission="billing:edit">
                        <button type="button" onClick={handleUpsertBrokerSplits} className="text-[10px] font-bold text-emerald-400 hover:text-emerald-300 border border-emerald-900 px-2 py-1 rounded-lg">Save Splits</button>
                      </Can>
                    </div>
                  )}
                </div>
                <div className="md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Stage</label>
                  <select value={nextStage} onChange={(e) => setNextStage(e.target.value)} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500">
                    {DEAL_STAGES.map((s) => <option key={s} value={s}>{s.replace(/([A-Z])/g, ' $1').trim()}</option>)}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Notes</label>
                  <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" placeholder="Optional notes..." />
                </div>
              </div>
              <div className="flex gap-2">
                <Can permission="deals:edit">
                  <button type="submit" className="flex-1 py-2.5 rounded-xl bg-amber-600 text-white text-xs font-bold">Save Changes</button>
                </Can>
                <button type="button" onClick={() => { setEditingDeal(null); setNotes(''); }} className="py-2.5 px-4 rounded-xl text-xs font-bold bg-slate-800 text-slate-300">Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
