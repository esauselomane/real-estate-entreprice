import apiClient from './apiClient.js';
import { propertyService } from './propertyService.js';

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
// 🛡️ Axios rejects with a real Error object, but the *re-thrown* value
// inside our crmService call-sites can be whatever
// `error.response?.data?.message` happens to be — sometimes a string,
// sometimes an object (e.g. ProblemDetails with nested fields). React
// components then crash with "Objects are not valid as a React child"
// when they try to render the error string. Always coerce.
const normalizeError = (err, fallback) => {
  if (!err) return fallback;
  if (typeof err === 'string') return err;
  if (err.message) return err.message;
  try { return JSON.stringify(err); } catch { return fallback; }
};

function parseAggregatesHeader(headers) {
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
      throw normalizeError(error.response?.data?.message, 'Dual pipeline boards data sync extraction failed.');
    }
  },

   getOwnerTenantRecords: async (page = 1) => {
     try {
       const response = await apiClient.get('/crm/contacts', { params: { page, ...crmService._tenantQueryParams() } });
       const data = Array.isArray(response.data) ? response.data : [];
       return Object.assign(data, {
         totalCount: Number(response.headers['x-total-count'] || 0),
         aggregates: parseAggregatesHeader(response.headers)
       });
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Contact registry sync failed.');
     }
    },

    postOwnerTenantRecordsFiltered: async (filters = {}) => {
      try {
        const body = { page: 1, ...crmService._tenantQueryParams() };
        if (filters.contactType && filters.contactType !== 'Any') body.contactType = filters.contactType;
        if (filters.budgetMin && filters.budgetMin.trim() !== '') body.budgetMin = Number(filters.budgetMin);
        if (filters.budgetMax && filters.budgetMax.trim() !== '') body.budgetMax = Number(filters.budgetMax);
        if (filters.areaIds?.length) body.areaIds = filters.areaIds;
        if (filters.area) body.area = filters.area;
       const response = await apiClient.post('/crm/contacts/search', body);
       const data = Array.isArray(response.data) ? response.data : [];
       return Object.assign(data, {
         totalCount: Number(response.headers['x-total-count'] || 0),
         aggregates: parseAggregatesHeader(response.headers)
       });
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Filtered contact search failed.');
     }
   },

  getOwnerTenantRecord: async (contactId) => {
    try {
      const response = await apiClient.get(`/crm/contacts/${encodeURIComponent(contactId)}`, { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact record could not be loaded.');
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
      throw normalizeError(error.response?.data?.message, 'Geographic area lookup failed.');
    }
  },

  createOwnerTenantRecord: async (payload) => {
    try {
      const response = await apiClient.post('/crm/contacts', {
        ...(crmService._normalizeOwnerInterests(payload)),
        tenantId: crmService._resolveWriteTenantId() || payload.tenantId
      });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact creation failed.');
    }
  },

  updateOwnerTenantRecord: async (payload) => {
    try {
      const response = await apiClient.put(`/crm/contacts/${encodeURIComponent(payload.contactId)}`, crmService._normalizeOwnerInterests(payload));
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact update failed.');
    }
  },

  getCRMReportOverview: async (year = new Date().getFullYear()) => {
    try {
      const response = await apiClient.get('/crm/report/overview', { params: { year, ...crmService._tenantQueryParams() } });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Metrics overview could not be loaded.');
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
      throw normalizeError(error.response?.data?.message, 'Agency list sync failed.');
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
      throw normalizeError(error.response?.data?.message, 'Agency roster sync failed.');
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
      throw normalizeError(error.response?.data?.message, 'Agency creation failed.');
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
      throw normalizeError(error.response?.data?.message, 'Agency record could not be loaded.');
    }
  },

  // 📇 Agency-scoped contacts (staff contacts bound to an agency).
  getAgencyContacts: async (agencyId) => {
    try {
      const response = await apiClient.get(`/crm/agencies/${agencyId}/contacts`, { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact list sync failed.');
    }
  },

  createContactPerson: async (agencyId, payload) => {
    try {
      const response = await apiClient.post(`/crm/agencies/${agencyId}/contacts`, payload);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact creation failed.');
    }
  },

  updateContactPerson: async (contactId, payload) => {
    try {
      const response = await apiClient.put(`/crm/contacts/${encodeURIComponent(contactId)}`, payload);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact update failed.');
    }
  },

  deleteContactPerson: async (contactId) => {
    try {
      const response = await apiClient.delete(`/crm/contacts/${encodeURIComponent(contactId)}`);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact removal failed.');
    }
  },

  promoteContactToBroker: async (contactId) => {
    try {
      const response = await apiClient.post(`/crm/contacts/${encodeURIComponent(contactId)}/promote-to-broker`);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Promotion to broker failed.');
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
      throw normalizeError(error.response?.data?.message, 'Broker list sync failed.');
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
      throw normalizeError(error.response?.data?.message, 'Broker onboarding failed.');
    }
  },

  updateBroker: async (brokerId, payload) => {
    const response = await apiClient.put(`/crm/brokers/${brokerId}`, payload);
    return response.data;
  },

  createLead: async (leadDetails) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      // 🧑‍💼 Brokers always self-assign: the backend stamps the caller's
      // NameIdentifier claim into AllocatedBrokerIds. For non-brokers we
      // trust whatever the form already provided (csv of broker ids).
      const payload = {
        ...leadDetails,
        tenantId: crmService._resolveWriteTenantId() || userProfile.tenantId,
        allocatedBrokerIds: userProfile.role === 'Broker'
          ? (userProfile.id || leadDetails.allocatedBrokerIds || '')
          : (leadDetails.allocatedBrokerIds || '')
      };
      const response = await apiClient.post('/crm/lead/create', payload);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Failed to submit unverified lead.');
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
       throw normalizeError(error.response?.data?.message, 'Failed to progress lead milestone.');
     }
   },

    updateLead: async (leadId, payload) => {
      try {
        // 🧑‍💼 LeadRecord.TenantId is [Required] on the server (model-bound by
        // [ApiController]), so the PUT body must carry a non-empty tenant id
        // or the request 400s before the handler runs — even though the
        // action itself scopes via the JWT tenant (GetCurrentTenant).
        const response = await apiClient.put(`/crm/lead/${leadId}`, {
          ...payload,
          tenantId: payload.tenantId || crmService._resolveWriteTenantId(),
        });
        return response.data;
      } catch (error) {
        throw normalizeError(error.response?.data?.message, 'Failed to update lead.');
      }
    },

    updateDealStage: async (dealId, nextStage, notesUpdate = '') => {
      try {
        const payload = { dealId: parseInt(dealId, 10), nextStage, notesUpdate };
        const response = await apiClient.post('/crm/update-deal-stage', payload);
        return response.data;
      } catch (error) {
        throw normalizeError(error.response?.data?.message, 'Failed to progress deal phase.');
      }
    },

    updateDeal: async (dealId, payload) => {
      try {
        const response = await apiClient.put(`/crm/deal/${dealId}`, payload);
        return response.data;
      } catch (error) {
        throw normalizeError(error.response?.data?.message, 'Failed to update deal.');
      }
    },

    // 📝 Append a timestamped review note to a deal's DealNotes.
    addDealReviewNote: async (dealId, noteText = '') => {
      try {
        const response = await apiClient.post(`/crm/deal/${dealId}/notes`, { noteText });
        return response.data;
      } catch (error) {
        throw normalizeError(error.response?.data?.message, 'Failed to save deal note.');
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
       throw normalizeError(error.response?.data?.message, 'Deal auto-promotion engine failed.');
     }
   },

   // ❌ Delete a deal (AgencyAdmin + PlatformAdmin only; Broker/Owner/Tenant are forbidden server-side).
   deleteDeal: async (dealId) => {
     try {
       const response = await apiClient.delete(`/crm/deals/${dealId}`);
       return response.data;
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Deal deletion permission denied or failed.');
     }
   },


  reallocateTarget: async (targetId, isDeal, targetAgentName) => {
    try {
      const payload = { targetId: parseInt(targetId, 10), isDeal, targetAgentName };
      const response = await apiClient.post('/crm/reallocate', payload);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Failed to reallocate handler.');
    }
  },

  getPipelineReportSummary: async () => {
    try {
      const response = await apiClient.get('/crm/report/summary', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Analytics metrics retrieval failed.');
    }
  },

  // 📊 Commission realised per deal stage (only ClosedWon carries realised commission).
  getCommissionByStage: async () => {
    try {
      const response = await apiClient.get('/crm/report/commission-by-stage', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Commission stage report unavailable.');
    }
  },

  // 📊 Broker leaderboard: deal count + pipeline value, ranked.
  getBrokerLeaderboard: async () => {
    try {
      const response = await apiClient.get('/crm/report/broker-leaderboard', { params: crmService._tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Broker leaderboard unavailable.');
    }
  },

  // 📊 Market share of property types — pulled from the X-Aggregates header
  // so the chart reflects the ENTIRE filtered universe, not just page 1.
  // Use propertyService's resolver so the active-tenant subtree (cached in
  // localStorage) is passed as a comma-separated tenantId list — a top-level
  // agency with descendants would otherwise only return its own row.
  getPropertyMarketShare: async () => {
    try {
      const override = propertyService._tenantQueryParams();
      const queryString = new URLSearchParams({ page: 1, pageSize: 1, ...override }).toString();
      const response = await apiClient.post(`/properties/search?${queryString}`, { propertyType: 'All', listingType: 'All' });
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

   // 🔎 Auto-complete search over companies (by business name / registration / VAT).
   searchCompanies: async (query = '') => {
     try {
       const response = await apiClient.get('/crm/companies/search', { params: { q: query || '', page: 1, ...crmService._tenantQueryParams() } });
       return Array.isArray(response.data) ? response.data : [];
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Company search failed.');
     }
   },

   // 🔎 Auto-complete search over legal entities (by registered name / tax ref / type).
   searchLegalEntities: async (query = '') => {
     try {
       const response = await apiClient.get('/crm/legal-entities/search', { params: { q: query || '', page: 1, ...crmService._tenantQueryParams() } });
       return Array.isArray(response.data) ? response.data : [];
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Legal entity search failed.');
     }
   },

   // 🆕 Create a new CRM company. The server stamps TenantId from the JWT, so a
   // __pending__ placeholder is sent to satisfy the [Required] model binding.
   createCompany: async (payload) => {
     try {
       const response = await apiClient.post('/crm/company/create', { tenantId: '__pending__', ...payload });
       return response.data;
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Company creation failed.');
     }
   },

   // 🆕 Create a new CRM legal entity. Belongs to a company via AssociatedCompanyId.
   createLegalEntity: async (payload) => {
     try {
       const response = await apiClient.post('/crm/legal-entity/create', { tenantId: '__pending__', ...payload });
       return response.data;
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Legal entity creation failed.');
     }
   },

   // 🧾 Fetch a single company by id (edit hydration + resolving SearchableSelect).
   getCompany: async (companyId) => {
     if (!companyId) return null;
     try {
       const response = await apiClient.get(`/crm/companies/${encodeURIComponent(companyId)}`, { params: crmService._tenantQueryParams() });
       return response.data || null;
     } catch (error) {
       if (error.response?.status === 404) return null;
       throw normalizeError(error.response?.data?.message, 'Company lookup failed.');
     }
   },

   // 🧾 Fetch a single legal entity by id.
   getLegalEntity: async (legalEntityId) => {
     if (!legalEntityId) return null;
     try {
       const response = await apiClient.get(`/crm/legal-entities/${encodeURIComponent(legalEntityId)}`, { params: crmService._tenantQueryParams() });
       return response.data || null;
     } catch (error) {
       if (error.response?.status === 404) return null;
       throw normalizeError(error.response?.data?.message, 'Legal entity lookup failed.');
     }
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
      throw normalizeError(error.response?.data?.message, 'Contact search failed.');
    }
  },

  // 🔎 Fetch a single contact by id — used to hydrate an already-linked contact
  // into the ContactPicker when opening an edit form. Returns null on 404.
  getContact: async (contactId) => {
    if (!contactId) return null;
    try {
      const response = await apiClient.get(`/crm/contacts/${encodeURIComponent(contactId)}`, { params: crmService._tenantQueryParams() });
      return response.data || null;
    } catch (error) {
      if (error.response?.status === 404) return null;
      throw normalizeError(error.response?.data?.message, 'Contact lookup failed.');
    }
  },

  // 🆕 Inline "create new contact" used by the lease ContactPicker when the
  // tenant/lessee doesn't exist yet. POSTs to /crm/contacts and returns
  // the new Contact (with ContactId) on success.
  createContact: async (payload) => {
    try {
      const response = await apiClient.post('/crm/contacts', {
        ...crmService._normalizeOwnerInterests(payload),
        tenantId: crmService._resolveWriteTenantId() || payload.tenantId || '__pending__'
      });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Contact creation failed.');
    }
  },

  updateContact: async (contactPayload) => {
    const response = await apiClient.put(`/crm/contacts/${encodeURIComponent(contactPayload.contactId)}`, crmService._normalizeOwnerInterests(contactPayload));
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
  },

  // ============================================================
  // 🛎️ BROKER DASHBOARD + NOTIFICATIONS
  // ============================================================

  // 📊 KPI metrics for the broker dashboard. Pass brokerId to filter
  // to a single broker; omit to aggregate the whole tenant.
  getBrokerMetrics: async (brokerId = null) => {
    try {
      const params = { ...crmService._tenantQueryParams() };
      if (brokerId) params.brokerId = brokerId;
      const response = await apiClient.get('/crm/report/broker-metrics', { params });
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Broker metrics sync failed.');
    }
  },

  // 📋 Agent performance leaderboard.
  getBrokerLeaderboard: async (brokerId = null) => {
    try {
      const params = { ...crmService._tenantQueryParams() };
      if (brokerId) params.brokerId = brokerId;
      const response = await apiClient.get('/crm/report/broker-leaderboard-v2', { params });
      return response.data || [];
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Leaderboard sync failed.');
    }
  },

  // 📥 Compliance inbox (expiring mandates + awaiting-broker-signature).
  getComplianceInbox: async (brokerId = null) => {
    try {
      const params = { ...crmService._tenantQueryParams() };
      if (brokerId) params.brokerId = brokerId;
      const response = await apiClient.get('/crm/compliance/inbox', { params });
      return response.data || [];
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Compliance inbox sync failed.');
    }
  },

  // 🛎️ Notification inbox.
  getNotifications: async ({ unreadOnly = false, limit = 50 } = {}) => {
    try {
      const response = await apiClient.get('/crm/notifications', {
        params: { unreadOnly, limit, ...crmService._tenantQueryParams() }
      });
      return response.data || { items: [], unreadCount: 0 };
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Notifications sync failed.');
    }
  },

  markNotificationRead: async (notificationId) => {
    try {
      const response = await apiClient.post(`/crm/notifications/${notificationId}/read`);
      return response.data;
    } catch (error) {
      throw normalizeError(error.response?.data?.message, 'Mark-read failed.');
    }
  },

   createNotification: async ({ userId, category, title, body, linkType, linkId }) => {
     try {
       const response = await apiClient.post('/crm/notifications', { userId, category, title, body, linkType, linkId });
       return response.data;
     } catch (error) {
       throw normalizeError(error.response?.data?.message, 'Notification create failed.');
     }
   },

    // 📥 Import data: download Excel template
    downloadImportTemplate: async () => {
      const response = await apiClient.get('/properties/import/template', { responseType: 'blob' });
      return response.data;
    },

    // 📥 Import data: upload Excel file (returns jobId)
    uploadImportFile: async (fileName, base64Content) => {
      const response = await apiClient.post('/properties/import/upload', { fileName, base64Content });
      return response.data;
    },

    // 📥 Get import job status
    getImportJobStatus: async (jobId) => {
      const response = await apiClient.get(`/properties/import/jobs/${jobId}`);
      return response.data;
    }
  };
