import { useEffect, useState } from 'react';
import { RefreshCw, TrendingUp, Banknote, Trophy } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line, Legend } from 'recharts';
import { crmService } from '../services/crmService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
export default function ReportsAnalytics() {
 const { userRole } = useAuth();
 const canManageAgencies = ["AgencyAdmin", "PlatformAdmin"].includes(userRole);
 const { activeTenantId } = useTenant();
  // 📊 REPORTS & ANALYTICS — tenant-scoped pipeline / commission / broker metrics
  const [pipelineSummary, setPipelineSummary] = useState(null);
  const [reportOverview, setReportOverview] = useState(null);
  const [commissionByStage, setCommissionByStage] = useState([]);
  const [brokerLeaderboard, setBrokerLeaderboard] = useState([]);
  const [reportsLoading, setReportsLoading] = useState(false);
  const [reportsError, setReportsError] = useState('');

  // 📊 Load all report datasets when the reports tab is activated (PlatformAdmin only)
  const loadReports = async () => {
    if (!canManageAgencies) return;
    setReportsLoading(true);
    setReportsError('');
    try {
      const [summary, overview, commission, leaderboard] = await Promise.all([
        crmService.getPipelineReportSummary().catch(() => null),
        crmService.getCRMReportOverview().catch(() => null),
        crmService.getCommissionByStage().catch(() => []),
        crmService.getBrokerLeaderboard().catch(() => [])
      ]);
      setPipelineSummary(summary);
      setReportOverview(overview);
      setCommissionByStage(commission || []);
      setBrokerLeaderboard(leaderboard || []);
      if (!summary && !overview) setReportsError('Some report data could not be loaded.');
    } catch {
      setReportsError('Unable to load report data.');
    } finally {
      setReportsLoading(false);
    }
  };

  useEffect(() => {
    if (canManageAgencies) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      loadReports();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManageAgencies, activeTenantId]);


 return <>      {canManageAgencies && (
        <div className="space-y-6">
          {reportsError && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{reportsError}</div>}

          {reportsLoading && (
            <div className="text-center py-8 text-slate-500 text-xs">
              <RefreshCw size={16} className="animate-spin inline mb-1" />
              <p>Loading report data…</p>
            </div>
          )}

          {/* 📊 PIPELINE SUMMARY CARDS */}
          {pipelineSummary && (
            <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-blue-400">{pipelineSummary.activeLeadsCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Active Leads</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-amber-400">{pipelineSummary.valuationsInProcessCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Valuations In Process</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-violet-400">{pipelineSummary.conveyancingAtDeedsCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Conveyancing</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-2xl font-bold text-emerald-400">{pipelineSummary.finalizedClosedCount}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Closed Won</div>
              </div>
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 text-center">
                <div className="text-sm font-bold text-white">R {Number(pipelineSummary.totalPipelineEstimatedValue || 0).toLocaleString()}</div>
                <div className="text-[10px] text-slate-500 uppercase tracking-wider">Pipeline Value</div>
              </div>
            </div>
          )}

          {/* 📈 MONTHLY TRENDS + COMMISSION BY STAGE (side by side on desktop) */}
          <div className="grid gap-6 lg:grid-cols-2">
            {reportOverview && (
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                  <TrendingUp size={14} className="text-blue-400" /> Monthly Trends
                </div>
                <div className="grid grid-cols-3 gap-3 mb-4 text-center">
                  <div><div className="text-xl font-bold text-white">{reportOverview.totalLeads}</div><div className="text-[10px] text-slate-500">Leads</div></div>
                  <div><div className="text-xl font-bold text-white">{reportOverview.totalDeals}</div><div className="text-[10px] text-slate-500">Deals</div></div>
                  <div><div className="text-sm font-bold text-emerald-400">R {Number(reportOverview.totalRevenue || 0).toLocaleString()}</div><div className="text-[10px] text-slate-500">Revenue</div></div>
                </div>
                <ResponsiveContainer width="100%" height={200}>
                  <LineChart data={reportOverview.monthlyBreakdown || []}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis dataKey="period" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3b8' }} />
                    <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3b8' }} />
                    <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', borderRadius: '8px', fontSize: '11px' }} />
                    <Legend wrapperStyle={{ fontSize: '10px', color: '#94a3b8' }} />
                    <Line type="monotone" dataKey="leads" stroke="#38bdf8" strokeWidth={2} name="Leads" />
                    <Line type="monotone" dataKey="deals" stroke="#a78bfa" strokeWidth={2} name="Deals" />
                    <Line type="monotone" dataKey="revenue" stroke="#4ade80" strokeWidth={2} name="Revenue" />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}

            {commissionByStage.length > 0 && (
              <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
                <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                  <Banknote size={14} className="text-emerald-400" /> Commission by Stage
                </div>
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={commissionByStage} layout="vertical" margin={{ top: 5, right: 20, left: 80, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis type="number" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3b8' }} />
                    <YAxis type="category" dataKey="period" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#94a3b8' }} />
                    <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #334155', borderRadius: '8px', fontSize: '11px' }} />
                    <Bar dataKey="revenue" fill="#4ade80" name="Commission" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </div>

          {/* 🏆 BROKER LEADERBOARD */}
          {brokerLeaderboard.length > 0 && (
            <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
              <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4">
                <Trophy size={14} className="text-amber-400" /> Broker Leaderboard
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-[11px]">
                  <thead>
                    <tr className="text-slate-500 uppercase text-[9px]">
                      <th className="text-left py-1">#</th>
                      <th className="text-left py-1">Broker</th>
                      <th className="text-right py-1">Deals</th>
                      <th className="text-right py-1">Pipeline Value</th>
                      <th className="text-right py-1">Commission</th>
                      <th className="text-right py-1">Won</th>
                    </tr>
                  </thead>
                  <tbody>
                    {brokerLeaderboard.map((b, idx) => (
                      <tr key={b.agent || idx} className="border-t border-slate-800">
                        <td className="py-1 text-slate-400">{idx + 1}</td>
                        <td className="py-1 text-white">{b.agent}</td>
                        <td className="py-1 text-right text-slate-300">{b.deals}</td>
                        <td className="py-1 text-right text-slate-300">R {Number(b.totalValue || 0).toLocaleString()}</td>
                        <td className="py-1 text-right text-emerald-400">R {Number(b.totalCommission || 0).toLocaleString()}</td>
                        <td className="py-1 text-right text-slate-300">{b.won}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

</>;
}
