import { useEffect, useMemo, useState, useCallback, useRef } from 'react';
import {
   Briefcase, Banknote, Home, Clock,   TrendingUp, TrendingDown,
   AlertTriangle, Bell, ChevronRight, Send,
   Users, CheckCircle2, ArrowUp, ArrowDown, Calendar, Target
} from 'lucide-react';
import {
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  AreaChart, Area, Legend, PieChart, Pie, Cell,
} from 'recharts';
import { crmService } from '../services/crmService.js';
import { authService } from '../services/authService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import LoadingOverlay from './LoadingOverlay.jsx';
import { useCelebration } from './CelebrationOverlay.jsx';
import { useEpilepsySafeMode } from './EpilepsySafeMode.jsx';
import BrokerPicker from './BrokerPicker.jsx';

const formatRelativeTime = (date) => {
  const now = new Date();
  const diffMs = now - new Date(date);
  const diffMin = Math.floor(diffMs / 60000);
  const diffHr = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHr / 24);
  if (diffMin < 1) return 'just now';
  if (diffMin < 60) return `${diffMin} min ago`;
  if (diffHr < 24) return `${diffHr} hr ago`;
  if (diffDay === 1) return 'yesterday';
  if (diffDay < 7) return `${diffDay} days ago`;
  if (diffDay < 30) return `${Math.floor(diffDay / 7)} wk ago`;
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
};

const fmtCurrency = (n) => {
  const num = Number(n || 0);
  if (num >= 1_000_000) return `R ${(num / 1_000_000).toFixed(2)}M`;
  if (num >= 1_000) return `R ${(num / 1_000).toFixed(1)}K`;
  return `R ${num.toFixed(2)}`;
};
const fmtNumber = (n) => Number(n || 0).toLocaleString('en-ZA');

const SEVERITY_STYLES = {
  critical: { dot: 'bg-red-500', badge: 'bg-red-950/60 border-red-800 text-red-300', label: 'CRITICAL' },
  high:     { dot: 'bg-amber-500', badge: 'bg-amber-950/60 border-amber-800 text-amber-300', label: 'HIGH' },
  warning:  { dot: 'bg-yellow-500', badge: 'bg-yellow-950/60 border-yellow-800 text-yellow-200', label: 'WARNING' },
  info:     { dot: 'bg-indigo-500', badge: 'bg-indigo-950/60 border-indigo-800 text-indigo-300', label: 'INFO' },
};

const PIPELINE_COLORS = ['#10b981', '#6366f1', '#a78bfa', '#fbbf24', '#f87171', '#94a3b8'];

const monthLabel = (yyyymm) => {
  const [y, m] = yyyymm.split('-').map(Number);
  return new Date(y, m - 1, 1).toLocaleDateString('en-ZA', { month: 'short' });
};

const generateMonthOptions = () => {
  const options = [];
  const now = new Date();
  for (let i = 11; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    const yyyymm = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    options.push({
      value: yyyymm,
      label: d.toLocaleDateString('en-ZA', { month: 'short', year: 'numeric' })
    });
  }
  return options;
};
const MONTH_OPTIONS = generateMonthOptions();

