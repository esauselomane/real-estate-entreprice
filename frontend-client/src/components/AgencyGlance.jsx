import { useEffect, useState, useCallback } from 'react';
import {
  Users, Briefcase, Home, TrendingUp, Building2, Mail, Phone,
  Calendar, CheckCircle, XCircle, FileText, MapPin
} from 'lucide-react';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  LineChart, Line, Legend, BarChart, Bar
} from 'recharts';
import { crmService } from '../services/crmService.js';
import { paymentService } from '../services/paymentService.js';
import { leadCaptureService } from '../services/leadCaptureService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useTenant } from '../contexts/TenantContext.jsx';
import LoadingOverlay from './LoadingOverlay.jsx';

const fmtCurrency = (n) => {
  const num = Number(n || 0);
  if (Math.abs(num) >= 1_000_000) return `R ${(num / 1_000_000).toFixed(2)}M`;
  if (Math.abs(num) >= 1_000) return `R ${(num / 1_000).toFixed(1)}K`;
  return `R ${num.toFixed(2)}`;
};

const fmtNumber = (n) => Number(n || 0).toLocaleString('en-ZA');

function KpiCard({ icon: Icon, label, value, accent = 'blue' }) {
  const accentMap = {
    blue: 'text-blue-400',
    emerald: 'text-emerald-400',
    amber: 'text-amber-400',
    violet: 'text-violet-400',
    red: 'text-red-400',
  };
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex items-center gap-4">
      <div className={`p-3 rounded-xl bg-slate-800/50 ${accentMap[accent] || accentMap.blue}`}>
        <Icon size={24} />
      </div>
      <div className="flex-1">
        <p className="text-xs text-slate-500 uppercase tracking-wider font-bold">{label}</p>
        <p className="text-3xl font-bold text-white mt-1">{value}</p>
      </div>
    </div>
  );
}

function GrowthChart({ title, data, dataKey, color }) {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
      <h3 className="text-sm font-semibold text-white mb-1">{title}</h3>
      <div className="h-64">
        {data.length === 0 ? (
          <div className="h-full flex items-center justify-center text-slate-500">No data yet.</div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data}>
              <defs>
                <linearGradient id={`grad-${color}`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={color} stopOpacity={0.3} />
                  <stop offset="100%" stopColor={color} stopOpacity={0} />
                </linearGradient>
              </defs>
              <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fill: '#6474a3', fontSize: 11 }} />
              <YAxis axisLine={false} tickLine={false} tick={{ fill: '#6474a3', fontSize: 11 }} />
              <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
              <Tooltip
                contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }}
                labelStyle={{ color: '#cbd5e1', fontSize: 11 }}
                itemStyle={{ color: '#94a3b8', fontSize: 11 }}
              />
              <Area
                type="monotone"
                dataKey={dataKey}
                stroke={color}
                fill={`url(#grad-${color})`}
                strokeWidth={2}
                dot={{ fill: color, r: 3 }}
                isAnimationActive={false}
              />
              <Legend />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}

