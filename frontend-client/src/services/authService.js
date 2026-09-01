import apiClient from './apiClient.js';

export const authService = {
  
  // --- 🔐 MULTI-TENANT SECURE AUTHENTICATION TRANSFERS ---
  login: async (email, password) => {
    try {
      const response = await apiClient.post('/auth/login', { email, password });
      
      if (response.data && response.data.token) {
        localStorage.setItem('user_session_jwt', response.data.token);
        localStorage.setItem('user_profile_data', JSON.stringify(response.data.profile));
      }
      
      return response.data;
    } catch (error) {
      const message = error.response?.data?.message 
        || error.message 
        || 'Authentication framework link failed.';
      throw new Error(message);
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

  // --- 👥 USER ROLE ADMINISTRATION (add / remove / reassign a SystemRole on an account) ---
  getUsers: async () => {
    try {
      const response = await apiClient.get('/auth/users');
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'User directory sync failed.';
    }
  },
   updateUserRole: async (userId, systemRole) => {
    try {
      const response = await apiClient.put(`/auth/users/${userId}/role`, { systemRole });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Role reassignment failed.';
    }
  },

   // --- 🚫 DEACTIVATE / disable a user account ( AgencyAdmin: own-tenant only ) ---
   disableUser: async (userId) => {
     try {
       const response = await apiClient.put(`/auth/users/${userId}/disable`);
       return response.data;
     } catch (error) {
       throw error.response?.data?.message || 'User deactivation failed.';
     }
   },

  // --- 📝 TRIAL ACCOUNT: Set password from activation token ---
  setTrialPassword: async (token, password) => {
    try {
      const response = await apiClient.post('/auth/trial/set-password', { token, password });
      return response.data;
    } catch (error) {
      throw error.response?.data?.message || 'Failed to set trial password.';
    }
  },

    // --- 👤 LOCAL ACCOUNT STATUS CLEARING ---
    logout: () => {
     localStorage.removeItem('user_session_jwt');
     localStorage.removeItem('user_profile_data');
     window.location.reload(); // Instantly hard resets the active canvas back to the login screen gate
    }
  };

// 🛡️ Resolve the caller's canonical role from the persisted profile for UI gating.
// Mirrors the backend normalization (SuperAdmin->PlatformAdmin, etc.) so the shell
// only renders add/delete controls that the role is actually permitted to invoke.
export const getCurrentRole = () => {
  try {
    const profile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    return (profile.role || '').toString().trim() || '';
  } catch { return ''; }
};

export const isReadOnlyRole = () => {
  const role = getCurrentRole();
  return role === 'Tenant' || role === 'Owner';
};

export const canManageUnits = () => !isReadOnlyRole();

// 🔍 Check trial status from the persisted profile (set during login JWT decode).
export const isTrialUser = () => {
  try {
    const profile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    return profile.isTrialActive === true;
  } catch { return false; }
};

export const getTrialExpiry = () => {
  try {
    const profile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
    return profile.trialExpiresAt || null;
  } catch { return null; }
};
