import React, { useEffect, useState } from 'react';
import { Building2, Users, Briefcase, Home, Bell, ShieldCheck, Layers, BarChart2, DollarSign, TrendingUp, PieChart as PieIcon, Award } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import { crmService } from '../services/crmService.js';
import { propertyService } from '../services/propertyService.js';

const roleLabels = { PlatformAdmin: 'Platform Administrator', AgencyAdmin: 'Agency Administrator', Broker: 'Broker', Owner: 'Property Owner', Tenant: 'Tenant' };
const STATUS_COLORS = { Active: '#22c55e', Review: '#f59e0b', Inactive: '#ef4444', Operational: '#22c55e' };
const ROLE_COLORS = { PlatformAdmin: '#7c3aed', AgencyAdmin: '#3b82f6', Broker: '#14b8a4', Owner: '#f97316', Tenant: '#0ea5e9' };
const DEAL_COLORS = { DiscoveryNeedsAnalysis: '#0ea5e9', Presentation: '#3b82f6', ProposalQuoteSent: '#a78bfa', Negotiation: '#f59e0b', ClosedWon: '#22c55e', ClosedLost: '#ef4444' };
const TYPE_COLORS = { Residential: '#22c55e', Commercial: '#3b82f6', Retail: '#f97316', Industrial: '#6b7280' };
const next = (role) => role === 'Tenant' ? 'operations' : role === 'Owner' ? 'owners-tenants' : role === 'Broker' ? 'crm' : 'admin';

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

