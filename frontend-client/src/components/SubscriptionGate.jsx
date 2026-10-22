import { useCallback, useEffect, useState } from 'react';
import { normalizeRole, useAuth } from '../contexts/AuthContext.jsx';
import { paymentService } from '../services/paymentService.js';
import BillingPage from './BillingPage.jsx';
import { SubscriptionContext } from '../contexts/SubscriptionContext.jsx';

const unrestrictedSubscription = Object.freeze({ status: 'exempt', requiresUpgrade: false, isTrial: false });

export default function SubscriptionGate({ children }) {
  const { activeProfile, userRole, logout } = useAuth();
  const isSuperAdmin = normalizeRole(userRole || activeProfile?.role) === 'PlatformAdmin';
  const tenantId = activeProfile?.tenantId;
  const [billingTenantId, setBillingTenantId] = useState(null);
  const [billingSnapshot, setBillingSnapshot] = useState(null);
  const [snapshot, setSnapshot] = useState(null);
  const [error, setError] = useState('');
  const [showBilling, setShowBilling] = useState(() => new URLSearchParams(window.location.search).has('billing'));
  const status = isSuperAdmin ? unrestrictedSubscription : snapshot?.tenantId === tenantId ? snapshot.status : null;
  const refresh = useCallback(async () => {
    if (isSuperAdmin || !tenantId) return;
    try {
      const status = await paymentService.getSubscriptionStatus(tenantId);
      setSnapshot({ tenantId, status }); setError('');
    } catch (error) { setError(error.message); setSnapshot(null); }
  }, [tenantId, isSuperAdmin]);
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- State changes occur after the API request settles.
    refresh();
    const timer = isSuperAdmin ? null : setInterval(refresh, 30000);
    function openBilling(event) {
      // Administrators may open an agency’s billing explicitly, but are never redirected by subscription errors.
      if (isSuperAdmin && !event.detail?.tenantId) return;
      setBillingTenantId(event.detail?.tenantId || tenantId);
      setShowBilling(true);
    }
    window.addEventListener('open-billing', openBilling);
    window.addEventListener('focus', refresh);
    return () => {
      clearInterval(timer);
      window.removeEventListener('open-billing', openBilling);
      window.removeEventListener('focus', refresh);
    };
  }, [refresh, tenantId, isSuperAdmin]);
  const refreshBilling = useCallback(async () => {
    if (!billingTenantId || billingTenantId === tenantId) return refresh();
    try {
      const status = await paymentService.getSubscriptionStatus(billingTenantId);
      setBillingSnapshot({ tenantId: billingTenantId, status }); setError('');
    } catch (error) { setError(error.message); }
  }, [billingTenantId, tenantId, refresh]);
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- State changes occur after the API request settles.
    if (showBilling) refreshBilling();
  }, [showBilling, refreshBilling]);
  if (!status) return <main className="min-h-screen bg-slate-950 text-white p-8">
    <p>{error || 'Checking subscription…'}</p><button onClick={refresh}>Retry</button> <button onClick={logout}>Sign out</button>
  </main>;
  const selectedTenantId = status.requiresUpgrade ? tenantId : billingTenantId || tenantId;
  const selectedStatus = selectedTenantId === tenantId ? status : billingSnapshot?.tenantId === selectedTenantId ? billingSnapshot.status : null;
  if (status.requiresUpgrade || showBilling) return <BillingPage tenantId={selectedTenantId} status={selectedStatus} loadError={error} onRefresh={selectedTenantId === tenantId ? refresh : refreshBilling} onBack={() => {
    const url = new URL(window.location.href);
    url.searchParams.delete('billing'); url.searchParams.delete('paymentStatus');
    window.history.replaceState({}, '', url); setShowBilling(false);
  }} />;
  return <SubscriptionContext.Provider value={status}>{status.isTrial && <div className="bg-amber-950 text-amber-100 px-8 py-2">Trial ends {new Date(status.trialEndsAt).toLocaleDateString()}. <button onClick={() => setShowBilling(true)}>View upgrade options</button></div>}{children}</SubscriptionContext.Provider>;
}
