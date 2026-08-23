import apiClient from './ apiClient.js';

export const authService = {
  
  // --- 🔐 MULTI-TENANT SECURE AUTHENTICATION TRANSFERS ---
  login: async (email, password) => {
    try {
      const response = await apiClient.post('/auth/login', { email, password });
      
      if (response.data && response.data.token) {
        // Commit the cryptographic access key token cleanly to browser memory registers
        localStorage.setItem('user_session_jwt', response.data.token);
        localStorage.setItem('user_profile_data', JSON.stringify(response.data.profile));
      }
      
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Authentication framework link failed.';
    }
  },

  // --- 📨 SELF-SERVICE PASSWORD RECOVERY EMAIL INTERFACE ---
  forgotPassword: async (email) => {
    try {
      const response = await apiClient.post('/auth/forgot-password', { email });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Outbound email recovery link trigger failed.';
    }
  },
  // --- 🏢 ROLE-RESTRICTED REAL ESTATE USER PROVISIONING ---
  registerUser: async (newUserPayload) => {
    try {
      // Pull active BA profile metadata to append multi-tenant context validation
      const userProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      
      const payload = {
        ...newUserPayload,
        tenantId: userProfile.tenantId // Automatically force-inject parent agency sandbox identifier
      };

      const response = await apiClient.post('/auth/register-user', payload);
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Corporate identity profile registration failed.';
    }
  },

  // --- 👤 LOCAL ACCOUNT STATUS CLEARING ---
  logout: () => {
    localStorage.removeItem('user_session_jwt');
    localStorage.removeItem('user_profile_data');
    window.location.reload(); // Instantly hard resets the active canvas back to the login screen gate
  }
};
