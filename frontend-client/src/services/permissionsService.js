import apiClient from './apiClient.js';

// 🔐 Permission registry (Identity microservice, SystemAdmin-only surface)
export const permissionsService = {
  getPermissions: async () => {
    const response = await apiClient.get('/system/permissions');
    return response.data;
  },

  getPermission: async (id) => {
    const response = await apiClient.get(`/system/permissions/${id}`);
    return response.data;
  },

  createPermission: async (payload) => {
    const response = await apiClient.post('/system/permissions', payload);
    return response.data;
  },

  updatePermission: async (id, payload) => {
    const response = await apiClient.put(`/system/permissions/${id}`, payload);
    return response.data;
  },

  deletePermission: async (id) => {
    const response = await apiClient.delete(`/system/permissions/${id}`);
    return response.data;
  }
};
