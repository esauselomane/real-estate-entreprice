import { useEffect, useMemo, useState } from 'react';
import { BarChart3, PieChart, TrendingUp, CalendarRange, DollarSign } from 'lucide-react';
import { crmService } from '../services/crmService.js';

export default function ReportsWorkspace() {
  const [report, setReport] = useState({ totalLeads: 0, totalDeals: 0, totalRevenue: 0, monthlyBreakdown: [] });
  const [year, setYear] = useState(new Date().getFullYear());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const load = async () => {
      setIsLoading(true);
      try {
        const data = await crmService.getCRMReportOverview(year);
        setReport(data || { totalLeads: 0, totalDeals: 0, totalRevenue: 0, monthlyBreakdown: [] });
      } catch (err) {
        setError(err || 'Report load failed.');
      } finally {
        setIsLoading(false);
      }
    };

    load();
  }, [year]);

  const peakMonth = useMemo(() => {
    return [...(report.monthlyBreakdown || [])].sort((a, b) => b.revenue - a.revenue)[0] || null;
  }, [report]);

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
        <div>
          <p className="text-[10px] uppercase tracking-[0.2em] text-slate-500">Performance dashboard</p>
          <h3 className="text-xl font-bold text-white">Overview, Lead & Deal Reports</h3>
        </div>
        <div className="flex items-center gap-2 bg-slate-900 border border-slate-800 rounded-xl px-3 py-2">
          <CalendarRange size={14} className="text-blue-500" />
          <select value={year} onChange={(e) => setYear(Number(e.target.value))} className="bg-transparent text-sm text-slate-200 outline-none">
            {[2024, 2025, 2026].map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
      </div>

      {error && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{error}</div>}

      {isLoading ? (
        <div className="py-10 text-center text-slate-400 text-sm">Loading report data...</div>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-4">
            <MetricCard icon={<BarChart3 size={16} />} label="Total Leads" value={report.totalLeads} tone="blue" />
            <MetricCard icon={<PieChart size={16} />} label="Total Deals" value={report.totalDeals} tone="amber" />
            <MetricCard icon={<TrendingUp size={16} />} label="Revenue" value={`R ${Number(report.totalRevenue || 0).toLocaleString()}`} tone="green" />
            <MetricCard icon={<DollarSign size={16} />} label="Peak Month" value={peakMonth ? peakMonth.period : 'N/A'} tone="purple" />
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <div className="flex justify-between items-center mb-4">
              <h4 className="text-sm font-bold text-white uppercase tracking-[0.2em]">Monthly drilldown</h4>
              <span className="text-[10px] text-slate-500">Leads / Deals / Revenue</span>
            </div>
            <div className="space-y-3">
              {(report.monthlyBreakdown || []).map((row) => (
                <div key={row.period} className="grid grid-cols-1 md:grid-cols-[80px_1fr_100px_120px] gap-3 items-center bg-slate-950/60 border border-slate-800 rounded-xl px-3 py-2">
                  <span className="text-sm font-semibold text-slate-300">{row.period}</span>
                  <div className="h-2 rounded-full bg-slate-800 overflow-hidden">
                    <div className="h-full rounded-full bg-gradient-to-r from-blue-500 to-cyan-400" style={{ width: `${Math.min(100, (row.leads / Math.max(report.totalLeads || 1, 1)) * 100)}%` }} />
                  </div>
                  <span className="text-xs text-slate-400">{row.leads} / {row.deals}</span>
                  <span className="text-xs text-emerald-400 text-right">R {Number(row.revenue || 0).toLocaleString()}</span>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function MetricCard({ icon, label, value, tone }) {
  const toneMap = {
    blue: 'bg-blue-950/40 border-blue-900 text-blue-300',
    amber: 'bg-amber-950/40 border-amber-900 text-amber-300',
    green: 'bg-emerald-950/40 border-emerald-900 text-emerald-300',
    purple: 'bg-violet-950/40 border-violet-900 text-violet-300'
  };

  return (
    <div className={`rounded-2xl border p-4 ${toneMap[tone] || toneMap.blue}`}>
      <div className="flex items-center justify-between">
        <span className="text-[10px] uppercase tracking-[0.2em]">{label}</span>
        <span>{icon}</span>
      </div>
      <div className="mt-4 text-2xl font-black text-white">{value}</div>
    </div>
  );
}
