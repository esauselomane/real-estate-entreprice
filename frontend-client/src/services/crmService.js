import apiClient from './apiClient.js';

export const crmService = {
   getOperationalRecords: async (module, page = 1) => {
    const response = await apiClient.get('/crm/operations', { params: { module, page } });
    return response.data;
  },

   // --- 🔗 UNIT-SCOPED OPERATIONAL RECORDS (leases / maintenance / document vault) ---
  getOperationalRecordsForUnit: async (module, propertyId, propertyUnitId, page = 1) => {
    const response = await apiClient.get('/crm/operations', {
      params: { module, propertyId, propertyUnitId, page }
    });
    return response.data;
  },

  createOperationalRecord: async (payload) => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.post('/crm/operations', { ...payload, tenantId: userProfile.tenantId || payload.tenantId });
    return response.data;
  },

  updateOperationalRecord: async (recordId, payload) => {
    const response = await apiClient.put(`/crm/operations/${recordId}`, payload);
    return response.data;
  },
  getCRMBoards: async (page = 1) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const parameters = {
        page,
        agentName: userProfile.name || '',
        userRole: userProfile.role || 'Agent'
      };
      const response = await apiClient.get('/crm/boards', { params: parameters });
      // X-Lead-Count / X-Deal-Count expose tenant-scoped totals for pagination
      return {
        ...response.data,
        leadCount: Number(response.headers['x-lead-count'] || 0),
        dealCount: Number(response.headers['x-deal-count'] || 0)
      };
    } catch (error) {
      throw error.response?.data?.message || 'Dual pipeline boards data sync extraction failed.';
    }
  },

  getOwnerTenantRecords: async (page = 1) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/owners-tenants', { params: { page } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant registry sync failed.';
    }
  },

  getOwnerTenantRecord: async (ownerTenantId) => {
    try {
      const response = await apiClient.get(`/crm/owners-tenants/${ownerTenantId}`);
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
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.post('/crm/owners-tenants', {
        ...(crmService._normalizeOwnerInterests(payload)),
        tenantId: userProfile.tenantId || payload.tenantId
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
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/report/overview', { params: { year } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Metrics overview could not be loaded.';
    }
  },

  getAgencies: async (page = 1) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/agencies', { params: { page } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Agency list sync failed.';
    }
  },

  createAgency: async (payload) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.post('/crm/agencies', {
        ...payload,
        tenantId: userProfile.tenantId || payload.tenantId || 'TEN-001'
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
      const response = await apiClient.get(`/crm/agencies/${agencyId}`);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Agency record could not be loaded.';
    }
  },

  // 📇 Contact persons bound to an agency (promotable into a broker / AgencyAdmin).
  getAgencyContacts: async (agencyId) => {
    try {
      const response = await apiClient.get(`/crm/agencies/${agencyId}/contacts`);
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
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/brokers', { params: { page } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Broker list sync failed.';
    }
  },

  createBroker: async (payload) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.post('/crm/brokers', {
        ...payload,
        tenantId: userProfile.tenantId || payload.tenantId || 'TEN-001'
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
        tenantId: userProfile.tenantId,
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

   updateDealStage: async (dealId, nextStage, notesUpdate = '') => {
     try {
       const payload = { dealId: parseInt(dealId, 10), nextStage, notesUpdate };
       const response = await apiClient.post('/crm/update-deal-stage', payload);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'Failed to progress deal phase.';
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
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/report/summary', { params: {} });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Analytics metrics retrieval failed.';
    }
  },

  // 📊 Commission realised per deal stage (only ClosedWon carries realised commission).
  getCommissionByStage: async () => {
    try {
      const response = await apiClient.get('/crm/report/commission-by-stage');
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Commission stage report unavailable.';
    }
  },

  // 📊 Broker leaderboard: deal count + pipeline value, ranked.
  getBrokerLeaderboard: async () => {
    try {
      const response = await apiClient.get('/crm/report/broker-leaderboard');
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Broker leaderboard unavailable.';
    }
  },

  // 📊 Market share of property types (client-aggregated from the listing inventory).
  getPropertyMarketShare: async () => {
    try {
      const properties = await apiClient.post('/properties/search?page=1', { propertyType: 'All', listingType: 'All' }).then((r) => r.data);
      const counts = {};
      let total = 0;
      for (const p of properties) {
        if (p.propertyTypeId != null || p.propertyTypeDetails?.name) {
          const name = p.propertyTypeDetails?.name || `Type ${p.propertyTypeId}`;
          counts[name] = (counts[name] || 0) + 1; total++;
        }
      }
      return Object.entries(counts).map(([name, value]) => ({ name, value, pct: total ? Math.round((value / total) * 100) : 0 }));
    } catch (error) {
      return [];
    }
  },

  getCompanies: async (page = 1) => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/companies', { params: { page } });
    return response.data;
  },

  updateCompany: async (companyPayload) => {
    const response = await apiClient.put('/crm/company/update', companyPayload);
    return response.data;
  },

  getLegalEntities: async (page = 1) => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/legal-entities', { params: { page } });
    return response.data;
  },

  updateLegalEntity: async (legalEntityPayload) => {
    const response = await apiClient.put('/crm/legal-entity/update', legalEntityPayload);
    return response.data;
  },

  getContacts: async (page = 1) => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/contacts', { params: { page } });
    return response.data;
  },

  updateContact: async (contactPayload) => {
    const response = await apiClient.put('/crm/contact/update', contactPayload);
    return response.data;
  }
};
