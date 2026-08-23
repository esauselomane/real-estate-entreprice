import apiClient from './ apiClient.js';

export const crmService = {
  getCRMBoards: async () => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const parameters = {
        tenantId: userProfile.tenantId || '',
        agentName: userProfile.name || '',
        userRole: userProfile.role || 'Agent'
      };
      const response = await apiClient.get('/crm/boards', { params: parameters });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Dual pipeline boards data sync extraction failed.';
    }
  },

  getOwnerTenantRecords: async () => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/owners-tenants', { params: { tenantId: userProfile.tenantId || '' } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant registry sync failed.';
    }
  },

  createOwnerTenantRecord: async (payload) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.post('/crm/owners-tenants', {
        ...payload,
        tenantId: userProfile.tenantId || payload.tenantId
      });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant record creation failed.';
    }
  },

  updateOwnerTenantRecord: async (payload) => {
    try {
      const response = await apiClient.put('/crm/owners-tenants', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Owner/tenant record update failed.';
    }
  },

  getCRMReportOverview: async (year = new Date().getFullYear()) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/report/overview', { params: { tenantId: userProfile.tenantId || '', year } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Metrics overview could not be loaded.';
    }
  },

  getAgencies: async () => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/agencies', { params: { tenantId: userProfile.tenantId || '' } });
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

  getBrokers: async () => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const response = await apiClient.get('/crm/brokers', { params: { tenantId: userProfile.tenantId || '' } });
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

  createLead: async (leadDetails) => {
    try {
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      const payload = {
        ...leadDetails,
        tenantId: userProfile.tenantId,
        allocatedToAgent: userProfile.role === 'Agent' ? userProfile.name : (leadDetails.allocatedToAgent || 'Unassigned Pool')
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
      const response = await apiClient.get('/crm/report/summary', { params: { tenantId: userProfile.tenantId || '' } });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Analytics metrics retrieval failed.';
    }
  },

  getCompanies: async () => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/companies', { params: { tenantId: userProfile.tenantId || '' } });
    return response.data;
  },

  updateCompany: async (companyPayload) => {
    const response = await apiClient.put('/crm/company/update', companyPayload);
    return response.data;
  },

  getLegalEntities: async () => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/legal-entities', { params: { tenantId: userProfile.tenantId || '' } });
    return response.data;
  },

  updateLegalEntity: async (legalEntityPayload) => {
    const response = await apiClient.put('/crm/legal-entity/update', legalEntityPayload);
    return response.data;
  },

  getContacts: async () => {
    const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    const response = await apiClient.get('/crm/contacts', { params: { tenantId: userProfile.tenantId || '' } });
    return response.data;
  },

  updateContact: async (contactPayload) => {
    const response = await apiClient.put('/crm/contact/update', contactPayload);
    return response.data;
  }
};