export default function AgencyGlance() {
  const { userRole } = useAuth();
  const { activeTenantId } = useTenant();
  const [summary, setSummary] = useState(null);
  const [tabData, setTabData] = useState(null);
  const [pricingPackages, setPricingPackages] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [statusBanner, setStatusBanner] = useState(null);
  const [checkoutState, setCheckoutState] = useState({ loading: false, packageCode: null });
  const [activeTab, setActiveTab] = useState('overview');
  const [isTrialExpired, setIsTrialExpired] = useState(false);

  const loadSummary = useCallback(async () => {
    try {
      const data = await crmService.getAgencyGlanceSummary();
      setSummary(data);
    } catch (err) {
      setError(err?.message || 'Failed to load agency glance data.');
    }
  }, [activeTenantId]);

  const loadTabData = useCallback(async () => {
    try {
      const [tabDataResponse, subscriptionHistory, plans] = await Promise.all([
        crmService.getAgencyGlanceTabs(),
        paymentService.getSubscriptionHistory(activeTenantId || null),
        leadCaptureService.getPricingPackages().catch(() => []),
      ]);

      const formattedTabData = {
        ...tabDataResponse,
        activeSubscriptions: (subscriptionHistory || [])
          .filter((item) => ['active', 'trialing'].includes((item.status || '').toLowerCase()))
          .map((item) => ({
            name: item.pricingPackageName || item.pricingPackageCode || 'Subscription',
            code: item.pricingPackageCode || 'SUBSCRIPTION',
            monthlyPrice: item.monthlyPrice ?? null,
            annualPrice: item.annualPrice ?? null,
            startDate: item.currentPeriodStart,
            endDate: item.currentPeriodEnd,
            status: item.status,
          })),
        pastSubscriptions: (subscriptionHistory || [])
          .filter((item) => !['active', 'trialing', 'none'].includes((item.status || '').toLowerCase()))
          .map((item) => ({
            name: item.pricingPackageName || item.pricingPackageCode || 'Subscription',
            code: item.pricingPackageCode || 'SUBSCRIPTION',
            monthlyPrice: item.monthlyPrice ?? null,
            annualPrice: item.annualPrice ?? null,
            startDate: item.currentPeriodStart,
            endDate: item.currentPeriodEnd,
            status: item.status,
          })),
      };

      setTabData(formattedTabData);
      setPricingPackages(Array.isArray(plans) ? plans : []);
    } catch (err) {
      setError(err?.message || 'Failed to load agency glance tab data.');
    }
  }, [activeTenantId]);

  const handleUpgrade = useCallback(async (packageCode) => {
    try {
      setCheckoutState({ loading: true, packageCode });
      setError('');

      const returnBaseUrl = `${window.location.origin}${window.location.pathname}`;
      const successUrl = `${returnBaseUrl}?paymentStatus=success&paymentSource=checkout`;
      const cancelUrl = `${returnBaseUrl}?paymentStatus=cancelled&paymentSource=checkout`;

      const result = await paymentService.createCheckoutSession({
        pricingPackageCode: packageCode,
        billingCycle: 'monthly',
        successUrl,
        cancelUrl,
      });

      if (result?.checkoutUrl) {
        window.location.href = result.checkoutUrl;
        return;
      }

      setError('Checkout session was created but no redirect URL was returned.');
    } catch (err) {
      setError(err?.message || 'Failed to start the checkout flow.');
    } finally {
      setCheckoutState({ loading: false, packageCode: null });
    }
  }, []);

  const handleManageBilling = useCallback(async () => {
    try {
      const returnBaseUrl = `${window.location.origin}${window.location.pathname}`;
      const result = await paymentService.createPortalSession(returnBaseUrl);
      if (result?.portalUrl) {
        window.location.href = result.portalUrl;
      } else {
        setError('No billing portal URL was returned.');
      }
    } catch (err) {
      setError(err?.message || 'Failed to open billing portal.');
    }
  }, []);

  const checkTrialStatus = useCallback(async () => {
    try {
      const status = await paymentService.getSubscriptionStatus();
      if (status?.IsTrial === false && status?.CanUpgrade === false) {
        setIsTrialExpired(true);
      }
    } catch {
      // Non-blocking - trial status is best-effort at this point
    }
  }, []);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const paymentStatus = params.get('paymentStatus');

    if (paymentStatus) {
      const banner =
        paymentStatus === 'success'
          ? {
              type: 'success',
              message: 'Subscription updated successfully. Your billing status is being refreshed.',
            }
          : {
              type: 'warning',
              message: 'Checkout was cancelled. No payment was taken and your current plan remains unchanged.',
            };

      setStatusBanner(banner);

      const refreshAfterReturn = async () => {
        await Promise.all([loadSummary(), loadTabData()]);
        const nextUrl = `${window.location.pathname}`;
        window.history.replaceState({}, '', nextUrl);
      };

      void refreshAfterReturn();
    }
  }, [loadSummary, loadTabData]);

  useEffect(() => {
    const loadAll = async () => {
      setLoading(true);
      setError('');
      await Promise.all([loadSummary(), loadTabData(), checkTrialStatus()]);
      setLoading(false);
    };
    loadAll();
  }, [loadSummary, loadTabData, checkTrialStatus]);

  if (loading && !summary && !tabData) {
    return (
      <LoadingOverlay label="Loading agency data…" subtitle="Aggregating KPIs and growth metrics" />
    );
  }

  return (
    <div className="space-y-6">
      {/* Tab Navigation */}
      <div className="flex gap-1 bg-slate-900 border border-slate-800 rounded-2xl p-1.5">
        <button
          onClick={() => setActiveTab('overview')}
          className={`px-4 py-2 rounded-xl text-sm font-semibold transition-all cursor-pointer ${
            activeTab === 'overview'
              ? 'bg-blue-600 text-white shadow-lg'
              : 'text-slate-400 hover:text-white hover:bg-slate-800'
          }`}
        >
          <TrendingUp size={14} className="inline mr-1.5" />
          Overview
        </button>
        <button
          onClick={() => setActiveTab('quick-glance')}
          className={`px-4 py-2 rounded-xl text-sm font-semibold transition-all cursor-pointer ${
            activeTab === 'quick-glance'
              ? 'bg-blue-600 text-white shadow-lg'
              : 'text-slate-400 hover:text-white hover:bg-slate-800'
          }`}
        >
          Quick Glance
        </button>
      </div>

      {statusBanner && (
        <div
          className={`p-3 rounded-xl border text-xs ${
            statusBanner.type === 'success'
              ? 'bg-emerald-950/30 border-emerald-800 text-emerald-300'
              : 'bg-amber-950/30 border-amber-800 text-amber-300'
          }`}
        >
          {statusBanner.message}
        </div>
      )}

      {error && (
        <div className="p-3 rounded-xl bg-red-950/30 border border-red-900 text-red-300 text-xs">
          {error}
        </div>
      )}

      {/* Overview Tab — moved reports/analytics from System Admin */}
      {activeTab === 'overview' && summary && tabData && (
        <div className="space-y-6">
          {/* KPI Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <KpiCard icon={Users} label="Total Users" value={fmtNumber(summary.totalUsers)} accent="blue" />
            <KpiCard icon={Briefcase} label="Total Leads" value={fmtNumber(summary.totalLeads)} accent="amber" />
            <KpiCard icon={Building2} label="Total Deals" value={fmtNumber(summary.totalDeals)} accent="violet" />
            <KpiCard icon={Home} label="Closed Deals" value={fmtNumber(summary.closedDeals)} accent="emerald" />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <KpiCard icon={FileText} label="Sole Mandates" value={fmtNumber(summary.totalPropertiesWithSoleMandates)} accent="blue" />
            <KpiCard icon={TrendingUp} label="Realised Commission" value={fmtCurrency(summary.totalRealisedCommission)} accent="emerald" />
          </div>

          {/* Growth Charts */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <GrowthChart
              title="Users Added (Last 6 Months)"
              data={tabData.usersOverTime || []}
              dataKey="userCount"
              color="#6366f1"
            />
            <GrowthChart
              title="Properties Added (Last 6 Months)"
              data={tabData.propertiesOverTime || []}
              dataKey="propertyCount"
              color="#10b981"
            />
          </div>
        </div>
      )}

      {/* Quick Glance Tab */}
      {activeTab === 'quick-glance' && tabData && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Agency Details */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <h3 className="text-sm font-semibold text-white mb-3 flex items-center gap-2">
              <Building2 size={16} className="text-blue-400" />
              Agency Details
            </h3>
            <dl className="space-y-2 text-xs">
              <div className="flex">
                <dt className="text-slate-500 w-40">Agency ID</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.agencyId || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Tenant ID</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.tenantId || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Agency Name</dt>
                <dd className="text-white flex-1">{tabData.agencyDetails?.name || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Trading Name</dt>
                <dd className="text-slate-300 flex-1">{tabData.agencyDetails?.tradingName || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Registration #</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.registrationNumber || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Brokerage License</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.brokerageLicense || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Contact Email</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.contactEmail || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Headquarters</dt>
                <dd className="text-slate-300 flex-1 truncate">{tabData.agencyDetails?.headquartersAddress || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Status</dt>
                <dd className="text-slate-300 flex-1">{tabData.agencyDetails?.operationalStatus || '-'}</dd>
              </div>
              <div className="flex">
                <dt className="text-slate-500 w-40">Created</dt>
                <dd className="text-slate-300 flex-1">
                  {tabData.agencyDetails?.createdAt ? new Date(tabData.agencyDetails.createdAt).toLocaleDateString('en-ZA') : '-'}
                </dd>
              </div>
            </dl>
          </div>

          {/* Contact Persons */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <h3 className="text-sm font-semibold text-white mb-3 flex items-center gap-2">
              <Users size={16} className="text-emerald-400" />
              Contact Persons
            </h3>
            <div className="space-y-3 max-h-96 overflow-y-auto">
              {(tabData.contactPersons || []).length === 0 ? (
                <div className="text-xs text-slate-500">No contact persons recorded.</div>
              ) : (
                (tabData.contactPersons || []).map((contact, idx) => (
                  <div key={idx} className="p-3 bg-slate-950 border border-slate-800 rounded-xl">
                    <div className="flex items-start justify-between">
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-semibold text-white truncate">{contact.fullName}</p>
                        <p className="text-[11px] text-slate-400 truncate">{contact.positionJobTitle}</p>
                      </div>
                      {contact.isAgencyAdmin && (
                        <CheckCircle size={12} className="text-blue-400 ml-2" title="Agency Admin" />
                      )}
                    </div>
                    <div className="grid grid-cols-2 gap-x-2 gap-y-1 mt-2">
                      <div className="flex items-center gap-1">
                        <Mail size={10} className="text-slate-500" />
                        <span className="text-[11px] text-slate-400 truncate">{contact.emailAddress || '-'}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Phone size={10} className="text-slate-500" />
                        <span className="text-[11px] text-slate-400 truncate">{contact.phoneNumber || '-'}</span>
                      </div>
                    </div>
                    <div className="mt-1">
                      <span className={`text-[10px] px-1.5 py-0.5 rounded border ${
                        contact.isAgencyAdmin
                          ? 'bg-blue-950/30 border-blue-800 text-blue-300'
                          : 'bg-slate-800 border-slate-700 text-slate-400'
                      }`}>
                        {contact.contactType || 'Contact'}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Subscriptions */}
          <div className="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl p-5">
            <h3 className="text-sm font-semibold text-white mb-3 flex items-center gap-2">
              <Calendar size={16} className="text-amber-400" />
              Subscription Packages
            </h3>

            {pricingPackages.length > 0 && (
              <div className="mb-5 grid grid-cols-1 md:grid-cols-3 gap-3">
                {pricingPackages.map((plan) => {
                  const isCurrentPlan = (tabData.activeSubscriptions || []).some(
                    (sub) => (sub.code || '').toUpperCase() === (plan.code || '').toUpperCase()
                  );

                  return (
                    <div
                      key={plan.code}
                      className={`rounded-2xl border p-4 ${
                        isCurrentPlan
                          ? 'border-emerald-500/60 bg-emerald-950/20'
                          : 'border-slate-700 bg-slate-950/60'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="text-sm font-semibold text-white">{plan.name}</p>
                          <p className="text-[11px] text-slate-400">{plan.code}</p>
                        </div>
                        {isCurrentPlan && (
                          <span className="text-[10px] uppercase tracking-wider bg-emerald-500/20 text-emerald-300 rounded-full px-2 py-1">
                            Current
                          </span>
                        )}
                      </div>

                      <div className="mt-3 flex items-end gap-1">
                        <span className="text-2xl font-bold text-white">{plan.monthlyPrice ? fmtCurrency(plan.monthlyPrice) : 'Free'}</span>
                        <span className="text-[11px] text-slate-400">/ month</span>
                      </div>

                      {plan.annualPrice && (
                        <p className="mt-1 text-[11px] text-slate-400">or {fmtCurrency(plan.annualPrice)} / year</p>
                      )}

                      <div className="mt-3 text-[11px] text-slate-300 space-y-1">
                        {(plan.features || []).slice(0, 3).map((feature) => (
                          <div key={feature} className="truncate">• {feature}</div>
                        ))}
                      </div>

                      <button
                        type="button"
                        onClick={() => handleUpgrade(plan.code)}
                        disabled={checkoutState.loading && checkoutState.packageCode === plan.code}
                        className={`mt-4 w-full rounded-xl px-3 py-2 text-xs font-semibold transition ${
                          isCurrentPlan
                            ? 'bg-slate-800 text-slate-300 cursor-not-allowed'
                            : 'bg-blue-600 text-white hover:bg-blue-500'
                        } disabled:opacity-60`}
                      >
                        {checkoutState.loading && checkoutState.packageCode === plan.code
                          ? 'Redirecting…'
                          : isCurrentPlan
                            ? 'Current Plan'
                            : 'Upgrade'}
                      </button>
                    </div>
                  );
                })}
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Active */}
              <div>
                <h4 className="text-xs font-bold text-emerald-400 uppercase tracking-wider mb-2">Active</h4>
                {(tabData.activeSubscriptions || []).length === 0 ? (
                  <div className="text-xs text-slate-500">No active subscriptions.</div>
                ) : (
                  <ul className="space-y-2">
                    {(tabData.activeSubscriptions || []).map((sub, idx) => (
                      <li key={idx} className="p-2 bg-emerald-950/20 border border-emerald-900/30 rounded-lg">
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium text-white">{sub.name || sub.code || 'Subscription'}</span>
                          <CheckCircle size={12} className="text-emerald-400" />
                        </div>
                        <div className="text-xs text-slate-400 mt-1">
                          {(sub.monthlyPrice ? fmtCurrency(sub.monthlyPrice) : 'Free')} / month
                          {sub.startDate && (
                            <span className="ml-2 text-slate-500">
                              starts {new Date(sub.startDate).toLocaleDateString('en-ZA')}
                            </span>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              {/* Past */}
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">Past</h4>
                {(tabData.pastSubscriptions || []).length === 0 ? (
                  <div className="text-xs text-slate-500">No past subscriptions.</div>
                ) : (
                  <ul className="space-y-2">
                    {(tabData.pastSubscriptions || []).map((sub, idx) => (
                      <li key={idx} className="p-2 bg-slate-950 border border-slate-800 rounded-lg">
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium text-slate-300">{sub.name || sub.code || 'Subscription'}</span>
                          <XCircle size={12} className="text-slate-500" />
                        </div>
                        <div className="text-xs text-slate-500 mt-1">
                          {(sub.monthlyPrice ? fmtCurrency(sub.monthlyPrice) : 'Free')} / month
                          {sub.endDate && (
                            <span className="ml-2">
                              expired {new Date(sub.endDate).toLocaleDateString('en-ZA')}
                            </span>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
