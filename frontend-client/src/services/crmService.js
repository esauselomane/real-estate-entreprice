import apiClient from './apiClient.js';

const ACTIVE_TENANT_STORAGE_KEY = 'active_tenant_override';
const ACTIVE_TENANT_STORAGE_MARKER = 'view-override';
const USER_PROFILE_STORAGE_KEY = 'user_profile_data';

// 🌐 Resolve the active-tenant override from the TenantSwitcher dropdown.
// The stored payload may be either an agency selection { agencyId, tenantId }
// or an "__all__" sentinel. Services read `tenantId` because that's the
// partition key the backend data tables use. The override is *only* used as
// a request filter — it is never written back to the JWT or to the user
// profile, so the logged-in tenantId always stays the authoritative identity.
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

// 📊 Parse the X-Aggregates response header (JSON map of { field → { bucket → count } }).
// The server pre-computes these over the entire filtered universe so chart
// components (donuts, bars, pies) can render true counts instead of being
// limited to the 20-row page that came back in the body.
function parseAggregatesHeader(headers) {
  debugger;
  const raw = headers?.['x-aggregates'];
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export const crmService = {
   _parseAggregates: parseAggregatesHeader,

   getOperationalRecords: async (module, page = 1) => {
     const response = await apiClient.get('/crm/operations', { params: { module, page, ...crmService._tenantQueryParams() } });
     const payload = Array.isArray(response.data) ? response.data : [];
     return Object.assign(payload, {
       totalCount: Number(response.headers['x-total-count'] || 0),
       aggregates: parseAggregatesHeader(response.headers)
     });
   },

   // 🌐 Returns axios params reflecting the active-tenant override, if any.
   // When the override is set to a single agencyId it ALWAYS wins; the logged-in
   // user's tenantId is only used as the fallback so backend filters are
   // driven by the visible "active tenant" in the UI rather than the JWT-bound
   // session tenant.
   _tenantQueryParams: () => {
     const override = resolveActiveTenantOverride();
     if (override && override !== '__all__') return { tenantId: override };
     const fallback = resolveLoggedInTenantId();
     return fallback ? { tenantId: fallback } : {};
   },

   // 🛰️ Resolves the tenant id a write should be stamped with: the active
   // override (when set to a specific agency) takes precedence; otherwise the
   // logged-in user's tenant is used. "__all__" is never a valid stamp — that
   // mode is read-only.
   _resolveWriteTenantId: () => {
     const override = resolveActiveTenantOverride();
     if (override && override !== '__all__') return override;
     return resolveLoggedInTenantId();
   },

   // --- 🔗 UNIT-SCOPED OPERATIONAL RECORDS (leases / maintenance / document vault) ---
  getOperationalRecordsForUnit: async (module, propertyId, propertyUnitId, page = 1) => {
    const response = await apiClient.get('/crm/operations', {
      params: { module, propertyId, propertyUnitId, page, ...crmService._tenantQueryParams() }
    });
    return response.data;
  },

  createOperationalRecord: async (payload) => {
    const response = await apiClient.post('/crm/operations', { ...payload, tenantId: crmService._resolveWriteTenantId() || payload.tenantId });
    return response.data;
  },

  updateOperationalRecord: async (recordId, payload) => {
    const response = await apiClient.put(`/crm/operations/${recordId}`, payload);
    return response.data;
  },
   getCRMBoards: async (page = 1) => {
     try {
       const parameters = {
         page,
         agentName: '',
         userRole: '',
         ...crmService._tenantQueryParams()
       };
       const response = await apiClient.get('/crm/boards', { params: parameters });
      // X-Lead-Count / X-Deal-Count expose tenant-scoped totals for pagination;
      // X-Aggregates carries { leadStage, dealStage } counts over the entire
      // filtered universe so stage charts reflect every lead + deal.
      return {
        ...response.data,
        leadCount: Number(response.headers['x-lead-count'] || 0),
        dealCount: Number(response.headers['x-deal-count'] || 0),
        aggregates: parseAggregatesHeader(response.headers)
      };
    } catch (error) {
      throw error.response?.data?.message || 'Dual pipeline boards data sync extraction failed.';
    }
  },

  getOwnerTenantRecords: async (page = 1) => {
    try {
      const response = await apiClient.get('/crm/owners-tenants', { params: { page, ...crmService._tenantQueryParams() } });
      const data = Array.isArray(response.data) ? response.data : [];
      return Object.assign(data, {
        totalCount: Number(response.headers['x-total-count'] || 0),
        aggregates: parseAggregatesHeader(response.headers)
      });
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant registry sync failed.';
    }
  },

  getOwnerTenantRecord: async (ownerTenantId) => {
    try {
      const response = await apiClient.get(`/crm/owners-tenants/${ownerTenantId}`, { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant record could not be loaded.';
    }
  },

  // Normalizes interest arrays into the JSON strings the backend persists.
  _normalizeOwnerInterests: (payload) => {
    const out = { ...payload };
    out.propertyTypeInterests = Array.isArray(out.propertyTypeInterests)
      ? JSON.stringify(out.propertyTypeInterests)
      : (out.propertyTypeInterests || '');
    out.areaInterestIds = Array.isArray(out.areaInterestIds)
      ? JSON.stringify(out.areaInterestIds)
      : (out.areaInterestIds || '');
    return out;
  },

  searchGeography: async (search) => {
    try {
      const response = await apiClient.get('/crm/geography', { params: { search: search || '' } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Geographic area lookup failed.';
    }
  },

  createOwnerTenantRecord: async (payload) => {
    try {
      const response = await apiClient.post('/crm/owners-tenants', {
        ...(crmService._normalizeOwnerInterests(payload)),
        tenantId: crmService._resolveWriteTenantId() || payload.tenantId
      });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant record creation failed.';
    }
  },

  updateOwnerTenantRecord: async (payload) => {
    try {
      const response = await apiClient.put('/crm/owners-tenants', crmService._normalizeOwnerInterests(payload));
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant record update failed.';
    }
  },

  getCRMReportOverview: async (year = new Date().getFullYear()) => {
    try {
      const response = await apiClient.get('/crm/report/overview', { params: { year, ...crmService._tenantQueryParams() } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Metrics overview could not be loaded.';
    }
  },

  getAgencies: async (page = 1, tenantIdOverride) => {
    try {
      // The optional `tenantIdOverride` lets the caller ask the backend
      // to expand a parent-agency subtree (top-level) into its full
      // descendant list, or pin to a single tenant (child). The CRM
      // applies the parent-vs-child rule on the server.
      const params = { page, ...crmService._tenantQueryParams() };
      if (tenantIdOverride) params.tenantId = tenantIdOverride;
      const response = await apiClient.get('/crm/agencies', { params });
      const data = Array.isArray(response.data) ? response.data : (response.data.items || []);
      return Object.assign(data, {
        totalCount: Number(response.headers['x-total-count'] || 0),
        aggregates: parseAggregatesHeader(response.headers)
      });
    } catch (error) {
      throw error.response?.data?.message || 'Agency list sync failed.';
    }
  },

  // 📜 Full non-paginated roster for the tenant-switcher dropdown. The
  // regular /agencies endpoint is paginated to 20 rows; the dropdown
  // needs every visible agency so the user can search and pick any of
  // them. Honours the same `?tenantId=` override the rest of the
  // visibility helpers use, so the dropdown can also be filtered to a
  // specific subtree.
  getAgenciesRoster: async (tenantIdOverride) => {
    try {
      const params = { ...crmService._tenantQueryParams() };
      if (tenantIdOverride) params.tenantId = tenantIdOverride;
      const response = await apiClient.get('/crm/agencies/roster', { params });
      return Array.isArray(response.data) ? response.data : [];
    } catch (error) {
      throw error.response?.data?.message || 'Agency roster sync failed.';
    }
  },

  createAgency: async (payload) => {
    try {
      const response = await apiClient.post('/crm/agencies', {
        ...payload,
        tenantId: crmService._resolveWriteTenantId() || payload.tenantId || 'TEN-001'
      });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Agency creation failed.';
    }
  },

  updateAgency: async (agencyId, payload) => {
    const response = await apiClient.put(`/crm/agencies/${agencyId}`, payload);
    return response.data;
  },

  getAgency: async (agencyId) => {
    try {
      const response = await apiClient.get(`/crm/agencies/${agencyId}`, { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Agency record could not be loaded.';
    }
  },

  // 📇 Contact persons bound to an agency (promotable into a broker / AgencyAdmin).
  getAgencyContacts: async (agencyId) => {
    try {
      const response = await apiClient.get(`/crm/agencies/${agencyId}/contacts`, { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Contact list sync failed.';
    }
  },

  createContactPerson: async (agencyId, payload) => {
    try {
      const response = await apiClient.post(`/crm/agencies/${agencyId}/contacts`, payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Contact person creation failed.';
    }
  },

  updateContactPerson: async (contactId, payload) => {
    try {
      const response = await apiClient.put(`/crm/contact-persons/${contactId}`, payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Contact person update failed.';
    }
  },

  deleteContactPerson: async (contactId) => {
    try {
      const response = await apiClient.delete(`/crm/contact-persons/${contactId}`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Contact person removal failed.';
    }
  },

  promoteContactToBroker: async (contactId) => {
    try {
      const response = await apiClient.post(`/crm/contact-persons/${contactId}/promote-to-broker`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Promotion to broker failed.';
    }
  },

  getBrokers: async (page = 1) => {
    try {
      const response = await apiClient.get('/crm/brokers', { params: { page, ...crmService._tenantQueryParams() } });
      const data = Array.isArray(response.data) ? response.data : (response.data.items || []);
      return Object.assign(data, {
        totalCount: Number(response.headers['x-total-count'] || 0),
        aggregates: parseAggregatesHeader(response.headers)
      });
    } catch (error) {
      throw error.response?.data?.message || 'Broker list sync failed.';
    }
  },

  createBroker: async (payload) => {
    try {
      const response = await apiClient.post('/crm/brokers', {
        ...payload,
        tenantId: crmService._resolveWriteTenantId() || payload.tenantId || 'TEN-001'
      });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Broker onboarding failed.';
    }
  },

  updateBroker: async (brokerId, payload) => {
    const response = await apiClient.put(`/crm/brokers/${brokerId}`, payload);
    return response.data;
  },

  createLead: async (leadDetails) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const payload = {
        ...leadDetails,
        tenantId: crmService._resolveWriteTenantId() || userProfile.tenantId,
        allocatedToAgent: userProfile.role === 'Broker' ? (userProfile.name || 'Unassigned Pool') : (leadDetails.allocatedToAgent || 'Unassigned Pool')
      };
      const response = await apiClient.post('/crm/lead/create', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to submit unverified lead.';
    }
  },

   updateLeadStage: async (leadId, nextStage, notesUpdate = '', propertyId = null, sellerName = '') => {
     try {
       const payload = {
         leadId: parseInt(leadId, 10),
         nextStage,
         notesUpdate,
         linkToPropertyId: propertyId ? parseInt(propertyId, 10) : null,
         sellerNameIfNewDeal: sellerName
       };
       const response = await apiClient.post('/crm/update-lead-stage', payload);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to progress lead milestone.';
     }
   },

   updateLead: async (leadId, payload) => {
     try {
       const response = await apiClient.put(`/crm/lead/${leadId}`, payload);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to update lead.';
     }
   },

    updateDealStage: async (dealId, nextStage, notesUpdate = '') => {
      try {
        const payload = { dealId: parseInt(dealId, 10), nextStage, notesUpdate };
        const response = await apiClient.post('/crm/update-deal-stage', payload);
        return response.data;
      } catch (error) {
        throw error.response?.data?.message || 'Failed to progress deal phase.';
      }
    },

   updateDeal: async (dealId, payload) => {
     try {
       const response = await apiClient.put(`/crm/deal/${dealId}`, payload);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to update deal.';
     }
   },

   // 🚀 Promote a lead to a deal with automatic property matching (rent/sale).
   promoteLeadToDeal: async (leadId, options = {}) => {
     try {
       const response = await apiClient.post(`/crm/leads/${leadId}/promote`, {
         isRent: !!options.isRent,
        propertyTypeOverride: options.propertyTypeOverride || null,
        preferredAreaOverride: options.preferredAreaOverride || null,
        minSize: options.minSize || null,
        maxSize: options.maxSize || null,
         linkToPropertyId: options.linkToPropertyId ? parseInt(options.linkToPropertyId, 10) : null,
         sellerNameIfNewDeal: options.sellerNameIfNewDeal || null
       });
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Deal auto-promotion engine failed.';
     }
   },

   // ❌ Delete a deal (AgencyAdmin + PlatformAdmin only; Broker/Owner/Tenant are forbidden server-side).
   deleteDeal: async (dealId) => {
     try {
       const response = await apiClient.delete(`/crm/deals/${dealId}`);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Deal deletion permission denied or failed.';
     }
   },


  reallocateTarget: async (targetId, isDeal, targetAgentName) => {
    try {
      const payload = { targetId: parseInt(targetId, 10), isDeal, targetAgentName };
      const response = await apiClient.post('/crm/reallocate', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to reallocate handler.';
    }
  },

  getPipelineReportSummary: async () => {
    try {
      const response = await apiClient.get('/crm/report/summary', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Analytics metrics retrieval failed.';
    }
  },

  // 📊 Commission realised per deal stage (only ClosedWon carries realised commission).
  getCommissionByStage: async () => {
    try {
      const response = await apiClient.get('/crm/report/commission-by-stage', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Commission stage report unavailable.';
    }
  },

  // 📊 Broker leaderboard: deal count + pipeline value, ranked.
  getBrokerLeaderboard: async () => {
    try {
      const response = await apiClient.get('/crm/report/broker-leaderboard', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Broker leaderboard unavailable.';
    }
  },

  // 📊 Market share of property types — pulled from the X-Aggregates header
  // so the chart reflects the ENTIRE filtered universe, not just page 1.
  getPropertyMarketShare: async () => {
    try {
      const response = await apiClient.post('/properties/search?page=1', { propertyType: 'All', listingType: 'All' });
      const buckets = parseAggregatesHeader(response.headers)?.propertyType || {};
      const total = Object.values(buckets).reduce((a, b) => a + Number(b || 0), 0);
      return Object.entries(buckets).map(([name, value]) => ({
        name,
        value: Number(value || 0),
        pct: total ? Math.round((Number(value || 0) / total) * 100) : 0
      }));
    } catch {
      return [];
    }
  },

  getCompanies: async (page = 1) => {
    const response = await apiClient.get('/crm/companies', { params: { page, ...crmService._tenantQueryParams() } });
    const data = Array.isArray(response.data) ? response.data : [];
    return Object.assign(data, {
      totalCount: Number(response.headers['x-total-count'] || 0),
      aggregates: parseAggregatesHeader(response.headers)
    });
  },

  updateCompany: async (companyPayload) => {
    const response = await apiClient.put('/crm/company/update', companyPayload);
    return response.data;
  },

  getLegalEntities: async (page = 1) => {
    const response = await apiClient.get('/crm/legal-entities', { params: { page, ...crmService._tenantQueryParams() } });
    const data = Array.isArray(response.data) ? response.data : [];
    return Object.assign(data, {
      totalCount: Number(response.headers['x-total-count'] || 0),
      aggregates: parseAggregatesHeader(response.headers)
    });
  },

  updateLegalEntity: async (legalEntityPayload) => {
    const response = await apiClient.put('/crm/legal-entity/update', legalEntityPayload);
    return response.data;
  },

  getContacts: async (page = 1) => {
    const response = await apiClient.get('/crm/contacts', { params: { page, ...crmService._tenantQueryParams() } });
    const data = Array.isArray(response.data) ? response.data : [];
    return Object.assign(data, {
      totalCount: Number(response.headers['x-total-count'] || 0),
      aggregates: parseAggregatesHeader(response.headers)
    });
  },

  searchContacts: async (query = '') => {
    try {
      const response = await apiClient.get('/crm/contacts/search', { params: { q: query, page: 1, ...crmService._tenantQueryParams() } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Contact search failed.';
    }
  },

  updateContact: async (contactPayload) => {
    const response = await apiClient.put('/crm/contact/update', contactPayload);
    return response.data;
  },

  // 🧾 Brochure card (cart) for the Presentation stage.
  getBrochureCardByDeal: async (dealId) => {
    const response = await apiClient.get('/crm/brochure-cards', { params: { dealId: parseInt(dealId, 10), ...crmService._tenantQueryParams() } });
    return response.data;
  },

  getActiveBrochureCards: async () => {
    const response = await apiClient.get('/crm/brochure-cards/active', { params: crmService._tenantQueryParams() });
    return response.data || [];
  },

  // Upsert the broker's card (client details + curated unit line items; max 5 units).
  saveBrochureCard: async (cardPayload) => {
    const response = await apiClient.put('/crm/brochure-cards', cardPayload);
    return response.data;
  },

  removeBrochureCardLine: async (cardId, lineId) => {
    const response = await apiClient.delete(`/crm/brochure-cards/${cardId}/lines/${lineId}`);
    return response.data;
  },

  getDealBrokerSplits: async (dealId) => {
    const response = await apiClient.get(`/crm/deals/${dealId}/broker-splits`, { params: crmService._tenantQueryParams() });
    return response.data;
  },

  upsertDealBrokerSplits: async (dealId, splits) => {
    const response = await apiClient.put(`/crm/deals/${dealId}/broker-splits`, { dealId, splits });
    return response.data;
  },

  linkContactToDeal: async (contactId, dealId) => {
    const response = await apiClient.post(`/crm/contacts/${contactId}/link-deal/${dealId}`);
    return response.data;
  }
};
