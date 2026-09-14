import { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useAppUi } from '../contexts/AppUiContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
import {   Building2, Users, Briefcase, Home, Bell, ShieldCheck, Layers, BarChart2, Banknote, TrendingUp, PieChart as PieIcon, Award, AlertTriangle } from 'lucide-react';
import LoadingOverlay from './LoadingOverlay.jsx';
import { BarChart, Bar, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell, LineChart, Line } from 'recharts';
import { crmService } from '../services/crmService.js';
import { propertyService } from '../services/propertyService.js';

const roleLabels = { PlatformAdmin: 'Platform Administrator', AgencyAdmin: 'Agency Administrator', Broker: 'Broker', Owner: 'Property Owner', Tenant: 'Tenant' };
const STATUS_COLORS = { Active: '#22c55e', Review: '#f59e0b', Inactive: '#ef4444', Operational: '#22c55e' };
const ROLE_COLORS = { PlatformAdmin: '#7c3aed', AgencyAdmin: '#3b82f6', Broker: '#14b8a4', Owner: '#f97316', Tenant: '#0ea5e9' };
const DEAL_COLORS = { DiscoveryNeedsAnalysis: '#0ea5e9', Presentation: '#3b82f6', ProposalQuoteSent: '#a78bfa', Negotiation: '#f59e0b', ClosedWon: '#22c55e', ClosedLost: '#ef4444' };
const TYPE_COLORS = { Residential: '#22c55e', Commercial: '#3b82f6', Retail: '#f97316', Industrial: '#6b7280' };
const next = (role) => role === 'Tenant' ? 'operations' : role === 'Owner' ? 'crm' : role === 'Broker' ? 'crm' : 'admin';

const formatTitle = (raw) => {
  if (!raw) return '';
  const special = { DiscoveryNeedsAnalysis: 'Discovery / Needs Anal.', ProposalQuoteSent: 'Proposal / Quote Sent', ClosedWon: 'Closed Won', ClosedLost: 'Closed Lost' };
  if (special[raw]) return special[raw];
  return String(raw).replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase());
};

const ChartCard = ({ title, icon: Icon, children }) => (
  <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl">
    <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.12em] text-[10px] mb-3">
      <Icon size={12} className="text-blue-500" /> {title}
    </div>
    <div className="h-56 w-full">{children}</div>
  </div>
);

const COLOR_FALLBACK = ['#3b82f6', '#14b8a4', '#f59e0b', '#ef4444', '#a78bfa', '#f97316'];

const cellFor = (colors, palette, entry, i) => <Cell key={i} fill={(colors && colors[entry.name]) || palette[i % (palette.length || 1)] || COLOR_FALLBACK[i % COLOR_FALLBACK.length]} />;

