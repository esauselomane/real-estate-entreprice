import apiClient from './apiClient.js';

const ACTIVE_TENANT_STORAGE_KEY = 'active_tenant_override';
const ACTIVE_TENANT_STORAGE_MARKER = 'view-override';
const USER_PROFILE_STORAGE_KEY = 'user_profile_data';

const toMessage = (err, fallback) => {
  if (!err) return fallback;
  if (typeof err === 'string') return err;
  if (err.message) return err.message;
  try { return JSON.stringify(err); } catch { return fallback; }
};

function resolveActiveTenantOverride() {
  try {
    const raw = localStorage.getItem(ACTIVE_TENANT_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return null;
    if (parsed.__source !== ACTIVE_TENANT_STORAGE_MARKER) return null;
    if (parsed.__all__) return '__all__';
    return parsed.tenantId || null;
  } catch { return null; }
}

function resolveLoggedInTenantId() {
  try {
    const raw = localStorage.getItem(USER_PROFILE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed?.tenantId || null;
  } catch { return null; }
}

// 🌐 Read params: prefer the active-tenant override; fall back to the
// logged-in user's tenant so backend filters always see a tenant id.
function tenantQueryParams() {
  const override = resolveActiveTenantOverride();
  if (override === '__all__') return {};
  if (override) return { tenantId: override };
  const profile = JSON.parse(localStorage.getItem('user_profile_data') || 'null');
      if (['PlatformAdmin', 'SuperAdmin'].includes(profile?.role)) return {};
      const fallback = resolveLoggedInTenantId();
  return fallback ? { tenantId: fallback } : {};
}

// 🛰️ Resolve the tenant id a write should be stamped with. "__all__" is
// never a valid stamp — that mode is read-only.
function resolveWriteTenantId() {
  const override = resolveActiveTenantOverride();
  if (override && override !== '__all__') return override;
  return resolveLoggedInTenantId();
}

export const authService = {
  
  // --- 🔐 MULTI-TENANT SECURE AUTHENTICATION TRANSFERS ---
  login: async (email, password) => {
    try {
      const response = await apiClient.post('/auth/login', { email, password });
      debugger;
      if (response.data && response.data.token) {
        localStorage.setItem('user_session_jwt', response.data.token);
        localStorage.setItem('user_profile_data', JSON.stringify(response.data.profile));
      }
      
      return response.data;
    } catch (error) {
      debugger
      const message = toMessage(error.response?.data?.message, null) || toMessage(error, 'Authentication framework link failed.');
      throw new Error(message, { cause: error });
    }
  },

   // --- 📨 SELF-SERVICE PASSWORD RECOVERY EMAIL INTERFACE ---
   forgotPassword: async (email) => {
    try {
      const response = await apiClient.post('/auth/forgot-password', { email });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Outbound email recovery link trigger failed.'));
    }
  },
  // --- 🏢 ROLE-RESTRICTED REAL ESTATE USER PROVISIONING ---
  registerUser: async (newUserPayload) => {
    try {
      // Active tenant override wins over the logged-in user's tenantId,
      // so a PlatformAdmin/AgencyAdmin can provision a new user into the
      // currently-selected subtree agency rather than their own tenant.
      const payload = {
        ...newUserPayload,
        tenantId: newUserPayload.tenantId || resolveWriteTenantId()
      };

      const response = await apiClient.post('/auth/register-user', payload);
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Corporate identity profile registration failed.'));
    }
  },

  // --- 👥 USER ROLE ADMINISTRATION (add / remove / reassign a SystemRole on an account) ---
  getUsers: async () => {
    try {
      const response = await apiClient.get('/auth/users', { params: tenantQueryParams() });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'User directory sync failed.'));
    }
  },
   updateUserRole: async (userId, systemRole) => {
    try {
      const response = await apiClient.put(`/auth/users/${userId}/role`, { systemRole });
      return response.data;
    } catch (error) {
      throw new Error(toMessage(error.response?.data?.message, 'Role reassignment failed.'));
    }
  },

   // --- 🚫 DEACTIVATE / disable a user account ( AgencyAdmin: own-tenant only ) ---
   disableUser: async (userId) => {
     try {
       const response = await apiClient.put(`/auth/users/${userId}/disable`);
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'User deactivation failed.'));
     }
   },

  // --- 📝 TRIAL ACCOUNT: Set password from activation token ---
   setTrialPassword: async (token, password) => {
     try {
       const response = await apiClient.post('/auth/trial/set-password', { token, password });
       return response.data;
     } catch (error) {
       throw new Error(toMessage(error.response?.data?.message, 'Failed to set trial password.'));
     }
   },

    // --- 👤 SELF-ADMIN: profile, out-of-office, picture upload ---
    profilePictureUrl: (url) => url?.startsWith('/profiles/') ? `${apiClient.defaults.baseURL.replace(/\/$/, '')}${url}` : url,
    getProfile: async () => {
      try {
        const response = await apiClient.get('/user/profile');
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to load profile.'));
      }
    },
    updateProfile: async (updates) => {
      try {
        const response = await apiClient.put('/user/profile', updates);
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to update profile.'));
      }
    },
    uploadProfilePicture: async (file) => {
      try {
        const formData = new FormData();
        formData.append('file', file);
        const response = await apiClient.post('/user/profile/picture', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to upload profile picture.'));
      }
    },
    changePassword: async (currentPassword, newPassword) => {
      try {
        const response = await apiClient.post('/auth/change-password', { currentPassword, newPassword });
        return response.data;
      } catch (error) {
        throw new Error(toMessage(error.response?.data?.message, 'Failed to change password.'));
      }
    },

    resetPassword: async (token, newPassword, confirmPassword) => {
      try {
        const response = await apiClient.post('/auth/reset-password', { token, newPassword, confirmPassword });
        return response.data;
      } catch (error) {
        throw new Error(error.response?.data?.message || 'Unable to reset password. Please request a new link.');
      }
    },

    // --- 👤 LOCAL ACCOUNT STATUS CLEARING ---
    logout: () => {
     localStorage.removeItem('user_session_jwt');
     localStorage.removeItem('user_profile_data');
     localStorage.removeItem('active_tenant_override');
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
