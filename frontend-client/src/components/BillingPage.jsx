import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../contexts/AuthContext.jsx';
import { paymentService } from '../services/paymentService.js';
import { leadCaptureService } from '../services/leadCaptureService.js';

export default function BillingPage({ tenantId, status, loadError, onRefresh, onBack }) {
  const { userRole, logout } = useAuth();
  const [plans, setPlans] = useState([]);
  const [cycle, setCycle] = useState('monthly');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const checkoutKeys = useRef(new Map());
  const canBill = ['AgencyAdmin', 'PlatformAdmin'].includes(userRole);
  const paymentReturn = new URLSearchParams(window.location.search).get('paymentStatus');
  const selectedPackage = new URLSearchParams(window.location.search).get('package');
  const salesUrl = import.meta.env.VITE_SALES_URL || 'http://localhost:3000/contact';

  useEffect(() => {
    let mounted = true;
    leadCaptureService.getPricingPackages().then(data => { if (mounted) setPlans(Array.isArray(data) ? data : []); })
      .catch(error => { if (mounted) setError(error.message); });
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    if (paymentReturn !== 'success') return;
    let attempts = 0;
    const timer = setInterval(() => {
      attempts += 1;
      onRefresh();
      if (attempts >= 12) clearInterval(timer);
    }, 5000);
    return () => clearInterval(timer);
  }, [paymentReturn, onRefresh]);

  async function checkout(plan) {
    setError('');
    setBusy(true);
    const key = `${tenantId}:${plan.code}:${cycle}`;
    if (!checkoutKeys.current.has(key)) checkoutKeys.current.set(key, crypto.randomUUID());
    try {
      const base = new URL(window.location.pathname, window.location.origin);
      base.searchParams.set('billing', '1');
      const success = new URL(base);
      success.searchParams.set('paymentStatus', 'success');
      const cancel = new URL(base);
      cancel.searchParams.set('paymentStatus', 'cancelled');
      const result = await paymentService.createCheckoutSession({
        tenantId, pricingPackageCode: plan.code, billingCycle: cycle,
        successUrl: success.href, cancelUrl: cancel.href, idempotencyKey: checkoutKeys.current.get(key),
      });
      if (result.method === 'POST') {
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = result.checkoutUrl;
        for (const [name, value] of Object.entries(result.formFields || {})) {
          const input = document.createElement('input');
          input.type = 'hidden'; input.name = name; input.value = value;
          form.appendChild(input);
        }
        document.body.appendChild(form);
        form.submit();
      } else {
        window.location.assign(result.checkoutUrl);
      }
    } catch (error) {
      setError(error.message);
      if (error.response?.status === 400) checkoutKeys.current.delete(key);
    } finally { setBusy(false); }
  }

  async function manageBilling() {
    setBusy(true); setError('');
    try {
      const url = new URL(window.location.pathname, window.location.origin);
      url.searchParams.set('billing', '1');
      const result = await paymentService.createPortalSession(url.href, tenantId);
      window.location.assign(result.portalUrl);
    } catch (error) { setError(error.message); }
    finally { setBusy(false); }
  }

  return <main className="min-h-screen w-full overflow-x-hidden bg-slate-950 text-slate-100 px-4 py-6 sm:px-8 sm:py-10">
    <div className="w-full max-w-6xl mx-auto space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4"><h1 className="text-3xl font-bold">Subscription & billing</h1>
        <button onClick={logout} className="text-slate-300">Sign out</button></div>
      <p>{status?.requiresUpgrade ? 'Your subscription needs attention to continue using the app.' : `Current status: ${status?.status || 'Loading'}`}</p>
      {status?.isTrial && <p>Your free trial ends on {new Date(status.trialEndsAt).toLocaleDateString()}. You will only be charged if you choose a paid plan.</p>}
      {paymentReturn === 'success' && <p role="status">{status?.status === 'active' ? 'Your subscription is active.' : 'Checkout returned. Waiting for payment confirmation. You can refresh the status below.'}</p>}
      {paymentReturn === 'cancelled' && <p role="status">Checkout was cancelled. Your subscription status is shown above.</p>}
      {loadError && <p role="alert" className="text-red-300">{loadError}</p>}
      {error && <p role="alert" className="text-red-300">{error}</p>}
      {status?.gateway && !status?.isOnceOff && !status?.canManageBilling && <p>Card management is unavailable in this environment. Contact billing support for subscription changes.</p>}
      {status?.isOnceOff && <p>Once-off payment · Access {status.requiresUpgrade ? 'ended' : 'ends'} on {new Date(status.currentPeriodEnd).toLocaleDateString()}. No automatic renewal.</p>}
      {!canBill && <p>Ask your agency administrator to manage this subscription.</p>}
      <div className="flex flex-wrap gap-4 items-center rounded-2xl border border-slate-800 bg-slate-900 p-4">
        <label>Billing cycle <select value={cycle} onChange={event => setCycle(event.target.value)} disabled={busy} className="bg-slate-800 p-2 rounded">
          <option value="monthly">Monthly</option><option value="annual">Annual</option><option value="onceoff">Once-off (one month)</option></select></label>
        <button disabled={busy} onClick={onRefresh}>Refresh status</button>
        {canBill && status?.canManageBilling && <button disabled={busy} onClick={manageBilling}>Manage Billing</button>}
        {!status?.requiresUpgrade && onBack && <button onClick={onBack}>Back to app</button>}
      </div>
      <p className="text-sm text-slate-300">{cycle === 'onceoff' ? 'Pay once for one month of access at the monthly price. Your card will not be charged automatically again.' : 'This plan renews automatically at the selected billing interval.'}</p>
      <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
        {plans.map(plan => {
          const current = status?.status === 'active' && !status?.requiresUpgrade && status?.pricingPackageCode === plan.code;
          const price = cycle === 'annual' ? plan.annualPrice : plan.monthlyPrice;
          const assisted = plan.code === 'ENTERPRISE' || price == null;
          return <article key={plan.code} className="min-w-0 flex flex-col gap-5 rounded-2xl border border-slate-700 bg-slate-900 p-6">
            <h2 className="text-xl font-bold">{plan.name}{selectedPackage === plan.code ? ' — selected' : ''}</h2>
            <p>{assisted ? 'Contact sales' : `${new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(price)} ${cycle === 'onceoff' ? 'once-off · one month' : `/ ${cycle === 'annual' ? 'year' : 'month'}`}`}</p>
            <ul className="flex-1 space-y-3 text-sm leading-6 text-slate-300">{(Array.isArray(plan.features) ? plan.features : []).map(feature => <li key={feature}>{feature}</li>)}</ul>
            {assisted ? <a className="block rounded-xl border border-blue-500 px-4 py-3 text-center font-semibold text-blue-300" href={`${salesUrl}?package=${encodeURIComponent(plan.code)}`}>Contact sales</a> :
              <button disabled={busy || !canBill || current || !status?.canUpgrade || status?.canManageBilling} onClick={() => checkout(plan)}
                className="w-full bg-blue-600 p-3 rounded-xl disabled:opacity-40">
                {current ? 'Current plan' : busy ? 'Please wait…' : 'Choose plan'}
              </button>}
          </article>;
        })}
      </div>
      <a href={salesUrl}>Contact billing support</a>
    </div>
  </main>;
}