export default function RoleDashboard({ profile, onNavigate }) {
  const role = profile?.role || 'Broker';
  const [metrics, setMetrics] = useState([]);
  const [charts, setCharts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

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
            { label: 'Agencies', value: agencies.length, icon: Building2 },
            { label: 'Platform controls', value: 'Active', icon: ShieldCheck },
            { label: 'Closed-won commission', value: `R ${Number(overview.totalRealisedCommission || 0).toLocaleString()}`, icon: DollarSign }
          ]);
          setCharts([
            { title: 'Agency operational status', icon: ShieldCheck, kind: 'pie', data: Object.entries((agencies || []).reduce((a, x) => { const k = x.operationalStatus || 'Active'; a[k] = (a[k] || 0) + 1; return a; }, {})).map(([name, value]) => ({ name, value })), colors: STATUS_COLORS },
            { title: 'Property type market share', icon: PieIcon, kind: 'pie', data: marketShare, colors: TYPE_COLORS, colorKey: true },
            { title: 'Monthly platform revenue', icon: TrendingUp, kind: 'bar', data: (overview.monthlyBreakdown || []).map((m) => ({ name: m.period, value: Number(m.revenue || 0) })), colors: { default: '#3b82f6' } },
            { title: 'Commission realised by stage', icon: BarChart2, kind: 'bar', data: commissionStages, colors: DEAL_COLORS }
          ]);
        } else if (role === 'AgencyAdmin') {
          const [brokers, boards, summary] = await Promise.all([crmService.getBrokers(), crmService.getCRMBoards(), crmService.getPipelineReportSummary()]);
          setMetrics([
            { label: 'Brokers', value: brokers.length, icon: Users },
            { label: 'Agency deals', value: boards.verifiedDeals?.length || 0, icon: Briefcase },
            { label: 'Finalised closed', value: summary.finalizedClosedCount || 0, icon: Award }
          ]);
          setCharts([
            { title: 'Broker role distribution', icon: Users, kind: 'pie', data: Object.entries((brokers || []).reduce((a, b) => { const k = b.role || 'Broker'; a[k] = (a[k] || 0) + 1; return a; }, {})).map(([name, value]) => ({ name, value })), colors: ROLE_COLORS },
            { title: 'Broker leaderboard (pipeline value)', icon: Award, kind: 'bar', data: (await crmService.getBrokerLeaderboard()).map((b) => ({ name: b.agent, value: Number(b.totalValue) })), colors: { default: '#3b82f6' } },
            { title: 'Pipeline funnel', icon: Layers, kind: 'bar', data: [
              { name: 'Active Leads', value: summary.activeLeadsCount },
              { name: 'Valuations In Process', value: summary.valuationsInProcessCount },
              { name: 'Conveyancing At Deeds', value: summary.conveyancingAtDeedsCount },
              { name: 'Finalised Closed', value: summary.finalizedClosedCount }
            ], colors: { default: '#10b981' } }
          ]);
        } else if (role === 'Broker') {
          const [boards, properties, commissionStages] = await Promise.all([
            crmService.getCRMBoards(),
            propertyService.searchProperties([], 'All', null, null),
            crmService.getCommissionByStage()
          ]);
          setMetrics([
            { label: 'My deals', value: boards.verifiedDeals?.length || 0, icon: Briefcase },
            { label: 'Available properties', value: properties.length, icon: Home },
            { label: 'Closed-won commission', value: `R ${Number((commissionStages || []).find((c) => c.period === 'ClosedWon')?.revenue || 0).toLocaleString()}`, icon: DollarSign }
          ]);
          setCharts([
            { title: 'My deal pipeline stages', icon: Briefcase, kind: 'pie', data: Object.entries((boards.verifiedDeals || []).reduce((a, d) => { const k = d.dealStage || 'Open'; a[k] = (a[k] || 0) + 1; return a; }, {})).map(([name, value]) => ({ name, value })), colors: DEAL_COLORS },
            { title: 'Commission by stage', icon: DollarSign, kind: 'donut', data: (commissionStages || []).map((c) => ({ name: formatTitle(c.period), value: Number(c.revenue || 0) })) },
            { title: 'My deal value pipeline', icon: Home, kind: 'bar', data: (boards.verifiedDeals || []).slice(0, 8).map((d) => ({ name: `DL-${d.dealId}`, value: Number(d.transactionValue || 0) })), colors: DEAL_COLORS }
          ]);
        } else if (role === 'Owner') {
          const ots = await crmService.getOwnerTenantRecords();
          const typeCount = {};
          ots.forEach((o) => {
            const interests = o.propertyTypeInterests ? JSON.parse(o.propertyTypeInterests) : [];
            (Array.isArray(interests) ? interests : [interests]).forEach((t) => { if (t) typeCount[t] = (typeCount[t] || 0) + 1; });
          });
          setMetrics([{ label: 'Interested parties', value: ots.length, icon: Users }, { label: 'Property operations', value: 'Active', icon: Home }]);
          setCharts([
            { title: 'Property type interests', icon: PieIcon, kind: 'pie', data: PROPERTY_TYPES.map((t) => ({ name: t, value: typeCount[t] || 0 })), colors: TYPE_COLORS },
            { title: 'Portfolio size by type', icon: Home, kind: 'bar', data: PROPERTY_TYPES.map((t) => ({ name: t, value: typeCount[t] || 0 })), colors: TYPE_COLORS }
          ]);
        } else {
          const ops = await crmService.getOperationalRecords('maintenance');
          setMetrics([{ label: 'Lease access', value: 'Active', icon: Home }, { label: 'Requests', value: ops.length, icon: Bell }]);
          setCharts([
            { title: 'Request status breakdown', icon: Bell, kind: 'pie', data: Object.entries(ops.reduce((a, o) => { const k = o.status || 'Open'; a[k] = (a[k] || 0) + 1; return a; }, {})).map(([name, value]) => ({ name, value })), colors: STATUS_COLORS }
          ]);
        }
      } catch {
        setMetrics([]); setCharts([]);
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [role]);

  const PROPERTY_TYPES = ['Residential', 'Commercial', 'Retail', 'Industrial'];

  return (
    <div className="space-y-6">
      <div>
        <p className="text-[10px] uppercase tracking-[.2em] text-slate-500">{roleLabels[role] || role}</p>
        <h3 className="text-2xl font-bold text-white">Welcome, {profile?.name || 'User'}</h3>
        <p className="text-sm text-slate-400 mt-1">Your workspace only surfaces information appropriate to your role.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {metrics.map(({ label, value, icon: Icon }) => (
          <div key={label} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex items-center justify-between">
            <div><p className="text-xs uppercase tracking-wider text-slate-500">{label}</p><p className="mt-2 text-2xl font-black text-white">{value}</p></div>
            <Icon className="text-blue-400" />
          </div>
        ))}
      </div>

      {isLoading ? (
        <div className="py-16 text-center text-slate-500 text-sm">Loading role-specific analytics...</div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