const renderChart = (chart) => {
  const { kind, data, colors } = chart;
  if (!data || !data.length) return <div className="text-[11px] text-slate-500">No data for this metric.</div>;
  const palette = Object.values(colors || {});
  if (kind === 'pie' || kind === 'donut') {
    const inner = kind === 'donut' ? 46 : 38;
    const radius = kind === 'donut' ? 78 : 72;
    return (
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={data} dataKey="value" nameKey="name" cx="50%" cy={kind === 'donut' ? '48%' : '45%'} outerRadius={radius} innerRadius={inner} label={({ name, value }) => `${name}: ${value}`}>
            {data.map((entry, i) => cellFor(colors, palette, entry, i))}
          </Pie>
          <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#cbd5e1', fontSize: 11 }} />
          <Legend verticalAlign="bottom" height={32} iconSize={10} wrapperStyle={{ fontSize: 10, color: '#94a3b8' }} />
        </PieChart>
      </ResponsiveContainer>
    );
  }
  if (kind === 'line') {
    const lines = chart.lines || [{ dataKey: 'value', color: '#38bdf8', name: 'Value' }];
    return (
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
          <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#94a3b8' }} />
          <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3b8' }} />
          <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#cbd5e1', fontSize: 11 }} />
          <Legend wrapperStyle={{ fontSize: 10, color: '#94a3b8' }} />
          {lines.map((l, i) => (
            <Line key={i} type="monotone" dataKey={l.dataKey} stroke={l.color} strokeWidth={2} name={l.name} dot={{ r: 2 }} />
          ))}
        </LineChart>
      </ResponsiveContainer>
    );
  }
  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={data} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
        <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#94a3b8' }} />
        <YAxis hide />
        <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#cbd5e1', fontSize: 11 }} />
        <Legend wrapperStyle={{ fontSize: 10, color: '#94a3b8' }} />
        <Bar dataKey="value" radius={[0, 0, 0, 0]}>
          {data.map((entry, i) => cellFor(colors, palette, entry, i))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
};

const PROPERTY_TYPES = ['Residential', 'Commercial', 'Retail', 'Industrial'];

export default function RoleDashboard() {
  const { activeProfile: profile } = useAuth();
  const { navigateTo: onNavigate } = useAppUi();
  const { activeTenantId } = useTenant();
  const role = profile?.role || 'Broker';
  const [metrics, setMetrics] = useState([]);
  const [charts, setCharts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [pipelineSummary, setPipelineSummary] = useState(null);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    const load = async () => {
      setIsLoading(true);
      try {
        if (role === 'PlatformAdmin') {
          const [agencies, overview, marketShare, commissionStages] = await Promise.all([
            crmService.getAgencies(),
            crmService.getCRMReportOverview(),
            crmService.getPropertyMarketShare(),
            crmService.getCommissionByStage()
          ]);
          setMetrics([
            { label: 'Agencies', value: agencies.totalCount || agencies.length, icon: Building2 },
            { label: 'Platform controls', value: 'Active', icon: ShieldCheck },
            { label: 'Closed-won commission', value: `R ${Number(overview.totalRealisedCommission || 0).toLocaleString()}`, icon: Banknote }
          ]);
            // 📊 Server-side aggregates (operationalStatus, isTrial, hasParent) are
            // computed over the ENTIRE filtered universe, not the 20-row page.
            // The pie chart below must reflect every agency, not just page 1.
            const agencyAggregates = agencies.aggregates || {};
            const statusBuckets = agencyAggregates.operationalStatus || {};
            setCharts([
              { title: 'Agency operational status', icon: ShieldCheck, kind: 'pie', data: Object.entries(statusBuckets).map(([name, value]) => ({ name, value })), colors: STATUS_COLORS },
            { title: 'Property type market share', icon: PieIcon, kind: 'pie', data: marketShare, colors: TYPE_COLORS, colorKey: true },
             { title: 'Monthly platform revenue', icon: TrendingUp, kind: 'bar', data: (overview.monthlyBreakdown || []).map((m) => ({ name: m.period, value: Number(m.revenue || 0) })), colors: { default: '#3b82f6' } },
            { title: 'Pipeline commission by stage', icon: BarChart2, kind: 'bar', data: (commissionStages || []).map((c) => ({ name: formatTitle(c.period), value: Number(c.revenue || 0) })), colors: DEAL_COLORS }
          ]);
         } else if (role === 'AgencyAdmin') {
          // 🎯 AgencyAdmin gets tenant-scoped pipeline summary, monthly trends, and commission data
          const [brokers, boards, summary, overview, commissionStages, leaderboard] = await Promise.all([
            crmService.getBrokers(),
            crmService.getCRMBoards(),
            crmService.getPipelineReportSummary(),
            crmService.getCRMReportOverview(),
            crmService.getCommissionByStage(),
            crmService.getBrokerLeaderboard()
          ]);
          setMetrics([
            { label: 'Brokers', value: brokers.totalCount || brokers.length, icon: Users },
            { label: 'Agency deals', value: boards.dealCount || boards.verifiedDeals?.length || 0, icon: Briefcase },
            { label: 'Finalised closed', value: summary.finalizedClosedCount || 0, icon: Award }
          ]);
          setPipelineSummary({
            activeLeadsCount: summary.activeLeadsCount,
            valuationsInProcessCount: summary.valuationsInProcessCount,
            conveyancingAtDeedsCount: summary.conveyancingAtDeedsCount,
            finalizedClosedCount: summary.finalizedClosedCount,
            totalPipelineEstimatedValue: summary.totalPipelineEstimatedValue
          });
            // 📊 Broker role distribution uses server-side aggregates so the pie
            // reflects every broker in the active tenant scope, not just the
            // 20 returned for the current page.
            const brokerAggregates = brokers.aggregates || {};
            const brokerRoleBuckets = brokerAggregates.role || {};
            setCharts([
              { title: 'Broker role distribution', icon: Users, kind: 'pie', data: Object.entries(brokerRoleBuckets).map(([name, value]) => ({ name, value })), colors: ROLE_COLORS },
            { title: 'Broker leaderboard (pipeline value)', icon: Award, kind: 'bar', data: (leaderboard || []).map((b) => ({ name: b.agent, value: Number(b.totalValue) })), colors: { default: '#3b82f6' } },
            { title: 'Pipeline funnel', icon: Layers, kind: 'bar', data: [
              { name: 'Active Leads', value: summary.activeLeadsCount },
              { name: 'Valuations In Process', value: summary.valuationsInProcessCount },
              { name: 'Conveyancing At Deeds', value: summary.conveyancingAtDeedsCount },
              { name: 'Finalised Closed', value: summary.finalizedClosedCount }
            ], colors: { default: '#10b981' } },
            { title: 'Monthly trends', icon: TrendingUp, kind: 'line', data: (overview?.monthlyBreakdown || []).map((m) => ({ name: m.period, value: Number(m.leads || 0) })), lines: [{ dataKey: 'value', color: '#38bdf8', name: 'Leads' }] },
            { title: 'Revenue by month', icon: TrendingUp, kind: 'line', data: (overview?.monthlyBreakdown || []).map((m) => ({ name: m.period, value: Number(m.revenue || 0) })), lines: [{ dataKey: 'value', color: '#4ade80', name: 'Revenue' }] },
            { title: 'Commission by stage', icon: BarChart2, kind: 'bar', data: (commissionStages || []).map((c) => ({ name: formatTitle(c.period), value: Number(c.revenue || 0) })), colors: DEAL_COLORS }
          ]);
        } else if (role === 'Broker') {
          const [boards, properties, commissionStages] = await Promise.all([
            crmService.getCRMBoards(),
            propertyService.searchProperties([], 'All', null, null),
            crmService.getCommissionByStage()
          ]);
          setMetrics([
            { label: 'My deals', value: boards.dealCount || boards.verifiedDeals?.length || 0, icon: Briefcase },
            { label: 'Available properties', value: properties.totalCount || properties.length, icon: Home },
            { label: 'Closed-won commission', value: `R ${Number((commissionStages || []).find((c) => c.period === 'ClosedWon')?.revenue || 0).toLocaleString()}`, icon: Banknote }
          ]);
            // 📊 Deal-stage distribution uses server-side aggregates so the pie
            // reflects every deal the broker owns in the active tenant scope,
            // not just the 20 returned for the current page.
            const dealStageBuckets = boards.aggregates?.dealStage || {};
            setCharts([
              { title: 'My deal pipeline stages', icon: Briefcase, kind: 'pie', data: Object.entries(dealStageBuckets).map(([name, value]) => ({ name: formatTitle(name), value })), colors: DEAL_COLORS },
            { title: 'Commission by stage', icon: Banknote, kind: 'donut', data: (commissionStages || []).map((c) => ({ name: formatTitle(c.period), value: Number(c.revenue || 0) })) },
            { title: 'My deal value pipeline', icon: Home, kind: 'bar', data: (boards.verifiedDeals || []).slice(0, 8).map((d) => ({ name: `DL-${d.dealId}`, value: Number(d.transactionValue || 0) })), colors: DEAL_COLORS }
          ]);
        } else if (role === 'Owner') {
          const ots = await crmService.getOwnerTenantRecords();
          // 📊 Server-side aggregates (contactType, primaryLocation) reflect every
          // record in the active tenant scope, not just the 20 returned for the
          // current page — keeps the chart consistent with the rest of the app.
          const otsAggregates = ots.aggregates || {};
          const recordTypeBuckets = otsAggregates.contactType || {};
          const locationBuckets = otsAggregates.primaryLocation || {};
          setMetrics([{ label: 'Interested parties', value: ots.totalCount || ots.length, icon: Users }, { label: 'Property operations', value: 'Active', icon: Home }]);
          setCharts([
            { title: 'Contact type distribution', icon: Users, kind: 'pie', data: Object.entries(recordTypeBuckets).map(([name, value]) => ({ name, value })), colors: TYPE_COLORS },
            { title: 'Primary location breakdown', icon: Home, kind: 'bar', data: Object.entries(locationBuckets).map(([name, value]) => ({ name, value })), colors: { default: '#3b82f6' } }
          ]);
        } else {
          const ops = await crmService.getOperationalRecords('maintenance');
          // 📊 Request status breakdown uses server-side aggregates (status) so
          // the pie reflects every maintenance call in the active tenant scope,
          // not just the 20 returned for the current page.
          const statusBuckets = ops.aggregates?.status || {};
          setMetrics([{ label: 'Lease access', value: 'Active', icon: Home }, { label: 'Requests', value: ops.totalCount || ops.length, icon: Bell }]);
          setCharts([
            { title: 'Request status breakdown', icon: Bell, kind: 'pie', data: Object.entries(statusBuckets).map(([name, value]) => ({ name, value })), colors: STATUS_COLORS }
          ]);
        }
       } catch (err) {
         setErrorMessage(err?.message || 'Failed to load dashboard analytics. Please try again.');
         setMetrics([]); setCharts([]);
       } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [role, activeTenantId]);

  return (
    <div className="space-y-6">
      <div>
        <p className="text-[10px] uppercase tracking-[.2em] text-slate-500">{roleLabels[role] || role}</p>
        <h3 className="text-2xl font-bold text-white">Welcome back, {profile?.name || 'User'}</h3>
        <p className="text-sm text-slate-400 mt-1">Your workspace only surfaces information appropriate to your role.</p>
      </div>

      {errorMessage && (
        <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium flex items-center gap-2">
          <AlertTriangle size={14} /> {errorMessage}
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {metrics.map(({ label, value, icon: Icon }) => (
          <div key={label} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex items-center justify-between">
            <div><p className="text-xs uppercase tracking-wider text-slate-500">{label}</p><p className="mt-2 text-2xl font-black text-white">{value}</p></div>
            <Icon className="text-blue-400" />
          </div>
        ))}
      </div>

       {pipelineSummary && (
        <div>
          <h4 className="text-xs font-bold text-slate-500 uppercase tracking-[0.2em] mb-3">Pipeline Summary</h4>
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-blue-400">{pipelineSummary.activeLeadsCount || 0}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Active Leads</div>
            </div>
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-amber-400">{pipelineSummary.valuationsInProcessCount || 0}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Valuations In Process</div>
            </div>
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-violet-400">{pipelineSummary.conveyancingAtDeedsCount || 0}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Conveyancing</div>
            </div>
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-2xl font-bold text-emerald-400">{pipelineSummary.finalizedClosedCount || 0}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Closed Won</div>
            </div>
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
              <div className="text-sm font-black text-white">R {Number(pipelineSummary.totalPipelineEstimatedValue || 0).toLocaleString()}</div>
              <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">Pipeline Value</div>
            </div>
          </div>
        </div>
       )}

       {isLoading ? (
         <LoadingOverlay label="Loading role-specific analytics…" subtitle="Aggregating platform-wide metrics" />
       ) : (
        <div className="grid gap-4 grid-cols-2">
          {charts.map((c) => (
            <ChartCard key={c.title} title={c.title} icon={c.icon}>
              {renderChart(c)}
            </ChartCard>
          ))}
        </div>
      )}

      <button type="button" onClick={() => onNavigate(next(role))} className="px-4 py-2.5 bg-blue-600 rounded-xl text-xs font-bold text-white">Open my workspace</button>
    </div>
  );
}
