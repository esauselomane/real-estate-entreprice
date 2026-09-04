const toMessage = (err, fallback) => { if (!err) return fallback; if (typeof err === 'string') return err; if (err.message) return err.message; try { return JSON.stringify(err); } catch { return fallback; } };

import apiClient from './apiClient.js';

export const leadCaptureService = {
  // 🔐 SuperAdmin — paginated list of leads, optionally filtered by status.
  // Returns { data, totalCount } where totalCount comes from the X-Total-Count header.
  getLeads: async (page = 1, status = null) => {
    try {
      const params = { page };
      if (status) params.status = status;
      const response = await apiClient.get('/system/lead-requests', { params });
      return {
        data: response.data || [],
        totalCount: Number(response.headers['x-total-count'] || 0),
      };
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Lead list sync failed.'));
    }
  },

  // 🟢 PUBLIC — capture a new lead from the marketing website.
  createLead: async (payload) => {
    try {
      const response = await apiClient.post('/system/lead-requests', payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Lead capture failed.'));
    }
  },

  // 🔐 SuperAdmin — update editable lead details.
  updateLead: async (id, payload) => {
    try {
      const response = await apiClient.put(`/system/lead-requests/${id}`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Lead update failed.'));
    }
  },

  // 🔐 SuperAdmin — advance a lead through the funnel.
  updateLeadStatus: async (id, status) => {
    try {
      const response = await apiClient.put(`/system/lead-requests/${id}/status`, { status });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Lead status update failed.'));
    }
  },

  // 🔐 SuperAdmin — get workflow history for a lead.
  getLeadWorkflow: async (id) => {
    try {
      const response = await apiClient.get(`/system/lead-requests/${id}/workflow`);
      return response.data || [];
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Workflow history load failed.'));
    }
  },

  // 🔐 SuperAdmin — add a workflow action to a lead.
  addWorkflowAction: async (id, payload) => {
    try {
      const response = await apiClient.post(`/system/lead-requests/${id}/workflow`, payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Workflow action failed.'));
    }
  },

  // 🟢 PUBLIC — list active pricing packages for the marketing website.
  getPricingPackages: async () => {
    try {
      const response = await apiClient.get('/system/lead-requests/pricing-packages');
      return response.data || [];
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Pricing packages load failed.'));
    }
  },
};