export default function BrokerDashboard() {
  const { userRole, activeProfile } = useAuth();
  const { navigateTo, setBrochureDealTarget } = useAppUi();
  const { triggerCelebration } = useCelebration();
  const { epilepsySafeMode } = useEpilepsySafeMode();

  const isBroker = (userRole === 'Broker');
  const isAdmin = (userRole === 'AgencyAdmin' || userRole === 'PlatformAdmin');

  // Admin can override the active broker; broker is locked to themselves.
  const [pickedBroker, setPickedBroker] = useState(null);
  const effectiveBrokerId = isBroker
    ? (activeProfile?.userId || null)
    : (pickedBroker?.brokerId || null);

  const [metrics, setMetrics] = useState(null);
   const [leaderboard, setLeaderboard] = useState([]);
   const [selectedMonth, setSelectedMonth] = useState('');
   const [sortKey, setSortKey] = useState('totalGci');
  const [sortDir, setSortDir] = useState('desc');
  const [compliance, setCompliance] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
     const [busyRemind, setBusyRemind] = useState(null);
     const [userIndex, setUserIndex] = useState({});

    // Activity log state — only populated when a broker is selected.
    const [activityLog, setActivityLog] = useState([]);
    const [lastActivityTimestamp, setLastActivityTimestamp] = useState(null);
    const activityLogRef = useRef(null);

  // Build a broker-email → userId (USR-xxx) map so the "Remind Agent"
  // action can target the right user's bell. Brokers in the CRM are
  // BRK-xxx entities; their login account lives in the identity service
  // and is linked here via the shared email address.
  const loadUsers = useCallback(async () => {
    try {
      const userPage = await authService.getUsers({ page: 1, pageSize: 100 });
      const idx = {};
      (userPage && Array.isArray(userPage.items) ? userPage.items : []).forEach((u) => {
        if (u?.email) idx[String(u.email).toLowerCase()] = u.UserId || u.userId;
      });
      setUserIndex(idx);
    } catch { /* non-fatal */ }
  }, []);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [m, lb, ci] = await Promise.all([
        crmService.getBrokerMetrics(effectiveBrokerId, selectedMonth || null),
        crmService.getBrokerPerformanceLeaderboard(effectiveBrokerId),
        crmService.getComplianceInbox(effectiveBrokerId),
      ]);
      setMetrics(m || {});
      setLeaderboard(Array.isArray(lb) ? lb : []);
      setCompliance(Array.isArray(ci) ? ci : []);
    } catch (err) {
      setError(err?.message || 'Failed to load broker dashboard.');
    } finally {
      setLoading(false);
    }
  }, [effectiveBrokerId, selectedMonth]);

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { refresh(); }, [refresh]);

  // 📊 Activity log polling — only when a broker is active.
  const fetchActivityLog = useCallback(async () => {
    if (!effectiveBrokerId) return;
    try {
      const result = await crmService.getBrokerActivity(effectiveBrokerId, lastActivityTimestamp || null);
      const newItems = result.items || [];
      if (newItems.length > 0) {
        setActivityLog((prev) => [...newItems, ...prev]);
        const latestTs = newItems.reduce((max, item) => {
          const ts = new Date(item.LastStageUpdatedAt || item.CreatedAt);
          return ts > max ? ts : max;
        }, new Date(0));
        setLastActivityTimestamp(latestTs.toISOString());

        // 🎉 Celebrate deal closings — skip in epilepsy-safe mode.
        if (!epilepsySafeMode && result.newCount > 0) {
          const closingItem = newItems.find((item) => {
            const newStage = (item.NewStage || item.newStage || '').toLowerCase();
            return newStage.includes('close') || newStage.includes('sold');
          });
          if (closingItem) {
            triggerCelebration({
              brokerName: closingItem.brokerName || closingItem.BrokerName || 'Broker',
              dealAddress: closingItem.dealAddress || closingItem.DealAddress || '',
            });
          }
        }
      }
    } catch (err) {
      // soft-fail — the activity log is supplementary
    }
  }, [effectiveBrokerId, lastActivityTimestamp, triggerCelebration, epilepsySafeMode]);

   useEffect(() => {
     fetchActivityLog();
     const handle = setInterval(fetchActivityLog, 30000);
     return () => clearInterval(handle);
   }, [fetchActivityLog]);

  useEffect(() => {
    if (isAdmin) loadUsers();
  }, [isAdmin, loadUsers]);

  const requestSort = (key) => {
    if (sortKey === key) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('desc');
    }
  };

  const sortableRows = useMemo(() => {
    const sorted = [...leaderboard];
    const accessor = (r) => (sortKey === 'name' ? r.name : Number(r[sortKey] || 0));
    sorted.sort((a, b) => {
      const av = accessor(a), bv = accessor(b);
      if (av < bv) return -1;
      if (av > bv) return 1;
      return 0;
    });
    return sortDir === 'asc' ? sorted : sorted.reverse();
  }, [leaderboard, sortKey, sortDir]);

  const pipelineData = useMemo(() => (metrics?.pipelineStages || []).map((stage, index) => ({
    name: stage.stage.replace(/([a-z])([A-Z])/g, '$1 $2'),
    value: stage.count,
    color: PIPELINE_COLORS[index % PIPELINE_COLORS.length],
  })), [metrics]);

  const trendData = useMemo(() => {
    const trend = metrics?.monthlyTrend || [];
    return trend.map((t) => ({
      period: monthLabel(t.period),
      GCI: t.gci,
      CompanyRand: t.companyDollar,
    }));
  }, [metrics]);

  const handleReview = async (item) => {
    if (item.linkType === 'deal') {
      // 📝 Log a review entry in the deal's notes before navigating, so the
      // audit trail captures the broker intervention from the dashboard.
      try {
        await crmService.addDealReviewNote(Number(item.linkId), 'Reviewed from broker dashboard compliance inbox');
      } catch { /* non-fatal — navigation still proceeds */ }
      navigateTo('crm');
      setBrochureDealTarget({ dealId: Number(item.linkId), autoOpen: true });
    } else if (item.linkType === 'property') {
      navigateTo('search');
    }
  };

  const handleRemind = async (item) => {
    if (busyRemind) return;
    if (isBroker) return; // brokers don't remind themselves
    if (!effectiveBrokerId) {
      alert('Pick a broker to remind first.');
      return;
    }
    // Resolve the broker's login user id (USR-xxx) via email match so we
    // can drop the notification on the bell for exactly that user.
    const targetEmail = pickedBroker?.email || pickedBroker?.Email;
    const resolvedUserId = targetEmail ? userIndex[String(targetEmail).toLowerCase()] : null;
    if (!resolvedUserId) {
      alert('Could not resolve the broker\'s user account. They will receive a tenant-wide reminder instead.');
    }
    setBusyRemind(item.itemId);
    try {
      await crmService.createNotification({
        userId: resolvedUserId || '',
        category: item.category,
        title: `Reminder: ${item.title}`,
        body: item.body,
        linkType: item.linkType,
        linkId: item.linkId,
      });
    } catch (err) {
      alert(err?.message || 'Failed to send reminder.');
    } finally {
      setBusyRemind(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* HEADER: title + admin broker filter */}
      <div className="flex flex-col md:flex-row md:items-end md:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight flex items-center gap-3">
            <Briefcase size={28} className="text-emerald-400" />
            Broker Dashboard
          </h1>
          <p className="text-slate-400 text-sm mt-1">
            {isBroker
              ? `Performance for ${activeProfile?.name || 'you'}.`
              : 'Filter by a specific broker, or view the tenant aggregate.'}
          </p>
        </div>
        {isAdmin && (
          <div className="w-full md:w-80">
            <label className="block text-[10px] uppercase tracking-widest text-slate-500 mb-1 font-bold">Filter by broker</label>
            <BrokerPicker
              value={pickedBroker}
              onChange={setPickedBroker}
              placeholder="All brokers (tenant aggregate)"
            />
          </div>
        )}
        <div className="w-full md:w-56">
          <label className="block text-[10px] uppercase tracking-widest text-slate-500 mb-1 font-bold flex items-center gap-1">
            <Calendar size={12} /> Report Month
          </label>
          <select
            value={selectedMonth}
            onChange={(e) => setSelectedMonth(e.target.value)}
            className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"
            title="Filter the revenue trend and pipeline by month"
          >
            <option value="">Current (last 6 months)</option>
            {MONTH_OPTIONS.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </div>
      </div>

      {error && (
        <div className="p-3 bg-red-950/40 border border-red-900 text-red-300 rounded-xl text-xs flex items-center gap-2">
          <AlertTriangle size={14} /> {error}
        </div>
      )}

       {loading && (
         <LoadingOverlay label="Loading broker dashboard…" subtitle="Syncing performance metrics" />
       )}

      {/* ROW 1: KPI cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5 gap-4">
        <KpiCard
          icon={Banknote}
          label="Gross Commission Income (GCI)"
          value={fmtCurrency(metrics?.gci)}
          trend={metrics?.gciTrendPct}
          accent="emerald"
        />
        <KpiCard
          icon={Banknote}
          label="Company Share"
          value={metrics?.companyDollar == null ? 'Not configured' : fmtCurrency(metrics.companyDollar)}
          accent="emerald"
        />
        <KpiCard
          icon={Home}
          label="Agency Active Listings"
          value={metrics?.activeListings == null ? 'Unavailable' : fmtNumber(metrics.activeListings)}
          accent="indigo"
        />
        <KpiCard
          icon={Clock}
          label="Agency Average Days on Market"
          value={metrics?.averageDom == null ? 'Unavailable' : `${fmtNumber(metrics.averageDom)} d`}
          accent="slate"
        />
        {effectiveBrokerId && metrics?.monthlyTargets && metrics.monthlyTargets.length > 0 && (
          <TargetGauge
            target={metrics.monthlyTargets[metrics.monthlyTargets.length - 1]?.target}
            realised={metrics.monthlyTargets[metrics.monthlyTargets.length - 1]?.realised}
            progress={metrics.monthlyTargets[metrics.monthlyTargets.length - 1]?.progress}
          />
        )}
      </div>

      {/* ROW 2: revenue trend + pipeline funnel */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <h3 className="text-sm font-semibold text-white mb-1 flex items-center gap-2">
            <TrendingUp size={16} className="text-emerald-400" /> Monthly Revenue Trend
          </h3>
          <p className="text-xs text-slate-500 mb-4">GCI vs Company Rand — last 6 months</p>
          <div className="h-64">
            {trendData.length === 0 ? (
              <EmptyState label="No revenue data yet." />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={trendData}>
                  <CartesianGrid stroke="#1e293b" strokeDasharray="3 3" />
                  <XAxis dataKey="period" stroke="#94a3b8" fontSize={11} />
                  <YAxis stroke="#94a3b8" fontSize={11} tickFormatter={(v) => v >= 1000 ? `${(v / 1000).toFixed(0)}K` : v} />
                  <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }} formatter={(v) => fmtCurrency(v)} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Area type="monotone" dataKey="GCI" stroke="#10b981" strokeWidth={2.5} dot={{ r: 3 }} fill="#10b981" fillOpacity={0.15} />
                  <Area type="monotone" dataKey="CompanyRand" stroke="#6366f1" strokeWidth={2.5} dot={{ r: 3 }} fill="#6366f1" fillOpacity={0.15} />
                </AreaChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <h3 className="text-sm font-semibold text-white mb-1 flex items-center gap-2">
            <Briefcase size={16} className="text-indigo-400" /> Pipeline Funnel
          </h3>
          <p className="text-xs text-slate-500 mb-4">Deal status split for the current view</p>
          <div className="h-64 flex items-center justify-center">
            {pipelineData.every((p) => p.value === 0) ? (
              <EmptyState label="No deals in the pipeline." />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={pipelineData} dataKey="value" nameKey="name" innerRadius={50} outerRadius={90} paddingAngle={2}>
                    {pipelineData.map((entry, idx) => (
                      <Cell key={idx} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                </PieChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>
      </div>

      {/* ROW 4: leaderboard + activity log (when broker selected) or compliance inbox */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
         <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
           <h3 className="text-sm font-semibold text-white mb-1 flex items-center gap-2">
             <Users size={16} className="text-emerald-400" /> {effectiveBrokerId ? 'Broker Activity Log' : 'Agent Performance'}
           </h3>
           <p className="text-xs text-slate-500 mb-4">
             {effectiveBrokerId ? 'Recent deal stage changes (polling every 30s)' : 'Realised commission on assigned deals. Shared deals appear for each broker; rows are not additive.'}
           </p>
           {effectiveBrokerId ? (
             activityLog.length === 0 ? (
               <EmptyState label="No recent activity." />
             ) : (
               <div className="overflow-auto max-h-80 rounded-xl border border-slate-800">
                 <table className="w-full text-xs">
                   <thead className="sticky top-0 bg-slate-900 z-10 border-b border-slate-800">
                     <tr className="text-left text-slate-500">
                       <th className="py-2 px-3 font-semibold">Broker</th>
                       <th className="py-2 px-3 font-semibold">Deal</th>
                       <th className="py-2 px-3 font-semibold">Stage Change</th>
                     </tr>
                   </thead>
                   <tbody>
                      {activityLog.map((entry, idx) => {
                        const rawDate = entry.LastStageUpdatedAt || entry.CreatedAt;
                        const ts = rawDate ? new Date(rawDate) : new Date(0);
                        const safeKey = ts.getTime() > 0 ? ts.toISOString() : `item-${idx}`;
                        return (
                          <tr key={`${entry.dealId}-${entry.BrokerId || entry.brokerId}-${safeKey}`} className="border-b border-slate-800/50">
                            <td className="py-2.5 px-3 text-white font-medium">{entry.brokerName || entry.BrokerName || 'Unknown'}</td>
                            <td className="py-2.5 px-3 text-slate-300 truncate">{entry.dealAddress || entry.DealAddress || entry.dealId}</td>
                            <td className="py-2.5 px-3 text-slate-400">
                              {entry.PreviousStage !== undefined && entry.NewStage !== undefined
                                ? `${entry.PreviousStage || entry.previousStage} → ${entry.NewStage || entry.newStage}`
                                : <span className="text-slate-500">Updated {ts.getTime() > 0 ? formatRelativeTime(ts) : 'recently'}</span>}
                           </td>
                         </tr>
                       );
                     })}
                   </tbody>
                 </table>
               </div>
             )
           ) : leaderboard.length === 0 ? (
             <EmptyState label="No agents on the roster yet." />
           ) : (
             <div className="overflow-auto max-h-80 rounded-xl border border-slate-800">
               <table className="w-full text-xs">
                 <thead className="sticky top-0 bg-slate-900 z-10">
                   <tr className="text-left text-slate-500 border-b border-slate-800">
                     <SortHeader label="Agent" field="name" active={sortKey === 'name'} dir={sortDir === 'asc'} onRequest={requestSort} right={false} />
                     <SortHeader label="Active Listings" field="activeListings" active={sortKey === 'activeListings'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                     <SortHeader label="Deals Closed" field="dealsClosed" active={sortKey === 'dealsClosed'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                     <SortHeader label="Assigned Deal GCI" field="totalGci" active={sortKey === 'totalGci'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                   </tr>
                 </thead>
                 <tbody>
                   {sortableRows.map((row) => (
                     <tr key={row.brokerId} className={`border-b border-slate-800/50 ${pickedBroker?.brokerId === row.brokerId ? 'bg-blue-950/30' : 'hover:bg-slate-800/30'}`}>
                       <td className="py-2.5 px-3 text-white font-medium">{row.name}</td>
                       <td className="py-2.5 px-3 text-right text-slate-300">{row.activeListings == null ? 'Unavailable' : fmtNumber(row.activeListings)}</td>
                       <td className="py-2.5 px-3 text-right text-slate-300">{fmtNumber(row.dealsClosed)}</td>
                       <td className="py-2.5 px-3 text-right font-semibold text-emerald-400">{fmtCurrency(row.totalGci)}</td>
                     </tr>
                   ))}
                 </tbody>
               </table>
             </div>
           )}
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <h3 className="text-sm font-semibold text-white mb-1 flex items-center gap-2">
            <Bell size={16} className="text-amber-400" /> Compliance & Alerts
          </h3>
          <p className="text-xs text-slate-500 mb-4">Items requiring immediate broker intervention</p>
          {compliance.length === 0 ? (
            <div className="p-4 bg-emerald-950/30 border border-emerald-900/50 rounded-xl text-xs text-emerald-300 flex items-center gap-2">
              <CheckCircle2 size={14} /> All clear — no urgent items.
            </div>
          ) : (
            <ul className="space-y-2 max-h-96 overflow-y-auto">
              {compliance.map((item) => {
                const sev = SEVERITY_STYLES[item.severity] || SEVERITY_STYLES.info;
                return (
                  <li key={item.itemId} className="p-3 bg-slate-950 border border-slate-800 rounded-xl flex flex-col gap-2">
                    <div className="flex items-start gap-2">
                      <span className="text-lg leading-none mt-0.5">{item.icon || '•'}</span>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className={`text-[9px] font-bold uppercase tracking-widest border rounded-md px-1.5 py-0.5 ${sev.badge}`}>
                            {sev.label}
                          </span>
                          <span className="text-sm font-semibold text-white truncate">{item.title}</span>
                        </div>
                        <p className="text-[11px] text-slate-400 mt-1 truncate">{item.body}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 justify-end">
                      <button
                        type="button"
                        onClick={() => handleRemind(item)}
                        disabled={!effectiveBrokerId || !!busyRemind}
                        className="text-[11px] font-semibold flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                      >
                        {busyRemind === item.itemId ? (
                          <>Sending…</>
                        ) : (
                          <><Send size={11} /> Remind Agent</>
                        )}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleReview(item)}
                        className="text-[11px] font-semibold flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white cursor-pointer"
                      >
                        Review <ChevronRight size={11} />
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>

    </div>
  );
}

function SortHeader({ label, field, active, dir, onRequest, right = false }) {
  return (
    <th className={`py-2 px-3 font-semibold cursor-pointer select-none ${right ? 'text-right' : 'text-left'}`} onClick={() => onRequest(field)}>
      <button type="button" className={`inline-flex items-center gap-1 font-semibold ${active ? 'text-white' : 'text-slate-500 hover:text-slate-300'}`}>
        {label}
        {active && (dir ? <ArrowUp size={10} /> : <ArrowDown size={10} />)}
      </button>
    </th>
  );
}

function KpiCard({ icon: Icon, label, value, trend, accent = 'emerald' }) {
  const accentMap = {
    emerald: 'text-emerald-400 bg-emerald-950/40 border-emerald-900/50',
    indigo:  'text-indigo-400 bg-indigo-950/40 border-indigo-900/50',
    slate:   'text-slate-300 bg-slate-800/60 border-slate-700',
  };
  const trendIsPos = Number(trend) > 0;
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col gap-3">
      <div className="flex items-start justify-between">
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center border ${accentMap[accent]}`}>
          <Icon size={18} />
        </div>
        {trend !== undefined && trend !== null && Number(trend) !== 0 && (
          <span className={`text-[10px] font-bold px-1.5 py-1 rounded-md flex items-center gap-0.5 ${trendIsPos ? 'text-emerald-300 bg-emerald-950/40 border border-emerald-900' : 'text-red-300 bg-red-950/40 border border-red-900'}`}>
            {trendIsPos ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
            {trendIsPos ? '+' : ''}{trend}% vs last month
          </span>
        )}
      </div>
      <div>
        <div className="text-[10px] uppercase tracking-widest text-slate-500 font-bold">{label}</div>
        <div className="text-2xl font-bold text-white mt-1">{value}</div>
      </div>
    </div>
  );
}

function EmptyState({ label }) {
  return (
    <div className="h-full w-full flex items-center justify-center text-xs text-slate-500">
      {label}
    </div>
  );
}

function TargetGauge({ target, realised, progress }) {
  const hasTarget = target && target > 0;
  const progressValue = Math.min(Math.max(progress || 0, 0), 100);
  const isOverTarget = progressValue >= 100;

  const circumference = 2 * Math.PI * 45; // radius = 45
  const strokeDashoffset = circumference * (1 - progressValue / 100);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col items-center">
      <h3 className="text-sm font-semibold text-white mb-4 flex items-center gap-2">
        <Target size={16} className="text-emerald-400" /> Monthly Target Progress
      </h3>
      {hasTarget ? (
        <div className="relative w-40 h-40 flex items-center justify-center">
          <svg width="100" height="100" viewBox="0 0 100 100" className="transform -rotate-90">
            {/* Background circle */}
            <circle
              cx="50"
              cy="50"
              r="45"
              stroke="#1e293b"
              strokeWidth="8"
              fill="none"
            />
            {/* Progress circle */}
            <circle
              cx="50"
              cy="50"
              r="45"
              stroke={isOverTarget ? '#10b981' : '#6366f1'}
              strokeWidth="8"
              fill="none"
              strokeDasharray={circumference}
              strokeDashoffset={strokeDashoffset}
              strokeLinecap="round"
              className="transition-all duration-500 ease-out"
              style={{ filter: isOverTarget ? 'drop-shadow(0 0 6px #10b981)' : 'none' }}
            />
          </svg>
          <div className="absolute inset-0 flex flex-col items-center justify-center">
            <span className={`text-2xl font-bold text-white ${isOverTarget ? 'text-emerald-400' : ''}`}>
              {progressValue.toFixed(1)}%
            </span>
            <span className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">
              {isOverTarget ? 'Target Exceeded' : 'of Target'}
            </span>
          </div>
        </div>
      ) : (
        <div className="w-40 h-40 flex items-center justify-center">
          <div className="text-center">
            <Target size={32} className="mx-auto text-slate-600 mb-2" />
            <p className="text-xs text-slate-500">No target set</p>
            <p className="text-[10px] text-slate-600 mt-1">Set a target in Admin Workspace</p>
          </div>
        </div>
      )}
      <div className="mt-4 w-full space-y-2 text-center">
        <div className="flex justify-between text-xs text-slate-400">
          <span>Target</span>
          <span className="font-semibold text-white">{hasTarget ? `R ${(target / 1000).toFixed(1)}K` : '—'}</span>
        </div>
        <div className="flex justify-between text-xs text-slate-400">
          <span>Realised</span>
          <span className="font-semibold text-emerald-400">{hasTarget ? `R ${(realised / 1000).toFixed(1)}K` : 'R 0'}</span>
        </div>
        {hasTarget && (
          <div className="flex justify-between text-xs text-slate-400 border-t border-slate-800 pt-2">
            <span>Remaining</span>
            <span className="font-semibold text-amber-400">
              R {((target - realised) / 1000).toFixed(1)}K
            </span>
          </div>
        )}
      </div>
    </div>
  );
}


