import apiClient from './apiClient.js';

const ACTIVE_TENANT_STORAGE_KEY = 'active_tenant_override';
const ACTIVE_TENANT_STORAGE_MARKER = 'view-override';
const USER_PROFILE_STORAGE_KEY = 'user_profile_data';

function resolveActiveTenantOverride() {
  try {
    const raw = localStorage.getItem(ACTIVE_TENANT_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return null;
    if (parsed.__source !== ACTIVE_TENANT_STORAGE_MARKER) return null;
    if (parsed.__all__) return '__all__';
    return parsed.tenantId || null;
  } catch {
    return null;
  }
}

function resolveLoggedInTenantId() {
  try {
    const raw = localStorage.getItem(USER_PROFILE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed?.tenantId || null;
  } catch {
    return null;
  }
}

export const paymentService = {
  _tenantQueryParams: () => {
    const override = resolveActiveTenantOverride();
    if (override && override !== '__all__') return { tenantId: override };
    const fallback = resolveLoggedInTenantId();
    return fallback ? { tenantId: fallback } : {};
  },

  getSubscriptionStatus: async (tenantId = null) => {
    const params = tenantId ? { tenantId } : paymentService._tenantQueryParams();
    const response = await apiClient.get('/payments/subscription/status', { params });
    return response.data || {};
  },

  getSubscriptionHistory: async (tenantId = null) => {
    const params = tenantId ? { tenantId } : paymentService._tenantQueryParams();
    const response = await apiClient.get('/payments/subscription/history', { params });
    return Array.isArray(response.data) ? response.data : [];
  },

  createCheckoutSession: async ({
    pricingPackageCode,
    billingCycle = 'monthly',
    successUrl = '',
    cancelUrl = '',
    tenantId = null,
    idempotencyKey,
  }) => {
    const response = await apiClient.post('/payments/create-checkout', {
      tenantId: tenantId || paymentService._tenantQueryParams().tenantId,
      idempotencyKey,
      pricingPackageCode,
      billingCycle,
      successUrl,
      cancelUrl,
    });
    return response.data;
  },

  createPortalSession: async (returnUrl = '', tenantId = null) => {
    const response = await apiClient.post('/payments/portal', { returnUrl, tenantId: tenantId || paymentService._tenantQueryParams().tenantId });
    return response.data || {};
  },
};
