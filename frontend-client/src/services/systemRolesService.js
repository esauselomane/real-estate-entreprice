import apiClient from './apiClient.js';

// 🔐 System-wide role configuration (Identity microservice, SystemAdmin-only surface)
export const systemRolesService = {
  getSystemRoles: async () => {
    const response = await apiClient.get('/system/roles');
    return response.data;
  },

  createSystemRole: async (payload) => {
    const response = await apiClient.post('/system/roles', payload);
    return response.data;
  },

  updateSystemRole: async (id, payload) => {
    const response = await apiClient.put(`/system/roles/${id}`, payload);
    return response.data;
  },

  deleteSystemRole: async (id) => {
    const response = await apiClient.delete(`/system/roles/${id}`);
    return response.data;
  }
};
