import { useEffect, useMemo, useState, useCallback } from 'react';
import {
  Briefcase, Banknote, Home, Clock,   TrendingUp, TrendingDown,
  AlertTriangle, Bell, ChevronRight, Send,
  Users, CheckCircle2, ArrowUp, ArrowDown
} from 'lucide-react';
import {
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  LineChart, Line, Legend, PieChart, Pie, Cell
} from 'recharts';
import { crmService } from '../services/crmService.js';
import { authService } from '../services/authService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import BrokerPicker from './BrokerPicker.jsx';

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

export default function BrokerDashboard() {
  const { userRole, activeProfile } = useAuth();
  const { navigateTo, setBrochureDealTarget } = useAppUi();

  const isBroker = (userRole === 'Broker');
  const isAdmin = (userRole === 'AgencyAdmin' || userRole === 'PlatformAdmin');

  // Admin can override the active broker; broker is locked to themselves.
  const [pickedBroker, setPickedBroker] = useState(null);
  const effectiveBrokerId = isBroker
    ? (activeProfile?.userId || null)
    : (pickedBroker?.brokerId || null);

  const [metrics, setMetrics] = useState(null);
   const [leaderboard, setLeaderboard] = useState([]);
  const [sortKey, setSortKey] = useState('totalGci');
  const [sortDir, setSortDir] = useState('desc');
  const [compliance, setCompliance] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busyRemind, setBusyRemind] = useState(null);
  const [userIndex, setUserIndex] = useState({});

  // Build a broker-email → userId (USR-xxx) map so the "Remind Agent"
  // action can target the right user's bell. Brokers in the CRM are
  // BRK-xxx entities; their login account lives in the identity service
  // and is linked here via the shared email address.
  const loadUsers = useCallback(async () => {
    try {
      const users = await authService.getUsers();
      const idx = {};
      (Array.isArray(users) ? users : []).forEach((u) => {
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
        crmService.getBrokerMetrics(effectiveBrokerId),
        crmService.getBrokerLeaderboard(effectiveBrokerId),
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
  }, [effectiveBrokerId]);

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { refresh(); }, [refresh]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
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

  // Pipeline split derived from leaderboard (closedCount per month + Negotiation count from compliance).
  const pipelineData = useMemo(() => {
    const trend = metrics?.monthlyTrend || [];
    const closedThisMonth = trend.length ? trend[trend.length - 1].closedCount : 0;
    const awaiting = compliance.filter((c) => c.category === 'compliance.awaiting_signature').length;
    const expiringCritical = compliance.filter((c) => c.severity === 'critical' || c.severity === 'high').length;
    return [
      { name: 'New / Active',  value: Math.max(0, (metrics?.dealCount || 0) - closedThisMonth - awaiting), color: PIPELINE_COLORS[1] },
      { name: 'Negotiation',   value: awaiting, color: PIPELINE_COLORS[2] },
      { name: 'Closed Won',    value: closedThisMonth, color: PIPELINE_COLORS[0] },
      { name: 'At Risk',       value: expiringCritical, color: PIPELINE_COLORS[4] },
    ];
  }, [metrics, compliance]);

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
      </div>

      {error && (
        <div className="p-3 bg-red-950/40 border border-red-900 text-red-300 rounded-xl text-xs flex items-center gap-2">
          <AlertTriangle size={14} /> {error}
        </div>
      )}

      {/* ROW 1: KPI cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard
          icon={Banknote}
          label="Gross Commission Income (GCI)"
          value={fmtCurrency(metrics?.gci)}
          trend={metrics?.gciTrendPct}
          accent="emerald"
        />
        <KpiCard
          icon={Banknote}
          label="Company Rand (Net)"
          value={fmtCurrency(metrics?.companyDollar)}
          accent="emerald"
        />
        <KpiCard
          icon={Home}
          label="Total Active Listings"
          value={fmtNumber(metrics?.activeListings)}
          accent="indigo"
        />
        <KpiCard
          icon={Clock}
          label="Average Days on Market"
          value={`${fmtNumber(metrics?.averageDom)} d`}
          accent="slate"
        />
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
                <LineChart data={trendData}>
                  <CartesianGrid stroke="#1e293b" strokeDasharray="3 3" />
                  <XAxis dataKey="period" stroke="#94a3b8" fontSize={11} />
                  <YAxis stroke="#94a3b8" fontSize={11} tickFormatter={(v) => v >= 1000 ? `${(v / 1000).toFixed(0)}K` : v} />
                  <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }} formatter={(v) => fmtCurrency(v)} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Line type="monotone" dataKey="GCI" stroke="#10b981" strokeWidth={2.5} dot={{ r: 3 }} />
                  <Line type="monotone" dataKey="CompanyRand" stroke="#6366f1" strokeWidth={2.5} dot={{ r: 3 }} />
                </LineChart>
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

      {/* ROW 3: leaderboard + compliance inbox */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <h3 className="text-sm font-semibold text-white mb-1 flex items-center gap-2">
            <Users size={16} className="text-emerald-400" /> Agent Performance
          </h3>
          <p className="text-xs text-slate-500 mb-4">Sortable leaderboard for the active tenant</p>
          {leaderboard.length === 0 ? (
            <EmptyState label="No agents on the roster yet." />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="text-left text-slate-500 border-b border-slate-800">
                    <SortHeader label="Agent" field="name" active={sortKey === 'name'} dir={sortDir === 'asc'} onRequest={requestSort} right={false} />
                    <SortHeader label="Active Listings" field="activeListings" active={sortKey === 'activeListings'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                    <SortHeader label="Deals Closed" field="dealsClosed" active={sortKey === 'dealsClosed'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                    <SortHeader label="GCI Generated" field="totalGci" active={sortKey === 'totalGci'} dir={sortDir === 'asc'} onRequest={requestSort} right={true} />
                  </tr>
                </thead>
                <tbody>
                  {sortableRows.map((row) => (
                    <tr key={row.brokerId} className={`border-b border-slate-800/50 ${pickedBroker?.brokerId === row.brokerId ? 'bg-blue-950/30' : 'hover:bg-slate-800/30'}`}>
                      <td className="py-2.5 text-white font-medium">{row.name}</td>
                      <td className="py-2.5 text-right text-slate-300">{fmtNumber(row.activeListings)}</td>
                      <td className="py-2.5 text-right text-slate-300">{fmtNumber(row.dealsClosed)}</td>
                      <td className="py-2.5 text-right font-semibold text-emerald-400">{fmtCurrency(row.totalGci)}</td>
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

      {loading && (
        <div className="text-xs text-slate-500 text-center">Refreshing broker metrics…</div>
      )}
    </div>
  );
}

function SortHeader({ label, field, active, dir, onRequest, right = false }) {
  return (
    <th className={`py-2 font-semibold cursor-pointer select-none ${right ? 'text-right' : 'text-left'}`} onClick={() => onRequest(field)}>
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
